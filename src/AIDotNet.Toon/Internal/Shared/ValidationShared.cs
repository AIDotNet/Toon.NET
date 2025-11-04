#nullable enable
using System.Text.RegularExpressions;

namespace AIDotNet.Toon.Internal.Shared
{
    /// <summary>
    /// 与 TypeScript 版 shared/validation.ts 对齐的校验工具：
    /// - IsValidUnquotedKey: 键名是否可不带引号
    /// - IsSafeUnquoted: 字符串值是否可不带引号
    /// - IsBooleanOrNullLiteral: 是否为 true/false/null
    /// - IsNumericLike: 是否看起来像数字文本（含前导零整数）
    /// </summary>
    internal static class ValidationShared
    {
        private static readonly Regex UnquotedKeyRegex =
            new("^[A-Za-z_][\\w.]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>键名是否可不带引号：^[A-Z_][\w.]*$（不区分大小写）。</summary>
        internal static bool IsValidUnquotedKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return UnquotedKeyRegex.IsMatch(key);
        }

        /// <summary>
        /// 值是否可不带引号：
        /// - 非空且无前后空白
        /// - 不为布尔/null，也不像数字
        /// - 不含冒号、引号、反斜杠
        /// - 不含方括号/花括号
        /// - 不含换行/回车/制表符
        /// - 不含当前分隔符
        /// - 不以 '-' 开头（列表标记）
        /// </summary>
        internal static bool IsSafeUnquoted(string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (!string.Equals(value.Trim(), value, StringComparison.Ordinal)) return false;

            var v = value;

            if (IsBooleanOrNullLiteral(v) || IsNumericLike(v)) return false;

            if (v.IndexOf(':') >= 0) return false;

            if (v.IndexOf('"') >= 0 || v.IndexOf('\\') >= 0) return false;

            if (v.IndexOf('[') >= 0 || v.IndexOf(']') >= 0 || v.IndexOf('{') >= 0 || v.IndexOf('}') >= 0) return false;

            if (v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0 || v.IndexOf('\t') >= 0) return false;

            if (delimiter != '\0' && v.IndexOf(delimiter) >= 0) return false;

            if (v.Length > 0 && v[0] == '-') return false;

            return true;
        }

        /// <summary>true/false/null 判定。</summary>
        internal static bool IsBooleanOrNullLiteral(string token)
        {
            return string.Equals(token, "true", StringComparison.Ordinal)
                || string.Equals(token, "false", StringComparison.Ordinal)
                || string.Equals(token, "null", StringComparison.Ordinal);
        }

        // 数字文本匹配与前导零整数匹配
        private static readonly Regex NumericLikeRegex =
            new("^-?\\d+(?:\\.\\d+)?(?:e[+-]?\\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex LeadingZeroIntegerRegex =
            new("^0\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>是否看起来像数字（含科学计数）或前导零整数。</summary>
        internal static bool IsNumericLike(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return NumericLikeRegex.IsMatch(value) || LeadingZeroIntegerRegex.IsMatch(value);
        }
    }
}