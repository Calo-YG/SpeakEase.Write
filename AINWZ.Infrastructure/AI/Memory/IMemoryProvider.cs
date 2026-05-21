namespace SpeakEase.Write.Infrastructure.AI.Memory;

public interface IMemoryProvider
{
    Task LoadAsync(string userId, string workId, CancellationToken cancellationToken = default);
    Task SaveSnapshotAsync(string userId, string workId, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string userId, string workId, CancellationToken cancellationToken = default);
}
