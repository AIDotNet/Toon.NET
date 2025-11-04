#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    public class EncodeAdditionalEdgeTests
    {
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void Encode_TabularArray_WithPipeDelimiter_And_LengthMarker_HeaderShowsBoth()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Pipe,
                strict: true,
                lengthMarker: '#',
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            var rows = new[]
            {
                new { id = 1, name = "a" },
                new { id = 2, name = "b" },
            };

            var toon = ToonSerializer.Serialize(rows, opts);
            var expected = string.Join('\n', new[]
            {
                "[#2|]{id|name}:",
                "  1|a",
                "  2|b",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_String_NumericLike_LeadingZero_MustBeQuoted()
        {
            var obj = new { s = "05" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("s: \"05\"", toon);
        }

        [Fact]
        public void Encode_ListItems_EmptyObject_PrintsHyphenLine()
        {
            var arr = new object?[]
            {
                new { }, // empty object
                new { }, // empty object
            };

            var toon = ToonSerializer.Serialize(arr, DefaultOpts());
            var expected = string.Join('\n', new[]
            {
                "[2]:",
                "  -",
                "  -",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_MixedArray_ListItem_ObjectWithNestedStructure_And_SiblingKey()
        {
            var value = new object?[]
            {
                new { a = new { b = 1, c = 2 }, d = 3 },
            };

            var toon = ToonSerializer.Serialize(value, DefaultOpts());
            var expected = string.Join('\n', new[]
            {
                "[1]:",
                "  - a:",
                "      b: 1",
                "      c: 2",
                "    d: 3",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_ObjectArray_Tabular_TabDelimiter()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Tab,
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
                "[2\t]{id\tname}:",
                "  1\ta",
                "  2\tb",
            });
            Assert.Equal(expected, toon);
        }
    }
}