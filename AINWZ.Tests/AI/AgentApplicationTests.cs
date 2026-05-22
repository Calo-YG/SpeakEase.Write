using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Creation.Dto;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Contract;
using SpeakEase.Write.Infrastructure.AI.Orchestrator;
using SpeakEase.Write.Infrastructure.Shared;

namespace AINWZ.Tests.AI;

public sealed class AgentApplicationTests
{
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
            new CreationRouter(services, NullLogger<CreationRouter>.Instance),
            new TestOpenAIContext(),
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
            MaxTokens = 800
        });

        Assert.Equal("server reply", response.Content);
        Assert.Equal("latest user request", agent.CapturedRequest.UserMessage);
        Assert.Equal(800, agent.CapturedRequest.MaxTokens);
        Assert.Equal("latest user request", sessionManager.AppendedUserMessage);
        Assert.Equal("server reply", sessionManager.AppendedAiMessage);
        Assert.Contains(agent.CapturedRequest.ConversationHistory, x => x is UserMessage user && (string)user.Content == "server-side history");
        Assert.DoesNotContain(agent.CapturedRequest.ConversationHistory, x => x is UserMessage user && (string)user.Content == "client old history");
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
}
