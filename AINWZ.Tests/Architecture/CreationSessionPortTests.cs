using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Application.Applications;

namespace AINWZ.Tests.Architecture;

public sealed class CreationSessionPortTests
{
    [Fact]
    public void CreationSessionManager_UsesTheNarrowCreationSessionPersistencePort()
    {
        var constructor = typeof(CreationSessionManager).GetConstructors().Single();

        Assert.Equal(typeof(ICreationSessionDbContext), constructor.GetParameters().First().ParameterType);
    }
}
