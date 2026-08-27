using System.Text.Json;
using System.Text.Json.Serialization;

using SpeakEase.Write.Infrastructure.AI.Tools;

namespace AINWZ.Tests.Infrastructure;

public sealed class ToolArgsHelperTests
{
    [Fact]
    public void Options_DeserializesSnakeCaseWorkId()
    {
        var arguments = JsonSerializer.Deserialize<WorkArguments>(
            "{\"work_id\":\"work-1\"}",
            ToolArgsHelper.Options);

        Assert.NotNull(arguments);
        Assert.Equal("work-1", arguments.WorkId);
    }

    [Fact]
    public void Options_DeserializesPascalCaseWorkIdWhenPropertyHasSnakeCaseAttribute()
    {
        var arguments = JsonSerializer.Deserialize<AttributedWorkArguments>(
            "{\"WorkId\":\"work-1\"}",
            ToolArgsHelper.Options);

        Assert.NotNull(arguments);
        Assert.Equal("work-1", arguments.WorkId);
    }

    private sealed class WorkArguments
    {
        public string WorkId { get; init; } = string.Empty;
    }

    private sealed class AttributedWorkArguments
    {
        [JsonPropertyName("work_id")]
        public string WorkId { get; init; } = string.Empty;
    }
}
