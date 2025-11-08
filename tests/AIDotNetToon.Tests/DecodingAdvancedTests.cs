using System;
using System.Text.Json.Nodes;
using AIDotNet.Toon.Options;
using Xunit;

namespace AIDotNet.Toon.Tests;

public class DecodingAdvancedTests
{
    [Fact]
    public void Decode_QuotedKey()
    {
        var toon = "\"complex:key\": 1";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        Assert.True(obj!.ContainsKey("complex:key"));
    }

    [Fact]
    public void Decode_QuotedString()
    {
        var toon = "a: \"hello,world\"";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        Assert.Equal("hello,world", obj!["a"]!.GetValue<string>());
    }

    [Fact]
    public void Decode_Tabular_With_Pipe_Delimiter()
    {
        var toon = "rows[2|]{a|b}:\n  1|2\n  3|4";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        var rows = obj!["rows"] as JsonArray;
        Assert.Equal(2, rows!.Count);
        Assert.Equal(4, rows[1]!.AsObject()["b"]!.GetValue<double>());
    }

    [Fact]
    public void Decode_Tabular_With_Tab_Delimiter()
    {
        var toon = "rows[2\t]{a\tb}:\n  1\t2\n  3\t4";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        var rows = obj!["rows"] as JsonArray;
        Assert.Equal(2, rows!.Count);
        Assert.Equal(4, rows[1]!.AsObject()["b"]!.GetValue<double>());
    }

    [Fact]
    public void Decode_List_ArrayOfPrimitiveArrays()
    {
        var toon = "items[2]:\n  - [2]: 1,2\n  - [1]: 3";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        var items = obj!["items"] as JsonArray;
        Assert.Equal(2, items!.Count);
    }

    [Fact]
    public void Decode_List_ArrayOfObjects_Nested()
    {
        var toon = "items[2]:\n  - child[1]: 10\n  - child[2]: 20,30";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        var items = obj!["items"] as JsonArray;
        Assert.Equal(2, items!.Count);
    }

    [Fact]
    public void Decode_LengthMarkerIgnored()
    {
        var toon = "nums[#2]: 1,2";
        var obj = ToonDecoder.Decode(toon) as JsonObject;
        var nums = obj!["nums"] as JsonArray;
        Assert.Equal(2, nums!.Count);
    }
}