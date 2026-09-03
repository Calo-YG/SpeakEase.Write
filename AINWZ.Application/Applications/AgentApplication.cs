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

    public async Task<AgentResponse> ChatAsync(
        AgentChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var state = new ChatExecutionState();
        await foreach (var _ in ExecuteSharedAsync(request, state, cancellationToken))
        {
        }

        if (state.ReplayResponse is not null)
            return state.ReplayResponse;
        if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
            BusinessThrow.ThrowException(state.ErrorMessage);

        EnsureSuccessfulRun(state.FinalResponse);
        return state.Response;
    }

    public async IAsyncEnumerable<AgentStreamChunk> StreamChatAsync(
        AgentChatRequestDto request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var state = new ChatExecutionState();
        await foreach (var chunk in ExecuteSharedAsync(request, state, cancellationToken))
            yield return chunk;

        if (state.ReplayResponse is not null || !string.IsNullOrWhiteSpace(state.ErrorMessage))
            yield break;

        EnsureSuccessfulRun(state.FinalResponse);
    }

    private async IAsyncEnumerable<AgentStreamChunk> ExecuteSharedAsync(
        AgentChatRequestDto request,
        ChatExecutionState state,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var normalized = NormalizeRequest(request);
        var sessionId = await EnsureActiveSessionAsync(normalized.WorkId);
        var run = await StartRunAsync(request, normalized.WorkId, sessionId, cancellationToken);
        if (run.IsReplay)
        {
            state.ReplayResponse = run.ExistingResponse;
            yield return new AgentStreamChunk { Type = "done", FinalResponse = run.ExistingResponse };
            yield break;
        }
        if (run.IsInProgress)
            BusinessThrow.ThrowException("The same request is already running.");

        var content = new StringBuilder();
        var toolResults = new List<(string ToolName, bool Success, string Content)>();
        var eventSequence = run.LastEventSequence;
        var streamCompleted = false;
        try
        {
            await foreach (var chunk in _orchestrator.ExecuteAsync(ToRuntimeRequest(normalized, run.RunId, sessionId), cancellationToken))
            {
                AlignRunEventMetadata(run.RunId, chunk, ++eventSequence);
                await AppendRunEventAsync(run.RunId, chunk, eventSequence, cancellationToken);
                CaptureChunk(chunk, content, toolResults, state);
                yield return chunk;
            }
            streamCompleted = true;
        }
        finally
        {
            if (!streamCompleted)
            {
                await CompleteRunAsync(
                    run.RunId,
                    CreateCancellationResponse(cancellationToken),
                    CancellationToken.None);
            }
        }

        if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
        {
            await CompleteRunAsync(run.RunId, state.FinalResponse ?? new AgentResponse
            {
                Content = string.Empty,
                StopReason = "llm_error"
            }, CancellationToken.None);
            yield break;
        }

        if (state.FinalResponse is not null && state.FinalResponse.StopReason != "completed")
        {
            await CompleteRunAsync(run.RunId, state.FinalResponse, CancellationToken.None);
            yield break;
        }

        var persistedContent = state.FinalResponse?.StopReason == "completed" &&
                               !string.IsNullOrWhiteSpace(state.FinalResponse.Content)
            ? state.FinalResponse.Content
            : content.ToString();
        var appendResult = await _sessionManager.AppendTurnAsync(
            sessionId,
            normalized.UserMessage,
            persistedContent,
            toolResults.Count > 0 ? toolResults : null,
            cancellationToken);
        if (!appendResult.Successed || appendResult.Data is null)
            BusinessThrow.ThrowException(appendResult.Message ?? "Failed to record conversation turn.");

        var runResult = BuildRunResult(state.FinalResponse, persistedContent);
        state.Response = new AgentResponse
        {
            Content = persistedContent,
            StopReason = runResult.StopReason,
            RunStatus = runResult.Status.ToString().ToLowerInvariant(),
            Model = state.FinalResponse?.Model,
            TotalUsage = state.FinalResponse?.TotalUsage
        };
        await CompleteRunAsync(run.RunId, state.Response, CancellationToken.None);
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

    private static AgentChatRuntimeRequest NormalizeRequest(AgentChatRequestDto request)
    {
        ValidateRequest(request);
        return new AgentChatRuntimeRequest
        {
            WorkId = request.WorkId.Trim(),
            UserMessage = ExtractLatestUserMessage(request.Messages),
            ClientMessageId = request.ClientMessageId ?? string.Empty,
            IdempotencyKey = request.IdempotencyKey ?? string.Empty,
            SkillName = request.SkillName ?? string.Empty,
            MaxIterations = request.MaxIterations,
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature,
            EnableAutoToolDispatch = request.EnableAutoToolDispatch
        };
    }

    private static AgentRuntimeRequest ToRuntimeRequest(
        AgentChatRuntimeRequest request,
        string runId,
        string sessionId)
        => new()
        {
            RunId = runId,
            WorkId = request.WorkId,
            SessionId = sessionId,
            UserMessage = request.UserMessage,
            ClientMessageId = request.ClientMessageId,
            IdempotencyKey = request.IdempotencyKey,
            SkillName = request.SkillName,
            MaxIterations = request.MaxIterations,
            MaxTokens = request.MaxTokens,
            Temperature = request.Temperature,
            EnableAutoToolDispatch = request.EnableAutoToolDispatch
        };

    private static void CaptureChunk(
        AgentStreamChunk chunk,
        StringBuilder content,
        List<(string ToolName, bool Success, string Content)> toolResults,
        ChatExecutionState state)
    {
        if (chunk.Type == "content" && !string.IsNullOrEmpty(chunk.Content))
            content.Append(chunk.Content);

        if (chunk.Type == "error")
            state.ErrorMessage = string.IsNullOrWhiteSpace(chunk.Content) ? "AI execution failed." : chunk.Content;

        if (chunk.Type == "done" && chunk.FinalResponse is not null)
            state.FinalResponse = chunk.FinalResponse;

        if (chunk.Type != "tool_result" || chunk.ToolResult is not { } result)
            return;

        var truncated = result.Content?.Length > 500
            ? result.Content[..500]
            : result.Content ?? string.Empty;
        toolResults.Add((result.ToolName ?? "tool", result.Success, truncated));
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

    private static void AlignRunEventMetadata(
        string runId,
        AgentStreamChunk chunk,
        long sequence)
    {
        chunk.RunId = runId;
        chunk.Sequence = sequence;
        if (string.IsNullOrWhiteSpace(chunk.StepId))
            chunk.StepId = "runtime";
    }

    private sealed class ChatExecutionState
    {
        public AgentResponse ReplayResponse { get; set; }
        public AgentResponse FinalResponse { get; set; }
        public AgentResponse Response { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

}
