# AIDotNet.Toon

Token-Oriented Object Notation（TOON）的 .NET 实现，对齐 https://github.com/toon-format/toon 规范，提供与 System.Text.Json 一致的 API 体验与选项模型。

- 高性能编码：对象、原子数组行内、对象数组表格等路径已实现
- 解码管线：扫描/解析/验证处于进行中，当前支持原子值回读
- 设计遵循“最少分配、可读优先”的工程取舍

[C#.ToonSerializer](src/AIDotNet.Toon/ToonSerializer.cs:13) · [C#.ToonSerializerOptions](src/AIDotNet.Toon/ToonSerializerOptions.cs:28) · [C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20) · [C#.Primitives.EncodePrimitive()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:32) · [C#.LineWriter](src/AIDotNet.Toon/Internal/Encode/Writer.cs:14)


## 徽章

- NuGet: AIDotNet.Toon
- 目标框架：net8.0 / net9.0 / net10.0（见 [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj)）
- 许可证：MIT


## 目录

- [AIDotNet.Toon](#aidotnettoon)
  - [徽章](#徽章)
  - [目录](#目录)
  - [安装](#安装)
  - [快速开始](#快速开始)
  - [API 与选项](#api-与选项)
  - [编码规则与格式](#编码规则与格式)
  - [示例片段](#示例片段)
  - [性能与实现细节](#性能与实现细节)
  - [对齐 TypeScript 规范](#对齐-typescript-规范)
  - [版本支持](#版本支持)
  - [路线图](#路线图)
  - [开发与测试](#开发与测试)
  - [贡献](#贡献)
  - [许可](#许可)


## 安装

NuGet：

```bash
dotnet add package AIDotNet.Toon
```

源代码方式：
- 将目录 `src/AIDotNet.Toon` 添加到解决方案，或以子模块方式引入
- 在你的 csproj 中开启包 Readme（已在本项目配置）：参阅 [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj)


## 快速开始

序列化为 TOON：

```csharp
using Toon;

var options = new ToonSerializerOptions
{
    Indent = 2,
    Delimiter = ToonDelimiter.Comma,
    Strict = true,
    LengthMarker = null
};

var data = new
{
    users = new[]
    {
        new { name = "alice", age = 30 },
        new { name = "bob", age = 25 }
    },
    tags = new[] { "a", "b", "c" },
    numbers = new[] { 1, 2, 3 }
};

string toonText = ToonSerializer.Serialize(data, options);
// users[2]{name,age}:
//   1,alice
//   2,bob
// tags[3]: a,b,c
// numbers[3]: 1,2,3
```

从 TOON 反序列化为 .NET（当前仅原子值稳定，其它结构将随解码器完善）：

```csharp
using Toon;

var s = ToonSerializer.Deserialize<string>("hello", options);   // "hello"
var n = ToonSerializer.Deserialize<double>("3.1415", options);  // 3.1415
```

相关 API： [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:20) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:39)


## API 与选项

公共 API：
- 泛型序列化：[C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:20)
- 显式类型序列化：[C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:28)
- 泛型反序列化：[C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:39)
- 显式类型反序列化：[C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:50)
- 字节 API：
  - 编码为 UTF-8：[C#.ToonSerializer.SerializeToUtf8Bytes()](src/AIDotNet.Toon/ToonSerializer.cs:63) · [C#.ToonSerializer.SerializeToUtf8Bytes()](src/AIDotNet.Toon/ToonSerializer.cs:70)
  - 从 UTF-8 解码：[C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:77) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:81) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:88) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:92)
- 流 API：
  - 写入流：[C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:101) · [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:110)
  - 从流解码：[C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:119) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:127)

选项模型：[C#.ToonSerializerOptions](src/AIDotNet.Toon/ToonSerializerOptions.cs:28)
- Indent：每级缩进空格数，默认 2
- Delimiter：分隔符，枚举 [C#.ToonDelimiter](src/AIDotNet.Toon/ToonSerializerOptions.cs:10)（Comma / Tab / Pipe）
- Strict：解码严格模式（缩进/空行/多余项等校验），默认 true
- LengthMarker：数组长度标记，仅支持 `#` 或 null，默认 null
- JsonOptions：直通 System.Text.Json 的 [C#.JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)，默认启用命名浮点字面量，并注册将 `NaN/±Infinity` 写出为 `null` 的转换器： [C#.DoubleNamedFloatToNullConverter](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs:13) / [C#.SingleNamedFloatToNullConverter](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs:32)

默认实例：[C#.ToonSerializerOptions.Default](src/AIDotNet.Toon/ToonSerializerOptions.cs:100)


## 编码规则与格式

入口：[C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20)

对象：
- 逐键输出：[C#.Encoders.EncodeObject()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:45) 与 [C#.Encoders.EncodeKeyValuePair()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:53)
- 键名：安全则裸写，否则加引号：[C#.Primitives.EncodeKey()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:101)
- 空对象：仅输出 `key:`

原子数组（行内）：
- 头部：[C#.Primitives.FormatHeader()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:142)
- 行内拼接：[C#.Encoders.EncodeInlineArrayLine()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:155)

对象数组（表格）：
- 抽取表头：[C#.Encoders.ExtractTabularHeader()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:181)
- 表头与数据行输出：[C#.Encoders.EncodeArrayOfObjectsAsTabular()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:173) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)

混合/复杂数组（回退为列表）：
- [C#.Encoders.EncodeMixedArrayAsListItems()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:253)
- 列表项写入器：[C#.LineWriter.PushListItem()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:49)

原子/字符串：
- 原子编码：[C#.Primitives.EncodePrimitive()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:32)
- 字符串加引号规则：[C#.Primitives.EncodeStringLiteral()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:88)
- 数值规则：`NaN/±Infinity -> null`、`-0 -> 0`、其余沿用 [C#.JsonElement.GetRawText()](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement.getrawtext)

行与缩进：
- 写入器：[C#.LineWriter](src/AIDotNet.Toon/Internal/Encode/Writer.cs:14)
- 普通行：[C#.LineWriter.Push()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:35)
- 列表项行：[C#.LineWriter.PushListItem()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:49)


## 示例片段

简单对象：

```csharp
var obj = new { a = 1, b = "x" };
var toon = ToonSerializer.Serialize(obj);
// a: 1
// b: x
```

原子数组（默认逗号分隔）：

```csharp
var arr = new[] { 1, 2, 3 };
ToonSerializer.Serialize(arr); // "[3]: 1,2,3"
```

对象数组（表格）：

```csharp
var rows = new[] { new { id = 1, name = "alice" }, new { id = 2, name = "bob" } };
ToonSerializer.Serialize(rows);
// [2]{id,name}:
//   1,alice
//   2,bob
```

字节与流：

```csharp
// 字节
var bytes = ToonSerializer.SerializeToUtf8Bytes(rows);
var rowsFromBytes = ToonSerializer.Deserialize<List<Dictionary<string, object>>>(bytes);

// 流
using var ms = new MemoryStream();
ToonSerializer.Serialize(rows, ms);   // 写入 UTF-8（无 BOM），保持流打开
ms.Position = 0;
var rowsFromStream = ToonSerializer.Deserialize<List<Dictionary<string, object>>>(ms);
```

特殊数值处理：

```csharp
ToonSerializer.Serialize(new { v = double.NaN });            // "v: null"
ToonSerializer.Serialize(new { v = double.PositiveInfinity }); // "v: null"
ToonSerializer.Serialize(new { v = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000)) }); // "v: 0"
```

相关断言可见测试：[tests/AIDotNet.Toon.Tests/EncodeTests.cs](tests/AIDotNet.Toon.Tests/EncodeTests.cs)


## 性能与实现细节

本实现尽量减少分配与不必要的分支，同时保持输出可读性：

- 缩进缓存： [C#.LineWriter.Push()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:35) 维护 `_indentCache`，避免重复构造
- 去 LINQ：表头检测/数据行写出等路径使用普通枚举器遍历：[C#.Encoders.ExtractTabularHeader()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:181) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)
- 复用拼接：原子数组与表格行采用逐段 Append：[C#.Primitives.EncodeAndJoinPrimitives()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:113) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)
- 原文数值：优先使用 [C#.JsonElement.GetRawText()](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement.getrawtext) 保证精度；快速路径规范化 `-0`

发布建议：使用 Release 构建；可考虑 R2R/ReadyToRun 提升启动性能。


## 对齐 TypeScript 规范

- 语法/渲染规则参考 upstream 规范与实现：
  - 规范与参考实现：https://github.com/toon-format/toon
- .NET 版模块映射：
  - Encoder： [C#.ToonEncoder.Encode()](src/AIDotNet.Toon/ToonEncoder.cs:13) -> [C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20)
  - Decoder： [C#.ToonDecoder.DecodeToJsonString()](src/AIDotNet.Toon/ToonDecoder.cs:15)（占位，逐步完善）


## 版本支持

- 运行时：.NET 8/9/10
- 平台：Windows / Linux / macOS
- 包元数据与 Readme 集成：见 [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj)


## 路线图

- 解码：扫描（Scanner）/解析（Parser）/校验（Validation）/解码器（Decoders）
- 严格模式错误模型：提供行列与上下文的 [C#.ToonFormatException](src/AIDotNet.Toon/ToonFormatException.cs:10)
- 归一化策略（Normalize）：日期/集合等的跨语言一致性
- 文档与示例完善，发布 NuGet 包


## 开发与测试

- 运行测试：

```bash
dotnet test
```

- 本地打包：

```bash
dotnet pack -c Release
```

- 重要源文件：
  - [src/AIDotNet.Toon/Internal/Encode/Encoders.cs](src/AIDotNet.Toon/Internal/Encode/Encoders.cs)
  - [src/AIDotNet.Toon/Internal/Encode/Primitives.cs](src/AIDotNet.Toon/Internal/Encode/Primitives.cs)
  - [src/AIDotNet.Toon/Internal/Encode/Writer.cs](src/AIDotNet.Toon/Internal/Encode/Writer.cs)
  - [src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs)
  - [src/AIDotNet.Toon/ToonSerializer.cs](src/AIDotNet.Toon/ToonSerializer.cs)


## 贡献

欢迎提交 Issue/PR。请尽量：
- 保持公共 API 与 System.Text.Json 风格一致
- 在优化时以可读性/真实收益为先
- 为新增路径与边界条件补充单元测试


## 许可

MIT © AIDotNet.Toon Contributors


致谢：感谢 upstream 项目 https://github.com/toon-format/toon 的设计与实现参考。
