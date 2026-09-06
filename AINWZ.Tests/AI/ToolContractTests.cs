using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace AINWZ.Tests.AI;

public sealed class ToolContractTests
{
    public static IEnumerable<object[]> LegacyToolDefinitions()
    {
        yield return new object[] { GetCharacterTool.ToolDefinition };
        yield return new object[] { UpdateCharacterTool.ToolDefinition };
        yield return new object[] { SaveChapterContentTool.ToolDefinition };
        yield return new object[] { CreateCharacterArcTool.ToolDefinition };
    }

    [Theory]
    [MemberData(nameof(LegacyToolDefinitions))]
    public void ToolDefinition_KeepsLegacyNameAndParameters(ToolDefinition definition)
    {
        Assert.False(string.IsNullOrWhiteSpace(definition.Function.Name));
        Assert.NotNull(definition.Function.Parameters);
        Assert.Equal("object", definition.Function.Parameters.Type);
        Assert.NotNull(definition.Function.Parameters.Properties);
    }

    [Fact]
    public void ToolDefinition_KeepsCriticalLegacyNames()
    {
        Assert.Equal("get_character", GetCharacterTool.ToolDefinition.Function.Name);
        Assert.Equal("update_character", UpdateCharacterTool.ToolDefinition.Function.Name);
        Assert.Equal("save_chapter_content", SaveChapterContentTool.ToolDefinition.Function.Name);
        Assert.Equal("create_character_arc", CreateCharacterArcTool.ToolDefinition.Function.Name);
    }
}
