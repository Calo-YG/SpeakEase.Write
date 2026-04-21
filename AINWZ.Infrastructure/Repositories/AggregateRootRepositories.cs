using AINWZ.Application.Repositories;
using AINWZ.Domain.Entities.AI;
using AINWZ.Domain.Entities.Learning;
using AINWZ.Domain.Entities.Memory;
using AINWZ.Domain.Entities.Story;
using AINWZ.Domain.Entities.Users;
using AINWZ.Domain.Entities.Works;
using AINWZ.Domain.Entities.World;
using AINWZ.Infrastructure.Persistence;

namespace AINWZ.Infrastructure.Repositories;

public sealed class UserRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<UserEntity>(dbContext), IUserRepository;
public sealed class WorkRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<WorkEntity>(dbContext), IWorkRepository;
public sealed class ChapterRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<ChapterEntity>(dbContext), IChapterRepository;
public sealed class CharacterRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<CharacterEntity>(dbContext), ICharacterRepository;
public sealed class OutlineRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<OutlineEntity>(dbContext), IOutlineRepository;
public sealed class WorldSettingRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<WorldSettingEntity>(dbContext), IWorldSettingRepository;
public sealed class AIModelDefinitionRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<AIModelDefinitionEntity>(dbContext), IAIModelDefinitionRepository;
public sealed class AIGenerationTaskRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<AIGenerationTaskEntity>(dbContext), IAIGenerationTaskRepository;
public sealed class MemorySnapshotRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<MemorySnapshotEntity>(dbContext), IMemorySnapshotRepository;
public sealed class ReferenceWorkRepository(SpeakEaseDbContext dbContext) : EfAggregateRootRepository<ReferenceWorkEntity>(dbContext), IReferenceWorkRepository;
