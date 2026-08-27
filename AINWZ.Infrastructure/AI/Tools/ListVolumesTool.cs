using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 卷列表查询工具：列出作品所有卷，包含卷序号、标题和所含章节概览
public sealed class ListVolumesTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "list_volumes",
            Description = "列出作品的所有卷，包含卷序号、标题和所含章节概览",
            Parameters = new FunctionParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ParameterSchema>
                {
                    ["work_id"] = new() { Type = "string", Description = "作品标识（必填）" }
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

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IWriteDbContext>();

        var volumes = await db.Volumes.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId)
            .OrderBy(x => x.Sequence)
            .Take(20)
            .ToListAsync(ct);

        var chapters = await db.Chapters.AsNoTracking()
            .Where(x => x.WorkId == args.WorkId)
            .OrderBy(x => x.Sequence)
            .Take(500)
            .ToListAsync(ct);

        var result = volumes.Select(v => new
        {
            v.Sequence,
            v.Title,
            v.Summary,
            chapters = chapters.Where(c => c.VolumeId == v.Id)
                .OrderBy(c => c.Sequence)
                .Select(c => new { c.Sequence, c.Title, c.Summary, c.WordCount, c.Status })
        });

        return ToolResult.Ok(JsonSerializer.Serialize(result, snapshot.Value));
    }

    private sealed record Args
    {
        public string WorkId { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
