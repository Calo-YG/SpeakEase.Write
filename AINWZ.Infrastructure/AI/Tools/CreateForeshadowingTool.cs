using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 伏笔创建/更新工具：记录章节中埋下的悬念/伏笔，支持设置重要性等级和预期回收章节
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
        Args args;
        try { args = JsonSerializer.Deserialize<Args>(arguments, ToolArgsHelper.Options); }
        catch (JsonException ex) { return ToolResult.Fail($"JSON 参数解析错误: {ex.Message}", "argument_parse_error"); }
        var validationError = args.Validate();
        if (validationError != null) return validationError;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        ForeshadowingEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
            entity = await db.Foreshadowings.FirstOrDefaultAsync(f => f.Id == args.Id && f.WorkId == args.WorkId, ct);
        if (entity == null)
            entity = await db.Foreshadowings.FirstOrDefaultAsync(f => f.WorkId == args.WorkId && f.Title == args.Title, ct);

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.Description)) entity.Description = args.Description;
            if (!string.IsNullOrEmpty(args.SetupChapterId)) entity.SetupChapterId = args.SetupChapterId;
            if (args.Importance > 0) entity.Importance = args.Importance;
            if (args.ExpectedPayoffChapterId != null) entity.PayoffChapterId = args.ExpectedPayoffChapterId ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"伏笔「{args.Title}」已更新，ID: {entity.Id}");
        }

        var newEntity = new ForeshadowingEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Title = args.Title,
            Description = args.Description ?? string.Empty,
            SetupChapterId = args.SetupChapterId ?? string.Empty,
            Importance = args.Importance > 0 ? args.Importance : 1,
            Status = "active",
            PayoffChapterId = args.ExpectedPayoffChapterId ?? string.Empty,
        };

        await db.Foreshadowings.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"伏笔「{args.Title}」已记录，ID: {newEntity.Id}，预计回收章节: {args.ExpectedPayoffChapterId ?? "待定"}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Title { get; init; }
        public string Description { get; init; }
        public string SetupChapterId { get; init; }
        public int Importance { get; init; }
        public string ExpectedPayoffChapterId { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Title))
                return ToolResult.Fail("缺少必需参数 'title'", "argument_parse_error");
            if (Importance != 0 && (Importance < 1 || Importance > 5))
                return ToolResult.Fail($"参数 'importance' 值 {Importance} 超出范围 [1, 5]", "argument_parse_error");
            return null;
        }
    }
}
