namespace SpeakEase.Write.Application.Contracts.AI.Dto
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class ReqAgentChat
    {
        /// <summary>
        /// 所属作品标识（创作 Agent 必填）
        /// </summary>
        public string WorkId { get; set; }

        /// <summary>
        /// 用户输入消息
        /// </summary>
        public string Message { get; set; }
    }
}
