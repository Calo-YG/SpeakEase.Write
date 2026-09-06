using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 角色列表查询工具：返回作品所有角色的名称、ID、身份和性格概要，不返回详细背景
public sealed class GetCharacterListTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_character_list",
            Description = "列出作品所有角色的名称、ID、身份和性格概要，不返回详细背景",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" },
                    ["limit"] = new() { Type = "integer", Description = "返回数量上限（默认30，范围1-100）" }
                },
                Required = ["work_id"]
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

        var limit = args.Limit != 0 ? args.Limit : 30;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ICharacterDbContext>();

        var characters = await db.Characters.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId)
            .Take(limit)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Identity,
                x.Gender,
                x.Personality
            })
            .ToListAsync(ct);

        if (characters.Count == 0)
            return ToolResult.Fail("当前作品暂无角色", "no_characters");

        return ToolResult.Ok(JsonSerializer.Serialize(characters, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public int Limit { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (Limit != 0 && (Limit < 1 || Limit > 100))
                return ToolResult.Fail($"参数 'limit' 值 {Limit} 超出范围 [1, 100]", "argument_parse_error");
            return null;
        }
    }
}
