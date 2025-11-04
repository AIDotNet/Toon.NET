#nullable enable
namespace AIDotNet.Toon.Internal
{
    /// <summary>
    /// TOON 的常量与字符映射，参考 TypeScript 版 constants.ts。
    /// 提供：
    /// - 列表标记、结构符号、括号/大括号、字面量、转义/控制字符
    /// - 分隔符默认值与 ToonDelimiter ↔ char 双向映射
    /// - 常用判断辅助
    /// </summary>
    internal static class Tokens
    {
        // #region List markers
        public const char ListItemMarker = '-';
        public const string ListItemPrefix = "- ";
        // #endregion

        // #region Structural characters
        public const char Comma = ',';
        public const char Colon = ':';
        public const char Space = ' ';
        public const char Pipe = '|';
        public const char Hash = '#';
        // #endregion

        // #region Brackets and braces
        public const char OpenBracket = '[';
        public const char CloseBracket = ']';
        public const char OpenBrace = '{';
        public const char CloseBrace = '}';
        // #endregion

        // #region Literals
        public const string NullLiteral = "null";
        public const string TrueLiteral = "true";
        public const string FalseLiteral = "false";
        // #endregion

        // #region Escape/control characters
        public const char Backslash = '\\';
        public const char DoubleQuote = '"';
        public const char Newline = '\n';
        public const char CarriageReturn = '\r';
        public const char Tab = '\t';
        // #endregion

        // #region Delimiter defaults and mapping
        public const ToonDelimiter DefaultDelimiterEnum = ToonDelimiter.Comma;
        public const char DefaultDelimiterChar = Comma;

        /// <summary>将枚举分隔符映射为具体字符。</summary>
        public static char ToDelimiterChar(ToonDelimiter delimiter) => delimiter switch
        {
            ToonDelimiter.Comma => Comma,
            ToonDelimiter.Tab => Tab,
            ToonDelimiter.Pipe => Pipe,
            _ => Comma
        };

        /// <summary>将字符分隔符映射为枚举；未知字符回退逗号。</summary>
        public static ToonDelimiter FromDelimiterChar(char delimiter) => delimiter switch
        {
            ',' => ToonDelimiter.Comma,
            '\t' => ToonDelimiter.Tab,
            '|' => ToonDelimiter.Pipe,
            _ => ToonDelimiter.Comma
        };

        /// <summary>是否为受支持的分隔符字符。</summary>
        public static bool IsDelimiterChar(char c) => c == Comma || c == Tab || c == Pipe;

        /// <summary>是否为空白字符（空格或制表符）。</summary>
        public static bool IsWhitespace(char c) => c == Space || c == Tab;

        /// <summary>是否为结构性字符。</summary>
        public static bool IsStructural(char c)
            => c == Colon || c == OpenBracket || c == CloseBracket || c == OpenBrace || c == CloseBrace;
        // #endregion
    }
}