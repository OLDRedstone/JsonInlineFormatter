# JsonInlineFormatter

[ English | [中文](README.zh-cn.md) ]

A tool for quickly mixed-formatting JSON.\
It can be used to format certain JSON fields into a single line or multiple lines to improve readability and maintainability.\
Field alignment has been added for arrays of arrays and arrays of objects.

## Usage
```bash
jsonInlineFormatter <input_file> <output_file> <format>
```

`<input_file>`: Input JSON file path
`<output_file>`: Output JSON file path
`<format>`: Specify the fields to be formatted\
- Single match rule:   
	- `.` represents an object node
	- `property` represents a property of an object node
	- `*` represents any property of an object node
	- `[0]` represents the first element of an array node
	- `[*]` represents all elements of an array node
	- `//` added as prefix disables the parsing of this rule (for debugging)
	> This syntax is different from JSONPath or JMESPath.
- Multiple match rules: \
	Use `;` or a line break to separate multiple matching rules, for example: `.property1;property2[*].subProperty`
  Shorter rules will take precedence for matching, for example: `.` will match before `.property1`.
- Indentation:\
  When using multiple matching rules, use whitespace characters at the beginning of the matching rule and separate them to indicate the indent character and amount,\
  for example: `  ;.property1;property2[*].subProperty` indicates using two spaces for indentation in multi-line mode.
> You can use `.` to directly compact the entire JSON file, or provide no rules to keep the default JSON formatting.

## Example
```json
{"name":"Alice","age":30,"email":"alice@example.com","aliases":["Ally","Alicia"],"roles":["developer","admin"],"active":true,"score":{"SUB_A":98.5,"SUB_B":88.0,"SUB_C":95.5},"profile":{"bio":"Software developer with 10 years of experience.","interests":["coding","hiking","cooking"],"social":{"twitter":"@alice","linkedin":"alice-chen"}},"actions":[{"type":"login","ip":"203.0.113.1","timestamp":"2026-05-21T09:00:00Z"},{"type":"deploy","timestamp":"2026-05-21T14:30:00Z"},{"type":"meeting","timestamp":"2026-05-21T16:00:00Z","participants":["Bob","Carol"]},{"type":"logout","timestamp":"2026-05-21T18:00:00Z","ip":"203.0.113.1"}],"skillsMatrix":[["js","python","go"],["mysql","mongodb"],["aws","gcp"]],"projects":[{"id":1,"name":"API GW","status":"active","team":["Bob","Carol","Dave"],"milestones":[{"phase":"design","complete":true},{"phase":"dev","complete":false},{"phase":"test","complete":false}],"dependencies":[2,3]},{"id":2,"name":"Pipeline","status":"done","team":["Alice"],"milestones":[],"dependencies":[]}]}
```

```powershell
jsonInlineFormatter example.json out.json "  ;
.aliases
.roles[*]
.score
.profile.
.actions[*].
.skillsMatrix[*][*]
.projects[*].*[*]"
```
Example Output:
```json
{
  "name": "Alice",
  "age": 30,
  "email": "alice@example.com",
  "aliases": ["Ally","Alicia"],
  "roles": 
    ["developer","admin"],
  "active": true,
  "score": {"SUB_A":98.5,"SUB_B":88.0,"SUB_C":95.5},
  "profile": 
    {"bio":"Software developer with 10 years of experience.","interests":["coding","hiking","cooking"],"social":{"twitter":"@alice","linkedin":"alice-chen"}},
  "actions": [
    {"type":"login",  "ip":"203.0.113.1","timestamp":"2026-05-21T09:00:00Z"},
    {"type":"deploy",                    "timestamp":"2026-05-21T14:30:00Z"},
    {"type":"meeting",                   "timestamp":"2026-05-21T16:00:00Z","participants":["Bob","Carol"]},
    {"type":"logout", "ip":"203.0.113.1","timestamp":"2026-05-21T18:00:00Z"}
  ],
  "skillsMatrix": [
    ["js",   "python", "go"],
    ["mysql","mongodb"],
    ["aws",  "gcp"]
  ],
  "projects": [
    {
      "id": 1,
      "name": "API GW",
      "status": "active",
      "team": 
        ["Bob","Carol","Dave"],
      "milestones": 
        [{"phase":"design","complete":true},{"phase":"dev","complete":false},{"phase":"test","complete":false}],
      "dependencies": 
        [2,3]
    },
    {
      "id": 2,
      "name": "Pipeline",
      "status": "done",
      "team": 
        ["Alice"],
      "milestones": 
        [],
      "dependencies": 
        []
    }
  ]
}
```

In this example,
- The `aliases` and `profile` fields are on the same line as the field name.
- The `roles` and `score` fields start on a new line.
- Each identical sub-field of the objects under the `actions` field is aligned in the same column.
- The array elements under the `skillsMatrix` field are aligned in the same column by index.
- Each array element field of the object array under the `projects` field is inline compressed into a single line.
- The other parts are formatted using the default JSON format, indented by two spaces.