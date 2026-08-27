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

// 角色关系图谱创建/更新工具：创建可视化关系网，每个作品可有多个版本的关系图谱
public sealed class CreateCharacterGraphTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character_graph",
            Description = "创建或更新角色关系图谱。通过 id 或 name 查找已有图谱，存在则更新，不存在则创建。每个作品可以有多个版本的关系图谱。",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
                    ["id"] = new() { Type = "string", Description = "图谱ID（可选），用于更新已有图谱" },
                    ["name"] = new() { Type = "string", Description = "图谱名称（必填），如: 第一卷角色关系、最终版关系网" },
                    ["description"] = new() { Type = "string", Description = "图谱描述（可选），说明该图谱的目的或覆盖范围" },
                    ["layout_json"] = new() { Type = "string", Description = "前端布局JSON（可选），保存节点/边的前端位置信息" }
                },
                Required = ["work_id", "name"]
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

        CharacterGraphEntity graph = null;
        if (!string.IsNullOrEmpty(args.Id))
            graph = await db.CharacterGraphs.FirstOrDefaultAsync(x => x.Id == args.Id && x.WorkId == args.WorkId, ct);
        if (graph == null)
            graph = await db.CharacterGraphs.FirstOrDefaultAsync(x => x.WorkId == args.WorkId && x.Name == args.Name, ct);

        if (graph != null)
        {
            graph.Name = args.Name;
            if (!string.IsNullOrEmpty(args.Description)) graph.Description = args.Description;
            if (args.LayoutJson != null) graph.LayoutJson = args.LayoutJson ?? string.Empty;
            await db.SaveChangesAsync(ct);
            return ToolResult.Ok($"角色关系图谱「{args.Name}」已更新，ID: {graph.Id}");
        }

        var newGraph = new CharacterGraphEntity
        {
            Id = idGen.NextIdString(),
            WorkId = args.WorkId,
            Name = args.Name,
            Description = args.Description ?? string.Empty,
            Version = 1,
            Status = "draft",
            LayoutJson = args.LayoutJson ?? string.Empty
        };

        await db.CharacterGraphs.AddAsync(newGraph, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色关系图谱「{args.Name}」已创建，ID: {newGraph.Id}，版本: v1");
    }

    private sealed record Args
    {
        public string WorkId { get; init; }
        public string Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string LayoutJson { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            if (string.IsNullOrWhiteSpace(Name))
                return ToolResult.Fail("缺少必需参数 'name'", "argument_parse_error");
            return null;
        }
    }
}
