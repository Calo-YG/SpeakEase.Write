# Layered Prompt Composer Implementation Plan

> **For agentic workers:** Execute this plan task-by-task with tests before production changes.

**Goal:** Introduce a layered prompt composition boundary while preserving current Agent, Tool, Skill and SSE compatibility.

**Architecture:** `PromptProfile` contains only agent guidance. `PromptComposer` renders ordered sections. Runtime policies remain in AgentLoop and the legacy `BuildPrompt()` API remains as a fallback during migration.

**Tech Stack:** .NET 10, C#, xUnit, existing ASP.NET Core solution.

---

### Task 1: Add Prompt Composer contracts

**Files:**
- Create: `SpeakEase.AI.Lib/Runtime/PromptProfile.cs`
- Create: `SpeakEase.AI.Lib/Runtime/PromptComposer.cs`
- Test: `AINWZ.Tests/AI/PromptComposerTests.cs`

- [ ] Write tests for ordered sections and omission of empty sections.
- [ ] Run the focused tests and confirm the missing-type failure.
- [ ] Implement the minimal profile/context/composer types.
- [ ] Run focused tests again.

### Task 2: Add compatibility profile to Agents

**Files:**
- Modify: `AINWZ.Infrastructure/AI/Contract/INovelAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/AgentBase.cs`
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`

- [ ] Add a default `BuildPromptProfile()` so existing implementations compile unchanged.
- [ ] Make `AgentBase` compose a profile when no explicit system prompt is supplied.
- [ ] Make the orchestrator use the profile composer at its Agent step boundary.
- [ ] Run existing AgentLoop and orchestrator tests.

### Task 3: Migrate professional Agent profiles

**Files:**
- Modify: `AINWZ.Infrastructure/AI/Agents/GeneralAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/WriteAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/WorldAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/OutlineAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/CreationAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/CritiqueAgent.cs`
- Modify: `AINWZ.Infrastructure/AI/Agents/AuditAgent.cs`

- [ ] Add concise identity/objective/quality/output profiles without runtime workflow instructions.
- [ ] Keep existing `BuildPrompt()` methods as compatibility fallback until the next migration slice.
- [ ] Run the complete test suite.

### Task 4: Verify and hand off

- [ ] Run `dotnet test --no-restore`.
- [ ] Run `dotnet build SpeakEase.Write.slnx --no-restore`.
- [ ] Review `git diff` and ensure only scoped files changed.
- [ ] Commit with `refactor: add layered prompt composer`.
- [ ] Push the current branch and report the remote result.
