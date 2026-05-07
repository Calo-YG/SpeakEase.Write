using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.Write.Infrastructure.AI.Context;

public interface IContextCompressor
{
    Task<List<ChatMessage>> CompressAsync(
        List<ChatMessage> history,
        string model,
        CancellationToken ct);
}
