using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterArcTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character_arc",
            Description = "为角色创建或更新成长弧线阶段。记录角色在故事中的阶段性变化：初始状态 → 触发事件 → 变化后的状态。每次重大性格转变或成长都应记录，用于确保角色发展连贯。通过 id 或 stage_title+character_name 查找已有阶段，存在则更新提供的字段，不存在则创建。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["id"] = new() { Type = "string", Description = "阶段ID（可选），用于更新已有阶段" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（必填），需与已有角色匹配" },
                    ["stage_title"] = new() { Type = "string", Description = "阶段标题（新建必填，更新可选），如: 初出茅庐/遭遇背叛/顿悟成长" },
                    ["initial_state"] = new() { Type = "string", Description = "初始状态（新建必填，更新可选），角色在该阶段开始时的性格/能力/处境" },
                    ["trigger_event"] = new() { Type = "string", Description = "触发变化的事件（新建必填，更新可选）" },
                    ["changed_state"] = new() { Type = "string", Description = "变化后的状态（新建必填，更新可选），角色经历事件后的改变" },
                    ["stage_order"] = new() { Type = "integer", Description = "阶段顺序号（可选），默认追加到末尾" }
                },
                Required = ["work_id", "character_name"]
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
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        CharacterArcEntity arc = null;
        if (!string.IsNullOrEmpty(args.Id))
            arc = await db.CharacterArcs.FirstOrDefaultAsync(a => a.Id == args.Id && a.WorkId == args.WorkId, ct);

        CharacterEntity character = null;
        if (arc == null)
        {
            character = await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == args.WorkId && c.Name == args.CharacterName, ct)
                ?? await db.Characters.FirstOrDefaultAsync(
                    c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.CharacterName), ct);

            if (character != null && !string.IsNullOrEmpty(args.StageTitle))
                arc = await db.CharacterArcs.FirstOrDefaultAsync(
                    a => a.WorkId == args.WorkId && a.CharacterId == character.Id && a.StageTitle == args.StageTitle, ct);
        }

        if (arc != null)
        {
            if (!string.IsNullOrEmpty(args.StageTitle)) arc.StageTitle = args.StageTitle;
            if (args.InitialState != null) arc.InitialState = args.InitialState;
            if (args.TriggerEvent != null) arc.TriggerEvent = args.TriggerEvent;
            if (args.ChangedState != null) arc.ChangedState = args.ChangedState;
            if (args.StageOrder > 0) arc.StageOrder = args.StageOrder;
            arc.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色「{args.CharacterName}」成长弧线阶段「{arc.StageTitle}」已更新，序号: {arc.StageOrder}");
        }

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.CharacterName}」", "not_found");

        if (string.IsNullOrEmpty(args.StageTitle))
            return ToolResult.Fail("创建阶段必须提供 stage_title");
        if (string.IsNullOrEmpty(args.InitialState))
            return ToolResult.Fail("创建阶段必须提供 initial_state");
        if (string.IsNullOrEmpty(args.TriggerEvent))
            return ToolResult.Fail("创建阶段必须提供 trigger_event");
        if (string.IsNullOrEmpty(args.ChangedState))
            return ToolResult.Fail("创建阶段必须提供 changed_state");

        var maxOrder = await db.CharacterArcs.AsNoTracking()
            .Where(a => a.WorkId == args.WorkId && a.CharacterId == character.Id)
            .MaxAsync(a => (int?)a.StageOrder, ct) ?? 0;

        var entity = new CharacterArcEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            CharacterId = character.Id,
            StageOrder = args.StageOrder > 0 ? args.StageOrder : maxOrder + 1,
            StageTitle = args.StageTitle,
            InitialState = args.InitialState,
            TriggerEvent = args.TriggerEvent,
            ChangedState = args.ChangedState
        };

        await db.CharacterArcs.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{character.Name}」成长弧线阶段「{args.StageTitle}」已创建，序号: {entity.StageOrder}");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string CharacterName { get; init; }
        public string StageTitle { get; init; }
        public string InitialState { get; init; }
        public string TriggerEvent { get; init; }
        public string ChangedState { get; init; }
        public int StageOrder { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(CharacterName))
                return ToolResult.Fail("缺少必需参数 'character_name'", "argument_parse_error");
            return null;
        }
    }
}
