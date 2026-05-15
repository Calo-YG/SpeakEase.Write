using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Domain.Entities.Story;
using SpeakEase.Write.Infrastructure.Ids;
using SpeakEase.Write.Infrastructure.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

public sealed class CreateCharacterGraphTool(IServiceScopeFactory scopeFactory) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "create_character_graph",
            Description = "创建角色关系图谱快照。为作品创建一个新的关系图谱，用于可视化角色之间的关系网络。每个作品可以有多个版本的关系图谱",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品ID（必填）" },
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
        var args = ToolArgumentParser.Parse(arguments);
        var workId = args.GetString("work_id", required: true);
        var name = args.GetString("name", required: true);
        var description = args.GetString("description");
        var layoutJson = args.GetString("layout_json");
        if (args.HasErrors) return args.ToErrorResult();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpeakEaseDbContext>();
        var idGen = scope.ServiceProvider.GetRequiredService<ISnowflakeIdGenerator>();

        var existing = await db.CharacterGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkId == workId && x.Name == name, ct);

        if (existing != null)
            return ToolResult.Fail($"图谱「{name}」已存在，ID: {existing.Id}", "duplicate_name");

        var graph = new CharacterGraphEntity
        {
            Id = idGen.NextIdString(),
            WorkId = workId,
            Name = name,
            Description = description ?? string.Empty,
            Version = 1,
            Status = "draft",
            LayoutJson = layoutJson ?? string.Empty
        };

        await db.CharacterGraphs.AddAsync(graph, ct);
        await db.SaveChangesAsync(ct);

        return ToolResult.Ok($"角色关系图谱「{name}」已创建，ID: {graph.Id}，版本: v1");
    }
}
