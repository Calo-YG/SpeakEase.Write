using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.AI.Context;
using SpeakEase.Write.Infrastructure.AI.Memory;

namespace AINWZ.Tests.Architecture;

public sealed class MemoryPortTests
{
    [Fact]
    public void HybridMemoryProvider_UsesTheNarrowMemoryPersistencePort()
    {
        var constructor = typeof(HybridMemoryProvider).GetConstructors().Single();

        Assert.Equal(typeof(IMemoryDbContext), constructor.GetParameters().First().ParameterType);
    }

    [Fact]
    public void CreationAgentContext_UsesTheNarrowMemoryPersistencePort()
    {
        var constructor = typeof(CreationAgentContext).GetConstructors().Single();

        Assert.Equal(typeof(IMemoryDbContext), constructor.GetParameters()[2].ParameterType);
    }
}
