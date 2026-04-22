using SpeakEase.Write.Application.Repositories;
using SpeakEase.Write.Domain.Entities.AI;
using SpeakEase.Write.Domain.Entities.Learning;
using SpeakEase.Write.Domain.Entities.Memory;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Domain.Entities.Users;
using SpeakEase.Write.Domain.Entities.Works;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Domain.Repositories;
using SpeakEase.Write.Infrastructure.Repositories;

namespace SpeakEase.Write.Infrastructure.Repositories;

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
