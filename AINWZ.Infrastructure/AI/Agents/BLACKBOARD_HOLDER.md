# BlackboardHolder 技术方案

## 一、问题描述

Orchestrator 构建黑板后，创作域工具（Tool）需要读取黑板数据。
但工具和 Orchestrator 运行在不同的 DI 作用域中。

```
请求作用域（Root Scope）
  └── CreationOrchestrator
        └── 设置 BlackboardHolder.Blackboard = 已构建的黑板
        │
        └── ReActAgent.ExecuteStreamAsync()
              │
              └── 内部 LLM 返回 tool_call
                    │
                    └── ToolCapable.ExecuteAsync()
                          │
                          └── CreateAsyncScope() ← 创建子作用域
                                │
                                └── scope.GetRequiredKeyedService<IToolExecutor>()
                                      │
                                      └── tool.ExecuteAsync(arguments)
                                            │
                                            └── 需要读取 BlackboardHolder.Blackboard
                                                  ← 但这是新的子作用域，
                                                     拿不到 Root Scope 的值 ❌
```

---

## 二、现有代码分析

### ToolCapable 的执行方式（不可修改）

```csharp
// 文件: SpeakEase.AI.Lib/ToolCapable.cs
public sealed class ToolCapable(IServiceProvider serviceProvider) : IToolCapable
{
    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        // ▸▸▸ 这里创建了子作用域 ◂◂◂
        await using var scope = serviceProvider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredKeyedService<IToolExecutor>(toolName);
        return await executor.ExecuteAsync(toolCall.Function.Arguments, ct);
    }
}
```

`CreateAsyncScope()` 是微软 DI 的标准行为——**子作用域里的 Scoped 服务是新实例**。

所以无论 `BlackboardHolder` 注册为 Scoped 还是 Singleton，在子作用域里都拿不到父作用域设置的值。

---

## 三、方案 A：AsyncLocal<T>（推荐）

### 原理

`AsyncLocal<T>` 是 .NET 标准库提供的**异步上下文传递**机制；
在同一个 `async` 调用链中，值自动流动，跨 `await`、跨线程安全。

```
同一个调用链（同一个 async context）
  Orchestrator
    └── await ReActAgent.ExecuteAsync(...)
          └── await ToolCapable.ExecuteAsync(...)
                └── await tool.ExecuteAsync(...)
                      └── 读取 AsyncLocal → 拿到值 ✅
```

### BlackboardHolder 实现

```csharp
/// <summary>
/// 持有当前请求的 WritingBlackboard 实例。
/// 使用 AsyncLocal<T> 确保在同一个异步调用链中自动传递，
/// 无论子作用域创建多少个新的 DI 容器，都能读取到值。
/// </summary>
public sealed class BlackboardHolder
{
    // AsyncLocal：同一个 async 调用链中自动传递
    private static readonly AsyncLocal<WritingBlackboard> _current = new();

    /// <summary>
    /// 当前请求的黑板数据。
    /// Orchestrator 设置 → 工具读取，自动共享。
    /// </summary>
    public WritingBlackboard? Blackboard
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

### 注册方式

```csharp
// BlackboardHolder 不依赖任何 DI 容器，注册为 Singleton
services.AddSingleton<BlackboardHolder>();
```

### 完整调用链

```csharp
// ─── CreationOrchestrator ───
public sealed class CreationOrchestrator
{
    private readonly BlackboardHolder _holder;

    public async IAsyncEnumerable<AgentStreamChunk> ExecuteAsync(
        string workId, string userMessage, CancellationToken ct)
    {
        // 1. 构建黑板
        var blackboard = await _blackboardBuilder.BuildAsync(workId, ...);

        // 2. ▸▸▸ 设置 AsyncLocal ◂◂◂
        //    在当前异步调用链中注入黑板数据
        _holder.Blackboard = blackboard;

        // 3. 执行 Agent（整个调用链在同一个 async context 中）
        var agent = _agents.First(a => a.Name == route.AgentName);
        await foreach (var chunk in agent.ExecuteStreamAsync(request, ct))
        {
            yield return chunk;
        }

        // 4. 请求结束，AsyncLocal 自动释放
    }
}

// ─── GetCharacterTool（创作域工具） ───
public sealed class GetCharacterTool : IToolExecutor
{
    private readonly BlackboardHolder _holder;

    public GetCharacterTool(BlackboardHolder holder) => _holder = holder;

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct)
    {
        // ▸▸▸ 读取 AsyncLocal ◂◂◂
        // 虽然 ToolCapable 创建了子作用域，
        // 但 BlackboardHolder 是 Singleton（在根容器中），
        // 且 AsyncLocal 在同一调用链中自动传递。
        var board = _holder.Blackboard;

        if (board?.Characters == null)
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Content = "暂无角色信息"
            });

        // ... 查询逻辑
    }
}
```

### 为什么 AsyncLocal 能穿透子作用域？

```
Orchestrator 设置 _holder.Blackboard = value
  │
  │  AsyncLocal 在当前 async 调用链中记录值
  │  （.NET 运行时维护 async-local 存储）
  │
  ├── await Agent.ExecuteAsync(...)
  │     │
  │     ├── await ReActAgent.ExecuteAsync(...)
  │     │     │
  │     │     ├── await OpenAICompatible.StreamAsync(...)
  │     │     │     │
  │     │     │     └── await foreach (chunk in stream)
  │     │     │           ← 同一个调用链
  │     │     │
  │     │     └── ToolCapable.ExecuteAsync(toolCall)
  │     │           │
  │     │           └── CreateAsyncScope()
  │     │                 │
  │     │                 └── scope.GetService<GetCharacterTool>()
  │     │                       │
  │     │                       └── GetCharacterTool 注入的 BlackboardHolder
  │     │                             │
  │     │                             └── _holder.Blackboard
  │     │                                   │
  │     │                                   └── AsyncLocal 读取
  │     │                                         ← 同一个 async 调用链 ✅
  │     │
  │     └── ...
  │
  └── 方法返回
        │
        └── AsyncLocal 自动清除
              ← 不会泄漏到其他请求 ✅
```

关键点：
- `BlackboardHolder` 注册为 **Singleton**，所以注入到工具中的是**同一个实例**
- 该实例中的 `AsyncLocal<WritingBlackboard>` 在同一个 `async` 调用链中自动传递值
- 请求结束后 AsyncLocal 自动释放，不会跨请求污染

---

## 四、方案 B：SkipScope ToolCapable（备用方案）

如果不想用 `AsyncLocal`，可以给 `ToolCapable` 加一个**不创建子作用域**的执行路径。

### 修改 ToolCapable

```csharp
// 在 ToolCapable 中新增方法
public sealed class ToolCapable(IServiceProvider serviceProvider) : IToolCapable
{
    // 原有方法（保留不动）—— 创建子作用域
    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await ExecuteInternalAsync(toolCall, scope.ServiceProvider, ct);
    }

    // 新增方法 —— 使用传入的 provider，不创建子作用域
    public async Task<ToolResult> ExecuteInScopeAsync(
        ToolCall toolCall, IServiceProvider scopeProvider, CancellationToken ct)
    {
        return await ExecuteInternalAsync(toolCall, scopeProvider, ct);
    }

    // 内部执行逻辑（提取公用）
    private async Task<ToolResult> ExecuteInternalAsync(
        ToolCall toolCall, IServiceProvider provider, CancellationToken ct)
    {
        var executor = provider.GetRequiredKeyedService<IToolExecutor>(toolName);
        return await executor.ExecuteAsync(toolCall.Function.Arguments, ct);
    }
}
```

### ReActAgent 也需要额外方法

```csharp
// 现有方法（保留不动）
public async Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken ct)
{
    // 使用原有的 ToolCapable.ExecuteAsync（创建子作用域）
}

// 新增方法 —— 接受作用域 provider
public async Task<AgentResponse> ExecuteInScopeAsync(
    AgentRequest request, IServiceProvider scopeProvider, CancellationToken ct)
{
    // 使用 scopeProvider，不创建子作用域
    // 调用 ToolCapable.ExecuteInScopeAsync(...)
}
```

### 缺点

| 对比 | 方案 A（AsyncLocal） | 方案 B（SkipScope） |
|------|---------------------|-------------------|
| 修改现有代码 | 0 行 | 需修改 ToolCapable + ReActAgent |
| 侵入性 | 无（只用新增类） | 高（改现有库代码） |
| 边界情况 | 线程切换安全 | 依赖调用方传参 |
| 理解成本 | 需了解 AsyncLocal | 直接 |

所以方案 A `AsyncLocal<T>` 是更好的选择。

---

## 五、总结

| | 方案 A：AsyncLocal ✅ | 方案 B：SkipScope |
|--|----------------------|------------------|
| **改动范围** | 仅 `BlackboardHolder` 一个文件 | 需改 `ToolCapable` + `ReActAgent` |
| **现有代码** | 完全不动 | 需要修改第三方库代码 |
| **注册方式** | Singleton | Scoped |
| **工具写法** | 正常 DI 注入 | 正常 DI 注入 |
| **推荐程度** | ★★★★★ | ★★★☆☆ |

**最终方案：BlackboardHolder 注册为 Singleton，内部使用 AsyncLocal<WritingBlackboard> 桥接数据。**
