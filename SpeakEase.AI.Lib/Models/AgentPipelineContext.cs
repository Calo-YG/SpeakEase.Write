using SpeakEase.AI.Lib.OpenAIModel;
namespace SpeakEase.AI.Lib.Models;

/// <summary>
/// Pipeline Filter 上下文，携带当前 Agent 执行状态
/// </summary>
public sealed class AgentPipelineContext
{
    /// <summary>
    /// 对话请求信息
    /// </summary>
    public ChatCompletionRequest ChatCompletionRequest { get; set; }

    /// <summary>
    /// 对话响应信息
    /// </summary>
    public ChatCompletionResponse ChatCompletionResponse { get; set; }

    /// <summary>
    /// 请求执行信息
    /// </summary>
    public List<ToolResult> ExecutedToolResults { get; set; }


    /// <summary>
    /// 当前执行轮次
    /// </summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// 最大执行轮次
    /// </summary>
    public int MaxIterations { get; set; }
}
