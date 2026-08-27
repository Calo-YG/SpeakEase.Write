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

// 大纲节点创建/更新工具：支持 book/volume/act/climax/resolution 五层节点，通过 parent_node_id 建立树状大纲结构
public sealed class CreateOutlineNodeTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_outline_node",
            Description = """
创建或更新大纲节点。大纲系统支持三层结构，使用 stage_type 标记层级，通过 parent_node_id 建立树状关系。

## 三层结构
- **book**：全书总纲级节点（5-8个），代表全书的大情节弧（开篇、主要冲突引入、第一幕高潮、中段转折、第二幕高潮、最终高潮、结局）
- **volume**：卷大纲级节点（每卷3-5个），代表卷内的情节点（卷开篇承接、卷内冲突发展、卷高潮、卷结尾/过渡）
- **act/climax/resolution**：章节级节点，标注具体章节在叙事结构中的位置（act=发展章、climax=高潮章、resolution=结局章）

## parent_node_id 树状关系
- 卷级节点 → parent_node_id 指向全书总纲节点
- 章节级节点 → parent_node_id 指向卷级节点
- 根节点（全书总纲）不需要 parent_node_id

## 层级验证
- book 节点不能有 parent_node_id
- volume 节点必须有 parent_node_id（指向 book 节点）
- act/climax/resolution 节点必须有 parent_node_id（指向 volume 或 book 节点）

## goal 字段
描述该节点的剧情目标或摘要。book 节点应标注对应的卷范围和预期字数占比；volume 节点应标注卷内章节分配。

## key_event 字段
该节点的关键事件/转折点简述。高潮和转折节点的 key_event 必须详实。

## 更新模式
通过 id 或 title 查找已有节点，存在则更新提供的字段（非空字符串覆盖），不存在则创建。
生成前必须先通过 get_outline 确认不重复，再逐个创建。严禁跳级（必须先 book → 再 volume → 最后章节级）。
""",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "节点ID（可选），用于更新已有节点" },
                    ["title"] = new() { Type = "string", Description = "节点标题（必填）" },
                    ["stage_type"] = new()
                    {
                        Type = "string",
                        Description = "层级/阶段类型（新建必填，更新可选）。枚举值: book(全书总纲) / volume(卷大纲) / act(发展章) / climax(高潮章) / resolution(结局章)",
                        Enum = new List<object> { "book", "volume", "act", "climax", "resolution" }
                    },
                    ["goal"] = new() { Type = "string", Description = "节点剧情目标/摘要（可选，但强烈建议填写）" },
                    ["key_event"] = new() { Type = "string", Description = "关键事件/转折点简述（可选，高潮章必填）" },
                    ["sequence"] = new() { Type = "integer", Description = "排序序号（可选，默认为当前最大序号+1）" },
                    ["parent_node_id"] = new() { Type = "string", Description = "父节点ID（可选）。book 节点不需要，volume 节点指向 book 节点，章节级节点指向 volume 节点" },
                    ["character_ids"] = new() { Type = "array", Items = new ParameterSchema { Type = "string" }, Description = "关联角色ID列表（可选），记录该节点涉及的主要角色" }
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

        OutlineNodeEntity entity = null;
        if (!string.IsNullOrEmpty(args.Id))
        {
            entity = await db.OutlineNodes.FirstOrDefaultAsync(x => x.Id == args.Id && x.WorkId == args.WorkId, ct);
        }

        if (entity == null)
        {
            entity = await db.OutlineNodes.FirstOrDefaultAsync(x => x.WorkId == args.WorkId && x.Title == args.Title, ct);
        }

        if (entity != null)
        {
            if (!string.IsNullOrEmpty(args.StageType) && !IsValidStageType(args.StageType))
            {
                return ToolResult.Fail($"无效的 stage_type: {args.StageType}，有效值: book/volume/act/climax/resolution");
            }

            if (!string.IsNullOrEmpty(args.StageType) && IsValidStageType(args.StageType))
            {
                entity.StageType = args.StageType;
            }

            if (args.Goal != null) entity.Goal = args.Goal;
            if (args.KeyEvent != null) entity.KeyEvent = args.KeyEvent;
            if (args.Sequence > 0) entity.Sequence = args.Sequence;
            if (args.ParentNodeId != null)
            {
                if (!string.IsNullOrEmpty(args.ParentNodeId))
                {
                    var parent = await db.OutlineNodes.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == args.ParentNodeId && x.WorkId == args.WorkId, ct);
                    if (parent == null)
                    {
                        return ToolResult.Fail($"parent_node_id {args.ParentNodeId} 不存在");
                    }
                }
                entity.ParentNodeId = args.ParentNodeId;
            }
            if (args.CharacterIds.Count > 0) entity.CharacterIds = args.CharacterIds;
            entity.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"大纲节点「{entity.Title}」已更新，层级: {entity.StageType}，ID: {entity.Id}");
        }

        if (string.IsNullOrEmpty(args.StageType))
            return ToolResult.Fail("创建节点必须提供 stage_type");
        if (!IsValidStageType(args.StageType))
            return ToolResult.Fail($"无效的 stage_type: {args.StageType}，有效值: book/volume/act/climax/resolution");

        if (!string.IsNullOrEmpty(args.ParentNodeId))
        {
            var parent = await db.OutlineNodes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == args.ParentNodeId && x.WorkId == args.WorkId, ct);
            if (parent == null)
                return ToolResult.Fail($"父节点 {args.ParentNodeId} 不存在");
        }

        var maxSeq = await db.OutlineNodes.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId)
            .MaxAsync(x => (int?)x.Sequence, ct) ?? 0;

        var newEntity = new OutlineNodeEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Title = args.Title,
            Goal = args.Goal ?? string.Empty,
            KeyEvent = args.KeyEvent ?? string.Empty,
            StageType = args.StageType,
            Sequence = args.Sequence > 0 ? args.Sequence : maxSeq + 1,
            ParentNodeId = args.ParentNodeId ?? string.Empty,
            CharacterIds = args.CharacterIds
        };

        await db.OutlineNodes.AddAsync(newEntity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"大纲节点「{args.Title}」已创建，层级: {args.StageType}，ID: {newEntity.Id}，序号: {newEntity.Sequence}");
    }

    private static bool IsValidStageType(string type)
    {
        return type is "book" or "volume" or "act" or "climax" or "resolution";
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Title { get; init; }
        public string Goal { get; init; }
        public string KeyEvent { get; init; }
        public string StageType { get; init; }
        public int Sequence { get; init; }
        public string ParentNodeId { get; init; }
        public List<string> CharacterIds { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Title))
                return ToolResult.Fail("缺少必需参数 'title'", "argument_parse_error");
            return null;
        }
    }
}
