namespace SpeakEase.Write.Application.Abstractions.Authorization;

public interface IWorkAccessChecker
{
    Task<bool> OwnsWorkAsync(
        string workId,
        string userId,
        CancellationToken cancellationToken = default);
}
