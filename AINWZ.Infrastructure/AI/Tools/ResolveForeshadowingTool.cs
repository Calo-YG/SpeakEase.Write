using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class ResolveForeshadowingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "resolve_foreshadowing",
            Description = "回扣（解决）一条伏笔。当章节中正式揭开悬念时调用，需说明回收方式并指向回收章节。严禁在伏笔未被正文揭示前调用，以免提前泄露情节。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["foreshadowing_id"] = new() { Type = "string", Description = "伏笔标识（必填）" },
                    ["payoff_chapter_id"] = new() { Type = "string", Description = "实际回收章节标识（必填）" },
                    ["resolution"] = new() { Type = "string", Description = "回收方式说明（必填），描述伏笔如何被揭示" }
                },
                Required = ["work_id", "foreshadowing_id", "payoff_chapter_id", "resolution"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var foreshadowingId = args.GetString("foreshadowing_id", required: true);
        var payoffChapterId = args.GetString("payoff_chapter_id", required: true);
        var resolution = args.GetString("resolution", required: true);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var entity = await db.Foreshadowings.FirstOrDefaultAsync(
            x => x.Id == foreshadowingId && x.WorkId == workId, ct);
        if (entity == null)
            return ToolResult.Fail($"伏笔 {foreshadowingId} 不存在，无法回扣", "not_found");

        entity.Status = "paid_off";
        entity.PayoffChapterId = payoffChapterId;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"伏笔「{entity.Title}」已回收，回收章节: {payoffChapterId}，方式: {resolution}");
    }
}
