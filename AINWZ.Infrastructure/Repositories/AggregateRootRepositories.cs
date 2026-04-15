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

public sealed class UserRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<UserEntity>(dbContext), IUserRepository;
public sealed class WorkRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<WorkEntity>(dbContext), IWorkRepository;
public sealed class ChapterRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<ChapterEntity>(dbContext), IChapterRepository;
public sealed class CharacterRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<CharacterEntity>(dbContext), ICharacterRepository;
public sealed class OutlineRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<OutlineEntity>(dbContext), IOutlineRepository;
public sealed class WorldSettingRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<WorldSettingEntity>(dbContext), IWorldSettingRepository;
public sealed class AIModelDefinitionRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<AIModelDefinitionEntity>(dbContext), IAIModelDefinitionRepository;
public sealed class AIGenerationTaskRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<AIGenerationTaskEntity>(dbContext), IAIGenerationTaskRepository;
public sealed class MemorySnapshotRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<MemorySnapshotEntity>(dbContext), IMemorySnapshotRepository;
public sealed class ReferenceWorkRepository(AINWZDbContext dbContext) : EfAggregateRootRepository<ReferenceWorkEntity>(dbContext), IReferenceWorkRepository;
