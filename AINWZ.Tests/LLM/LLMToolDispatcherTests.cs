using AINWZ.Infrastructure.LLM;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using Moq;

namespace AINWZ.Tests.LLM;

public class LLMToolDispatcherTests
{
    private static LLMToolCall MakeToolCall(string id, string name, string arguments = "{}")
    {
        return new LLMToolCall
        {
            Id = id,
            Type = "function",
            Function = new LLMToolFunctionCall { Name = name, Arguments = arguments }
        };
    }

    [Fact]
    public async Task DispatchAsync_SingleTool_ExecutesAndReturnsResult()
    {
        // Arrange
        var handler = new Mock<ILLMToolHandler>();
        handler.SetupGet(h => h.Name).Returns("echo");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new LLMToolExecutionResult { ToolName = "echo", Success = true, Content = "hello" });

        var dispatcher = new LLMToolDispatcher(new[] { handler.Object });
        var toolCalls = new List<LLMToolCall> { MakeToolCall("call_1", "echo", """{"message":"hello"}""") };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("hello", results[0].Content);
        Assert.Equal("call_1", results[0].ToolCallId);
    }

    [Fact]
    public async Task DispatchAsync_MultipleTools_ExecutesAll()
    {
        // Arrange
        var echoHandler = new Mock<ILLMToolHandler>();
        echoHandler.SetupGet(h => h.Name).Returns("echo");
        echoHandler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new LLMToolExecutionResult { ToolName = "echo", Success = true, Content = "echoed" });

        var timeHandler = new Mock<ILLMToolHandler>();
        timeHandler.SetupGet(h => h.Name).Returns("get_current_time");
        timeHandler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new LLMToolExecutionResult { ToolName = "get_current_time", Success = true, Content = """{"iso":"2026-01-01T00:00:00+08:00"}""" });

        var dispatcher = new LLMToolDispatcher(new[] { echoHandler.Object, timeHandler.Object });
        var toolCalls = new List<LLMToolCall>
        {
            MakeToolCall("call_1", "echo", """{"message":"test"}"""),
            MakeToolCall("call_2", "get_current_time")
        };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal("echo", results[0].ToolName);
        Assert.Equal("get_current_time", results[1].ToolName);
    }

    [Fact]
    public async Task DispatchAsync_UnregisteredTool_ReturnsToolNotFoundError()
    {
        // Arrange
        var handler = new Mock<ILLMToolHandler>();
        handler.SetupGet(h => h.Name).Returns("echo");

        var dispatcher = new LLMToolDispatcher(new[] { handler.Object });
        var toolCalls = new List<LLMToolCall> { MakeToolCall("call_x", "unknown_tool") };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal("tool_not_found", results[0].ErrorCode);
        Assert.Equal("call_x", results[0].ToolCallId);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ReturnsExecutionFailedError()
    {
        // Arrange
        var handler = new Mock<ILLMToolHandler>();
        handler.SetupGet(h => h.Name).Returns("failing_tool");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var dispatcher = new LLMToolDispatcher(new[] { handler.Object });
        var toolCalls = new List<LLMToolCall> { MakeToolCall("call_err", "failing_tool") };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal("tool_execution_failed", results[0].ErrorCode);
        Assert.Equal("boom", results[0].Content);
    }

    [Fact]
    public async Task DispatchAsync_ResultMissingToolCallId_BackFilled()
    {
        // Arrange
        var handler = new Mock<ILLMToolHandler>();
        handler.SetupGet(h => h.Name).Returns("echo");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new LLMToolExecutionResult { ToolName = "echo", Success = true, Content = "ok" });

        var dispatcher = new LLMToolDispatcher(new[] { handler.Object });
        var toolCalls = new List<LLMToolCall> { MakeToolCall("call_backfill", "echo") };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Equal("call_backfill", results[0].ToolCallId);
    }

    [Fact]
    public async Task DispatchAsync_ResultMissingToolName_BackFilled()
    {
        // Arrange
        var handler = new Mock<ILLMToolHandler>();
        handler.SetupGet(h => h.Name).Returns("echo");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new LLMToolExecutionResult { Success = true, Content = "ok" });

        var dispatcher = new LLMToolDispatcher(new[] { handler.Object });
        var toolCalls = new List<LLMToolCall> { MakeToolCall("call_1", "echo") };

        // Act
        var results = await dispatcher.DispatchAsync(toolCalls);

        // Assert
        Assert.Equal("echo", results[0].ToolName);
    }

    [Fact]
    public async Task DispatchAsync_EmptyToolCalls_ReturnsEmptyList()
    {
        // Arrange
        var dispatcher = new LLMToolDispatcher(Array.Empty<ILLMToolHandler>());

        // Act
        var results = await dispatcher.DispatchAsync(new List<LLMToolCall>());

        // Assert
        Assert.Empty(results);
    }
}
