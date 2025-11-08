using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace AIDotNet.Toon.Tests;

public class ToonSerializerTests
{
    [Fact]
    public void JsonToToon_And_Back()
    {
        var json = "{\"a\":1,\"b\":\"x\"}";
        var toon = ToonSerializer.JsonToToon(json);
        Assert.Contains("a: 1", toon);
        var jsonBack = ToonSerializer.ToonToJson(toon);
        Assert.Contains("\"a\":1", jsonBack);
    }

    [Fact]
    public void Serialize_Object_Generic()
    {
        var v = new { a = 1, b = "x" };
        var t = ToonSerializer.Serialize(v);
        Assert.Contains("a: 1", t);
    }

    [Fact]
    public void Deserialize_Object_Generic()
    {
        var toon = "a: 1\nb: x";
        var obj = ToonSerializer.Deserialize<TestPoco>(toon);
        Assert.NotNull(obj);
        Assert.Equal(1, obj!.a);
        Assert.Equal("x", obj.b);
    }

    [Fact]
    public void NormalizeToon_Works()
    {
        var original = "[2]: 1,2";
        var bytes = Encoding.UTF8.GetBytes(original);
        var normalized = ToonSerializer.NormalizeToon(bytes);
        Assert.Equal(original, normalized.Trim());
    }

    private class TestPoco
    {
        public int a { get; set; }
        public string? b { get; set; }
    }
}