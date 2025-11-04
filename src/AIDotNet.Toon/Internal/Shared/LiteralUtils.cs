#nullable enable
using System.Globalization;

namespace AIDotNet.Toon.Internal.Shared
{
    /// <summary>
    /// 与 TypeScript 版 shared/literal-utils.ts 对齐的字面量判定工具。
    /// - IsBooleanOrNullLiteral: 判断是否 true/false/null
    /// - IsNumericLiteral: 判断是否为数值字面量，并拒绝不合法的前导零形式
    /// </summary>
    internal static class LiteralUtils
    {
        /// <summary>
        /// 检查 token 是否为布尔或 null 字面量：true、false、null。
        /// 等价 TS: isBooleanOrNullLiteral
        /// </summary>
        internal static bool IsBooleanOrNullLiteral(string token)
        {
            return string.Equals(token, "true", StringComparison.Ordinal)
                || string.Equals(token, "false", StringComparison.Ordinal)
                || string.Equals(token, "null", StringComparison.Ordinal);
        }

        /// <summary>
        /// 检查 token 是否为有效数值字面量。
        /// 规则与 TS 对齐：
        /// - 拒绝前导零（除 "0" 自身或小数 "0.xxx"）
        /// - 解析成功且为有限数值（非 NaN/Infinity）
        /// </summary>
        internal static bool IsNumericLiteral(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            // Must not have leading zeros (except "0" itself or decimals like "0.5")
            if (token.Length > 1 && token[0] == '0' && token[1] != '.')
                return false;

            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return false;

            return !double.IsNaN(num) && !double.IsInfinity(num);
        }
    }
}