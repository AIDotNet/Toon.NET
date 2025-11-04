#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace AIDotNetToon.Tests
{
    public class ByteAndStreamTests
    {
        private static readonly Encoding Utf8NoBomStrict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void SerializeToUtf8Bytes_Generic_MatchesStringUtf8()
        {
            var obj = new { a = 1, b = "x" };

            var text = ToonSerializer.Serialize(obj, DefaultOpts());
            var bytes = ToonSerializer.SerializeToUtf8Bytes(obj, DefaultOpts());

            var expected = Utf8NoBomStrict.GetBytes(text);
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void SerializeToUtf8Bytes_TypeOverload_MatchesStringUtf8()
        {
            var dict = new Dictionary<string, int> { ["x"] = 1 };

            var text = ToonSerializer.Serialize((object)dict, typeof(Dictionary<string, int>), DefaultOpts());
            var bytes = ToonSerializer.SerializeToUtf8Bytes((object)dict, typeof(Dictionary<string, int>), DefaultOpts());

            var expected = Utf8NoBomStrict.GetBytes(text);
            Assert.Equal(expected, bytes);
        }

        [Fact]
        public void Deserialize_FromUtf8Bytes_Generic_Succeeds()
        {
            var toon = "[3]: 1,2,3";
            var data = Utf8NoBomStrict.GetBytes(toon);

            var arr = ToonSerializer.Deserialize<int[]>(data, DefaultOpts());
            Assert.NotNull(arr);
            Assert.Equal(new[] { 1, 2, 3 }, arr!);
        }

        [Fact]
        public void Deserialize_FromUtf8Span_Generic_Succeeds()
        {
            var toon = "nums[3]: 1,2,3";
            var data = Utf8NoBomStrict.GetBytes(toon);

            var obj = ToonSerializer.Deserialize<JsonElement>(data.AsSpan(), DefaultOpts());
            Assert.Equal(JsonValueKind.Object, obj.ValueKind);
            var nums = obj.GetProperty("nums");
            Assert.Equal(JsonValueKind.Array, nums.ValueKind);
            Assert.Equal(3, nums.GetArrayLength());
            Assert.Equal(1, nums[0].GetInt32());
            Assert.Equal(2, nums[1].GetInt32());
            Assert.Equal(3, nums[2].GetInt32());
        }

        [Fact]
        public void Deserialize_FromUtf8Bytes_Type_Succeeds()
        {
            var toon = "[4]: 1,2,3,4";
            var data = Utf8NoBomStrict.GetBytes(toon);

            var ret = ToonSerializer.Deserialize(data, typeof(List<int>), DefaultOpts());
            Assert.IsType<List<int>>(ret);
            Assert.Equal(new[] { 1, 2, 3, 4 }, ((List<int>)ret!).ToArray());
        }

        [Fact]
        public void Deserialize_FromUtf8Span_Type_Succeeds()
        {
            // 使用扁平对象以匹配当前解码器占位实现能力（不含嵌套对象解析）
            var toon = "a: 1";
            var data = Utf8NoBomStrict.GetBytes(toon);

            var ret = ToonSerializer.Deserialize(data.AsSpan(), typeof(JsonElement), DefaultOpts());
            var root = Assert.IsType<JsonElement>(ret);
            Assert.Equal(1, root.GetProperty("a").GetInt32());
        }

        [Fact]
        public void Serialize_ToStream_LeavesOpen_AndContentMatchesUtf8()
        {
            var obj = new { a = 1, b = "x" };
            var expectedText = ToonSerializer.Serialize(obj, DefaultOpts());
            using var ms = new MemoryStream();

            ToonSerializer.Serialize(obj, ms, DefaultOpts()); // generic stream overload
            Assert.True(ms.CanWrite);

            ms.Position = 0;
            using var reader = new StreamReader(ms, Utf8NoBomStrict, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var written = reader.ReadToEnd();

            Assert.Equal(expectedText, written);
            Assert.True(ms.CanWrite); // still open
        }

        [Fact]
        public void Serialize_Type_ToStream_LeavesOpen_AndContentMatchesUtf8()
        {
            var dict = new Dictionary<string, int> { ["x"] = 1 };
            var expectedText = ToonSerializer.Serialize((object)dict, typeof(Dictionary<string, int>), DefaultOpts());

            using var ms = new MemoryStream();
            ToonSerializer.Serialize((object)dict, typeof(Dictionary<string, int>), ms, DefaultOpts()); // type stream overload

            ms.Position = 0;
            using var reader = new StreamReader(ms, Utf8NoBomStrict, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var written = reader.ReadToEnd();

            Assert.Equal(expectedText, written);
            Assert.True(ms.CanWrite);
        }

        [Fact]
        public void Deserialize_FromStream_Generic_LeavesOpen()
        {
            var toon = "a: 1";
            var data = Utf8NoBomStrict.GetBytes(toon);
            using var ms = new MemoryStream(data, writable: true);

            var obj = ToonSerializer.Deserialize<JsonElement>(ms, DefaultOpts());
            Assert.Equal(1, obj.GetProperty("a").GetInt32());
            Assert.True(ms.CanRead);
            Assert.True(ms.CanWrite);
        }

        [Fact]
        public void Deserialize_FromStream_Type_LeavesOpen()
        {
            var toon = "nums[2]: 10,20";
            var data = Utf8NoBomStrict.GetBytes(toon);
            using var ms = new MemoryStream(data, writable: true);

            var ret = ToonSerializer.Deserialize(ms, typeof(JsonElement), DefaultOpts());
            var root = Assert.IsType<JsonElement>(ret);
            var nums = root.GetProperty("nums");
            Assert.Equal(2, nums.GetArrayLength());
            Assert.Equal(10, nums[0].GetInt32());
            Assert.Equal(20, nums[1].GetInt32());
            Assert.True(ms.CanRead);
        }

        [Fact]
        public void Deserialize_InvalidUtf8_Bytes_ThrowsDecoderFallback()
        {
            // Invalid UTF-8 sequence (0xC3 0x28)
            byte[] invalid = new byte[] { 0xC3, 0x28 };

            Assert.Throws<DecoderFallbackException>(() =>
            {
                _ = ToonSerializer.Deserialize<string>(invalid, DefaultOpts());
            });
        }

        [Fact]
        public void Deserialize_InvalidUtf8_Stream_ThrowsDecoderFallback()
        {
            byte[] invalid = new byte[] { 0xE3, 0x28, 0xA1 }; // invalid sequence
            using var ms = new MemoryStream(invalid);

            Assert.Throws<DecoderFallbackException>(() =>
            {
                _ = ToonSerializer.Deserialize<string>(ms, DefaultOpts());
            });
        }
    }
}