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

        Assert.NotNull(abstraction);
        Assert.Same(concrete, abstraction);
    }
}
