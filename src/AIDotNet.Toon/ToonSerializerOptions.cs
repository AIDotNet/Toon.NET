#nullable enable
using System.Text.Json;
using System.Text.Json.Serialization;
using AIDotNet.Toon.Options;
using ToonFormat.Internal.Converters;

namespace AIDotNet.Toon;

/// <summary>
/// 与 System.Text.Json 的 JsonSerializerOptions 风格一致的 TOON 选项聚合，
/// 用于 ToonSerializer 便捷 API，以一个对象同时配置编码与解码相关选项。
/// </summary>
public sealed class ToonSerializerOptions
{
    /// <summary>每级缩进的空格数，默认 2。</summary>
    public int Indent { get; set; } = 2;
    /// <summary>数组行与行内原子数组分隔符，默认逗号。</summary>
    public ToonDelimiter Delimiter { get; set; } = Constants.DEFAULT_DELIMITER_ENUM;
    /// <summary>解码严格模式：校验行数、缩进等，默认 true。</summary>
    public bool Strict { get; set; } = true;
    /// <summary>数组长度标记：null 输出 [N]；true 输出 [#N]；默认 null。</summary>
    public bool? LengthMarker { get; set; } = null;

    /// <summary>
    /// 透传 System.Text.Json 的选项，用于对象与 JSON 的桥接及数值特殊值处理。
    /// 默认启用命名浮点字面量并注册 NaN/±Infinity 写出为 null 的转换器。
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; set; } = CreateDefaultJsonOptions();

    /// <summary>默认实例。</summary>
    public static ToonSerializerOptions Default { get; } = new ToonSerializerOptions();

    // 将聚合选项拆分为底层编码选项
    internal ToonEncodeOptions ToEncodeOptions() => new ToonEncodeOptions
    {
        Indent = Indent,
        Delimiter = Delimiter,
        LengthMarker = LengthMarker == true
    };

    // 将聚合选项拆分为底层解码选项
    internal ToonDecodeOptions ToDecodeOptions() => new ToonDecodeOptions
    {
        Indent = Indent,
        Strict = Strict
    };

    private static JsonSerializerOptions CreateDefaultJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };
        opts.Converters.Add(new DoubleNamedFloatToNullConverter());
        opts.Converters.Add(new SingleNamedFloatToNullConverter());
        return opts;
    }
}
