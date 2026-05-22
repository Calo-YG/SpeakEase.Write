using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.AI.Dto;
using SpeakEase.Write.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SpeakEase.Authorization.Authorization;
using SpeakEase.Write.Infrastructure.Shared;

namespace SpeakEase.Write.Application.Applications;

// LLM调用日志查询应用服务：分页查询用户的所有AI调用记录，支持按类型、技能名、成功/失败状态筛选
public class LLMCallLogApplication(SpeakEaseDbContext dbContext,IUserContext userContext) : ILLMCallLogApplication
{
    // 分页查询LLM调用日志：支持多条件筛选（CallType、SkillName、OnlyFailed）
    public async Task<ApiResult<PageResult<LLMCallLogDto>>> GetPagedAsync(LLMCallLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        // 分页参数处理：页码最小1，每页1-100条（默认10条）
        var page = Math.Max(request.Pagination.Page, 1);
        var pageSize = Math.Clamp(request.Pagination.PageSize < 1 ? 10 : request.Pagination.PageSize, 1, 100);

        // 基础查询：仅查当前用户的日志
        var query = dbContext.LlmCallLogs.AsNoTracking().Where(p=>p.OwnerId == userContext.UserId);

        // 可选筛选：按调用类型过滤
        if (!string.IsNullOrWhiteSpace(request.CallType))
        {
            query = query.Where(x => x.CallType == request.CallType);
        }

        // 可选筛选：按技能名过滤
        if (!string.IsNullOrWhiteSpace(request.SkillName))
        {
            query = query.Where(x => x.SkillName == request.SkillName);
        }

        // 可选筛选：仅查看失败的调用记录
        if (request.OnlyFailed == true)
        {
            query = query.Where(x => !x.Success);
        }

        // 先统计符合条件的总记录数
        var total = await query.CountAsync(cancellationToken);

        // 按创建时间倒序分页查询详情
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

        var result = PageResult<LLMCallLogDto>.Create(total, items, page, pageSize);

        return new ApiResult<PageResult<LLMCallLogDto>>(result);
    }

    // 按ID查询单条LLM调用日志详情（同时校验数据归属）
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
