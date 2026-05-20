using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateGeographyTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_geography",
            Description = "创建或更新地理条目（大陆/国家/城市/特殊区域）。通过 id 或 name 查找已有条目，存在则更新，不存在则创建。可通过 parent_name 指定上级地理区域形成层级关系。geography_type 建议: 大陆/国家/城市/山脉/河流/秘境/禁地。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "地理ID（可选），用于更新已有条目" },
                    ["name"] = new() { Type = "string", Description = "地理名称（必填）" },
                    ["geography_type"] = new() { Type = "string", Description = "地理类型（新建必填，更新可选），如: 大陆/国家/城市/山脉/河流/秘境/禁地" },
                    ["description"] = new() { Type = "string", Description = "地理描述（新建必填，更新可选），包含环境、特色、重要性等" },
                    ["parent_name"] = new() { Type = "string", Description = "上级地理名称（可选），用于建立层级关系，如城市属于某个国家" }
                },
                Required = ["work_id", "name"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var name = args.GetString("name", required: true);
        var geoType = args.GetString("geography_type");
        var description = args.GetString("description");
        var parentName = args.GetString("parent_name");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);

        GeographyEntity entity = null;
        if (!string.IsNullOrEmpty(id))
            entity = await db.Geographies.FirstOrDefaultAsync(g => g.Id == id && g.WorkId == workId, ct);
        if (entity == null)
            entity = await db.Geographies.FirstOrDefaultAsync(g => g.WorkId == workId && g.Name == name, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(geoType)) entity.GeographyType = geoType;
            if (!string.IsNullOrEmpty(description)) entity.Description = description;
            if (!string.IsNullOrEmpty(parentName))
            {
                var parent = await db.Geographies.FirstOrDefaultAsync(
                    g => g.WorkId == workId && g.Name == parentName, ct)
                    ?? await db.Geographies.FirstOrDefaultAsync(
                        g => g.WorkId == workId && g.Name != null && g.Name.Contains(parentName), ct);
                if (parent != null) entity.ParentGeographyId = parent.Id;
            }
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"地理「{name}」（{entity.GeographyType}）已更新，ID: {entity.Id}");
        }

        var parentId = string.Empty;
        if (!string.IsNullOrEmpty(parentName))
        {
            var parent = await db.Geographies.FirstOrDefaultAsync(
                g => g.WorkId == workId && g.Name == parentName, ct)
                ?? await db.Geographies.FirstOrDefaultAsync(
                    g => g.WorkId == workId && g.Name != null && g.Name.Contains(parentName), ct);
            if (parent != null) parentId = parent.Id;
        }

        var newEntity = new GeographyEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = name,
            GeographyType = geoType ?? string.Empty,
            Description = description ?? string.Empty,
            ParentGeographyId = parentId
        };

        await db.Geographies.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        var parentInfo = !string.IsNullOrEmpty(parentId) ? $"，所属: {parentName}" : "";
        return ToolResult.Ok($"地理「{name}」（{newEntity.GeographyType}）已创建{parentInfo}，ID: {newEntity.Id}");
    }
}
