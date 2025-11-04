#nullable enable
using System;
using System.Text.Json;
using Xunit;

namespace Toon.Tests
{
    /// <summary>
    /// 解码与严格模式相关的覆盖项占位测试。
    /// 由于当前解码器 [C#.ToonDecoder.DecodeToJsonString](src/Toon.NET/ToonSerializer.cs:98) 仍为占位实现，这些测试临时跳过。
    /// 待实现 Scanner/Parser/Validation/Decoders 后，去掉 Skip 并补充断言。
    /// </summary>
    public class DecodePendingTests
    {
        private static ToonSerializerOptions StrictOpts() => new ToonSerializerOptions(
            indent: 2,
            delimiter: ToonDelimiter.Comma,
            strict: true,
            lengthMarker: null,
            jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions)
        );

        [Fact]
        public void Decode_Strict_ListArray_BlankLinesNotAllowed()
        {
            var toon = string.Join('\n', new[]
            {
                "[3]:",
                "  - 1",
                "  ",          // blank line (should fail in strict mode)
                "  - 2",
                "  - 3",
            });

            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_ListArray_NoExtraItems()
        {
            var toon = string.Join('\n', new[]
            {
                "[2]:",
                "  - 1",
                "  - 2",
                "  - 3", // extra (should fail)
            });

            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_Tabular_NoExtraRows()
        {
            var toon = string.Join('\n', new[]
            {
                "[2]{id,name}:",
                "  1,alice",
                "  2,bob",
                "  3,charlie", // extra (should fail)
            });

            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_Indentation_NoTabsAllowed()
        {
            var toon = "[1]:\n\t- 1"; // TAB indentation
            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_Indentation_MustBeMultipleOfIndent()
        {
            // indent=2，但此处为1个空格，非整倍数
            var toon = "[1]:\n - 1";
            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_InlinePrimitiveArray_CountMustMatch()
        {
            var toon = "nums[3]: 1,2"; // fewer values than header
            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_Strict_Tabular_FieldCountMustMatch()
        {
            var toon = string.Join('\n', new[]
            {
                "[2]{id,name}:",
                "  1,alice",
                "  2", // field missing
            });

            Assert.ThrowsAny<Exception>(() => ToonSerializer.Deserialize<object>(toon, StrictOpts()));
        }

        [Fact]
        public void Decode_String_Unescape_And_QuotedKey()
        {
            var toon = "\"full name\": \"a\\n\\t\\r\\\"b\"";
            var obj = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            // 期望：obj.GetProperty("full name").GetString() == "a\n\t\r\"b"
        }
    }
}