---
artifact_type: ac_test_matrix
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T20:25:00Z
updated_at: 2026-10-26T20:25:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: docs/designs/database/US-058-db-design.md
    version: 1
  - path: docs/designs/database/US-058-entity-model.md
    version: 1
  - path: docs/tests/US-058-test-strategy.md
    version: 1
supersedes: null
---

# US-058 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`): **CWP** = `Application.UseCases.CourseWorkPresenceTests`,
**SCR** = `Application.UseCases.SkippedCourseRecordTests`, **INV** = `Application.UseCases.SyncDataHealthInvariantTests`,
**PER** = `Infrastructure.Persistence.SyncDataHealthPersistenceTests`, **HOST** = `Web.BackgroundServices.CourseWorkPresenceSyncTests`,
**MIG** = `Infrastructure.Persistence.AppUserMigrationTests`.

Status: **Red** = fails before IMPLEMENTATION for the expected reason (`NotImplementedException`, missing column/table,
unregistered repository, missing log line); **Green\*** = passes before IMPLEMENTATION, explained in the test generation report §5.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Graded item absent from full read: kept, marked with run instant, submission and grade unchanged; returned item unmarked | Unit | CWP | `AnItemGoogleNoLongerReturns_IsKeptWithItsGrade_AndMarkedWithTheRunInstant` | mark = run 2 instant; title, max, state, grade as before | Red |
| AC-001 | Mark sets the instant, says new, leaves the mirror | Unit | INV | `Marking_SetsTheInstant_ReturnsTrue_AndLeavesTheMirror` | true; instant set; fields unchanged | Red |
| AC-002 | Material absent: kept and marked | Unit | CWP | `AMaterialGoogleNoLongerReturns_IsKept_AndMarked` | mark = run 2 instant | Red |
| AC-001, AC-002 | Key is (resource, Google id) together | Unit | CWP | `TheMark_FollowsTheResourceAndTheGoogleId_Together` | material marked, coursework with same id not | Red |
| AC-003 | Marked item still absent keeps first instant | Unit | CWP | `AMarkedItemStillAbsent_KeepsTheFirstInstant` | mark = run 2 instant after run 3 | Red |
| AC-003 | Marking again keeps the first instant, returns false | Unit | INV | `MarkingAgain_KeepsTheFirstInstant_AndReturnsFalse` | false; first instant | Red |
| AC-004 | Marked item returned with a new title: unmarked, new title | Unit | CWP | `AMarkedItemThatComesBack_LosesTheMark_AndIsUpdated` | mark null; new title | Red |
| AC-004 | `UpdateFrom` clears the mark | Unit | INV | `UpdatingFromGoogle_ClearsTheMark_AndAppliesTheData`, `AnItemThatCameBack_IsMarkedAfreshWhenItGoesAgain` | mark null; later fresh mark | Red |
| AC-005 | Unchanged archive and out-of-range course: no new mark, existing marks kept; archive not read | Unit | CWP | `ACourseNotReadInFull_GainsAndLosesNoMark` | 12, 22 unmarked; 13, 23 keep run 2 instant | Red |
| AC-005 | Course reported gone (`404`): no mark change | Unit | CWP | `ACourseReportedGone_GainsAndLosesNoMark` | 2 unmarked; 3 keeps instant | Red |
| AC-006 | Run stopped by a non-retriable submissions read: no mark change (starts from an existing mark) | Unit | CWP | `ARunStoppedByTheCourseRead_ChangesNoMarkOfThatCourse` | run failed; item 2 keeps mark; item 1 unmarked | Red |
| AC-006 | Failure inside the course transaction rolls marks back (SPEC_REVIEW m-4) | Integration (host) | HOST | `AFailureInsideTheCourseTransaction_RollsItsMarksBack` | run failed; item 2 keeps run 2 mark; item 1 unmarked | Red |
| AC-007 | Out-of-range never-loaded course and unknown-state course recorded with id, name, reason, run instant; nothing else stored | Unit | SCR | `ACourseSkippedForEitherReason_IsRecorded` | two records as specified | Red |
| AC-007 | Unknown-state course with blank name → no name; name trimmed (VR-002) | Unit | SCR | `AnUnknownStateCourse_WithABlankName_IsRecordedWithNoName` | null; trimmed | Red |
| AC-007 | Recording a stored course leaves the course (FR-005.4) | Unit | SCR | `RecordingAStoredCourse_LeavesTheCourseAsItWas` | record has new name; course keeps old | Red |
| AC-007 | Entity holds id, name, reason, instant; refusals and cutting (VR-001 … VR-003) | Unit | INV | `Recording_HoldsWhatItWasGiven`, `ABlankGoogleId_IsRefused`, `TheGoogleId_IsTrimmedAndCut`, `TheName_IsTrimmed_AndBlankIsNoName`, `ALongName_IsCut`, `AnUndefinedReason_IsRefused` | as specified | Red |
| AC-007 | Real run records the unknown-state course in `sync_skipped_course` | Integration (host) | HOST | `ARealRun_RecordsTheSkippedCourse_AndTheLastReadInstant` | row with id, name, `unknown_course_state`, run instant | Red |
| AC-007 | Table, checks, unique index, repository round trip | Integration | PER | `TheTable_HasExactlyTheDesignedColumns`, `TheTable_HasTheDesignedKeysIndexesAndChecks`, `BothReasonCodes_AndNoName_AreAccepted`, `AViolatingRow_IsRejectedByItsCheck` (×3), `ASecondRecordForOneCourse_IsRejected`, `ARecord_RoundTripsThroughTheRepository` | as db-design §4 | Red |
| AC-008 | Repeated skip, other reason, new name: first instant, new name and reason, one record | Unit | SCR | `ARepeatedSkip_KeepsTheFirstInstant_AndTakesTheNewNameAndReason` | as specified | Red |
| AC-008 | Same skip again keeps the first instant | Unit | SCR | `TheSameSkipAgain_KeepsTheFirstInstant` | first instant | Red |
| AC-008 | `SkipAgain` in the entity; tracked change saved | Unit / Integration | INV, PER | `SkippingAgain_TakesTheNameAndReason_AndKeepsTheInstant`, `SkippingAgain_WithABlankName_StoresNoName`, `ARecordReadFromTheRepository_CanBeChangedAndRemoved` | as specified | Red |
| AC-009 | Recorded course read in full: record gone | Unit | SCR | `ARecordedCourseReadInFull_LosesItsRecord` | course stored; no record | Red |
| AC-010 | Recorded course no longer listed: record gone after the whole list; listed one kept | Unit | SCR | `ARecordedCourseNoLongerListed_LosesItsRecord_WhenTheListIsGoneThrough` | only course 8's record | Red |
| AC-011 | `404`, blank name, aged-out new course: no record | Unit | SCR | `OtherSkips_CreateNoRecord` | no record; nothing stored | Green\* |
| AC-011 | Recorded course gone mid-run or blank-named keeps its record (FR-007.4) | Unit | SCR | `ARecordedCourse_GoneMidRunOrBlankNamed_KeepsItsRecord` | both records keep run 1 instant | Red |
| AC-012 | One line per run counting newly marked and unmarked (1/1; already-marked not counted; 2/0 in the run before); no title, item id, person or grade in any line; no course id in the line | Integration (host) | HOST | `EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing` | as specified | Red |
| AC-013 | Full read stores run instant; out-of-range course keeps its own | Unit | CWP | `AFullRead_StoresTheRunInstant_AndASkippedCourseKeepsItsOwn` | run 2 / run 1 instants | Red |
| AC-013 | Unchanged archive keeps its last-read instant | Unit | CWP | `AnUnchangedArchive_KeepsItsLastReadInstant` | run 1 instant | Red |
| AC-013 | Entity: `RecordCompleteRead(readAt)` stores both facts; mirror update leaves it | Unit | INV | `AnImportedCourse_HasNoLastReadInstant`, `RecordingACompleteRead_StoresTheRunInstant_AndTheListedUpdateTime`, `UpdatingFromTheListing_LeavesTheLastReadInstant` | as specified | Red (first: Green\*) |
| AC-013 | Columns stored; real run writes `last_complete_read_at` | Integration | PER, HOST | `TheMarkColumn_IsANullableTimestamptzWithNoDefault`, `TheLastReadColumn_IsANullableTimestamptzWithNoDefault`, `TheMarkAndTheLastReadInstant_AreStored`, `ARealRun_RecordsTheSkippedCourse_AndTheLastReadInstant` | as db-design §2, §3 | Red |
| AC-014 | Recorded never-loaded old course, value corrected: aged out, record gone | Unit | SCR | `ARecordedCourseAgedOut_IsNotLoaded_AndLosesItsRecord` | skipped by age; no record | Red |
| AC-015 | Run stopped by a permission failure after committing course 1: its mark kept, unlisted record kept | Unit | SCR | `AStoppedRun_KeepsTheMarksItCommitted_AndRemovesNoUnlistedRecord` | item 5 marked; record 9 kept | Red |
| AC-015 | List failing part-way removes no unlisted record | Unit | SCR | `AListThatFailsPartWay_RemovesNoUnlistedRecord` | record 9 kept | Red |
| — (PC-2) | Migration list and table set | Integration | MIG | `TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` | 22 migrations, `_AddSyncDataHealthRecords` last; `sync_skipped_course` present | Red |
| — (DB_DESIGN note) | Existing `RecordCompleteRead` tests adapted to the new signature | Unit / Integration | `IncrementalSyncRuleTests`, `IncrementalSyncPersistenceTests` | 3 + 2 methods calling `RecordCompleteRead(T)` | unchanged expectations | Red (adapted) |

Every Acceptance Criterion AC-001 … AC-015 has at least one mapped test; none is untested.
