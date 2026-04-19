using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib
{
    /// <summary>
    /// 流式工具调用增量拼接的共享工具类。
    /// 供 ReActLoopStrategy、PlanAndExecuteStrategy 等策略复用。
    /// </summary>
    internal static class StreamToolCallHelper
    {
        /// <summary>
        /// 将流式工具调用增量合并到缓冲区中，逐步拼接完整的工具调用。
        /// </summary>
        /// <param name="bufferMap">按索引存储的缓冲区字典。</param>
        /// <param name="delta">本次接收到的工具调用增量。</param>
        public static void MergeToolCallDelta(IDictionary<int, StreamToolCallBuffer> bufferMap, ToolCallDelta delta)
        {
            if (!bufferMap.TryGetValue(delta.Index, out var buffer))
            {
                buffer = new StreamToolCallBuffer();
                bufferMap[delta.Index] = buffer;
            }

            if (!string.IsNullOrWhiteSpace(delta.Id)) buffer.Id = delta.Id;
            if (!string.IsNullOrWhiteSpace(delta.Type)) buffer.Type = delta.Type;
            if (!string.IsNullOrWhiteSpace(delta.Name))
            {
                buffer.Name ??= string.Empty;
                buffer.Name += delta.Name;
            }
            if (!string.IsNullOrWhiteSpace(delta.Arguments))
            {
                buffer.Arguments ??= string.Empty;
                buffer.Arguments += delta.Arguments;
            }
        }

        /// <summary>
        /// 将缓冲区中的增量拼接结果转换为完整的 ToolCall 列表。
        /// </summary>
        public static List<ToolCall> BuildCompletedToolCalls(IDictionary<int, StreamToolCallBuffer> bufferMap)
        {
            return bufferMap
                .OrderBy(p => p.Key)
                .Select(p => new ToolCall
                {
                    Id = p.Value.Id,
                    Type = string.IsNullOrWhiteSpace(p.Value.Type) ? "function" : p.Value.Type,
                    Function = new ToolFunctionCall
                    {
                        Name = p.Value.Name ?? string.Empty,
                        Arguments = p.Value.Arguments ?? string.Empty
                    }
                })
                .ToList();
        }
    }

    /// <summary>
    /// 流式工具调用增量缓冲区，用于拼接 SSE 分片为完整的工具调用。
    /// </summary>
    internal sealed class StreamToolCallBuffer
    {
        /// <summary>工具调用 ID。</summary>
        public string Id { get; set; }
        /// <summary>调用类型（通常为 "function"）。</summary>
        public string Type { get; set; }
        /// <summary>函数名称（增量拼接）。</summary>
        public string Name { get; set; }
        /// <summary>函数参数 JSON（增量拼接）。</summary>
        public string Arguments { get; set; }
    }
}
