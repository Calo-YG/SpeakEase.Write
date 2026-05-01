using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterTool : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CreateCharacterTool(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character",
            Description = "创建一个新角色",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识" },
                    ["name"] = new() { Type = "string", Description = "角色姓名" },
                    ["identity"] = new() { Type = "string", Description = "身份/称号" },
                    ["gender"] = new() { Type = "string", Description = "性别" },
                    ["age"] = new() { Type = "string", Description = "年龄描述" },
                    ["personality"] = new() { Type = "string", Description = "性格描述" },
                    ["background"] = new() { Type = "string", Description = "背景故事" },
                    ["motivation"] = new() { Type = "string", Description = "动机/目标" },
                    ["appearance"] = new() { Type = "string", Description = "外貌描述" }
                },
                Required = new List<string> { "work_id", "name" }
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();

        string workId = null, name = null, identity = null, gender = null,
            age = null, personality = null, background = null, motivation = null, appearance = null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            if (root.TryGetProperty("work_id", out var w)) workId = w.GetString();
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
        if (string.IsNullOrEmpty(name)) return ToolResult.Fail("缺少 name 参数");

        var exists = await db.Characters.AsNoTracking()
            .AnyAsync(x => x.WorkId == workId && x.Name == name, ct);
        if (exists) return ToolResult.Fail(string.Format("角色「{0}」已存在，请勿重复创建", name));

        var entity = new CharacterEntity
        {
            Id = Guid.NewGuid().ToString(),
            WorkId = workId,
            Name = name,
            Identity = identity ?? string.Empty,
            Gender = gender ?? string.Empty,
            AgeDescription = age ?? string.Empty,
            Personality = personality ?? string.Empty,
            BackgroundStory = background ?? string.Empty,
            Motivation = motivation ?? string.Empty,
            Appearance = appearance ?? string.Empty
        };

        db.Characters.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok(string.Format("角色「{0}」已创建，id: {1}", name, entity.Id));
    }
}
