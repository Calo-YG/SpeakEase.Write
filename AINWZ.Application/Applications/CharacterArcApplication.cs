using Microsoft.EntityFrameworkCore;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

// 角色成长弧线应用服务：管理角色故事发展轨迹的阶段定义（初始状态→触发事件→变化状态）
public class CharacterArcApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext) : ICharacterArcApplication
{
    // 验证当前用户是否拥有该作品的操作权限
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // 按角色查询其所有成长弧线阶段，按阶段序号升序排列
    public async Task<ApiResult<List<CharacterArcResponse>>> ListArcsByCharacterAsync(string workId, string characterId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<CharacterArcResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.CharacterArcs.AsNoTracking()
            .Where(x => x.WorkId == workId && x.CharacterId == characterId)
            .OrderBy(x => x.StageOrder)
            .Select(x => new CharacterArcResponse
            {
                Id = x.Id, WorkId = x.WorkId, CharacterId = x.CharacterId,
                StageOrder = x.StageOrder, StageTitle = x.StageTitle,
                InitialState = x.InitialState, ChangedState = x.ChangedState,
                TriggerEvent = x.TriggerEvent
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<CharacterArcResponse>>(list);
    }

    // 查询作品下所有角色的成长弧线，先按角色ID再按阶段序号排序
    public async Task<ApiResult<List<CharacterArcResponse>>> ListAllArcsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<CharacterArcResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.CharacterArcs.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.CharacterId).ThenBy(x => x.StageOrder)
            .Select(x => new CharacterArcResponse
            {
                Id = x.Id, WorkId = x.WorkId, CharacterId = x.CharacterId,
                StageOrder = x.StageOrder, StageTitle = x.StageTitle,
                InitialState = x.InitialState, ChangedState = x.ChangedState,
                TriggerEvent = x.TriggerEvent
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<CharacterArcResponse>>(list);
    }

    // 创建角色成长弧线阶段：定义角色的初始状态、触发事件和变化后的状态
    public async Task<ApiResult<CharacterArcResponse>> CreateArcAsync(string workId, string characterId, SaveCharacterArcRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterArcResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.StageTitle))
            return new ApiResult<CharacterArcResponse>("阶段标题不能为空。", 400);

        var entity = new CharacterArcEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            CharacterId = characterId,
            OwnerId = userId,
            StageOrder = request.StageOrder,
            StageTitle = request.StageTitle,
            InitialState = request.InitialState,
            ChangedState = request.ChangedState,
            TriggerEvent = request.TriggerEvent,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.CharacterArcs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<CharacterArcResponse>(new CharacterArcResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, CharacterId = entity.CharacterId,
            StageOrder = entity.StageOrder, StageTitle = entity.StageTitle,
            InitialState = entity.InitialState, ChangedState = entity.ChangedState,
            TriggerEvent = entity.TriggerEvent
        });
    }

    // 更新成长弧线阶段：修改阶段标题、状态描述和触发事件
    public async Task<ApiResult<CharacterArcResponse>> UpdateArcAsync(string workId, string characterId, string arcId, SaveCharacterArcRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterArcResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterArcs
            .FirstOrDefaultAsync(x => x.Id == arcId && x.WorkId == workId && x.CharacterId == characterId, cancellationToken);

        if (entity == null)
            return new ApiResult<CharacterArcResponse>("成长弧线不存在。", 404);

        entity.StageOrder = request.StageOrder;
        entity.StageTitle = request.StageTitle;
        entity.InitialState = request.InitialState;
        entity.ChangedState = request.ChangedState;
        entity.TriggerEvent = request.TriggerEvent;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<CharacterArcResponse>(new CharacterArcResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, CharacterId = entity.CharacterId,
            StageOrder = entity.StageOrder, StageTitle = entity.StageTitle,
            InitialState = entity.InitialState, ChangedState = entity.ChangedState,
            TriggerEvent = entity.TriggerEvent
        });
    }

    // 删除成长弧线阶段：软删除（从DbContext中移除）
    public async Task<ApiResult> DeleteArcAsync(string workId, string characterId, string arcId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.CharacterArcs
            .FirstOrDefaultAsync(x => x.Id == arcId && x.WorkId == workId && x.CharacterId == characterId, cancellationToken);

        if (entity == null)
            return new ApiResult("成长弧线不存在。", 404);

        dbContext.CharacterArcs.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult(true);
    }
}
