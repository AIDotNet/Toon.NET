using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace AIDotNet.Toon.Tests;

public class ToonEncoderTests
{
    [Fact]
    public void Encode_PrimitiveValues()
    {
        Assert.Equal("null", ToonEncoder.Encode((object?)null));
        Assert.Equal("true", ToonEncoder.Encode(true));
        Assert.Equal("123", ToonEncoder.Encode(123));
        var dbl = ToonEncoder.Encode(3.14);
        Assert.Equal("3.1400000000000001", dbl);
        Assert.Equal("hello", ToonEncoder.Encode("hello"));
        Assert.Equal("\"a,b\"", ToonEncoder.Encode("a,b"));
    }

    [Fact]
    public void Encode_ObjectAndArray()
    {
        var obj = new { a = 1, b = "x" };
        var toon = ToonEncoder.Encode(obj);
        Assert.Contains("a: 1", toon);
        Assert.Contains("b: x", toon);
        var arr = new[] { 1, 2, 3 };
        var toon2 = ToonEncoder.Encode(arr);
        Assert.Equal("[3]: 1,2,3", toon2.Trim());
    }

    [Fact]
    public void Encode_ArrayOfObjects_Tabular()
    {
        var rows = new[] { new { name = "a", age = 1 }, new { name = "b", age = 2 } };
        var txt = ToonEncoder.Encode(rows, new Options.ToonEncodeOptions { Delimiter = ToonDelimiter.COMMA });
        Assert.Contains("{name,age}", txt);
        Assert.Contains("a,1", txt);
        Assert.Contains("b,2", txt);
    }

    [Fact]
    public void Encode_ListItems_Mixed()
    {
        var v = new JsonArray
        {
            JsonValue.Create(1), new JsonObject { ["x"] = 2 },
            new JsonArray { JsonValue.Create(1), JsonValue.Create(2) }
        };
        var s = ToonEncoder.Encode(v, new Options.ToonEncodeOptions());
        Assert.Contains("[3]:", s);
        Assert.Contains("- x: 2", s);
    }

    [Fact]
    public void EncodeToBytes_And_Stream()
    {
        var b = ToonEncoder.EncodeToBytes(new { a = 1 });
        Assert.NotEmpty(b);
        using var ms = new MemoryStream();
        ToonEncoder.EncodeToStream(new { a = 1 }, ms);
        Assert.True(ms.Position > 0);
    }
}