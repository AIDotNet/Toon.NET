#nullable enable
using System.Text;

namespace AIDotNet.Toon
{
    /// <summary>
    /// TOON 解析与验证异常。对齐 System.Text.Json 的异常体验，提供行号与列号信息，便于定位。
    /// </summary>
    public sealed class ToonFormatException : Exception
    {
        /// <summary>错误类型（语法、范围、校验、缩进、分隔符、未知）。</summary>
        public ToonErrorKind Kind { get; }

        /// <summary>1-based 行号。</summary>
        public int? LineNumber { get; }

        /// <summary>1-based 列号。</summary>
        public int? ColumnNumber { get; }

        /// <summary>出现错误的原始行文本（可截断）。</summary>
        public string? SourceLine { get; }

        /// <summary>缩进深度（可选，调试信息）。</summary>
        public int? Depth { get; }

        /// <summary>构造异常，自动拼接带位置与行上下文的消息。</summary>
        public ToonFormatException(
            ToonErrorKind kind,
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            : base(BuildMessage(kind, message, lineNumber, columnNumber, sourceLine), inner)
        {
            Kind = kind;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            SourceLine = sourceLine;
            Depth = depth;
        }

        /// <summary>语法错误工厂方法。</summary>
        public static ToonFormatException Syntax(
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            => new(ToonErrorKind.Syntax, message, lineNumber, columnNumber, sourceLine, depth, inner);

        /// <summary>范围错误工厂方法（数目不匹配等）。</summary>
        public static ToonFormatException Range(
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            => new(ToonErrorKind.Range, message, lineNumber, columnNumber, sourceLine, depth, inner);

        /// <summary>校验错误工厂方法（多余行/空行/结构规则）。</summary>
        public static ToonFormatException Validation(
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            => new(ToonErrorKind.Validation, message, lineNumber, columnNumber, sourceLine, depth, inner);

        /// <summary>缩进错误工厂方法（严格模式下缩进必须为 indent 整数倍且不可含 TAB）。</summary>
        public static ToonFormatException Indentation(
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            => new(ToonErrorKind.Indentation, message, lineNumber, columnNumber, sourceLine, depth, inner);

        /// <summary>分隔符相关错误工厂方法。</summary>
        public static ToonFormatException Delimiter(
            string message,
            int? lineNumber = null,
            int? columnNumber = null,
            string? sourceLine = null,
            int? depth = null,
            Exception? inner = null)
            => new(ToonErrorKind.Delimiter, message, lineNumber, columnNumber, sourceLine, depth, inner);

        private static string BuildMessage(
            ToonErrorKind kind,
            string message,
            int? lineNumber,
            int? columnNumber,
            string? sourceLine)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(kind).Append("] ").Append(message);

            if (lineNumber is not null)
                sb.Append(" (Line ").Append(lineNumber.Value).Append(')');
            if (columnNumber is not null)
                sb.Append(" (Column ").Append(columnNumber.Value).Append(')');

            if (!string.IsNullOrEmpty(sourceLine))
            {
                sb.AppendLine();
                sb.Append("  > ").Append(sourceLine);

                if (columnNumber is not null && columnNumber.Value > 0)
                {
                    sb.AppendLine();
                    sb.Append("    ");
                    // 指向列位置的插入符号
                    var caretPos = Math.Max(1, columnNumber.Value);
                    sb.Append(new string(' ', caretPos - 1)).Append('^');
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>TOON 错误类型分类。</summary>
    public enum ToonErrorKind
    {
        /// <summary>语法错误：扫描/解析阶段遇到非法令牌或结构。</summary>
        Syntax,
        /// <summary>范围错误：数量不匹配（如 [N] 与实际项数不一致）。</summary>
        Range,
        /// <summary>校验错误：严格模式下的结构/规则校验失败（如多余行、空行等）。</summary>
        Validation,
        /// <summary>缩进错误：缩进不是 Indent 的整数倍或包含 TAB。</summary>
        Indentation,
        /// <summary>分隔符错误：字段/值包含不允许的分隔符或分隔推断失败。</summary>
        Delimiter,
        /// <summary>未知错误：未归类的异常。</summary>
        Unknown
    }
}