using System.Runtime.CompilerServices;
using System.Text;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Abstractions.AI;
using CreationOrchestrator = SpeakEase.Write.Application.Abstractions.AI.IAgentOrchestrator;
using SpeakEase.Write.Application.Exceptions;

namespace SpeakEase.Write.Application.Applications;

// AI创作助手应用服务：处理与AI编排器的对话交互，支持同步和流式两种响应模式
public sealed class AgentApplication(
    CreationOrchestrator orchestrator,
    ICreationSessionManager sessionManager,
    IAgentRunStore runStore = null) : IAgentApplication
{
    private readonly CreationOrchestrator _orchestrator = orchestrator;
    private readonly ICreationSessionManager _sessionManager = sessionManager;
    private readonly IAgentRunStore _runStore = runStore;

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
        var runId = await StartRunAsync(request, workId, sessionId, cancellationToken);
        if (runId.IsReplay)
            return runId.ExistingResponse;
        if (runId.IsInProgress)
            BusinessThrow.ThrowException("The same request is already running.");
        var contentParts = new List<string>();
        var toolResults = new List<(string ToolName, bool Success, string Content)>();
        var errorMessage = string.Empty;
        AgentResponse finalResponse = null;
        var eventSequence = runId.LastEventSequence;

        // 通过AI编排器执行对话，收集返回的内容块
        try
        {
            await foreach (var chunk in _orchestrator.ExecuteAsync(new AgentRuntimeRequest
            {
                RunId = runId.RunId,
                WorkId = workId,
                SessionId = sessionId,
                UserMessage = userMessage,
                ClientMessageId = request.ClientMessageId,
                IdempotencyKey = request.IdempotencyKey,
                SkillName = request.SkillName,
                MaxIterations = request.MaxIterations,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                EnableAutoToolDispatch = request.EnableAutoToolDispatch
            }, cancellationToken))
            {
                await AppendRunEventAsync(runId.RunId, chunk, ++eventSequence, cancellationToken);
                if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                    contentParts.Add(chunk.Content);

                if (chunk.Type == "error" && !string.IsNullOrWhiteSpace(chunk.Content))
                    errorMessage = chunk.Content;

                if (chunk.Type == "tool_result" && chunk.ToolResult is { } result)
                {
                    var truncated = result.Content?.Length > 500
                        ? result.Content[..500]
                        : result.Content ?? string.Empty;
                    toolResults.Add((result.ToolName ?? "tool", result.Success, truncated));
                }

                if (chunk.Type == "done" && chunk.FinalResponse is not null)
                    finalResponse = chunk.FinalResponse;
            }
        }
        catch (OperationCanceledException)
        {
            await CompleteRunAsync(
                runId.RunId,
                CreateCancellationResponse(cancellationToken),
                CancellationToken.None);
            throw;
        }

        // 如果AI编排器返回错误，直接抛出业务异常
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            await CompleteRunAsync(runId.RunId, finalResponse ?? new AgentResponse
            {
                Content = string.Empty,
                StopReason = "llm_error"
            }, CancellationToken.None);
            BusinessThrow.ThrowException(errorMessage);
        }

        if (finalResponse is not null && finalResponse.StopReason != "completed")
            await CompleteRunAsync(runId.RunId, finalResponse, CancellationToken.None);
        EnsureSuccessfulRun(finalResponse);

        // 拼接所有内容片段为完整响应文本
        var streamedContent = string.Join(string.Empty, contentParts);
        var aiContent = finalResponse?.StopReason == "completed" &&
                        !string.IsNullOrWhiteSpace(finalResponse.Content)
            ? finalResponse.Content
            : streamedContent;
        // 将本轮对话（用户消息+AI回复）追加到会话记录
        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            userMessage,
            aiContent,
            toolResults.Count > 0 ? toolResults : null,
            cancellationToken: cancellationToken);

        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");

        var runResult = BuildRunResult(finalResponse, aiContent);
        var response = new AgentResponse
        {
            Content = aiContent,
            StopReason = runResult.StopReason,
            RunStatus = runResult.Status.ToString().ToLowerInvariant(),
            Model = finalResponse?.Model,
            TotalUsage = finalResponse?.TotalUsage
        };
        await CompleteRunAsync(runId.RunId, response, CancellationToken.None);
        return response;
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
        var runId = await StartRunAsync(request, workId, sessionId, cancellationToken);
        if (runId.IsReplay)
        {
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = runId.ExistingResponse
            };
            yield break;
        }
        if (runId.IsInProgress)
            BusinessThrow.ThrowException("The same request is already running.");
        var accumulatedContent = new StringBuilder();
        // 收集工具调用结果用于记录会话历史
        var toolResults = new List<(string ToolName, bool Success, string Content)>();
        var hadError = false;
        AgentResponse finalResponse = null;
        var eventSequence = runId.LastEventSequence;

        // 流式执行AI编排器，实时yield内容块给调用方
        var streamCompleted = false;
        try
        {
            await foreach (var chunk in _orchestrator.ExecuteAsync(new AgentRuntimeRequest
            {
                RunId = runId.RunId,
                WorkId = workId,
                SessionId = sessionId,
                UserMessage = userMessage,
                ClientMessageId = request.ClientMessageId,
                IdempotencyKey = request.IdempotencyKey,
                SkillName = request.SkillName,
                MaxIterations = request.MaxIterations,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                EnableAutoToolDispatch = request.EnableAutoToolDispatch
            }, cancellationToken))
            {
                await AppendRunEventAsync(runId.RunId, chunk, ++eventSequence, cancellationToken);
                if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
                    accumulatedContent.Append(chunk.Content);

                if (chunk.Type == "error")
                    hadError = true;

                if (chunk.Type == "done" && chunk.FinalResponse is not null)
                    finalResponse = chunk.FinalResponse;

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
            streamCompleted = true;
        }
        finally
        {
            if (!streamCompleted)
            {
                await CompleteRunAsync(
                    runId.RunId,
                    CreateCancellationResponse(cancellationToken),
                    CancellationToken.None);
            }
        }

        // 如果流式过程中发生错误，不再记录会话历史
        if (hadError)
        {
            await CompleteRunAsync(runId.RunId, finalResponse ?? new AgentResponse
            {
                Content = string.Empty,
                StopReason = "llm_error"
            }, CancellationToken.None);
            yield break;
        }

        if (finalResponse is not null && finalResponse.StopReason != "completed")
            await CompleteRunAsync(runId.RunId, finalResponse, CancellationToken.None);
        EnsureSuccessfulRun(finalResponse);

        // 流式完成后，将本轮对话追加到会话记录（含工具调用结果）
        var persistedContent = finalResponse?.StopReason == "completed" &&
                               !string.IsNullOrWhiteSpace(finalResponse.Content)
            ? finalResponse.Content
            : accumulatedContent.ToString();
        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            userMessage,
            persistedContent,
            toolResults.Count > 0 ? toolResults : null,
            cancellationToken);

        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");

        await CompleteRunAsync(runId.RunId, finalResponse ?? new AgentResponse
        {
            Content = persistedContent,
            StopReason = "completed"
        }, CancellationToken.None);
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
        AgentInputNormalizer.Normalize(request);
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

    private static void EnsureSuccessfulRun(AgentResponse finalResponse)
    {
        var stopReason = finalResponse?.StopReason;
        if (string.IsNullOrWhiteSpace(stopReason) || stopReason == "completed")
            return;

        var message = stopReason switch
        {
            "max_iterations_reached" => "AI 执行达到最大迭代次数，未生成完整回复。",
            "cancelled" => "AI 执行已取消。",
            "timed_out" => "AI 执行超时。",
            "invalid_request" => "AI 执行请求无效。",
            "tool_dispatch_disabled" => "当前运行未启用工具调度。",
            _ => "AI 执行未正常完成。"
        };

        BusinessThrow.ThrowException(message);
    }

    private static AgentResponse CreateCancellationResponse(CancellationToken cancellationToken)
    {
        var stopReason = cancellationToken.IsCancellationRequested ? "cancelled" : "timed_out";
        return new AgentResponse
        {
            Content = string.Empty,
            StopReason = stopReason,
            RunStatus = stopReason
        };
    }

    private static AgentRunResult BuildRunResult(AgentResponse finalResponse, string content)
    {
        var stopReason = finalResponse?.StopReason ?? "completed";
        var status = stopReason switch
        {
            "completed" => AgentRunStatus.Completed,
            "cancelled" => AgentRunStatus.Cancelled,
            "timed_out" => AgentRunStatus.TimedOut,
            "max_iterations_reached" => AgentRunStatus.MaxIterationsReached,
            "invalid_request" => AgentRunStatus.InvalidRequest,
            _ => AgentRunStatus.Failed
        };

        return new AgentRunResult
        {
            Status = status,
            StopReason = stopReason,
            Content = content ?? string.Empty
        };
    }

    private async Task<AgentRunStartResult> StartRunAsync(
        AgentChatRequestDto request,
        string workId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var deduplicationKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? request.IdempotencyKey.Trim()
            : !string.IsNullOrWhiteSpace(request.ClientMessageId)
                ? request.ClientMessageId.Trim()
                : Guid.NewGuid().ToString("N");

        if (_runStore is not null)
        {
            return await _runStore.StartAsync(
                workId,
                sessionId,
                deduplicationKey,
                request.ClientMessageId ?? string.Empty,
                cancellationToken);
        }

        return new AgentRunStartResult { RunId = deduplicationKey };
    }

    private Task CompleteRunAsync(
        string runId,
        AgentResponse response,
        CancellationToken cancellationToken)
    {
        return _runStore?.CompleteAsync(runId, response, cancellationToken) ?? Task.CompletedTask;
    }

    private Task AppendRunEventAsync(
        string runId,
        AgentStreamChunk chunk,
        long sequence,
        CancellationToken cancellationToken)
    {
        return _runStore?.AppendEventAsync(
            runId,
            chunk.StepId ?? string.Empty,
            sequence,
            chunk.Type ?? string.Empty,
            chunk,
            cancellationToken) ?? Task.CompletedTask;
    }

}
