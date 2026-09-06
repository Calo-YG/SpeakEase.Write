using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.World;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 地理查询工具：查询地理设定，按类型过滤并以层级树结构展示（大陆→国家→城市等）
public sealed class GetGeographyTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_geography",
            Description = "查询作品中的地理设定，返回地理层级树结构。可按类型过滤。用于世界观构建和场景描写时参考地理环境。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["geography_type"] = new()
                    {
                        Type = "string",
                        Description = "按地理类型过滤（可选），枚举值: 大陆/国家/城市/山脉/河流/秘境/禁地",
                        Enum = new List<object> { "大陆", "国家", "城市", "山脉", "河流", "秘境", "禁地" }
                    }
                },
                Required = ["work_id"]
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
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var query = db.Geographies.AsNoTracking().Where(g => g.WorkId == args.WorkId);

        if (!string.IsNullOrEmpty(args.GeographyType))
            query = query.Where(g => g.GeographyType == args.GeographyType);

        var geos = await query.OrderBy(g => g.Name).Take(200).ToListAsync(ct);

        if (geos.Count == 0)
            return ToolResult.Ok("暂无地理设定");

        var geoMap = geos.ToDictionary(g => g.Id, g => g);
        var roots = geos.Where(g => string.IsNullOrEmpty(g.ParentGeographyId)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"## 地理设定（{geos.Count}个条目）");
        sb.AppendLine();

        foreach (var root in roots)
            BuildGeoTree(sb, root, geoMap, geos, 0);

        return ToolResult.Ok(sb.ToString());
    }

    private static void BuildGeoTree(StringBuilder sb, GeographyEntity geo,
        Dictionary<string, GeographyEntity> map, List<GeographyEntity> all, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}{geo.Name}（{geo.GeographyType}）: {geo.Description}");

        var children = all.Where(g => g.ParentGeographyId == geo.Id).OrderBy(g => g.Name);
        foreach (var child in children)
            BuildGeoTree(sb, child, map, all, depth + 1);
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string GeographyType { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
