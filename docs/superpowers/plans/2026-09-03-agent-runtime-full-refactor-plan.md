# AgentRuntime Full Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变现有 API、SSE、Tool/Skill 契约和历史数据库数据的前提下，完成 AI 调用链路、Prompt-light Agent、Tool 暴露策略、人物自生长和 Chat/Memory 边界的后端重构。

**Architecture:** 保留模块化单体，在 `SpeakEase.AI.Lib` 内建设由 `Runner/RuntimeHost/RunContext/StepScheduler/EventSink` 组成的 AgentRuntime。Infrastructure 通过兼容适配器连接现有 Agent、Tool、Skill 和 EF/Application ports；人物动态状态采用追加式事件与版本化快照，旧 `CharacterEntity` 和 `CharacterArcEntity` 作为兼容投影。

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core, PostgreSQL, xUnit, `IChatCompatible`, `IToolCapable`, SSE。

**Spec:** `docs/architecture/agent-runtime-reference-architecture.md`

## Global Constraints

- 必须兼容现有 API、SSE 事件、Tool 名称、参数 Schema、返回契约和数据库历史数据。
- 不引入第三方 Agent Framework，不采用 `ReactAgent` 作为 Runtime 核心。
- 现有 `INovelAgent`、`ReActAgent`、`IToolCapable`、`ISkilCapable` 和 `SKILL.md` 路径继续可用。
- Runtime 核心不得引用 EF Core、Application 业务实现、Infrastructure DbContext 或领域实体。
- Nullable 保持 disabled，C# 使用 4 空格缩进，公共接口和 DTO 遵循项目命名规范。
- 只读 EF 查询使用 `AsNoTracking()`；批量更新/删除使用 `ExecuteUpdateAsync` / `ExecuteDeleteAsync`。
- 多表写入使用显式事务；所有有副作用 Tool 必须经过 Guard、Journal 和幂等检查。
- 所有新增数据库表采用追加式迁移，不删除或重解释旧列。
- 每个任务完成后运行对应的聚焦测试，并使用 `<type>: <description>` 提交。

---

### Task 1: 冻结兼容行为并建立回归基线

**Files:**
- Create: `AINWZ.Tests/AI/CompatibilityContractTests.cs`
- Create: `AINWZ.Tests/AI/ToolContractTests.cs`
- Modify: `AINWZ.Tests/AI/AgentApplicationTests.cs`
- Modify: `AINWZ.Tests/AI/CreationOrchestratorTests.cs`（若文件不存在则创建）

**Interfaces:**
- Consumes: 现有 `IAgentApplication`、`IAgentOrchestrator`、`AgentStreamChunk`、`ToolDefinition`。
- Produces: 可重复执行的 API/SSE/Tool 契约测试，供后续每个任务运行。

- [ ] **Step 1: 写 SSE 兼容失败测试**

为当前 `/ai/agent/chat/stream` 的测试 Fake 添加事件序列断言，至少覆盖 `content`、`tool_result`、`done` 和错误终态：

```csharp
[Fact]
public async Task StreamChat_PreservesLegacyEventTypesAndOrder()
{
    var chunks = await RunStreamingChatAsync();

    Assert.Equal(new[] { "content", "tool_result", "content", "done" },
        chunks.Select(x => x.Type));
    Assert.NotNull(chunks[^1].FinalResponse);
}
```

- [ ] **Step 2: 写 Tool Schema 兼容测试**

读取所有已注册 Tool 的 `ToolDefinition.Function.Name` 和参数 JSON，断言关键名称存在且 Schema 非空：

```csharp
[Theory]
[InlineData("get_character")]
[InlineData("update_character")]
[InlineData("save_chapter_content")]
[InlineData("create_character_arc")]
public void ToolDefinition_KeepsLegacyNameAndParameters(string name)
{
    var definition = _registry.Get(name);

    Assert.Equal(name, definition.Function.Name);
    Assert.NotNull(definition.Function.Parameters);
}
```

- [ ] **Step 3: 运行基线测试**

运行：

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CompatibilityContractTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~ToolContractTests --no-restore
```

预期：当前实现通过；若失败，先记录真实现有行为并将断言调整为协议事实，不修改生产代码。

- [ ] **Step 4: 提交基线**

```powershell
git add AINWZ.Tests/AI/CompatibilityContractTests.cs AINWZ.Tests/AI/ToolContractTests.cs AINWZ.Tests/AI/AgentApplicationTests.cs AINWZ.Tests/AI/CreationOrchestratorTests.cs
git commit -m "test: freeze ai compatibility contracts"
```

### Task 2: 建立 Tool Registry 与动态暴露策略

**Files:**
- Create: `SpeakEase.AI.Lib/Runtime/ToolCapabilityDescriptor.cs`
- Create: `SpeakEase.AI.Lib/Runtime/IToolRegistry.cs`
- Create: `SpeakEase.AI.Lib/Runtime/ToolRegistry.cs`
- Create: `SpeakEase.AI.Lib/Runtime/ToolExposurePolicy.cs`
- Create: `SpeakEase.AI.Lib/Runtime/ToolExposureContext.cs`
- Create: `SpeakEase.AI.Lib/Runtime/LegacyToolRegistryAdapter.cs`
- Modify: `SpeakEase.AI.Lib/ToolCapable.cs`
- Modify: `AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs`
- Test: `AINWZ.Tests/AI/ToolExposurePolicyTests.cs`

**Interfaces:**
- Consumes: 现有 `IToolCapable.Tools`、`ToolDefinition`、`IToolExecutionGuard`。
- Produces: `IToolRegistry.Get(string)`, `IToolRegistry.GetExposed(ToolExposureContext)`, `ToolExposurePolicy.Select(...)`。

- [ ] **Step 1: 写动态暴露失败测试**

```csharp
[Fact]
public void Select_CommitPhase_ExposesOnlyWriteTools()
{
    var selected = _policy.Select(new ToolExposureContext
    {
        AgentName = "write",
        Phase = "commit",
        AllowedGroups = new[] { "chapter.write", "character.write" }
    });

    Assert.Contains(selected, x => x.Function.Name == "save_chapter_content");
    Assert.DoesNotContain(selected, x => x.Function.Name == "web_search");
}
```

- [ ] **Step 2: 定义 Registry 合同**

`ToolCapabilityDescriptor` 必须包含 `Name`、`Group`、`RiskLevel`、`ReadOnly`、`RequiresExplicitConsent`、`RequiredScopes`、`RequiredPhases`。Registry 必须保留原始 `ToolDefinition`，不能重命名或修改参数 Schema。

- [ ] **Step 3: 实现 Registry 适配现有 Tool**

从 `IToolCapable.Tools` 构建全量 Registry；未声明元数据的旧 Tool 使用安全默认值：`Group="system.legacy"`、`RiskLevel="medium"`、`RequiredPhases=["generate"]`。`PowerShell`、`WebSearch`、浏览器和 Skill 查找工具默认标记为 `RequiresExplicitConsent=true`。

- [ ] **Step 4: 接入 AgentRuntime 请求**

在 `AgentLoopRequest` 中增加可选的 `ToolExposureContext`；当未提供时保持现有全量 Tool 行为，确保兼容模式不改变。

- [ ] **Step 5: 运行测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~ToolExposurePolicyTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~ToolContractTests --no-restore
git add SpeakEase.AI.Lib AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs AINWZ.Tests/AI/ToolExposurePolicyTests.cs
git commit -m "feat: add runtime tool exposure policy"
```

### Task 3: 将 Agent 收敛为 Descriptor，移除新 Agent 的长 Prompt 依赖

**Files:**
- Create: `SpeakEase.AI.Lib/Runtime/AgentDescriptor.cs`
- Create: `SpeakEase.AI.Lib/Runtime/IAgentDefinition.cs`
- Create: `SpeakEase.AI.Lib/Runtime/PromptProfileCatalog.cs`
- Create: `SpeakEase.AI.Lib/Runtime/PolicyProfileCatalog.cs`
- Create: `SpeakEase.AI.Lib/Runtime/PromptCompileRequest.cs`
- Create: `SpeakEase.AI.Lib/Runtime/PromptCompiler.cs`
- Create: `AINWZ.Infrastructure/AI/Agents/AgentDefinitionAdapter.cs`
- Modify: `SpeakEase.AI.Lib/Runtime/PromptComposer.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/AgentBase.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/WriteAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/CreationAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/OutlineAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/WorldAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/CritiqueAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/AuditAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/GeneralAgent.cs`
- Test: `AINWZ.Tests/AI/PromptCompilerTests.cs`

**Interfaces:**
- Consumes: `PromptProfile`、`AgentMetadata`、现有 `BuildPrompt()` 兼容接口。
- Produces: `IAgentDefinition.Descriptor`、`PromptCompiler.Compile(...)`。

- [ ] **Step 1: 写 Prompt 编译失败测试**

```csharp
[Fact]
public void Compile_UsesPlanAndPolicyWithoutEmbeddingWorkflowScript()
{
    var prompt = _compiler.Compile(new PromptCompileRequest
    {
        ProfileKey = "novel.writer",
        TaskObjective = "完成当前章节正文",
        AllowedCapabilities = new[] { "chapter.read", "chapter.write" },
        ContextSummary = "当前章节位于第二卷"
    });

    Assert.Contains("完成当前章节正文", prompt);
    Assert.DoesNotContain("必须先调用", prompt);
    Assert.DoesNotContain("Thought", prompt);
}
```

- [ ] **Step 2: 定义 Agent Descriptor**

新增 Agent 只实现 `IAgentDefinition`，声明 `Name`、`Domain`、`InputKinds`、`OutputKind`、`PromptProfileKey`、`PolicyProfileKey`、`ToolGroups`、`MemoryScopes`。旧 Agent 仍由 `AgentDefinitionAdapter` 从 `BuildPrompt()` 生成兼容 Profile。

- [ ] **Step 3: 把长 Prompt 拆成 Profile 数据**

先保持现有输出行为，将角色身份、质量标准、风格提示和输出契约移到 Profile Catalog；删除流程顺序、预算、持久化和 ReAct 指令。Tool 调度由 Task 2 的 Registry 提供。

- [ ] **Step 4: 让 AgentBase 使用 PromptCompiler**

`AgentBase.ExecuteStreamAsync` 只负责校验请求、构建 Descriptor/Prompt 请求并委托 AgentLoop；不再读取具体 Agent 的流程 Prompt。

- [ ] **Step 5: 运行 Prompt 与 AI 回归测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~PromptCompilerTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentApplicationTests --no-restore
git add SpeakEase.AI.Lib/Runtime AINWZ.Infrastructure/AI/Agents AINWZ.Tests/AI/PromptCompilerTests.cs
git commit -m "refactor: make agents descriptor driven"
```

### Task 4: 重构 AgentRuntime 为 Runner、RunContext 和 RuntimeHost

**Files:**
- Create: `SpeakEase.AI.Lib/Runtime/IAgentRuntimeRunner.cs`
- Create: `SpeakEase.AI.Lib/Runtime/AgentRuntimeRunner.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RuntimeRunRequest.cs`
- Create: `SpeakEase.AI.Lib/Runtime/AgentRuntimeOptions.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RunContext.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RuntimeHost.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RuntimeState.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RuntimeTransition.cs`
- Create: `SpeakEase.AI.Lib/Runtime/IStepScheduler.cs`
- Create: `SpeakEase.AI.Lib/Runtime/LinearStepScheduler.cs`
- Create: `SpeakEase.AI.Lib/Runtime/IRuntimeEventSink.cs`
- Create: `SpeakEase.AI.Lib/Runtime/RuntimeEvent.cs`
- Modify: `SpeakEase.AI.Lib/Runtime/AgentLoop.cs`
- Modify: `SpeakEase.AI.Lib/Runtime/AgentLoopRequest.cs`
- Test: `AINWZ.Tests/AI/RuntimeHostTests.cs`
- Test: `AINWZ.Tests/AI/AgentLoopTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `IToolRegistry`、Task 3 的 `IAgentDefinition`/`PromptCompiler`、现有 `IChatCompatible`。
- Produces: `IAgentRuntimeRunner.RunAsync(RuntimeRunRequest, CancellationToken)` 和统一 `RuntimeEvent` 流。

- [ ] **Step 1: 写状态转换失败测试**

覆盖 `Created → Running → ModelTurn → ToolExecuting → ModelTurn → Completed`、取消、超时、最大迭代、Tool 拒绝和 LLM 错误。

- [ ] **Step 2: 定义 RunContext**

```csharp
public sealed class RunContext
{
    public string RunId { get; init; }
    public string StepId { get; init; }
    public string UserId { get; init; }
    public string WorkId { get; init; }
    public string SessionId { get; init; }
    public AgentRuntimeOptions Options { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

RunContext 只保存运行元数据和依赖，不保存隐藏推理，不直接引用业务实体。

- [ ] **Step 3: 实现 RuntimeHost 状态机**

统一处理预算、超时、取消、Tool/Skill 调用、事件序列和最终 StopReason。`AgentLoop` 保留为兼容入口，内部委托 `RuntimeHost`。

- [ ] **Step 4: 实现 Runner 与线性 Scheduler**

Runner 创建 RunContext，Scheduler 执行现有线性 Pipeline；计划校验 Agent 存在、Step 唯一、依赖存在且无环。第一阶段不开放任意 DAG。

- [ ] **Step 5: 运行 Runtime 测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~RuntimeHostTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentLoopTests --no-restore
git add SpeakEase.AI.Lib/Runtime AINWZ.Tests/AI/RuntimeHostTests.cs AINWZ.Tests/AI/AgentLoopTests.cs
git commit -m "refactor: add agent runtime host"
```

### Task 5: 接入 Run/Step/Event/Checkpoint 持久化与 SSE 投影

**Files:**
- Create: `AINWZ.Application/Abstractions/AI/IAgentRuntimeStore.cs`
- Create: `AINWZ.Application/Abstractions/AI/AgentCheckpointDto.cs`
- Create: `AINWZ.Infrastructure/AI/Runtime/AgentRuntimeStore.cs`
- Create: `AINWZ.Infrastructure/AI/Runtime/AgentEventSseProjector.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/AI/AgentRuntimeEntityConfigurations.cs`
- Modify: `AINWZ.Infrastructure/Persistence/SpeakEaseDbContext.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Create: EF Core migration generated by `dotnet ef migrations add AddAgentRuntimeCheckpoints` under `AINWZ.Infrastructure/Migrations/`
- Test: `AINWZ.Tests/AI/AgentRuntimePersistenceTests.cs`
- Test: `AINWZ.Tests/AI/SseProjectionTests.cs`

**Interfaces:**
- Consumes: Task 4 的 `RuntimeEvent`、现有 `AgentRunEntity`、`AgentRunEventEntity`、`AgentToolCallEntity`、`AgentArtifactEntity`。
- Produces: `IAgentRuntimeStore.AppendEventAsync(...)`、`SaveCheckpointAsync(...)`、`LoadCheckpointAsync(...)`、`ReplayToolCallAsync(...)`。

- [ ] **Step 1: 写事件顺序和 Tool Replay 失败测试**

断言同一 Run 的 Sequence 单调递增；已完成且幂等键相同的 ToolCall 恢复时直接返回历史结果。

- [ ] **Step 2: 增加 Checkpoint 模型**

Checkpoint 至少保存 `RunId`、`StepId`、状态、消息游标、当前迭代、待处理 ToolCall、版本和更新时间。不得保存隐藏推理文本。

- [ ] **Step 3: 实现 Application Port 和 Infrastructure Store**

Runtime 只依赖 `IAgentRuntimeStore`；EF 实现通过 `IAgentRuntimeDbContext` 操作现有 Run 表和新增 Checkpoint 表。

- [ ] **Step 4: 实现 SSE Projector**

将内部 `RuntimeEvent` 投影为现有 `content`、`reasoning`、`tool_result`、`agent_start`、`done`、`error` 事件，新增 `runId`、`stepId`、`sequence` 只放在兼容扩展字段。

- [ ] **Step 5: 创建迁移并验证历史数据**

```powershell
dotnet ef migrations add AddAgentRuntimeCheckpoints --project AINWZ.Infrastructure --startup-project AINWZ
dotnet build AINWZ.slnx --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentRuntimePersistenceTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~SseProjectionTests --no-restore
```

- [ ] **Step 6: 提交持久化和投影**

```powershell
git add AINWZ.Application/Abstractions/AI AINWZ.Infrastructure/AI/Runtime AINWZ.Infrastructure/Persistence AINWZ.Infrastructure/Migrations AINWZ.Application/Applications/AgentApplication.cs AINWZ.Tests/AI
git commit -m "feat: persist agent runtime checkpoints"
```

### Task 6: 增加 CharacterStateEvent/Snapshot/GrowthProposal 实体

**Files:**
- Create: `AINWZ.Domain/Entities/Story/CharacterStateEventEntity.cs`
- Create: `AINWZ.Domain/Entities/Story/CharacterStateSnapshotEntity.cs`
- Create: `AINWZ.Domain/Entities/Story/CharacterGrowthProposalEntity.cs`
- Create: `AINWZ.Domain/Entities/Story/RelationshipStateEventEntity.cs`
- Create: `AINWZ.Application/Abstractions/Story/ICharacterStateStore.cs`
- Create: `AINWZ.Infrastructure/Persistence/Configurations/Story/CharacterStateEntityConfigurations.cs`
- Modify: `AINWZ.Infrastructure/Persistence/SpeakEaseDbContext.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/Story/CharacterEntityConfiguration.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/Story/CharacterRelationshipEntityConfiguration.cs`
- Create: EF Core migration generated by `dotnet ef migrations add AddCharacterRuntimeState` under `AINWZ.Infrastructure/Migrations/`
- Test: `AINWZ.Tests/Story/CharacterStateEntityTests.cs`

**Interfaces:**
- Consumes: 现有 `CharacterEntity`、`CharacterArcEntity`、`CharacterRelationshipEntity` 和 `AgentRunEntity`。
- Produces: `ICharacterStateStore.AppendEventAsync(...)`、`GetLatestSnapshotAsync(...)`、`SaveProposalAsync(...)`、`CompareAndSwapSnapshotAsync(...)`。

- [ ] **Step 1: 写版本和兼容初始化失败测试**

覆盖：没有 Snapshot 时由 `CharacterEntity` 初始化基线；旧版本 Snapshot 不能覆盖新版本；同一来源事件幂等。

- [ ] **Step 2: 定义实体字段**

`CharacterStateEventEntity` 包含 `WorkId`、`CharacterId`、`SourceRunId`、`SourceChapterId`、`EventType`、`EvidenceJson`、`ChangesJson`、`Confidence`、`Version`；Snapshot 包含 `StateJson`、`BasedOnEventId`、`Version`、`Status`；Proposal 包含 `ProposalJson`、`Severity`、`Status`、审核字段。

- [ ] **Step 3: 配置索引和并发约束**

为事件配置 `(WorkId, CharacterId, SourceRunId, EventType)` 幂等索引；为 Snapshot 配置 `(WorkId, CharacterId)` 当前唯一索引和并发 Token；为 Proposal 配置状态和来源 Run 索引。

- [ ] **Step 4: 创建追加式迁移**

```powershell
dotnet ef migrations add AddCharacterRuntimeState --project AINWZ.Infrastructure --startup-project AINWZ
```

不修改旧字段，不回填历史数据；首次读取时在 Application Store 中从旧实体生成基线快照。

- [ ] **Step 5: 运行测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CharacterStateEntityTests --no-restore
git add AINWZ.Domain/Entities/Story AINWZ.Application/Abstractions/Story/ICharacterStateStore.cs AINWZ.Infrastructure/Persistence AINWZ.Tests/Story
git commit -m "feat: add character state persistence"
```

### Task 7: 实现人物状态评估、一致性校验和剧情钩子

**Files:**
- Create: `AINWZ.Application/Abstractions/Story/CharacterStateChangeProposal.cs`
- Create: `AINWZ.Application/Abstractions/Story/ICharacterStateEvaluator.cs`
- Create: `AINWZ.Application/Abstractions/Story/IGrowthConsistencyValidator.cs`
- Create: `AINWZ.Application/Abstractions/Story/IPlotHookGenerator.cs`
- Create: `AINWZ.Application/Abstractions/Story/PlotHookProposal.cs`
- Create: `AINWZ.Infrastructure/AI/Character/CharacterStateEvaluator.cs`
- Create: `AINWZ.Infrastructure/AI/Character/GrowthConsistencyValidator.cs`
- Create: `AINWZ.Infrastructure/AI/Character/PlotHookGenerator.cs`
- Create: `AINWZ.Infrastructure/AI/Character/CharacterRuntimeWorker.cs`
- Modify: `AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs`
- Test: `AINWZ.Tests/AI/CharacterStateEvaluatorTests.cs`
- Test: `AINWZ.Tests/AI/GrowthConsistencyValidatorTests.cs`
- Test: `AINWZ.Tests/AI/PlotHookGeneratorTests.cs`

**Interfaces:**
- Consumes: Task 6 的 `ICharacterStateStore`、章节 Artifact、Timeline/Relationship 查询端口。
- Produces: 事件提案、自动提交或待确认 Proposal、`PlotHookProposal`。

- [ ] **Step 1: 写普通变化与重大变化测试**

```csharp
[Fact]
public async Task Evaluate_NormalEmotionChange_IsAutoApproved()
{
    var result = await _evaluator.EvaluateAsync(_chapterArtifact, CancellationToken.None);

    Assert.Equal("approved", result.Status);
}

[Fact]
public async Task Evaluate_CoreValueChange_RequiresReview()
{
    var result = await _evaluator.EvaluateAsync(_coreValueChangeArtifact, CancellationToken.None);

    Assert.Equal("needs_review", result.Status);
}
```

- [ ] **Step 2: 实现证据提取**

输入必须包含 `SourceRunId`、章节/Artifact 引用和证据片段；输出前后状态、变化维度、置信度和严重级别。没有证据的变化返回 `rejected`。

- [ ] **Step 3: 实现一致性校验**

校验核心身份、价值观、能力边界、时间线、关系状态和版本冲突。普通变化直接写 Event + Snapshot，重大变化写 GrowthProposal，不覆盖已确认 Snapshot。

- [ ] **Step 4: 实现剧情钩子生成**

根据未满足目标、内部冲突、关系张力和风险偏好生成候选钩子；钩子必须带来源 CharacterId、状态版本和置信度，交给 PlanCompiler 再决定是否执行。

- [ ] **Step 5: 接入后台 Worker**

章节完成后发布 `CharacterStateRefreshRequested`；Worker 失败不影响 Chat 成功，只记录重试和 `stale` 状态。

- [ ] **Step 6: 运行测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CharacterStateEvaluatorTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~GrowthConsistencyValidatorTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~PlotHookGeneratorTests --no-restore
git add AINWZ.Application/Abstractions/Story AINWZ.Infrastructure/AI/Character AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs AINWZ.Tests/AI
git commit -m "feat: add character self growth runtime"
```

### Task 8: 重构 Chat 入口、ContextAssembler 和 Memory Refresh

**Files:**
- Create: `AINWZ.Application/Abstractions/AI/AgentChatRuntimeRequest.cs`
- Create: `AINWZ.Application/Abstractions/Memory/IMemoryContextProvider.cs`
- Create: `AINWZ.Application/Abstractions/Memory/IMemoryRefreshQueue.cs`
- Create: `AINWZ.Infrastructure/AI/Context/LayeredContextAssembler.cs`
- Create: `AINWZ.Infrastructure/AI/Memory/MemoryRefreshWorker.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/Context/CreationAgentContext.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/Memory/MemorySnapshotEntityConfiguration.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/Memory/MemoryFactEntityConfiguration.cs`
- Test: `AINWZ.Tests/AI/AgentInputNormalizerTests.cs`
- Test: `AINWZ.Tests/AI/LayeredContextAssemblerTests.cs`
- Test: `AINWZ.Tests/AI/MemoryRefreshWorkerTests.cs`

**Interfaces:**
- Consumes: Task 4 的 Runner、Task 5 的 Runtime Store、现有 Memory Snapshot/Facts 和 InputNormalizer。
- Produces: Chat/StreamChat 共享的 `AgentChatRuntimeRequest`，完整轮次裁剪和异步记忆刷新。

- [ ] **Step 1: 写 Chat 去重和失败隔离测试**

断言同一 `IdempotencyKey` 不重复执行副作用 Tool；记忆刷新失败不使已完成 Chat 返回失败；非 `completed` Run 不写入成功对话轮次。

- [ ] **Step 2: 统一 Chat 两条路径**

抽取规范化、Session/Run 创建、Runtime 调用、事件持久化和最终结果处理；同步 Chat 消费同一事件流，SSE 只负责投影。

- [ ] **Step 3: 实现分层上下文**

按完整 user/assistant 轮次读取 L1；注入 L2 摘要、L3 事实、L4 检索；执行 `input + reserved output <= context window`，裁剪顺序为 L4 → L2 → L1。

- [ ] **Step 4: 解耦记忆刷新**

消息和 Run 事务提交后发布 `MemoryRefreshRequested`；Worker 按 `VersionTurn` 条件 Upsert Snapshot/Facts，失败标记 `stale` 并重试。

- [ ] **Step 5: 运行测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentInputNormalizerTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~LayeredContextAssemblerTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~MemoryRefreshWorkerTests --no-restore
git add AINWZ.Application AINWZ.Infrastructure/AI AINWZ.Infrastructure/Persistence AINWZ.Tests/AI
git commit -m "refactor: unify chat and layered memory runtime"
```

### Task 9: CreationRuntimeFacade、多 Agent Artifact 和兼容切换

**Files:**
- Create: `AINWZ.Application/Abstractions/AI/ICreationRuntimeFacade.cs`
- Create: `AINWZ.Application/Abstractions/AI/CreationRuntimeRequest.cs`
- Create: `AINWZ.Infrastructure/AI/Runtime/CreationRuntimeFacade.cs`
- Create: `AINWZ.Infrastructure/AI/Runtime/ArtifactContextBuilder.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/NovelAIServiceCollectionExtensions.cs`
- Modify: `AINWZ.Tests/AI/CreationOrchestratorPerformanceTests.cs`
- Create: `AINWZ.Tests/AI/CreationRuntimeFacadeTests.cs`

**Interfaces:**
- Consumes: Task 4 Runner、Task 5 Artifact Store、Task 8 Layered Context。
- Produces: 线性 Plan 到 Runtime 的结构化转换，Artifact 摘要/引用传递和 `AiRuntime:Mode` 双模式切换。

- [ ] **Step 1: 写兼容模式测试**

```csharp
[Theory]
[InlineData("legacy")]
[InlineData("agent-loop")]
public async Task ExecuteAsync_BothModesKeepSameExternalResult(string mode)
{
    var result = await _facade.ExecuteAsync(_request with { RuntimeMode = mode }, CancellationToken.None);

    Assert.Equal("completed", result.StopReason);
    Assert.NotNull(result.Content);
}
```

- [ ] **Step 2: 将 RouteResult 转为受约束 Plan**

校验 Agent、Step 唯一性、依赖存在性、无环、Tool 分组和 Step 数量；默认保持当前线性 Pipeline。

- [ ] **Step 3: 使用 Artifact 替代字符串拼接**

后续 Agent 默认接收前序 Artifact 摘要和引用，只有预算允许时加载正文；禁止将所有前序输出拼入 `UserMessage`。

- [ ] **Step 4: 将 CreationOrchestrator 收敛为兼容 Facade**

删除其中的循环、上下文拼接和 SSE 投影职责，保留旧接口并委托 `CreationRuntimeFacade`。

- [ ] **Step 5: 运行性能和兼容测试并提交**

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CreationRuntimeFacadeTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CreationOrchestratorPerformanceTests --no-restore
git add AINWZ.Application/Abstractions/AI AINWZ.Infrastructure/AI/Runtime AINWZ.Infrastructure/AI/Orchestrator AINWZ.Application/Applications/AgentApplication.cs AINWZ.Tests/AI
git commit -m "refactor: route creation through runtime facade"
```

### Task 10: 全量验证、灰度切换和文档收口

**Files:**
- Modify: `AINWZ/appsettings.json`
- Modify: `AINWZ/appsettings.Development.json`
- Modify: `docs/architecture/adr-004-agent-runtime-refactor.md`
- Modify: `docs/architecture/agent-runtime-reference-architecture.md`
- Create: `AINWZ.Tests/AI/AgentRuntimeEndToEndTests.cs`

- [ ] **Step 1: 增加配置开关**

默认配置保持兼容模式：

```json
{
  "AiRuntime": {
    "Mode": "legacy",
    "EnableDynamicToolExposure": false,
    "EnableCharacterSelfGrowth": false
  }
}
```

- [ ] **Step 2: 执行端到端兼容测试**

```powershell
dotnet build AINWZ.slnx --no-restore
dotnet test --no-restore
```

必须覆盖：直接回答、Tool Call、Skill Resolve、取消、超时、最大迭代、SSE 断开、重复提交、Tool Replay、记忆刷新失败、人物普通成长和重大成长待确认。

- [ ] **Step 3: 开发环境启用 agent-loop**

仅在开发/测试环境设置 `Mode=agent-loop`，对比旧模式的最终内容、Tool 调用数量、延迟、错误率和 SSE 序列。

- [ ] **Step 4: 更新架构状态**

将 ADR-004 的实现状态更新为已实现阶段，记录仍保留的兼容 Facade、旧 Tool 和旧字段。

- [ ] **Step 5: 最终提交**

```powershell
git add AINWZ/appsettings.json AINWZ/appsettings.Development.json docs/architecture AINWZ.Tests/AI/AgentRuntimeEndToEndTests.cs
git commit -m "chore: verify agent runtime migration"
```

## 计划自检

- API/SSE/Tool/Skill/数据库兼容：Task 1、5、9、10。
- Prompt-light Agent：Task 2、3。
- AgentRuntime 生命周期、取消、超时、幂等、checkpoint：Task 4、5。
- 人物自生长和实体演进：Task 6、7。
- Chat 输入、分层记忆、异步刷新：Task 8。
- 多 Agent Artifact 和兼容切换：Task 9。
- 全量构建、测试和灰度：Task 10。
- 文档中无 TODO、TBD 或未定义接口名称；所有新增类型在对应任务中给出路径和用途。
