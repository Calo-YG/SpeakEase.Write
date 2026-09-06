# Core Performance and Reuse Improvements Implementation Plan

> **For agentic workers:** Execute this plan inline with TDD checkpoints. Each production change must have a focused regression test first.

**Goal:** Reduce avoidable latency, database round-trips, cache races, and repeated implementation in the core AI and work-query paths.

**Architecture:** Keep the existing Application/Infrastructure boundaries. Apply low-risk optimizations in place: add query indexes, aggregate read queries, make cache invalidation serialized, reuse the existing scoped LLM context for routing, and replace quadratic string accumulation. Defer asynchronous logging, distributed locks, snapshot queues, and a full AgentBase/ReActAgent merge to a later phase.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core 10/Npgsql, xUnit, Moq, in-memory test doubles.

---

### Task 1: Protect cache refresh and remove stale per-key locks

**Files:**
- Modify: `AINWZ.Infrastructure/MutilCache/MultiCacheService.cs`
- Test: `AINWZ.Tests/Infrastructure/MultiCacheServiceTests.cs`

- [x] Add tests showing concurrent `RemoveAsync` cannot race with a refill and that completed per-key locks are removed from the registry.
- [x] Run the focused tests and confirm the current implementation fails.
- [x] Serialize `GetOrSetAsync`, `RefreshAsync`, and `RemoveAsync` through one per-key gate; remove the gate from the static registry when no waiter remains.
- [x] Remove the unreachable post-eviction memory-cache check in `RefreshAsync`.
- [x] Run the focused tests and the full test suite.

### Task 2: Optimize work list and tool aggregate queries

**Files:**
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/Works/WorkEntityConfiguration.cs`
- Modify: `AINWZ.Application/Applications/WorkApplication.cs`
- Modify: `AINWZ.Infrastructure/AI/Tools/GetWorkInfoTool.cs`
- Test: `AINWZ.Tests/Performance/HotQueryTests.cs`

- [x] Add a test asserting the work model exposes a `(UserId, UpdateAt)` index.
- [x] Add a test for work-info counts using one aggregate query seam rather than three independent counts.
- [x] Add the composite work-owner/update index.
- [x] Collapse work-info chapter, volume, and character counts into one database projection.
- [x] Reduce `QueryWorksAsync` to a projected page query plus grouped counts, preserving ordering and response shape.
- [x] Run focused and full tests.

### Task 3: Remove avoidable AI-path allocation and duplicate context resolution

**Files:**
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationRouter.cs`
- Modify: `AINWZ.Infrastructure/AI/Context/CreationAgentContext.cs`
- Test: `AINWZ.Tests/AI/CreationOrchestratorPerformanceTests.cs`

- [x] Add a regression test proving long pipeline output preserves content while using bounded accumulation behavior.
- [x] Replace `previousResult += chunk.Content` with `StringBuilder` and cap the chained result before sending it to the next Agent.
- [x] Pass the request-scoped `IOpenAIContext` and `IChatCompatible` into routing instead of creating a second DI scope.
- [x] Avoid repeating session ownership work when the orchestrator already received a validated active session; retain a defensive check at the application boundary.
- [x] Run focused and full tests.

### Task 4: Verify the handoff

- [x] Run `dotnet build SpeakEase.Write.slnx --no-restore`.
- [x] Run `dotnet test --no-restore`.
- [x] Run `git diff --check` and inspect the changed-file list.
- [x] Report deferred Phase 2 items: async log batching, memory snapshot upsert/retention, autosave coalescing, export streaming, and AgentBase/ReActAgent unification.
