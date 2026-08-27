using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.AI.Runtime;

namespace AINWZ.Tests.Architecture;

public sealed class AgentRuntimePortTests
{
    [Fact]
    public void AgentRunStore_UsesTheNarrowRuntimePersistencePort()
    {
        var constructor = typeof(AgentRunStore).GetConstructors().Single();
        var databaseParameter = constructor.GetParameters().First().ParameterType;

        Assert.Equal(typeof(IAgentRuntimeDbContext), databaseParameter);
    }
}
