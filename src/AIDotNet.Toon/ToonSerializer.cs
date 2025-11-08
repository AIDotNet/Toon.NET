#nullable enable
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIDotNet.Toon.Options;

namespace AIDotNet.Toon;

/// <summary>
/// 提供类似 System.Text.Json 的简洁 API，用于 TOON 与对象 / JSON 之间互转。
/// </summary>
public static class ToonSerializer
{
    // ===================== TOON -> JSON 字符串 =====================
    /// <summary>toon 字符串 -> json 字符串。</summary>
    public static string ToonToJson(string toon, ToonDecodeOptions? options = null)
    {
        var node = ToonDecoder.Decode(toon, options ?? new ToonDecodeOptions());
        return node?.ToJsonString() ?? "null";
    }

    /// <summary>toon 字符串 -> json 字符串（聚合选项）。</summary>
    public static string ToonToJson(string toon, ToonSerializerOptions? options)
    {
        var node = ToonDecoder.Decode(toon, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());
        return node?.ToJsonString(options?.JsonOptions) ?? "null";
    }

    /// <summary>toon UTF-8 字节 -> json 字符串。</summary>
    public static string ToonToJson(byte[] utf8Toon, ToonDecodeOptions? options = null)
    {
        var node = ToonDecoder.Decode(utf8Toon, options ?? new ToonDecodeOptions());
        return node?.ToJsonString() ?? "null";
    }

    /// <summary>toon UTF-8 字节 -> json 字符串（聚合选项）。</summary>
    public static string ToonToJson(byte[] utf8Toon, ToonSerializerOptions? options)
    {
        var node = ToonDecoder.Decode(utf8Toon, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());
        return node?.ToJsonString(options?.JsonOptions) ?? "null";
    }

    /// <summary>toon 流(UTF-8) -> json 字符串。流保持打开。</summary>
    public static string ToonToJson(Stream toonStream, ToonDecodeOptions? options = null)
    {
        var node = ToonDecoder.Decode(toonStream, options ?? new ToonDecodeOptions());
        return node?.ToJsonString() ?? "null";
    }

    /// <summary>toon 流(UTF-8) -> json 字符串（聚合选项）。流保持打开。</summary>
    public static string ToonToJson(Stream toonStream, ToonSerializerOptions? options)
    {
        var node = ToonDecoder.Decode(toonStream, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());
        return node?.ToJsonString(options?.JsonOptions) ?? "null";
    }

    // ===================== TOON -> 对象 =====================
    /// <summary>toon 字符串 -> T 对象。</summary>
    public static T? Deserialize<T>(string toon, ToonDecodeOptions? options = null)
        => ToonDecoder.Decode<T>(toon, options ?? new ToonDecodeOptions());

    /// <summary>toon 字符串 -> T 对象（聚合选项）。</summary>
    public static T? Deserialize<T>(string toon, ToonSerializerOptions? options)
        => ToonDecoder.Decode<T>(toon, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());

    /// <summary>toon UTF-8 字节 -> T 对象。</summary>
    public static T? Deserialize<T>(byte[] utf8Toon, ToonDecodeOptions? options = null)
        => ToonDecoder.Decode<T>(utf8Toon, options ?? new ToonDecodeOptions());

    /// <summary>toon UTF-8 字节 -> T 对象（聚合选项）。</summary>
    public static T? Deserialize<T>(byte[] utf8Toon, ToonSerializerOptions? options)
        => ToonDecoder.Decode<T>(utf8Toon, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());

    /// <summary>toon 流(UTF-8) -> T 对象。流保持打开。</summary>
    public static T? Deserialize<T>(Stream toonStream, ToonDecodeOptions? options = null)
        => ToonDecoder.Decode<T>(toonStream, options ?? new ToonDecodeOptions());

    /// <summary>toon 流(UTF-8) -> T 对象（聚合选项）。流保持打开。</summary>
    public static T? Deserialize<T>(Stream toonStream, ToonSerializerOptions? options)
        => ToonDecoder.Decode<T>(toonStream, (options ?? ToonSerializerOptions.Default).ToDecodeOptions());

    // ===================== JSON / 对象 -> TOON 字符串 =====================
    /// <summary>JSON 字符串 -> toon 字符串。</summary>
    public static string JsonToToon(string json, ToonEncodeOptions? options = null, JsonDocumentOptions docOptions = default)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        JsonNode? node = JsonNode.Parse(json, null, docOptions);
        return ToonEncoder.Encode(node, options ?? new ToonEncodeOptions());
    }

    /// <summary>JSON 字符串 -> toon 字符串（聚合选项）。</summary>
    public static string JsonToToon(string json, ToonSerializerOptions? options)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        var opts = options ?? ToonSerializerOptions.Default;
        // 使用 JsonSerializer 以支持命名浮点字面量等行为
        JsonNode? node = JsonSerializer.Deserialize<JsonNode?>(json, opts.JsonOptions);
        return ToonEncoder.Encode(node, opts.ToEncodeOptions());
    }

    /// <summary>JSON UTF-8 字节 -> toon 字符串。</summary>
    public static string JsonToToon(byte[] utf8Json, ToonEncodeOptions? options = null, JsonDocumentOptions docOptions = default)
    {
        if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
        JsonNode? node = JsonNode.Parse(utf8Json, null, docOptions);
        return ToonEncoder.Encode(node, options ?? new ToonEncodeOptions());
    }

    /// <summary>JSON UTF-8 字节 -> toon 字符串（聚合选项）。</summary>
    public static string JsonToToon(byte[] utf8Json, ToonSerializerOptions? options)
    {
        if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
        var opts = options ?? ToonSerializerOptions.Default;
        JsonNode? node = JsonSerializer.Deserialize<JsonNode?>(utf8Json, opts.JsonOptions);
        return ToonEncoder.Encode(node, opts.ToEncodeOptions());
    }

    /// <summary>JSON 流(UTF-8) -> toon 字符串。流保持打开。</summary>
    public static string JsonToToon(Stream jsonStream, ToonEncodeOptions? options = null, JsonDocumentOptions docOptions = default)
    {
        if (jsonStream == null) throw new ArgumentNullException(nameof(jsonStream));
        using var reader = new StreamReader(jsonStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var json = reader.ReadToEnd();
        JsonNode? node = JsonNode.Parse(json, null, docOptions);
        return ToonEncoder.Encode(node, options ?? new ToonEncodeOptions());
    }

    /// <summary>JSON 流(UTF-8) -> toon 字符串（聚合选项）。流保持打开。</summary>
    public static string JsonToToon(Stream jsonStream, ToonSerializerOptions? options)
    {
        if (jsonStream == null) throw new ArgumentNullException(nameof(jsonStream));
        using var reader = new StreamReader(jsonStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var json = reader.ReadToEnd();
        var opts = options ?? ToonSerializerOptions.Default;
        JsonNode? node = JsonSerializer.Deserialize<JsonNode?>(json, opts.JsonOptions);
        return ToonEncoder.Encode(node, opts.ToEncodeOptions());
    }

    /// <summary>对象 T -> toon 字符串。</summary>
    public static string Serialize<T>(T value, ToonEncodeOptions? options = null)
        => ToonEncoder.Encode(value, options ?? new ToonEncodeOptions());

    /// <summary>对象 T -> toon 字符串（聚合选项）。</summary>
    public static string Serialize<T>(T value, ToonSerializerOptions? options)
        => ToonEncoder.Encode(value, (options ?? ToonSerializerOptions.Default).ToEncodeOptions());

    /// <summary>对象 -> toon 字符串。</summary>
    public static string Serialize(object? value, ToonEncodeOptions? options = null)
        => ToonEncoder.Encode(value, options ?? new ToonEncodeOptions());

    /// <summary>对象 -> toon 字符串（聚合选项）。</summary>
    public static string Serialize(object? value, ToonSerializerOptions? options)
        => ToonEncoder.Encode(value, (options ?? ToonSerializerOptions.Default).ToEncodeOptions());

    /// <summary>JSON UTF-8 字节 -> toon 字符串（同 JsonToToon）。提供与 System.Text.Json 类似的命名。</summary>
    public static string SerializeJsonBytesToToon(byte[] utf8Json, ToonEncodeOptions? options = null, JsonDocumentOptions docOptions = default)
        => JsonToToon(utf8Json, options, docOptions);

    /// <summary>JSON 流 -> toon 字符串（同 JsonToToon）。</summary>
    public static string SerializeJsonStreamToToon(Stream jsonStream, ToonEncodeOptions? options = null, JsonDocumentOptions docOptions = default)
        => JsonToToon(jsonStream, options, docOptions);

    // ===================== 额外便捷：TOON UTF-8 -> TOON 字符串（规范化） =====================
    /// <summary>将 TOON UTF-8 字节解码再重新编码（用于规范化）。</summary>
    public static string NormalizeToon(byte[] utf8Toon, ToonDecodeOptions? decodeOptions = null, ToonEncodeOptions? encodeOptions = null)
    {
        var node = ToonDecoder.Decode(utf8Toon, decodeOptions ?? new ToonDecodeOptions());
        return ToonEncoder.Encode(node, encodeOptions ?? new ToonEncodeOptions());
    }

    /// <summary>将 TOON 流解码再重新编码（用于规范化）。</summary>
    public static string NormalizeToon(Stream toonStream, ToonDecodeOptions? decodeOptions = null, ToonEncodeOptions? encodeOptions = null)
    {
        var node = ToonDecoder.Decode(toonStream, decodeOptions ?? new ToonDecodeOptions());
        return ToonEncoder.Encode(node, encodeOptions ?? new ToonEncodeOptions());
    }
}
