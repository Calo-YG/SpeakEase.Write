using System.Text.Json;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Identity;

namespace SpeakEase.Write.Infrastructure.AI;

public sealed class WorkToolExecutionGuard(
    IWorkAccessChecker workAccessChecker,
    IUserContext userContext) : IToolExecutionGuard
{
    public async Task<ToolResult> AuthorizeAsync(
        string toolName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorkId(arguments, out var workId) || string.IsNullOrWhiteSpace(workId))
            return ToolResult.Ok(string.Empty);

        var userId = userContext.UserId;
        if (string.IsNullOrWhiteSpace(userId) ||
            !await workAccessChecker.OwnsWorkAsync(workId, userId, cancellationToken))
        {
            return ToolResult.Fail("无权访问该作品。", "work_access_denied");
        }

        return ToolResult.Ok(string.Empty);
    }

    private static bool TryGetWorkId(string arguments, out string workId)
    {
        workId = string.Empty;
        if (string.IsNullOrWhiteSpace(arguments))
            return false;

        try
        {
            using var document = JsonDocument.Parse(arguments);
            if (!document.RootElement.TryGetProperty("work_id", out var property))
            {
                foreach (var candidate in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(candidate.Name, "work_id", StringComparison.OrdinalIgnoreCase))
                    {
                        property = candidate.Value;
                        break;
                    }
                }
            }

            if (property.ValueKind != JsonValueKind.String)
                return false;

            workId = property.GetString() ?? string.Empty;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
