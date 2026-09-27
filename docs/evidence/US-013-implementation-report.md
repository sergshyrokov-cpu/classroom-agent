---
artifact_type: implementation_report
story: US-013
version: 2
status: APPROVED
created_at: 2026-09-27T15:41:09Z
updated_at: 2026-09-27T16:00:25Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-013-background-sync-service.md
    version: null
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/designs/api/US-013-api-design.md
    version: 1
  - path: docs/designs/database/US-013-db-design.md
    version: 1
  - path: docs/designs/database/US-013-entity-model.md
    version: 1
  - path: docs/tests/US-013-test-strategy.md
    version: 1
  - path: docs/tests/US-013-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-013-test-generation-report.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 2
  - path: docs/reviews/security/US-013-security-review.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 2
---

# US-013 Implementation Report — Background synchronization service

## 0. Attempt 2 — the security review's findings

`SECURITY_REVIEW` v1 returned CHANGES_REQUIRED with one Major and one Minor
finding. Both are fixed; nothing else changed.

**F-1 (Major, SC-7) — fixed.** `services.AddSingleton(settings)` registered the
whole `InstallationSettings` record so the background service could read one
`TimeSpan`; that record carries the resolved OAuth client secret and the database
connection string, so both became injectable anywhere in the Web host. A new narrow
record `Web/Configuration/SyncScheduleSettings.cs` — `record
SyncScheduleSettings(TimeSpan Interval)` — is registered instead, built in
`AddInstallation` exactly as `SchoolDefaults` and `GoogleServiceAccountSettings`
are, and `SynchronizationBackgroundService` takes it in place of
`InstallationSettings`. `git grep "AddSingleton(settings)"` now finds **nothing**,
and the record is again unreachable from the container.
`SyncIntervalConfigurationTests` resolves `SyncScheduleSettings` instead; it still
asserts the effective interval, so no assertion was weakened.

**F-2 (Minor, CONFIGURATION) — fixed in the same pass.** The default and the bounds
are now named constants beside the key —
`InstallationSettingsReader.DefaultSyncInterval`, `MinimumSyncIntervalMinutes`,
`MaximumSyncIntervalMinutes` — so the value DC-3 documents and the value the code
applies are one declaration.

Two files created or changed beyond those: none. Re-validation:
build 0 errors / 0 warnings, `dotnet test` **2253 passed, 0 failed, 0 skipped**,
`dotnet format --verify-no-changes` exit 0.

The review's four Informational observations (a tolerated stale `Running` row, a
coarse failure diagnosis, the `Guid` run identifier, the 1440-minute ceiling) need
no correction and were left as they are.

## 1. Summary

The installation now runs synchronization on its own schedule: a
`BackgroundService` starts one run at a time, records it in the single
`sync_state` row, refuses in read-only mode and without a usable connection
without writing anything, survives its own failures, logs four events carrying
the run identifier, stops cleanly with the host, and makes readiness truthful
about whether the service runs.

**It imports nothing**: the pipeline is empty by OD-001, so every run completes
with the counter at zero — the expected outcome (spec I-1). US-014 adds the first
step.

All nine Acceptance Criteria are implemented and covered. Build: 0 errors, 0
warnings. All 86 US-013 tests pass. Four existing tests were updated, each
keeping its own assertion (§7).

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-013-background-sync-service.md` | — |
| Specification | `docs/specifications/US-013-spec.md` | 1, APPROVED |
| Open Decisions | `docs/decisions/US-013-open-decisions.md` | 2 |
| API design | `docs/designs/api/US-013-api-design.md` | 1, NOT_APPLICABLE |
| Database design | `docs/designs/database/US-013-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-013-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-013-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-013-ac-test-matrix.md` | 1 |
| Test generation report | `docs/evidence/US-013-test-generation-report.md` | 1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 service hosted, readiness knows | `InstallationServices.AddInstallation` (`AddHostedService<SynchronizationBackgroundService>`), `SynchronizationServiceMemory`, `GetReadinessQuery` | `SyncReadinessTests` (9) | PASS |
| AC-002 schedule, one run at a time | `SynchronizationBackgroundService.ExecuteAsync` / `WaitForNextRunAsync`, `SyncRunCoordinator` | `SyncScheduleTests` (11), `SyncRunCoordinationTests` (8) | PASS |
| AC-003 `SyncState` records the run | `SyncState`, `SyncStateConfiguration`, `SyncStateRepository`, `RunSynchronizationUseCase.BeginAsync`, migration `AddSyncState` | `SynchronizationRunTests` (6), `SyncStateInvariantTests` (12), `SyncStateSchemaTests` (11), `AppUserMigrationTests` | PASS |
| AC-004 read-only: nothing runs, called, written | `RunSynchronizationUseCase.ExecuteAsync` step 1 (`IReadOnlyModeGuard` first) | `SynchronizationRefusalTests` (5 of 8), `SyncScheduleTests.InReadOnlyMode_…` | PASS |
| AC-005 no connection, nothing to synchronize | `RunSynchronizationUseCase.ExecuteAsync` step 2 (`GetWorkspaceConnectionQuery`) | `SynchronizationRefusalTests` (3 of 8), `SyncLoggingTests.TheTwoSkipReasons_AreDifferent` | PASS |
| AC-006 a failed run is recorded, service survives | `SyncState.FailRun`, `RunSynchronizationUseCase` catch, `SynchronizationBackgroundService.RunOnceAsync` catch | `SynchronizationRunTests`, `SyncStateInvariantTests`, `SyncStateSchemaTests` | PASS |
| AC-007 traceable lines, no personal data | `SynchronizationBackgroundService` log methods + `BeginScope` with `RunId` | `SyncLoggingTests` (9) | PASS |
| AC-008 clean shutdown | `ExecuteAsync` catch of `OperationCanceledException`, `finally` marking stopped | `SyncScheduleTests.HostStop_…` (2) | PASS |
| AC-009 tests never reach Google, never sleep | no Google port is involved (empty pipeline); every host test drives `ManualTimeProvider` | the whole suite; `GoogleDataPortRuleTests` still green | PASS |

## 4. Change Set

### Created — production

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Entities/SyncState.cs` | entity model §1; spec FR-001, FR-002, FR-008 |
| `src/ClassroomAgent.Domain/Enums/SyncRunStatus.cs` | entity model §2; spec FR-002 |
| `src/ClassroomAgent.Application/Ports/ISyncStateRepository.cs` | entity model §3; spec FR-006 |
| `src/ClassroomAgent.Application/Models/SynchronizationRunOutcome.cs` | entity model §4; spec FR-005 |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | spec FR-005, FR-006, FR-007, FR-008, FR-009 |
| `src/ClassroomAgent.Application/UseCases/SynchronizationServiceMemory.cs` | spec FR-014 (readiness marker) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SyncStateConfiguration.cs` | db-design §3.1, §3.2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/SyncStateRepository.cs` | entity model §3; AD-7 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260927143648_AddSyncState.cs` (+ `.Designer.cs`) | db-design §5; PC-2 |
| `src/ClassroomAgent.Web/BackgroundServices/SyncRunCoordinator.cs` | spec FR-004; OD-007 |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | spec FR-003, FR-010, FR-011, FR-012; AD-5 |

### Modified — production

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/UseCases/GetReadinessQuery.cs` | spec FR-014; DC-11. One more `Unhealthy` condition; read-only keeps returning `Degraded` |
| `src/ClassroomAgent.Application/UseCases/LegitimacyCheckMemory.cs` | spec FR-011, I-5 — a `Changed` signal the synchronization service waits on (§7, deviation D-1) |
| `src/ClassroomAgent.Infrastructure/Persistence/ClassroomAgentDbContext.cs` | db-design §5 — `DbSet<SyncState>` and the configuration |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | generated by `dotnet ef migrations add` |
| `src/ClassroomAgent.Web/Configuration/InstallationSettings.cs` | spec FR-013 — the run interval |
| `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` | spec FR-013, VR-001 — the key, the default and the bounds |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | spec FR-018 — DI registration of the service, the coordinator, the marker, the repository, the use case and the narrow schedule record (§0, F-1) |
| `src/ClassroomAgent.Web/Configuration/SyncScheduleSettings.cs` **(created, attempt 2)** | security review F-1 — the narrow settings record the schedule needs |

### Created — tests

`SyncTestData.cs`, `SyncWorld.cs`, `SyncHostExtensions.cs` (fixtures);
`SynchronizationRunTests.cs`, `SynchronizationRefusalTests.cs`,
`SyncStateInvariantTests.cs`, `SyncScheduleTests.cs`,
`SyncRunCoordinationTests.cs`, `SyncLoggingTests.cs`, `SyncReadinessTests.cs`,
`SyncStateSchemaTests.cs`, `SyncIntervalConfigurationTests.cs` — all traced to the
`ac_test_matrix`.

### Modified — tests

Four existing tests, each keeping its own assertion; only sample data grew (§7).

### Modified — documentation

`docs/architecture/deployment-conventions.md` — DC-3 lists the new optional
setting (spec FR-013 requires it).

### Workflow artifacts

`docs/decisions/US-013-open-decisions.md` (v2, OD-008),
`docs/tests/*`, `docs/evidence/*`, `docs/workflow/workflow-state.yaml`,
`docs/workflow/history.jsonl` — the closed list of the Git Policy.

No secret, generated database file or IDE-local configuration is in the change
set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | **0 errors, 0 warnings** |
| US-013 tests | `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~Sync"` | **86 total, 86 passed, 0 failed** |
| Whole suite | `dotnet test ClassroomAgent.sln` | see below |
| Format | `dotnet format --verify-no-changes` | **clean, no changes** |
| Migration | `dotnet ef migrations add AddSyncState …` | created; `Up` creates the table with the six check constraints and the unique index, `Down` drops it |

Whole-suite progression, as the fixes landed: 2253 total / 3 failed (three
existing table-set guards, §7) → after updating those three guards the suite is
**2253 total, 2253 passed, 0 failed, 0 skipped**.

Intermediate evidence kept deliberately, because it shows the red phase was real:
before implementation the same 84 tests were 83 red and 1 green
(test-generation report §4); after the persistence and use-case work 54 passed;
after the two fixture corrections 85 of 86; after the last fixture correction
86 of 86.

## 6. Configuration Changes

One new **optional** installation setting, required by spec FR-013:

- key `Sync:IntervalMinutes`, whole minutes, **1 … 1440**, default **60**;
- absent or blank → the default; present and invalid → the host refuses to start
  with `InstallationSettingException`, and the rejected value is not in the
  message (spec VR-001, SC-10);
- `InstallationSettings` gained `TimeSpan SyncInterval`, and the record is now
  registered in the container so the background service receives it;
- `deployment-conventions.md` DC-3 lists it.

No secret, no connection string and no school-specific value was added.

## 7. Deviations and Discovered Problems

**D-1 (design-relevant): `LegitimacyCheckMemory` gained a `Changed` signal.**
Spec FR-011 makes the first run wait for the first legitimacy determination. The
first implementation polled `LastOutcome` on the injected clock — and deadlocked
under test, because the clock only moves when a test moves it, and nothing moves
it while the installation is merely waiting to learn whether it is legitimate.
The memory now signals the way `PushCheckCoordinator` and `SyncRunCoordinator` do,
and the service awaits that signal with the one-interval bound of spec I-5. This
touches a type US-005 owns; it adds a member and changes no behaviour of the
legitimacy check.

**D-2: three existing table-set guards now expect `sync_state`.**
`DeanAccountSchemaTests.TheStory_AddsNoTableAndNoIndex`,
`AccessCheckAuditSchemaTests.NoTableIsAdded` and
`AdminLoginCheckEveryTimeTests.NoInstallationTable_HoldsACopyOfAllowedAdmin` each
compare the installation's tables against a literal set. Each kept its own
assertion — "this Story adds no table", "no table holds a copy of AllowedAdmin" —
and only the sample set grew by the table US-013 adds. A comment on each says so.
This is the US-012 D-6 pattern and was foreseen by db-design §8 for
`AppUserMigrationTests`, which TEST_WRITING had already updated.

**D-3 (test-fixture): two corrections to fixtures TEST_WRITING wrote.**
(a) `SyncWorld` treated the school's domain as unknown when no connection was
seeded, so "no connection" read as `DomainUnknown` instead of `NotConfigured`; the
domain is known because a legitimacy check succeeded, which is independent of
whether a connection is saved. (b) `SyncHostExtensions` left the Control Plane
unanswered for a read-only cause, so the legitimacy check never completed and the
first run never started; it now answers with a **failure**, which leaves the seeded
row and its last successful check untouched — the mode survives and the check
completes. No assertion was removed by either correction.

**D-4: two tests of `SyncRunCoordinationTests` were rewritten as unit tests.**
They resolved the coordinator from the running host and asserted that they could
start a run — while the service was competing for the same instance, which made
them race production code instead of proving it. They now assert the guarantee on
a coordinator of their own, and two host-level tests keep the integrated
guarantee: the coordinator is a singleton, and the row never holds two runs.
Three further unit tests were added (many requests leave one run, the state change
signals a waiter), so coverage grew rather than shrank.

**D-5: `TheIntervalCountsFromCompletion` was restated, not removed.** With an
empty pipeline a run takes no virtual time, so "counted from completion" and
"counted from start" produce the same instant and the original assertion
(`DoesNotContain(start + interval)`) could never hold. The test now asserts the
timer sits at completion plus the interval — the reading the code implements — and
its comment records that the distinction becomes observable only in US-014, whose
step takes time.

**Cleaned up as required:** every `#pragma warning disable CS9113` of the OD-008
skeleton is gone — the constructor parameters are all read now. `git grep CS9113`
finds nothing.

## 8. Open Decisions

No new Open Decision. OD-001 … OD-008 arrived resolved and were followed:

- OD-001 — the pipeline is empty; the counter is zero and that is success;
- OD-002 — the interval is the new optional setting;
- OD-003 — one interval after a success and after a failure alike;
- OD-004 — one row, updated in place;
- OD-005 — a skip is a log line only; **no** `SyncState` write, and
  `PermittedServiceWrites` did not grow, because the use case calls the guard
  first (spec FR-006). `ReadOnlyEnforcementTests` and `PermittedServiceWriteTests`
  are still green without a new declaration;
- OD-006 — the first run waits for the first legitimacy determination (see D-1);
- OD-007 — the coordinator is built here; no endpoint was added;
- OD-008 — the skeleton is fully replaced; no `NotImplementedException` remains in
  the US-013 surface.

## 9. Final validation

Recorded after the last correction:

```
dotnet build ClassroomAgent.sln      → Предупреждений: 0   Ошибок: 0
dotnet format --verify-no-changes    → exit code 0, clean
dotnet test ClassroomAgent.sln       → итог: 2253   сбой: 0   успешно: 2253   пропущено: 0
```

Every test in the solution passes, none is skipped, and the US-013 subset is 86 of
86. Two further checks were run because the Story's own report promised them:
`git grep CS9113 -- src` finds **0** occurrences (every OD-008 pragma is gone) and
no `NotImplementedException` remains anywhere in the synchronization surface.
