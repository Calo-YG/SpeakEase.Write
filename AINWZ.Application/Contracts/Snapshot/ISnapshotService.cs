using SpeakEase.Write.Application.Contracts.Snapshot.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Snapshot;

public interface ISnapshotService
{
    Task<string> CaptureBeforeSnapshotAsync(string workId, string correlationId);
    Task<string> CaptureAfterSnapshotAsync(string workId, string correlationId);
    Task<ApiResult<bool>> RestoreSnapshotAsync(string snapshotId);
    Task<ApiResult<bool>> RestoreLastAfterSnapshotAsync(string workId);
    Task<ApiResult<bool>> UndoLastAgentRunAsync(string workId);
    Task<ApiResult<List<SnapshotSummaryDto>>> ListSnapshotsAsync(string workId);
    Task<int> ArchiveOldSnapshotsAsync();
}
