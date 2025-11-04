#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toon.Internal.Converters
{
    /// <summary>
    /// 将 double 的 NaN/Infinity 在写出 JSON 时规范化为 null，其余保持原始数值精度。
    /// 读取时仍交给默认处理，不做特殊转换。
    /// 目的：与 TS 规范一致（NaN/±Infinity -> null），并为后续 TOON 编码阶段提供稳定的 JsonElement。
    /// </summary>
    internal sealed class DoubleNamedFloatToNullConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteNumberValue(value);
        }
    }

    /// <summary>
    /// 将 float 的 NaN/Infinity 在写出 JSON 时规范化为 null；读取保持默认行为。
    /// </summary>
    internal sealed class SingleNamedFloatToNullConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetSingle();

        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteNumberValue(value);
        }
    }
}