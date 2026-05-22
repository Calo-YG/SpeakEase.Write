using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

// 上下文压缩器接口：当对话历史超过 token 预算时，将早期消息压缩为摘要
public interface IContextCompressor
{
    Task<List<ChatMessage>> CompressAsync(
        List<ChatMessage> history,
        string model,
        CancellationToken ct);
}
