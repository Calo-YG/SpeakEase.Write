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
            Description = "创建或更新伏笔/悬念。写完章节后埋下的重要情节线索应主动调用此工具。通过 id 或 title 查找已有伏笔，存在则更新，不存在则创建。importance 范围 1-5，伏笔状态默认 active。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "当前作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "伏笔ID（可选），用于更新已有伏笔" },
                    ["title"] = new() { Type = "string", Description = "伏笔标题（必填）" },
                    ["description"] = new() { Type = "string", Description = "伏笔内容描述（新建必填，更新可选）" },
                    ["setup_chapter_id"] = new() { Type = "string", Description = "埋下该伏笔的章节标识（新建必填，更新可选）" },
                    ["importance"] = new() { Type = "integer", Description = "重要性等级（新建必填，更新可选，范围 1-5）" },
                    ["expected_payoff_chapter_id"] = new() { Type = "string", Description = "预期回收章节标识（可选）" }
                },
                Required = ["work_id", "title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var title = args.GetString("title", required: true);
        var description = args.GetString("description");
        var setupChapterId = args.GetString("setup_chapter_id");
        var importance = args.GetInt32("importance", defaultValue: 0, min: 1, max: 5);
        var expectedPayoffChapterId = args.GetString("expected_payoff_chapter_id");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        ForeshadowingEntity entity = null;
        if (!string.IsNullOrEmpty(id))
            entity = await db.Foreshadowings.FirstOrDefaultAsync(f => f.Id == id && f.WorkId == workId, ct);
        if (entity == null)
            entity = await db.Foreshadowings.FirstOrDefaultAsync(f => f.WorkId == workId && f.Title == title, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(description)) entity.Description = description;
            if (!string.IsNullOrEmpty(setupChapterId)) entity.SetupChapterId = setupChapterId;
            if (importance > 0) entity.Importance = importance;
            if (args.Has("expected_payoff_chapter_id")) entity.PayoffChapterId = expectedPayoffChapterId ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"伏笔「{title}」已更新，ID: {entity.Id}");
        }

        var newEntity = new ForeshadowingEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Title = title,
            Description = description ?? string.Empty,
            SetupChapterId = setupChapterId ?? string.Empty,
            Importance = importance > 0 ? importance : 1,
            Status = "active",
            PayoffChapterId = expectedPayoffChapterId ?? string.Empty,
        };

        await db.Foreshadowings.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"伏笔「{title}」已记录，ID: {newEntity.Id}，预计回收章节: {expectedPayoffChapterId ?? "待定"}");
    }
}
