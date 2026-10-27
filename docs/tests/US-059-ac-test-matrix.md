---
artifact_type: ac_test_matrix
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T12:30:00Z
updated_at: 2026-10-27T12:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
  - path: docs/designs/database/US-059-db-design.md
    version: 1
  - path: docs/designs/database/US-059-entity-model.md
    version: 1
  - path: docs/tests/US-059-test-strategy.md
    version: 1
supersedes: null
---

# US-059 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`): **CLP** = `Application.UseCases.CourseListPresenceTests`,
**INV** = `Application.UseCases.CourseListPresenceInvariantTests`, **SWC** = `Application.UseCases.SaveWorkspaceConnectionAccountChangeTests`,
**RDR** = `Infrastructure.Google.GoogleClassroomReaderValueRangeTests`, **PER** = `Infrastructure.Persistence.CourseListMarksPersistenceTests`,
**HOST** = `Web.BackgroundServices.CourseListPresenceSyncTests`, **NEG** = `Web.BackgroundServices.NegativeValueSyncTests`,
**MIG** = `Infrastructure.Persistence.AppUserMigrationTests`, **SCH** = `Web.Persistence.SyncStateSchemaTests` / `Web.Persistence.CourseSchemaTests`.

Status: **Red** = fails before IMPLEMENTATION for the expected reason (`NotImplementedException` from the skeleton, a
missing column, a missing log line, a negative value passing the reader); **Green\*** = passes before IMPLEMENTATION,
explained in the test generation report §5.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Absent course kept with state, item, submission, grade; marked "not in Classroom" with run instant; item unmarked; listed course unmarked | Unit | CLP | `ACourseGoogleNoLongerLists_IsKeptWithItsData_AndMarkedNotInClassroom` | mark NotInClassroom @ run 2 | Red |
| AC-001 | Archived course is marked too (FR-001.1, BR-047) | Unit | CLP | `AnArchivedCourseNoLongerListed_IsMarkedToo` | mark @ run 2; state Archived | Red |
| AC-001 | Marking sets kind and instant together, returns true, leaves the mirror | Unit | INV | `MarkingAbsent_SetsKindAndInstant_ReturnsTrue_AndLeavesTheMirror` (×2) | as specified | Red |
| AC-001 | Real run stores `not_in_classroom` with the run instant = listed course's sighting | Integration (host) | HOST | `ARealRun_StoresTheMarkCode_TheRunInstant_AndTheSighting` | as specified | Red |
| AC-002 | Marked course still absent keeps the first instant | Unit | CLP | `AMarkedCourseStillAbsent_KeepsTheFirstInstant` | @ run 2 after run 3 | Red |
| AC-002 | Marking again keeps the first kind and instant, returns false | Unit | INV | `MarkingAgain_KeepsTheFirstKindAndInstant_AndReturnsFalse` (×3) | as specified | Red |
| AC-003 | Marked course back with a new name: no mark, new name, sighting = run | Unit | CLP | `AMarkedCourseThatComesBack_LosesTheMark_AndTakesTheNewName` | as specified | Red |
| AC-003 | `RecordListed` removes the mark, returns true; mirror update leaves mark and sighting | Unit | INV | `RecordingListed_StoresTheSighting_RemovesTheMark_AndReturnsTrue`, `RecordingListed_OnAnUnmarkedCourse_TakesTheLaterSighting_AndReturnsFalse`, `UpdatingFromGoogle_LeavesTheMarkAndTheSighting` | as specified | Red |
| AC-004 | Unchanged archive, out-of-range course, unknown-state course listed: none marked, each sighted | Unit | CLP | `ListedCoursesTheRunDoesNotRead_AreNotMarked_AndAreSighted` | no marks; sighting = run 2 | Red |
| AC-004 | Marked course listed but skipped for values loses its mark (I-7) | Unit | CLP | `AMarkedCourseOnTheListButNotRead_LosesItsMark` | no mark; sighting = run 3 | Red |
| AC-005 | List failing part-way: nothing marked or unmarked, sighting and flag kept | Unit | CLP | `ARunWhoseListFailsPartWay_MarksAndUnmarksNothing` | run failed; marks as before | Red |
| AC-005 | Run stopped by a course read: nothing marked, no sighting | Unit | CLP | `ARunStoppedByACourseRead_MarksNothing_AndRecordsNoSighting` | run failed; as before | Red |
| AC-006 | Empty list: no mark gained or lost, no sighting, skip record kept, flag set, run completes | Unit | CLP | `AnEmptyListWhileCoursesAreStored_MarksNothing_KeepsSkipRecords_AndSetsTheFlag` | as specified | Red |
| AC-006 | List with only id-less entries is empty (I-4) | Unit | CLP | `AListWithOnlyIdlessEntries_IsEmpty` | no mark; flag set | Red |
| AC-006 | Real run: flag stored, no mark, one line with the stored count (FR-007.2) | Integration (host) | HOST | `AnEmptyList_SetsTheFlag_MarksNothing_AndLogsTheStoredCount` | flag true; numbers `[2]` | Red |
| AC-006, AC-007 | Flag set and cleared; Begin/Complete/Fail leave it | Unit | INV | `ANewState_HasNoEmptyListFlag`, `RecordingAnEmptyList_SetsTheFlag_AndANonEmptyOne_ClearsIt`, `BeginningCompletingAndFailingARun_LeaveTheFlag` | as specified | Red (first: Green\*) |
| AC-007 | Non-empty list clears the flag and marks absent courses | Unit | CLP | `AFlaggedEmptyList_IsClearedByANonEmptyList_AndAbsentCoursesAreMarked` | flag false; mark @ run 3 | Red |
| AC-008 | No course stored, empty list: run completes, flag cleared | Unit | CLP | `AnEmptyListWithNoCourseStored_CompletesAndClearsTheFlag` | flag false | Red |
| AC-009 | Negative assigned grade, draft grade, maximum: course skipped, skip record, run completes, ids logged without values; stored course unchanged; ordinary course loaded | Integration (host + real adapter) | NEG | `CoursesWithNegativeValues_AreSkippedAndRecorded_AndTheRunCompletes` | as specified | Red |
| AC-009 | Reader refuses every negative as `ValueOutOfRange` (VR-005) | Adapter | RDR | `ANegativeValue_IsAValueOutOfRangeFailure` (×12) | `ValueOutOfRange` | Red |
| AC-009 | The failure carries no value and no identifier (SC-10) | Adapter | RDR | `TheNegativeValueFailure_CarriesNoValueAndNoIdentifier` (×3) | as specified | Red |
| AC-009 | Zero is a value (FR-008.3) | Adapter / host | RDR, NEG | `Zero_IsConvertedAsToday` (×3), `ZeroValues_AreStored_AndTheCourseIsNotSkipped` | 0 stored; no skip | Green\* |
| AC-010 | One line per run with the counts 1/0/0 then 1/0/1 (multiset); nothing named in any line | Integration (host) | HOST | `EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` | `[0,0,1]`, `[0,1,1]`; no names/ids/persons/grade | Red |
| AC-011 | First save, same account in other case — no change; another account — the save's instant; again same — kept | Unit | SWC | `OnlyASaveWithAnotherAccount_RecordsItsInstantAsTheChange`, `SavingTheSameAccountAgain_KeepsThePreviousChangeInstant` | as specified | Red |
| AC-011 | One audit row per save, no new event (FR-002.4) | Unit | SWC | `AnAccountChange_WritesNoAuditEventOfItsOwn` | 2 × `WorkspaceConnectionSaved` | Red |
| AC-011 | Entity: change instant only for another address; latest kept; outside-domain still refused | Unit | INV | `ACreatedConnection_HasNoChangeInstant`, `ChangingToAnotherAccount_RecordsTheInstant`, `ChangingToTheSameAccount_InOtherCaseOrWithSpaces_KeepsThePreviousInstant`, `ChangingTheAccountTwice_KeepsTheLatestInstant`, `ChangingToAnAccountOutsideTheDomain_IsStillRefused` | as specified | Red (first: Green\*) |
| AC-011 | Change instant stored through EF Core | Integration | PER | `TheChangeInstant_IsStored` | `technical_account_changed_at` = T | Red |
| AC-012 | Course lost after the change: "not visible" @ run, data kept; listed course sighted | Unit | CLP | `ACourseLostAfterTheAccountChanged_IsMarkedNotVisible` | as specified | Red |
| AC-012 | Real run reads the stored change instant: `not_visible_to_technical_account` | Integration (host) | HOST | `ARealRun_AfterTheAccountChanged_MarksALostCourseNotVisible` | as specified | Red |
| AC-012, AC-013, AC-017 | Rule: sighting vs change instant (strict), none, never sighted | Unit | INV | `TheRule_ChoosesTheMark_FromTheSightingAndTheChange` (×6), `TheRule_SeesOneTickBeforeTheChange_AsBefore` | as specified | Red |
| AC-013 | Seen by the new account, then lost: "not in Classroom" | Unit | CLP | `ACourseTheNewAccountSaw_IsNotInClassroomWhenLost` | mark NotInClassroom @ run 3 | Red |
| AC-014 | "Not visible" keeps its instant while absent; listed again: no mark, sighting = that run | Unit | CLP | `ANotVisibleCourse_KeepsItsInstant_AndLosesTheMarkWhenListedAgain` | as specified | Red |
| AC-015 | Marked "not in Classroom", account changes, still absent: keeps the kind and first instant | Unit | CLP | `ACourseMarkedNotInClassroom_KeepsThatMarkAfterTheAccountChanges` | NotInClassroom @ run 2 | Red |
| AC-016 | Unchanged archive listed, not read: sighting = run, last complete read unchanged | Unit | CLP | `AnUnchangedArchiveOnTheList_RecordsTheRunInstantAsItsLastSighting` | as specified | Red |
| AC-017 | Never-sighted course: "not visible" with a change, "not in Classroom" without | Unit | CLP | `ACourseNeverSighted_IsMarkedByWhetherAChangeIsRecorded` (×2) | as specified | Red |
| AC-018 | Listed then `404`: no mark, sighted | Unit | CLP | `ACourseListedButGoneDuringItsReads_IsNotMarked_AndIsSighted` | as specified | Red |
| VR-003 | Undefined mark refused; course unmarked | Unit | INV | `AnUndefinedMark_IsRefused`, `AnImportedCourse_HasNoMarkAndNoSighting` | as specified | Red (second: Green\*) |
| db-design §6 R1 | Observation instants taken before the connection read | Unit | CLP | `TheRunInstant_IsTakenBeforeTheConnectionIsRead` | sighting and last read = instant before | Red |
| db-design §6 R2 | Change saved during the list seen at its end: "not visible" | Unit | CLP | `AnAccountChangeSavedDuringTheRun_IsSeenAtTheEndOfTheList` | NotVisible @ run 2 | Red |
| db-design §2 … §4, §7 | Column shapes, defaults, check accepts/rejects, codes stored, `GetAllAsync` tracked, flag round trip | Integration | PER | `TheCourseColumns_AreAsDesigned`, `TheChangeInstantColumn_IsANullableTimestamptzWithNoDefault`, `TheFlagColumn_IsANotNullBooleanDefaultingToFalse`, `AValidMark_IsAccepted` (×3), `AnInvalidMark_IsRejectedByTheCheck` (×4), `TheMarksAndTheSighting_AreStoredAsTheirCodesAndInstants`, `EveryCourse_IsLoadedTracked_WithItsMark`, `TheEmptyListFlag_IsStoredAndReadBack` | as db-design | Red |
| db-design §7 | Migration `AddCourseListMarks` is the 23rd; sync_state 15 columns; course checks | Integration | MIG, SCH | `TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent`, `TheMigration_CreatesTheTable`, `TheCheckConstraints_AreTheStateVocabularyAndTheAbsenceMark` | as db-design | Red |

Every Acceptance Criterion AC-001 … AC-018 maps to at least one test.
