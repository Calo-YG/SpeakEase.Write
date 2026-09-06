using SpeakEase.AI.Lib;

namespace AINWZ.Tests.AI;

public sealed class SkillCatalogLoaderTests
{
    [Fact]
    public async Task RegisterFromDirectory_BlankMetadataNameFallsBackToSkillDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ainwz-skills-{Guid.NewGuid():N}");
        var skillDirectory = Path.Combine(root, "character-growth");
        Directory.CreateDirectory(skillDirectory);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        await File.WriteAllTextAsync(
            skillPath,
            "---\nname: \ndescription: Tracks confirmed character state\n---\n# Workflow");
        try
        {
            var catalog = new SkillCapable();

            SkillCatalogLoader.RegisterFromDirectory(catalog, root);

            var skill = Assert.Single(catalog.Skills);
            Assert.Equal("character-growth", skill.Name);
            Assert.Equal("Tracks confirmed character state", skill.Description);
            Assert.Equal(Path.GetFullPath(skillPath), skill.Path);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
