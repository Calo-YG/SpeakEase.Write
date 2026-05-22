using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// 工具执行器接口：每个工具实现此接口以对外暴露执行逻辑。
    /// 通过 Keyed DI 按工具函数名注册，ToolCapable 按名路由执行。
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// 执行工具调用
        /// </summary>
        /// <param name="arguments">LLM 传入的参数字符串（JSON 格式）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果（包含成功/失败状态、输出内容、错误码）</returns>
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
    }
}
