using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.AI.Tools;

namespace AINWZ.Tests.Architecture;

public sealed class StoryPortTests
{
    [Theory]
    [InlineData(typeof(CreateChapterOutlineTool))]
    [InlineData(typeof(GetChapterBySequenceTool))]
    [InlineData(typeof(GetChapterTool))]
    [InlineData(typeof(GetChapterVersionsTool))]
    [InlineData(typeof(GetOutlineTool))]
    [InlineData(typeof(CreateOutlineTool))]
    [InlineData(typeof(CreateOutlineNodeTool))]
    [InlineData(typeof(SearchOutlineTool))]
    [InlineData(typeof(GetRecentChaptersTool))]
    [InlineData(typeof(ListVolumesTool))]
    [InlineData(typeof(SaveChapterContentTool))]
    [InlineData(typeof(UpdateChapterSummaryTool))]
    public void ChapterAndOutlineTools_UseTheNarrowStoryPersistencePort(Type toolType)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var sourcePath = Path.Combine(
            repositoryRoot,
            "AINWZ.Infrastructure",
            "AI",
            "Tools",
            $"{toolType.Name}.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("GetRequiredService<IStoryDbContext>()", source);
        Assert.DoesNotContain("GetRequiredService<IWriteDbContext>()", source);
    }
}
