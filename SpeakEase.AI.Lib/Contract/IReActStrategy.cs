using SpeakEase.AI.Lib.Models;

namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent Loop 执行策略抽象。
    /// 不同的策略决定 Agent 如何迭代：ReAct 循环、单轮调用、Plan-and-Execute 等。
    /// </summary>
    public interface IReActStrategy
    {

    }
}
