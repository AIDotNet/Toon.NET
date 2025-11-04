using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIDotNet.Toon
{
    /// <summary>
    /// TOON 的统一选项配置，风格对齐 System.Text.Json。用于控制缩进、分隔符、严格模式、长度标记以及底层 JSON 行为。
    /// </summary>
    public enum ToonDelimiter
    {
        /// <summary>逗号 ,</summary>
        Comma,
        /// <summary>制表符 \t</summary>
        Tab,
        /// <summary>竖线 |</summary>
        Pipe
    }

    /// <summary>
    /// TOON 序列化器选项，提供与 System.Text.Json.JsonSerializerOptions 类似的统一配置入口。
    /// - Indent: 缩进空格数，默认 2
    /// - Delimiter: 分隔符（逗号/制表符/竖线），默认逗号
    /// - Strict: 解码严格模式，默认启用
    /// - LengthMarker: 数组长度标记，可为 '#' 或 null（null 表示不启用）
    /// - JsonOptions: 直通 System.Text.Json 的 JsonSerializerOptions，默认允许命名浮点常量以便后续归一化处理
    /// </summary>
    public sealed class ToonSerializerOptions
    {
        /// <summary>每级缩进的空格数。默认 2。</summary>
        public int Indent { get; init; } = 2;

        /// <summary>用于表格数组和行内原子数组的分隔符。默认逗号。</summary>
        public ToonDelimiter Delimiter { get; init; } = ToonDelimiter.Comma;

        /// <summary>解码严格模式。启用时将验证缩进、行数、空行与多余项等。默认 true。</summary>
        public bool Strict { get; init; } = true;

        /// <summary>可选的数组长度标记。仅支持 '#' 或 null。默认 null（不启用）。</summary>
        public char? LengthMarker { get; init; } = null;

        /// <summary>底层 System.Text.Json 行为配置。默认启用 AllowNamedFloatingPointLiterals 以允许 NaN/Infinity 后续归一化为 null。</summary>
        public JsonSerializerOptions JsonOptions { get; init; }

        /// <summary>构造函数：使用默认 JSON 选项。</summary>
        public ToonSerializerOptions()
        {
            JsonOptions = CreateDefaultJsonOptions();
        }

        /// <summary>构造函数：指定全部选项。</summary>
        public ToonSerializerOptions(
            int indent,
            ToonDelimiter delimiter,
            bool strict = true,
            char? lengthMarker = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            Indent = indent;
            Delimiter = delimiter;
            Strict = strict;
            LengthMarker = lengthMarker;
            JsonOptions = jsonOptions ?? CreateDefaultJsonOptions();
            Validate();
        }

        /// <summary>将枚举分隔符转换为具体字符。</summary>
        internal char GetDelimiterChar() => Delimiter switch
        {
            ToonDelimiter.Comma => ',',
            ToonDelimiter.Tab => '\t',
            ToonDelimiter.Pipe => '|',
            _ => ','
        };

        /// <summary>校验配置是否合法。</summary>
        internal void Validate()
        {
            if (Indent < 0)
                throw new ArgumentOutOfRangeException(nameof(Indent), "Indent must be greater than or equal to 0.");
            if (LengthMarker.HasValue && LengthMarker.Value != '#')
                throw new ArgumentException("LengthMarker must be '#' or null.", nameof(LengthMarker));
        }

        /// <summary>创建默认的 System.Text.Json 选项。</summary>
        private static JsonSerializerOptions CreateDefaultJsonOptions()
        {
            // 允许命名浮点常量，从而在编码阶段可以将 NaN/Infinity 归一化为 null（与 TS 行为一致）
            var opts = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            // 写出时将 double/float 的 NaN/Infinity 规范化为 null，其余数值保持精度
            opts.Converters.Add(new global::Toon.Internal.Converters.DoubleNamedFloatToNullConverter());
            opts.Converters.Add(new global::Toon.Internal.Converters.SingleNamedFloatToNullConverter());
            return opts;
        }

        /// <summary>默认的只读预设。</summary>
        public static ToonSerializerOptions Default { get; } = new ToonSerializerOptions();
    }
}