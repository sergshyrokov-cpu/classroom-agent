---
artifact_type: implementation_report
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T13:35:00Z
updated_at: 2026-10-27T13:35:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-059-sync-marks-courses-gone-and-skips-negative-values.md
    version: null
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
  - path: docs/designs/api/US-059-api-design.md
    version: 1
  - path: docs/designs/database/US-059-db-design.md
    version: 1
  - path: docs/designs/database/US-059-entity-model.md
    version: 1
  - path: docs/tests/US-059-test-strategy.md
    version: 1
  - path: docs/tests/US-059-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-059 Implementation Report

## 1. Summary

Synchronization now closes a course list read to its end: stored courses the list no longer yields are kept and marked
"not in Classroom" or — when last seen before the technical account's last change — "not visible to the technical
account"; listed courses record the run's instant as their last sighting and lose their mark; an empty list while
courses are stored marks nothing, removes no skip record and sets the `SyncState` flag; one log line per run counts the
marks (or reports the empty list). Saving the connection with another technical account records the save's instant. The
Google reader refuses a negative maximum, assigned grade or draft grade as `ValueOutOfRange`, so the course is skipped
instead of the run stopping on a database check. One migration, `AddCourseListMarks`.

Build 0 warnings; full suite 6363 tests, all green apart from a known flaky test that passes on rerun (§5); format check
clean. One disclosed deviation from db-design (§7, D-1). `security_sensitive: false`: no endpoint, role, policy,
credential or audit change; the log lines carry counts only (SC-10).

## 2. Source Artifacts

As in the front matter: Story (authored, no version), Specification v1 (APPROVED), Open Decisions v1 (none open),
API design v1 (NOT_APPLICABLE), DB design v1, entity model v1, test strategy v1, AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class.method) | Status |
|---|---|---|---|
| AC-001, AC-002 | `RunSynchronizationUseCase.CloseCourseListAsync`; `Course.MarkAbsent` | `CourseListPresenceTests.ACourseGoogleNoLongerLists_…`, `AMarkedCourseStillAbsent_…`, `AnArchivedCourseNoLongerListed_…`; `CourseListPresenceSyncTests.ARealRun_StoresTheMarkCode_…` | green |
| AC-003, AC-004, AC-016, AC-018 | `CloseCourseListAsync`; `Course.RecordListed` | `CourseListPresenceTests.AMarkedCourseThatComesBack_…`, `ListedCoursesTheRunDoesNotRead_…`, `AMarkedCourseOnTheListButNotRead_…`, `AnUnchangedArchiveOnTheList_…`, `ACourseListedButGoneDuringItsReads_…` | green |
| AC-005 | end-of-list step reached only after the `await foreach` ends (`ImportCoursesAsync`) | `ARunWhoseListFailsPartWay_…`, `ARunStoppedByACourseRead_…` | green |
| AC-006 … AC-008 | `CloseCourseListAsync` empty-list branch; `SyncState.RecordCourseListRead` | `AnEmptyListWhileCoursesAreStored_…`, `AListWithOnlyIdlessEntries_IsEmpty`, `AFlaggedEmptyList_…`, `AnEmptyListWithNoCourseStored_…`; `CourseListPresenceSyncTests.AnEmptyList_…` | green |
| AC-009 | `GoogleClassroomReader.Points` (negative → `ValueOutOfRange`) | `GoogleClassroomReaderValueRangeTests.ANegativeValue_…`, `TheNegativeValueFailure_…`, `Zero_…`; `NegativeValueSyncTests.*` | green |
| AC-010 | `CourseListMarkCounts`; `SynchronizationRunOutcome.CourseList`; events 5137 `SyncCourseListPresenceChanged`, 5138 `SyncCourseListEmpty` in `SynchronizationBackgroundService` | `CourseListPresenceSyncTests.EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` | green |
| AC-011 | `WorkspaceConnection.ChangeTo(domain, email, changedAt)`; `SaveWorkspaceConnectionUseCase.StoreAsync` passes `now` | `SaveWorkspaceConnectionAccountChangeTests.*`; `CourseListPresenceInvariantTests.Changing…`; `CourseListMarksPersistenceTests.TheChangeInstant_IsStored` | green |
| AC-012 … AC-015, AC-017 | `CourseAbsenceRule.MarkFor`; change instant read at the end of the list (R2) | `CourseListPresenceTests.ACourseLostAfterTheAccountChanged_…`, `ACourseTheNewAccountSaw_…`, `ANotVisibleCourse_…`, `ACourseMarkedNotInClassroom_…`, `ACourseNeverSighted_…`; `CourseListPresenceSyncTests.ARealRun_AfterTheAccountChanged_…`; `CourseListPresenceInvariantTests.TheRule_…` | green |
| db-design §6 R1, R2 | `observedAt` taken before `connectionQuery.ExecuteAsync`, passed into `ImportCoursesAsync`; `workspaceConnections.GetForReadAsync` in `CloseCourseListAsync` | `TheRunInstant_IsTakenBeforeTheConnectionIsRead`, `AnAccountChangeSavedDuringTheRun_IsSeenAtTheEndOfTheList` | green |

## 4. Change Set

Production:

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Enums/CourseAbsenceMark.cs` (new) | entity model §1 |
| `src/ClassroomAgent.Domain/Rules/CourseAbsenceRule.cs` (new) | entity model §3, FR-006 |
| `src/ClassroomAgent.Domain/Entities/Course.cs` | entity model §2, FR-001, FR-003, FR-004; class remark rewritten (entity model §2) |
| `src/ClassroomAgent.Domain/Entities/SyncState.cs` | entity model §4, FR-005 |
| `src/ClassroomAgent.Domain/Entities/WorkspaceConnection.cs` | entity model §5, FR-002; two-argument `ChangeTo` removed |
| `src/ClassroomAgent.Application/Ports/ICourseRepository.cs` | entity model §6 |
| `src/ClassroomAgent.Application/Models/CourseListMarkCounts.cs` (new) | FR-007 (supporting: carries the counts to the host, entity model §7 "counts carried") |
| `src/ClassroomAgent.Application/Models/SynchronizationRunOutcome.cs` | FR-007 (supporting) |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | FR-001 … FR-006, db-design §5, §6; `workspaceConnections` made a required constructor dependency (entity model §6) |
| `src/ClassroomAgent.Application/UseCases/SaveWorkspaceConnectionUseCase.cs` | FR-002 |
| `src/ClassroomAgent.Infrastructure/Google/GoogleClassroomReader.cs` | FR-008, VR-005 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseRepository.cs` | entity model §6 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseConfiguration.cs` | db-design §2 (see D-1) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SyncStateConfiguration.cs` | db-design §4 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/WorkspaceConnectionConfiguration.cs` | db-design §3 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261027110753_AddCourseListMarks.cs`, `.Designer.cs` (new), `ClassroomAgentDbContextModelSnapshot.cs` | db-design §7, PC-2 |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-007 (events 5137, 5138) |

DI: none added — `IWorkspaceConnectionRepository` was already registered; `RunSynchronizationUseCase` resolves it.

Tests (beyond the TEST_WRITING set, which is listed in the test generation report):

| File | Trace |
|---|---|
| `tests/.../Application/UseCases/CourseWorkFailureTests.cs`, `CourseWorkImportTests.cs`, `SubmissionImportTests.cs`, `SubmitterWithoutRosterTests.cs` | supporting: their own run factories pass the new required `workspaceConnections` (`world.Connections`) |
| `tests/.../Application/UseCases/CourseImportTests.cs` | finding US-059/TEST_WRITING/INFO-1: summary comment only |
| `tests/.../Web/Persistence/CourseSchemaTests.cs` | `TheMigration_CreatesTheTable`: 19 → 22 columns plus the three new ones (db-design §2) — an adaptation TEST_WRITING missed (D-3) |
| `tests/.../Infrastructure/Persistence/CourseListMarksPersistenceTests.cs` | `TheChangeInstant_IsStored` reads the instant with `QueryAsync`/`GetFieldValue<DateTimeOffset>` instead of `ScalarAsync<DateTimeOffset?>`, which cannot cast Npgsql's `DateTime` (D-3) |

No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence

All runs from the repository root, delegated to `cheap-worker` (Sonnet) except the last rerun:

- `dotnet build ClassroomAgent.sln` — exit 0, 0 warnings, 0 errors (twice).
- Story classes after the first implementation: all green except 3 (D-1, D-3) — `CourseListPresenceInvariantTests` 25/25,
  `CourseListPresenceTests` 22/22, `SaveWorkspaceConnectionAccountChangeTests` 3/3, `GoogleClassroomReaderValueRangeTests`
  36/36, `CourseListPresenceSyncTests` 4/4, `NegativeValueSyncTests` 2/2, `AppUserMigrationTests` 6/6,
  `SyncStateSchemaTests` 11/11; `CourseListMarksPersistenceTests` 12/14, `CourseSchemaTests` 11/12.
- First full suite `dotnet test ClassroomAgent.sln --no-build`: 6363 total, 6359 passed, 4 failed (D-1, D-2, D-3 ×2).
- After the fixes: `CourseListMarksPersistenceTests` 14/14, `CourseSchemaTests` 12/12, `SynchronizationRunTests` 7/7,
  `CourseListPresenceTests` 22/22.
- Second full suite: 6363 total, 6362 passed, 1 failed, 0 skipped, exit 2 —
  `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` (`Assert.NotNull` at line 103), the flaky test recorded at US-043
  (INFO-1), unrelated to this Story's code. Rerun inline:
  `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~MeetingCodeLinkRepositoryTests"` — 10/10 passed.
- `dotnet format ClassroomAgent.sln --verify-no-changes` — exit 0, no files (twice).

Delegated: every build, test and format run except the flaky-class rerun. Kept inline: all production code, the
migration generation, the failure analysis and the test corrections of §7.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1 (deviation from db-design §2, implements VR-003).** The design's `ck_course_absence_mark` text,
  `(absence_mark IS NULL AND absent_since IS NULL) OR (absence_mark IN (…) AND absent_since IS NOT NULL)`, accepts an
  instant without a kind: with `absence_mark` NULL the second clause is NULL, and PostgreSQL accepts a CHECK that
  evaluates to NULL. `AnInvalidMark_IsRejectedByTheCheck(null, true)` caught it. Implemented as
  `… OR (absence_mark IS NOT NULL AND absence_mark IN (…) AND absent_since IS NOT NULL)` — the design's stated rule
  ("kind and instant always together", VR-003), same name, same accepted rows. db-design §2 text should be corrected to
  match (non-blocking finding m-1).
- **D-2.** Saving at the end of every list added a third commit to an empty installation's run and broke US-013
  `SynchronizationRunTests.ARun_IsCommittedAsRunning_BeforeItsOutcome` (two commits per run). The end-of-list save now
  runs only when a course is stored, the flag was set, or a skip record is removed — nothing else can have changed then.
  db-design §5 "one save" is kept for every list that writes something.
- **D-3 (test defects from TEST_WRITING, corrected without weakening).** `CourseSchemaTests.TheMigration_CreatesTheTable`
  still expected 19 columns; `TheChangeInstant_IsStored` used a helper that cannot cast a `timestamptz` scalar. Both
  assert exactly what they asserted before, correctly.

## 8. Open Decisions

None touched or required.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-059
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-059-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-1: db-design §2 ck_course_absence_mark text lets an instant without a kind pass (NULL IN … is NULL); implemented with an explicit absence_mark IS NOT NULL (report §7 D-1) — the design text should be corrected"
  closed_findings:
    - id: US-059/TEST_WRITING/INFO-1
      resolution: FIXED
      note: "CourseImportTests.ACourseClassroomStoppedReturning_IsLeftUntouched summary rewritten for v100; Course class remark rewritten"
      ref: tests/ClassroomAgent.Tests/Application/UseCases/CourseImportTests.cs
```
