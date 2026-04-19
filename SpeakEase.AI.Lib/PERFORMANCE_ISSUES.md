# SpeakEase.AI.Lib 性能问题清单

按影响程度排序，待修复。

---

## P1: `PrepareRequestAsync` 每轮迭代都深拷贝工具定义 + O(n²) 去重

**文件**: `ChatAgentBase.cs` L96-L109

**问题**:
- `CloneRequest` 深拷贝已有的 `request.Tools`（请求级工具被深拷贝一次）
- `CloneToolDefinition` 再次深拷贝 `toolCapable.Tools`（Agent 注册的工具又被深拷贝一次）
- `Any` 去重检查是 O(n) per tool，n 个工具注入是 O(n²)

**影响**: 假设 30 个工具、5 轮迭代 → 深拷贝 150 次 ToolDefinition + 150 次 ToolFunctionDefinition，去重检查约 2250 次字符串比较。这是 ReAct 主循环热路径。

**修复方向**: 工具定义注册后不变（只读），不需要每轮深拷贝。首次 `PrepareRequestAsync` 时构建完整的工具列表，后续迭代直接复用。去重用 `HashSet<string>` 替代 `Any`。

---

## P2: `ToolCapableBase.Tools` 每次访问都分配新列表

**文件**: `ToolCapableBase.cs` L23

**问题**:
```csharp
public IReadOnlyList<ToolDefinition> Tools => _definitions.Values.ToList().AsReadOnly();
```
每次访问都 `ToList()` + `AsReadOnly()`，分配两个新对象。

**触发点**:
- `PrepareRequestAsync` 每轮迭代遍历 `toolCapable.Tools`
- `ReActAgent.EnableSubAgent` 的 `() => _toolCapable.Tools` 闭包
- `SubAgentCapable.CreateSubAgent` 中 `_parentToolsProvider()` + `.Where(...)`

**修复方向**: 注册/移除工具时维护一个缓存的 `IReadOnlyList<ToolDefinition>`，访问时直接返回缓存。标记脏位或重建。

---

## P3: `OpenAICompatibleLLMBackend` 每次请求都重新配置 HttpClient Header

**文件**: `OpenAICompatibleLLMBackend.cs` L131-L152

**问题**:
- 每次 LLM 调用都 `CreateClient()` + 设置 BaseAddress + 添加/移除 Header
- `DefaultRequestHeaders` 操作非线程安全且冗余
- 模型回退循环中可能多次重建

**修复方向**: 配置不变时缓存预配置的 HttpClient；或在 `HttpRequestMessage` 级别设置 Header 而非 `DefaultRequestHeaders`。

---

## P4: `PlanAndExecuteStrategy` 流式路径用 `+=` 拼接字符串

**文件**: `PlanAndExecuteStrategy.cs` L251

**问题**:
```csharp
stepContent += chunk.ContentDelta ?? string.Empty;
```
每个 SSE chunk 都触发字符串拼接，创建新 string 对象。几百个 chunk → 几百次中间字符串分配。

**修复方向**: 改用 `StringBuilder` 或 `StringWriter`。

---

## P5: `PlanAndExecuteStrategy` 流式路径 `Skip(messages.Count)` 非 O(1)

**文件**: `PlanAndExecuteStrategy.cs` L303

**问题**:
```csharp
messages.AddRange(stepMessages.Skip(messages.Count));
```
`List<T>.Skip` 是 O(n)，随步骤推进 `messages` 越长，开销线性增长。

**修复方向**: 用范围操作 `stepMessages[messages.Count..]`，O(1)。

---

## P6: `SubAgentCapable` 每次工具注册都创建闭包对象

**文件**: `SubAgentCapable.cs` L206-L211

**问题**:
```csharp
var capturedName = toolDef.Function?.Name;
subAgent.RegisterTool(toolDef, (args, ct) =>
{
    return _parentToolExecutorProvider(capturedName, args, ct);
});
```
每个工具注册都创建一个闭包 DisplayClass + 捕获 `capturedName`。30 个工具 = 30 个闭包对象，且每次 spawn 都重复创建。

**修复方向**: 闭包无法避免，但影响很小，优先级最低。如果后续 spawn 频率高，可考虑缓存工具执行器映射表。
