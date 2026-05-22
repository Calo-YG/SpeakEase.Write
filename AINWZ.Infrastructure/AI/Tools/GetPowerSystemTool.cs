using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 力量体系查询工具：查询作品的力量体系/修炼体系，按名称精确查询或列出全部
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var query = db.PowerSystems.AsNoTracking().Where(p => p.WorkId == args.WorkId);

        if (!string.IsNullOrEmpty(args.Name))
            query = query.Where(p => p.Name == args.Name);

        var systems = await query.ToListAsync(ct);

        if (systems.Count == 0)
            return ToolResult.Fail(string.IsNullOrEmpty(args.Name)
                ? "当前作品暂无力量体系设定"
                : $"未找到力量体系「{args.Name}」", "not_found");

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

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Name { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
