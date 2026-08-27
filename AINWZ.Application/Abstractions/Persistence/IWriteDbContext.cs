using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Entities.Tags;
using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Domain.Entities.World;

namespace SpeakEase.Write.Application.Abstractions.Persistence;

public interface IWriteDbContext
{
    DbSet<UserEntity> Users { get; }
    DbSet<UserPreferenceEntity> UserPreferences { get; }
    DbSet<UserAiModelConfigEntity> UserAiModelConfigs { get; }
    DbSet<WorkEntity> Works { get; }
    DbSet<VolumeEntity> Volumes { get; }
    DbSet<ChapterEntity> Chapters { get; }
    DbSet<ChapterVersionEntity> ChapterVersions { get; }
    DbSet<CharacterEntity> Characters { get; }
    DbSet<CharacterRelationshipEntity> CharacterRelationships { get; }
    DbSet<CharacterArcEntity> CharacterArcs { get; }
    DbSet<CharacterGraphEntity> CharacterGraphs { get; }
    DbSet<CharacterGraphNodeEntity> CharacterGraphNodes { get; }
    DbSet<CharacterGraphEdgeEntity> CharacterGraphEdges { get; }
    DbSet<OutlineEntity> Outlines { get; }
    DbSet<OutlineNodeEntity> OutlineNodes { get; }
    DbSet<ForeshadowingEntity> Foreshadowings { get; }
    DbSet<TimelineEventEntity> TimelineEvents { get; }
    DbSet<WorldSettingEntity> WorldSettings { get; }
    DbSet<WorldRuleEntity> WorldRules { get; }
    DbSet<PowerSystemEntity> PowerSystems { get; }
    DbSet<FactionEntity> Factions { get; }
    DbSet<GeographyEntity> Geographies { get; }
    DbSet<HistoricalEventEntity> HistoricalEvents { get; }
    DbSet<AIModelDefinitionEntity> AIModelDefinitions { get; }
    DbSet<AIGenerationTaskEntity> AIGenerationTasks { get; }
    DbSet<AIGenerationResultEntity> AIGenerationResults { get; }
    DbSet<ChapterAnalysisResultEntity> ChapterAnalysisResults { get; }
    DbSet<LLMCallLogEntity> LlmCallLogs { get; }
    DbSet<PromptTemplateEntity> PromptTemplates { get; }
    DbSet<AICreationSessionEntity> AICreationSessions { get; }
    DbSet<AICreationMessageEntity> AICreationMessages { get; }
    DbSet<AgentRunEntity> AgentRuns { get; }
    DbSet<AgentRunEventEntity> AgentRunEvents { get; }
    DbSet<AgentToolCallEntity> AgentToolCalls { get; }
    DbSet<AgentArtifactEntity> AgentArtifacts { get; }
    DbSet<MemorySnapshotEntity> MemorySnapshots { get; }
    DbSet<MemoryFactEntity> MemoryFacts { get; }
    DbSet<ContextAssemblyLogEntity> ContextAssemblyLogs { get; }
    DbSet<ReferenceWorkEntity> ReferenceWorks { get; }
    DbSet<ReferencePassageEntity> ReferencePassages { get; }
    DbSet<InspirationRecordEntity> InspirationRecords { get; }
    DbSet<TagEntity> Tags { get; }
    DbSet<UserPassageFavoriteEntity> UserPassageFavorites { get; }

    DatabaseFacade Database { get; }

    void Detach(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
