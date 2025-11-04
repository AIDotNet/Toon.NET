#nullable enable
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
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
        private static readonly Encoding Utf8NoBomStrict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // ========= 字符串 API =========

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

        // ========= byte[] / Span API =========

        /// <summary>将 .NET 对象编码为 UTF-8 字节（无 BOM）。</summary>
        public static byte[] SerializeToUtf8Bytes<T>(T value, ToonSerializerOptions? options = null)
        {
            var text = Serialize(value, options);
            return Utf8NoBomStrict.GetBytes(text);
        }

        /// <summary>将 .NET 对象（显式类型）编码为 UTF-8 字节（无 BOM）。</summary>
        public static byte[] SerializeToUtf8Bytes(object? value, Type inputType, ToonSerializerOptions? options = null)
        {
            var text = Serialize(value, inputType, options);
            return Utf8NoBomStrict.GetBytes(text);
        }

        /// <summary>从 UTF-8 字节解码为 .NET 对象。</summary>
        public static T? Deserialize<T>(byte[] utf8Bytes, ToonSerializerOptions? options = null)
            => Deserialize<T>(utf8Bytes.AsSpan(), options);

        /// <summary>
        /// 从 UTF-8 只读字节解码为 .NET 对象。
        /// 性能说明：Encoding.GetString(ReadOnlySpan<byte>) 直接从 Span 解码为单个字符串分配，无中间缓冲拷贝；
        /// 这是在当前解码管线（基于 string 的扫描/解析）下最少分配的做法。若需进一步优化，应让解码器改为基于
        /// ReadOnlySpan<char> / 逐段扫描以避免整体字符串实体化，这属于未来的流水线级改造。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Bytes, ToonSerializerOptions? options = null)
        {
            if (utf8Bytes.Length == 0)
                throw new ArgumentException("Cannot decode empty UTF-8 payload.", nameof(utf8Bytes));

            var toon = Utf8NoBomStrict.GetString(utf8Bytes);
            return Deserialize<T>(toon, options);
        }

        /// <summary>从 UTF-8 字节解码为指定类型实例。</summary>
        public static object? Deserialize(byte[] utf8Bytes, Type returnType, ToonSerializerOptions? options = null)
            => Deserialize(utf8Bytes.AsSpan(), returnType, options);

        /// <summary>
        /// 从 UTF-8 只读字节解码为指定类型实例。
        /// 性能说明：同上，单次字符串分配+解码，避免额外拷贝；严格 UTF-8（无 BOM）在遇到非法序列时立即抛出异常。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? Deserialize(ReadOnlySpan<byte> utf8Bytes, Type returnType, ToonSerializerOptions? options = null)
        {
            if (returnType is null) throw new ArgumentNullException(nameof(returnType));
            if (utf8Bytes.Length == 0)
                throw new ArgumentException("Cannot decode empty UTF-8 payload.", nameof(utf8Bytes));

            var toon = Utf8NoBomStrict.GetString(utf8Bytes);
            return Deserialize(toon, returnType, options);
        }

        // ========= Stream API =========

        /// <summary>将 .NET 对象编码为 TOON，并写入 UTF-8 流（无 BOM，保持流打开）。</summary>
        public static void Serialize<T>(T value, Stream utf8Stream, ToonSerializerOptions? options = null)
        {
            if (utf8Stream is null) throw new ArgumentNullException(nameof(utf8Stream));
            var bytes = SerializeToUtf8Bytes(value, options);
            utf8Stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>将 .NET 对象（显式类型）编码为 TOON，并写入 UTF-8 流（无 BOM，保持流打开）。</summary>
        public static void Serialize(object? value, Type inputType, Stream utf8Stream, ToonSerializerOptions? options = null)
        {
            if (inputType is null) throw new ArgumentNullException(nameof(inputType));
            if (utf8Stream is null) throw new ArgumentNullException(nameof(utf8Stream));
            var bytes = SerializeToUtf8Bytes(value, inputType, options);
            utf8Stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>从 UTF-8 流解码为 .NET 对象（保持流打开）。</summary>
        public static T? Deserialize<T>(Stream utf8Stream, ToonSerializerOptions? options = null)
        {
            if (utf8Stream is null) throw new ArgumentNullException(nameof(utf8Stream));
            var toon = ReadToEndUtf8(utf8Stream);
            return Deserialize<T>(toon, options);
        }

        /// <summary>从 UTF-8 流解码为指定类型实例（保持流打开）。</summary>
        public static object? Deserialize(Stream utf8Stream, Type returnType, ToonSerializerOptions? options = null)
        {
            if (utf8Stream is null) throw new ArgumentNullException(nameof(utf8Stream));
            if (returnType is null) throw new ArgumentNullException(nameof(returnType));
            var toon = ReadToEndUtf8(utf8Stream);
            return Deserialize(toon, returnType, options);
        }

        // ========= helpers =========

        private static string ReadToEndUtf8(Stream stream)
        {
            using var reader = new StreamReader(stream, Utf8NoBomStrict, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            return reader.ReadToEnd();
        }
    }
}
