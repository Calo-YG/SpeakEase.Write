using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Entities.Tags;
using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SpeakEase.Write.Infrastructure.Persistence;

/// <summary>
/// SpeakEase.Write 应用数据库上下文。
/// </summary>
public class SpeakEaseDbContext(DbContextOptions<SpeakEaseDbContext> options) : DbContext(options), IWriteDbContext, IAgentRuntimeDbContext, IMemoryDbContext, ICreationSessionDbContext, IStoryDbContext
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
    public DbSet<ChapterAnalysisResultEntity> ChapterAnalysisResults => Set<ChapterAnalysisResultEntity>();
    public DbSet<LLMCallLogEntity> LlmCallLogs => Set<LLMCallLogEntity>();
    public DbSet<PromptTemplateEntity> PromptTemplates => Set<PromptTemplateEntity>();
    public DbSet<AICreationSessionEntity> AICreationSessions => Set<AICreationSessionEntity>();
    public DbSet<AICreationMessageEntity> AICreationMessages => Set<AICreationMessageEntity>();
    public DbSet<MemorySnapshotEntity> MemorySnapshots => Set<MemorySnapshotEntity>();
    public DbSet<MemoryFactEntity> MemoryFacts => Set<MemoryFactEntity>();
    public DbSet<ContextAssemblyLogEntity> ContextAssemblyLogs => Set<ContextAssemblyLogEntity>();
    public DbSet<AgentRunEntity> AgentRuns => Set<AgentRunEntity>();
    public DbSet<AgentRunEventEntity> AgentRunEvents => Set<AgentRunEventEntity>();
    public DbSet<AgentToolCallEntity> AgentToolCalls => Set<AgentToolCallEntity>();
    public DbSet<AgentArtifactEntity> AgentArtifacts => Set<AgentArtifactEntity>();
    public DbSet<ReferenceWorkEntity> ReferenceWorks => Set<ReferenceWorkEntity>();
    public DbSet<ReferencePassageEntity> ReferencePassages => Set<ReferencePassageEntity>();
    public DbSet<InspirationRecordEntity> InspirationRecords => Set<InspirationRecordEntity>();
    public DbSet<TagEntity> Tags => Set<TagEntity>();
    public DbSet<UserPassageFavoriteEntity> UserPassageFavorites => Set<UserPassageFavoriteEntity>();

    public void Detach(object entity)
        => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpeakEaseDbContext).Assembly);
    }
}
