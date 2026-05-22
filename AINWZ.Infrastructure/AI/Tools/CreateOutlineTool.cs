using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateOutlineTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_outline",
            Description = """
创建/更新作品的主大纲（聚合根）。大纲是全书剧情规划的总入口，所有大纲节点（create_outline_node）都隶属于大纲。

## structure_template 枚举
- **three_act**: 三幕式结构（开篇→冲突升级→高潮结局），适合大多数小说
- **four_act**: 四幕式结构，适合更复杂的多线叙事
- **hero_journey**: 英雄之旅（平凡世界→冒险召唤→考验→归来），适合成长型主角
- **freeform**: 自由结构，无固定模板约束

## 使用时机
- **从零规划大纲的第一步**：在创建大纲节点之前，必须先调用本工具建立大纲根
- 如果大纲已存在，调用本工具将更新标题和结构模板

## 后续步骤
创建大纲后，依次使用 create_outline_node（stage_type=book → volume → act/climax/resolution）和 create_chapter_outline 逐级构建大纲树。
""",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["title"] = new() { Type = "string", Description = "大纲标题（必填），如「全书总纲」「仙途争锋·完整剧情大纲」" },
                    ["structure_template"] = new()
                    {
                        Type = "string",
                        Description = "结构模板（必填）。枚举: three_act(三幕式) / four_act(四幕式) / hero_journey(英雄之旅) / freeform(自由结构)",
                        Enum = new List<object> { "three_act", "four_act", "hero_journey", "freeform" }
                    },
                    ["summary"] = new() { Type = "string", Description = "大纲整体摘要（可选），概述全书主线方向、核心矛盾和大致卷分布" }
                },
                Required = ["work_id", "title", "structure_template"]
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

        if (!IsValidTemplate(args.StructureTemplate))
            return ToolResult.Fail($"无效的结构模板: {args.StructureTemplate}，有效值: three_act/four_act/hero_journey/freeform");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var existingPrimary = await db.Outlines
            .FirstOrDefaultAsync(o => o.WorkId == args.WorkId && o.IsPrimary, ct);

        if (existingPrimary != null)
        {
            existingPrimary.Title = args.Title;
            existingPrimary.StructureTemplate = args.StructureTemplate;
            if (!string.IsNullOrEmpty(args.Summary))
                existingPrimary.Summary = args.Summary;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"主大纲已更新：「{args.Title}」，结构模板: {GetTemplateLabel(args.StructureTemplate)}");
        }

        var outline = new OutlineEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Title = args.Title,
            StructureTemplate = args.StructureTemplate,
            Summary = args.Summary ?? string.Empty,
            IsPrimary = true
        };

        await db.Outlines.AddAsync(outline, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"主大纲「{args.Title}」已创建，ID: {outline.Id}，结构模板: {GetTemplateLabel(args.StructureTemplate)}。接下来可使用 create_outline_node 创建全书总纲节点（stage_type=book）");
    }

    private static bool IsValidTemplate(string template)
    {
        return template is "three_act" or "four_act" or "hero_journey" or "freeform";
    }

    private static string GetTemplateLabel(string template)
    {
        return template switch
        {
            "three_act" => "三幕式",
            "four_act" => "四幕式",
            "hero_journey" => "英雄之旅",
            "freeform" => "自由结构",
            _ => template
        };
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Title { get; init; }
        public string StructureTemplate { get; init; }
        public string Summary { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Title))
                return ToolResult.Fail("缺少必需参数 'title'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(StructureTemplate))
                return ToolResult.Fail("缺少必需参数 'structure_template'", "argument_parse_error");
            return null;
        }
    }
}
