using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;

namespace AINWZ.Tests.AI;

public sealed class ToolExposurePolicyTests
{
    [Fact]
    public void Select_CommitPhase_ExposesOnlyAllowedWriteTools()
    {
        var registry = new ToolRegistry();
        registry.Register(CreateDefinition("save_chapter_content"), new ToolCapabilityDescriptor
        {
            Name = "save_chapter_content",
            Group = "chapter.write",
            ReadOnly = false,
            RequiredPhases = new[] { "commit" }
        });
        registry.Register(CreateDefinition("get_character"), new ToolCapabilityDescriptor
        {
            Name = "get_character",
            Group = "character.read",
            ReadOnly = true,
            RequiredPhases = new[] { "context_loading" }
        });
        registry.Register(CreateDefinition("web_search"), new ToolCapabilityDescriptor
        {
            Name = "web_search",
            Group = "system.high-risk",
            RequiresExplicitConsent = true
        });

        var policy = new ToolExposurePolicy(registry);
        var selected = policy.Select(new ToolExposureContext
        {
            AgentName = "write",
            Phase = "commit",
            AllowedGroups = new[] { "chapter.write", "character.read", "system.high-risk" }
        });

        var names = selected.Select(x => x.Function.Name).ToArray();
        Assert.Equal(new[] { "save_chapter_content" }, names);
    }

    [Fact]
    public void Select_RequiresExplicitConsentForHighRiskTool()
    {
        var registry = new ToolRegistry();
        registry.Register(CreateDefinition("run_powershell"), new ToolCapabilityDescriptor
        {
            Name = "run_powershell",
            Group = "system.high-risk",
            RequiresExplicitConsent = true
        });

        var policy = new ToolExposurePolicy(registry);

        Assert.Empty(policy.Select(new ToolExposureContext
        {
            Phase = "generate",
            AllowedGroups = new[] { "system.high-risk" },
            HasExplicitConsent = false
        }));

        Assert.Single(policy.Select(new ToolExposureContext
        {
            Phase = "generate",
            AllowedGroups = new[] { "system.high-risk" },
            HasExplicitConsent = true
        }));
    }

    [Fact]
    public void Select_RunPhase_PrioritizesProfileAndBoundsVisibleTools()
    {
        var registry = new ToolRegistry();
        for (var i = 0; i < 20; i++)
            registry.Register(CreateDefinition($"get_tool_{i:00}"));
        registry.Register(CreateDefinition("save_chapter_content"));

        var selected = new ToolExposurePolicy(registry).Select(new ToolExposureContext
        {
            AgentName = "write",
            Phase = "run",
            AllowedGroups = new[] { "system.legacy.read", "chapter.write" },
            PreferredTools = new[] { "save_chapter_content" },
            MaxTools = 12
        });

        Assert.Equal(12, selected.Count);
        Assert.Equal("save_chapter_content", selected[0].Function.Name);
    }

    [Fact]
    public void Select_EmptyCapabilityGroups_FailsClosed()
    {
        var registry = new ToolRegistry();
        registry.Register(CreateDefinition("save_chapter_content"));

        var selected = new ToolExposurePolicy(registry).Select(new ToolExposureContext
        {
            AgentName = "write",
            Phase = "run",
            AllowedGroups = Array.Empty<string>(),
            MaxTools = 12
        });

        Assert.Empty(selected);
    }

    [Fact]
    public void Select_ZeroToolBudget_ExposesNothing()
    {
        var registry = new ToolRegistry();
        registry.Register(CreateDefinition("get_character"));

        var selected = new ToolExposurePolicy(registry).Select(new ToolExposureContext
        {
            AgentName = "creation",
            Phase = "run",
            AllowedGroups = new[] { "system.legacy.read" },
            MaxTools = 0
        });

        Assert.Empty(selected);
    }

    private static ToolDefinition CreateDefinition(string name)
    {
        return new ToolDefinition
        {
            Function = new FunctionDefinition
            {
                Name = name,
                Parameters = new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, ParameterSchema>()
                }
            }
        };
    }
}
