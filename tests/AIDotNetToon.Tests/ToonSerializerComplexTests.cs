using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using AIDotNet.Toon;
using Xunit;

namespace AIDotNetToon.Tests
{
    public class ToonSerializerComplexTests
    {
        [Fact]
        public void Serialize_And_Decode_ComplexStructure_Roundtrip()
        {
            var complex = new
            {
                id = 123,
                name = "测试,Example",
                meta = new
                {
                    tags = new[] { "a,b", "c", "emoji😀" },
                    flags = new[] { true, false },
                    metrics = new[] { double.NaN, double.PositiveInfinity, -0.0, 1.2345678901234567 },
                    map = new Dictionary<string, object?>
                    {
                        ["quoted:key"] = 1,
                        ["space key"] = "value",
                        ["emoji🔑"] = new[] { 1, 2 }
                    }
                },
                items = new object?[]
                {
                    new { x = 1, y = 2 },
                    new { x = 3, y = 4, extra = new[] { new { z = 5 }, new { z = 6 } } },
                    new object?[] { 7, 8, null, "a,b,c" }
                }
            };

            var toon = ToonEncoder.Encode(complex, new AIDotNet.Toon.Options.ToonEncodeOptions { LengthMarker = true });
            Assert.Contains("\"quoted:key\":", toon);
            Assert.Contains("\"space key\":", toon);
            Assert.Contains("\"emoji🔑\"[#2]:", toon);
            Assert.Contains("\"a,b\"", toon); // quoted tag
            Assert.Contains("[#4]", toon); // metrics length marker (array of 4)

            var node = ToonDecoder.Decode(toon) as JsonObject;
            Assert.NotNull(node);
            var meta = node!["meta"]!.AsObject();
            var metrics = meta["metrics"] as JsonArray;
            Assert.Equal(4, metrics!.Count);
            // NaN & Infinity -> null, -0.0 canonicalized to 0
            Assert.True(metrics[0]?.GetValue<JsonNode?>() is null);
            Assert.True(metrics[1]?.GetValue<JsonNode?>() is null);
            Assert.Equal(0, metrics[2]!.GetValue<double>());
            Assert.Equal(1.2345678901234567, metrics[3]!.GetValue<double>());

            var map = meta["map"]!.AsObject();
            Assert.Equal(1, map["quoted:key"]!.GetValue<double>());
            Assert.Equal("value", map["space key"]!.GetValue<string>());
            var emojiArr = map["emoji🔑"] as JsonArray;
            Assert.Equal(2, emojiArr!.Count);

            var items = node["items"] as JsonArray;
            Assert.Equal(3, items!.Count);
            // Mixed list item types
            Assert.Equal(1, items[0]!.AsObject()["x"]!.GetValue<double>());
            Assert.Equal(5, items[1]!.AsObject()["extra"]!.AsArray()[0]!.AsObject()["z"]!.GetValue<double>());
            var third = items[2]!.AsArray();
            Assert.Equal("a,b,c", third[3]!.GetValue<string>());
        }

        [Fact]
        public void Encode_Tabular_Pipe_Delimiter_ComplexValues()
        {
            var rows = new[] { new { name = "a,b", age = 1 }, new { name = "c", age = 2 } };
            var toon = ToonEncoder.Encode(rows,
                new AIDotNet.Toon.Options.ToonEncodeOptions { Delimiter = ToonDelimiter.PIPE, LengthMarker = true });
            // Header with pipe delimiter & length marker
            Assert.Contains("[#2|]{name|age}", toon);
            // First row should quote name containing comma
            Assert.Contains("a,b|1", toon);
            Assert.Contains("c|2", toon);
            // Roundtrip decode
            var obj = ToonDecoder.Decode(toon) as JsonArray;
            Assert.NotNull(obj);
            Assert.Equal(2, obj!.Count);
            Assert.Equal("a,b", obj[0]!.AsObject()["name"]!.GetValue<string>());
            Assert.Equal(1, obj[0]!.AsObject()["age"]!.GetValue<double>());
        }

        [Fact]
        public void NormalizeToon_ComplexBytes_Works()
        {
            var original = ToonEncoder.Encode(new { a = 1, b = new[] { 1, 2, 3 } },
                new AIDotNet.Toon.Options.ToonEncodeOptions { LengthMarker = true });
            var bytes = Encoding.UTF8.GetBytes(original);
            var normalized = ToonSerializer.NormalizeToon(bytes);
            // After normalize should still parse & contain keys
            Assert.Contains("a: 1", normalized);
            var node = ToonDecoder.Decode(normalized) as JsonObject;
            Assert.NotNull(node);
            Assert.Equal(1, node!["a"]!.GetValue<double>());
        }
    }
}