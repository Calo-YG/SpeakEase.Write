using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpeakEase.AI.Lib.Contract;
using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;
using SpeakEase.Write.Application.Abstractions.Persistence;

namespace SpeakEase.Write.Infrastructure.AI.Tools;

// 作品信息查询工具：返回作品完整基本信息（简介/题材/风格/视角/字数/章节数/角色数等）
public sealed class GetWorkInfoTool(IServiceScopeFactory scopeFactory, IOptionsSnapshot<JsonSerializerOptions> snapshot) : IToolExecutor
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    public static readonly ToolDefinition ToolDefinition = new()
    {
        Type = "function",
        Function = new FunctionDefinition
        {
            Name = "get_work_info",
            Description = "获取作品的完整基本信息（简介、题材、风格、视角、字数等）",
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

        var work = await db.Works.AsNoTracking()
            .Where(x => x.Id == args.WorkId)
            .Select(x => new
            {
                x.Title,
                x.Summary,
                x.Genre,
                x.Perspective,
                x.StyleTags,
                x.CreationMode,
                x.Status,
                x.TotalWordCount,
                x.WritingRules,
                ChapterCount = db.Chapters.Count(chapter => chapter.WorkId == x.Id),
                VolumeCount = db.Volumes.Count(volume => volume.WorkId == x.Id),
                CharacterCount = db.Characters.Count(character => character.WorkId == x.Id)
            })
            .FirstOrDefaultAsync(ct);

        if (work == null)
            return ToolResult.Fail($"未找到作品 {args.WorkId}", "not_found");

        return ToolResult.Ok(JsonSerializer.Serialize(new
        {
            work.Title,
            work.Summary,
            work.Genre,
            work.Perspective,
            work.StyleTags,
            work.CreationMode,
            work.Status,
            work.TotalWordCount,
            work.WritingRules,
            chapterCount = work.ChapterCount,
            volumeCount = work.VolumeCount,
            characterCount = work.CharacterCount
        }, snapshot.Value));
    }

    private sealed record Args
    {
        [JsonPropertyName("work_id")]
        public string WorkId { get; init; }

        public ToolResult Validate()
        {
            if (string.IsNullOrWhiteSpace(WorkId))
                return ToolResult.Fail("缺少必需参数 'work_id'", "argument_parse_error");
            return null;
        }
    }
}
