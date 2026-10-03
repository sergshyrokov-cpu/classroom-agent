---
artifact_type: test_generation_report
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T19:30:20Z
updated_at: 2026-10-03T19:30:20Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/designs/api/US-037-api-design.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-037-entity-model.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 3
  - path: docs/tests/US-037-test-strategy.md
    version: 1
  - path: docs/tests/US-037-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-037 Test Generation Report — Retention purge

## 1. Result

**Red phase verified.** The full suite was run on a build with 0 warnings and
0 errors:

- 2780 tests in total, **2690 passed, 90 failed**;
- every failure is in a test this Story adds or updates, plus one existing
  architecture test that the skeleton turns red on purpose (§5);
- every failure is caused by missing behaviour, never by a test or environment
  defect;
- no existing regression test fails.

## 2. Production skeleton (OD-007, option 1; IMPLEMENTATION owns it)

Created, every behavioural member throwing `NotImplementedException`, nothing
registered in DI, no EF configuration, no migration:

| File | Content |
|---|---|
| `src/ClassroomAgent.Domain/Enums/AuditAction.cs` | member `RetentionPurgeRun` |
| `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` | factory `RetentionPurgeRun(RetentionPurgeCounts, DateTimeOffset)`, throwing |
| `src/ClassroomAgent.Domain/Rules/RetentionPurgeCounts.cs` | record struct, in full (data) |
| `src/ClassroomAgent.Domain/Rules/RetentionRule.cs` | `Cutoff`, `IsExpired`, `LatestActivity`, throwing |
| `src/ClassroomAgent.Application/Ports/IRetentionPurgeStore.cs` | interface of entity model §3 |
| `src/ClassroomAgent.Application/Models/CourseActivityDates.cs`, `RetentionPurgeOutcome.cs`, `RetentionPurgeFailure.cs`, `RetentionPurgeStep.cs` | data, in full |
| `src/ClassroomAgent.Application/UseCases/RunRetentionPurgeUseCase.cs` | constructor + `ExecuteAsync`, throwing |
| `src/ClassroomAgent.Infrastructure/Persistence/RetentionPurgeStore.cs` | every member throwing |
| `src/ClassroomAgent.Web/BackgroundServices/RetentionPurgeBackgroundService.cs` | `Interval` = 24 h; `ExecuteAsync` throwing; **not registered** |
| `src/ClassroomAgent.Web/BackgroundServices/SyncRunCoordinator.cs` | `TryStartPurge()`, `PurgeCompleted()`, throwing |

**Deviation from OD-007's list, recorded here.** OD-007 listed the five `int?`
count properties on `AuditEvent`. They were **left out** of the skeleton:

- EF Core maps a public property by convention, so with no configuration and
  no migration every existing audit write would have failed on a missing
  column;
- the tests read the counts through SQL and do not need the properties.

IMPLEMENTATION adds them together with their configuration and the migration
(entity model §1, db-design §2.2).

**Names the tests fix for IMPLEMENTATION:**

- log events `RetentionPurgeStarted`, `RetentionPurgeCompleted` (Information),
  `RetentionPurgeUnitFailed` (Error), `RetentionPurgeWaitingForSync`
  (Information);
- `RetentionPurgeBackgroundService.Interval`;
- the constraint and index names of db-design §2.4, §2.5 and §3.

## 3. Test files

### Created

| File | Tests |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/RetentionPurgeTestData.cs` | fixture |
| `tests/ClassroomAgent.Tests/TestInfrastructure/RetentionPurgeHost.cs` | fixture |
| `tests/ClassroomAgent.Tests/Application/UseCases/RetentionRuleTests.cs` | 9 |
| `tests/ClassroomAgent.Tests/Application/UseCases/RetentionPurgeAuditEventTests.cs` | 8 |
| `tests/ClassroomAgent.Tests/Application/UseCases/RetentionPurgeTests.cs` | 24 |
| `tests/ClassroomAgent.Tests/Application/UseCases/RetentionPurgeReadOnlyTests.cs` | 4 |
| `tests/ClassroomAgent.Tests/Application/UseCases/SynchronizationNeverDeletesTests.cs` | 1 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/RetentionPurgeSchemaTests.cs` | 27 |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/RetentionPurgeCoordinationTests.cs` | 9 |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/RetentionPurgeScheduleTests.cs` | 5 |
| `tests/ClassroomAgent.Tests/Web/Logging/RetentionPurgeLoggingTests.cs` | 3 |
| `tests/ClassroomAgent.Tests/Web/UseCases/AdminReturnsAfterPurgeTests.cs` | 1 |
| `tests/ClassroomAgent.Tests/Architecture/AuditDeletionConfinementTests.cs` | 3 |

### Modified

| File | Change | Why |
|---|---|---|
| `TestInfrastructure/InstallationTestHost.cs` | `AuditRowsAsync` excludes `retention_purge_run` rows | OD-007: the purge writes one at every host start; ~200 existing assertions are about other actions. `AuditRowsAsJsonAsync` still reads every row |
| `TestInfrastructure/CourseTestData.cs` | constant `MembershipOffRosterLastSeen` | the new index name |
| `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | five columns, four constraints; the index test now expects `ix_audit_event_occurred_at` (renamed `TheTable_HasOnlyThePrimaryKeyAndThePurgeIndex`) | db-design §2; US-008's "no index" was explicitly deferred to the purge |
| `Web/Persistence/CourseMembershipSchemaTests.cs` | `TheRequiredIndexes_Exist` expects the partial index | db-design §3 |

No assertion was weakened, no test disabled or deleted.

## 4. Commands

```
dotnet build ClassroomAgent.sln                        → 0 warnings, 0 errors
dotnet test tests/ClassroomAgent.Tests --no-build --filter <unit, schema, architecture classes>
                                                       → 98 run, 52 failed, 46 passed
dotnet test tests/ClassroomAgent.Tests --no-build --filter <host-level classes>
                                                       → 37 run, 37 failed
dotnet test ClassroomAgent.sln --no-build              → 2780 run, 2690 passed, 90 failed, 0 skipped
```

Docker was running; Testcontainers started PostgreSQL normally.

## 5. Failure classification (full suite, 90)

| Cause | Count | Tests |
|---|---|---|
| `NotImplementedException` from the skeleton | 25 | `RetentionRuleTests` (9), `RetentionPurgeAuditEventTests` factory tests (7), `RetentionPurgeCoordinationTests` (9) |
| `No service for type RunRetentionPurgeUseCase` (not yet registered) | 28 | `RetentionPurgeTests` (24), `RetentionPurgeReadOnlyTests` theory (3), `AdminReturnsAfterPurgeTests` (1) |
| `42703: column "purged_courses" does not exist` / index missing / constraint absent (no migration yet) | 31 | `RetentionPurgeSchemaTests` (22), `InstallationAuditEventSchemaTests` (3), `CourseMembershipSchemaTests` (1), `RetentionPurgeScheduleTests` (5; it reads the purge rows) |
| Timed out waiting for `RetentionPurgeCompleted` (service not registered) | 3 | `RetentionPurgeLoggingTests` |
| Registry / structure not yet in place | 3 | `ThePurge_IsDeclaredAsTheRetentionPurgeServiceWrite`, `SetBasedDeletes_ExistOnlyInThePurgeStore`, and the **existing** `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` |

The last one is an existing US-007 structural test. It now reports
`RunRetentionPurgeUseCase` as a write path that is neither guarded nor
registered. That is exactly what it must do until IMPLEMENTATION adds the
`PermittedServiceWrites` entry (FR-011). This is expected red, not a
regression.

Unexpected failures: **none**.

## 6. New tests that pass before implementation (GUARD)

Each was checked for vacuity:

| Test | Why it passes now, and why it is kept |
|---|---|
| `RetentionPurgeSchemaTests.EveryForeignKeyThePurgeCrosses_StaysRestrict` (×3) | PC-8 already holds. It guards against the tempting shortcut of a `Cascade` migration (db-design §1); it would fail if IMPLEMENTATION took it |
| `RetentionPurgeSchemaTests.AppUser_GetsNoPurgeIndex` | db-design §5 deliberately adds none; the guard is against adding one |
| `RetentionPurgeAuditEventTests.TheEntity_StaysImmutable` | SC-11 already holds; the guard is for when the five properties are added — they must keep private setters |
| `AuditDeletionConfinementTests.TheAuditRepository_CanOnlyAdd`, `NoOtherCode_TouchesAuditRows` | true today; fail if IMPLEMENTATION puts the audit delete on the repository or anywhere outside the store |
| `SynchronizationNeverDeletesTests.APersonWhoLeftTheRoster_KeepsTheirMembershipAndSubmissions` | AC-013 states an existing property (PC-11); it must stay true once the purge exists and the rule is shared. It asserts the membership **exists and is off the roster** and the submission **exists**, so it is not "nothing happened" — the second run did mark it off the roster |

AC-016 relies on the unchanged US-015 suite (`CourseAgeRuleTests`), which
passes now and must pass unchanged after IMPLEMENTATION.

## 7. Untested Acceptance Criteria

None. Every AC-001 … AC-017 is mapped in `docs/tests/US-037-ac-test-matrix.md`.
Partial coverage is noted in test strategy §6:

- shutdown in the middle of a course transaction is covered indirectly (per-unit
  atomicity and the prompt-stop test);
- the log lines' counts are asserted through the audit row, not by parsing the
  log template.

## 8. Open Decisions

OD-007 was raised and resolved at this stage (option 1, by the Owner,
2026-10-03). None open.

## 9. Notes for IMPLEMENTATION

- Once `RetentionPurgeBackgroundService` is registered, it runs at **every**
  host start in the existing suite. The existing host tests seed recent data at
  `DefaultStart`, so nothing they rely on should be expired. If one fails after
  registration, check its seeded dates before touching the purge.
- `TheHost_StopsPromptly` and the schedule tests use the manual clock. The
  service must take its delay from the injected `TimeProvider`, or they hang
  until the 30-second limit.
- `RetentionPurgeScheduleTests.APurgeDueDuringASynchronizationRun_WaitsForItsEnd`
  expects the `RetentionPurgeWaitingForSync` line when the purge finds the gate
  held.
