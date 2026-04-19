namespace SpeakEase.AI.Lib.Contract
{
    /// <summary>
    /// Agent 标记接口，标识一个 Agent 实现。
    /// 不定义行为，仅用于 DI 注册和类型识别。
    /// 具体能力通过 IChatAgent、IToolCapable、ISkillCapable 等组合接口表达。
    /// </summary>
    public interface IAgent
    {
    }
}
