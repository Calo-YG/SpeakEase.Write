using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色成长弧查询工具：返回角色从出场到当前的所有成长阶段，用于续写时保持角色发展连贯
public sealed class GetCharacterArcTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character_arc",
            Description = "查询角色的成长弧线，返回该角色从出场到当前的所有成长阶段。用于续写时保持角色发展的连贯性，避免性格突变。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["character_name"] = new() { Type = "string", Description = "角色名称（必填）" }
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

        var character = await db.Characters.FirstOrDefaultAsync(
            c => c.WorkId == args.WorkId && c.Name == args.CharacterName, ct)
            ?? await db.Characters.FirstOrDefaultAsync(
                c => c.WorkId == args.WorkId && c.Name != null && c.Name.Contains(args.CharacterName), ct);

        if (character == null)
            return ToolResult.Fail($"未找到角色「{args.CharacterName}」", "not_found");

        var arcs = await db.CharacterArcs.AsNoTracking()
            .Where(a => a.WorkId == args.WorkId && a.CharacterId == character.Id)
            .OrderBy(a => a.StageOrder)
            .ToListAsync(ct);

        if (arcs.Count == 0)
            return ToolResult.Ok($"角色「{character.Name}」暂无成长弧线记录");

        var sb = new StringBuilder();
        sb.AppendLine($"## {character.Name} 的成长弧线（{arcs.Count}个阶段）");
        sb.AppendLine();

        for (var i = 0; i < arcs.Count; i++)
        {
            var arc = arcs[i];
            sb.AppendLine($"### 阶段 {arc.StageOrder}: {arc.StageTitle}");
            sb.AppendLine($"  初始状态: {arc.InitialState}");
            sb.AppendLine($"  触发事件: {arc.TriggerEvent}");
            sb.AppendLine($"  变化结果: {arc.ChangedState}");
            if (i < arcs.Count - 1)
                sb.AppendLine("  ↓");
            sb.AppendLine();
        }

        return ToolResult.Ok(sb.ToString());
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string CharacterName { get; init; }

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
