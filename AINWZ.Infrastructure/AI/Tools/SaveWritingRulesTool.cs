using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class SaveWritingRulesTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "save_writing_rules",
            Description = "保存或更新作品的写作规则与约束要求。传入完整的写作规则文本，将覆盖已有规则",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["rules"] = new() { Type = "string", Description = "写作规则与约束要求文本（必填）。包含所有用户提出的写作规范、约束条件、特殊要求等" }
                },
                Required = ["work_id", "rules"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var rules = args.GetString("rules", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var work = await db.Works
            .FirstOrDefaultAsync(x => x.Id == workId, ct);

        if (work == null)
            return ToolResult.Fail($"未找到作品 {workId}", "not_found");

        work.WritingRules = rules;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok("写作规则与约束要求已保存。");
    }
}
