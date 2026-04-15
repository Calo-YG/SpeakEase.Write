using AINWZ.Domain.Entities.AI;
using AINWZ.Domain.Entities.Learning;
using AINWZ.Domain.Entities.Memory;
using AINWZ.Domain.Entities.Story;
using AINWZ.Domain.Entities.Users;
using AINWZ.Domain.Entities.Works;
using AINWZ.Domain.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace AINWZ.Infrastructure.Persistence;

/// <summary>
/// AINWZ 应用数据库上下文。
/// </summary>
public class AINWZDbContext(DbContextOptions<AINWZDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();
    public DbSet<UserAiModelConfigEntity> UserAiModelConfigs => Set<UserAiModelConfigEntity>();
    public DbSet<WorkEntity> Works => Set<WorkEntity>();
    public DbSet<VolumeEntity> Volumes => Set<VolumeEntity>();
    public DbSet<ChapterEntity> Chapters => Set<ChapterEntity>();
    public DbSet<ChapterVersionEntity> ChapterVersions => Set<ChapterVersionEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<CharacterRelationshipEntity> CharacterRelationships => Set<CharacterRelationshipEntity>();
    public DbSet<CharacterArcEntity> CharacterArcs => Set<CharacterArcEntity>();
    public DbSet<CharacterGraphEntity> CharacterGraphs => Set<CharacterGraphEntity>();
    public DbSet<CharacterGraphNodeEntity> CharacterGraphNodes => Set<CharacterGraphNodeEntity>();
    public DbSet<CharacterGraphEdgeEntity> CharacterGraphEdges => Set<CharacterGraphEdgeEntity>();
    public DbSet<OutlineEntity> Outlines => Set<OutlineEntity>();
    public DbSet<OutlineNodeEntity> OutlineNodes => Set<OutlineNodeEntity>();
    public DbSet<ForeshadowingEntity> Foreshadowings => Set<ForeshadowingEntity>();
    public DbSet<TimelineEventEntity> TimelineEvents => Set<TimelineEventEntity>();
    public DbSet<WorldSettingEntity> WorldSettings => Set<WorldSettingEntity>();
    public DbSet<WorldRuleEntity> WorldRules => Set<WorldRuleEntity>();
    public DbSet<PowerSystemEntity> PowerSystems => Set<PowerSystemEntity>();
    public DbSet<FactionEntity> Factions => Set<FactionEntity>();
    public DbSet<GeographyEntity> Geographies => Set<GeographyEntity>();
    public DbSet<HistoricalEventEntity> HistoricalEvents => Set<HistoricalEventEntity>();
    public DbSet<AIModelDefinitionEntity> AIModelDefinitions => Set<AIModelDefinitionEntity>();
    public DbSet<AIGenerationTaskEntity> AIGenerationTasks => Set<AIGenerationTaskEntity>();
    public DbSet<AIGenerationResultEntity> AIGenerationResults => Set<AIGenerationResultEntity>();
    public DbSet<LLMCallLogEntity> LlmCallLogs => Set<LLMCallLogEntity>();
    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();
    public DbSet<MemoryChunkEntity> MemoryChunks => Set<MemoryChunkEntity>();
    public DbSet<MemorySnapshotEntity> MemorySnapshots => Set<MemorySnapshotEntity>();
    public DbSet<ContextAssemblyLogEntity> ContextAssemblyLogs => Set<ContextAssemblyLogEntity>();
    public DbSet<ReferenceWorkEntity> ReferenceWorks => Set<ReferenceWorkEntity>();
    public DbSet<ReferencePassageEntity> ReferencePassages => Set<ReferencePassageEntity>();
    public DbSet<InspirationRecordEntity> InspirationRecords => Set<InspirationRecordEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AINWZDbContext).Assembly);
    }
}
