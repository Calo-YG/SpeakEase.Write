namespace SpeakEase.Write.Infrastructure.AI.Memory
{
    /// <summary>
    /// 记忆触发事件
    /// </summary>
    public sealed class TriggerEvent
    {
        /// <summary>
        /// 会话Id
        /// </summary>
        public string SessionId { get; private set; }

        /// <summary>
        /// 消息触发长度
        /// </summary>
        public int TriggerCount { get; private set; }

        /// <summary>
        /// 模型最大上下文长度
        /// </summary>
        public int ModelMaxToken { get; private set; }

        /// <summary>
        /// 记忆触发事件
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="modelMaxToken"></param>
        public TriggerEvent(string sessionId,int modelMaxToken)
        {
            SessionId = sessionId;
            TriggerCount = 100;
            ModelMaxToken = modelMaxToken;
        }
        
    }
}
