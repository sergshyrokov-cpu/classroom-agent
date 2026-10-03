---
artifact_type: ac_test_matrix
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T19:26:15Z
updated_at: 2026-10-03T19:26:15Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-037-entity-model.md
    version: 1
  - path: docs/tests/US-037-test-strategy.md
    version: 1
supersedes: null
---

# US-037 Acceptance Criteria → Test Matrix

"Status" is the red-phase status, recorded before IMPLEMENTATION:

- **RED** fails for missing behaviour (`NotImplementedException`, an
  unregistered service, a missing column or index);
- **GUARD** passes already, because it protects an existing property the Story
  must not break (explained in the test-generation report).

Classes are under `tests/ClassroomAgent.Tests/`: `Application/UseCases/` (A),
`Infrastructure/Persistence/` (I), `Web/BackgroundServices/` (B),
`Web/Logging/` (L), `Web/UseCases/` (W), `Web/Persistence/` (P),
`Architecture/` (X).

| AC | Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | expired course gone with everything, active and archived | Integration | A `RetentionPurgeTests` | `AnExpiredCourse_IsDeletedWithEverythingInIt_WhateverItsState` (×2) | course, coursework, memberships, submissions gone | RED |
| AC-001 | the deleted rows cross only `Restrict` FKs | Schema | I `RetentionPurgeSchemaTests` | `EveryForeignKeyThePurgeCrosses_StaysRestrict` (×3) | `confdeltype = 'r'` | GUARD |
| AC-002 | recent coursework update keeps the course | Integration | A `RetentionPurgeTests` | `ARecentCourseworkUpdate_KeepsTheWholeCourse` | course whole | RED |
| AC-002 | recent coursework creation keeps the course | Integration | A `RetentionPurgeTests` | `ARecentCourseworkCreation_KeepsTheWholeCourse` | course whole | RED |
| AC-002 | recent submission keeps the course | Integration | A `RetentionPurgeTests` | `ARecentSubmissionChange_KeepsTheWholeCourse` | course whole | RED |
| AC-002 | recent course update keeps the course | Integration | A `RetentionPurgeTests` | `ARecentCourseUpdate_KeepsTheCourse` | course whole | RED |
| AC-002 | latest-of rule | Unit | A `RetentionRuleTests` | `TheLatestActivity_IsTheLatestDate`, `NullDates_AreIgnored` | max, nulls ignored | RED |
| AC-003 | expired leaver gone with submissions; recent leaver, roster member, course stay | Integration | A `RetentionPurgeTests` | `AnExpiredLeaver_IsDeletedWithTheirSubmissions_AndOnlyThey` | as stated | RED |
| AC-003 | a leaver's submissions in another course stay | Integration | A `RetentionPurgeTests` | `ALeaversSubmissionsInAnotherCourse_Stay` | other course untouched | RED |
| AC-003 | leaver index | Schema | I `RetentionPurgeSchemaTests` | `TheLeaverQuery_HasAPartialIndexOverOffRosterRows` | partial index on `last_seen_at` where `on_roster = false` | RED |
| AC-003 | leaver index listed | Schema | P `CourseMembershipSchemaTests` | `TheRequiredIndexes_Exist` (updated) | five indexes | RED |
| AC-004 | orphan deleted, participant enrolled elsewhere kept | Integration | A `RetentionPurgeTests` | `AnOrphanedParticipant_IsDeleted_AndOneStillEnrolledElsewhereIsKept` | as stated | RED |
| AC-004 | leaver with no other course deleted as a participant | Integration | A `RetentionPurgeTests` | `ALeaverWithNoOtherCourse_IsDeletedAsAParticipantInTheSameRun` | participant gone | RED |
| AC-005 | expired Admin, disabled Dean, never-signed-in Dean gone; used accounts stay; audit ids kept | Integration | A `RetentionPurgeTests` | `UnusedAccounts_AreDeleted_AndAuditRowsKeepTheirIds` | as stated | RED |
| AC-005 | no purge index on `app_user` | Schema | I `RetentionPurgeSchemaTests` | `AppUser_GetsNoPurgeIndex` | two indexes only | GUARD |
| AC-006 | deleted Admin signs in again, gets a new account | Integration (HTTP) | W `AdminReturnsAfterPurgeTests` | `ADeletedAdmin_SignsInAgain_AndGetsANewAccount` | new id, role admin, old audit row keeps old id | RED |
| AC-007 | exactly rows older than the cutoff deleted, incl. one naming a purged course | Integration | A `RetentionPurgeTests` | `ExactlyTheAuditRowsOlderThanTheCutoff_AreDeleted` | as stated | RED |
| AC-007 | audit repository add-only | Architecture | X `AuditDeletionConfinementTests` | `TheAuditRepository_CanOnlyAdd` | only `Add` | GUARD |
| AC-007 | set-based deletes only in the purge store | Architecture | X `AuditDeletionConfinementTests` | `SetBasedDeletes_ExistOnlyInThePurgeStore` | store holds `ExecuteDelete`, nothing else does | RED |
| AC-007 | no other code removes or updates audit rows | Architecture | X `AuditDeletionConfinementTests` | `NoOtherCode_TouchesAuditRows` | no offender | GUARD |
| AC-007 | entity immutable | Unit | A `RetentionPurgeAuditEventTests` | `TheEntity_StaysImmutable` | no public setter or mutator | GUARD |
| AC-007 | audit index | Schema | I `RetentionPurgeSchemaTests`, I `InstallationAuditEventSchemaTests` | `TheAuditRowDelete_HasItsIndexOnOccurredAt`, `TheTable_HasOnlyThePrimaryKeyAndThePurgeIndex` (updated) | `ix_audit_event_occurred_at` | RED |
| AC-008 | one row: system, no target, succeeded, no request id, five counts | Integration | A `RetentionPurgeTests` | `TheRun_WritesOneAuditEventWithTheCounts` | counts (1,2,3,1,3) | RED |
| AC-008 | nothing removed → one row of zeros | Integration | A `RetentionPurgeTests` | `ARunThatRemovesNothing_StillWritesOneAuditEvent` | (0,0,0,0,0) | RED |
| AC-008 | each run its own row, which the next run does not delete | Integration | A `RetentionPurgeTests` | `EachRun_WritesItsOwnRow_AndTheRowsSurviveTheNextRun` | two rows, `AuditRows` = 0 | RED |
| AC-008 | no personal data in the audit trail | Integration | A `RetentionPurgeTests` | `TheAuditEvent_CarriesNoPersonalData` | no id, name, email, grade | RED |
| AC-008 | factory shape and negative counts | Unit | A `RetentionPurgeAuditEventTests` | `ThePurgeRow_IsASystemActionWithNoTargetAndNoRequest`, `AZeroRun_IsAValidRow`, `ANegativeCount_IsRejected` (×5) | as stated | RED |
| AC-008 | schema of the counts and the purge row | Schema | I `RetentionPurgeSchemaTests` | `AValidPurgeRow_IsAccepted`, `APurgeRowOfZeros_IsAccepted`, `APurgeRowMissingACount_IsRejected` (×5), `AnotherActionCarryingACount_IsRejected` (×2), `ANegativeCount_IsRejected` (×5), `APurgeRowWithAnotherShape_IsRejected` (×4), `TheActionCode_IsOnTheClosedList`, `OrdinaryRows_CarryNoCount` | as db-design §2.4 | RED |
| AC-008 | columns and constraints listed | Schema | I `InstallationAuditEventSchemaTests` | `Columns_MatchTheDesign`, `PrimaryKeyAndConstraints_AreNamedAsTheDesignFixesThem` (updated) | five columns, four constraints | RED |
| AC-009 | read-only (3 causes): deletes, records the run, no Google call | Integration | A `RetentionPurgeReadOnlyTests` | `InReadOnlyMode_ThePurgeDeletesAndRecordsItsRun` (×3) | as stated | RED |
| AC-009 | declared as the BR-026 write | Architecture | A `RetentionPurgeReadOnlyTests` | `ThePurge_IsDeclaredAsTheRetentionPurgeServiceWrite` | `RetentionPurge` | RED |
| AC-010 | failing course whole, others gone, counted out, named by id, retried next run | Integration | A `RetentionPurgeTests` | `AFailingCourse_IsLeftWhole_AndTheOthersAreDeleted_AndTheNextRunRetries` | as stated | RED |
| AC-010 | failing leaver delete rolled back for that course only | Integration | A `RetentionPurgeTests` | `AFailingLeaverDelete_IsRolledBackForThatCourseOnly` | as stated | RED |
| AC-010 | one `Error` line with the internal id, no Google id or message | Host | L `RetentionPurgeLoggingTests` | `AFailedCourse_IsLoggedOnceAsAnErrorWithItsInternalId` | as stated | RED |
| AC-011 | gate semantics: purge vs scheduled run, request remembered, signal, `IsRunning` unchanged | Unit | B `RetentionPurgeCoordinationTests` | all nine methods | as named | RED |
| AC-011 | purge due during a run waits, logs, runs after it | Host | B `RetentionPurgeScheduleTests` | `APurgeDueDuringASynchronizationRun_WaitsForItsEnd` | as stated | RED |
| AC-012 | first run shortly after start | Host | B `RetentionPurgeScheduleTests` | `TheFirstPurge_RunsShortlyAfterStart` | one row at start time | RED |
| AC-012 | next run 24 h later; none before | Host | B `RetentionPurgeScheduleTests` | `TheNextPurge_IsDue24HoursLater`, `BeforeTheInterval_ThereIsNoSecondPurge` | as stated | RED |
| AC-012 | prompt stop | Host | B `RetentionPurgeScheduleTests` | `TheHost_StopsPromptly` | stop within the real-time limit | RED |
| AC-012 | start/completion logged | Host | L `RetentionPurgeLoggingTests` | `ARun_LogsItsStartAndItsCompletionWithTheCounts` | two Information lines | RED |
| AC-013 | synchronization keeps a leaver's membership and submissions | Unit | A `SynchronizationNeverDeletesTests` | `APersonWhoLeftTheRoster_KeepsTheirMembershipAndSubmissions` | membership off roster, submission stays | GUARD |
| AC-014 | strict boundary: course, leaver, account, audit row | Integration | A `RetentionPurgeTests` | `TheCourseBoundary_IsStrict`, `TheLeaverBoundary_IsStrict`, `TheAccountBoundary_IsStrict`, `ExactlyTheAuditRowsOlderThanTheCutoff_AreDeleted` | cutoff kept, earlier deleted | RED |
| AC-014 | rule-level boundary and cutoff | Unit | A `RetentionRuleTests` | `TheCutoff_IsNowMinusNCalendarYears`, `TheCutoff_FollowsCalendarYears_OnALeapDay`, `ADateOnTheCutoff_IsNotExpired`, `ADateOneTickBeforeTheCutoff_IsExpired`, `ADateAfterTheCutoff_IsNotExpired`, `DatesWithOffsets_AreComparedAsInstants` | as named | RED |
| AC-015 | no Google date → judged by `created_at` | Integration | A `RetentionPurgeTests` | `ACourseWithNoGoogleDate_IsJudgedByWhenItWasImported` | old import gone, recent kept | RED |
| AC-015 | a Google date outranks a recent import | Integration | A `RetentionPurgeTests` | `AnOldGoogleDate_OutranksARecentImport` | course gone | RED |
| AC-015 | no date → no activity at rule level | Unit | A `RetentionRuleTests` | `WithNoDateAtAll_ThereIsNoActivity` | null | RED |
| AC-016 | synchronization's refusal unchanged | Unit (existing) | A `CourseAgeRuleTests` (US-015) | `UnknownCourseOlderThanN_ImportsNothing`, `UnknownCourseWithinN_IsImportedInFull`, `LastActivity_TakesTheLatestOfCourseWorkAndSubmissionDates`, `CourseExactlyAtTheBoundary_IsImported`, `AlreadyImportedCourse_IsUpdatedHoweverOld` | unchanged | GUARD |
| AC-017 | failing step rolled back; later step and audit event still run | Integration | A `RetentionPurgeTests` | `AFailingStep_IsRolledBack_AndTheLaterStepsStillRun` | accounts kept, old audit row gone, counts (0, 1) | RED |
| — | no purged data in any log line | Host | L `RetentionPurgeLoggingTests` | `NoLogLine_CarriesWhatWasPurged` | none present | RED |

Every Acceptance Criterion AC-001 … AC-017 has at least one mapped test.
