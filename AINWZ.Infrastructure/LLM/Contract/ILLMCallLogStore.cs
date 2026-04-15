using AINWZ.Infrastructure.LLM.Models;

namespace AINWZ.Infrastructure.LLM.Contract
{
    /// <summary>
    /// LLM 调用日志存储抽象。
    /// </summary>
    public interface ILLMCallLogStore
    {
        Task SaveAsync(LLMCallLogRecord record, CancellationToken cancellationToken = default);
    }
}
