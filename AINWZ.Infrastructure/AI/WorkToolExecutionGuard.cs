using System.Reflection;
using System.Text.Json;

using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Authorization;
using SpeakEase.Write.Application.Abstractions.Identity;

namespace SpeakEase.Write.Infrastructure.AI;

public sealed class WorkToolExecutionGuard(
    IWorkAccessChecker workAccessChecker,
    IUserContext userContext) : IToolExecutionGuard
{
    private static readonly HashSet<string> WorkScopedToolNames = DiscoverWorkScopedToolNames();

    public async Task<ToolResult> AuthorizeAsync(
        string toolName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        if (!WorkScopedToolNames.Contains(toolName))
            return ToolResult.Ok(string.Empty);

        if (!TryGetWorkId(arguments, out var workId) || string.IsNullOrWhiteSpace(workId))
            return ToolResult.Fail("无权访问该作品。", "work_access_denied");

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
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var foundWorkId = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!IsWorkIdProperty(property.Name))
                    continue;

                if (property.Value.ValueKind != JsonValueKind.String)
                    return false;

                var candidateWorkId = property.Value.GetString()?.Trim() ?? string.Empty;
                if (foundWorkId && !string.Equals(workId, candidateWorkId, StringComparison.Ordinal))
                    return false;

                workId = candidateWorkId;
                foundWorkId = true;
            }

            return foundWorkId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsWorkIdProperty(string propertyName)
    {
        return string.Equals(propertyName, "work_id", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(propertyName, "WorkId", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> DiscoverWorkScopedToolNames()
    {
        return typeof(WorkToolExecutionGuard).Assembly
            .GetTypes()
            .Select(type => type.GetField(
                "ToolDefinition",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null))
            .OfType<ToolDefinition>()
            .Where(definition => definition.Function?.Parameters?.Properties?
                .ContainsKey("work_id") == true)
            .Select(definition => definition.Function.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
    }
}
