#nullable enable
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AIDotNet.Toon.Internal.Shared;

namespace AIDotNet.Toon.Internal.Encode
{
    /// <summary>
    /// 与 TypeScript 版 encode/primitives.ts 等价的原语编码工具：
    /// - EncodePrimitive：原子值编码（string/number/bool/null）
    /// - EncodeStringLiteral：按规则决定是否加引号与转义
    /// - EncodeKey：键名编码（可不加引号的规则与 TS 相同）
    /// - EncodeAndJoinPrimitives：将原子值序列化并按分隔符连接
    /// - FormatHeader：数组头部渲染 [N] 或 [#N]，可附加字段集 {a,b}
    /// </summary>
    internal static class Primitives
    {
        /// <summary>
        /// 原子值编码：
        /// - null -> "null"
        /// - bool -> "true"/"false"
        /// - number -> 按规则：NaN/Infinity/-Infinity -> "null"；-0 规范化为 "0"；其余保留 GetRawText 精度
        /// - string -> EncodeStringLiteral()
        /// 其他 JsonValueKind（对象/数组）不应进入此处，若进入则回退 "null"（与 TS 规范保持一致性由上层保证）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string EncodePrimitive(JsonElement value, ToonSerializerOptions options)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null => Tokens.NullLiteral,
                JsonValueKind.True => Tokens.TrueLiteral,
                JsonValueKind.False => Tokens.FalseLiteral,
                JsonValueKind.Number => EncodeNumber(value),
                JsonValueKind.String => EncodeStringLiteral(value.GetString() ?? string.Empty, options),
                _ => Tokens.NullLiteral,
            };

            static string EncodeNumber(JsonElement numberElement)
            {
                // 原文快速判断命名浮点常量
                var raw = numberElement.GetRawText();
                if (raw == "NaN" || raw == "Infinity" || raw == "-Infinity")
                    return Tokens.NullLiteral;

                // 仅在可能为负零时进行快速文本判断，避免解析为 double 带来的额外开销
                if (raw.Length > 0 && raw[0] == '-' && IsNegativeZeroText(raw))
                    return "0";

                return raw;

                static bool IsNegativeZeroText(string text)
                {
                    // 假定 text[0] == '-'
                    int i = 1;
                    bool hasDigit = false;
                    for (; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c >= '0' && c <= '9')
                        {
                            if (c != '0') return false; // 任意非零数字则不是 -0
                            hasDigit = true;
                        }
                        else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                        {
                            continue; // 允许小数点与科学计数法标记
                        }
                        else
                        {
                            return false; // 其他字符不接受
                        }
                    }
                    return hasDigit;
                }
            }
        }

        /// <summary>
        /// 字符串字面量编码。若满足 isSafeUnquoted 则直接输出；否则加双引号并转义。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string EncodeStringLiteral(string value, ToonSerializerOptions options)
        {
            var delimiter = options.GetDelimiterChar();
            if (ValidationShared.IsSafeUnquoted(value, delimiter))
                return value;

            return StringUtils.Quote(value);
        }

        /// <summary>
        /// 键名编码。若满足 isValidUnquotedKey 则直接输出；否则加双引号并转义。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string EncodeKey(string key)
        {
            if (ValidationShared.IsValidUnquotedKey(key))
                return key;

            return StringUtils.Quote(key);
        }

        /// <summary>
        /// 编码并按分隔符连接一组 JsonElement 原子值。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string EncodeAndJoinPrimitives(IEnumerable<JsonElement> values, ToonSerializerOptions options)
        {
            var delimiter = options.GetDelimiterChar();
            var sb = new StringBuilder();
            bool first = true;
            foreach (var v in values)
            {
                var token = EncodePrimitive(v, options);
                if (!first) sb.Append(delimiter);
                sb.Append(token);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 按指定分隔符连接一组已编码的字符串片段（辅助）。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string JoinEncoded(IEnumerable<string> encodedTokens, char delimiter)
            => string.Join(delimiter, encodedTokens);

        /// <summary>
        /// 构造数组头部：
        /// - 可选 key 前缀（已按 EncodeKey 规则编码）
        /// - 方括号内 [#N] 或 [N]；仅当分隔符不是逗号时在方括号末尾附加分隔符字符
        /// - 可选字段集：{col1,col2,...}，字段名按 EncodeKey 规则编码并用当前分隔符连接
        /// - 最终结尾加冒号
        /// </summary>
        internal static string FormatHeader(
            int length,
            ToonSerializerOptions options,
            string? key = null,
            IReadOnlyList<string>? fields = null,
            char? delimiterOverride = null,
            char? lengthMarkerOverride = null)
        {
            var delimiter = delimiterOverride ?? options.GetDelimiterChar();
            var includeDelimiterInBracket = delimiter != Tokens.DefaultDelimiterChar;

            var hasLengthMarker =
                (lengthMarkerOverride.HasValue && lengthMarkerOverride.Value == Tokens.Hash)
                || (options.LengthMarker.HasValue && options.LengthMarker.Value == Tokens.Hash);

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(key))
            {
                sb.Append(EncodeKey(key!));
            }

            sb.Append(Tokens.OpenBracket);
            if (hasLengthMarker)
                sb.Append(Tokens.Hash);
            sb.Append(length);
            if (includeDelimiterInBracket)
                sb.Append(delimiter);
            sb.Append(Tokens.CloseBracket);

            if (fields is { Count: > 0 })
            {
                sb.Append(Tokens.OpenBrace);
                for (int i = 0; i < fields.Count; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    sb.Append(EncodeKey(fields[i]));
                }
                sb.Append(Tokens.CloseBrace);
            }

            sb.Append(Tokens.Colon);
            return sb.ToString();
        }
    }
}