using System.Runtime.CompilerServices;
using System.Text;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.Exceptions;

namespace SpeakEase.Write.Application.Applications;

public sealed class AgentApplication(
    CreationOrchestrator orchestrator,
    ICreationSessionManager sessionManager) : IAgentApplication
{
    private readonly CreationOrchestrator _orchestrator = orchestrator;
    private readonly ICreationSessionManager _sessionManager = sessionManager;

    public async Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var workId = request.WorkId.Trim();
        var userMessage = ExtractLatestUserMessage(request.Messages);
        var sessionId = await EnsureActiveSessionAsync(workId);
        var contentParts = new List<string>();
        var errorMessage = string.Empty;

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId,
            sessionId,
            userMessage,
            request.MaxIterations,
            request.MaxTokens,
            request.Temperature,
            cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                contentParts.Add(chunk.Content);

            if (chunk.Type == "error" && !string.IsNullOrWhiteSpace(chunk.Content))
                errorMessage = chunk.Content;
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
            BusinessThrow.ThrowException(errorMessage);

        var aiContent = string.Join(string.Empty, contentParts);
        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            userMessage,
            aiContent,
            cancellationToken: cancellationToken);

        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");

        return new AgentResponse
        {
            Content = aiContent,
            StopReason = "completed"
        };
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var workId = request.WorkId.Trim();
        var userMessage = ExtractLatestUserMessage(request.Messages);
        var sessionId = await EnsureActiveSessionAsync(workId);
        var accumulatedContent = new StringBuilder();
        var toolResults = new List<(string ToolName, bool Success, string Content)>();
        var hadError = false;

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId,
            sessionId,
            userMessage,
            request.MaxIterations,
            request.MaxTokens,
            request.Temperature,
            cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                accumulatedContent.Append(chunk.Content);

            if (chunk.Type == "error")
                hadError = true;

            if (chunk.Type == "tool_result" && chunk.ToolResult is { } result)
            {
                var truncated = result.Content?.Length > 500
                    ? result.Content[..500]
                    : result.Content ?? string.Empty;

                toolResults.Add((result.ToolName ?? "tool", result.Success, truncated));
            }

            yield return chunk;
        }

        if (hadError)
            yield break;

        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            userMessage,
            accumulatedContent.ToString(),
            toolResults.Count > 0 ? toolResults : null,
            cancellationToken);

        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");
    }

    private async Task<string> EnsureActiveSessionAsync(string workId)
    {
        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        if (sessionResult.Successed && !string.IsNullOrWhiteSpace(sessionResult.Data?.SessionId))
            return sessionResult.Data.SessionId;

        var startResult = await _sessionManager.StartSessionAsync(workId);
        if (!startResult.Successed || string.IsNullOrWhiteSpace(startResult.Data?.SessionId))
            BusinessThrow.ThrowException(startResult.Message ?? "Unable to create an AI creation session.");

        return startResult.Data.SessionId;
    }

    private static void ValidateRequest(AgentChatRequestDto request)
    {
        if (request is null)
            BusinessThrow.ThrowException("Request cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.WorkId))
            BusinessThrow.ThrowException("WorkId cannot be empty.");

        if (request.Messages == null || request.Messages.Count == 0)
            BusinessThrow.ThrowException("Messages cannot be empty.");

        if (!request.Messages.Any(m => m.Role == "user" && !string.IsNullOrWhiteSpace(m.Content)))
            BusinessThrow.ThrowException("User message cannot be empty.");
    }

    private static string ExtractLatestUserMessage(List<AgentChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
            return string.Empty;

        var lastUserIndex = messages.FindLastIndex(m => m.Role == "user");
        return lastUserIndex >= 0
            ? messages[lastUserIndex].Content
            : string.Empty;
    }
}
