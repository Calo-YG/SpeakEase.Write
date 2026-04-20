using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// 工具执行类
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// 工具执行类
        /// </summary>
        public ToolDefinition ToolDefinition { get; }

        /// <summary>
        /// 工具执行方法
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
    }
}
