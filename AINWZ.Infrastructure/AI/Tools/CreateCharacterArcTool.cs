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
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var id = args.GetString("id");
        var characterName = args.GetString("character_name", required: true);
        var stageTitle = args.GetString("stage_title");
        var initialState = args.GetString("initial_state");
        var triggerEvent = args.GetString("trigger_event");
        var changedState = args.GetString("changed_state");
        var stageOrder = args.GetInt32("stage_order", min: 0);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        CharacterArcEntity arc = null;
        if (!string.IsNullOrEmpty(id))
            arc = await db.CharacterArcs.FirstOrDefaultAsync(a => a.Id == id && a.WorkId == workId, ct);

        CharacterEntity character = null;
        if (arc == null)
        {
            character = await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == workId && c.Name == characterName, ct)
                ?? await db.Characters.FirstOrDefaultAsync(
                    c => c.WorkId == workId && c.Name != null && c.Name.Contains(characterName), ct);

            if (character != null && !string.IsNullOrEmpty(stageTitle))
                arc = await db.CharacterArcs.FirstOrDefaultAsync(
                    a => a.WorkId == workId && a.CharacterId == character.Id && a.StageTitle == stageTitle, ct);
        }

        if (arc != null)
        {
            if (!string.IsNullOrEmpty(stageTitle)) arc.StageTitle = stageTitle;
            if (initialState != null) arc.InitialState = initialState;
            if (triggerEvent != null) arc.TriggerEvent = triggerEvent;
            if (changedState != null) arc.ChangedState = changedState;
            if (stageOrder > 0) arc.StageOrder = stageOrder;
            arc.UpdateAt = DateTime.Now;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色「{characterName}」成长弧线阶段「{arc.StageTitle}」已更新，序号: {arc.StageOrder}");
        }

        if (character == null)
            return ToolResult.Fail($"未找到角色「{characterName}」", "not_found");

        if (string.IsNullOrEmpty(stageTitle))
            return ToolResult.Fail("创建阶段必须提供 stage_title");
        if (string.IsNullOrEmpty(initialState))
            return ToolResult.Fail("创建阶段必须提供 initial_state");
        if (string.IsNullOrEmpty(triggerEvent))
            return ToolResult.Fail("创建阶段必须提供 trigger_event");
        if (string.IsNullOrEmpty(changedState))
            return ToolResult.Fail("创建阶段必须提供 changed_state");

        var maxOrder = await db.CharacterArcs.AsNoTracking()
            .Where(a => a.WorkId == workId && a.CharacterId == character.Id)
            .MaxAsync(a => (int?)a.StageOrder, ct) ?? 0;

        var entity = new CharacterArcEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            CharacterId = character.Id,
            StageOrder = stageOrder > 0 ? stageOrder : maxOrder + 1,
            StageTitle = stageTitle,
            InitialState = initialState,
            TriggerEvent = triggerEvent,
            ChangedState = changedState
        };

        await db.CharacterArcs.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{character.Name}」成长弧线阶段「{stageTitle}」已创建，序号: {entity.StageOrder}");
    }
}
