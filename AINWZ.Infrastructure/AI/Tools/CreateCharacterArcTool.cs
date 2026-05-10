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
            Description = "为角色创建一个成长弧线阶段。记录角色在故事中的阶段性变化：初始状态 → 触发事件 → 变化后的状态。每次重大性格转变或成长都应记录，用于确保角色发展连贯。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（必填），需与已有角色匹配" },
                    ["stage_title"] = new() { Type = "string", Description = "阶段标题（必填），如: 初出茅庐/遭遇背叛/顿悟成长" },
                    ["initial_state"] = new() { Type = "string", Description = "初始状态（必填），角色在该阶段开始时的性格/能力/处境" },
                    ["trigger_event"] = new() { Type = "string", Description = "触发变化的事件（必填）" },
                    ["changed_state"] = new() { Type = "string", Description = "变化后的状态（必填），角色经历事件后的改变" },
                    ["stage_order"] = new() { Type = "integer", Description = "阶段顺序号（可选），默认追加到末尾" }
                },
                Required = ["work_id", "character_name", "stage_title", "initial_state", "trigger_event", "changed_state"]
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var characterName = args.GetString("character_name", required: true);
        var stageTitle = args.GetString("stage_title", required: true);
        var initialState = args.GetString("initial_state", required: true);
        var triggerEvent = args.GetString("trigger_event", required: true);
        var changedState = args.GetString("changed_state", required: true);
        var stageOrder = args.GetInt32("stage_order", min: 0);
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var character = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == workId && c.Name == characterName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == workId && c.Name != null && c.Name.Contains(characterName), ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{characterName}」", "not_found");

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
