#nullable enable
using System.Text.Json;

namespace AIDotNet.Toon
{
    /// <summary>
    /// 提供与 System.Text.Json 风格一致的 TOON 编解码入口，统一由 <see cref="ToonSerializerOptions"/> 控制行为。
    /// Serialize 路径: .NET 对象 -> JsonElement -> TOON 文本
    /// Deserialize 路径: TOON 文本 -> JSON 字符串/DOM -> 目标类型
    /// </summary>
    public static class ToonSerializer
    {
        /// <summary>将 .NET 对象编码为 TOON 文本。</summary>
        public static string Serialize<T>(T value, ToonSerializerOptions? options = null)
        {
            options ??= ToonSerializerOptions.Default;
            var element = JsonSerializer.SerializeToElement(value, options.JsonOptions);
            return ToonEncoder.Encode(element, options);
        }

        /// <summary>将 .NET 对象（使用显式类型）编码为 TOON 文本。</summary>
        public static string Serialize(object? value, Type inputType, ToonSerializerOptions? options = null)
        {
            if (inputType is null) throw new ArgumentNullException(nameof(inputType));
            options ??= ToonSerializerOptions.Default;

            // 使用显式类型序列化为 JsonElement，以保持精度与转换器行为
            var element = JsonSerializer.SerializeToElement(value, inputType, options.JsonOptions);
            return ToonEncoder.Encode(element, options);
        }

        /// <summary>将 TOON 文本解码为 .NET 对象。</summary>
        public static T? Deserialize<T>(string toon, ToonSerializerOptions? options = null)
        {
            if (toon is null) throw new ArgumentNullException(nameof(toon));
            options ??= ToonSerializerOptions.Default;

            // 先解码为 JSON（字符串或 DOM），再交给 System.Text.Json
            var json = ToonDecoder.DecodeToJsonString(toon, options);
            return JsonSerializer.Deserialize<T>(json, options.JsonOptions);
        }

        /// <summary>将 TOON 文本解码为指定类型实例。</summary>
        public static object? Deserialize(string toon, Type returnType, ToonSerializerOptions? options = null)
        {
            if (toon is null) throw new ArgumentNullException(nameof(toon));
            if (returnType is null) throw new ArgumentNullException(nameof(returnType));
            options ??= ToonSerializerOptions.Default;

            var json = ToonDecoder.DecodeToJsonString(toon, options);
            return JsonSerializer.Deserialize(json, returnType, options.JsonOptions);
        }
    }
}
