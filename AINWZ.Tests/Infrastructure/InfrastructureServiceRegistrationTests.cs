using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEase.Write.Infrastructure.Persistence;

namespace AINWZ.Tests.Infrastructure;

public sealed class InfrastructureServiceRegistrationTests
{
    [Fact]
    public void AddInfrastructurePersistence_RegistersWriteDbContextPortToScopedDbContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:SpeakEaseWrite"] =
                    "Host=localhost;Database=ainwz_test;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructurePersistence(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var concrete = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var abstraction = scope.ServiceProvider.GetService<IWriteDbContext>();
        var runtimePort = scope.ServiceProvider.GetService<IAgentRuntimeDbContext>();
        var memoryPort = scope.ServiceProvider.GetService<IMemoryDbContext>();
        var creationSessionPort = scope.ServiceProvider.GetService<ICreationSessionDbContext>();

        Assert.NotNull(abstraction);
        Assert.Same(concrete, abstraction);
        Assert.NotNull(runtimePort);
        Assert.Same(concrete, runtimePort);
        Assert.NotNull(memoryPort);
        Assert.Same(concrete, memoryPort);
        Assert.NotNull(creationSessionPort);
        Assert.Same(concrete, creationSessionPort);
    }
}
