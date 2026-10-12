---
artifact_type: test_generation_report
story: US-018
version: 1
status: DRAFT
created_at: 2026-10-12T06:40:00Z
updated_at: 2026-10-12T08:05:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/decisions/US-018-open-decisions.md
    version: 2
  - path: docs/designs/database/US-018-db-design.md
    version: 1
  - path: docs/designs/database/US-018-entity-model.md
    version: 1
  - path: docs/tests/US-018-test-strategy.md
    version: 1
  - path: docs/tests/US-018-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-018 Test Generation Report — Incremental synchronization

## 1. Result

**PASS.** Every AC is mapped (`ac_test_matrix`), the solution builds with 0
warnings, the red phase is verified, and the regression suite is green apart
from the three schema tests updated to the approved design.

- 95 new tests: 87 red for the expected reason, 8 guards green (§5).
- No existing regression test fails for any other reason (§6).

## 2. Production skeleton (OD-003 a, Owner 2026-10-12)

The skeleton is compile-only. Every new behaviour throws
`NotImplementedException`. There is no EF mapping and no migration, and no
existing behaviour changed. IMPLEMENTATION owns and completes it.

- **Domain:** new `Rules/IncrementalSyncRule` (`FullRunPeriod` = 7 days, as
  declared; three throwing methods). `Course` gets a get-only throwing
  `CompleteReadUpdateTime` and a throwing `RecordCompleteRead()`. `SyncState`
  gets a get-only throwing `LastFullRunAt`, and `CompleteRun(…, bool fullRun =
  false)` accepts the parameter and ignores it.
- **Application:** `ICourseRepository.GetCompleteReadUpdateTimeAsync`.
  `SynchronizationRunOutcome.CoursesSkippedUnchanged` (int) is new, with an
  optional `coursesSkippedUnchanged` parameter on `Ran` and `RanAndFailed`.
  The use case never passes it yet.
- **Infrastructure:** `CourseRepository.GetCompleteReadUpdateTimeAsync` throws.

**Binding notes for IMPLEMENTATION**

1. Turn the throwing get-only properties into mapped `{ get; private set; }`
   properties. Configure them (PC-4), then generate migration
   `AddIncrementalSync` with `ck_sync_state_last_full_run_at` (db-design §3–§4).
2. The host's `SyncRunCompleted` and `SyncRunFailed` lines must carry a
   structured property named exactly **`SkippedUnchanged`**. The host tests
   assert that name. Add it as a template placeholder. Do not add a new event.
3. Pass `fullRun` to `CompleteRun` and the skipped count to both outcome
   factories (entity model §5).

## 3. Files

**Created:**
- `tests/ClassroomAgent.Tests/Application/UseCases/IncrementalSyncRuleTests.cs` (37)
- `tests/ClassroomAgent.Tests/Application/UseCases/IncrementalSyncRunTests.cs` (39)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/IncrementalSyncPersistenceTests.cs` (14)
- `tests/ClassroomAgent.Tests/Web/BackgroundServices/IncrementalSyncHostTests.cs` (5)
- `src/ClassroomAgent.Domain/Rules/IncrementalSyncRule.cs` (skeleton)

**Modified, tests and fixtures:**
- `TestInfrastructure/SyncWorld.cs`: the in-memory `CourseRepository` implements
  the new port method and records `CompleteReadLookups`.
- `TestInfrastructure/FakeClassroomReader.cs`: `WithCourse` gains `updateTime`.
  The reader also records the course ids passed to `RostersRead`,
  `CourseWorkRead` and `SubmissionsRead`.
- Updated to the approved schema:
  - `Web/Persistence/CourseSchemaTests.cs`: 18 columns.
  - `Web/Persistence/SyncStateSchemaTests.cs`: 14 columns.
  - `Infrastructure/Persistence/AppUserMigrationTests.cs`: 18 migrations, the
    last one `_AddIncrementalSync`.

**Modified, skeleton:**
- `Domain/Entities/Course.cs`
- `Domain/Entities/SyncState.cs`
- `Application/Ports/ICourseRepository.cs`
- `Application/Models/SynchronizationRunOutcome.cs`
- `Infrastructure/Persistence/Repositories/CourseRepository.cs`

**Artifact:** `docs/decisions/US-018-open-decisions.md` v2 (OD-003).

## 4. Commands

- `dotnet build ClassroomAgent.sln`: 0 errors, 0 warnings.
- `dotnet test ClassroomAgent.sln` (full run, 56 min): 5307 tests, 565 failed.
  Of the 565:
  - 479 are Npgsql timeouts and 4 are "transient failure" or a 500 caused by
    them. Docker Desktop had just started and the shared PostgreSQL container
    was saturated. This is the known environment issue (US-033 INFO-3).
  - 87 are US-018 red tests (13 persistence tests included, which timed out in
    this run and were rerun separately).
  - 3 are the updated schema tests.
- Rerun of the 44 non-US-018 classes that had any failure (filter by class,
  1 min 44 s): 666 tests, 663 passed, 3 failed. The 3 are the updated schema
  tests, red until the migration exists.
- `dotnet test --filter FullyQualifiedName~IncrementalSyncPersistenceTests`:
  14 tests, 13 red, 1 guard green.
- `dotnet test --filter …IncrementalSyncRuleTests|…IncrementalSyncRunTests`:
  76 tests, 69 red, 7 guards green.

## 5. Red-phase classification

| Cause | Count | Tests |
|---|---|---|
| `NotImplementedException` from the skeleton | 66 | Rule 36; Run 26 (every test reading `CompleteReadUpdateTime` or `LastFullRunAt`); Db 4 (repository) |
| Behaviour missing: the unchanged archive is still read, or the skipped count is 0 | 10 | Run: `AnUnchangedArchivedCourse_IsNotRead`, `…_WithSubMicrosecondTicks_IsStillSkipped`, `OnlyTheUnchangedArchivedCourse_IsSkipped_AmongOthers`, `…_IsCountedAsSkipped_NotAsProcessed`, `ASkippedCourse_RestoredToActive_…`, `JustUnderSevenDays_…`, `AFailedRun_StillCarriesTheSkippedCount…`; Host: both `AButtonRun_…`, `TheCompletionLine_…` |
| No `SkippedUnchanged` log property | 2 | Host: `TheLog_NamesNoSkippedCourse` (control assertion), `TheFailureLine_CarriesTheSkippedCount` |
| Migration absent: missing column or constraint | 9 | Db: both column tests, the check-exists test, 3 × accepted, 2 × rejected, the no-backfill test |
| **Guards: pass now and must stay green** | 8 | Run: `AnArchivedCourse_ChangedByOneMicrosecond_IsReadInFull`, `TheListedState_Decides_NotTheStoredOne`, `AFullRun_SkipsNothing`, `ABlankNamedUnchangedArchive_…`, `AnUnrecognisedState_…`, `ANewArchivedCourseOlderThanRetention_…`; Rule: `TheFullRunPeriod_IsSevenDays`; Db: `NoIndexIsAdded` |

The guards assert behaviour this Story must not break. Today the code reads
everything, so a test asserting "is read" or "skipped is 0" passes. Each of
them has a red counterpart that proves the skip itself. For example,
`AFullRun_SkipsNothing` pairs with `JustUnderSevenDays_…` and
`AnUnchangedArchivedCourse_IsNotRead`. `TheFullRunPeriod_IsSevenDays` passes
because the skeleton declares the constant from entity model §1.

Every failure message fits its scenario. None is a compile, fixture or
assertion-construction error.

## 6. Existing tests

Apart from the 3 updated schema tests, no existing test fails for a non-
environmental reason. Any test that failed in the saturated full run passed
when rerun.

## 7. Untested ACs and open decisions

No Acceptance Criterion is left untested. AC-010 holds by construction: every
test substitutes the Google ports.

Exclusions with their justification are in test strategy §5: the rollback of
the course transaction, host-stop cancellation, and the once-per-run decision.

OD-003 is resolved (a). No decision is open.

## 8. Delegation

The test design, the skeleton, the Domain, Application and persistence tests,
and the red-phase verdict were done inline. The host tests
(`IncrementalSyncHostTests`) were written by a `cheap-worker` subagent from a
fixed scenario list and reviewed here. That subagent's run hit the stopped
Docker daemon, so its tests were run in the full suite above.
