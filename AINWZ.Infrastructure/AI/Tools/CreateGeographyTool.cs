using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 地理创建/更新工具：创建大陆/国家/城市等地理条目，支持层级关系（parent_name 指定上级区域）
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var worldSetting = await db.WorldSettings.FirstOrDefaultAsync(w => w.WorkId == args.WorkId, ct);

        GeographyEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.Geographies.FirstOrDefaultAsync(g => g.Id == args.Id && g.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.Geographies.FirstOrDefaultAsync(g => g.WorkId == args.WorkId && g.Name == args.Name, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.GeographyType)) entity.GeographyType = args.GeographyType;
            if (!string.IsNullOrEmpty(args.Description)) entity.Description = args.Description;
            if (!string.IsNullOrEmpty(args.ParentName))
            {
                var parent = await db.Geographies.FirstOrDefaultAsync(
                    g => g.WorkId == args.WorkId && g.Name == args.ParentName, ct)
                    ?? await db.Geographies.FirstOrDefaultAsync(
                        g => g.WorkId == args.WorkId && g.Name != null && g.Name.Contains(args.ParentName), ct);
                if (parent != null) entity.ParentGeographyId = parent.Id;
            }
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"地理「{args.Name}」（{entity.GeographyType}）已更新，ID: {entity.Id}");
        }

        var parentId = string.Empty;
        if (!string.IsNullOrEmpty(args.ParentName))
        {
            var parent = await db.Geographies.FirstOrDefaultAsync(
                g => g.WorkId == args.WorkId && g.Name == args.ParentName, ct)
                ?? await db.Geographies.FirstOrDefaultAsync(
                    g => g.WorkId == args.WorkId && g.Name != null && g.Name.Contains(args.ParentName), ct);
            if (parent != null) parentId = parent.Id;
        }

        var newEntity = new GeographyEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            WorldSettingId = worldSetting?.Id ?? string.Empty,
            Name = args.Name,
            GeographyType = args.GeographyType ?? string.Empty,
            Description = args.Description ?? string.Empty,
            ParentGeographyId = parentId
        };

        await db.Geographies.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        var parentInfo = !string.IsNullOrEmpty(parentId) ? $"，所属: {args.ParentName}" : "";
        return ToolResult.Ok($"地理「{args.Name}」（{newEntity.GeographyType}）已创建{parentInfo}，ID: {newEntity.Id}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string GeographyType { get; init; }
        public string Description { get; init; }
        public string ParentName { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Name))
                return ToolResult.Fail("缺少必需参数 'name'", "argument_parse_error");
            return null;
        }
    }
}
