#nullable enable
using System;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    public class EncodeTests
    {
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void Encode_Object_SimpleKeyValues()
        {
            var obj = new { a = 1, b = "x" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            var expected = "a: 1\nb: x";
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_PrimitiveArray_InlineFormat_WithDefaultCommaDelimiter()
        {
            var arr = new[] { 1, 2, 3 };
            var toon = ToonSerializer.Serialize(arr, DefaultOpts());
            var expected = "[3]: 1,2,3";
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_ObjectArray_TabularFormat_CommaDelimiter()
        {
            var rows = new[]
            {
                new { id = 1, name = "alice" },
                new { id = 2, name = "bob" },
            };
            var toon = ToonSerializer.Serialize(rows, DefaultOpts());

            // Header with fields, then 2 data rows
            var expected = string.Join('\n', new[]
            {
                "[2]{id,name}:",
                "  1,alice",
                "  2,bob",
            });
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_String_RequiresQuotes_WhenContainsColonOrSpaces()
        {
            var obj = new { s = "has colon: inside" };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            var expected = "s: \"has colon: inside\"";
            Assert.Equal(expected, toon);
        }

        [Fact]
        public void Encode_Number_NaN_And_Infinity_Become_Null()
        {
            var obj1 = new { v = double.NaN };
            var t1 = ToonSerializer.Serialize(obj1, DefaultOpts());
            Assert.Equal("v: null", t1);

            var obj2 = new { v = double.PositiveInfinity };
            var t2 = ToonSerializer.Serialize(obj2, DefaultOpts());
            Assert.Equal("v: null", t2);

            var obj3 = new { v = double.NegativeInfinity };
            var t3 = ToonSerializer.Serialize(obj3, DefaultOpts());
            Assert.Equal("v: null", t3);
        }

        [Fact]
        public void Encode_Number_MinusZero_Canonicalized_To_Zero()
        {
            double minusZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000));
            var obj = new { v = minusZero };
            var toon = ToonSerializer.Serialize(obj, DefaultOpts());
            Assert.Equal("v: 0", toon);
        }

        [Fact]
        public void Encode_Indent_CustomIndentOf4()
        {
            var opts = new ToonSerializerOptions
            (
                indent: 4,
                delimiter: ToonDelimiter.Comma,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions)
            );

            var obj = new { a = new { b = 1 } };
            var toon = ToonSerializer.Serialize(obj, opts);

            var expected = string.Join('\n', new[]
            {
                "a:",
                "    b: 1"
            });
            Assert.Equal(expected, toon);
        }
    }
}