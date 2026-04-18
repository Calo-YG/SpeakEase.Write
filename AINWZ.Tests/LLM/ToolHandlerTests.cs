using AINWZ.Infrastructure.LLM.ToolHandlers;

namespace AINWZ.Tests.LLM;

public class ToolHandlerTests
{
    [Fact]
    public async Task EchoToolHandler_EchoesInputArguments()
    {
        var handler = new EchoToolHandler();
        Assert.Equal("echo", handler.Name);

        var result = await handler.ExecuteAsync("""{"message":"HelloAINW"}""");
        Assert.True(result.Success);
        Assert.Equal("""{"message":"HelloAINW"}""", result.Content);
    }

    [Fact]
    public async Task GetCurrentTimeToolHandler_ReturnsValidTimeInfo()
    {
        var handler = new GetCurrentTimeToolHandler();
        Assert.Equal("get_current_time", handler.Name);

        var result = await handler.ExecuteAsync("{}");
        Assert.True(result.Success);
        Assert.Contains("iso", result.Content);
        Assert.Contains("localTime", result.Content);
        Assert.Contains("unixTimeSeconds", result.Content);
        Assert.Contains("timeZone", result.Content);
    }

    [Fact]
    public async Task GetCurrentTimeToolHandler_IgnoresArguments()
    {
        var handler = new GetCurrentTimeToolHandler();

        // 即使传入任意参数，也返回当前时间
        var result = await handler.ExecuteAsync("""{"unused":"value"}""");
        Assert.True(result.Success);
        Assert.Contains("iso", result.Content);
    }
}
