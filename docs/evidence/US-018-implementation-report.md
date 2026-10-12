---
artifact_type: implementation_report
story: US-018
version: 1
status: DRAFT
created_at: 2026-10-12T08:20:00Z
updated_at: 2026-10-12T09:30:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-018-incremental-sync.md
    version: null
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
    version: 2
  - path: docs/evidence/US-018-test-generation-report.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-018 Implementation Report — Incremental synchronization

## 1. Summary

A synchronization run now skips an archived course whose listed `updateTime`
equals the value remembered at its last complete read. It reads every other
course in full. Once a week the run reads everything (OD-001 a). The full-run
time is recorded only when a full run succeeds (OD-002 a). Skipped courses are
counted, and the count is logged on the host's completion and failure lines.

The Story adds no endpoint, page, policy, audit event or translation. Build,
the full test suite and the format check pass (§5).

One test assertion was corrected with the Owner's agreement (§7). The correction
concerns a run started after the clock was moved back.

## 2. Source artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-018-incremental-sync.md` | — |
| Specification | `docs/specifications/US-018-spec.md` | 1 |
| Open Decisions | `docs/decisions/US-018-open-decisions.md` | 2 |
| API design / OpenAPI | NOT_APPLICABLE | — |
| Database design | `docs/designs/database/US-018-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-018-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-018-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-018-ac-test-matrix.md` | 2 |
| Test generation report | `docs/evidence/US-018-test-generation-report.md` | 1 |

## 3. Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `RunSynchronizationUseCase.ImportCoursesAsync` performs the skip after the blank-name check. The decision is `IncrementalSyncRule.IsUnchangedArchive` against `ICourseRepository.GetCompleteReadUpdateTimeAsync`. | `IncrementalSyncRunTests` AC-001 group; `IncrementalSyncRuleTests` | PASS |
| AC-002 | `Course.RecordCompleteRead()` is called in `ImportCourseAsync` before the transaction's last `SaveChangesAsync`. `IncrementalSyncRule.ToStoragePrecision` normalises the value. Column `course.complete_read_update_time`. | Run AC-002 group; Rule (Course, precision); `IncrementalSyncPersistenceTests` | PASS |
| AC-003 | `IsUnchangedArchive` accepts only the listed `CourseState.Archived`. | Run `ANonArchivedCourse_…` (4 states), `TheListedState_Decides_…` | PASS |
| AC-004 | Same rule: a restored course is listed as `Active`. | Run `ASkippedCourse_RestoredToActive_…` | PASS |
| AC-005 | A failed read never reaches the transaction, so `RecordCompleteRead` is not called for that course. | Run `AFailedCourseRead_…` (9 cases), `ANewCourseWhoseReadFailed_…` | PASS |
| AC-006 | `BeginAsync` computes `IsFullRunDue` once per run. `SyncState.CompleteRun(…, fullRun)` sets `LastFullRunAt = StartedAt`. Column `sync_state.last_full_run_at` and check `ck_sync_state_last_full_run_at`. | Run AC-006 group; Rule (decision, `SyncState`); Persistence (column, check) | PASS |
| AC-007 | One code path: the button and the schedule both call `RunOnceAsync` → `RunSynchronizationUseCase`. | `IncrementalSyncHostTests.AButtonRun_…` (2) | PASS |
| AC-008 | `SynchronizationRunOutcome.CoursesSkippedUnchanged`. `SynchronizationBackgroundService` writes `SkippedUnchanged` on `SyncRunCompleted` and `SyncRunFailed`. Only the count is logged. | Host `TheCompletionLine_…`, `TheLog_NamesNoSkippedCourse`, `TheFailureLine_…`; Run `AFailedRun_StillCarries…` | PASS |
| AC-009 | Unchanged: the read-only guard runs before `BeginAsync`, so the run reads and looks up nothing. | Run `InReadOnlyMode_…` | PASS |
| AC-010 | Every test substitutes the Google ports. | all | PASS |

## 4. Change set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Rules/IncrementalSyncRule.cs` (new) | entity model §1; FR-001, FR-005, VR-001…VR-003 |
| `src/ClassroomAgent.Domain/Entities/Course.cs` | entity model §2; FR-002 |
| `src/ClassroomAgent.Domain/Entities/SyncState.cs` | entity model §3; FR-006, I-3 |
| `src/ClassroomAgent.Application/Ports/ICourseRepository.cs` | entity model §4 |
| `src/ClassroomAgent.Application/Models/SynchronizationRunOutcome.cs` | entity model §5 step 4; FR-008, FR-009 |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | entity model §5; FR-001…FR-008 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseConfiguration.cs` | db-design §2; PC-4 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SyncStateConfiguration.cs` | db-design §3 (column and check) |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseRepository.cs` | entity model §4; db-design §6 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261012073623_AddIncrementalSync.cs`, `.Designer.cs` (new) and `ClassroomAgentDbContextModelSnapshot.cs` | db-design §4; PC-2 |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-009, I-2; test report §2 note 2 |
| `tests/…/Application/UseCases/IncrementalSyncRunTests.cs` | `ac_test_matrix` (one assertion corrected, §7) |
| `tests/…/Application/UseCases/IncrementalSyncRuleTests.cs` | `ac_test_matrix` |
| `tests/…/Infrastructure/Persistence/IncrementalSyncPersistenceTests.cs` | `ac_test_matrix` |
| `tests/…/Web/BackgroundServices/IncrementalSyncHostTests.cs` | `ac_test_matrix` |
| `tests/…/TestInfrastructure/SyncWorld.cs`, `FakeClassroomReader.cs` | test fixtures (TEST_WRITING) |
| `tests/…/Web/Persistence/CourseSchemaTests.cs`, `SyncStateSchemaTests.cs`, `tests/…/Infrastructure/Persistence/AppUserMigrationTests.cs` | schema registry updates from db-design §2–§4 (TEST_WRITING) |
| `docs/tests/US-018-ac-test-matrix.md` (v2) | one row corrected (§7) |
| `docs/evidence/US-018-implementation-report.md` | this report |

The change set contains no secret, no generated database file, no `.xlsx` and
no IDE-local config.

## 5. Validation evidence

- **Build.** `dotnet build ClassroomAgent.sln` exits 0 with 0 warnings and
  0 errors.
- **Format.** `dotnet format ClassroomAgent.sln --verify-no-changes` exits 0.
- **Migration.** Generated with `dotnet ef migrations add AddIncrementalSync`.
  `Up` adds the two nullable `timestamptz` columns and the check. `Down` drops
  all three. This matches db-design §4.
- **Targeted run** (the US-018 classes and the sync, Meet, course and schema
  classes): 1563 tests, 1561 passed, 2 failed.
  - `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` passed when its class was
    rerun alone (10/10). It does not touch course or sync code.
  - `IncrementalSyncRunTests.AFullRunTimeInTheFuture_MakesTheRunFull` is
    discussed in §7.
- **Full run.** `dotnet test ClassroomAgent.sln` took 24 min: 5307 tests, 5033
  passed, 274 failed. All 274 failures are environmental:
  - 273 are Npgsql timeouts. The shared PostgreSQL container saturates under the
    full suite (US-033 INFO-3).
  - 1 is a sign-in that returned 500 after an 11-minute wait.
  - No US-018 test is among the failures.
- **Rerun of the 32 affected classes:** 465 tests, all passed.
- **Delegation.** None this stage. Every edit embodies the rule or the design,
  and each run's failures needed classification as they appeared. Builds and
  test runs ran in background shells rather than in subagents, to keep the
  red-to-green evidence in one place.

## 6. Configuration changes

None.

## 7. Deviations and discovered problems

1. **`AFullRunTimeInTheFuture_MakesTheRunFull` asserted more than VR-002.**
   - The run after the clock moves back is correctly full: it reads the archive.
   - The test also expected that run to complete and to rewrite
     `LastFullRunAt`.
   - The approved US-031 watermark rule prevents completion:
     `CompleteRun` refuses a Meet watermark earlier than the stored one, so the
     run stops at the Meet step. This was already true before US-018.
   - With the Owner's agreement (2026-10-12), the test now asserts what VR-002
     requires: the run is full, nothing is skipped, and the full-run time is not
     moved forward.
   - The consequence for US-018 is safe. Every run stays full until the clock
     catches up.
   - Matrix row updated (v2).
2. **db-design §3 is inaccurate.** It says a clock moved back makes the next run
   full, "which rewrites both". The run is full, but it does not complete (point
   1), so nothing is rewritten. The check constraint still holds. This is a
   non-blocking design-text inaccuracy; the design itself is not edited.

## 8. Open Decisions

None raised. OD-001 … OD-003 are all resolved.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-018
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-018-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - AFullRunTimeInTheFuture assertion corrected with Owner agreement (US-031 watermark blocks completion after clock moved back)
    - db-design §3 "rewrites both" inaccurate for a clock moved back
```
