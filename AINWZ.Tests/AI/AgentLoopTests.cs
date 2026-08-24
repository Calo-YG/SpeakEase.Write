using System.Runtime.CompilerServices;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class AgentLoopTests
{
    [Fact]
    public async Task RunAsync_ReturnsCompletedAfterDirectModelAnswer()
    {
        var llm = new ScriptedChatCompatible(
            _ => new LLMTurnResult { Success = true, Model = "test", Content = "final answer" });
        var tools = new RecordingToolCapable();
        var loop = new AgentLoop();

        var chunks = await CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            AgentName = "general",
            Llm = llm,
            Tools = tools,
            Request = new AgentRequest
            {
                SystemPrompt = "system",
                UserMessage = "hello",
                MaxIterations = 3
            }
        }));

        var done = Assert.Single(chunks, x => x.Type == "done");
        Assert.Equal("completed", done.FinalResponse.StopReason);
        Assert.Equal("final answer", done.FinalResponse.Content);
        Assert.Single(llm.Requests);
    }

    [Fact]
    public async Task RunAsync_ExecutesToolThenContinuesWithToolMessage()
    {
        var toolCall = new ToolCall
        {
            Id = "call-1",
            Function = new FunctionCallDetail { Name = "lookup", Arguments = "{}" }
        };
        var llm = new ScriptedChatCompatible(
            _ => new LLMTurnResult
            {
                Success = true,
                Model = "test",
                Content = "need tool",
                ToolCalls = new List<ToolCall> { toolCall }
            },
            _ => new LLMTurnResult { Success = true, Model = "test", Content = "tool answer" });
        var tools = new RecordingToolCapable
        {
            Result = new ToolResult
            {
                ToolCallId = "call-1",
                ToolName = "lookup",
                Success = true,
                Content = "tool content"
            }
        };
        var loop = new AgentLoop();

        var chunks = await CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            AgentName = "general",
            Llm = llm,
            Tools = tools,
            Request = new AgentRequest { UserMessage = "hello", MaxIterations = 3 }
        }));

        Assert.Single(tools.Calls);
        Assert.Contains(chunks, x => x.Type == "tool_result");
        Assert.Equal("completed", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
        Assert.Contains(llm.Requests[1], x => x is ToolMessage message && message.Content == "tool content");
    }

    [Fact]
    public async Task RunAsync_StopsWithMaxIterationsReached()
    {
        var toolCall = new ToolCall
        {
            Id = "call-loop",
            Function = new FunctionCallDetail { Name = "lookup", Arguments = "{}" }
        };
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult
        {
            Success = true,
            Model = "test",
            ToolCalls = new List<ToolCall> { toolCall }
        });
        var loop = new AgentLoop();

        var chunks = await CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            AgentName = "general",
            Llm = llm,
            Tools = new RecordingToolCapable(),
            Request = new AgentRequest { UserMessage = "hello", MaxIterations = 1 }
        }));

        Assert.Equal("max_iterations_reached", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
    }

    [Fact]
    public async Task IAgentLoop_EmitsSequencedEventsWithRunAndStepIdentity()
    {
        var loop = (IAgentLoop)new AgentLoop();
        var events = new List<AgentEvent>();
        await foreach (var item in loop.RunAsync(new AgentLoopRequest
        {
            RunId = "run-1",
            StepId = "step-1",
            Llm = new ScriptedChatCompatible(_ => new LLMTurnResult
            {
                Success = true,
                Model = "test",
                Content = "done"
            }),
            Tools = new RecordingToolCapable(),
            Request = new AgentRequest { UserMessage = "hello" }
        }))
        {
            events.Add(item);
        }

        Assert.NotEmpty(events);
        Assert.Equal("run-1", events[0].RunId);
        Assert.Equal("step-1", events[0].StepId);
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(x => (int)x.Sequence));
        Assert.Equal("done", events[^1].Type);
    }

    [Fact]
    public async Task RunAsync_StopsWhenToolCallBudgetIsExceeded()
    {
        var toolCall = new ToolCall
        {
            Id = "call-1",
            Function = new FunctionCallDetail { Name = "lookup", Arguments = "{}" }
        };
        var loop = new AgentLoop();
        var chunks = await CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            Llm = new ScriptedChatCompatible(_ => new LLMTurnResult
            {
                Success = true,
                ToolCalls = new List<ToolCall> { toolCall }
            }),
            Tools = new RecordingToolCapable(),
            Options = new AgentLoopOptions { MaxToolCalls = 1 },
            Request = new AgentRequest { UserMessage = "hello", MaxIterations = 2 }
        }));

        Assert.Equal("max_tool_calls_reached", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
    }

    [Fact]
    public async Task RunAsync_ReplaysCompletedToolCallWithoutExecutingToolAgain()
    {
        var toolCall = new ToolCall
        {
            Id = "call-replay",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };
        var tools = new RecordingToolCapable();
        var journal = new ReplayToolExecutionJournal(new ToolResult
        {
            Success = true,
            Content = "already saved",
            ToolCallId = toolCall.Id,
            ToolName = toolCall.Function.Name
        });
        var loop = new AgentLoop();

        var chunks = await CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            Llm = new ScriptedChatCompatible(
                _ => new LLMTurnResult { Success = true, ToolCalls = new List<ToolCall> { toolCall } },
                _ => new LLMTurnResult { Success = true, Content = "completed" }),
            Tools = tools,
            Journal = journal,
            Request = new AgentRequest { UserMessage = "retry", MaxIterations = 3 }
        }));

        Assert.Equal("completed", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
        Assert.Empty(tools.Calls);
        Assert.Equal(1, journal.BeginCount);
        Assert.Equal(0, journal.CompleteCount);
    }

    private static async Task<List<AgentStreamChunk>> CollectAsync(IAsyncEnumerable<AgentStreamChunk> stream)
    {
        var chunks = new List<AgentStreamChunk>();
        await foreach (var chunk in stream)
            chunks.Add(chunk);
        return chunks;
    }

    private sealed class ScriptedChatCompatible(params Func<List<ChatMessage>, LLMTurnResult>[] responses) : IChatCompatible
    {
        public List<List<ChatMessage>> Requests { get; } = new();
        private int _index;

        public Task<LLMTurnResult> ChatAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new List<ChatMessage>(messages));
            var response = responses[Math.Min(_index++, responses.Length - 1)](messages);
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<LLMTurnChunk> StreamAsync(
            LLMTurnContext context,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(new List<ChatMessage>(messages));
            var response = responses[Math.Min(_index++, responses.Length - 1)](messages);
            await Task.Yield();
            yield return new LLMTurnChunk { Type = "done", TurnResult = response };
        }
    }

    private sealed class RecordingToolCapable : IToolCapable
    {
        public IReadOnlyList<ToolDefinition> Tools { get; } = new List<ToolDefinition>();
        public List<ToolCall> Calls { get; } = new();
        public ToolResult Result { get; set; } = new() { Success = true, Content = "ok" };

        public void RegisterTool(ToolDefinition tool)
        {
        }

        public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken)
        {
            Calls.Add(toolCall);
            return Task.FromResult(Result);
        }
    }

    private sealed class ReplayToolExecutionJournal(ToolResult replay) : IToolExecutionJournal
    {
        public int BeginCount { get; private set; }
        public int CompleteCount { get; private set; }

        public Task<ToolExecutionLease> BeginAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.FromResult(ToolExecutionLease.Replay(replay));
        }

        public Task CompleteAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            ToolResult result,
            CancellationToken cancellationToken = default)
        {
            CompleteCount++;
            return Task.CompletedTask;
        }
    }
}
