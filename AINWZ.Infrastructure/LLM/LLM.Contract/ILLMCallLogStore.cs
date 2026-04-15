namespace AINWZ.Infrastructure.LLM.LLM.Contract
{
    /// <summary>
    /// LLM 调用日志存储抽象。
    /// </summary>
    public interface ILLMCallLogStore
    {
        Task SaveAsync(LLMCallLogRecord record, CancellationToken cancellationToken = default);
    }
}
