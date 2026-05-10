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
            Description = "创建地理条目（大陆/国家/城市/特殊区域），用于世界观的空间构建。可通过 parent_name 指定上级地理区域形成层级关系。geography_type 建议: 大陆/国家/城市/山脉/河流/秘境/禁地。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["name"] = new() { Type = "string", Description = "地理名称（必填）" },
                    ["geography_type"] = new() { Type = "string", Description = "地理类型（必填），如: 大陆/国家/城市/山脉/河流/秘境/禁地" },
                    ["description"] = new() { Type = "string", Description = "地理描述（必填），包含环境、特色、重要性等" },
                    ["parent_name"] = new() { Type = "string", Description = "上级地理名称（可选），用于建立层级关系，如城市属于某个国家" }
                },
                Required = ["work_id", "name", "geography_type", "description"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        var geoType = args.GetString("geography_type", required: true);
        var description = args.GetString("description", required: true);
        var parentName = args.GetString("parent_name");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == workId, ct);
        var parentId = string.Empty;

        if (!string.IsNullOrEmpty(parentName))
        {
            var parent = await db.Geographies.FirstOrDefaultAsync(
                g => g.WorkId == workId && g.Name == parentName, ct)
                ?? await db.Geographies.FirstOrDefaultAsync(
                    g => g.WorkId == workId && g.Name != null && g.Name.Contains(parentName), ct);

            if (parent != null)
                parentId = parent.Id;
        }

        var entity = new GeographyEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = name,
            GeographyType = geoType,
            Description = description,
            ParentGeographyId = parentId
        };

        await db.Geographies.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        var parentInfo = !string.IsNullOrEmpty(parentId) ? $"，所属: {parentName}" : "";
        return ToolResult.Ok($"地理「{name}」（{geoType}）已创建{parentInfo}，ID: {entity.Id}");
    }
}
