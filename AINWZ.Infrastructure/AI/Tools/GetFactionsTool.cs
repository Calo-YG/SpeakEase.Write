using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetFactionsTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_factions",
            Description = "查询作品中的所有势力（门派/家族/国家/组织），可按类型或关键词过滤。用于世界观构建、情节设计中涉及势力纷争时参考。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["keyword"] = new() { Type = "string", Description = "关键词过滤（可选），在名称和描述中搜索" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var keyword = args.GetString("keyword");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.Factions.AsNoTracking().Where(f => f.WorkId == workId);

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(f => f.Name.Contains(keyword) || f.Description.Contains(keyword));

        var factions = await query
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        if (factions.Count == 0)
            return ToolResult.Ok("暂无势力记录");

        var sb = new StringBuilder();
        sb.AppendLine($"## 势力列表（{factions.Count}个）");
        sb.AppendLine();

        foreach (var f in factions)
        {
            sb.AppendLine($"### {f.Name}（{f.FactionType}）");
            sb.AppendLine($"  {f.Description}");
            if (!string.IsNullOrEmpty(f.RelationshipJson))
                sb.AppendLine($"  势力关系: {f.RelationshipJson}");
            sb.AppendLine();
        }

        return ToolResult.Ok(sb.ToString());
    }
}
