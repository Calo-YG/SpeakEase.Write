# Overall Review Remediation Implementation Plan

> **For agentic workers:** Execute the steps in order and verify each checkpoint before moving on.

**Goal:** Restore a buildable solution and remove the highest-risk tenant-isolation, transaction, concurrency, and error-disclosure defects identified in the overall review.

**Architecture:** Application references Domain and Application abstractions; Infrastructure implements those abstractions. Ownership is checked through a single Application port instead of letting AI tools trust model-supplied identifiers. Database operations that form one business action share one transaction.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core 10, PostgreSQL/Npgsql, xUnit, Moq, nullable disabled.

---

### Task 1: Restore the compile baseline

**Files:**
- Modify: `AINWZ.Application/SpeakEase.Write.Application.csproj`
- Test: solution build

- [x] Add the `AINWZ.Domain` project reference.
- [x] Run `dotnet build SpeakEase.Write.slnx --no-restore` and confirm the missing `SpeakEase.Write.Domain` errors are gone.

### Task 2: Add a reusable ownership port and regression tests

**Files:**
- Create: `AINWZ.Application/Abstractions/Authorization/IWorkAccessChecker.cs`
- Create: `AINWZ.Infrastructure/Authorization/WorkAccessChecker.cs`
- Modify: `AINWZ.Infrastructure/Authorization/AuthorizationExtensions.cs`
- Test: `AINWZ.Tests/Security/WorkAccessCheckerTests.cs`

- [x] Define `Task<bool> OwnsWorkAsync(string workId, string userId, CancellationToken cancellationToken = default)`.
- [x] Implement it with an `AsNoTracking` `Works` query.
- [x] Register it as scoped.
- [x] Write regression tests for owner and non-owner cases and verify them.

### Task 3: Enforce tenant isolation at application and AI boundaries

**Files:**
- Modify: `AINWZ.Application/Novel/Export/ExportService.cs`
- Modify: `AINWZ.Application/Applications/ChapterVersionManager.cs`
- Modify: `AINWZ/MapRoute/Works/VersionRoute.cs`
- Modify: representative AI tools under `AINWZ.Infrastructure/AI/Tools`
- Test: `AINWZ.Tests/Security/TenantIsolationTests.cs`

- [x] Inject current-user ownership checking into export and version-list flows.
- [x] Pass `workId` through version-list calls and validate `workId + chapterId` ownership.
- [x] Add a scoped `IToolExecutionGuard` to AI tool execution so tools cannot use arbitrary model-supplied work IDs.
- [x] Make tool calls reject a work ID not owned by the current user.
- [x] Add regression tests for export, version listing, ownership checking, and tool authorization.

### Task 4: Make adoption and work deletion atomic and complete

**Files:**
- Modify: `AINWZ.Application/Applications/AdoptionManager.cs`
- Modify: `AINWZ.Application/Applications/WorkApplication.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/AI/AICreationMessageEntityConfiguration.cs`
- Test: `AINWZ.Tests/AI/AdoptionConsistencyTests.cs`

- [x] Move version creation into the adoption transaction and fail fast when version creation fails.
- [x] Delete `AICreationMessages` before sessions.
- [x] Add a regression test proving a failed version creation does not modify the chapter.

### Task 5: Add session concurrency protection

**Files:**
- Modify: `AINWZ.Application/Applications/CreationSessionManager.cs`
- Modify: `AINWZ.Infrastructure/Persistence/Configurations/AI/AICreationSessionEntityConfiguration.cs`
- Create or modify: session concurrency tests under `AINWZ.Tests/AI`

- [x] Enforce one active session per work with a PostgreSQL filtered unique index and transactional start.
- [x] Configure PostgreSQL `xmin` as the session optimistic concurrency token and return 409 on conflicts.
- [ ] Add provider-backed tests for concurrent session start and concurrent turn append.

### Task 6: Harden error and time handling

**Files:**
- Modify: `AINWZ.Infrastructure/AI/Orchestrator/CreationOrchestrator.cs`
- Modify: `AINWZ.Infrastructure/Authorization/AuthorizationExtensions.cs`
- Modify: `AINWZ.Infrastructure/Authorization/TokenManager.cs`
- Test: `AINWZ.Tests/Security/ErrorSanitizationTests.cs`

- [x] Return a generic SSE/tool error payload and keep exception details out of the client response.
- [x] Decouple `ClockSkew` from `ExpMinutes` and use a fixed five-minute skew.
- [x] Replace JWT issue/expiry calculations with UTC-based values where touched.

### Task 7: Verify and report

- [x] Run `dotnet build SpeakEase.Write.slnx --no-restore`.
- [x] Run `dotnet test --no-restore` (21 passed).
- [x] Run `dotnet list SpeakEase.Write.slnx package --vulnerable --include-transitive`.
- [x] Inspect `git diff` and preserve unrelated user changes.
