#nullable enable
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AIDotNet.Toon.Internal.Encode
{
    /// <summary>
    /// 与 TypeScript 版 encode/encoders.ts 对齐的编码器实现。
    /// 负责将 JsonElement 根据 TOON 规则编码为字符串。
    /// </summary>
    internal static class Encoders
    {
        // #region Encode normalized JsonValue

        internal static string EncodeValue(JsonElement value, ToonSerializerOptions options)
        {
            if (IsJsonPrimitive(value))
            {
                return Primitives.EncodePrimitive(value, options);
            }

            var writer = new LineWriter(options.Indent);

            if (value.ValueKind == JsonValueKind.Array)
            {
                EncodeArray(key: null, value, writer, depth: 0, options);
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                EncodeObject(value, writer, depth: 0, options);
            }

            return writer.ToString();
        }

        // #endregion

        // #region Object encoding

        private static void EncodeObject(JsonElement value, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            foreach (var prop in value.EnumerateObject())
            {
                EncodeKeyValuePair(prop.Name, prop.Value, writer, depth, options);
            }
        }

        private static void EncodeKeyValuePair(string key, JsonElement value, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var encodedKey = Primitives.EncodeKey(key);
            if (IsJsonPrimitive(value))
            {
                writer.Push(depth, $"{encodedKey}: {Primitives.EncodePrimitive(value, options)}");
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                EncodeArray(key, value, writer, depth, options);
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                // 空对象
                var enumerator = value.EnumerateObject().GetEnumerator();
                if (!enumerator.MoveNext())
                {
                    writer.Push(depth, $"{encodedKey}:");
                }
                else
                {
                    writer.Push(depth, $"{encodedKey}:");
                    EncodeObject(value, writer, depth + 1, options);
                }
            }
        }

        // #endregion

        // #region Array encoding

        private static void EncodeArray(string? key, JsonElement value, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            int length = value.GetArrayLength();

            if (length == 0)
            {
                var header = Primitives.FormatHeader(0, options, key: key);
                writer.Push(depth, header);
                return;
            }

            // 原子数组 - 行内
            if (IsArrayOfPrimitives(value))
            {
                var formatted = EncodeInlineArrayLine(value, options, prefix: key);
                writer.Push(depth, formatted);
                return;
            }

            // 数组的数组（所有子数组都是原子数组）
            {
                bool allPrimitivePrimitiveArrays = true;
                foreach (var a in value.EnumerateArray())
                {
                    if (!IsArrayOfPrimitives(a)) { allPrimitivePrimitiveArrays = false; break; }
                }
                if (allPrimitivePrimitiveArrays)
                {
                    EncodeArrayOfArraysAsListItems(key, value, writer, depth, options);
                    return;
                }
            }

            // 对象数组
            if (IsArrayOfObjects(value))
            {
                var headerFields = ExtractTabularHeader(value);
                if (headerFields is not null)
                {
                    EncodeArrayOfObjectsAsTabular(key, value, headerFields, writer, depth, options);
                }
                else
                {
                    EncodeMixedArrayAsListItems(key, value, writer, depth, options);
                }
                return;
            }

            // 混合数组：回退到列表格式
            EncodeMixedArrayAsListItems(key, value, writer, depth, options);
        }

        // #endregion

        // #region Array of arrays (expanded format)

        private static void EncodeArrayOfArraysAsListItems(string? prefix, JsonElement values, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var header = Primitives.FormatHeader(values.GetArrayLength(), options, key: prefix);
            writer.Push(depth, header);

            foreach (var arr in values.EnumerateArray())
            {
                if (IsArrayOfPrimitives(arr))
                {
                    var inline = EncodeInlineArrayLine(arr, options, prefix: null);
                    writer.PushListItem(depth + 1, inline);
                }
            }
        }

        private static string EncodeInlineArrayLine(JsonElement values, ToonSerializerOptions options, string? prefix)
        {
            int len = values.GetArrayLength();
            var header = Primitives.FormatHeader(len, options, key: prefix);

            if (len == 0)
            {
                return header;
            }

            var joined = Primitives.EncodeAndJoinPrimitives(values.EnumerateArray(), options);
            return $"{header} {joined}";
        }

        // #endregion

        // #region Array of objects (tabular format)

        private static void EncodeArrayOfObjectsAsTabular(string? prefix, JsonElement rows, IReadOnlyList<string> header, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var formattedHeader = Primitives.FormatHeader(rows.GetArrayLength(), options, key: prefix, fields: header);
            writer.Push(depth, formattedHeader);

            WriteTabularRows(rows, header, writer, depth + 1, options);
        }

        private static IReadOnlyList<string>? ExtractTabularHeader(JsonElement rows)
        {
            if (rows.GetArrayLength() == 0) return null;

            var first = rows[0];
            if (first.ValueKind != JsonValueKind.Object) return null;

            var firstKeys = new List<string>();
            foreach (var p in first.EnumerateObject())
            {
                firstKeys.Add(p.Name);
            }
            if (firstKeys.Count == 0) return null;

            if (IsTabularArray(rows, firstKeys))
            {
                return firstKeys;
            }

            return null;
        }

        private static bool IsTabularArray(JsonElement rows, IReadOnlyList<string> header)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) return false;

                // 键数必须一致
                int rowKeyCount = 0;
                foreach (var _ in row.EnumerateObject()) rowKeyCount++;
                if (rowKeyCount != header.Count) return false;

                // 必须全部包含 header 的键，并且对应值为原子
                foreach (var key in header)
                {
                    if (!row.TryGetProperty(key, out var v)) return false;
                    if (!IsJsonPrimitive(v)) return false;
                }
            }

            return true;
        }

        private static void WriteTabularRows(JsonElement rows, IReadOnlyList<string> header, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var delimiter = options.GetDelimiterChar();
            foreach (var row in rows.EnumerateArray())
            {
                System.Text.StringBuilder? sb = null;
                for (int i = 0; i < header.Count; i++)
                {
                    var val = row.GetProperty(header[i]);
                    var token = Primitives.EncodePrimitive(val, options);
                    if (i == 0)
                    {
                        sb = new System.Text.StringBuilder(token.Length * header.Count + header.Count - 1);
                        sb.Append(token);
                    }
                    else
                    {
                        sb!.Append(delimiter).Append(token);
                    }
                }
                writer.Push(depth, sb?.ToString() ?? string.Empty);
            }
        }

        // #endregion

        // #region Array of objects (expanded format)

        private static void EncodeMixedArrayAsListItems(string? prefix, JsonElement items, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var header = Primitives.FormatHeader(items.GetArrayLength(), options, key: prefix);
            writer.Push(depth, header);

            foreach (var item in items.EnumerateArray())
            {
                EncodeListItemValue(item, writer, depth + 1, options);
            }
        }

        private static void EncodeObjectAsListItem(JsonElement obj, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            var enumerator = obj.EnumerateObject().GetEnumerator();
            if (!enumerator.MoveNext())
            {
                writer.Push(depth, Tokens.ListItemMarker.ToString());
                return;
            }

            // 第一对 key-value 与 "- " 同行
            var first = enumerator.Current;
            var firstKeyEncoded = Primitives.EncodeKey(first.Name);
            var firstValue = first.Value;

            if (IsJsonPrimitive(firstValue))
            {
                writer.PushListItem(depth, $"{firstKeyEncoded}: {Primitives.EncodePrimitive(firstValue, options)}");
            }
            else if (firstValue.ValueKind == JsonValueKind.Array)
            {
                if (IsArrayOfPrimitives(firstValue))
                {
                    // 原子数组行内格式
                    var inline = EncodeInlineArrayLine(firstValue, options, prefix: first.Name);
                    writer.PushListItem(depth, inline);
                }
                else if (IsArrayOfObjects(firstValue))
                {
                    // 判断能否使用表格格式
                    var header = ExtractTabularHeader(firstValue);
                    if (header is not null)
                    {
                        var formattedHeader = Primitives.FormatHeader(firstValue.GetArrayLength(), options, key: first.Name, fields: header);
                        writer.PushListItem(depth, formattedHeader);
                        WriteTabularRows(firstValue, header, writer, depth + 1, options);
                    }
                    else
                    {
                        // 回退：列表格式
                        writer.PushListItem(depth, $"{firstKeyEncoded}[{firstValue.GetArrayLength()}]:");
                        foreach (var it in firstValue.EnumerateArray())
                        {
                            EncodeObjectAsListItem(it, writer, depth + 1, options);
                        }
                    }
                }
                else
                {
                    // 复杂数组换行
                    writer.PushListItem(depth, $"{firstKeyEncoded}[{firstValue.GetArrayLength()}]:");

                    foreach (var it in firstValue.EnumerateArray())
                    {
                        EncodeListItemValue(it, writer, depth + 1, options);
                    }
                }
            }
            else if (firstValue.ValueKind == JsonValueKind.Object)
            {
                var nestedEnumerator = firstValue.EnumerateObject().GetEnumerator();
                if (!nestedEnumerator.MoveNext())
                {
                    writer.PushListItem(depth, $"{firstKeyEncoded}:");
                }
                else
                {
                    writer.PushListItem(depth, $"{firstKeyEncoded}:");
                    EncodeObject(firstValue, writer, depth + 2, options);
                }
            }

            // 其余键按缩进输出
            while (enumerator.MoveNext())
            {
                var p = enumerator.Current;
                EncodeKeyValuePair(p.Name, p.Value, writer, depth + 1, options);
            }
        }

        // #endregion

        // #region List item encoding helpers

        private static void EncodeListItemValue(JsonElement value, LineWriter writer, int depth, ToonSerializerOptions options)
        {
            if (IsJsonPrimitive(value))
            {
                writer.PushListItem(depth, Primitives.EncodePrimitive(value, options));
            }
            else if (value.ValueKind == JsonValueKind.Array && IsArrayOfPrimitives(value))
            {
                var inline = EncodeInlineArrayLine(value, options, prefix: null);
                writer.PushListItem(depth, inline);
            }
            else if (value.ValueKind == JsonValueKind.Object)
            {
                EncodeObjectAsListItem(value, writer, depth, options);
            }
        }

        // #endregion

        // #region Helpers: type guards

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsJsonPrimitive(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.String
                || value.ValueKind == JsonValueKind.Number
                || value.ValueKind == JsonValueKind.True
                || value.ValueKind == JsonValueKind.False
                || value.ValueKind == JsonValueKind.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsArrayOfPrimitives(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in value.EnumerateArray())
            {
                if (!IsJsonPrimitive(item)) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsArrayOfArrays(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsArrayOfObjects(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Array) return false;
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return false;
            }
            return true;
        }

        // #endregion
    }
}