using System;
using Xunit;

namespace AIDotNet.Toon.Tests;

public class ErrorHandlingTests
{
    [Fact]
    public void Strict_CountMismatch_List()
    {
        var toon = "[2]:\n  - 1\n  - 2\n  - 3";
        Assert.Throws<ToonFormatException>(() =>
            ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
    }

    [Fact]
    public void Strict_CountMismatch_Tabular()
    {
        var toon = "rows[1]{a}:\n  1\n  2";
        Assert.Throws<ToonFormatException>(() =>
            ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
    }

    [Fact]
    public void Strict_BlankInsideTabular()
    {
        var toon = "rows[2]{a}:\n  1\n\n  2";
        Assert.Throws<ToonFormatException>(() =>
            ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
    }

    [Fact]
    public void Strict_TabsInIndent()
    {
        var toon = "a: 1\n\tb: 2";
        Assert.Throws<ToonFormatException>(() =>
            ToonDecoder.Decode(toon, new Options.ToonDecodeOptions { Strict = true }));
    }

    [Fact]
    public void Invalid_Number_Literal_Rejected()
    {
        var json = "{\"x\":001}";
        Assert.ThrowsAny<Exception>(() => ToonSerializer.JsonToToon(json));
    }
}