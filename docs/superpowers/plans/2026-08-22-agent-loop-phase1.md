# AgentLoop Phase 1 Implementation Plan

> **For agentic workers:** Execute this plan task-by-task with test-first development. The existing workspace contains unrelated uncommitted changes; preserve them and do not reset or commit.

**Goal:** Introduce a shared AgentLoop execution kernel, pass Chat runtime options through the existing orchestration chain, and make terminal Run outcomes explicit without breaking Tool, Skill, or SSE compatibility.

**Architecture:** Add `SpeakEase.AI.Lib.Runtime.AgentLoop` as the shared loop implementation. Existing `AgentBase` delegates streaming execution to it, while `ReActAgent` remains a compatibility entry point until a later migration step. Add an `AgentRuntimeRequest` overload to the application abstraction so `SkillName` and `EnableAutoToolDispatch` reach the orchestrator. Keep the existing positional overload for current callers.

**Tech Stack:** .NET 10, C#, xUnit, existing `IChatCompatible`, `IToolCapable`, `AgentRequest`, `AgentStreamChunk`, and `IAgentOrchestrator` contracts.

---

### Task 1: Add AgentLoop contracts and behavior tests

**Files:**
- Create: `SpeakEase.AI.Lib/Runtime/AgentLoopRequest.cs`
- Create: `SpeakEase.AI.Lib/Runtime/AgentLoop.cs`
- Test: `AINWZ.Tests/AI/AgentLoopTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests covering three externally observable behaviors:

```csharp
[Fact]
public async Task RunAsync_ReturnsCompletedAfterDirectModelAnswer()
{
    // Fake IChatCompatible returns one done result without ToolCalls.
    // Assert one done chunk, StopReason == "completed", and final content.
}

[Fact]
public async Task RunAsync_ExecutesToolThenContinuesWithToolMessage()
{
    // Fake model returns ToolCall on first turn and final answer on second.
    // Assert IToolCapable was called once, the second model request contains a ToolMessage,
    // and the stream contains tool_result followed by done.
}

[Fact]
public async Task RunAsync_StopsWithMaxIterationsReached()
{
    // Fake model always returns a ToolCall.
    // Set MaxIterations = 1 and assert done.FinalResponse.StopReason is max_iterations_reached.
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentLoopTests --no-restore
```

Expected: compilation/test failure because `AgentLoop` and its request type do not exist yet.

- [ ] **Step 3: Define the minimal request contract**

`AgentLoopRequest` must contain `AgentRequest Request`, `string AgentName`, `IChatCompatible Llm`, and `IToolCapable Tools`, with null-safe defaults for request history. The loop must not reference EF Core, Application, Infrastructure, or business entities.

- [ ] **Step 4: Implement the minimal loop**

`AgentLoop.RunAsync` must:

1. Build `SystemPrompt → ConversationHistory → UserMessage` messages.
2. Call `IChatCompatible.StreamAsync` once per iteration.
3. Forward `content`, `reasoning`, and `tool_call` chunks unchanged as `AgentStreamChunk`.
4. On a failed turn, emit `error` then `done` with `StopReason = "llm_error"`.
5. On ToolCalls, append an Assistant message, execute each Tool, emit `tool_result`, and append a ToolMessage.
6. On a final answer, append an Assistant message and emit `done` with accumulated usage and `StopReason = "completed"`.
7. When the iteration budget is exhausted, emit `done` with `StopReason = "max_iterations_reached"`.
8. Respect cancellation and avoid swallowing `OperationCanceledException`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the same focused test command and require all `AgentLoopTests` to pass.

### Task 2: Migrate AgentBase to AgentLoop

**Files:**
- Modify: `AINWZ.Infrastructure/AI/Agents/AgentBase.cs`
- Test: `AINWZ.Tests/AI/AgentApplicationTests.cs`

- [ ] **Step 1: Add a regression assertion**

Extend the existing streaming-agent test to assert the final `AgentResponse.StopReason` is preserved when an Agent emits `max_iterations_reached`; this must fail against the current `AgentApplication`, which currently treats a stream without an `error` as success.

- [ ] **Step 2: Run the regression test and verify RED**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentApplicationTests --no-restore
```

- [ ] **Step 3: Delegate AgentBase execution to AgentLoop**

Inject or instantiate `AgentLoop` using the existing `IChatCompatible`, `IToolCapable`, and `ILogger`. Preserve `RegisterTools`, Agent metadata, validation, and all existing chunk types. `AgentBase.ExecuteStreamAsync` should only construct the loop request and yield the loop chunks.

- [ ] **Step 4: Make AgentApplication inspect the final stop reason**

Track `chunk.FinalResponse` in both `ChatAsync` and `StreamChatAsync`. Treat `llm_error`, `max_iterations_reached`, `cancelled`, `timed_out`, and `invalid_request` as non-success outcomes; do not append a successful conversation turn for them. Preserve existing error chunk text and add a deterministic business error for a terminal non-success without an error chunk.

- [ ] **Step 5: Run focused and existing AI tests**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentApplicationTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CreationOrchestratorPerformanceTests --no-restore
```

### Task 3: Thread runtime options through Chat and orchestrator

**Files:**
- Create: `AINWZ.Application/Abstractions/AI/AgentRuntimeRequest.cs`
- Modify: `AINWZ.Application/Abstractions/AI/IAgentOrchestrator.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Modify: `AINWZ.Tests/AI/AgentApplicationTests.cs`

- [ ] **Step 1: Add a failing propagation test**

Use a capturing orchestrator in a focused application test and assert that `SkillName` and `EnableAutoToolDispatch` from `AgentChatRequestDto` are present in the runtime request. The test must fail before the new overload exists.

- [ ] **Step 2: Define `AgentRuntimeRequest`**

The request must include `WorkId`, `SessionId`, `UserMessage`, `SkillName`, `MaxIterations`, `MaxTokens`, `Temperature`, and `EnableAutoToolDispatch`. Keep the existing `IAgentOrchestrator.ExecuteAsync(workId, sessionId, userMessage, ...)` method for compatibility.

- [ ] **Step 3: Implement the overload and pass fields**

Add `ExecuteAsync(AgentRuntimeRequest request, CancellationToken)` to the interface and `CreationOrchestrator`. Update both Chat paths in `AgentApplication` to call it. `CreationOrchestrator` must set `AgentRequest.SkillName` and use `EnableAutoToolDispatch` to decide whether Tool definitions are exposed/executed for that run; default behavior remains enabled.

- [ ] **Step 4: Run propagation and existing orchestration tests**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentApplicationTests --no-restore
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~CreationOrchestratorPerformanceTests --no-restore
```

### Task 4: Add the Run outcome contract without persistence migration

**Files:**
- Create: `AINWZ.Application/Abstractions/AI/AgentRunStatus.cs`
- Create: `AINWZ.Application/Abstractions/AI/AgentRunResult.cs`
- Modify: `AINWZ.Application/Applications/AgentApplication.cs`
- Test: `AINWZ.Tests/AI/AgentRunOutcomeTests.cs`

- [ ] **Step 1: Write failing outcome tests**

Cover `completed`, `max_iterations_reached`, and cancellation. Assert that only `completed` reaches `AppendTurnAsync`.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AgentRunOutcomeTests --no-restore
```

- [ ] **Step 3: Implement status mapping**

Map the final `AgentResponse.StopReason` to `AgentRunStatus`, keep the mapping internal to Application for now, and expose the status through the returned `AgentResponse` without changing the public endpoint shape. Do not add database tables in this phase.

- [ ] **Step 4: Run all AI tests**

Run:

```powershell
dotnet test AINWZ.Tests/AINWZ.Tests.csproj --filter FullyQualifiedName~AI --no-restore
```

### Task 5: Verification and handoff

**Files:**
- Modify: `docs/architecture/adr-004-agent-runtime-refactor.md` only if implementation details materially differ from the approved design.

- [ ] **Step 1: Build the solution**

Run:

```powershell
dotnet build AINWZ.slnx --no-restore
```

- [ ] **Step 2: Run the complete test suite**

Run:

```powershell
dotnet test --no-restore
```

- [ ] **Step 3: Review the diff**

Confirm no unrelated files were reverted, no Tool/Skill schema changed, and no commit was created.

