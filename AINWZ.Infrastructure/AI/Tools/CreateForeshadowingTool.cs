using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateForeshadowingTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_foreshadowing",
            Description = "记录一条新的伏笔/悬念。写完章节后，如果埋下了重要情节线索应主动调用此工具，为后续章节自动预留回扣提示。importance 范围 1-5，伏笔状态默认 active，解析前不得泄露答案。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "当前作品标识（必填）" },
                    ["title"] = new() { Type = "string", Description = "伏笔标题（必填）" },
                    ["description"] = new() { Type = "string", Description = "伏笔内容描述（必填）" },
                    ["setup_chapter_id"] = new() { Type = "string", Description = "埋下该伏笔的章节标识（必填）" },
                    ["importance"] = new() { Type = "integer", Description = "重要性等级（必填，范围 1-5，5 为最高）" },
                    ["expected_payoff_chapter_id"] = new() { Type = "string", Description = "预期回收章节标识（可选）" }
                },
                Required = ["work_id", "title", "description", "setup_chapter_id", "importance"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var title = args.GetString("title", required: true);
        var description = args.GetString("description", required: true);
        var setupChapterId = args.GetString("setup_chapter_id", required: true);
        var importance = args.GetInt32("importance", required: true, min: 1, max: 5);
        var expectedPayoffChapterId = args.GetString("expected_payoff_chapter_id");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var entity = new ForeshadowingEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Title = title,
            Description = description,
            SetupChapterId = setupChapterId,
            Importance = importance,
            Status = "active",
            PayoffChapterId = expectedPayoffChapterId ?? string.Empty,
        };

        await db.Foreshadowings.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"伏笔「{title}」已记录，ID: {entity.Id}，预计回收章节: {expectedPayoffChapterId ?? "待定"}");
    }
}
