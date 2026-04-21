using AINWZ.Application.Contracts.AI;
using AINWZ.Application.Contracts.AI.Dto;
using AINWZ.Infrastructure.Persistence;
using AINWZ.Infrastructure.Shared;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Authorization.Authorization;

namespace AINWZ.Application.Applications;

/// <summary>
/// LLM 调用日志查询应用服务实现。
/// </summary>
public class LLMCallLogApplication(SpeakEaseDbContext dbContext,IUserContext userContext) : ILLMCallLogApplication
{
    /// <inheritdoc />
    public async Task<ApiResult<PageResult<LLMCallLogDto>>> GetPagedAsync(LLMCallLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(request.Pagination.Page, 1);
        var pageSize = Math.Clamp(request.Pagination.PageSize < 1 ? 10 : request.Pagination.PageSize, 1, 100);

        var query = dbContext.LlmCallLogs.AsNoTracking().Where(p=>p.OwnerId == userContext.UserId);

        if (!string.IsNullOrWhiteSpace(request.CallType))
        {
            query = query.Where(x => x.CallType == request.CallType);
        }

        if (!string.IsNullOrWhiteSpace(request.SkillName))
        {
            query = query.Where(x => x.SkillName == request.SkillName);
        }

        if (request.OnlyFailed == true)
        {
            query = query.Where(x => !x.Success);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreateAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LLMCallLogDto
        {
            Id = x.Id,
            CallType = x.CallType,
            SkillName = x.SkillName,
            RequestSummary = x.RequestSummary,
            ResponseSummary = x.ResponseSummary,
            PrimaryModel = x.PrimaryModel,
            FinalModel = x.FinalModel,
            UsedFallback = x.UsedFallback,
            FallbackModel = x.FallbackModel,
            RequestId = x.RequestId,
            FinishReason = x.FinishReason,
            ToolCallsSummary = x.ToolCallsSummary,
            ToolResultsSummary = x.ToolResultsSummary,
            Success = x.Success,
            ErrorMessage = x.ErrorMessage,
            CreateAt = x.CreateAt
        })
            .ToListAsync(cancellationToken);

        var result = PageResult<LLMCallLogDto>.Create(total, items);

        return new ApiResult<PageResult<LLMCallLogDto>>(result);
    }

    /// <inheritdoc />
    public async Task<ApiResult<LLMCallLogDto>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LlmCallLogs
               .Where(x => x.Id == id && x.OwnerId == userContext.UserId)
                           .Select(x => new LLMCallLogDto
        {
            Id = x.Id,
            CallType = x.CallType,
            SkillName = x.SkillName,
            RequestSummary = x.RequestSummary,
            ResponseSummary = x.ResponseSummary,
            PrimaryModel = x.PrimaryModel,
            FinalModel = x.FinalModel,
            UsedFallback = x.UsedFallback,
            FallbackModel = x.FallbackModel,
            RequestId = x.RequestId,
            FinishReason = x.FinishReason,
            ToolCallsSummary = x.ToolCallsSummary,
            ToolResultsSummary = x.ToolResultsSummary,
            Success = x.Success,
            ErrorMessage = x.ErrorMessage,
            CreateAt = x.CreateAt
        })
               .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return new ApiResult<LLMCallLogDto>($"未找到标识为 {id} 的调用日志。", 404);
        }

        return new ApiResult<LLMCallLogDto>(entity);
    }
}
