using AINWZ.Infrastructure.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AINWZ.Tests.LLM;

public class LLMServiceTests
{
    private readonly Mock<ILLMProvider> _provider = new();
    private readonly Mock<ILLMToolDispatcher> _dispatcher = new();
    private readonly Mock<ILLMSkillRegistry> _skillRegistry = new();
    private readonly Mock<ILogger<LLMService>> _logger = new();

    private LLMService CreateSut()
    {
        _skillRegistry.Setup(r => r.GetAll()).Returns(new List<LLMSkillDefinition>());
        return new LLMService(_provider.Object, _dispatcher.Object, _skillRegistry.Object, _logger.Object);
    }

    private static LLMChatRequest SimpleRequest(string userMessage, bool enableAutoToolDispatch = true, int maxIterations = 20)
    {
        return new LLMChatRequest
        {
            Messages = [new("user", userMessage)],
            EnableAutoToolDispatch = enableAutoToolDispatch,
            MaxIterations = maxIterations
        };
    }

    private static LLMChatResponse ProviderResponse(
        string content = "OK",
        string finishReason = "stop",
        List<LLMToolCall> toolCalls = null)
    {
        return new LLMChatResponse
        {
            Content = content,
            FinishReason = finishReason,
            ToolCalls = toolCalls ?? new()
        };
    }

    private static LLMToolCall MakeToolCall(string id, string name, string arguments = "{}")
    {
        return new LLMToolCall
        {
            Id = id,
            Type = "function",
            Function = new LLMToolFunctionCall { Name = name, Arguments = arguments }
        };
    }

    // === 无工具调用场景 ===

    [Fact]
    public async Task ChatAsync_NoToolCalls_ReturnsDirectly()
    {
        // Arrange
        var sut = CreateSut();
        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("Hello!", "stop"));

        // Act
        var result = await sut.ChatAsync(SimpleRequest("Hi"));

        // Assert
        Assert.Equal("Hello!", result.Content);
        Assert.Equal(1, result.Iterations);
        Assert.Equal("completed", result.StopReason);
        Assert.Empty(result.ToolResults);
    }

    // === 单工具调用+二轮补全 ===

    [Fact]
    public async Task ChatAsync_SingleToolCall_ExecutesAndGetsFinalResponse()
    {
        // Arrange
        var sut = CreateSut();
        var toolCall = MakeToolCall("call_1", "get_current_time");

        // 首轮：返回工具调用
        _provider.SetupSequence(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("", "tool_calls", [toolCall]))
            .ReturnsAsync(ProviderResponse("The time is 12:00", "stop"));

        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default))
            .ReturnsAsync(new List<LLMToolExecutionResult>
            {
                new() { ToolCallId = "call_1", ToolName = "get_current_time", Success = true, Content = """{"iso":"2026-01-01T12:00:00+08:00"}""" }
            });

        // Act
        var result = await sut.ChatAsync(SimpleRequest("What time is it?"));

        // Assert
        Assert.Equal("The time is 12:00", result.Content);
        Assert.Equal(2, result.Iterations);
        Assert.Equal("completed", result.StopReason);
        Assert.Single(result.ToolResults);
        Assert.Equal("get_current_time", result.ToolResults[0].ToolName);
    }

    // === 多工具并行调用 ===

    [Fact]
    public async Task ChatAsync_ParallelToolCalls_ExecutesAllAndGetsFinalResponse()
    {
        // Arrange
        var sut = CreateSut();
        var toolCalls = new List<LLMToolCall>
        {
            MakeToolCall("call_1", "echo", """{"message":"test"}"""),
            MakeToolCall("call_2", "get_current_time")
        };

        _provider.SetupSequence(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("", "tool_calls", toolCalls))
            .ReturnsAsync(ProviderResponse("Echo: test, Time: 12:00", "stop"));

        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default))
            .ReturnsAsync(new List<LLMToolExecutionResult>
            {
                new() { ToolCallId = "call_1", ToolName = "echo", Success = true, Content = """{"message":"test"}""" },
                new() { ToolCallId = "call_2", ToolName = "get_current_time", Success = true, Content = """{"iso":"2026-01-01T12:00:00+08:00"}""" }
            });

        // Act
        var result = await sut.ChatAsync(SimpleRequest("Use both tools"));

        // Assert
        Assert.Equal(2, result.Iterations);
        Assert.Equal(2, result.ToolResults.Count);
    }

    // === Agent Loop 多轮迭代 ===

    [Fact]
    public async Task ChatAsync_MultiIteration_ExecutesToolsInSequence()
    {
        // Arrange
        var sut = CreateSut();

        _provider.SetupSequence(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            // 迭代1：调用 echo step1
            .ReturnsAsync(ProviderResponse("", "tool_calls", [MakeToolCall("call_1", "echo", """{"message":"step1"}""")]))
            // 迭代2：调用 echo step2
            .ReturnsAsync(ProviderResponse("", "tool_calls", [MakeToolCall("call_2", "echo", """{"message":"step2"}""")]))
            // 迭代3：最终回复
            .ReturnsAsync(ProviderResponse("Step1 then step2 done", "stop"));

        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default))
            .ReturnsAsync((IReadOnlyList<LLMToolCall> calls, CancellationToken ct) =>
                calls.Select(c => new LLMToolExecutionResult
                {
                    ToolCallId = c.Id,
                    ToolName = c.Function.Name,
                    Success = true,
                    Content = c.Function.Arguments
                }).ToList());

        // Act
        var result = await sut.ChatAsync(SimpleRequest("Do step1 then step2"));

        // Assert
        Assert.Equal(3, result.Iterations);
        Assert.Equal("completed", result.StopReason);
        Assert.Equal(2, result.ToolResults.Count);
        // 验证 conversationHistory 包含 assistant + tool 消息
        Assert.Equal(5, result.ConversationHistory.Count); // user + assistant(tool_calls) + tool + assistant(tool_calls) + tool
    }

    // === 禁用自动工具分发 ===

    [Fact]
    public async Task ChatAsync_AutoDispatchDisabled_ReturnsToolCallsWithoutExecution()
    {
        // Arrange
        var sut = CreateSut();
        var toolCall = MakeToolCall("call_1", "echo");

        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("", "tool_calls", [toolCall]));

        // Act
        var result = await sut.ChatAsync(SimpleRequest("Use echo", enableAutoToolDispatch: false, maxIterations: 1));

        // Assert
        Assert.Single(result.ToolCalls);
        Assert.Empty(result.ToolResults);
        Assert.Equal(1, result.Iterations);
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default), Times.Never);
    }

    // === 达到最大迭代次数 ===

    [Fact]
    public async Task ChatAsync_MaxIterationsReached_ReturnsMaxIterationsStopReason()
    {
        // Arrange
        var sut = CreateSut();

        // 始终返回工具调用，让循环耗尽 maxIterations
        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("", "tool_calls", [MakeToolCall("call_loop", "echo")]));

        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default))
            .ReturnsAsync([new LLMToolExecutionResult { ToolCallId = "call_loop", ToolName = "echo", Success = true, Content = "ok" }]);

        // Act - maxIterations=1 意味着循环只跑 1 次，然后走循环外
        var result = await sut.ChatAsync(SimpleRequest("Loop", maxIterations: 1));

        // Assert
        Assert.Equal("max_iterations", result.StopReason);
        Assert.Equal(1, result.Iterations);
    }

    // === 技能注入 ===

    [Fact]
    public async Task ChatAsync_WithSkill_MergesSystemPromptAndTools()
    {
        // Arrange
        var sut = CreateSut();
        var skill = new LLMSkillDefinition
        {
            Name = "writer",
            Description = "写作助手",
            SystemPrompt = "你是专业写作助手。",
            DefaultTools =
            [
                new LLMToolDefinition
                {
                    Type = "function",
                    Function = new LLMToolFunctionDefinition
                    {
                        Name = "echo",
                        Description = "Echo tool",
                        Parameters = """{"type":"object","properties":{"message":{"type":"string"}},"required":["message"]}"""
                    }
                }
            ]
        };

        _skillRegistry.Setup(r => r.GetByName("writer")).Returns(skill);
        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync((LLMChatRequest req, CancellationToken ct) =>
            {
                // 验证系统提示词已合并
                Assert.Contains("专业写作助手", req.SystemPrompt);
                // 验证技能默认工具已注入
                Assert.Contains(req.Tools, t => t.Function.Name == "echo");
                return ProviderResponse("Writing done", "stop");
            });

        // Act
        var result = await sut.ChatAsync(new LLMChatRequest
        {
            Messages = [new("user", "Write something")],
            SkillName = "writer",
            SystemPrompt = "Keep it short."
        });

        // Assert
        Assert.Equal("Writing done", result.Content);
    }

    [Fact]
    public async Task ChatAsync_WithSkillOverridePrompt_UsesOverrideInsteadOfSkillDefault()
    {
        // Arrange
        var sut = CreateSut();
        var skill = new LLMSkillDefinition
        {
            Name = "coder",
            SystemPrompt = "你是编码助手。",
        };

        _skillRegistry.Setup(r => r.GetByName("coder")).Returns(skill);
        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync((LLMChatRequest req, CancellationToken ct) =>
            {
                Assert.Contains("Override prompt", req.SystemPrompt);
                Assert.DoesNotContain("编码助手", req.SystemPrompt);
                return ProviderResponse("Code done", "stop");
            });

        // Act
        var result = await sut.ChatAsync(new LLMChatRequest
        {
            Messages = [new("user", "Write code")],
            SkillName = "coder",
            SkillOverridePrompt = "Override prompt"
        });

        // Assert
        Assert.Equal("Code done", result.Content);
    }

    [Fact]
    public async Task ChatAsync_WithSkill_ToolDeduplication_DoesNotAddDuplicateTools()
    {
        // Arrange
        var sut = CreateSut();
        var skill = new LLMSkillDefinition
        {
            Name = "writer",
            SystemPrompt = "写作助手",
            DefaultTools =
            [
                new LLMToolDefinition
                {
                    Type = "function",
                    Function = new LLMToolFunctionDefinition { Name = "echo", Description = "Echo" }
                }
            ]
        };

        _skillRegistry.Setup(r => r.GetByName("writer")).Returns(skill);
        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync((LLMChatRequest req, CancellationToken ct) =>
            {
                // 请求已自带 echo，技能注入不应重复
                Assert.Single(req.Tools, t => t.Function.Name == "echo");
                return ProviderResponse("Done", "stop");
            });

        // Act
        var result = await sut.ChatAsync(new LLMChatRequest
        {
            Messages = [new("user", "Test")],
            SkillName = "writer",
            Tools =
            [
                new LLMToolDefinition
                {
                    Type = "function",
                    Function = new LLMToolFunctionDefinition { Name = "echo", Description = "My echo" }
                }
            ]
        });

        Assert.Equal("Done", result.Content);
    }

    // === 多轮对话上下文 ===

    [Fact]
    public async Task ChatAsync_WithHistory_ContextPreservedInConversationHistory()
    {
        // Arrange
        var sut = CreateSut();
        var history = new List<LLMChatMessage>
        {
            new("user", "My name is Alice"),
            new("assistant", "Hi Alice!")
        };

        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync((LLMChatRequest req, CancellationToken ct) =>
            {
                // 验证历史消息完整传入
                Assert.Equal(3, req.Messages.Count);
                return ProviderResponse("Your name is Alice", "stop");
            });

        // Act
        var request = SimpleRequest("What is my name?");
        request.Messages = [.. history, new("user", "What is my name?")];
        var result = await sut.ChatAsync(request);

        // Assert
        Assert.Equal("Your name is Alice", result.Content);
        Assert.Equal(3, result.ConversationHistory.Count);
    }

    // === ShouldExecuteTools 安全门控 ===

    [Fact]
    public async Task ChatAsync_FinishReasonContentFilter_DoesNotExecuteTools()
    {
        // Arrange
        var sut = CreateSut();
        var toolCall = MakeToolCall("call_1", "echo");

        _provider.Setup(p => p.ChatAsync(It.IsAny<LLMChatRequest>(), default))
            .ReturnsAsync(ProviderResponse("filtered", "content_filter", [toolCall]));

        // Act
        var result = await sut.ChatAsync(SimpleRequest("Trigger filter"));

        // Assert - content_filter 不应触发工具执行
        Assert.Empty(result.ToolResults);
        Assert.Equal(1, result.Iterations);
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IReadOnlyList<LLMToolCall>>(), default), Times.Never);
    }
}
