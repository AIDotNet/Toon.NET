using System.Globalization;
using System.Text;
using AIDotNet.Toon.Internal;
using AIDotNet.Toon.Internal.Shared;

namespace AIDotNet.Toon
{
    /// <summary>
    /// 解码器入口：从 TOON 文本生成 JSON 字符串。
    /// 该类的完整实现将对齐 TypeScript 版本的 scanner.ts/parser.ts/decoders.ts/validation.ts。
    /// 当前为占位实现，后续提交中逐步替换。
    /// </summary>
    internal static class ToonDecoder
    {
        internal static string DecodeToJsonString(string toon, ToonSerializerOptions options)
        {
            // TODO：完整实现将替换为 Scanner -> Parser/Decoders -> JSON DOM
            // 当前增强（MVP+）：
            // - 纯原子与完整字符串字面量
            // - 单行 key: value
            // - 头部数组（行内原子数组、列表数组、表格数组）：支持 [#N] 与可选非默认分隔符、可选字段集 {a,b}
            // - 简单多行平铺对象（多行 key: value）
            // - 严格模式的最小计数校验（items/rows 与 header.length）
            //
            // 说明：此实现为过渡版本，帮助尽早打通解码主要形态；后续将迁移到严格的词法/语法分析管线。
            var t = toon?.Trim() ?? throw new ArgumentNullException(nameof(toon));
            if (t.Length == 0)
                throw new ArgumentException("Cannot decode empty input.", nameof(toon));

            // 单行路径：优先尝试 header（行内数组），否则按原子/kv/字符串处理
            if (t.IndexOf('\n') < 0 && TryParseHeaderLine(t, out var hKey, out var hLen, out var hDelim, out var hFields, out var hInline))
            {
                if (hFields is not null && hFields.Count > 0)
                {
                    // 单行表格头部通常不承载就地数据；若长度非 0，则无法在单行还原 -> 退回字符串或在 strict 下报错
                    if (options.Strict && hLen != 0)
                        throw ToonFormatException.Syntax("Tabular header requires subsequent rows.");
                    var arrayJsonEmpty = "[]";
                    return hKey is null ? arrayJsonEmpty : "{" + QuoteJson(hKey) + ":" + arrayJsonEmpty + "}";
                }
                else
                {
                    // 行内原子数组：key[N]: v1<delim>v2...
                    var values = new List<string>();
                    if (!string.IsNullOrEmpty(hInline))
                    {
                        var toks = SplitByUnquoted(hInline, hDelim);
                        foreach (var tok in toks)
                        {
                            var v = tok.Trim();
                            if (v.Length == 0) continue;
                            values.Add(DecodeValueToken(v));
                        }
                    }
                    if (options.Strict && hLen != values.Count)
                        throw ToonFormatException.Syntax("Inline primitive array item count does not match header length.");

                    var arrayJson = "[" + string.Join(",", values) + "]";
                    return hKey is null ? arrayJson : "{" + QuoteJson(hKey) + ":" + arrayJson + "}";
                }
            }

            // 多行路径：尝试 header + 列表/表格；若无 header，则尝试平铺对象；否则回退
            if (t.IndexOf('\n') >= 0)
            {
                var normalized = t.Replace("\r\n", "\n").Replace("\r", "\n");
                var lines = normalized.Split('\n');
                var first = lines[0].TrimEnd();

                if (TryParseHeaderLine(first, out var key, out var len, out var delim, out var fields, out var inline))
                {
                    if (fields is not null && fields.Count > 0)
                    {
                        // 表格数组：[#]N{fields}:
                        var arrItems = new List<string>();
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var raw = lines[i];

                            if (options.Strict)
                            {
                                int k = 0;
                                int spaceCount = 0;
                                while (k < raw.Length && (raw[k] == Tokens.Space || raw[k] == Tokens.Tab))
                                {
                                    if (raw[k] == Tokens.Tab)
                                        throw ToonFormatException.Syntax("TAB indentation is not allowed in strict mode.");
                                    spaceCount++;
                                    k++;
                                }
                                if (spaceCount > 0 && options.Indent > 0 && (spaceCount % options.Indent != 0))
                                    throw ToonFormatException.Syntax("Indentation must be multiples of indent size in strict mode.");
                            }

                            var line = raw.Trim();
                            if (line.Length == 0)
                            {
                                if (options.Strict) throw ToonFormatException.Syntax("Blank line is not allowed inside tabular rows in strict mode.");
                                continue;
                            }

                            var tokens = SplitByUnquoted(line, delim);
                            if (options.Strict && tokens.Count != fields.Count)
                                throw ToonFormatException.Syntax("Tabular row field count mismatch.");

                            var sb = new StringBuilder();
                            sb.Append('{');
                            for (int c = 0; c < fields.Count; c++)
                            {
                                if (c > 0) sb.Append(',');
                                var fieldPlain = fields[c];
                                var prop = QuoteJson(fieldPlain);
                                string cellToken = c < tokens.Count ? tokens[c].Trim() : string.Empty;
                                var valJson = DecodeValueToken(cellToken);
                                sb.Append(prop).Append(':').Append(valJson);
                            }
                            sb.Append('}');
                            arrItems.Add(sb.ToString());
                        }

                        if (options.Strict && arrItems.Count != len)
                            throw ToonFormatException.Syntax("Tabular rows count does not match header length.");

                        var arrayJson = "[" + string.Join(",", arrItems) + "]";
                        return key is null ? arrayJson : "{" + QuoteJson(key) + ":" + arrayJson + "}";
                    }
                    else
                    {
                        // 列表数组或行内 + 列表追加
                        var values = new List<string>();
                        if (!string.IsNullOrEmpty(inline))
                        {
                            var toks = SplitByUnquoted(inline, delim);
                            foreach (var tok in toks)
                            {
                                var v = tok.Trim();
                                if (v.Length == 0) continue;
                                values.Add(DecodeValueToken(v));
                            }
                        }

                        for (int i = 1; i < lines.Length; i++)
                        {
                            var raw = lines[i];
                            var s = raw.TrimStart();
                            if (s.Length == 0)
                            {
                                if (options.Strict) throw ToonFormatException.Syntax("Blank line is not allowed inside list arrays in strict mode.");
                                continue;
                            }

                            if (options.Strict)
                            {
                                int k = 0;
                                int spaceCount = 0;
                                while (k < raw.Length && (raw[k] == Tokens.Space || raw[k] == Tokens.Tab))
                                {
                                    if (raw[k] == Tokens.Tab)
                                        throw ToonFormatException.Syntax("TAB indentation is not allowed in strict mode.");
                                    spaceCount++;
                                    k++;
                                }
                                if (spaceCount > 0 && options.Indent > 0 && (spaceCount % options.Indent != 0))
                                    throw ToonFormatException.Syntax("Indentation must be multiples of indent size in strict mode.");
                            }

                            if (s.Length >= 2 && s[0] == Tokens.ListItemMarker && s[1] == Tokens.Space)
                            {
                                var itemText = s.Substring(2).Trim();
                                if (itemText.Length == 0) { values.Add("null"); continue; }
                                values.Add(DecodeValueToken(itemText));
                            }
                        }

                        if (options.Strict && len != values.Count)
                            throw ToonFormatException.Syntax("List array item count does not match header length.");

                        var arrayJson = "[" + string.Join(",", values) + "]";
                        return key is null ? arrayJson : "{" + QuoteJson(key) + ":" + arrayJson + "}";
                    }
                }

                // 无 header：尝试多行平铺对象（每行 "key: value"）
                {
                    var pairs = new List<string>();
                    bool seenAnyKv = false;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0)
                        {
                            if (options.Strict) throw ToonFormatException.Syntax("Blank line not allowed in flat object in strict mode.");
                            continue;
                        }

                        var idx = StringUtils.FindUnquotedChar(trimmed, Tokens.Colon, 0);
                        if (idx <= 0) { seenAnyKv = false; break; }
                        seenAnyKv = true;

                        var left = trimmed.Substring(0, idx).Trim();
                        var right = trimmed.Substring(idx + 1).Trim();

                        var keyPlain = DecodeKeyPlain(left);
                        var keyJson = QuoteJson(keyPlain);
                        var valJson = DecodeValueToken(right);
                        pairs.Add($"{keyJson}:{valJson}");
                    }

                    if (seenAnyKv && pairs.Count > 0)
                    {
                        return "{" + string.Join(",", pairs) + "}";
                    }
                }

                // 多行但不符合已知结构：以整体字符串回退
                var escapedMl = StringUtils.EscapeString(t);
                return $"\"{escapedMl}\"";
            }

            // 单行（非 header）：字符串字面量 / 原子 / 单行对象 / 字符串回退

            // 1) 完整带引号字符串字面量（确保首尾成对且考虑转义）
            if (t.Length >= 2 && t[0] == Tokens.DoubleQuote)
            {
                var closing = StringUtils.FindClosingQuote(t, 0);
                if (closing == t.Length - 1)
                {
                    var inner = t.Substring(1, t.Length - 2);
                    var unescaped = StringUtils.UnescapeString(inner);
                    var escapedForJson = StringUtils.EscapeString(unescaped);
                    return $"\"{escapedForJson}\"";
                }
            }

            // 2) 原子：null/true/false/number（与编码端保持一致）
            if (LiteralUtils.IsBooleanOrNullLiteral(t))
                return t;
            if (LiteralUtils.IsNumericLiteral(t))
                return t;

            // 3) 单行对象：key: value
            {
                var colon = StringUtils.FindUnquotedChar(t, Tokens.Colon, 0);
                if (colon > 0)
                {
                    var left = t.Substring(0, colon).Trim();
                    var right = t.Substring(colon + 1).Trim();

                    var keyPlain = DecodeKeyPlain(left);
                    var keyJson = QuoteJson(keyPlain);
                    var valJson = DecodeValueToken(right);
                    return $"{{{keyJson}:{valJson}}}";
                }
            }

            // 4) 其他：按字符串回退（加入引号并规范转义）
            var escapedFallback = StringUtils.EscapeString(t);
            return $"\"{escapedFallback}\"";

            // ========== 本地辅助函数 ==========
            static string QuoteJson(string plain)
            {
                var escaped = StringUtils.EscapeString(plain ?? string.Empty);
                return $"\"{escaped}\"";
            }

            static string DecodeKeyPlain(string keyToken)
            {
                var k = keyToken.Trim();
                if (k.Length >= 2 && k[0] == Tokens.DoubleQuote)
                {
                    var end = StringUtils.FindClosingQuote(k, 0);
                    if (end == k.Length - 1)
                    {
                        var inner = k.Substring(1, k.Length - 2);
                        return StringUtils.UnescapeString(inner);
                    }
                }
                return k;
            }

            static string DecodeValueToken(string token)
            {
                var v = token.Trim();
                if (v.Length >= 2 && v[0] == Tokens.DoubleQuote)
                {
                    var end = StringUtils.FindClosingQuote(v, 0);
                    if (end == v.Length - 1)
                    {
                        var inner = v.Substring(1, v.Length - 2);
                        var unescaped = StringUtils.UnescapeString(inner);
                        return QuoteJson(unescaped);
                    }
                }
                if (LiteralUtils.IsBooleanOrNullLiteral(v) || LiteralUtils.IsNumericLiteral(v))
                    return v;

                // 非字面 token 作为字符串处理
                return QuoteJson(v);
            }

            static bool TryParseHeaderLine(
                string line,
                out string? keyPlain,
                out int length,
                out char delimiter,
                out List<string>? fields,
                out string inlineValues)
            {
                keyPlain = null;
                length = 0;
                delimiter = Tokens.DefaultDelimiterChar;
                fields = null;
                inlineValues = string.Empty;

                var s = line.Trim();
                if (s.Length == 0) return false;

                // 定位 '[' 与 ']'
                int lb = s.IndexOf(Tokens.OpenBracket);
                if (lb < 0) return false;
                int rb = s.IndexOf(Tokens.CloseBracket, lb + 1);
                if (rb < 0) return false;

                // 前缀键
                var prefix = s.Substring(0, lb).Trim();
                if (prefix.Length > 0)
                {
                    keyPlain = DecodeKeyPlain(prefix);
                }

                // 解析方括号内部：[#]N[delimiter?]
                var inside = s.Substring(lb + 1, rb - lb - 1);
                bool hasHash = inside.Length > 0 && inside[0] == Tokens.Hash;
                int idx = hasHash ? 1 : 0;

                // 读取长度数字
                int i = idx;
                while (i < inside.Length && inside[i] >= '0' && inside[i] <= '9') i++;
                if (i == idx) return false; // 必须有数字
                var numText = inside.Substring(idx, i - idx);
                if (!int.TryParse(numText, NumberStyles.None, CultureInfo.InvariantCulture, out length))
                    return false;

                // 可选 bracket 内分隔符
                if (i < inside.Length)
                {
                    delimiter = inside[i];
                }
                else
                {
                    delimiter = Tokens.DefaultDelimiterChar;
                }

                // 可选字段集 {a,b,...}
                int braceStart = s.IndexOf(Tokens.OpenBrace, rb + 1);
                int braceEnd = braceStart >= 0 ? s.IndexOf(Tokens.CloseBrace, braceStart + 1) : -1;

                int colon = StringUtils.FindUnquotedChar(s, Tokens.Colon, rb + 1);
                if (colon < 0) return false;

                if (braceStart >= 0 && braceEnd > braceStart && braceEnd < colon)
                {
                    var inner = s.Substring(braceStart + 1, braceEnd - braceStart - 1);
                    var rawFields = SplitByUnquoted(inner, delimiter);
                    if (rawFields.Count <= 1 && delimiter != Tokens.DefaultDelimiterChar && inner.IndexOf(Tokens.Comma) >= 0)
                    {
                        // 兼容：方括号使用非默认分隔符（如 '|'）但字段集仍用逗号分隔的写法
                        rawFields = SplitByUnquoted(inner, Tokens.DefaultDelimiterChar);
                    }
                    var list = new List<string>();
                    foreach (var rf in rawFields)
                    {
                        var f = rf.Trim();
                        if (f.Length == 0) continue;
                        list.Add(DecodeKeyPlain(f));
                    }
                    fields = list;
                }

                // header 行内值
                inlineValues = s.Substring(colon + 1).Trim();
                return true;
            }

            static List<string> SplitByUnquoted(string content, char delim)
            {
                var list = new List<string>();
                if (string.IsNullOrEmpty(content))
                {
                    return list;
                }

                bool inQuotes = false;
                int start = 0;
                int i = 0;
                while (i < content.Length)
                {
                    var ch = content[i];
                    if (inQuotes && ch == Tokens.Backslash && i + 1 < content.Length)
                    {
                        i += 2; // 跳过转义
                        continue;
                    }

                    if (ch == Tokens.DoubleQuote)
                    {
                        inQuotes = !inQuotes;
                        i++;
                        continue;
                    }

                    if (!inQuotes && ch == delim)
                    {
                        list.Add(content.Substring(start, i - start));
                        i++;
                        start = i;
                        continue;
                    }

                    i++;
                }

                // 尾段
                if (start <= content.Length)
                {
                    list.Add(content.Substring(start));
                }

                return list;
            }
        }
    }
}