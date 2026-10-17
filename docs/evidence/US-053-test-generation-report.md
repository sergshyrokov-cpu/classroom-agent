---
artifact_type: test_generation_report
story: US-053
version: 1
status: DRAFT
created_at: 2026-10-16T16:55:00Z
updated_at: 2026-10-16T16:55:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/designs/api/US-053-api-design.md
    version: 1
  - path: docs/designs/database/US-053-db-design.md
    version: 1
  - path: docs/tests/US-053-test-strategy.md
    version: 1
  - path: docs/tests/US-053-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-053 Test Generation Report

## 1. Files

| Change | File |
|---|---|
| Created | `tests/ClassroomAgent.Tests/Web/BackgroundServices/FailedRunRecordedTests.cs` (8 tests) |
| Created | `docs/tests/US-053-test-strategy.md`, `docs/tests/US-053-ac-test-matrix.md`, this report |
| Modified | none |

**Compile-only skeleton: none.** The tests reach the discard of FR-002 through the run; no new
declaration is referenced (spec I-2, DB design). No file under `src/` was touched.

## 2. Commands

Run by a `cheap-worker` subagent (delegated; test design, test code and the red-phase verdict were
written inline by the test-writer):

1. `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors; no fix needed.
2. `dotnet test --filter FullyQualifiedName~FailedRunRecordedTests` — 8 tests: 1 passed, 7 failed.
3. `dotnet test ClassroomAgent.sln` — 6076 total, 6069 passed, 7 failed, 0 skipped. The 7 failures are
   exactly the 7 new tests below; no existing test fails.

## 3. Red phase — per test

| Test | Result | Cause | Verdict |
|---|---|---|---|
| `ARejectedCourseWrite_IsRecordedAsAFailedRunAtTheClassroomStep` (AC-001) | fail | "The run's failure save threw: the host wrote SyncRunException and sync_state stays 'running'." | Expected — the defect itself (spec §1) |
| `ARejectedCourseWrite_LogsTheFailureLine_AndNotTheEscapedLine` (AC-001, FR-005) | fail | same | Expected |
| `ARejectedMeetPageWrite_IsRecordedAsAFailedRunAtTheMeetStep` (AC-002) | fail | same | Expected — the Meet step has the same defect |
| `AfterARejectedCourseWrite_TheLastSynchronizationBlockShowsTheFailure` (AC-004) | fail | same (the state AC-004 reads never arises) | Expected |
| `AfterARejectedCourseWrite_TheNextRunCompletes_WhenTheCauseIsGone` (AC-005) | fail | same (first run never finishes) | Expected |
| `ANonDatabaseFailureAfterTheCourseSavedInItsTransaction_StoresNothingOfThatCourse` (AC-007b) | fail | same: the failure save re-inserts the staged item of the rolled-back course, the foreign key rejects it, the exception escapes | Expected |
| `ANonDatabaseFailureBeforeTheCourseSaved_StoresNothingOfThatCourse` (AC-007a) | fail | `Assert.Equal() Failure: Expected: 0, Actual: 1` at the `course` count for course 2 (line 301) — the run **was** recorded as failed, but the failure save committed the staged course outside its transaction | Expected — **proves spec I-3 / SPEC_REVIEW m-3**: the partial write is real, not only code reading |
| `ARejectedLinkingWrite_IsRecordedAsAFailedRunAtTheLinkingStep_AndKeepsWhatEarlierStepsCommitted` (AC-003) | pass | US-032 already detaches the failed link and its audit row | Expected — spec AC-003 says it may pass before the change; kept as a regression test |

Unexpected failures: none. Invalid tests / environment failures: none.

The AC-003 pass is not vacuous: the same assertions on `failed_step = 'linking'` fail if the linking
step stops writing its failure (as US-032's own `AFailureOfTheLinkingStep_…` test shows); here it
additionally pins that a Classroom course committed by the same run stays.

## 4. Coverage

Every AC maps to at least one test (`ac_test_matrix`). AC-006 is the full suite staying green after
IMPLEMENTATION (6069 existing tests green now). SPEC_REVIEW m-1: AC-007 is reachable without a seam
in `src/` — the domain guards of `ClassroomParticipant.Import` and `CourseWork.Import` throw inside the
course's transaction on synthetic data from the substituted Classroom port. I-5: the database
rejections are CHECK constraints / a trigger the tests add to their own database only.

Untested Acceptance Criteria: none.

## 5. Notes for IMPLEMENTATION

- AC-007a shows the discard must also drop entries that were never saved (course and person `Added`,
  no save yet), not only those a failed save left behind.
- AC-007b and AC-001 show it must drop entries staged inside a rolled-back transaction whose first
  save succeeded (course rows `Unchanged` with rolled-back ids, item `Added`); the failure save must
  write `sync_state` only.

## 6. Open Decisions

None.

## 7. Overall result

PASS — tests compile, existing suite green, every new behaviour test fails only for the missing
behaviour, AC-003 passes as the specification foresees.
