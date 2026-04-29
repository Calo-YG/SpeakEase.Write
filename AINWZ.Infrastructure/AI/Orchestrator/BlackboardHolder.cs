namespace SpeakEase.Write.Infrastructure.AI.Orchestrator;

/// <summary>
/// 持有当前请求的 WritingBlackboard 实例。
/// 使用 AsyncLocal 确保在同一个异步调用链中自动传递，
/// 无论子作用域创建多少个新的 DI 容器，工具端都能读取到值。
/// 注册为 Singleton。
/// </summary>
public sealed class BlackboardHolder
{
    private static readonly AsyncLocal<WritingBlackboard> _current = new();

    public WritingBlackboard Blackboard
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
