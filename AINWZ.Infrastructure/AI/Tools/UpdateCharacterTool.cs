using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class UpdateCharacterTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public UpdateCharacterTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "update_character",
            Description = "更新已有角色信息，只传需要修改的字段即可",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["character_id"] = new() { Type = "string", Description = "角色 ID" },
                    ["name"] = new() { Type = "string", Description = "角色姓名" },
                    ["identity"] = new() { Type = "string", Description = "身份/称号" },
                    ["gender"] = new() { Type = "string", Description = "性别" },
                    ["age"] = new() { Type = "string", Description = "年龄描述" },
                    ["personality"] = new() { Type = "string", Description = "性格描述" },
                    ["background"] = new() { Type = "string", Description = "背景故事" },
                    ["motivation"] = new() { Type = "string", Description = "动机/目标" },
                    ["appearance"] = new() { Type = "string", Description = "外貌描述" }
                },
                Required = new List<string> { "work_id", "character_id" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, characterId = null;
        string name = null, identity = null, gender = null,
            age = null, personality = null, background = null, motivation = null, appearance = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
            if (root.TryGetProperty("character_id", out var c)) characterId = c.GetString();
            if (root.TryGetProperty("name", out var n)) name = n.GetString();
            if (root.TryGetProperty("identity", out var i)) identity = i.GetString();
            if (root.TryGetProperty("gender", out var g)) gender = g.GetString();
            if (root.TryGetProperty("age", out var a)) age = a.GetString();
            if (root.TryGetProperty("personality", out var p)) personality = p.GetString();
            if (root.TryGetProperty("background", out var b)) background = b.GetString();
            if (root.TryGetProperty("motivation", out var m)) motivation = m.GetString();
            if (root.TryGetProperty("appearance", out var ap)) appearance = ap.GetString();
        }
        catch { }

        if (string.IsNullOrEmpty(workId)) return ToolResult.Fail("缺少 work_id 参数");
        if (string.IsNullOrEmpty(characterId)) return ToolResult.Fail("缺少 character_id 参数");

        var entity = await db.Characters
            .FirstOrDefaultAsync(x => x.Id == characterId && x.WorkId == workId, ct);

        if (entity is null)
            return ToolResult.Fail($"角色(id={characterId})不存在");

        if (name is not null) entity.Name = name;
        if (identity is not null) entity.Identity = identity;
        if (gender is not null) entity.Gender = gender;
        if (age is not null) entity.AgeDescription = age;
        if (personality is not null) entity.Personality = personality;
        if (background is not null) entity.BackgroundStory = background;
        if (motivation is not null) entity.Motivation = motivation;
        if (appearance is not null) entity.Appearance = appearance;
        entity.UpdateAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色「{entity.Name}」更新成功。身份：{entity.Identity}，性格：{entity.Personality}，动机：{entity.Motivation}");
    }
}
