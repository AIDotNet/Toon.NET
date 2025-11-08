using System;
using System.Text.Json.Nodes;
using Xunit;

namespace AIDotNet.Toon.Tests;

public class EncodingAdvancedTests
{
    [Fact]
    public void Encode_NegativeZero_Canonicalized()
    {
        double d = -0.0;
        var s = ToonEncoder.Encode(d);
        Assert.Equal("0", s);
    }

    [Fact]
    public void Encode_NaN_Infinity_ToNull()
    {
        var nan = double.NaN;
        var inf = double.PositiveInfinity;
        Assert.Equal("null", ToonEncoder.Encode(nan));
        Assert.Equal("null", ToonEncoder.Encode(inf));
    }

    [Fact]
    public void Encode_LengthMarker_Array()
    {
        var data = new[] { 1, 2 };
        var toon = ToonEncoder.Encode(data, new Options.ToonEncodeOptions { LengthMarker = true });
        Assert.StartsWith("[#2]", toon.Trim());
    }

    [Fact]
    public void Encode_Tabular_With_Tab_Delimiter()
    {
        var rows = new[] { new { a = 1, b = 2 }, new { a = 3, b = 4 } };
        var toon = ToonEncoder.Encode(rows, new Options.ToonEncodeOptions { Delimiter = ToonDelimiter.TAB });
        Assert.Contains("[2\t]{a\tb}", toon);
        Assert.Contains("1\t2", toon);
    }

    [Fact]
    public void Encode_PrimitiveArray_CustomPipeDelimiter()
    {
        var arr = new[] { "a", "b" };
        var toon = ToonEncoder.Encode(arr, new Options.ToonEncodeOptions { Delimiter = ToonDelimiter.PIPE });
        Assert.StartsWith("[2|]:", toon.Trim());
        Assert.Contains("a|b", toon);
    }

    [Fact]
    public void Encode_QuotedStringNeeded()
    {
        var s = ToonEncoder.Encode("needs,comma");
        Assert.Equal("\"needs,comma\"", s);
    }

    [Fact]
    public void Encode_ObjectWithEmptyChild()
    {
        var obj = new { a = 1, empty = new { } };
        var toon = ToonEncoder.Encode(obj);
        Assert.Contains("empty:", toon);
    }

    [Fact]
    public void Encode_ArrayOfEmptyObjects()
    {
        var arr = new[] { new { }, new { } };
        var toon = ToonEncoder.Encode(arr);
        Assert.Contains("[2]:", toon);
        Assert.Contains("-", toon);
    }
}