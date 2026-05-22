using System.Text.Json;
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        var entity = await db.Foreshadowings.FirstOrDefaultAsync(
            x => x.Id == args.ForeshadowingId && x.WorkId == args.WorkId, ct);
        if (entity == null)
            return ToolResult.Fail($"伏笔 {args.ForeshadowingId} 不存在，无法回扣", "not_found");

        entity.Status = "paid_off";
        entity.PayoffChapterId = args.PayoffChapterId;
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"伏笔「{entity.Title}」已回收，回收章节: {args.PayoffChapterId}，方式: {args.Resolution}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string ForeshadowingId { get; init; }
        public string PayoffChapterId { get; init; }
        public string Resolution { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(ForeshadowingId))
                return ToolResult.Fail("缺少必需参数 'foreshadowing_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(PayoffChapterId))
                return ToolResult.Fail("缺少必需参数 'payoff_chapter_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Resolution))
                return ToolResult.Fail("缺少必需参数 'resolution'", "argument_parse_error");
            return null;
        }
    }
}
