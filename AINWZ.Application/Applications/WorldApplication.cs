using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpeakEase.Write.Application.Abstractions.Identity;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Story.Dto;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Application.Abstractions.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;
using SpeakEaseDbContext = SpeakEase.Write.Application.Abstractions.Persistence.IWriteDbContext;
using SpeakEase.Write.Application.Shared;

namespace SpeakEase.Write.Application.Applications;

// 世界观应用服务：管理世界观设定、地理、势力、力量体系、世界规则、历史事件
public class WorldApplication(
    SpeakEaseDbContext dbContext,
    ISnowflakeIdGenerator idGenerator,
    IUserContext userContext,
    ILogger<WorldApplication> logger) : IWorldApplication
{
    // 验证作品归属权，确保用户只能操作自己的作品
    private async Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken ct)
        => await dbContext.Works.AnyAsync(x => x.Id == workId && x.UserId == userId, ct);

    // ═══════════ WorldSetting ═══════════

    // 获取或自动创建世界观设定：首次访问时如果不存在则自动创建空设定
    public async Task<ApiResult<WorldSettingResponse>> GetOrCreateWorldSettingAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<WorldSettingResponse>("作品不存在或无权访问。", 404);

        var setting = await dbContext.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);

        if (setting != null)
            return new ApiResult<WorldSettingResponse>(new WorldSettingResponse
            {
                Id = setting.Id, WorkId = setting.WorkId, WorldName = setting.WorldName,
                EraBackground = setting.EraBackground, OverallStyle = setting.OverallStyle,
                Summary = setting.Summary, CreatedAt = setting.CreateAt
            });

        var entity = new WorldSettingEntity
        {
            Id = idGenerator.NextIdString(),
            WorkId = workId,
            OwnerId = userId,
            WorldName = string.Empty,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.WorldSettings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("用户 {UserId} 为作品 {WorkId} 自动创建世界观设定", userId, workId);

        return new ApiResult<WorldSettingResponse>(new WorldSettingResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, WorldName = entity.WorldName,
            EraBackground = entity.EraBackground, OverallStyle = entity.OverallStyle,
            Summary = entity.Summary, CreatedAt = entity.CreateAt
        });
    }

    // 更新世界观设定：仅更新请求中非 null 的字段
    public async Task<ApiResult<WorldSettingResponse>> UpdateWorldSettingAsync(string workId, SaveWorldSettingRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<WorldSettingResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);

        if (entity is null)
            return new ApiResult<WorldSettingResponse>("世界观设定不存在。", 404);

        if (request.WorldName is not null) entity.WorldName = request.WorldName;
        if (request.EraBackground is not null) entity.EraBackground = request.EraBackground;
        if (request.OverallStyle is not null) entity.OverallStyle = request.OverallStyle;
        if (request.Summary is not null) entity.Summary = request.Summary;
        if (request.JsonContent is not null) entity.JsonContent = request.JsonContent;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<WorldSettingResponse>(new WorldSettingResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, WorldName = entity.WorldName,
            EraBackground = entity.EraBackground, OverallStyle = entity.OverallStyle,
            Summary = entity.Summary, CreatedAt = entity.CreateAt
        });
    }

    // ═══════════ Geography ═══════════

    // 查询作品下所有地理节点，按类型和名称排序
    public async Task<ApiResult<List<GeographyResponse>>> ListGeographiesAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<GeographyResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.Geographies.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.GeographyType).ThenBy(x => x.Name)
            .Select(x => new GeographyResponse
            {
                Id = x.Id, WorkId = x.WorkId, Name = x.Name,
                GeographyType = x.GeographyType, Description = x.Description,
                ParentGeographyId = x.ParentGeographyId, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<GeographyResponse>>(list);
    }

    // 创建地理节点：需先确保世界观设定已存在
    public async Task<ApiResult<GeographyResponse>> CreateGeographyAsync(string workId, SaveGeographyRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<GeographyResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<GeographyResponse>("地理名称不能为空。", 400);

        var setting = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<GeographyResponse>("请先创建世界观设定。", 400);

        var entity = new GeographyEntity
        {
            Id = idGenerator.NextIdString(),
            WorldSettingId = setting.Id,
            WorkId = workId,
            OwnerId = userId,
            Name = request.Name.Trim(),
            GeographyType = request.GeographyType ?? string.Empty,
            Description = request.Description ?? string.Empty,
            ParentGeographyId = request.ParentGeographyId ?? string.Empty,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Geographies.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<GeographyResponse>(new GeographyResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            GeographyType = entity.GeographyType, Description = entity.Description,
            ParentGeographyId = entity.ParentGeographyId, CreatedAt = entity.CreateAt
        });
    }

    // 更新地理节点：仅更新请求中非 null 的字段
    public async Task<ApiResult<GeographyResponse>> UpdateGeographyAsync(string workId, string id, SaveGeographyRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<GeographyResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Geographies
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult<GeographyResponse>("地理不存在。", 404);

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.GeographyType is not null) entity.GeographyType = request.GeographyType;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.ParentGeographyId is not null) entity.ParentGeographyId = request.ParentGeographyId;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<GeographyResponse>(new GeographyResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            GeographyType = entity.GeographyType, Description = entity.Description,
            ParentGeographyId = entity.ParentGeographyId, CreatedAt = entity.CreateAt
        });
    }

    // 删除地理节点（物理删除，不级联删除子节点）
    public async Task<ApiResult> DeleteGeographyAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Geographies
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult("地理不存在。", 404);

        dbContext.Geographies.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult(true);
    }

    // ═══════════ Faction ═══════════

    // 查询作品下所有势力，按类型和名称排序
    public async Task<ApiResult<List<FactionResponse>>> ListFactionsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<FactionResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.Factions.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.FactionType).ThenBy(x => x.Name)
            .Select(x => new FactionResponse
            {
                Id = x.Id, WorkId = x.WorkId, Name = x.Name,
                FactionType = x.FactionType, Description = x.Description,
                RelationshipJson = x.RelationshipJson, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<FactionResponse>>(list);
    }

    // 创建势力：需先确保世界观设定已存在
    public async Task<ApiResult<FactionResponse>> CreateFactionAsync(string workId, SaveFactionRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<FactionResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<FactionResponse>("势力名称不能为空。", 400);

        var setting = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<FactionResponse>("请先创建世界观设定。", 400);

        var entity = new FactionEntity
        {
            Id = idGenerator.NextIdString(),
            WorldSettingId = setting.Id,
            WorkId = workId,
            OwnerId = userId,
            Name = request.Name.Trim(),
            FactionType = request.FactionType ?? string.Empty,
            Description = request.Description ?? string.Empty,
            RelationshipJson = request.RelationshipJson ?? "{}",
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.Factions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<FactionResponse>(new FactionResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            FactionType = entity.FactionType, Description = entity.Description,
            RelationshipJson = entity.RelationshipJson, CreatedAt = entity.CreateAt
        });
    }

    // 更新势力：仅更新请求中非 null 的字段
    public async Task<ApiResult<FactionResponse>> UpdateFactionAsync(string workId, string id, SaveFactionRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<FactionResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.Factions
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult<FactionResponse>("势力不存在。", 404);

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.FactionType is not null) entity.FactionType = request.FactionType;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.RelationshipJson is not null) entity.RelationshipJson = request.RelationshipJson;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<FactionResponse>(new FactionResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            FactionType = entity.FactionType, Description = entity.Description,
            RelationshipJson = entity.RelationshipJson, CreatedAt = entity.CreateAt
        });
    }

    // 删除势力（物理删除）
    public async Task<ApiResult> DeleteFactionAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.Factions
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult("势力不存在。", 404);

        dbContext.Factions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult(true);
    }

    // ═══════════ PowerSystem ═══════════

    // 查询作品下所有力量体系，按名称排序
    public async Task<ApiResult<List<PowerSystemResponse>>> ListPowerSystemsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<PowerSystemResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.PowerSystems.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.Name)
            .Select(x => new PowerSystemResponse
            {
                Id = x.Id, WorkId = x.WorkId, Name = x.Name,
                LevelDefinitionJson = x.LevelDefinitionJson, AbilityRule = x.AbilityRule,
                ResourceSystem = x.ResourceSystem, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<PowerSystemResponse>>(list);
    }

    // 创建力量体系：需先确保世界观设定已存在
    public async Task<ApiResult<PowerSystemResponse>> CreatePowerSystemAsync(string workId, SavePowerSystemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<PowerSystemResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Name))
            return new ApiResult<PowerSystemResponse>("体系名称不能为空。", 400);

        var setting = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<PowerSystemResponse>("请先创建世界观设定。", 400);

        var entity = new PowerSystemEntity
        {
            Id = idGenerator.NextIdString(),
            WorldSettingId = setting.Id,
            WorkId = workId,
            OwnerId = userId,
            Name = request.Name.Trim(),
            LevelDefinitionJson = request.LevelDefinitionJson ?? "{}",
            AbilityRule = request.AbilityRule ?? string.Empty,
            ResourceSystem = request.ResourceSystem ?? string.Empty,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.PowerSystems.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<PowerSystemResponse>(new PowerSystemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            LevelDefinitionJson = entity.LevelDefinitionJson, AbilityRule = entity.AbilityRule,
            ResourceSystem = entity.ResourceSystem, CreatedAt = entity.CreateAt
        });
    }

    // 更新力量体系：仅更新请求中非 null 的字段
    public async Task<ApiResult<PowerSystemResponse>> UpdatePowerSystemAsync(string workId, string id, SavePowerSystemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<PowerSystemResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.PowerSystems
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult<PowerSystemResponse>("力量体系不存在。", 404);

        if (request.Name is not null) entity.Name = request.Name.Trim();
        if (request.LevelDefinitionJson is not null) entity.LevelDefinitionJson = request.LevelDefinitionJson;
        if (request.AbilityRule is not null) entity.AbilityRule = request.AbilityRule;
        if (request.ResourceSystem is not null) entity.ResourceSystem = request.ResourceSystem;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<PowerSystemResponse>(new PowerSystemResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Name = entity.Name,
            LevelDefinitionJson = entity.LevelDefinitionJson, AbilityRule = entity.AbilityRule,
            ResourceSystem = entity.ResourceSystem, CreatedAt = entity.CreateAt
        });
    }

    // 删除力量体系（物理删除）
    public async Task<ApiResult> DeletePowerSystemAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.PowerSystems
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult("力量体系不存在。", 404);

        dbContext.PowerSystems.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult(true);
    }

    // ═══════════ WorldRule ═══════════

    // 查询作品下所有世界规则，按规则类型和名称排序
    public async Task<ApiResult<List<WorldRuleResponse>>> ListWorldRulesAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<WorldRuleResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.WorldRules.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.RuleType).ThenBy(x => x.RuleName)
            .Select(x => new WorldRuleResponse
            {
                Id = x.Id, WorkId = x.WorkId, RuleName = x.RuleName,
                RuleType = x.RuleType, Description = x.Description,
                ConstraintJson = x.ConstraintJson, CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<WorldRuleResponse>>(list);
    }

    // 创建世界规则：需先确保世界观设定已存在
    public async Task<ApiResult<WorldRuleResponse>> CreateWorldRuleAsync(string workId, SaveWorldRuleRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<WorldRuleResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.RuleName))
            return new ApiResult<WorldRuleResponse>("规则名称不能为空。", 400);

        var setting = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<WorldRuleResponse>("请先创建世界观设定。", 400);

        var entity = new WorldRuleEntity
        {
            Id = idGenerator.NextIdString(),
            WorldSettingId = setting.Id,
            WorkId = workId,
            OwnerId = userId,
            RuleName = request.RuleName.Trim(),
            RuleType = request.RuleType ?? string.Empty,
            Description = request.Description ?? string.Empty,
            ConstraintJson = request.ConstraintJson ?? "{}",
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.WorldRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<WorldRuleResponse>(new WorldRuleResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, RuleName = entity.RuleName,
            RuleType = entity.RuleType, Description = entity.Description,
            ConstraintJson = entity.ConstraintJson, CreatedAt = entity.CreateAt
        });
    }

    // 更新世界规则：仅更新请求中非 null 的字段
    public async Task<ApiResult<WorldRuleResponse>> UpdateWorldRuleAsync(string workId, string id, SaveWorldRuleRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<WorldRuleResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.WorldRules
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult<WorldRuleResponse>("世界规则不存在。", 404);

        if (request.RuleName is not null) entity.RuleName = request.RuleName.Trim();
        if (request.RuleType is not null) entity.RuleType = request.RuleType;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.ConstraintJson is not null) entity.ConstraintJson = request.ConstraintJson;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<WorldRuleResponse>(new WorldRuleResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, RuleName = entity.RuleName,
            RuleType = entity.RuleType, Description = entity.Description,
            ConstraintJson = entity.ConstraintJson, CreatedAt = entity.CreateAt
        });
    }

    // 删除世界规则（物理删除）
    public async Task<ApiResult> DeleteWorldRuleAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.WorldRules
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult("世界规则不存在。", 404);

        dbContext.WorldRules.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult(true);
    }

    // ═══════════ HistoricalEvent ═══════════

    // 查询作品下所有历史事件，按事件时间排序
    public async Task<ApiResult<List<HistoricalEventResponse>>> ListHistoricalEventsAsync(string workId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<List<HistoricalEventResponse>>("作品不存在或无权访问。", 404);

        var list = await dbContext.HistoricalEvents.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .OrderBy(x => x.EventTime)
            .Select(x => new HistoricalEventResponse
            {
                Id = x.Id, WorkId = x.WorkId, Title = x.Title,
                Description = x.Description, EraLabel = x.EraLabel,
                EventTime = x.EventTime, ImpactSummary = x.ImpactSummary,
                CreatedAt = x.CreateAt
            })
            .ToListAsync(cancellationToken);

        return new ApiResult<List<HistoricalEventResponse>>(list);
    }

    // 创建历史事件：需先确保世界观设定已存在
    public async Task<ApiResult<HistoricalEventResponse>> CreateHistoricalEventAsync(string workId, SaveHistoricalEventRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<HistoricalEventResponse>("作品不存在或无权访问。", 404);

        if (string.IsNullOrWhiteSpace(request.Title))
            return new ApiResult<HistoricalEventResponse>("事件标题不能为空。", 400);

        var setting = await dbContext.WorldSettings
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<HistoricalEventResponse>("请先创建世界观设定。", 400);

        var entity = new HistoricalEventEntity
        {
            Id = idGenerator.NextIdString(),
            WorldSettingId = setting.Id,
            WorkId = workId,
            OwnerId = userId,
            Title = request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            EraLabel = request.EraLabel ?? string.Empty,
            EventTime = request.EventTime ?? string.Empty,
            ImpactSummary = request.ImpactSummary ?? string.Empty,
            CreateBy = userId,
            UpdateBy = userId
        };

        dbContext.HistoricalEvents.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApiResult<HistoricalEventResponse>(new HistoricalEventResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Title = entity.Title,
            Description = entity.Description, EraLabel = entity.EraLabel,
            EventTime = entity.EventTime, ImpactSummary = entity.ImpactSummary,
            CreatedAt = entity.CreateAt
        });
    }

    // 更新历史事件：仅更新请求中非 null 的字段
    public async Task<ApiResult<HistoricalEventResponse>> UpdateHistoricalEventAsync(string workId, string id, SaveHistoricalEventRequest request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<HistoricalEventResponse>("作品不存在或无权访问。", 404);

        var entity = await dbContext.HistoricalEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult<HistoricalEventResponse>("历史事件不存在。", 404);

        if (request.Title is not null) entity.Title = request.Title.Trim();
        if (request.Description is not null) entity.Description = request.Description;
        if (request.EraLabel is not null) entity.EraLabel = request.EraLabel;
        if (request.EventTime is not null) entity.EventTime = request.EventTime;
        if (request.ImpactSummary is not null) entity.ImpactSummary = request.ImpactSummary;
        entity.UpdateBy = userId;
        entity.UpdateAt = DateTime.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult<HistoricalEventResponse>(new HistoricalEventResponse
        {
            Id = entity.Id, WorkId = entity.WorkId, Title = entity.Title,
            Description = entity.Description, EraLabel = entity.EraLabel,
            EventTime = entity.EventTime, ImpactSummary = entity.ImpactSummary,
            CreatedAt = entity.CreateAt
        });
    }

    // 删除历史事件（物理删除）
    public async Task<ApiResult> DeleteHistoricalEventAsync(string workId, string id, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult("作品不存在或无权访问。", 404);

        var entity = await dbContext.HistoricalEvents
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkId == workId, cancellationToken);
        if (entity is null) return new ApiResult("历史事件不存在。", 404);

        dbContext.HistoricalEvents.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ApiResult(true);
    }

    // 获取世界观设定下各子实体的数量统计（地理、势力、力量体系、规则、历史事件）
    public async Task<ApiResult<Dictionary<string, int>>> GetSubEntityCountsAsync(string workId, string worldSettingId, CancellationToken cancellationToken = default)
    {
        var userId = userContext.UserId;
        if (!await OwnsWorkAsync(workId, userId, cancellationToken))
            return new ApiResult<Dictionary<string, int>>("作品不存在或无权访问。", 404);

        var setting = await dbContext.WorldSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId, cancellationToken);
        if (setting is null)
            return new ApiResult<Dictionary<string, int>>("世界观设定不存在。", 404);

        // 如果传入了 worldSettingId 则使用传入值，否则使用查到的 setting Id
        var actualSettingId = string.IsNullOrEmpty(worldSettingId) ? setting.Id : worldSettingId;

        var counts = new Dictionary<string, int>
        {
            ["geographies"] = await dbContext.Geographies.CountAsync(x => x.WorldSettingId == actualSettingId, cancellationToken),
            ["factions"] = await dbContext.Factions.CountAsync(x => x.WorldSettingId == actualSettingId, cancellationToken),
            ["powerSystems"] = await dbContext.PowerSystems.CountAsync(x => x.WorldSettingId == actualSettingId, cancellationToken),
            ["worldRules"] = await dbContext.WorldRules.CountAsync(x => x.WorldSettingId == actualSettingId, cancellationToken),
            ["historicalEvents"] = await dbContext.HistoricalEvents.CountAsync(x => x.WorldSettingId == actualSettingId, cancellationToken)
        };

        return new ApiResult<Dictionary<string, int>>(counts);
    }
}
