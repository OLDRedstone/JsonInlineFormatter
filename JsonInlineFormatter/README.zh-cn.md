# JsonInlineFormatter

[ [English](README.md) | 中文 ]

一个用于快速混合格式化 JSON 的工具。\
可用于将 JSON 中的某些字段格式化为单行或多行，以提高可读性和维护性。\
针对数组数组和对象数组，增加了字段对齐。

## 使用方式
```bash
jsonInlineFormatter <input_file> <output_file> <format>
```

`<input_file>`: 输入 JSON 文件路径
`<output_file>`: 输出 JSON 文件路径
`<format>`: 指定需要格式化的字段\
- 单个匹配规则:   
	- `.` 表示对象节点
	- `property` 表示对象节点的属性
	- `*` 表示对象节点的任何属性
	- `[0]` 表示数组节点的第一个元素
	- `[*]` 表示数组节点的所有元素
	- `//` 添加前缀表示禁止解析此规则（用于调试）
	> 此语法不同于 JSONPath 或 JMESPath.
- 多个匹配规则: \
	使用 `;` 或换行分隔多个匹配规则，例如：`.property1;property2[*].subProperty`
  将优先使用短规则进行匹配，例如：`.` 将优先于 `.property1` 进行匹配。
- 缩进：\
  在多个匹配规则时，在匹配规则开头使用空白字符并分隔标记缩进字符和数量，\
  例如：`  ;.property1;property2[*].subProperty` 表示在多行模式时使用两个空格进行缩进。
> 可以使用 `.` 来直接压缩整个 JSON 文件，或是不给予规则来保持默认的 JSON 格式化方式。

## 示例
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
示例输出：
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

在这个示例中，
- `aliases` 和 `profile` 字段与字段名在同一行。
- `roles`、`score` 字段另起一行。
- `actions` 字段下的对象的每个相同子字段对齐在同一列。
- `skillsMatrix` 字段下的数组元素按索引对齐在同一列。
- `projects` 字段下的对象数组的每个数组元素字段都被压为一行。
- 其他部分按照默认的 JSON 格式化方式输出，且按两个空格缩进。