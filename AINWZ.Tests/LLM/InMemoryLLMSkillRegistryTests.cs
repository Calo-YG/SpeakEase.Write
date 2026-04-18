using AINWZ.Infrastructure.LLM;

namespace AINWZ.Tests.LLM;

public class InMemoryLLMSkillRegistryTests
{
    [Fact]
    public void GetAll_ReturnsAllBuiltInSkills()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skills = registry.GetAll();

        Assert.Equal(3, skills.Count);
        Assert.Contains(skills, s => s.Name == "writer");
        Assert.Contains(skills, s => s.Name == "coder");
        Assert.Contains(skills, s => s.Name == "analyst");
    }

    [Fact]
    public void GetByName_Writer_ReturnsCorrectSkill()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("writer");

        Assert.NotNull(skill);
        Assert.Equal("writer", skill.Name);
        Assert.Contains("写作", skill.SystemPrompt);
    }

    [Fact]
    public void GetByName_Coder_ReturnsCorrectSkill()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("coder");

        Assert.NotNull(skill);
        Assert.Equal("coder", skill.Name);
        Assert.Contains("工程", skill.SystemPrompt);
    }

    [Fact]
    public void GetByName_Analyst_ReturnsCorrectSkill()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("analyst");

        Assert.NotNull(skill);
        Assert.Equal("analyst", skill.Name);
        Assert.Contains("分析", skill.SystemPrompt);
    }

    [Fact]
    public void GetByName_Unknown_ReturnsNull()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("unknown_skill");
        Assert.Null(skill);
    }

    [Fact]
    public void GetByName_Null_ReturnsNull()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName(null);
        Assert.Null(skill);
    }

    [Fact]
    public void GetByName_EmptyString_ReturnsNull()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("");
        Assert.Null(skill);
    }

    [Fact]
    public void GetByName_CaseInsensitive_MatchesCorrectly()
    {
        var registry = new InMemoryLLMSkillRegistry();
        var skill = registry.GetByName("WRITER");
        Assert.NotNull(skill);
        Assert.Equal("writer", skill.Name);
    }
}
