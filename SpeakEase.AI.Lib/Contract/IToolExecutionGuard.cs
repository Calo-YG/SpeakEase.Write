using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract;

public interface IToolExecutionGuard
{
    Task<ToolResult> AuthorizeAsync(
        string toolName,
        string arguments,
        CancellationToken cancellationToken = default);
}
