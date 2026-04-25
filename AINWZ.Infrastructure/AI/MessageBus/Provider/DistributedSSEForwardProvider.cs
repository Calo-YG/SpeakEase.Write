namespace SpeakEase.Write.Infrastructure.AI.MessageBus.Provider
{
    /// <summary>
    /// 分布式 SubAgent 事件转发 todo （考虑集群情况负载均衡情况下SSE Chunck 分发，1方案需要扩展一个分布式SSE Client  内部采用 发布订阅机制，2使用SignaIR 的机制 分发chunck） 
    /// </summary>
    public sealed class DistributedSSEForwardProvider:ISSEForwardProvider
    {
        //todo 当前版本不实现
    }
}
