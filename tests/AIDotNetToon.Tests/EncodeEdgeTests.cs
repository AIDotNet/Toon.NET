#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    public class EncodeEdgeTests
    {
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void Encode_String_WithCommaDelimiter_MustBeQuoted()
        {
            var obj = new { s = "a,b" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"a,b\"", toon);
        }

        [Fact]
        public void Encode_String_WithBackslash_And_Quote_MustEscape()
        {
            var obj = new { s = "a\\\"b" }; // actual text: a\"b
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"a\\\\\\\"b\"", toon);
        }

        [Fact]
        public void Encode_String_WithNewline_Tab_CarriageReturn_MustEscape()
        {
            var obj = new { s = "line1\nline2\t\r" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"line1\\nline2\\t\\r\"", toon);
        }

        [Fact]
        public void Encode_String_WithBrackets_Or_Braces_MustBeQuoted()
        {
            var obj = new { s = "[x]{y}" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"[x]{y}\"", toon);
        }

        [Fact]
        public void Encode_String_StartingWithHyphen_MustBeQuoted()
        {
            var obj = new { s = "-abc" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"-abc\"", toon);
        }

        [Fact]
        public void Encode_Key_WithSpaces_MustBeQuoted()
        {
            var obj = new Dictionary<string, object?> { ["full name"] = 1 };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("\"full name\": 1", toon);
        }

        [Fact]
        public void Encode_Key_StartingWithDigit_MustBeQuoted()
        {
            var obj = new Dictionary<string, object?> { ["9a"] = 2 };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("\"9a\": 2", toon);
        }

        [Fact]
        public void Encode_Key_WithDotAndUnderscore_IsUnquoted()
        {
            var obj = new Dictionary<string, object?> { ["A_b.c"] = 1 };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("A_b.c: 1", toon);
        }

        [Fact]
        public void Encode_ArrayOfObjects_NonUniform_FallsBack_ToListItems()
        {
            var rows = new object[]
            {
                new { a = 1 },
                new { b = 2 },
            };

            var toon = ToonSerializer.Serialize(rows, DefaultOpts());
            var expected = string.Join('\n', new[]
            {
                "[2]:",
                "  - a: 1",
                "  - b: 2",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_TabularArray_WithLengthMarker_AndFields()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Comma,
                strict: true,
                lengthMarker: '#',
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var rows = new[]
            {
                new { id = 1, name = "x" },
                new { id = 2, name = "y" },
            };

            var toon = ToonSerializer.Serialize(rows, opts);
            var expected = string.Join('\n', new[]
            {
                "[#2]{id,name}:",
                "  1,x",
                "  2,y",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_TabularArray_HeaderKeysOrderFromFirstRow_AcceptsDifferentOrderRows()
        {
            // second row different insertion order via Dictionary
            var row1 = new Dictionary<string, object> { ["id"] = 1, ["name"] = "a" };
            var row2 = new Dictionary<string, object> { ["name"] = "b", ["id"] = 2 };

            var rows = new[] { row1, row2 };
            var toon = ToonSerializer.Serialize(rows, DefaultOpts());

            var expected = string.Join('\n', new[]
            {
                "[2]{id,name}:",
                "  1,a",
                "  2,b",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_InlineArray_WithPipeDelimiter_HeaderShowsPipe_ValuesJoinedByPipe()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Pipe,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var arr = new[] { "a", "b" };
            var toon = ToonSerializer.Serialize(arr, opts);
            Assert.Equal("[2|]: a|b", toon);
        }

        [Fact]
        public void Encode_Number_RepeatingDecimal_TextContainsFullPrecision()
        {
            var obj = new { value = 1.0 / 3.0 };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Contains("0.3333333333333333", toon);
        }
    }
}