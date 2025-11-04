#nullable enable
using System.Runtime.CompilerServices;
using System.Text;

namespace AIDotNet.Toon.Internal.Encode
{
    /// <summary>
    /// 行写入器：管理缩进与行拼接，生成最终 TOON 文本。
    /// 与 TypeScript 版 encode/writer.ts 的行为一致。
    /// </summary>
    internal sealed class LineWriter
    {
        private readonly List<string> _lines = [];
        private readonly string _indentationString;
        private readonly List<string> _indentCache = [string.Empty];

        /// <summary>
        /// 构造写入器。
        /// </summary>
        /// <param name="indentSize">每级缩进的空格数。</param>
        public LineWriter(int indentSize)
        {
            if (indentSize < 0)
                throw new ArgumentOutOfRangeException(nameof(indentSize), "Indent size must be >= 0.");
            _indentationString = new string(Tokens.Space, indentSize);
        }

        /// <summary>
        /// 添加一行，带指定深度缩进。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(int depth, string content)
        {
            if (depth < 0) depth = 0;
            while (_indentCache.Count <= depth)
            {
                _indentCache.Add(_indentCache[^1] + _indentationString);
            }
            _lines.Add(_indentCache[depth] + content);
        }

        /// <summary>
        /// 添加列表项行：在当前深度位置输出 "- " 前缀。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushListItem(int depth, string content)
        {
            Push(depth, $"{Tokens.ListItemPrefix}{content}");
        }

        public override string ToString()
        {
            return string.Join("\n", _lines);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Repeat(string s, int count)
        {
            if (count <= 0) return string.Empty;
            if (count == 1) return s;

            var sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}