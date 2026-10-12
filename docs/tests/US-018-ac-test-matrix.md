---
artifact_type: ac_test_matrix
story: US-018
version: 2
status: DRAFT
created_at: 2026-10-12T06:40:00Z
updated_at: 2026-10-12T08:40:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/designs/database/US-018-db-design.md
    version: 1
  - path: docs/designs/database/US-018-entity-model.md
    version: 1
  - path: docs/tests/US-018-test-strategy.md
    version: 1
supersedes: 1
---

# US-018 Acceptance Criteria → Test Matrix

Classes: **Run** = `Application/UseCases/IncrementalSyncRunTests`, **Rule** =
`Application/UseCases/IncrementalSyncRuleTests`, **Db** =
`Infrastructure/Persistence/IncrementalSyncPersistenceTests`, **Host** =
`Web/BackgroundServices/IncrementalSyncHostTests`. Status is the red-phase
expectation: **Red** fails until IMPLEMENTATION, **Guard** passes now and must
stay green (behaviour this Story must not break).

| AC | Scenario | Level | Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Unchanged archive not read | Unit | Run | `AnUnchangedArchivedCourse_IsNotRead` | no roster/coursework/submission read; list read once | Red |
| AC-001 | Stored data unchanged, nobody off roster | Unit | Run | `AnUnchangedArchivedCourse_KeepsItsStoredDataExactly` | name, value, membership (on roster, last seen), item, submission unchanged | Red |
| AC-001 | Counted as skipped, not processed (I-1) | Unit | Run | `AnUnchangedArchivedCourse_IsCountedAsSkipped_NotAsProcessed` | skipped 1, processed 0 in outcome and row | Red |
| AC-001 | Sub-µs ticks still equal (VR-001) | Unit | Run | `AnUnchangedArchivedCourse_WithSubMicrosecondTicks_IsStillSkipped` | not read, skipped 1 | Red |
| AC-001 | Only the archive skipped among others | Unit | Run | `OnlyTheUnchangedArchivedCourse_IsSkipped_AmongOthers` | archive skipped, active read | Red |
| AC-001 | Skip rule | Unit | Rule | `AnArchivedCourse_WithTheRememberedValue_IsUnchanged`, `AFinerListedValue_EqualsTheTruncatedStoredOne`, `TheSameInstantInAnotherOffset_IsEqual` | true | Red |
| AC-002 | Changed archive read, new value stored | Unit | Run | `AChangedArchivedCourse_IsReadInFull_AndRemembersTheNewUpdateTime` | read in full; value T2 | Red |
| AC-002 | 1 µs change is a change | Unit | Run | `AnArchivedCourse_ChangedByOneMicrosecond_IsReadInFull` | read in full | Guard |
| AC-002 | No stored value (pre-Story course) | Unit | Run | `AnArchivedCourse_WithNoRememberedValue_IsReadInFull_AndThenRemembers` | read in full; value T | Red |
| AC-002 | No updateTime from Google | Unit | Run | `AnArchivedCourse_ListedWithoutUpdateTime_IsReadInFullEveryRun` | nothing remembered; read in full | Red |
| AC-002 | Value at storage precision | Unit | Run | `TheRememberedValue_IsKeptAtStoragePrecision` | truncated to µs | Red |
| AC-002 | Rule: change / missing values | Unit | Rule | `OneMicrosecondApart_IsAChange`, `AMissingValue_IsNeverEqual` (3) | false | Red |
| AC-002 | Course entity records the listed value | Unit | Rule | `AnImportedCourse_RemembersNothingYet`, `RecordingACompleteRead_RemembersTheListedUpdateTime_AtStoragePrecision`, `RecordingACompleteRead_WithoutAnUpdateTime_RemembersNone`, `UpdatingFromTheListing_LeavesTheRememberedValue` | as named | Red |
| AC-002 | Storage normalisation | Unit | Rule | `SubMicrosecondTicks_AreTruncated` (3), `WholeMicroseconds_AreKept`, `TheValue_IsInUtc`, `Normalising_IsIdempotent` | truncated, UTC | Red |
| AC-002 | Column, round-trip, lookup | Integration | Db | `TheCourseColumn_IsANullableTimestamptzWithNoDefault`, `AnExistingCourse_HasNoRememberedValue`, `AnUnknownCourse_HasNoRememberedValue`, `ACourseNeverCompletelyRead_HasNoRememberedValue`, `TheRememberedValue_RoundTripsAtStoragePrecision`, `TheLookup_AttachesNoEntity` | as named | Red |
| AC-002 | No index added (db-design §2) | Integration | Db | `NoIndexIsAdded` | no index named for the new columns | Guard |
| AC-003 | Non-archived states read in full | Unit | Run | `ANonArchivedCourse_WithAnUnchangedUpdateTime_IsReadInFull` (4 states) | read in full; skipped 0; value T | Red |
| AC-003 | Listed state decides | Unit | Run | `TheListedState_Decides_NotTheStoredOne` | read in full | Guard |
| AC-003 | Rule: only ARCHIVED | Unit | Rule | `ANonArchivedState_IsNeverUnchanged` (4) | false | Red |
| AC-004 | Restored course read next run | Unit | Run | `ASkippedCourse_RestoredToActive_IsReadByTheNextRun` | skipped, then read; state Active; value T2 | Red |
| AC-005 | Failed read keeps value; next run reads | Unit | Run | `AFailedCourseRead_DoesNotAdvanceTheRememberedValue_AndTheNextRunReadsItInFull` (gone/transient/permission × 3 reads) | value stays T; next run reads; value T2 | Red |
| AC-005 | New course whose read failed | Unit | Run | `ANewCourseWhoseReadFailed_IsReadInFullByTheNextRun` | read in full; value T | Red |
| AC-006 | First run full, records start (I-3) | Unit | Run | `TheFirstRun_IsFull_AndRecordsItsStart` | LastFullRunAt = start | Red |
| AC-006 | Exactly 7 days → full | Unit | Run | `SevenDaysAfterTheLastFullRun_TheRunIsFull_AndReadsTheUnchangedArchive` | read; time replaced | Red |
| AC-006 | 7 days − 1 tick → not full | Unit | Run | `JustUnderSevenDays_TheRunIsNotFull_AndLeavesTheFullRunTime` | skipped; time kept | Red |
| AC-006 | Failed full run → next full (OD-002 a) | Unit | Run | `AFailedFullRun_LeavesTheFullRunTime_AndTheNextRunIsFullAgain`, `AFailedFirstRun_LeavesNoFullRunTime_AndTheNextRunIsFull` | time unchanged; next run reads | Red |
| AC-006 | Non-full success keeps time | Unit | Run | `ASuccessfulNonFullRun_LeavesTheFullRunTime` | time kept | Red |
| AC-006 | Future time → full (I-5) | Unit | Run | `AFullRunTimeInTheFuture_MakesTheRunFull` | read; skipped 0; time not moved forward (completion is US-031's watermark rule — Owner, IMPLEMENTATION 2026-10-12) | Red |
| AC-006 | Full run skips nothing (FR-008) | Unit | Run | `AFullRun_SkipsNothing` | skipped 0, processed 1 | Guard |
| AC-006 | Rule: decision and period | Unit | Rule | `TheFullRunPeriod_IsSevenDays`, `NoFullRunYet_IsDue`, `TheRunIsFull_FromSevenDaysAfterTheLastFullRun` (5), `OneTickShortOfSevenDays_IsNotDue`, `AFullRunTimeLaterThanTheStart_IsDue`, `TheDecision_IgnoresTheOffset` | as named | Red (period: Guard) |
| AC-006 | SyncState full-run time | Unit | Rule | `ANewRow_HasNoFullRunTime`, `ACompletedFullRun_RecordsItsStart`, `ACompletedNonFullRun_LeavesTheFullRunTime` (2), `BeginningAndFailingARun_LeaveTheFullRunTime`, `ALaterFullRun_ReplacesTheTime` | as named | Red |
| AC-006 | Column and check | Integration | Db | `TheSyncStateColumn_IsANullableTimestamptzWithNoDefault`, `TheFullRunCheck_Exists`, `AFullRunTimeNotLaterThanTheLastSuccess_IsAccepted` (3), `AFullRunTimeLaterThanTheLastSuccess_IsRejected`, `AFullRunTimeWithoutASuccessfulRun_IsRejected` | as named | Red |
| AC-007 | Button run skips like schedule | Integration | Host | `AButtonRun_SkipsTheUnchangedArchive_AsAScheduledRunWould` | 2nd run makes no read; SkippedUnchanged 1 | Red |
| AC-007 | Button run reads active course | Integration | Host | `AButtonRun_ReadsAnActiveCourse_AsAScheduledRunWould` | read twice; SkippedUnchanged 0 | Red |
| AC-008 | Completion line carries the count | Integration | Host | `TheCompletionLine_CarriesTheSkippedCount` | 0 then 1; processed 0 | Red |
| AC-008 | No course id or name logged | Integration | Host | `TheLog_NamesNoSkippedCourse` | absent; control skip 1 | Red |
| AC-008 | Failure line carries the count (I-2) | Integration | Host | `TheFailureLine_CarriesTheSkippedCount` | property present, 0 | Red |
| AC-008 | Failed run outcome keeps the count | Unit | Run | `AFailedRun_StillCarriesTheSkippedCountReachedBeforeTheStop` | skipped 1 | Red |
| AC-009 | Read-only: nothing starts or is looked up | Unit | Run | `InReadOnlyMode_NoRunStarts_AndNothingOfTheRuleIsTouched` | no listing, no lookup, time and value kept | Red |
| AC-010 | Google ports substituted everywhere | All | Run, Host | every test above (`SyncWorld.ClassroomReader`, `FakeClassroomReader`); skips observed as calls not made | no live call | — |
| FR-004 | Checks precede the skip | Unit | Run | `ABlankNamedUnchangedArchive_IsReportedAsBlank_NotAsSkippedUnchanged`, `AnUnrecognisedState_IsReportedAsBefore_NotAsSkippedUnchanged` | own report; skipped 0 | Guard |
| FR-003 | Age rule unchanged | Unit | Run | `ANewArchivedCourseOlderThanRetention_IsStillRefusedByTheAgeRule` | refused; not stored; skipped 0 | Guard |
