#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    public class EncodeMoreTests
    {
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void Encode_EmptyArray_HeaderOnly()
        {
            var toon = ToonSerializer.Serialize(Array.Empty<int>(), DefaultOpts());
            Assert.Equal("[0]:", toon);
        }

        [Fact]
        public void Encode_ArrayOfArrays_AsListItems_WithInlinePrimitiveArrays()
        {
            var value = new[]
            {
                new[] { 1, 2 },
                new[] { 3, 4 },
            };
            var toon = ToonSerializer.Serialize(value, DefaultOpts());

            var expected = string.Join('\n', new[]
            {
                "[2]:",
                "  - [2]: 1,2",
                "  - [2]: 3,4",
            });

            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_MixedArray_FallsBack_ToListItems()
        {
            var value = new object?[]
            {
                1,
                new { a = 2 },
                new[] { 3, 4 },
            };

            var toon = ToonSerializer.Serialize(value, DefaultOpts());
            var expected = string.Join('\n', new[]
            {
                "[3]:",
                "  - 1",
                "  - a: 2",
                "  - [2]: 3,4",
            });

            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_InlineArray_WithTabDelimiter_HeaderContainsTab_ValuesJoinedByTab()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Tab,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var value = new[] { "a", "b" };
            var toon = ToonSerializer.Serialize(value, opts);

            var expected = "[2\t]: a\tb";
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_ObjectArray_TabularFormat_WithPipeDelimiter_IndentedRows()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Pipe,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var rows = new[]
            {
                new { id = 1, name = "a" },
                new { id = 2, name = "b" },
            };

            var toon = ToonSerializer.Serialize(rows, opts);
            var expected = string.Join('\n', new[]
            {
                "[2|]{id|name}:",
                "  1|a",
                "  2|b",
            });

            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_LengthMarker_HeaderUsesHash()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Comma,
                strict: true,
                lengthMarker: '#',
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var value = new[] { 1, 2 };
            var toon = ToonSerializer.Serialize(value, opts);
            Assert.Equal("[#2]: 1,2", toon);
        }

        [Fact]
        public void Encode_String_MustBeQuoted_WhenContainsActiveDelimiter()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Pipe,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var obj = new { s = "a|b" };
            var toon = ToonSerializer.Serialize(obj, opts);
            Assert.Equal("s: \"a|b\"", toon);
        }

        [Fact]
        public void Encode_Key_MustBeQuoted_WhenContainsSpaces()
        {
            var obj = new Dictionary<string, object?>
            {
                ["full name"] = "bob"
            };

            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("\"full name\": bob", toon);
        }

        [Fact]
        public void Encode_EmptyObject_Value_WritesKeyWithColonOnly()
        {
            var obj = new { x = new { } };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("x:", toon);
        }

        [Fact]
        public void Encode_ObjectListItem_PrimitiveArrayOnFirstKey_InlineWithKeyPrefix()
        {
            var value = new[]
            {
                new { nums = new[] { 1, 2 } }
            };

            var toon = ToonSerializer.Serialize(value, DefaultOpts());
            var expected = string.Join('\n', new[]
            {
                "[1]:",
                "  - nums[2]: 1,2",
            });

            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_DateTime_AsIsoString()
        {
            var dt = new DateTime(2024, 12, 31, 23, 59, 58, DateTimeKind.Utc);
            var obj = new { t = dt };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());

            // System.Text.Json 默认按 ISO-8601 写出 UTC DateTime
            Assert.StartsWith("t: \"2024-12-31T23:59:58", toon);
        }
    }
}