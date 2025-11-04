#nullable enable
using System;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    /// <summary>
    /// 覆盖更复杂的 TOON 结构解码用例：
    /// - 非默认分隔符行内数组
    /// - 列表数组（带键名）
    /// - 表格数组（含带引号字段名与带引号单元格）
    /// - 长度标记 # 与分隔符覆盖
    /// - 行内数组 + 列表项追加
    /// - 多行平铺对象
    /// - 引号键名与字符串反转义的完整断言
    /// </summary>
    public class DecodeMoreTests
    {
        private static ToonSerializerOptions StrictOpts() => new ToonSerializerOptions(
            indent: 2,
            delimiter: ToonDelimiter.Comma,
            strict: true,
            lengthMarker: null,
            jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions)
        );

        [Fact]
        public void Decode_InlinePrimitiveArray_WithPipeDelimiter()
        {
            // nums[3|]: 1|"b c"|true  - 非默认分隔符行内数组
            var toon = "nums[3|]: 1|\"b c\"|true";

            var obj = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            Assert.Equal(JsonValueKind.Object, obj.ValueKind);

            var arr = obj.GetProperty("nums");
            Assert.Equal(JsonValueKind.Array, arr.ValueKind);
            Assert.Equal(3, arr.GetArrayLength());

            Assert.Equal(1, arr[0].GetInt32());
            Assert.Equal("b c", arr[1].GetString());
            Assert.True(arr[2].GetBoolean());
        }

        [Fact]
        public void Decode_ListArray_Keyed_Success()
        {
            // items[3]:
            //   - 1
            //   - 2
            //   - 3
            var toon = string.Join('\n', new[]
            {
                "items[3]:",
                "  - 1",
                "  - 2",
                "  - 3",
            });

            var obj = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            var arr = obj.GetProperty("items");
            Assert.Equal(3, arr.GetArrayLength());
            Assert.Equal(1, arr[0].GetInt32());
            Assert.Equal(2, arr[1].GetInt32());
            Assert.Equal(3, arr[2].GetInt32());
        }

        [Fact]
        public void Decode_Tabular_WithQuotedFields_AndQuotedCells()
        {
            // people[2]{"full name",age}:
            //   "alice smith",20
            //   "bob, jr",30   // 单元格内逗号由引号保护
            var toon = string.Join('\n', new[]
            {
                "people[2]{\"full name\",age}:",
                "  \"alice smith\",20",
                "  \"bob, jr\",30",
            });

            var root = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            var people = root.GetProperty("people");
            Assert.Equal(JsonValueKind.Array, people.ValueKind);
            Assert.Equal(2, people.GetArrayLength());

            var p1 = people[0];
            Assert.Equal("alice smith", p1.GetProperty("full name").GetString());
            Assert.Equal(20, p1.GetProperty("age").GetInt32());

            var p2 = people[1];
            Assert.Equal("bob, jr", p2.GetProperty("full name").GetString());
            Assert.Equal(30, p2.GetProperty("age").GetInt32());
        }

        [Fact]
        public void Decode_Tabular_WithLengthMarker_AndPipeDelimiter()
        {
            // rows[#2|]{a,b}:
            //   1|x
            //   2|y
            var toon = string.Join('\n', new[]
            {
                "rows[#2|]{a,b}:",
                "  1|x",
                "  2|y",
            });

            var root = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            var rows = root.GetProperty("rows");
            Assert.Equal(2, rows.GetArrayLength());

            Assert.Equal(1, rows[0].GetProperty("a").GetInt32());
            Assert.Equal("x", rows[0].GetProperty("b").GetString());
            Assert.Equal(2, rows[1].GetProperty("a").GetInt32());
            Assert.Equal("y", rows[1].GetProperty("b").GetString());
        }

        [Fact]
        public void Decode_HeaderInline_Then_ListAppend_Success()
        {
            // [4]: 1,2
            //   - 3
            //   - 4
            var toon = string.Join('\n', new[]
            {
                "[4]: 1,2",
                "  - 3",
                "  - 4",
            });

            var arr = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            Assert.Equal(JsonValueKind.Array, arr.ValueKind);
            Assert.Equal(4, arr.GetArrayLength());
            Assert.Equal(1, arr[0].GetInt32());
            Assert.Equal(2, arr[1].GetInt32());
            Assert.Equal(3, arr[2].GetInt32());
            Assert.Equal(4, arr[3].GetInt32());
        }

        [Fact]
        public void Decode_FlatObject_Multiline_Success()
        {
            // a: 1
            // b: "x\n"
            // c: false
            var toon = string.Join('\n', new[]
            {
                "a: 1",
                "b: \"x\\n\"",
                "c: false",
            });

            var obj = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            Assert.Equal(1, obj.GetProperty("a").GetInt32());
            Assert.Equal("x\n", obj.GetProperty("b").GetString());
            Assert.False(obj.GetProperty("c").GetBoolean());
        }

        [Fact]
        public void Decode_QuotedKey_And_StringUnescape_FullAssert()
        {
            // "full name": "a\n\t\r\"b"
            var toon = "\"full name\": \"a\\n\\t\\r\\\"b\"";

            var obj = ToonSerializer.Deserialize<JsonElement>(toon, StrictOpts());
            Assert.Equal("a\n\t\r\"b", obj.GetProperty("full name").GetString());
        }
    }
}