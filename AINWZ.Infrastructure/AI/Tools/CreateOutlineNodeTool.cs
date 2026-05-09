using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateOutlineNodeTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_outline_node",
            Description = "创建大纲节点。stage_type 枚举: act/climax/resolution.",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["title"] = new() { Type = "string", Description = "节点标题（必填）" },
                    ["goal"] = new() { Type = "string", Description = "目标（可选）" },
                    ["key_event"] = new() { Type = "string", Description = "关键事件（可选）" },
                    ["stage_type"] = new()
                    {
                        Type = "string",
                        Description = "阶段类型（可选），枚举值: act/climax/resolution",
                        Enum = new List<object> { "act", "climax", "resolution" }
                    },
                    ["sequence"] = new() { Type = "integer", Description = "排序序号（可选，默认为当前最大序号+1）" }
                },
                Required = ["work_id", "title"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var title = args.GetString("title", required: true);
        var goal = args.GetString("goal");
        var keyEvent = args.GetString("key_event");
        var stageType = args.GetString("stage_type");
        var sequence = args.GetInt32("sequence", min: 0);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var maxSeq = await db.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == workId)
            .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;

        var entity = new OutlineNodeEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Title = title,
            Goal = goal ?? string.Empty,
            KeyEvent = keyEvent ?? string.Empty,
            StageType = stageType ?? string.Empty,
            Sequence = sequence > 0 ? sequence : maxSeq + 1
        };

        await db.OutlineNodes.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"大纲节点「{title}」已创建，ID: {entity.Id}，序号: {entity.Sequence}");
    }
}
