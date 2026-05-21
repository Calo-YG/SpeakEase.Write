using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

/// <summary>
/// 角色管理应用服务实现。
/// </summary>
public class CharacterApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<CharacterApplication> logger) : ICharacterApplication
{
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    private static CharacterItemResponse ToResponse(CharacterEntity x) => new()
    {
        Id = x.Id,
        WorkId = x.WorkId,
        Name = x.Name,
        Alias = x.Alias,
        Gender = x.Gender,
        AgeDescription = x.AgeDescription,
        Identity = x.Identity,
        Appearance = x.Appearance,
        Personality = x.Personality,
        BackgroundStory = x.BackgroundStory,
        Motivation = x.Motivation,
        AbilityDescription = x.AbilityDescription,
        Tags = x.Tags
    };

    public async Task<ApiResult<List<CharacterItemResponse>>> ListCharactersAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<CharacterItemResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.Characters.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.CreateAt)
            .ToListAsync(cancellationToken);

        return new ApiResult<List<CharacterItemResponse>>(list.Select(ToResponse).ToList());
    }

    public async Task<ApiResult<CharacterItemResponse>> GetCharacterByIdAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Characters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<CharacterItemResponse>("角色不存在。", 404);

        return new ApiResult<CharacterItemResponse>(ToResponse(entity));
    }

    public async Task<ApiResult<CharacterItemResponse>> CreateCharacterAsync(string workId, SaveCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterItemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<CharacterItemResponse>("角色名称不能为空。", 400);

        var entity = new CharacterEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            Name = request.Name.Trim(),
            Alias = request.Alias ?? string.Empty,
            Gender = request.Gender ?? string.Empty,
            AgeDescription = request.AgeDescription ?? string.Empty,
            Identity = request.Identity ?? string.Empty,
            Appearance = request.Appearance ?? string.Empty,
            Personality = request.Personality ?? string.Empty,
            BackgroundStory = request.BackgroundStory ?? string.Empty,
            Motivation = request.Motivation ?? string.Empty,
            AbilityDescription = request.AbilityDescription ?? string.Empty,
            Tags = request.Tags ?? new(),
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Characters.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 在作品 {WorkId} 创建角色：{Name}", userId, workId, entity.Name);

        return new ApiResult<CharacterItemResponse>(ToResponse(entity));
    }

    public async Task<ApiResult<CharacterItemResponse>> UpdateCharacterAsync(string workId, string id, SaveCharacterRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<CharacterItemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Characters
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<CharacterItemResponse>("角色不存在。", 404);

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.Alias is not null) entity.Alias = request.Alias;
        if (request.Gender is not null) entity.Gender = request.Gender;
        if (request.AgeDescription is not null) entity.AgeDescription = request.AgeDescription;
        if (request.Identity is not null) entity.Identity = request.Identity;
        if (request.Appearance is not null) entity.Appearance = request.Appearance;
        if (request.Personality is not null) entity.Personality = request.Personality;
        if (request.BackgroundStory is not null) entity.BackgroundStory = request.BackgroundStory;
        if (request.Motivation is not null) entity.Motivation = request.Motivation;
        if (request.AbilityDescription is not null) entity.AbilityDescription = request.AbilityDescription;
        if (request.Tags is not null) entity.Tags = request.Tags;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 更新角色：{Name}，Id={Id}", userId, entity.Name, entity.Id);

        return new ApiResult<CharacterItemResponse>(ToResponse(entity));
    }

    public async Task<ApiResult> DeleteCharacterAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Characters
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult("角色不存在。", 404);

        dbContext.Characters.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 删除角色：{Name}，Id={Id}", userId, entity.Name, entity.Id);

        return new ApiResult(true);
    }
}
