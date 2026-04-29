namespace SpeakEase.Write.Infrastructure.AI.Memory;

public interface IMemoryProvider
{
    Task<MemoryContext> LoadAsync(string userId, string workId, CancellationToken cancellationToken = default);
    void SaveSnapshot(string userId, string workId, MemoryContext ctx);
    void Invalidate(string userId, string workId);
}
