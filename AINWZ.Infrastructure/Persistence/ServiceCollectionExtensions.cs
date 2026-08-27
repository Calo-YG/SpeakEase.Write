using SpeakEase.Write.Domain.Repositories;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ApplicationDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using ApplicationAgentRuntimeDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IAgentRuntimeDbContext;
using ApplicationMemoryDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IMemoryDbContext;
using ApplicationIdGenerator = SpeakEase.Write.Application.Abstractions.Ids.ISnowflakeIdGenerator;

namespace SpeakEase.Write.Infrastructure.Persistence;

/// <summary>
/// 基础设施层依赖注入扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 EF Core 上下文、基础设施能力与聚合根仓储。
    /// </summary>
    public static IServiceCollection AddInfrastructurePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SpeakEaseWrite");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'SpeakEaseWrite' is required.");
        }

        services.AddDbContext<SpeakEaseDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<SpeakEaseDbContext>());
        services.AddScoped<ApplicationAgentRuntimeDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<SpeakEaseDbContext>());
        services.AddScoped<ApplicationMemoryDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<SpeakEaseDbContext>());

        services.AddOptions<SnowflakeIdOptions>()
            .Bind(configuration.GetSection(SnowflakeIdOptions.SectionName));

        services.AddSingleton<ISnowflakeIdGenerator>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SnowflakeIdOptions>>().Value;
            return new SnowflakeIdGenerator(options.WorkerId, options.MaxBackwardMilliseconds);
        });
        services.AddSingleton<ApplicationIdGenerator>(sp => sp.GetRequiredService<ISnowflakeIdGenerator>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkRepository, WorkRepository>();
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IOutlineRepository, OutlineRepository>();
        services.AddScoped<IWorldSettingRepository, WorldSettingRepository>();
        services.AddScoped<IAIModelDefinitionRepository, AIModelDefinitionRepository>();
        services.AddScoped<IAIGenerationTaskRepository, AIGenerationTaskRepository>();
        services.AddScoped<IMemorySnapshotRepository, MemorySnapshotRepository>();
        services.AddScoped<IReferenceWorkRepository, ReferenceWorkRepository>();

        return services;
    }
}
