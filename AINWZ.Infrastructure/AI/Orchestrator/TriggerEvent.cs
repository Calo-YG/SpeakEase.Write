namespace SpeakEase.Write.Infrastructure.AI.Orchestrator
{
    public class TriggerEvent
    {
        /// <summary>
        /// 事件触发
        /// </summary>
        public Func<Task> Tgigger { get; private set; }

        /// <summary>
        /// 会话Id
        /// </summary>
        public string SessionId { get; private set; }
    }
}
