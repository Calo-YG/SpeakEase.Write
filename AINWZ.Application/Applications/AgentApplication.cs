using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Infrastructure.AI.Memory;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.Exceptions;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Application.Applications;

public sealed class AgentApplication(
    CreationOrchestrator orchestrator,
    ICreationSessionManager sessionManager,
    SpeakEaseDbContext dbContext,
    IUserContext userContext,
    ISnowflakeIdGenerator snowflakeIdGenerator,
    IMemoryProvider  memoryProvider) : IAgentApplication
{
    private readonly CreationOrchestrator _orchestrator = orchestrator;
    private readonly ICreationSessionManager _sessionManager = sessionManager;

    public async Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var workId = request.WorkId ?? string.Empty;
        var (userMessage, history) = ExtractMessages(request.Messages);

        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);
        string sessionId = sessionResult.Data?.SessionId ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        var contentParts = new List<string>();

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, userMessage, history, cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                contentParts.Add(chunk.Content);
        }

        if (sessionId != null)
        {
            var aiContent = string.Join(string.Empty, contentParts);
            var recordResult = await _sessionManager.RecordTurnAsync(sessionId);
            var turnNumber = recordResult.Data?.TurnCount ?? 0;
            await _sessionManager.SaveMessagesAsync(sessionId, turnNumber, userMessage, aiContent);
        }

        return new AgentResponse
        {
            Content = string.Join(string.Empty, contentParts),
            StopReason = "completed"
        };
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var workId = request.WorkId ?? string.Empty;

        var (userMessage, history) = ExtractMessages(request.Messages);

        var sessionResult = await _sessionManager.GetActiveSessionAsync(workId);

        string sessionId = sessionResult.Data?.SessionId
                           ?? (await _sessionManager.StartSessionAsync(workId)).Data?.SessionId;

        var accumulatedContent = new System.Text.StringBuilder();

        var toolResults = new List<(string ToolName, bool Success, string Content)>();

        await foreach (var chunk in _orchestrator.ExecuteAsync(
            workId, userMessage, history, cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                accumulatedContent.Append(chunk.Content);

            if (chunk.Type == "tool_result" && chunk.ToolResult is { } tr)
            {
                var truncated = tr.Content?.Length > 500
                    ? tr.Content[..500]
                    : tr.Content ?? string.Empty;
                toolResults.Add((tr.ToolName ?? "tool", tr.Success, truncated));
            }

            yield return chunk;
        }

        if (sessionId != null)
        {
            var aiContent = accumulatedContent.ToString();

            var recordResult = await _sessionManager.RecordTurnAsync(sessionId);

            var turnNumber = recordResult.Data?.TurnCount ?? 0;

            await _sessionManager.SaveMessagesAsync(
                sessionId, turnNumber, userMessage, aiContent,
                toolResults.Count > 0 ? toolResults : null);
        }
    }

    private static void ValidateRequest(AgentChatRequestDto request)
    {
        if (request.Messages == null || request.Messages.Count == 0)
            BusinessThrow.ThrowException("消息列表不能为空。");
    }

    private static (string userMessage, List<ChatMessage> history) ExtractMessages(
        List<AgentChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
            return (string.Empty, new List<ChatMessage>());

        var lastUserIndex = messages.FindLastIndex(m => m.Role == "user");
        var history = new List<ChatMessage>();
        for (int i = 0; i < lastUserIndex; i++)
        {
            var m = messages[i];
            if (m.Role == "user")
                history.Add(ChatMessage.User(m.Content));
            else if (m.Role == "assistant")
                history.Add(ChatMessage.Assistant(m.Content));
        }

        var lastUserMsg = string.Empty;
        if (lastUserIndex >= 0)
            lastUserMsg = messages[lastUserIndex].Content;

        return (lastUserMsg, history);
    }

    /// <summary>
    /// V2 版本 AI写的有的垃圾
    /// </summary>
    /// <param name="req"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<AgentStreamChunk> StreamChateV2Async(ReqAgentChat req, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(req.Message))
        {
             BusinessThrow.ThrowException("请输入对话消息");
        }

        if (string.IsNullOrEmpty(req.WorkId))
        {
            BusinessThrow.ThrowException("请携带WorkId");
        }

        var user = userContext;

        var session = dbContext.AICreationSessions.AsNoTracking().FirstOrDefault(p=>p.WorkId == req.WorkId && p.Status == "active");

        session ??= new AICreationSessionEntity
        {
            Id = snowflakeIdGenerator.NextIdString(),
            WorkId = req.WorkId,
            UserId = userContext.UserId,
            CreateBy = userContext.UserId,
            CreateAt = DateTime.Now,
        };

        var sessionId = session.Id;

        var toolResults = new List<(string ToolName, bool Success, string Content)>();

        var accumulatedContent = new System.Text.StringBuilder();

        List<ChatMessage> history = [];
        //从memroyProvider 中获取历史消息

        await foreach (var chunk in _orchestrator.ExecuteAsync(req.WorkId, req.Message, history, cancellationToken))
        {
            if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                accumulatedContent.Append(chunk.Content);

            if (chunk.Type == "tool_result" && chunk.ToolResult is { } tr)
            {
                var truncated = tr.Content?.Length > 500
                    ? tr.Content[..500]
                    : tr.Content ?? string.Empty;
                toolResults.Add((tr.ToolName ?? "tool", tr.Success, truncated));
            }

            yield return chunk;
        }

        var aiContent = accumulatedContent.ToString();

        var recordResult = await _sessionManager.RecordTurnAsync(session.Id);

        var turnNumber = recordResult.Data?.TurnCount ?? 0;

        await _sessionManager.SaveMessagesAsync(
            sessionId, turnNumber, req.Message, aiContent,
            toolResults.Count > 0 ? toolResults : null);
    }
}
