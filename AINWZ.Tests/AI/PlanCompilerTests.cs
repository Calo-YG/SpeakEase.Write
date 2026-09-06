using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace AINWZ.Tests.AI;

public sealed class PlanCompilerTests
{
    [Fact]
    public void Compile_UsesPrimaryAgentWhenIntentHasNoExplicitPlan()
    {
        var plan = new PlanCompiler().Compile(
            new IntentResolution { PrimaryAgent = "general" },
            new[] { "general", "write" });

        var step = Assert.Single(plan.Steps);
        Assert.Equal("general", step.AgentName);
        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void Compile_TopologicallySortsExplicitDag()
    {
        var plan = new PlanCompiler().Compile(
            new IntentResolution
            {
                PrimaryAgent = "write",
                PlanSteps = new[]
                {
                    new AgentPlanCandidateStep { Id = "review", AgentName = "critique", DependsOn = new[] { "write" } },
                    new AgentPlanCandidateStep { Id = "write", AgentName = "write" }
                }
            },
            new[] { "write", "critique" });

        Assert.Equal(new[] { "write", "review" }, plan.Steps.Select(x => x.Id));
        Assert.Equal(new[] { "write" }, plan.Steps[1].DependsOn);
    }

    [Fact]
    public void Compile_RejectsCyclesAndUnknownDependencies()
    {
        var compiler = new PlanCompiler();

        Assert.Throws<InvalidOperationException>(() => compiler.Compile(
            new IntentResolution
            {
                PrimaryAgent = "write",
                PlanSteps = new[]
                {
                    new AgentPlanCandidateStep { Id = "a", AgentName = "write", DependsOn = new[] { "b" } },
                    new AgentPlanCandidateStep { Id = "b", AgentName = "write", DependsOn = new[] { "a" } }
                }
            },
            new[] { "write" }));

        Assert.Throws<InvalidOperationException>(() => compiler.Compile(
            new IntentResolution
            {
                PrimaryAgent = "write",
                PlanSteps = new[]
                {
                    new AgentPlanCandidateStep { Id = "write", AgentName = "write", DependsOn = new[] { "missing" } }
                }
            },
            new[] { "write" }));
    }
}
