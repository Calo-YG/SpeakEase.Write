using SpeakEase.Write.Application.Repositories;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.Write.Domain.Repositories;

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

        services.AddOptions<SnowflakeIdOptions>()
            .Bind(configuration.GetSection(SnowflakeIdOptions.SectionName));

        services.AddSingleton<ISnowflakeIdGenerator>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<SnowflakeIdOptions>>().Value;
            return new SnowflakeIdGenerator(options.WorkerId, options.MaxBackwardMilliseconds);
        });

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
