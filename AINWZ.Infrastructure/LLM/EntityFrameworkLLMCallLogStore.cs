using AINWZ.Domain.Entities.AI;
using AINWZ.Infrastructure.LLM.Contract;
using AINWZ.Infrastructure.LLM.Models;
using AINWZ.Infrastructure.Persistence;

namespace AINWZ.Infrastructure.LLM;

/// <summary>
/// 基于 Entity Framework 的 LLM 调用日志存储实现。
/// </summary>
/// <remarks>
/// 初始化存储实现。
/// </remarks>
public sealed class EntityFrameworkLLMCallLogStore(AINWZDbContext dbContext) : ILLMCallLogStore
{

    /// <inheritdoc />
    public async Task SaveAsync(LLMCallLogRecord record, CancellationToken cancellationToken = default)
    {
        var entity = new LLMCallLogEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            CallType = record.CallType,
            SkillName = record.SkillName,
            RequestSummary = Truncate(record.RequestSummary, 4000) ?? string.Empty,
            ResponseSummary = Truncate(record.ResponseSummary, 4000) ?? string.Empty,
            PrimaryModel = Truncate(record.PrimaryModel, 128) ?? string.Empty,
            FinalModel = Truncate(record.FinalModel, 128) ?? string.Empty,
            UsedFallback = record.UsedFallback,
            FallbackModel = Truncate(record.FallbackModel, 128),
            RequestId = Truncate(record.RequestId, 128),
            FinishReason = Truncate(record.FinishReason, 64),
            ToolCallsSummary = Truncate(record.ToolCallsSummary, 4000),
            ToolResultsSummary = Truncate(record.ToolResultsSummary, 4000),
            Success = record.Success,
            ErrorMessage = Truncate(record.ErrorMessage, 4000)
        };

        dbContext.LlmCallLogs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
