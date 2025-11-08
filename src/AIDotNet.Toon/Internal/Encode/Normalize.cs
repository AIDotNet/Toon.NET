#nullable enable
using System.Collections;
using System.Text.Json.Nodes;

namespace AIDotNet.Toon.Internal.Encode
{
    /// <summary>
    /// Normalization utilities for converting arbitrary .NET objects to JsonNode representations
    /// and type guards for JSON value classification.
    /// Aligned with TypeScript encode/normalize.ts
    /// </summary>
    internal static class Normalize
    {
        // #region Normalization (object → JsonNode)

        /// <summary>
        /// Normalizes an arbitrary .NET value to a JsonNode representation.
        /// Handles primitives, collections, dates, and custom objects.
        /// </summary>
        public static JsonNode? NormalizeValue(object? value)
        {
            // 1. null
            if (value == null) return null;

            // 2. 已经是 JsonNode 直接返回，防止重复包装
            if (value is JsonNode jsonNode) return jsonNode;

            // 3. 原有的基础类型处理保持不变
            // Primitives: string, boolean
            if (value is string str) return JsonValue.Create(str);
            if (value is bool b) return JsonValue.Create(b);

            // Numbers: canonicalize -0 to 0, handle NaN and Infinity
            if (value is double d)
            {
                if (d == 0.0 && double.IsNegative(d)) return JsonValue.Create(0.0);
                if (!double.IsFinite(d)) return null;
                return JsonValue.Create(d);
            }
            if (value is float f)
            {
                if (f == 0.0f && float.IsNegative(f)) return JsonValue.Create(0.0f);
                if (!float.IsFinite(f)) return null;
                return JsonValue.Create(f);
            }
            // Other numeric types
            if (value is int i) return JsonValue.Create(i);
            if (value is long l) return JsonValue.Create(l);
            if (value is decimal dec) return JsonValue.Create(dec);
            if (value is byte by) return JsonValue.Create(by);
            if (value is sbyte sb) return JsonValue.Create(sb);
            if (value is short sh) return JsonValue.Create(sh);
            if (value is ushort us) return JsonValue.Create(us);
            if (value is uint ui) return JsonValue.Create(ui);
            if (value is ulong ul) return JsonValue.Create(ul);

            // DateTime → ISO string
            if (value is DateTime dt) return JsonValue.Create(dt.ToString("O")); // ISO 8601 format
            if (value is DateTimeOffset dto) return JsonValue.Create(dto.ToString("O"));

            // 4. 字典优先（避免先匹配 IEnumerable 导致枚举 KeyValuePair 再反射）
            if (value is IDictionary dict)
            {
                var jsonObject = new JsonObject();
                foreach (DictionaryEntry entry in dict)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    jsonObject[key] = NormalizeValue(entry.Value);
                }
                return jsonObject;
            }

            // 5. IEnumerable（排除 string / JsonNode / IDictionary）
            if (value is IEnumerable enumerable)
            {
                var jsonArray = new JsonArray();
                foreach (var item in enumerable)
                {
                    jsonArray.Add(NormalizeValue(item));
                }
                return jsonArray;
            }

            // 6. 排除 KeyValuePair 避免反射生成 {Key,Value} 嵌套导致深度结构
            var type = value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                // 直接转成 JsonObject { key: NormalizeValue(value) } 的简单形式
                var keyProp = type.GetProperty("Key");
                var valProp = type.GetProperty("Value");
                var keyObj = keyProp?.GetValue(value)?.ToString() ?? string.Empty;
                var valObj = NormalizeValue(valProp?.GetValue(value));
                var obj = new JsonObject { [keyObj] = valObj };
                return obj;
            }

            // 7. Plain object 反射
            if (IsPlainObject(value))
            {
                var jsonObject = new JsonObject();
                var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props.Where(p => p.CanRead))
                {
                    jsonObject[prop.Name] = NormalizeValue(prop.GetValue(value));
                }
                return jsonObject;
            }

            return null;
        }

        /// <summary>
        /// Normalizes a value of generic type to a JsonNode representation.
        /// This overload aims to avoid an initial boxing for common value types.
        /// </summary>
        public static JsonNode? NormalizeValue<T>(T value)
        {
            if (value is null) return null;

            // JsonNode 直接返回
            if (value is JsonNode jn) return jn;

            // 快路径：常见基元类型
            switch (value)
            {
                case string s:
                    return JsonValue.Create(s);
                case bool b:
                    return JsonValue.Create(b);
                case int i:
                    return JsonValue.Create(i);
                case long l:
                    return JsonValue.Create(l);
                case double d:
                    if (d == 0.0 && double.IsNegative(d)) return JsonValue.Create(0.0);
                    if (!double.IsFinite(d)) return null;
                    return JsonValue.Create(d);
                case float f:
                    if (f == 0.0f && float.IsNegative(f)) return JsonValue.Create(0.0f);
                    if (!float.IsFinite(f)) return null;
                    return JsonValue.Create(f);
                case decimal dec:
                    return JsonValue.Create(dec);
                case byte by:
                    return JsonValue.Create(by);
                case sbyte sb:
                    return JsonValue.Create(sb);
                case short sh:
                    return JsonValue.Create(sh);
                case ushort us:
                    return JsonValue.Create(us);
                case uint ui:
                    return JsonValue.Create(ui);
                case ulong ul:
                    return JsonValue.Create(ul);
                case DateTime dt:
                    return JsonValue.Create(dt.ToString("O"));
                case DateTimeOffset dto:
                    return JsonValue.Create(dto.ToString("O"));
            }

            // 先处理字典
            if (value is IDictionary dict)
            {
                var jsonObject = new JsonObject();
                foreach (DictionaryEntry entry in dict)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    jsonObject[key] = NormalizeValue(entry.Value);
                }
                return jsonObject;
            }

            // 再处理 IEnumerable（排除 string/JsonNode/IDictionary 已经在上面处理）
            if (value is IEnumerable enumerable)
            {
                var jsonArray = new JsonArray();
                foreach (var item in enumerable)
                {
                    jsonArray.Add(NormalizeValue(item));
                }
                return jsonArray;
            }

            // 排除 KeyValuePair 作为普通对象
            var type = value!.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                var keyProp = type.GetProperty("Key");
                var valProp = type.GetProperty("Value");
                var keyObj = keyProp?.GetValue(value)?.ToString() ?? string.Empty;
                var valObj = NormalizeValue(valProp?.GetValue(value));
                var obj = new JsonObject { [keyObj] = valObj };
                return obj;
            }

            // 普通对象反射
            if (IsPlainObject(value!))
            {
                var jsonObject = new JsonObject();
                var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (prop.CanRead)
                    {
                        jsonObject[prop.Name] = NormalizeValue(prop.GetValue(value));
                    }
                }
                return jsonObject;
            }

            return null;
        }

        /// <summary>
        /// Determines if a value is a plain object (not a primitive, collection, or special type).
        /// </summary>
        private static bool IsPlainObject(object value)
        {
            if (value == null) return false;
            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset)) return false;
            if (typeof(IEnumerable).IsAssignableFrom(type)) return false;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) return false;
            return type.IsClass || type.IsValueType;
        }

        // #endregion

        // #region Type guards

        /// <summary>
        /// Checks if a JsonNode is a primitive value (null, string, number, or boolean).
        /// </summary>
        public static bool IsJsonPrimitive(JsonNode? value)
        {
            if (value == null)
                return true;

            if (value is JsonValue jsonValue)
            {
                // Check if it's a primitive type
                return jsonValue.TryGetValue<string>(out _)
                    || jsonValue.TryGetValue<bool>(out _)
                    || jsonValue.TryGetValue<int>(out _)
                    || jsonValue.TryGetValue<long>(out _)
                    || jsonValue.TryGetValue<double>(out _)
                    || jsonValue.TryGetValue<decimal>(out _);
            }

            return false;
        }

        /// <summary>
        /// Checks if a JsonNode is a JsonArray.
        /// </summary>
        public static bool IsJsonArray(JsonNode? value)
        {
            return value is JsonArray;
        }

        /// <summary>
        /// Checks if a JsonNode is a JsonObject.
        /// </summary>
        public static bool IsJsonObject(JsonNode? value)
        {
            return value is JsonObject;
        }

        // #endregion

        // #region Array type detection

        /// <summary>
        /// Checks if a JsonArray contains only primitive values.
        /// </summary>
        public static bool IsArrayOfPrimitives(JsonArray array)
        {
            return array.All(item => IsJsonPrimitive(item));
        }

        /// <summary>
        /// Checks if a JsonArray contains only arrays.
        /// </summary>
        public static bool IsArrayOfArrays(JsonArray array)
        {
            return array.All(item => IsJsonArray(item));
        }

        /// <summary>
        /// Checks if a JsonArray contains only objects.
        /// </summary>
        public static bool IsArrayOfObjects(JsonArray array)
        {
            return array.All(item => IsJsonObject(item));
        }

        // #endregion
    }
}
