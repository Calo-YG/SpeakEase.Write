using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class GetPowerSystemTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_power_system",
            Description = "查询作品的力量体系/修炼体系设定。可按名称精确查询或列出全部体系。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["name"] = new() { Type = "string", Description = "体系名称（可选），精确匹配，不传则返回全部" }
                },
                Required = ["work_id"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.PowerSystems.AsNoTracking().Where(p => p.WorkId == workId);

        if (!string.IsNullOrEmpty(name))
            query = query.Where(p => p.Name == name);

        var systems = await query.ToListAsync(ct);

        if (systems.Count == 0)
            return ToolResult.Fail(string.IsNullOrEmpty(name)
                ? "当前作品暂无力量体系设定"
                : $"未找到力量体系「{name}」", "not_found");

        var sb = new StringBuilder();
        sb.AppendLine($"## 力量体系（共{systems.Count}个）");

        foreach (var sys in systems)
        {
            sb.AppendLine($"\n### {sys.Name}");
            sb.AppendLine($"等级定义：{sys.LevelDefinitionJson}");
            if (!string.IsNullOrEmpty(sys.AbilityRule))
                sb.AppendLine($"能力规则：{sys.AbilityRule}");
            if (!string.IsNullOrEmpty(sys.ResourceSystem))
                sb.AppendLine($"资源体系：{sys.ResourceSystem}");
        }

        return ToolResult.Ok(sb.ToString());
    }
}
