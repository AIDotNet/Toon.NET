# AIDotNet.Toon

.NET implementation of Token-Oriented Object Notation (TOON), aligned with the https://github.com/toon-format/toon specification, providing a consistent API experience and options model similar to System.Text.Json.

- High-performance encoding: Object, inline atomic arrays, tabular object arrays, and other paths are implemented
- Decoding pipeline: Scanning/parsing/validation is in progress, currently supports atomic value reading
- Design follows "minimal allocation, readability first" engineering trade-offs

[中文文档](README.zh-CN.md)

[C#.ToonSerializer](src/AIDotNet.Toon/ToonSerializer.cs:13) · [C#.ToonSerializerOptions](src/AIDotNet.Toon/ToonSerializerOptions.cs:28) · [C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20) · [C#.Primitives.EncodePrimitive()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:32) · [C#.LineWriter](src/AIDotNet.Toon/Internal/Encode/Writer.cs:14)


## Badges

- NuGet: AIDotNet.Toon
- Target Frameworks: net8.0 / net9.0 / net10.0 (see [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj))
- License: MIT


## Table of Contents

- [AIDotNet.Toon](#aidotnettoon)
  - [Badges](#badges)
  - [Table of Contents](#table-of-contents)
  - [Installation](#installation)
  - [Quick Start](#quick-start)
  - [API & Options](#api--options)
  - [Encoding Rules & Format](#encoding-rules--format)
  - [Example Snippets](#example-snippets)
  - [Performance & Implementation Details](#performance--implementation-details)
  - [Alignment with TypeScript Specification](#alignment-with-typescript-specification)
  - [Version Support](#version-support)
  - [Roadmap](#roadmap)
  - [Development & Testing](#development--testing)
  - [Contributing](#contributing)
  - [License](#license)


## Installation

NuGet:

```bash
dotnet add package AIDotNet.Toon
```

Source code method:
- Add the `src/AIDotNet.Toon` directory to your solution, or include it as a submodule
- Enable package Readme in your csproj (already configured in this project): see [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj)


## Quick Start

Serialize to TOON:

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

Deserialize from TOON to .NET (currently only atomic values are stable, other structures will improve as the decoder is enhanced):

```csharp
using Toon;

var s = ToonSerializer.Deserialize<string>("hello", options);   // "hello"
var n = ToonSerializer.Deserialize<double>("3.1415", options);  // 3.1415
```

Related APIs: [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:20) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:39)


## API & Options

Public API:
- Generic serialization: [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:20)
- Explicit type serialization: [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:28)
- Generic deserialization: [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:39)
- Explicit type deserialization: [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:50)
- Byte APIs:
  - Encode to UTF-8: [C#.ToonSerializer.SerializeToUtf8Bytes()](src/AIDotNet.Toon/ToonSerializer.cs:63) · [C#.ToonSerializer.SerializeToUtf8Bytes()](src/AIDotNet.Toon/ToonSerializer.cs:70)
  - Decode from UTF-8: [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:77) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:81) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:88) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:92)
- Stream APIs:
  - Write to stream: [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:101) · [C#.ToonSerializer.Serialize()](src/AIDotNet.Toon/ToonSerializer.cs:110)
  - Decode from stream: [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:119) · [C#.ToonSerializer.Deserialize()](src/AIDotNet.Toon/ToonSerializer.cs:127)

Options model: [C#.ToonSerializerOptions](src/AIDotNet.Toon/ToonSerializerOptions.cs:28)
- Indent: Number of spaces per indentation level, default 2
- Delimiter: Delimiter, enumeration [C#.ToonDelimiter](src/AIDotNet.Toon/ToonSerializerOptions.cs:10) (Comma / Tab / Pipe)
- Strict: Strict mode for decoding (indentation/blank lines/extra items validation), default true
- LengthMarker: Array length marker, only supports `#` or null, default null
- JsonOptions: Pass-through to System.Text.Json's [C#.JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions), defaults to enabling named float literals and registers converters that write `NaN/±Infinity` as `null`: [C#.DoubleNamedFloatToNullConverter](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs:13) / [C#.SingleNamedFloatToNullConverter](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs:32)

Default instance: [C#.ToonSerializerOptions.Default](src/AIDotNet.Toon/ToonSerializerOptions.cs:100)


## Encoding Rules & Format

Entry point: [C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20)

Objects:
- Key-by-key output: [C#.Encoders.EncodeObject()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:45) and [C#.Encoders.EncodeKeyValuePair()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:53)
- Key names: Write bare if safe, otherwise quote: [C#.Primitives.EncodeKey()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:101)
- Empty objects: Only output `key:`

Atomic arrays (inline):
- Header: [C#.Primitives.FormatHeader()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:142)
- Inline concatenation: [C#.Encoders.EncodeInlineArrayLine()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:155)

Object arrays (tabular):
- Extract table header: [C#.Encoders.ExtractTabularHeader()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:181)
- Header and data row output: [C#.Encoders.EncodeArrayOfObjectsAsTabular()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:173) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)

Mixed/complex arrays (fallback to list):
- [C#.Encoders.EncodeMixedArrayAsListItems()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:253)
- List item writer: [C#.LineWriter.PushListItem()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:49)

Atoms/strings:
- Atomic encoding: [C#.Primitives.EncodePrimitive()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:32)
- String quoting rules: [C#.Primitives.EncodeStringLiteral()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:88)
- Number rules: `NaN/±Infinity -> null`, `-0 -> 0`, others use [C#.JsonElement.GetRawText()](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement.getrawtext)

Lines and indentation:
- Writer: [C#.LineWriter](src/AIDotNet.Toon/Internal/Encode/Writer.cs:14)
- Regular line: [C#.LineWriter.Push()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:35)
- List item line: [C#.LineWriter.PushListItem()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:49)


## Example Snippets

Simple object:

```csharp
var obj = new { a = 1, b = "x" };
var toon = ToonSerializer.Serialize(obj);
// a: 1
// b: x
```

Atomic array (default comma-separated):

```csharp
var arr = new[] { 1, 2, 3 };
ToonSerializer.Serialize(arr); // "[3]: 1,2,3"
```

Object array (tabular):

```csharp
var rows = new[] { new { id = 1, name = "alice" }, new { id = 2, name = "bob" } };
ToonSerializer.Serialize(rows);
// [2]{id,name}:
//   1,alice
//   2,bob
```

Bytes and streams:

```csharp
// Bytes
var bytes = ToonSerializer.SerializeToUtf8Bytes(rows);
var rowsFromBytes = ToonSerializer.Deserialize<List<Dictionary<string, object>>>(bytes);

// Stream
using var ms = new MemoryStream();
ToonSerializer.Serialize(rows, ms);   // Write to stream as UTF-8 (no BOM), keeping stream open
ms.Position = 0;
var rowsFromStream = ToonSerializer.Deserialize<List<Dictionary<string, object>>>(ms);
```

Special number handling:

```csharp
ToonSerializer.Serialize(new { v = double.NaN });            // "v: null"
ToonSerializer.Serialize(new { v = double.PositiveInfinity }); // "v: null"
ToonSerializer.Serialize(new { v = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000)) }); // "v: 0"
```

Related assertions can be seen in tests: [tests/AIDotNet.Toon.Tests/EncodeTests.cs](tests/AIDotNet.Toon.Tests/EncodeTests.cs)


## Performance & Implementation Details

This implementation minimizes allocations and unnecessary branches while maintaining output readability:

- Indentation cache: [C#.LineWriter.Push()](src/AIDotNet.Toon/Internal/Encode/Writer.cs:35) maintains `_indentCache` to avoid repeated construction
- No LINQ: Table header detection/data row writing paths use plain enumerator traversal: [C#.Encoders.ExtractTabularHeader()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:181) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)
- Reuse concatenation: Atomic arrays and table rows use segment-by-segment Append: [C#.Primitives.EncodeAndJoinPrimitives()](src/AIDotNet.Toon/Internal/Encode/Primitives.cs:113) · [C#.Encoders.WriteTabularRows()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:225)
- Raw numbers: Prioritize [C#.JsonElement.GetRawText()](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement.getrawtext) to ensure precision; fast path normalizes `-0`

Publishing recommendations: Use Release build; consider R2R/ReadyToRun to improve startup performance.


## Alignment with TypeScript Specification

- Syntax/rendering rules reference the upstream specification and implementation:
  - Specification and reference implementation: https://github.com/toon-format/toon
- .NET version module mapping:
  - Encoder: [C#.ToonEncoder.Encode()](src/AIDotNet.Toon/ToonEncoder.cs:13) -> [C#.Encoders.EncodeValue()](src/AIDotNet.Toon/Internal/Encode/Encoders.cs:20)
  - Decoder: [C#.ToonDecoder.DecodeToJsonString()](src/AIDotNet.Toon/ToonDecoder.cs:15) (placeholder, work in progress)


## Version Support

- Runtime: .NET 8/9/10
- Platforms: Windows / Linux / macOS
- Package metadata and Readme integration: see [src/AIDotNet.Toon/AIDotNet.Toon.csproj](src/AIDotNet.Toon/AIDotNet.Toon.csproj)


## Roadmap

- Decoding: Scanner / Parser / Validation / Decoders
- Strict mode error model: Provide row/column and context-aware [C#.ToonFormatException](src/AIDotNet.Toon/ToonFormatException.cs:10)
- Normalization strategy: Cross-language consistency for dates/collections, etc.
- Documentation and example improvements, publish NuGet package


## Development & Testing

- Run tests:

```bash
dotnet test
```

- Local packaging:

```bash
dotnet pack -c Release
```

- Important source files:
  - [src/AIDotNet.Toon/Internal/Encode/Encoders.cs](src/AIDotNet.Toon/Internal/Encode/Encoders.cs)
  - [src/AIDotNet.Toon/Internal/Encode/Primitives.cs](src/AIDotNet.Toon/Internal/Encode/Primitives.cs)
  - [src/AIDotNet.Toon/Internal/Encode/Writer.cs](src/AIDotNet.Toon/Internal/Encode/Writer.cs)
  - [src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs](src/AIDotNet.Toon/Internal/Converters/NamedFloatToNullConverters.cs)
  - [src/AIDotNet.Toon/ToonSerializer.cs](src/AIDotNet.Toon/ToonSerializer.cs)


## Contributing

Issues and PRs are welcome. Please try to:
- Keep the public API consistent with the System.Text.Json style
- Prioritize readability/real benefits when optimizing
- Add unit tests for new paths and edge conditions


## License

MIT © AIDotNet.Toon Contributors


Acknowledgments: Thanks to the upstream project https://github.com/toon-format/toon for design and implementation reference.
