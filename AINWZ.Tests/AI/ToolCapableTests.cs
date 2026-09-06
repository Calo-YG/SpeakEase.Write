using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace AINWZ.Tests.AI;

public sealed class ToolCapableTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNotExposeExecutorExceptionMessage()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IToolExecutor, ThrowingToolExecutor>("secret_tool");
        await using var provider = services.BuildServiceProvider();
        var toolCapable = new ToolCapable(provider);

        var result = await toolCapable.ExecuteAsync(new ToolCall
        {
            Id = "call-1",
            Function = new FunctionCallDetail
            {
                Name = "secret_tool",
                Arguments = "{}"
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("execution_error", result.ErrorCode);
        Assert.DoesNotContain("database password", result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationFromExecutor()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IToolExecutor, CancellingToolExecutor>("cancel_tool");
        await using var provider = services.BuildServiceProvider();
        var toolCapable = new ToolCapable(provider);

        await Assert.ThrowsAsync<OperationCanceledException>(() => toolCapable.ExecuteAsync(new ToolCall
        {
            Id = "call-2",
            Function = new FunctionCallDetail
            {
                Name = "cancel_tool",
                Arguments = "{}"
            }
        }, new CancellationTokenSource().Token));
    }

    private sealed class ThrowingToolExecutor : IToolExecutor
    {
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("database password: super-secret");
    }

    private sealed class CancellingToolExecutor : IToolExecutor
    {
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }
}
