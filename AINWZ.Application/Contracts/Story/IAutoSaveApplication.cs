using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Story;

public interface IAutoSaveApplication
{
    Task<ApiResult> AutoSaveAsync(AutoSaveRequest request, CancellationToken cancellationToken = default);
}
