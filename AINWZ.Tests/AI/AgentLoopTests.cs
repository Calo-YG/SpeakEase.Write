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

    [Fact]
    public async Task RunAsync_CompletesToolJournalBeforePropagatingCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var chunks = new List<AgentStreamChunk>();
        var events = new List<string>();
        var toolCall = new ToolCall
        {
            Id = "call-save",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };
        var tools = new RecordingToolCapable
        {
            OnExecuted = () =>
            {
                events.Add("tool_completed");
                cancellation.Cancel();
            }
        };
        var journal = new RecordingToolExecutionJournal(events);
        var loop = new AgentLoop();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in loop.RunAsync(
                new AgentLoopRequest
                {
                    Llm = new ScriptedChatCompatible(_ => new LLMTurnResult
                    {
                        Success = true,
                        ToolCalls = new List<ToolCall> { toolCall }
                    }),
                    Tools = tools,
                    Journal = journal,
                    Request = new AgentRequest { UserMessage = "save", MaxIterations = 1 }
                },
                cancellation.Token))
            {
                chunks.Add(chunk);
            }
        });

        Assert.Equal(new[] { "tool_completed", "journal_completed" }, events);
        Assert.Equal(1, journal.CompleteCount);
        Assert.False(journal.CompleteTokenWasCanceled);
        Assert.DoesNotContain(chunks, chunk => chunk.Type is "tool_result" or "done");
    }

    [Fact]
    public async Task RunAsync_DoesNotExecuteNextToolAfterCancellationFollowingFirstCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var events = new List<string>();
        var firstToolCall = new ToolCall
        {
            Id = "call-save-1",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":1}" }
        };
        var secondToolCall = new ToolCall
        {
            Id = "call-save-2",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{\"value\":2}" }
        };
        var executionCount = 0;
        var tools = new RecordingToolCapable
        {
            OnExecuted = () =>
            {
                executionCount++;
                if (executionCount == 1)
                    cancellation.Cancel();
            }
        };
        var journal = new RecordingToolExecutionJournal(events);
        var loop = new AgentLoop();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CollectAsync(loop.RunAsync(
            new AgentLoopRequest
            {
                Llm = new ScriptedChatCompatible(_ => new LLMTurnResult
                {
                    Success = true,
                    ToolCalls = new List<ToolCall> { firstToolCall, secondToolCall }
                }),
                Tools = tools,
                Journal = journal,
                Request = new AgentRequest { UserMessage = "save", MaxIterations = 1 }
            },
            cancellation.Token)));

        var executedToolCall = Assert.Single(tools.Calls);
        Assert.Equal(firstToolCall.Id, executedToolCall.Id);
        Assert.Equal(1, journal.CompleteCount);
    }

    [Fact]
    public async Task RunAsync_CancelsBlockedToolJournalAfterCompletionTimeout()
    {
        var toolCall = new ToolCall
        {
            Id = "call-blocked-journal",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{}" }
        };
        var journal = new BlockingToolExecutionJournal();
        var loop = new AgentLoop();
        var runTask = CollectAsync(loop.RunAsync(new AgentLoopRequest
        {
            Llm = new ScriptedChatCompatible(_ => new LLMTurnResult
            {
                Success = true,
                ToolCalls = new List<ToolCall> { toolCall }
            }),
            Tools = new RecordingToolCapable(),
            Journal = journal,
            Options = new AgentLoopOptions
            {
                ToolJournalCompletionTimeout = TimeSpan.FromMilliseconds(50)
            },
            Request = new AgentRequest { UserMessage = "save", MaxIterations = 1 }
        }));

        await journal.CompletionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var completedTask = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromMilliseconds(250)));
        if (completedTask != runTask)
            journal.Release();
        var exception = await Record.ExceptionAsync(() => runTask);

        Assert.Same(runTask, completedTask);
        Assert.True(journal.CompletionTokenCanBeCanceled);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(-2L)]
    [InlineData(4_294_967_295L)]
    public async Task RunAsync_RejectsInvalidToolJournalCompletionTimeoutBeforeExecution(long timeoutMilliseconds)
    {
        var toolCall = new ToolCall
        {
            Id = "call-invalid-timeout",
            Function = new FunctionCallDetail { Name = "save", Arguments = "{}" }
        };
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult
        {
            Success = true,
            ToolCalls = new List<ToolCall> { toolCall }
        });
        var tools = new RecordingToolCapable();
        var loop = new AgentLoop();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CollectAsync(loop.RunAsync(
            new AgentLoopRequest
            {
                Llm = llm,
                Tools = tools,
                Journal = new RecordingToolExecutionJournal(new List<string>()),
                Options = new AgentLoopOptions
                {
                    ToolJournalCompletionTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds)
                },
                Request = new AgentRequest { UserMessage = "save" }
            })));

        Assert.Equal(nameof(AgentLoopOptions.ToolJournalCompletionTimeout), exception.ParamName);
        Assert.Empty(llm.Requests);
        Assert.Empty(tools.Calls);
    }

    [Fact]
    public async Task RunAsync_RejectsRequestWhenMinimumContextCannotFit()
    {
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult { Success = true, Content = "unexpected" });
        var chunks = await CollectAsync(new AgentLoop().RunAsync(new AgentLoopRequest
        {
            Llm = llm,
            Tools = new RecordingToolCapable(),
            Request = new AgentRequest
            {
                ContextWindowTokens = 100,
                MaxTokens = 20,
                SystemPrompt = new string('系', 200),
                UserMessage = new string('用', 200)
            }
        }));

        Assert.Empty(llm.Requests);
        Assert.Contains(chunks, x => x.Type == "error");
        Assert.Equal("context_budget_exceeded", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
    }

    [Fact]
    public async Task RunAsync_CountsResolvedSkillAndToolSchemaInBudget()
    {
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult { Success = true, Content = "unexpected" });
        var tools = new RecordingToolCapable(new[]
        {
            new ToolDefinition
            {
                Function = new FunctionDefinition
                {
                    Name = "lookup",
                    Description = new string('工', 300),
                    Parameters = new FunctionParameters { Properties = new Dictionary<string, ParameterSchema>() }
                }
            }
        });
        var chunks = await CollectAsync(new AgentLoop().RunAsync(new AgentLoopRequest
        {
            Llm = llm,
            Tools = tools,
            SkillResolver = new StaticSkillResolver(new string('技', 300)),
            Request = new AgentRequest
            {
                ContextWindowTokens = 300,
                MaxTokens = 50,
                SystemPrompt = "system",
                UserMessage = "request",
                SkillName = "large-skill"
            }
        }));

        Assert.Empty(llm.Requests);
        Assert.Equal("context_budget_exceeded", chunks.Single(x => x.Type == "done").FinalResponse.StopReason);
    }

    [Fact]
    public async Task RunAsync_TrimsOldestCompleteHistoryTurnBeforeLlmCall()
    {
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult { Success = true, Content = "done" });
        await CollectAsync(new AgentLoop().RunAsync(new AgentLoopRequest
        {
            Llm = llm,
            Tools = new RecordingToolCapable(),
            Request = new AgentRequest
            {
                ContextWindowTokens = 260,
                MaxTokens = 40,
                SystemPrompt = "system",
                UserMessage = "current",
                ConversationHistory = new List<ChatMessage>
                {
                    ChatMessage.User("old-user-" + new string('旧', 120)),
                    ChatMessage.Assistant("old-assistant-" + new string('旧', 120)),
                    ChatMessage.User("recent-user"),
                    ChatMessage.Assistant("recent-assistant")
                }
            }
        }));

        var sent = Assert.Single(llm.Requests);
        Assert.DoesNotContain(sent, x => GetMessageText(x).Contains("old-user-"));
        Assert.DoesNotContain(sent, x => GetMessageText(x).Contains("old-assistant-"));
        Assert.Contains(sent, x => GetMessageText(x) == "recent-user");
        Assert.Contains(sent, x => GetMessageText(x) == "recent-assistant");
    }

    [Fact]
    public async Task RunAsync_CountsContentPartTextWhenTrimmingHistory()
    {
        var llm = new ScriptedChatCompatible(_ => new LLMTurnResult { Success = true, Content = "done" });
        await CollectAsync(new AgentLoop().RunAsync(new AgentLoopRequest
        {
            Llm = llm,
            Tools = new RecordingToolCapable(),
            Request = new AgentRequest
            {
                ContextWindowTokens = 220,
                MaxTokens = 40,
                SystemPrompt = "system",
                UserMessage = "current",
                ConversationHistory = new List<ChatMessage>
                {
                    new UserMessage
                    {
                        Content = new List<ContentPart>
                        {
                            new() { Type = "text", Text = "multipart-" + new string('图', 200) }
                        }
                    },
                    ChatMessage.Assistant("old answer"),
                    ChatMessage.User("recent-user"),
                    ChatMessage.Assistant("recent-assistant")
                }
            }
        }));

        var sent = Assert.Single(llm.Requests);
        Assert.DoesNotContain(sent, x => GetMessageText(x).Contains("multipart-"));
        Assert.Contains(sent, x => GetMessageText(x) == "recent-user");
    }

    private static string GetMessageText(ChatMessage message) => message switch
    {
        SystemMessage system => system.Content ?? string.Empty,
        UserMessage user when user.Content is string text => text,
        UserMessage user when user.Content is IEnumerable<ContentPart> parts => string.Join("", parts.Select(x => x.Text)),
        AssistantMessage assistant => assistant.Content ?? string.Empty,
        ToolMessage tool => tool.Content ?? string.Empty,
        _ => string.Empty
    };

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

    private sealed class RecordingToolCapable(IReadOnlyList<ToolDefinition> definitions = null) : IToolCapable
    {
        public IReadOnlyList<ToolDefinition> Tools { get; } = definitions ?? new List<ToolDefinition>();
        public List<ToolCall> Calls { get; } = new();
        public ToolResult Result { get; set; } = new() { Success = true, Content = "ok" };
        public Action OnExecuted { get; set; }

        public void RegisterTool(ToolDefinition tool)
        {
        }

        public Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken)
        {
            Calls.Add(toolCall);
            OnExecuted?.Invoke();
            return Task.FromResult(Result);
        }
    }

    private sealed class StaticSkillResolver(string content) : ISkillResolver
    {
        public Task<SkillContent> ResolveAsync(string skillName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SkillContent { SkillName = skillName, Content = content });
        }
    }

    private sealed class RecordingToolExecutionJournal(List<string> events) : IToolExecutionJournal
    {
        public int CompleteCount { get; private set; }
        public bool CompleteTokenWasCanceled { get; private set; }

        public Task<ToolExecutionLease> BeginAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolExecutionLease.Execute());
        }

        public Task CompleteAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            ToolResult result,
            CancellationToken cancellationToken = default)
        {
            CompleteCount++;
            CompleteTokenWasCanceled = cancellationToken.IsCancellationRequested;
            events.Add("journal_completed");
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingToolExecutionJournal : IToolExecutionJournal
    {
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> CompletionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CompletionTokenCanBeCanceled { get; private set; }

        public Task<ToolExecutionLease> BeginAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolExecutionLease.Execute());
        }

        public async Task CompleteAsync(
            string runId,
            string stepId,
            ToolCall toolCall,
            ToolResult result,
            CancellationToken cancellationToken = default)
        {
            CompletionTokenCanBeCanceled = cancellationToken.CanBeCanceled;
            CompletionStarted.TrySetResult(true);
            await Task.WhenAny(
                Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
                _release.Task);
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void Release()
        {
            _release.TrySetResult(true);
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
