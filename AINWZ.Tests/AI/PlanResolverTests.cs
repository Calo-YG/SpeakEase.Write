using SpeakEase.Write.Infrastructure.AI.Orchestrator;

namespace AINWZ.Tests.AI;

public sealed class PlanResolverTests
{
    [Fact]
    public void Resolve_RejectsUnregisteredAgent()
    {
        var resolver = new PlanResolver();

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            new[] { "write", "unknown" },
            new[] { "write", "critique" }));
    }

    [Fact]
    public void Resolve_CreatesExplicitLinearDependencies()
    {
        var plan = new PlanResolver().Resolve(
            new[] { "write", "critique" },
            new[] { "write", "critique" });

        Assert.Equal("step-1", plan.Steps[0].Id);
        Assert.Empty(plan.Steps[0].DependsOn);
        Assert.Equal(new[] { "step-1" }, plan.Steps[1].DependsOn);
    }
}
