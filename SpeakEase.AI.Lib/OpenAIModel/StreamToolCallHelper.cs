using System.Text.Json;

namespace SpeakEase.AI.Lib.OpenAIModel
{
    /// <summary>
    /// 流式 ToolCall 增量拼接辅助类。
    /// OpenAI 流式响应中 function call 以增量片段形式出现，需要通过索引合并为完整调用。
    /// </summary>
    public static class StreamToolCallHelper
    {
        /// <summary>
        /// 将单个 <see cref="StreamToolCallDelta"/> 合并到累加器字典中。
        /// </summary>
        /// <param name="accumulators">索引 -> 累加状态的字典。</param>
        /// <param name="delta">来自 SSE chunk 的增量片段。</param>
        public static void MergeDelta(IDictionary<int, ToolCallAccumulator> accumulators, StreamToolCallDelta delta)
        {
            if (delta is null)
                return;

            if (!accumulators.TryGetValue(delta.Index, out var acc))
            {
                acc = new ToolCallAccumulator { Index = delta.Index };
                accumulators[delta.Index] = acc;
            }

            if (!string.IsNullOrWhiteSpace(delta.Id))
                acc.Id = delta.Id;

            if (!string.IsNullOrWhiteSpace(delta.Type))
                acc.Type = delta.Type;

            if (delta.Function is not null)
            {
                if (!string.IsNullOrWhiteSpace(delta.Function.Name))
                    acc.Name += delta.Function.Name;

                if (!string.IsNullOrWhiteSpace(delta.Function.Arguments))
                    acc.Arguments += delta.Function.Arguments;
            }
        }

        /// <summary>
        /// 将累加器状态转换为标准的 <see cref="ToolCall"/> 列表。
        /// </summary>
        /// <param name="accumulators">索引 -> 累加状态的字典。</param>
        /// <returns>按索引排序的完整 ToolCall 列表。</returns>
        public static List<ToolCall> ToToolCalls(IDictionary<int, ToolCallAccumulator> accumulators)
        {
            return accumulators
                .OrderBy(kv => kv.Key)
                .Select(kv => new ToolCall
                {
                    Id = kv.Value.Id ?? string.Empty,
                    Type = string.IsNullOrWhiteSpace(kv.Value.Type) ? "function" : kv.Value.Type,
                    Function = new FunctionCallDetail
                    {
                        Name = kv.Value.Name ?? string.Empty,
                        Arguments = kv.Value.Arguments ?? string.Empty
                    }
                })
                .ToList();
        }

        /// <summary>
        /// 将累加器状态转换为流式增量格式的完整列表（用于回填到最后一个 chunk）。
        /// </summary>
        public static List<StreamToolCallDelta> ToStreamToolCallDeltas(IDictionary<int, ToolCallAccumulator> accumulators)
        {
            return accumulators
                .OrderBy(kv => kv.Key)
                .Select(kv => new StreamToolCallDelta
                {
                    Index = kv.Key,
                    Id = kv.Value.Id ?? string.Empty,
                    Type = string.IsNullOrWhiteSpace(kv.Value.Type) ? "function" : kv.Value.Type,
                    Function = new StreamFunctionDelta
                    {
                        Name = kv.Value.Name ?? string.Empty,
                        Arguments = kv.Value.Arguments ?? string.Empty
                    }
                })
                .ToList();
        }

        /// <summary>
        /// 尝试解析累加器中第一个完成的 tool call 的参数为指定类型。
        /// </summary>
        public static T TryParseFirstArguments<T>(IDictionary<int, ToolCallAccumulator> accumulators) where T : class
        {
            var first = accumulators.Values.OrderBy(a => a.Index).FirstOrDefault();
            if (first is null || string.IsNullOrWhiteSpace(first.Arguments))
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(first.Arguments, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 单个 tool call 的流式累加状态。
    /// </summary>
    public sealed class ToolCallAccumulator
    {
        public int Index { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
    }
}
