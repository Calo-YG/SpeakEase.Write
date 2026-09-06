using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.AI.Lib.Runtime;
using SpeakEase.Write.Infrastructure.AI.Agents;
using SpeakEase.Write.Infrastructure.AI.Contract;

namespace AINWZ.Tests.AI;

public sealed class AgentPromptProfileTests
{
    [Fact]
    public void ProfessionalAgentProfiles_DoNotContainRuntimeWorkflowScripts()
    {
        var llm = Mock.Of<IChatCompatible>();
        var tools = Mock.Of<IToolCapable>();
        INovelAgent[] agents =
        {
            new GeneralAgent(llm, tools, NullLogger<GeneralAgent>.Instance),
            new WriteAgent(llm, tools, NullLogger<WriteAgent>.Instance),
            new WorldAgent(llm, tools, NullLogger<WorldAgent>.Instance),
            new OutlineAgent(llm, tools, NullLogger<OutlineAgent>.Instance),
            new CreationAgent(llm, tools, NullLogger<CreationAgent>.Instance),
            new CritiqueAgent(llm, tools, NullLogger<CritiqueAgent>.Instance),
            new AuditAgent(llm, tools, NullLogger<AuditAgent>.Instance)
        };
        var composer = new PromptComposer();

        foreach (var agent in agents)
        {
            var prompt = composer.Compose(agent.BuildPromptProfile());

            Assert.DoesNotContain("ReAct", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Thought", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("严格遵循", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(agent.BuildPromptProfile().Objective));
        }
    }

    [Fact]
    public void ProfessionalAgentDescriptors_DeclareToolCapabilitiesExplicitly()
    {
        var llm = Mock.Of<IChatCompatible>();
        var tools = Mock.Of<IToolCapable>();
        IAgentDefinition[] agents =
        {
            new GeneralAgent(llm, tools, NullLogger<GeneralAgent>.Instance),
            new WriteAgent(llm, tools, NullLogger<WriteAgent>.Instance),
            new WorldAgent(llm, tools, NullLogger<WorldAgent>.Instance),
            new OutlineAgent(llm, tools, NullLogger<OutlineAgent>.Instance),
            new CreationAgent(llm, tools, NullLogger<CreationAgent>.Instance),
            new CritiqueAgent(llm, tools, NullLogger<CritiqueAgent>.Instance),
            new AuditAgent(llm, tools, NullLogger<AuditAgent>.Instance)
        };

        foreach (var agent in agents)
        {
            if (agent.Descriptor.Name == "critique")
            {
                Assert.Empty(agent.Descriptor.ToolGroups);
                Assert.Empty(agent.Descriptor.PreferredTools);
            }
            else
            {
                Assert.NotEmpty(agent.Descriptor.ToolGroups);
                Assert.NotEmpty(agent.Descriptor.PreferredTools);
            }
        }
    }
}
