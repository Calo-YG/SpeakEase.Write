using SpeakEase.AI.Lib;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class LegacySkillResolverAdapterTests
{
    [Fact]
    public async Task ResolveAsync_LoadsRegisteredSkillDocumentBody()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ainwz-skill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "SKILL.md");
        await File.WriteAllTextAsync(path, "# Story Skill\nUse the evidence-driven workflow.");
        try
        {
            var skills = new SkillCapable();
            skills.RegiSkill(new SkillDefinition
            {
                Name = "story-skill",
                Description = "summary only",
                Path = path
            });

            var resolved = await new LegacySkillResolverAdapter(skills).ResolveAsync("story-skill");

            Assert.Contains("Use the evidence-driven workflow.", resolved.Content);
            Assert.DoesNotContain("summary only", resolved.Content);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
