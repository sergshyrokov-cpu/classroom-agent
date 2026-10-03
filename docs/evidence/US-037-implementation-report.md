---
artifact_type: implementation_report
story: US-037
version: 2
status: DRAFT
created_at: 2026-10-03T20:05:23Z
updated_at: 2026-10-03T20:36:10Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-037-retention-purge.md
    version: null
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 4
  - path: docs/designs/api/US-037-api-design.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-037-entity-model.md
    version: 1
  - path: docs/tests/US-037-test-strategy.md
    version: 1
  - path: docs/tests/US-037-ac-test-matrix.md
    version: 1
  - path: docs/reviews/security/US-037-security-review.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 2
---

# US-037 Implementation Report — Retention purge

## 1. Summary

The installation now runs the retention purge once shortly after start and
every 24 hours thereafter. Each run deletes:

- expired courses, with their coursework, submissions and memberships;
- leavers whose N years have run out, with their submissions in that course;
- orphaned participants;
- unused Admin and Dean accounts;
- audit rows older than N.

Each run writes one `system` audit event carrying the five counts.

Each unit is its own transaction. A failed unit rolls back, is logged at `Error`
by internal id, and is retried by the next run. The purge runs in read-only mode
(BR-026, BR-075), calls no Google port, and never overlaps a synchronization
run. Synchronization's age rule now shares the single `RetentionRule`.

Validation:

- build: 0 warnings, 0 errors;
- tests: **2780 / 2780 passed**, 0 skipped;
- `dotnet format --verify-no-changes`: clean.

**Security-sensitive:** the Story deletes personal data and accounts, adds a
write path that is permitted in read-only mode, and adds the only set-based
deletes in the installation.

## 2. Source artifacts

Listed in the front matter: Story; Specification v1 (APPROVED); Open
Decisions v3 (OD-001 … OD-007, all resolved); API design v1 (NOT_APPLICABLE);
DB design v1; entity model v1; test strategy v1; AC matrix v1.

## 3. Acceptance Criteria

| AC | Implementation | Tests (`ac_test_matrix`) | Status |
|---|---|---|---|
| AC-001 | `RunRetentionPurgeUseCase.ExecuteAsync` / `IsExpired`; `RetentionPurgeStore.DeleteCourseAsync` | `RetentionPurgeTests.AnExpiredCourse_*` | PASS |
| AC-002 | `RetentionRule.LatestActivity`; `RetentionPurgeStore.GetCourseActivityDatesAsync` | `RetentionPurgeTests.ARecent*` | PASS |
| AC-003 | `RetentionPurgeStore.GetCourseIdsWithExpiredLeaversAsync`, `DeleteExpiredLeaversAsync` | `AnExpiredLeaver_*`, `ALeaversSubmissionsInAnotherCourse_Stay` | PASS |
| AC-004 | `RetentionPurgeStore.DeleteOrphanedParticipantsAsync` | `AnOrphanedParticipant_*`, `ALeaverWithNoOtherCourse_*` | PASS |
| AC-005 | `RetentionPurgeStore.DeleteExpiredAccountsAsync` | `UnusedAccounts_*`, `TheAccountBoundary_IsStrict` | PASS |
| AC-006 | existing `CompleteGoogleSignInUseCase`, unchanged | `AdminReturnsAfterPurgeTests` | PASS |
| AC-007 | `RetentionPurgeStore.DeleteAuditEventsOlderThanAsync`; `ix_audit_event_occurred_at` | `ExactlyTheAuditRowsOlderThanTheCutoff_AreDeleted`, `AuditDeletionConfinementTests` | PASS |
| AC-008 | `AuditEvent.RetentionPurgeRun`; five columns; four check constraints | `TheRun_WritesOneAuditEventWithTheCounts` and the rest of the AC-008 rows | PASS |
| AC-009 | `ServiceWriteScope.Declare(RetentionPurge)` in the use case; `PermittedServiceWrites` entry | `RetentionPurgeReadOnlyTests` | PASS |
| AC-010 | `TryUnitAsync` per course; `RetentionPurgeBackgroundService.LogUnitFailed` | `AFailingCourse_*`, `AFailingLeaverDelete_*`, `RetentionPurgeLoggingTests.AFailedCourse_*` | PASS |
| AC-011 | `SyncRunCoordinator.TryStartPurge` / `PurgeCompleted` / `IsPurging`; `RetentionPurgeBackgroundService.AcquireGateAsync`; `SynchronizationBackgroundService` waits for the purge's end | `RetentionPurgeCoordinationTests`, `APurgeDueDuringASynchronizationRun_WaitsForItsEnd`, all existing synchronization host tests | PASS |
| AC-012 | `RetentionPurgeBackgroundService.ExecuteAsync` (due at once, then start + 24 h; `TimeProvider`; stops on cancellation) | `RetentionPurgeScheduleTests` | PASS |
| AC-013 | unchanged synchronization (no delete) | `SynchronizationNeverDeletesTests` | PASS |
| AC-014 | `RetentionRule.IsExpired` (strict `<`); store predicates use `<` | boundary tests | PASS |
| AC-015 | `IsExpired` falls back to `CourseCreatedAt` (OD-006) | `ACourseWithNoGoogleDate_*`, `AnOldGoogleDate_*` | PASS |
| AC-016 | `RunSynchronizationUseCase.IsOlderThanRetention` rewritten on `RetentionRule` | `CourseAgeRuleTests` unchanged and green | PASS |
| AC-017 | `TryUnitAsync` for each non-course step | `AFailingStep_IsRolledBack_AndTheLaterStepsStillRun` | PASS |

## 4. Change set

### Production — created

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Rules/RetentionRule.cs` | FR-001, FR-002; entity model §2 |
| `src/ClassroomAgent.Domain/Rules/RetentionPurgeCounts.cs` | FR-010; entity model §2 |
| `src/ClassroomAgent.Application/Ports/IRetentionPurgeStore.cs` | entity model §3 |
| `src/ClassroomAgent.Application/Models/CourseActivityDates.cs` | entity model §3; db-design §4 |
| `src/ClassroomAgent.Application/Models/RetentionPurgeOutcome.cs`, `RetentionPurgeFailure.cs`, `RetentionPurgeStep.cs` | FR-012; OD-007 |
| `src/ClassroomAgent.Application/UseCases/RunRetentionPurgeUseCase.cs` | FR-001 … FR-011; entity model §4 |
| `src/ClassroomAgent.Infrastructure/Persistence/RetentionPurgeStore.cs` | db-design §3 … §6 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261003194237_AddRetentionPurge.cs` (+ `.Designer.cs`) | db-design §7 (PC-2) |
| `src/ClassroomAgent.Web/BackgroundServices/RetentionPurgeBackgroundService.cs` | FR-012 … FR-015; entity model §5 |

### Production — modified

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Enums/AuditAction.cs` | `RetentionPurgeRun` (db-design §2.3) |
| `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` | five count properties and the factory (entity model §1) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | columns, action code, constraints, index (db-design §2); FR-018 comment |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseMembershipConfiguration.cs` | partial index (db-design §3) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AppUserConfiguration.cs` | FR-018 comment (no index; db-design §5) |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | generated with the migration |
| `src/ClassroomAgent.Application/Ports/IAuditEventRepository.cs` | FR-018 comment |
| `src/ClassroomAgent.Application/UseCases/PermittedServiceWrites.cs` | FR-011; BR-026 registry entry |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | FR-002: shared rule (AC-016) |
| `src/ClassroomAgent.Web/BackgroundServices/SyncRunCoordinator.cs` | FR-014: purge slot, `IsPurging` |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-014: a run refused because the purge holds the gate waits for its end (§7) |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI registration (supporting change) |

### Tests

Created at TEST_WRITING and listed in the test-generation report. Modified at
IMPLEMENTATION (see §7):

- `PermittedServiceWriteTests.cs`;
- `AppUserMigrationTests.cs`;
- `AccessCheckAuditSchemaTests.cs`;
- `DeanAccountAuditSchemaTests.cs`;
- `AdminSignInAuditTests.cs`;
- `WorkspaceConnectionReadOnlyTests.cs`.

### Workflow artifacts

The three TEST_WRITING documents, this report, and the workflow state files.

No secret, generated database file or IDE-local config is in the change set.

## 5. Validation evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter <US-037 classes + CourseAgeRuleTests + ReadOnlyEnforcementTests>` | 0 | 149 / 149 passed |
| `dotnet test ClassroomAgent.sln --no-build` (first) | 2 | 2725 passed, **55 failed** — see §7 |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter <synchronization host classes + RetentionPurge*>` after the fix | 0 | 135 / 135 passed |
| `dotnet test ClassroomAgent.sln --no-build` (final) | 0 | **2780 / 2780 passed**, 0 skipped |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | clean |
| `rg "TODO\|TBD\|FIXME\|NotImplementedException" src` | — | no match: the skeleton is fully replaced |

## 6. Configuration changes

None. N comes from the existing `Retention:Years` (US-015). The 24-hour
interval is fixed by OD-001 and is deliberately not configurable (spec §10).

## 7. Deviations and discovered problems

1. **Synchronization lost its first run behind the purge.** This was the cause
   of 49 of the 55 first-run failures.
   - What happened: at host start the purge took the shared gate. The
     synchronization service's `TryStartScheduledRun` then returned false, and
     the service waited a whole interval before trying again.
   - Why it is a defect: FR-014 and AC-011 require that run to start when the
     purge ends.
   - Fix: `SynchronizationBackgroundService` now waits on the coordinator's
     signal and retries when the gate is refused **because the purge holds
     it** (`SyncRunCoordinator.IsPurging`). Its interval behaviour is
     otherwise unchanged.
   - Evidence: every existing synchronization host test, with the purge now
     running at start in each of them.
   - TEST_WRITING had no dedicated test for this direction at host level. The
     existing suite proved to be one.
2. **Six existing tests asserted the pre-US-037 schema and registry**:
   - the exact `audit_event` column list (2 tests);
   - the migration count;
   - the `PermittedServiceWrites` list and count (2 tests);
   - the number of rows `AuditRowsAsJsonAsync` returns after two sign-ins (the
     purge's start-up row is now among them).

   They were updated to the approved DB design and FR-011, with comments citing
   US-037. No assertion was weakened: the "no personal data" check still
   covers every row, the purge row included. TEST_WRITING should have caught
   these, which is recorded here as a lesson.
3. **The skeleton left out the count properties** (test-generation report §2).
   They are added now, with their configuration and migration, as the entity
   model specifies.
4. **Migration `Down`** re-creates the old `ck_audit_event_action`. It
   therefore fails while `retention_purge_run` rows exist. This is deliberate:
   rolling back must not silently delete audit rows (SC-11). An operator would
   have to decide.
5. **I-1** is implemented as "next due = this run's actual start + 24 h". A
   start delayed by a synchronization run shifts later runs by that delay only.

## 8. Open Decisions

None touched or newly required. OD-001 … OD-007 are resolved and implemented as
resolved.

## 9. Attempt 2 — after the HUMAN_PR_APPROVAL rejection

The Owner rejected the gate on 2026-10-03 with: "исправить N-2 (потеря
сигнала об окончании очистки) и N-3 (расчёт отсечки внутри try и верхняя граница
Retention:Years = 100); N-1 вынести в §7". These findings come from the
independent security review.

| Finding | Change | Trace |
|---|---|---|
| N-2: a purge ending between `TryStartScheduledRun` and the `IsPurging` check pushed the due synchronization run back a whole interval | `SynchronizationBackgroundService.ExecuteAsync` decides again when `runs.IsPurging \|\| changed.IsCompleted`. The signal is captured before the attempt, so a state change in between is never lost | spec FR-014, AC-011 |
| N-3a: the cutoff was computed outside the run's error handling, so an exception there faulted the service and stopped the host | `RetentionPurgeBackgroundService.RunOnceAsync`: `LogStarted(…Cutoff…)` moved inside `try` | spec FR-012; OD-008 |
| N-3b: `Retention:Years` had no upper bound | `InstallationSettingsReader.RetentionYears` refuses values above `MaxRetentionYears` = 100 at start-up | **OD-008** (new, resolved by the Owner in the rejection comment) |
| N-1: a purged leaver can be re-imported by the next synchronization | **no code change** — a requirements question for `trebovaniya.md` §7, as the Owner directed | — |

### Files changed in attempt 2

- `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` — N-2.
- `src/ClassroomAgent.Web/BackgroundServices/RetentionPurgeBackgroundService.cs` — N-3a.
- `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` — N-3b
  (`MaxRetentionYears`).
- `tests/ClassroomAgent.Tests/Web/Configuration/RetentionConfigurationTests.cs`
  — `RetentionYearsAboveTheBound_RefusesToStart` (101) and
  `RetentionYearsOnTheBound_Starts` (100).
- `docs/decisions/US-037-open-decisions.md` → v4 (OD-008).

### Validation (attempt 2)

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` | 0 | **2782 / 2782 passed**, 0 skipped |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | clean |

### Limitations and follow-ups

- **N-2 has no deterministic test.** The interleaving happens between two
  statements of a private loop and cannot be forced from outside without a
  test seam the design does not define. The main path — a run waiting for the
  purge's end — is covered by every synchronization host test, since the purge
  runs at each host start.
- **`trebovaniya.md` v81 is still due**, in its own commit, never in this
  Story's commit:
  - §5 must state the 1 … 100 bound of N (OD-008);
  - §7 must get the new open item for N-1.
  - `deployment-conventions.md` DC-3 ("a positive whole number") follows from
    §5 and is updated with it.
- **The non-blocking test hardenings were not done** (out of the rejection's
  scope):
  - M-1 — a purged account's cookie;
  - M-2 / N-4 — the confinement test missing `ExecuteSqlRaw`, `Remove` and
    `RemoveRange`.
