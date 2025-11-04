#nullable enable
using System.Collections.Generic;
using System.Text.Json;
using AIDotNet.Toon;
using Xunit;

namespace Toon.Tests
{
    public class EncodeHeaderFieldQuotingTests
    {
        private static ToonSerializerOptions DefaultOpts() => ToonSerializerOptions.Default;

        [Fact]
        public void TabularHeader_Quotes_Unsafe_FieldNames_With_Comma_Delimiter()
        {
            // 字段名含空格需要加引号
            var rows = new[]
            {
                new Dictionary<string, object?> { ["full name"] = "alice", ["age"] = 30 },
                new Dictionary<string, object?> { ["full name"] = "bob", ["age"] = 31 },
            };

            var toon = ToonSerializer.Serialize(rows, DefaultOpts());

            var expected = string.Join('\n', new[]
            {
                "[2]{\"full name\",age}:",
                "  alice,30",
                "  bob,31",
            });

            Assert.Equal(expected, toon);
        }

        [Fact]
        public void TabularHeader_Quotes_Unsafe_FieldNames_With_Pipe_Delimiter()
        {
            var opts = new ToonSerializerOptions(
                indent: 2,
                delimiter: ToonDelimiter.Pipe,
                strict: true,
                lengthMarker: null,
                jsonOptions: new JsonSerializerOptions(ToonSerializerOptions.Default.JsonOptions));

            // 字段中包含分隔符 '|' 与空格，均需要加引号；字段之间用管道分隔
            var rows = new[]
            {
                new Dictionary<string, object?> { ["role|id"] = 1, ["full name"] = "a|b" },
                new Dictionary<string, object?> { ["role|id"] = 2, ["full name"] = "c|d" },
            };

            var toon = ToonSerializer.Serialize(rows, opts);

            var expected = string.Join('\n', new[]
            {
                "[2|]{\"role|id\"|\"full name\"}:",
                "  1|\"a|b\"",
                "  2|\"c|d\"",
            });

            Assert.Equal(expected, toon);
        }
    }
}