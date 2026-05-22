using System.ComponentModel.Design;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;


internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(string.Join(",", args));
            Console.WriteLine("Please provide the paths to format as a single argument, separated by newlines.");
            return;
        }

        string[] singleLinePaths = args.Length > 2
            ? [.. args[2]
                .Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(i => !string.IsNullOrEmpty(i) && !i.StartsWith("//"))]
            : [];
        int firstLineLen = args[2].Length > 2 ? args[2].IndexOfAny(['\n', '\r', ';']) : -1;
        string indent = args.Length > 2 && firstLineLen > 0 ? args[2][..firstLineLen] : string.Empty;
        if (string.IsNullOrEmpty(indent))
            indent = "\t";

        IJsonPath[][] splitPaths = [.. singleLinePaths.Select(SplitPath)];

        Dictionary<Type, IJsonPath[][]> groupedPaths = splitPaths.GroupBy(path => path.Last().GetType()).ToDictionary(g => g.Key, g => g.ToArray());

        ReadOnlySpan<byte> jsonData = File.ReadAllBytes(args[0]);
        Utf8JsonReader jsonReader = new(jsonData, new JsonReaderOptions { AllowTrailingCommas = true });
        using FileStream outputStream = new(args[1], FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(outputStream, leaveOpen: false);
        using Utf8JsonWriter jsonWriter = new(writer.BaseStream, new JsonWriterOptions { Indented = true, IndentCharacter = indent[0], IndentSize = indent.Length });
        Stack<IJsonPath> path = new();
        IJsonPath? currentSegment;
        bool isInArray = false;
        Utf8JsonReader arrayCheckPoint = jsonReader;
        while (jsonReader.Read())
        {
            var token = jsonReader.ValueSpan;
            isInArray = false;
            if (path.TryPeek(out currentSegment) && currentSegment is JsonArrayIndexSegment arraySegment1)
            {
                isInArray = true;
                if (arraySegment1.Index.HasValue)
                { path.Pop(); path.Push(new JsonArrayIndexSegment(arraySegment1.Index.Value + 1)); }
                else
                { path.Pop(); path.Push(new JsonArrayIndexSegment(0)); }
            }
            else
                arrayCheckPoint = jsonReader;
            switch (jsonReader.TokenType)
            {
                case JsonTokenType.String:
                    jsonWriter.WriteStringValue(token);
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.Number:
                    writer.Flush();
                    if (jsonReader.TryGetInt64(out long longValue))
                        jsonWriter.WriteNumberValue(longValue);
                    else if (jsonReader.TryGetDouble(out double doubleValue))
                        jsonWriter.WriteNumberValue(doubleValue);
                    else
                        throw new FormatException("Invalid number format.");
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.True:
                    jsonWriter.WriteBooleanValue(true);
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.False:
                    jsonWriter.WriteBooleanValue(false);
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.Null:
                    jsonWriter.WriteNullValue();
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    jsonWriter.WritePropertyName(token);
                    path.Push(new JsonPropertySegment(Encoding.UTF8.GetString(token)));
                    if (groupedPaths.TryGetValue(typeof(JsonPropertySegment), out var expectedPaths1) &&
                        expectedPaths1.Any(expectedPath => IsPathMatch(path.Reverse(), expectedPath)))
                    {
                        WriteElement(jsonReader, jsonWriter, false, true);
                        jsonReader.Skip();
                        if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    }
                    break;
                case JsonTokenType.StartObject:
                    path.Push(new JsonObjectSegment());
                    if (groupedPaths.TryGetValue(typeof(JsonObjectSegment), out var expectedPaths2) &&
                        expectedPaths2.Any(expectedPath => IsPathMatch(path.Reverse(), expectedPath)))
                    {
                        if (isInArray && path.Count > 1 && path.ElementAt(1) is JsonArrayIndexSegment jais && jais.Index == 0)
                        {
                            JsonElement[] arrayElements = JsonElement.ParseValue(ref arrayCheckPoint).EnumerateArray().ToArray();
                            if (!FormatJsonElement([.. arrayElements], jsonWriter, arrayCheckPoint.CurrentDepth, jsonWriter.Options.IndentCharacter, jsonWriter.Options.IndentSize))
                                goto object_else;
                            jsonReader = arrayCheckPoint;
                            path.Pop();
                            goto case JsonTokenType.EndArray;
                        }
                    object_else:
                        WriteElement(jsonReader, jsonWriter, path.Count > 1, true);
                        jsonWriter.Flush();
                        jsonReader.Skip();
                        if (path.TryPeek(out currentSegment) && currentSegment is JsonObjectSegment) path.Pop();
                        if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();

                    }
                    else
                    {
                        jsonWriter.WriteStartObject();
                    }
                    break;
                case JsonTokenType.StartArray:
                    path.Push(new JsonArrayIndexSegment(null));
                    if (groupedPaths.TryGetValue(typeof(JsonArrayIndexSegment), out var expectedPaths3) &&
                        expectedPaths3.Any(expectedPath => IsPathMatch(path.Reverse(), expectedPath)))
                    {
                        if (isInArray && path.Count > 1 && path.ElementAt(1) is JsonArrayIndexSegment jais && jais.Index == 0)
                        {
                            List<JsonElement> arrayElements = [];
                            while (arrayCheckPoint.Read() && arrayCheckPoint.TokenType != JsonTokenType.EndArray)
                            {
                                arrayElements.Add(JsonElement.ParseValue(ref arrayCheckPoint));
                            }
                            if (!FormatJsonArray([.. arrayElements], jsonWriter, arrayCheckPoint.CurrentDepth, jsonWriter.Options.IndentCharacter, jsonWriter.Options.IndentSize))
                                goto array_else;
                            jsonReader = arrayCheckPoint;
                            path.Pop();
                            goto case JsonTokenType.EndArray;
                        }
                    array_else:
                        WriteElement(jsonReader, jsonWriter, true, !isInArray);
                        jsonReader.Skip();
                        if (path.TryPeek(out currentSegment) && currentSegment is JsonArrayIndexSegment) path.Pop();
                        if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                        break;
                    }
                    else
                    {
                        jsonWriter.WriteStartArray();
                    }
                    break;
                case JsonTokenType.EndObject:
                    jsonWriter.WriteEndObject();
                    path.Pop();
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
                case JsonTokenType.EndArray:
                    jsonWriter.WriteEndArray();
                    path.Pop();
                    if (path.TryPeek(out currentSegment) && currentSegment is JsonPropertySegment) path.Pop();
                    break;
            }
        }
    }

    private static bool IsPathMatch(IEnumerable<IJsonPath> path, IEnumerable<IJsonPath> expectedPath)
    {
        if (path.Count() < expectedPath.Count())
            return false;
        for (int i = 0; i < expectedPath.Count(); i++)
        {
            bool match = (path.ElementAt(i), expectedPath.ElementAt(i)) switch
            {
                (JsonObjectSegment, JsonObjectSegment) => true,
                (JsonPropertySegment current, JsonPropertySegment expected) =>
                    expected.Name is null || current.Name == expected.Name,
                (JsonArrayIndexSegment current, JsonArrayIndexSegment expected) =>
                    expected.Index is null || current.Index == expected.Index,
                _ => false
            };
            if (!match)
                return false;
        }
        return true;
    }

    private static IJsonPath[] SplitPath(string path)
    {
        List<IJsonPath> segments = new();
        int i = 0;
        while (i < path.Length)
        {
            if (path[i] == '.')
            {
                segments.Add(new JsonObjectSegment());
                int start = ++i;
                while (i < path.Length && path[i] != '.' && path[i] != '[')
                    i++;
                string propertyName = path[start..i];
                if (propertyName == "*")
                    segments.Add(new JsonPropertySegment());
                else if (!string.IsNullOrEmpty(propertyName))
                    segments.Add(new JsonPropertySegment(propertyName));
            }
            else if (path[i] == '[')
            {
                int start = i + 1;
                while (i < path.Length && path[i] != ']')
                    i++;
                string indexPart = path[start..i];
                if (indexPart == "*")
                    segments.Add(new JsonArrayIndexSegment(null));
                else if (int.TryParse(indexPart, out int index))
                    segments.Add(new JsonArrayIndexSegment(index));
                else
                    throw new FormatException($"Invalid array index: {indexPart}");
                i++; // Skip the closing ']'
            }
            else
            {
                throw new FormatException($"Unexpected character at position {i}: {path[i]}");
            }
        }
        return [.. segments];
    }
    private static bool FormatJsonArray(JsonElement[] array, Utf8JsonWriter writer, int depth, char indentCharacter, int indentSize)
    {
        JsonElement[] elements = array;
        if (elements.Length == 0)
            return true;
        foreach (JsonElement e in elements)
            if (e.ValueKind != JsonValueKind.Array)
                return false;
        JsonValueKind kind = elements[0].ValueKind;
        for (int i = 1; i < elements.Length; i++)
        {
            if (elements[i].ValueKind != kind)
            { kind = JsonValueKind.Undefined; break; }
        }
        switch (kind)
        {
            case JsonValueKind.Array:
                {
                    (int[] maxLengths, string[][] plainValues) = GetJsonArrayMaxValueLengths(elements);
                    StringBuilder sb = new();

                    for (int i = 0; i < elements.Length; i++)
                    {
                        JsonElement element = elements[i];
                        sb.Append($"{Environment.NewLine}{new string(indentCharacter, indentSize * (depth + 1))}");
                        JsonElement[] subElements = element.EnumerateArray().ToArray();
                        if (subElements.Length == 0)
                        { sb.Append("[]"); goto write; }
                        sb.Append($"[{plainValues[i][0]}");
                        for (int j = 1; j < subElements.Length; j++)
                            sb.Append($",{new string(' ', maxLengths[j - 1] - (plainValues[i][j - 1]).Length)}{plainValues[i][j]}");
                        sb.Append(']');
                    write:
                        writer.WriteRawValue(sb.ToString());
                        sb.Clear();
                    }
                }
                break;
            default:
                {
                    StringBuilder sb = new();
                    sb.Append($"{Environment.NewLine}{new string(indentCharacter, indentSize * (depth + 1))}{elements[0]}");
                    for (int i = 1; i < elements.Length; i++)
                        sb.Append($",{Environment.NewLine}{new string(indentCharacter, indentSize * (depth + 1))}{elements[i]}");
                }
                break;
        }
        return true;
    }
    private static bool FormatJsonElement(JsonElement[] array, Utf8JsonWriter writer, int depth, char indentCharacter, int indentSize)
    {
        JsonElement[] elements = array;
        if (elements.Length == 0)
            return true;
        foreach (JsonElement e in elements)
            if (e.ValueKind != JsonValueKind.Object)
                return false;
        var res = GetJsonObjectMaxValueLengths(elements);
        KeyValuePair<string, int>[] lengths = res.Item1.ToArray();
        Dictionary<string, string>[] plainValues = res.Item2;
        StringBuilder sb = new();

        for (int i = 0; i < elements.Length; i++)
        {
            JsonElement element = elements[i];
            sb.Append($"{Environment.NewLine}{new string(indentCharacter, indentSize * (depth + 1))}");
            Dictionary<string, JsonElement> properties = element.EnumerateObject().ToDictionary(i => i.Name, i => i.Value);
            if (properties.Count == 0)
            { sb.Append("{}"); goto write; ; }
            JsonElement? previous = null;
            int space;
            bool hasPrevious = false;
            sb.Append('{');
            if (properties.TryGetValue(lengths[0].Key, out JsonElement subElement))
            {
                sb.Append($"\"{lengths[0].Key}\":{plainValues[i][lengths[0].Key]}");
                previous = subElement;
                space = lengths[0].Value - plainValues[i][lengths[0].Key].Length;
                hasPrevious = true;
            }
            else
                space = lengths[0].Key.Length + 3 + lengths[0].Value;
            for (int j = 1; j < lengths.Length; j++)
            {
                if (properties.TryGetValue(lengths[j].Key, out JsonElement subCurElement))
                {
                    sb.Append($"{(hasPrevious ? ',' : ' ')}{new string(' ', space)}\"{lengths[j].Key}\":{plainValues[i][lengths[j].Key]}");
                    previous = subCurElement;
                    space = lengths[j].Value - plainValues[i][lengths[j].Key].Length;
                    hasPrevious = true;
                }
                else
                {
                    space += lengths[j].Key.Length + 4 + lengths[j].Value;
                }
            }
            sb.Append('}');
        write:
            writer.WriteRawValue(sb.ToString());
            sb.Clear();
        }
        return true;
    }
    private static string GetPlainText(JsonElement element)
    {
        using MemoryStream tempStream = new();
        using StreamWriter tempStreamWriter = new(tempStream, leaveOpen: false);
        using Utf8JsonWriter tempWriter = new(tempStream, new JsonWriterOptions { Indented = false });
        element.WriteTo(tempWriter);
        tempWriter.Flush();
        return Encoding.UTF8.GetString(tempStream.ToArray());
    }
    private static (Dictionary<string, int>, Dictionary<string, string>[]) GetJsonObjectMaxValueLengths(JsonElement[] elements)
    {
        Dictionary<string, int> maxLengths = new();
        Dictionary<string, string>[] plainValues = new Dictionary<string, string>[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            JsonElement element = elements[i];
            plainValues[i] = new Dictionary<string, string>();
            foreach (var property in element.EnumerateObject())
            {
                string plainText = GetPlainText(property.Value);
                int length = plainText.Length;
                if (!maxLengths.ContainsKey(property.Name) || length > maxLengths[property.Name])
                    maxLengths[property.Name] = length;
                plainValues[i][property.Name] = plainText;
            }
        }
        return (maxLengths, plainValues);
    }
    private static (int[], string[][]) GetJsonArrayMaxValueLengths(JsonElement[] elements)
    {
        List<int> maxLengths = new();
        List<string>[] plainValues = new List<string>[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            JsonElement[] subElement = elements[i].EnumerateArray().ToArray();
            plainValues[i] = [];
            for (int j = 0; j < subElement.Length; j++)
            {
                JsonElement element = subElement[j];
                string plainText = GetPlainText(element);
                int length = plainText.Length;
                if (maxLengths.Count <= j)
                    maxLengths.Add(length);
                else if (length > maxLengths[j])
                    maxLengths[j] = length;
                plainValues[i].Add(plainText);
            }
        }
        return ([.. maxLengths], [.. plainValues.Select(pv => pv.ToArray())]);
    }

    private static string FormatPath(IEnumerable<IJsonPath> path)
    {
        StringBuilder sb = new();
        foreach (var segment in path)
        {
            switch (segment)
            {
                case JsonObjectSegment:
                    sb.Append('.');
                    break;
                case JsonPropertySegment propertySegment:
                    sb.Append(propertySegment.Name);
                    break;
                case JsonArrayIndexSegment arraySegment:
                    sb.Append(arraySegment.Index is int index ? $"[{index}]" : "[*]");
                    break;
            }
        }
        return sb.ToString();
    }

    private static void WriteElement(Utf8JsonReader reader, Utf8JsonWriter writer, bool newLine, bool indentOnce)
    {
        using MemoryStream tempStream = new();
        using StreamWriter tempStreamWriter = new(tempStream, leaveOpen: false);
        using Utf8JsonWriter tempWriter = new(tempStream, new JsonWriterOptions { Indented = false });
        JsonElement element = JsonElement.ParseValue(ref reader);
        switch (element.ValueKind)
        {
            //case JsonValueKind.Object:
            //    break;
            //case JsonValueKind.Array:
            //    break;
            default:
                element.WriteTo(tempWriter);
                break;
        }
        tempWriter.Flush();
        int depth = reader.CurrentDepth + (indentOnce ? 1 : 0);
        char indent = writer.Options.IndentCharacter;
        int indentSize = writer.Options.IndentSize;
        string indentString = newLine ? Environment.NewLine + new string(indent, indentSize * depth) : string.Empty;
        writer.WriteRawValue($"{indentString}{Encoding.UTF8.GetString(tempStream.ToArray())}");
    }
}

interface IJsonPath { }
// `.`
record struct JsonObjectSegment() : IJsonPath;
// `propertyName`
record struct JsonPropertySegment(string Name) : IJsonPath;
// `[*]` or `[index]`
record struct JsonArrayIndexSegment(int? Index) : IJsonPath;