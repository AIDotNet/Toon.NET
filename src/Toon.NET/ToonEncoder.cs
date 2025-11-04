
namespace Toon.Internal.Encode
{
    using System.Text.Json;
    using Toon.Internal.Shared;

    /// <summary>
    /// 编码器入口：从 JsonElement 生成 TOON 文本。
    /// 该类的完整实现将对齐 TypeScript 版本的 encoders.ts/normalize.ts/primitives.ts/writer.ts。
    /// 当前为占位实现，后续提交中逐步替换。
    /// </summary>
    internal static class ToonEncoder
    {
        internal static string Encode(JsonElement element, Toon.ToonSerializerOptions options)
        {
            // TODO: 替换为完整实现：Normalize(JsonElement) -> Encoders -> Writer
            // 现阶段策略：输出可用的 TOON 文本以支撑端到端流程
            // - 原子值: string/number/bool/null
            // - 对象/数组: 使用 Encoders 进行结构化 TOON 编码（非 JSON 回退）
            if (element.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                return Encoders.EncodeValue(element, options);
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => Primitives.EncodeStringLiteral(element.GetString() ?? string.Empty, options),
                JsonValueKind.Number => Primitives.EncodePrimitive(element, options),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => "null",
            };
        }
    }
}
