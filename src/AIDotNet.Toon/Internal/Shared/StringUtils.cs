#nullable enable
using System.Text;

namespace AIDotNet.Toon.Internal.Shared
{
    /// <summary>
    /// 与 TypeScript 版 shared/string-utils.ts 对齐的字符串工具：
    /// - EscapeString：编码时对特殊字符转义
    /// - UnescapeString：解码时还原转义序列
    /// - FindClosingQuote：查找考虑转义的配对引号位置
    /// - FindUnquotedChar：查找不在引号内出现的目标字符
    /// </summary>
    internal static class StringUtils
    {
        /// <summary>
        /// 转义特殊字符：反斜杠、引号、换行、回车、制表符。
        /// 等价于 TS escapeString。
        /// </summary>
        internal static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// 反转义字符串，支持 \n、\t、\r、\\、\"。非法序列会抛出 <see cref="ToonFormatException"/>。
        /// 等价于 TS unescapeString。
        /// </summary>
        internal static string UnescapeString(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

            var sb = new StringBuilder(value.Length);
            int i = 0;
            while (i < value.Length)
            {
                var ch = value[i];
                if (ch == Tokens.Backslash)
                {
                    if (i + 1 >= value.Length)
                        throw ToonFormatException.Syntax("Invalid escape sequence: backslash at end of string");

                    var next = value[i + 1];
                    switch (next)
                    {
                        case 'n':
                            sb.Append(Tokens.Newline);
                            i += 2;
                            continue;
                        case 't':
                            sb.Append(Tokens.Tab);
                            i += 2;
                            continue;
                        case 'r':
                            sb.Append(Tokens.CarriageReturn);
                            i += 2;
                            continue;
                        case '\\':
                            sb.Append(Tokens.Backslash);
                            i += 2;
                            continue;
                        case '"':
                            sb.Append(Tokens.DoubleQuote);
                            i += 2;
                            continue;
                        default:
                            throw ToonFormatException.Syntax($"Invalid escape sequence: \\{next}");
                    }
                }

                sb.Append(ch);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 查找从 start 起始的字符串中，考虑转义的下一个双引号位置。
        /// 若未找到返回 -1。等价于 TS findClosingQuote。
        /// </summary>
        internal static int FindClosingQuote(string content, int start)
        {
            int i = start + 1;
            while (i < content.Length)
            {
                // 在引号内部遇到转义，跳过下一个字符
                if (content[i] == Tokens.Backslash && i + 1 < content.Length)
                {
                    i += 2;
                    continue;
                }

                if (content[i] == Tokens.DoubleQuote)
                    return i;

                i++;
            }
            return -1;
        }

        /// <summary>
        /// 查找不在引号内出现的目标字符位置；未找到返回 -1。
        /// 在引号内的转义序列会被跳过。等价于 TS findUnquotedChar。
        /// </summary>
        internal static int FindUnquotedChar(string content, char target, int start = 0)
        {
            bool inQuotes = false;
            int i = start;

            while (i < content.Length)
            {
                if (inQuotes && content[i] == Tokens.Backslash && i + 1 < content.Length)
                {
                    // 引号内的转义，跳过下一个字符
                    i += 2;
                    continue;
                }

                if (content[i] == Tokens.DoubleQuote)
                {
                    inQuotes = !inQuotes;
                    i++;
                    continue;
                }

                if (!inQuotes && content[i] == target)
                    return i;

                i++;
            }

            return -1;
        }

        /// <summary>
        /// 生成带引号的字符串字面量，如果需要则转义内部字符。
        /// 注意：是否需要加引号应由调用方基于 ValidationShared 规则判定。
        /// </summary>
        internal static string Quote(string value)
        {
            return $"\"{EscapeString(value)}\"";
        }
    }
}