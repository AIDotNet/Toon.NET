using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace AIDotNet.Toon.Tests
{
    public class ToonDecoderTests
    {
        [Fact]
        public void Decode_Primitive()
        {
            var node = ToonDecoder.Decode("true");
            Assert.True(node!.GetValue<bool>());
        }

        [Fact]
        public void Decode_Object()
        {
            var obj = ToonDecoder.Decode("a: 1\nb: x") as JsonObject;
            Assert.NotNull(obj);
            Assert.Equal(1, obj!["a"]!.GetValue<double>());
            Assert.Equal("x", obj["b"]!.GetValue<string>());
        }

        [Fact]
        public void Decode_Array_PrimitiveInline()
        {
            var arr = ToonDecoder.Decode("[3]: 1,2,3") as JsonArray;
            Assert.Equal(3, arr!.Count);
            Assert.Equal(2, arr[1]!.GetValue<double>());
        }

        [Fact]
        public void Decode_Array_ListFormat()
        {
            var toon = "items[2]:\n  - a: 1\n    b: 2\n  - a: 3\n    b: 4";
            var node = ToonDecoder.Decode(toon) as JsonObject;
            var items = node!["items"] as JsonArray;
            Assert.Equal(2, items!.Count);
        }

        [Fact]
        public void Decode_Array_Tabular()
        {
            var toon = "users[2]{name,age}:\n  a,1\n  b,2";
            var obj = ToonDecoder.Decode(toon) as JsonObject;
            var users = obj!["users"] as JsonArray;
            Assert.Equal("a", users![0]!.AsObject()["name"]!.GetValue<string>());
        }

        [Fact]
        public void Decode_Stream()
        {
            var text = "[2]: 1,2";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var arr = ToonDecoder.Decode(ms) as JsonArray;
            Assert.Equal(2, arr!.Count);
        }

        [Fact]
        public void Decode_Strict_CountMismatch()
        {
            var toon = "[2]: 1,2,3";
            Assert.Throws<ToonFormatException>(() =>
                ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
        }

        [Fact]
        public void Decode_Strict_BlankLineInList()
        {
            var toon = "[2]:\n  - 1\n\n  - 2";
            Assert.Throws<ToonFormatException>(() =>
                ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
        }
    }
}