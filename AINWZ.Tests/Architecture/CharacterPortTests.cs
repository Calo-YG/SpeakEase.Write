using SpeakEase.Write.Infrastructure.AI.Tools;

namespace AINWZ.Tests.Architecture;

public sealed class CharacterPortTests
{
    [Theory]
    [InlineData(typeof(CreateCharacterTool))]
    [InlineData(typeof(GetCharacterTool))]
    [InlineData(typeof(GetCharacterListTool))]
    [InlineData(typeof(SearchCharactersTool))]
    [InlineData(typeof(UpdateCharacterTool))]
    [InlineData(typeof(CreateRelationshipTool))]
    [InlineData(typeof(GetRelationshipsTool))]
    [InlineData(typeof(CreateCharacterArcTool))]
    [InlineData(typeof(GetCharacterArcTool))]
    [InlineData(typeof(CreateCharacterGraphTool))]
    [InlineData(typeof(GetCharacterGraphTool))]
    [InlineData(typeof(CreateCharacterGraphNodeTool))]
    [InlineData(typeof(CreateCharacterGraphEdgeTool))]
    public void CharacterTools_UseTheNarrowCharacterPersistencePort(Type toolType)
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

        Assert.Contains("GetRequiredService<ICharacterDbContext>()", source);
        Assert.DoesNotContain("GetRequiredService<IWriteDbContext>()", source);
    }
}
