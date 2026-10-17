---
artifact_type: ac_test_matrix
story: US-053
version: 1
status: DRAFT
created_at: 2026-10-16T16:40:00Z
updated_at: 2026-10-16T16:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-053-failed-sync-run-recorded.md
    version: null
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/tests/US-053-test-strategy.md
    version: 1
supersedes: null
---

# US-053 Acceptance Criteria → Test Matrix

New test class: `tests/ClassroomAgent.Tests/Web/BackgroundServices/FailedRunRecordedTests.cs` (`FRR`).
All tests are integration tests through the real host and PostgreSQL (TC-2), Google ports substituted (TC-4).
Status is the red-phase status recorded in `docs/evidence/US-053-test-generation-report.md`.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Second course rejected by a test CHECK constraint | Integration | FRR | `ARejectedCourseWrite_IsRecordedAsAFailedRunAtTheClassroomStep` | `failed` / `Unexpected` / `classroom`, count 1, course 1 stored, nothing of course 2, no success or watermark | see report |
| AC-001 (FR-005) | Host log after the rejection | Integration | FRR | `ARejectedCourseWrite_LogsTheFailureLine_AndNotTheEscapedLine` | one `SyncRunFailed` (Error, `Unexpected`, `Classroom`, no course id / constraint name); no `SyncRunException` | see report |
| AC-002 | Second Meet page rejected by a test CHECK constraint | Integration | FRR | `ARejectedMeetPageWrite_IsRecordedAsAFailedRunAtTheMeetStep` | `failed` / `Unexpected` / `meet`; course and page 1 stored; nothing of page 2; watermark null | see report |
| AC-003 | Linking insert rejected by a trigger | Integration | FRR | `ARejectedLinkingWrite_IsRecordedAsAFailedRunAtTheLinkingStep_AndKeepsWhatEarlierStepsCommitted` | `failed` / `Unexpected` / `linking`; Classroom course and Meet session stay; no link, no audit row | see report (may pass before the change) |
| AC-004 | "Last synchronization" query after AC-001's state | Integration | FRR | `AfterARejectedCourseWrite_TheLastSynchronizationBlockShowsTheFailure` | `Failed`, `Unexpected`, `Classroom` — not `Running` | see report |
| AC-005 | Constraint dropped, next scheduled run | Integration | FRR | `AfterARejectedCourseWrite_TheNextRunCompletes_WhenTheCauseIsGone` | `completed`, no `LastError`, no `FailedStep`, course 2 stored, count 2 | see report |
| AC-006 | Existing synchronization, Meet, linking, erasure and account tests; host stop not a failure (FR-009); US-032/US-048 handling (FR-008) | Mixed | existing suite, incl. `SyncScheduleTests`, `MeetLinkingHostTests`, `AppUserConflictTranslationTests`, `SynchronizationFailureHandlingTests` | full `dotnet test` | stays green | see report |
| AC-007 | Non-DB exception before the course's first save (blank roster user id) | Integration | FRR | `ANonDatabaseFailureBeforeTheCourseSaved_StoresNothingOfThatCourse` | `failed` / `Unexpected` / `classroom`; only course 1 and its person stored | see report |
| AC-007 | Non-DB exception after the course's first in-tx save (blank item title) | Integration | FRR | `ANonDatabaseFailureAfterTheCourseSavedInItsTransaction_StoresNothingOfThatCourse` | `failed` / `Unexpected` / `classroom`; nothing of course 2, no `course_work` row | see report |

## Validation rules

| Rule | Covered by |
|---|---|
| VR-001 | stored `last_error` / `failed_step` in AC-001, AC-002, AC-003, AC-007 tests |
| VR-002 | `processed_count` 1 and `finished_at` set in AC-001 |
| VR-003 | existing `SyncStateInvariantTests` (AC-006) |
