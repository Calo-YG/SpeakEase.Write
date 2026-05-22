using System.Runtime.CompilerServices;
using System.Text;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.Exceptions;

namespace SpeakEase.Write.Application.Applications;

// AI创作助手应用服务：处理与AI编排器的对话交互，支持同步和流式两种响应模式
public sealed class AgentApplication(
    CreationOrchestrator orchestrator,
    ICreationSessionManager sessionManager) : IAgentApplication
{
    private readonly CreationOrchestrator _orchestrator = orchestrator;
    private readonly ICreationSessionManager _sessionManager = sessionManager;

    // 同步聊天：收集AI编排器返回的所有内容片段后，一次性返回完整响应
    public async Task<AgentResponse> ChatAsync(AgentChatRequestDto request, CancellationToken cancellationToken = default)
    {
        // 参数校验：检查WorkId、Messages等必填项
        ValidateRequest(request);

        var workId = request.WorkId.Trim();
        // 提取最新一条用户消息作为AI输入
        var userMessage = ExtractLatestUserMessage(request.Messages);
        // 确保有活跃的创作会话（不存在则自动创建）
        var sessionId = await EnsureActiveSessionAsync(workId);
        var contentParts = new List<string>();
        var errorMessage = string.Empty;

        // 通过AI编排器执行对话，收集返回的内容块
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

        // 如果AI编排器返回错误，直接抛出业务异常
        if (!string.IsNullOrWhiteSpace(errorMessage))
            BusinessThrow.ThrowException(errorMessage);

        // 拼接所有内容片段为完整响应文本
        var aiContent = string.Join(string.Empty, contentParts);
        // 将本轮对话（用户消息+AI回复）追加到会话记录
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

    // 流式聊天：实时yield返回AI编排器的内容块（SSE），完成后记录对话历史
    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 参数校验：检查WorkId、Messages等必填项
        ValidateRequest(request);

        var workId = request.WorkId.Trim();
        // 提取最新一条用户消息作为AI输入
        var userMessage = ExtractLatestUserMessage(request.Messages);
        // 确保有活跃的创作会话（不存在则自动创建）
        var sessionId = await EnsureActiveSessionAsync(workId);
        var accumulatedContent = new StringBuilder();
        // 收集工具调用结果用于记录会话历史
        var toolResults = new List<(string ToolName, bool Success, string Content)>();
        var hadError = false;

        // 流式执行AI编排器，实时yield内容块给调用方
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

            // 截断过长的工具结果内容（超过500字符），避免存储过大
            if (chunk.Type == "tool_result" && chunk.ToolResult is { } result)
            {
                var truncated = result.Content?.Length > 500
                    ? result.Content[..500]
                    : result.Content ?? string.Empty;

                toolResults.Add((result.ToolName ?? "tool", result.Success, truncated));
            }

            yield return chunk;
        }

        // 如果流式过程中发生错误，不再记录会话历史
        if (hadError)
            yield break;

        // 流式完成后，将本轮对话追加到会话记录（含工具调用结果）
        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            userMessage,
            accumulatedContent.ToString(),
            toolResults.Count > 0 ? toolResults : null,
            cancellationToken);

        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");
    }

    // 确保作品有活跃的AI创作会话：先查已有会话，没有则创建新会话
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

    // 校验聊天请求参数：WorkId和Messages（含至少一条user消息）不能为空
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

    // 从消息列表中提取最新一条role为"user"的消息内容
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
