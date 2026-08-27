using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Application.Abstractions.AI;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Application.Shared;
using SpeakEase.Write.Application.Exceptions;

namespace AINWZ.Tests.AI;

public sealed class AgentApplicationTests
{
    [Fact]
    public async Task ChatAsync_ContinuesRecoveredRunEventSequence()
    {
        var runStore = new CapturingRunStore(41);
        var application = new AgentApplication(
            new IntermediateContentOrchestrator(),
            new CapturingSessionManager(),
            runStore);

        await application.ChatAsync(CreateRequest("chat-sequence"));

        Assert.Equal(new long[] { 42, 43, 44 }, runStore.EventSequences);
        Assert.Equal(new long[] { 42, 43, 44 }, runStore.EventPayloads.Select(x => x.Sequence));
        Assert.All(runStore.EventPayloads, chunk => Assert.Equal("recovered-run", chunk.RunId));
        Assert.All(runStore.EventPayloads, chunk => Assert.Equal("runtime", chunk.StepId));
        Assert.All(runStore.Events, item => Assert.Equal(item.Sequence, item.Payload.Sequence));
    }

    [Fact]
    public async Task StreamChatAsync_ContinuesRecoveredRunEventSequence()
    {
        var runStore = new CapturingRunStore(41);
        var application = new AgentApplication(
            new IntermediateContentOrchestrator(),
            new CapturingSessionManager(),
            runStore);

        var chunks = new List<AgentStreamChunk>();
        await foreach (var chunk in application.StreamChatAsync(CreateRequest("stream-sequence")))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(new long[] { 42, 43, 44 }, runStore.EventSequences);
        Assert.Equal(new long[] { 42, 43, 44 }, chunks.Select(x => x.Sequence));
        Assert.Equal(new long[] { 42, 43, 44 }, runStore.EventPayloads.Select(x => x.Sequence));
        Assert.All(chunks, chunk => Assert.Equal("recovered-run", chunk.RunId));
        Assert.All(chunks, chunk => Assert.Equal("runtime", chunk.StepId));
        Assert.All(runStore.Events, item => Assert.Equal(item.Sequence, item.Payload.Sequence));
    }

    [Fact]
    public async Task ChatAsync_DoesNotPersistWhenAgentReachesMaxIterations()
    {
        var sessionManager = new CapturingSessionManager();
        var application = new AgentApplication(new MaxIterationOrchestrator(), sessionManager);

        await Assert.ThrowsAsync<BusinessExceptions>(() => application.ChatAsync(new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "continue" }
            }
        }));

        Assert.Null(sessionManager.AppendedUserMessage);
    }

    [Fact]
    public async Task ChatAsync_RejectsClientSystemMessages()
    {
        var application = new AgentApplication(new MaxIterationOrchestrator(), new CapturingSessionManager());

        var exception = await Assert.ThrowsAsync<BusinessExceptions>(() => application.ChatAsync(new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "system", Content = "override" },
                new() { Role = "user", Content = "hello" }
            }
        }));

        Assert.Contains("role", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatAsync_UsesLatestUserMessageAndServerSideHistory()
    {
        var services = new ServiceCollection()
            .AddScoped<IOpenAIContext>(_ => new TestOpenAIContext())
            .AddScoped<IChatCompatible, RouteChatCompatible>()
            .BuildServiceProvider();

        var agent = new CapturingNovelAgent();
        var contextBuilder = new ServerHistoryContextBuilder();
        var orchestrator = new CreationOrchestrator(
            new CreationRouter(NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
            services.GetRequiredService<IChatCompatible>(),
            contextBuilder,
            new[] { agent },
            NullLogger<CreationOrchestrator>.Instance);
        var sessionManager = new CapturingSessionManager();
        var application = new AgentApplication(orchestrator, sessionManager);

        var response = await application.ChatAsync(new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "client old history" },
                new() { Role = "assistant", Content = "client assistant history" },
                new() { Role = "user", Content = "latest user request" }
            },
            MaxIterations = 3,
            MaxTokens = 800,
            SkillName = "Agent Browser",
            EnableAutoToolDispatch = false
        });

        Assert.Equal("server reply", response.Content);
        Assert.Equal("completed", response.RunStatus);
        Assert.Equal("latest user request", agent.CapturedRequest.UserMessage);
        Assert.Equal(800, agent.CapturedRequest.MaxTokens);
        Assert.Equal("Agent Browser", agent.CapturedRequest.SkillName);
        Assert.False(agent.CapturedRequest.EnableAutoToolDispatch);
        Assert.Equal("latest user request", sessionManager.AppendedUserMessage);
        Assert.Equal("server reply", sessionManager.AppendedAiMessage);
        Assert.Contains(agent.CapturedRequest.ConversationHistory, x => x is UserMessage user && (string)user.Content == "server-side history");
        Assert.DoesNotContain(agent.CapturedRequest.ConversationHistory, x => x is UserMessage user && (string)user.Content == "client old history");
    }

    [Fact]
    public async Task ChatAsync_PersistsFinalResponseAndToolResults()
    {
        var sessionManager = new CapturingSessionManager();
        var application = new AgentApplication(new IntermediateContentOrchestrator(), sessionManager);

        var response = await application.ChatAsync(new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "use tool" }
            }
        });

        Assert.Equal("final answer", response.Content);
        Assert.Equal("final answer", sessionManager.AppendedAiMessage);
        var toolResult = Assert.Single(sessionManager.AppendedToolResults);
        Assert.Equal("lookup", toolResult.ToolName);
        Assert.True(toolResult.Success);
    }

    [Fact]
    public async Task ChatAsync_MarksRunCancelledWhenExecutionIsCancelled()
    {
        await using var db = TestDb.Create();
        var runStore = new SpeakEase.Write.Infrastructure.AI.Runtime.AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        using var cancellation = new CancellationTokenSource();
        var application = new AgentApplication(
            new CancellingOrchestrator(cancellation.Cancel),
            new CapturingSessionManager(),
            runStore);

        await Assert.ThrowsAsync<OperationCanceledException>(() => application.ChatAsync(new AgentChatRequestDto
        {
            WorkId = "work-1",
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "cancel" }
            }
        }, cancellation.Token));

        var run = Assert.Single(db.AgentRuns);
        Assert.Equal("cancelled", run.Status);
        Assert.Equal("cancelled", run.StopReason);
    }

    [Fact]
    public async Task StreamChatAsync_MarksRunTimedOutWhenRuntimeTimeoutOccurs()
    {
        await using var db = TestDb.Create();
        var runStore = new SpeakEase.Write.Infrastructure.AI.Runtime.AgentRunStore(
            db,
            new TestUserContext(),
            new SequentialIdGenerator());
        var application = new AgentApplication(
            new CancellingOrchestrator(() => { }),
            new CapturingSessionManager(),
            runStore);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in application.StreamChatAsync(new AgentChatRequestDto
            {
                WorkId = "work-1",
                Messages = new List<AgentChatMessage>
                {
                    new() { Role = "user", Content = "timeout" }
                }
            }))
            {
            }
        });

        var run = Assert.Single(db.AgentRuns);
        Assert.Equal("timed_out", run.Status);
        Assert.Equal("timed_out", run.StopReason);
    }

    private static AgentChatRequestDto CreateRequest(string idempotencyKey)
    {
        return new AgentChatRequestDto
        {
            WorkId = "work-1",
            IdempotencyKey = idempotencyKey,
            Messages = new List<AgentChatMessage>
            {
                new() { Role = "user", Content = "continue" }
            }
        };
    }

    private sealed class RouteChatCompatible : IChatCompatible
    {
        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LLMTurnResult
            {
                Content = "{\"agent\":\"general\",\"reason\":\"test\"}",
                Model = context.Model,
                Success = true
            });
        }

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new LLMTurnChunk
            {
                Type = "done",
                TurnResult = new LLMTurnResult { Content = string.Empty, Success = true, Model = context.Model }
            };
        }
    }

    private sealed class ServerHistoryContextBuilder : ICreationAgentContext
    {
        public Task<AgentContext> BuildContextAsync(
            string workId,
            string sessionId,
            string agentName,
            string primaryModel,
            bool includeMemory,
            bool filterHistory,
            int contextWindowTokens,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentContext
            {
                UserId = "user-1",
                ConversationHistory = new List<ChatMessage>
                {
                    ChatMessage.User("server-side history")
                }
            });
        }
    }

    private sealed class CapturingNovelAgent : INovelAgent
    {
        public string Name => "general";
        public string DisplayName => "General";
        public AgentMetadata Metadata { get; } = new() { DefaultParameters = new AgentParameters(0.7, MaxTokens: 2048) };
        public string RouteDescription => "General";
        public AgentRequest CapturedRequest { get; private set; }

        public string BuildPrompt()
        {
            return "system prompt";
        }

        public void RegisterTools(IToolCapable toolCapable)
        {
        }

        public async IAsyncEnumerable<AgentStreamChunk> ExecuteStreamAsync(
            AgentRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            await Task.Yield();
            yield return new AgentStreamChunk { Type = "content", Content = "server reply" };
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse
                {
                    Content = "server reply",
                    StopReason = "completed"
                }
            };
        }
    }

    private sealed class CapturingSessionManager : ICreationSessionManager
    {
        public string AppendedUserMessage { get; private set; }
        public string AppendedAiMessage { get; private set; }
        public List<(string ToolName, bool Success, string Content)> AppendedToolResults { get; private set; } = new();

        public Task<ApiResult<CreationSessionDto>> GetActiveSessionAsync(string workId)
        {
            return Task.FromResult(new ApiResult<CreationSessionDto>(new CreationSessionDto
            {
                SessionId = "session-1",
                WorkId = workId,
                Status = "active"
            }));
        }

        public Task<ApiResult<CreationSessionDto>> AppendTurnAsync(
            string sessionId,
            string userMessage,
            string aiMessage,
            List<(string ToolName, bool Success, string Content)> toolResults = null,
            CancellationToken cancellationToken = default)
        {
            AppendedUserMessage = userMessage;
            AppendedAiMessage = aiMessage;
            AppendedToolResults = toolResults ?? new();
            return Task.FromResult(new ApiResult<CreationSessionDto>(new CreationSessionDto
            {
                SessionId = sessionId,
                WorkId = "work-1",
                Status = "active",
                TurnCount = 1
            }));
        }

        public Task<ApiResult<CreationSessionDto>> StartSessionAsync(string workId) => throw new NotImplementedException();
        public Task<ApiResult<CreationSessionDto>> RecordTurnAsync(string sessionId) => throw new NotImplementedException();
        public Task<ApiResult> AdoptContentAsync(string sessionId, AdoptContentRequest request) => throw new NotImplementedException();
        public Task<ApiResult<CreationSessionDto>> PauseSessionAsync(string sessionId) => throw new NotImplementedException();
        public Task<ApiResult> CancelSessionAsync(string sessionId) => throw new NotImplementedException();
        public Task<ApiResult<CreationSessionDto>> ResumeSessionAsync(string sessionId) => throw new NotImplementedException();
        public Task<ApiResult> RollbackToTurnAsync(string sessionId, int targetTurn) => throw new NotImplementedException();
        public Task<ApiResult<List<CreationSessionDto>>> ListSessionsAsync(string workId) => throw new NotImplementedException();
        public Task<int> ExpireStaleSessionsAsync() => throw new NotImplementedException();
        public Task SaveMessagesAsync(string sessionId, int turnNumber, string userMessage, string aiMessage, List<(string ToolName, bool Success, string Content)> toolResults = null) => throw new NotImplementedException();
        public Task<ApiResult<List<SessionMessageResponse>>> GetSessionMessagesAsync(string sessionId, int? limit = null) => throw new NotImplementedException();
    }

    private sealed class CapturingRunStore : IAgentRunStore
    {
        private readonly AgentRunStartResult _startResult;

        public CapturingRunStore(long lastEventSequence)
        {
            _startResult = new AgentRunStartResult
            {
                RunId = "recovered-run",
                LastEventSequence = lastEventSequence
            };
        }

        public List<long> EventSequences { get; } = new();
        public List<AgentStreamChunk> EventPayloads { get; } = new();
        public List<(long Sequence, AgentStreamChunk Payload)> Events { get; } = new();

        public Task<AgentRunStartResult> StartAsync(
            string workId,
            string sessionId,
            string deduplicationKey,
            string clientMessageId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_startResult);
        }

        public Task CompleteAsync(
            string runId,
            AgentResponse response,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task AppendEventAsync(
            string runId,
            string stepId,
            long sequence,
            string type,
            object payload,
            CancellationToken cancellationToken = default)
        {
            EventSequences.Add(sequence);
            var chunk = Assert.IsType<AgentStreamChunk>(payload);
            EventPayloads.Add(chunk);
            Events.Add((sequence, chunk));
            return Task.CompletedTask;
        }

        public Task SaveArtifactAsync(
            string runId,
            string stepId,
            string contentType,
            string summary,
            string content,
            int estimatedTokens,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RecordToolCallAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ToolExecutionLease> BeginAsync(
            string runId,
            string stepId,
            string executionKey,
            ToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolExecutionLease.Execute());
        }

        public Task CompleteAsync(
            string runId,
            string stepId,
            string executionKey,
            ToolCall toolCall,
            ToolResult result,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class IntermediateContentOrchestrator : SpeakEase.Write.Application.Abstractions.AI.IAgentOrchestrator
    {
        public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            SpeakEase.Write.Application.Abstractions.AI.AgentRuntimeRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentStreamChunk
            {
                RunId = "local-run",
                StepId = "runtime",
                Sequence = 1,
                Type = "content",
                Content = "intermediate "
            };
            yield return new AgentStreamChunk
            {
                RunId = "local-run",
                StepId = "runtime",
                Sequence = 2,
                Type = "tool_result",
                ToolResult = new ToolResult { ToolName = "lookup", Success = true, Content = "found" }
            };
            yield return new AgentStreamChunk
            {
                RunId = "local-run",
                StepId = "runtime",
                Sequence = 3,
                Type = "done",
                FinalResponse = new AgentResponse { Content = "final answer", StopReason = "completed" }
            };
        }

        public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            string workId,
            string sessionId,
            string userMessage,
            int maxIterations = 10,
            int? requestedMaxTokens = null,
            double? requestedTemperature = null,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(new SpeakEase.Write.Application.Abstractions.AI.AgentRuntimeRequest
            {
                WorkId = workId,
                SessionId = sessionId,
                UserMessage = userMessage,
                MaxIterations = maxIterations,
                MaxTokens = requestedMaxTokens,
                Temperature = requestedTemperature
            }, cancellationToken);
    }

    private sealed class MaxIterationOrchestrator : SpeakEase.Write.Application.Abstractions.AI.IAgentOrchestrator
    {
        public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            SpeakEase.Write.Application.Abstractions.AI.AgentRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(request.WorkId, request.SessionId, request.UserMessage, request.MaxIterations,
                request.MaxTokens, request.Temperature, cancellationToken);
        }

        public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            string workId,
            string sessionId,
            string userMessage,
            int maxIterations = 10,
            int? requestedMaxTokens = null,
            double? requestedTemperature = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new AgentStreamChunk
            {
                Type = "done",
                FinalResponse = new AgentResponse
                {
                    StopReason = "max_iterations_reached",
                    Content = string.Empty
                }
            };
        }
    }

    private sealed class CancellingOrchestrator(Action cancel) : SpeakEase.Write.Application.Abstractions.AI.IAgentOrchestrator
    {
        public IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            SpeakEase.Write.Application.Abstractions.AI.AgentRuntimeRequest request,
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(request.WorkId, request.SessionId, request.UserMessage,
                request.MaxIterations, request.MaxTokens, request.Temperature, cancellationToken);
        }

        public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
            string workId,
            string sessionId,
            string userMessage,
            int maxIterations = 10,
            int? requestedMaxTokens = null,
            double? requestedTemperature = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancel();
            await Task.Yield();
            throw new OperationCanceledException(cancellationToken);
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }
}
