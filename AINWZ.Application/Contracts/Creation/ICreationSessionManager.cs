using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Contracts.Creation;

public interface ICreationSessionManager
{
    Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId);
    Task<ApiResult<CreationSessionDto>> RecordTurnAsync(string sessionId);
    Task<ApiResult> AdoptContentAsync(string sessionId, AdoptContentRequest request);
    Task<ApiResult<CreationSessionDto>> PauseSessionAsync(string sessionId);
    Task<ApiResult> CancelSessionAsync(string sessionId);
    Task<ApiResult<CreationSessionDto>> ResumeSessionAsync(string sessionId);
    Task<ApiResult> RollbackToTurnAsync(string sessionId, int targetTurn);
    Task<ApiResult<CreationSessionDto>> GetActiveSessionAsync(string workId);
    Task<ApiResult<List<CreationSessionDto>>> ListSessionsAsync(string workId);
    Task<int> ExpireStaleSessionsAsync();
    Task SaveMessagesAsync(string sessionId, int turnNumber, string userMessage, string aiMessage, List<(string ToolName, bool Success, string Content)>? toolResults = null);
    Task<ApiResult<List<SessionMessageResponse>>> GetSessionMessagesAsync(string sessionId, int? limit = null);
}
