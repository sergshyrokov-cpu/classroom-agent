---
artifact_type: ac_test_matrix
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T13:22:00Z
updated_at: 2026-09-28T15:03:58Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/designs/database/US-015-entity-model.md
    version: 1
  - path: docs/tests/US-015-test-strategy.md
    version: 1
supersedes: null
---

# US-015 Acceptance Criteria → Test Matrix

Levels: **U** use case (ports substituted, TC-1), **D** domain unit,
**I** integration against real PostgreSQL (Testcontainers, TC-2),
**H** host integration, **S** security-relevant.

## AC-001 A run imports each course's assignments and materials

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| both resources read | U | `CourseWorkImportTests` | `BothClassroomResources_AreImported` | one `courseWork` and one `courseWorkMaterial` both present |
| paging | U | `CourseWorkImportTests` | `CourseWorkAndMaterials_ArePagedToTheEnd` | two pages of each, all items stored |
| only `PUBLISHED` | U | `CourseWorkImportTests` | `DraftAndDeletedWork_AreNotImported` | neither stored |
| control for the above | U | `CourseWorkImportTests` | `PublishedWork_IsImported` | stored — so the refusal is not vacuous |
| cascade 1 | U | `CourseWorkImportTests` | `ItemDate_UsesScheduledTimeWhenPresent` | `item_date` = `scheduledTime` |
| cascade 2 | U | `CourseWorkImportTests` | `ItemDate_FallsBackToDueDate` | = `dueDate` |
| cascade 3 | U | `CourseWorkImportTests` | `ItemDate_FallsBackToUpdateTime` | = `updateTime` |
| cascade 4 | U | `CourseWorkImportTests` | `ItemDate_FallsBackToCreationTime` | = `creationTime` |
| Google times kept | U | `CourseWorkImportTests` | `GoogleCreationAndUpdateTimes_AreStoredAsGiven` | stored in UTC unchanged |
| key scope | I | `CourseWorkSchemaTests` | `DuplicateGoogleIdInTheSameCourseAndResource_IsRejected` | unique violation |
| key scope, positive | I | `CourseWorkSchemaTests` | `TheSameGoogleIdInTwoCourses_IsAccepted` | **succeeds** (Spec v2) |
| key scope, positive | I | `CourseWorkSchemaTests` | `TheSameGoogleIdUnderBothResources_IsAccepted` | **succeeds** (PC-3) |
| vocabulary | I | `CourseWorkSchemaTests` | `ResourceOutsideTheVocabulary_IsRejected` | check violation |
| table presence | I | `CourseWorkSchemaTests` | `TheTable_Exists` | asserted **first**, so later absence tests are not vacuous |

## AC-002 A run imports the submissions of each piece of work

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| one call per course | U | `SubmissionImportTests` | `Submissions_AreReadOncePerCourseWithTheWildcard` | reader called once, `courseWorkId = "-"` |
| attribution | U | `SubmissionImportTests` | `EachSubmission_IsAttributedByItsOwnCourseWorkId` | two items, each submission under the right one |
| facts stored as given | U | `SubmissionImportTests` | `GradesAndLateFlag_AreStoredExactlyAsGoogleGaveThem` | raw points, `late` unchanged |
| last turn-in | U | `SubmissionImportTests` | `LastTurnIn_IsTheLatestTransition` | the **second** turn-in (BR-058) |
| no history | U | `SubmissionImportTests` | `SubmissionWithoutTurnInHistory_HasNoDate` | `turned_in_at` null, not invented |
| materials | U | `SubmissionImportTests` | `Materials_AreNeverQueriedForSubmissions` | no submission attributed to a material |
| paging | U | `SubmissionImportTests` | `Submissions_ArePagedToTheEnd` | two pages stored |
| grades not stored | D | `SubmissionInvariantTests` | `GradeOnUngradedWork_IsStoredAsGiven` | stored, not discarded (I-8) |
| key scope | I | `SubmissionSchemaTests` | `DuplicateGoogleIdUnderTheSameCourseWork_IsRejected` | unique violation |
| key scope, positive | I | `SubmissionSchemaTests` | `TheSameGoogleIdUnderAnotherCourseWork_IsAccepted` | **succeeds** (Spec v2) |
| no false uniqueness | I | `SubmissionSchemaTests` | `TwoSubmissionsOfOneItemByOneStudent_AreAccepted` | **succeeds** (db-design §4.3) |
| nothing else stored | I | `SubmissionSchemaTests` | `TheTable_HasNoContentOrHistoryColumn` | no column beyond the design's list (BR-059) |
| table presence | I | `SubmissionSchemaTests` | `TheTable_Exists` | asserted first |

### VR-004 — the state vocabulary (OD-005, OD-011)

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| unrecognised is stored | U | `SubmissionImportTests` | `UnrecognisedState_IsStoredWithTheRawStringAndTheRunCompletes` | row exists, marker set, raw string kept, run completed |
| recognised stays clean | U | `SubmissionImportTests` | `RecognisedStates_AreStoredWithoutARawString` | all six accepted, `raw_state` null |
| entity guard | D | `SubmissionInvariantTests` | `UnrecognisedState_RequiresTheRawString` | refused without it |
| entity guard | D | `SubmissionInvariantTests` | `RecognisedState_ForbidsARawString` | refused with it |
| database guard | I | `SubmissionSchemaTests` | `StateOutsideTheVocabulary_IsRejected` | check violation |
| database guard | I | `SubmissionSchemaTests` | `UnrecognisedWithoutRawState_IsRejected` | biconditional, half one |
| database guard | I | `SubmissionSchemaTests` | `RecognisedWithRawState_IsRejected` | biconditional, half two |

## AC-003 A second run changes nothing it should not

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| idempotent item | U | `CourseWorkImportTests` | `SecondRun_UpdatesTheSameRowRatherThanAddingOne` | one row, same surrogate id |
| points added later | U | `CourseWorkImportTests` | `WorkThatGainedMaximumPoints_UpdatesTheSameRow` | same row, kind now graded |
| grade changed | U | `SubmissionImportTests` | `SecondRun_UpdatesTheGradeInPlace` | new value, no second row, no history |
| counter | U | `CourseAgeRuleTests` | `ProcessedCount_CountsCoursesNotItems` | unchanged meaning (FR-014) |

## AC-004 A submitter with no roster sighting gets a membership

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| membership created | U | `SubmitterWithoutRosterTests` | `SubmitterNeverSeenOnRoster_GetsAnOffRosterStudentMembership` | role `student`, `on_roster` false |
| dates | U | `SubmitterWithoutRosterTests` | `BothSeenDates_AreTheRunInstant` | first = last = run instant (I-1) |
| person shape | U | `SubmitterWithoutRosterTests` | `ParticipantCreatedFromUserIdAlone_HasNoNameOrEmail` | both absent (OD-006) |
| existing untouched | U | `SubmitterWithoutRosterTests` | `ExistingMembership_IsNotRewritten` | first-seen unchanged, still on roster |

Bound by OD-010: these prove the **program's** rule on synthetic data, never that
Classroom returns such a submission.

## AC-005 The database holds what the purge will need

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| item times | U | `CourseWorkImportTests` | `GoogleCreationAndUpdateTimes_AreStoredAsGiven` | present for last activity (PC-11) |
| submission time | U | `SubmissionImportTests` | `GoogleUpdateTimeIsStored` | present (OD-007) |
| optional times | I | `CourseWorkSchemaTests` | `ItemWithoutOptionalGoogleTimes_IsStorable` | nullability is deliberate |
| deletion order | I | `CourseWorkSchemaTests` | `DeletingACourseThatHasCourseWork_IsRefused` | `Restrict` |
| deletion order | I | `SubmissionSchemaTests` | `DeletingCourseWorkThatHasSubmissions_IsRefused` | `Restrict` |
| nothing deleted here | U | `CourseWorkImportTests` | `AnItemGoogleNoLongerReturns_KeepsItsRow` | untouched, as US-014 leaves a vanished course |

## AC-006 Read-only mode never reaches the new reads

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| no coursework call | U/S | `CourseWorkRefusalTests` | `ReadOnlyMode_DoesNotReadCourseWork` | substituted reader records no call |
| no submission call | U/S | `CourseWorkRefusalTests` | `ReadOnlyMode_DoesNotReadSubmissions` | idem |
| no write | U/S | `CourseWorkRefusalTests` | `ReadOnlyMode_WritesNoCourseWorkOrSubmissionRow` | no rows |
| **control** | U/S | `CourseWorkRefusalTests` | `AllowedRun_ReadsBothAndWrites` | the same world, allowed, **does** both |
| guard list unchanged | U/S | `CourseWorkRefusalTests` | `PermittedServiceWrites_DoesNotGrow` | BR-026 list unchanged |

## AC-007 A failure part-way leaves a consistent database

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| committed courses whole | U | `CourseWorkFailureTests` | `FailureAfterSomeCourses_LeavesOnlyCompleteCourses` | no course with items but no submissions |
| run recorded | U | `CourseWorkFailureTests` | `FailedRun_IsRecordedInSyncState` | `RunFailed:<type>`, no personal data |
| recovery | U | `CourseWorkFailureTests` | `NextRun_CompletesWithoutDuplicating` | BR-041 |

## AC-008 No grade and no personal data reaches a log

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| no grade | H/S | `SubmissionImportLoggingTests` | `ARunThatImportedSubmissions_WritesNoGrade` | no line contains a grade value |
| no person | H/S | `SubmissionImportLoggingTests` | `NoStudentNameEmailOrGoogleIdIsLogged` | none present |
| no titles | H/S | `SubmissionImportLoggingTests` | `NoCourseNameOrWorkTitleIsLogged` | none present |
| **control** | H/S | `SubmissionImportLoggingTests` | `TheImportReallyHappened` | rows exist, so the absences are not the absence of a run |
| the one warning | H/S | `SubmissionImportLoggingTests` | `UnrecognisedStateWarning_CarriesOnlyTheStateAndTheSubmissionId` | exactly those two values |
| no new surface | I/S | existing endpoint enumeration test | unchanged | **no new row** (FR-019, SC-4) |
| no audit row | I/S | `SubmissionSchemaTests` | `AScheduledRun_WritesNoAuditRow` | count unchanged (FR-020) |

## AC-009 Tests never reach Google, and the schema is tested for real

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| no live call | U | all Application classes | substituted `IClassroomReader` | TC-4 |
| real PostgreSQL | I | both schema classes | Testcontainers fixture | TC-2, no InMemory |
| paging proved | U | `CourseWorkImportTests`, `SubmissionImportTests` | the paging methods above | a single-page implementation fails |
| synthetic fixtures | — | `CourseWorkTestData` | reviewed | no real name, address or school domain |
| migration set | I | `AppUserMigrationTests` *(modified)* | seven → **eight**, `_AddCourseWorkAndSubmissions` last, tables nine → **eleven** | expected change |
| no pending model changes | I | `AppUserMigrationTests` | `TheModel_HasNoPendingChangesAgainstTheMigrations` | green |

## AC-010 A course older than N is not imported

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| skipped whole | U | `CourseAgeRuleTests` | `UnknownCourseOlderThanN_ImportsNothing` | no course, participant, membership, item or submission |
| **control** | U | `CourseAgeRuleTests` | `UnknownCourseWithinN_IsImportedInFull` | everything written |
| last activity | U | `CourseAgeRuleTests` | `LastActivity_TakesTheLatestOfCourseWorkAndSubmissionDates` | a fresh submission keeps an old course |
| boundary | U | `CourseAgeRuleTests` | `CourseExactlyAtTheBoundary_IsImported` | exactly N is not "older than N" |
| exemption | U | `CourseAgeRuleTests` | `AlreadyImportedCourse_IsUpdatedHoweverOld` | §5 v55, I-4 |
| counter | U | `CourseAgeRuleTests` | `SkippedCourse_IsNotCounted` | I-5 |
| setting required | H | `RetentionConfigurationTests` | `MissingRetentionYears_RefusesToStart` | VR-008 |
| setting invalid | H | `RetentionConfigurationTests` | `NonNumericRetentionYears_RefusesToStart` | VR-008 |
| setting invalid | H | `RetentionConfigurationTests` | `ZeroRetentionYears_RefusesToStart` | VR-008 |
| setting invalid | H | `RetentionConfigurationTests` | `NegativeRetentionYears_RefusesToStart` | VR-008 |
| **control** | H | `RetentionConfigurationTests` | `ValidRetentionYears_Starts` | the host starts |

## Domain invariants (VR-002, FR-005, FR-008)

| Scenario | Lv | Class | Method | Expected |
|---|---|---|---|---|
| truncation | D | `CourseWorkInvariantTests` | `OverlongTitle_IsCutNotRefused` | cut at `MaxTitleLength`, item still imported |
| kind | D | `CourseWorkInvariantTests` | `Kind_IsMaterialForTheMaterialResource` | `Material` |
| kind | D | `CourseWorkInvariantTests` | `Kind_IsGradedWorkWhenMaximumPointsAreSet` | `GradedWork` |
| kind | D | `CourseWorkInvariantTests` | `Kind_IsUngradedWorkWhenMaximumPointsAreAbsent` | `UngradedWork` |
| not stored | I | `CourseWorkSchemaTests` | `TheTable_HasNoKindColumn` | derived, never persisted (PC-3) |
| unimportable | D | `CourseWorkInvariantTests` | `MissingGoogleId_IsRefused` | refused |
| unimportable | D | `CourseWorkInvariantTests` | `ItemWithNoCascadeDate_IsRefused` | refused (db-design §3.3) |
| due pair | D | `CourseWorkInvariantTests` | `DueDateWithoutATime_IsStoredAsNoDueDate` | not guessed (db-design §3.4) |
| late default | D | `SubmissionInvariantTests` | `LateDefaultsToFalseWhenGoogleOmitsIt` | `false` |
| sanity | I | `CourseWorkSchemaTests` | `NegativeMaxPoints_IsRejected` | check violation |
| sanity | I | `SubmissionSchemaTests` | `NegativeGrades_AreRejected` | check violation |
| material shape | I | `CourseWorkSchemaTests` | `MaterialWithMaxPointsOrDueDate_IsRejected` | check violation (OD-008) |

## Coverage statement

Every Acceptance Criterion AC-001 … AC-010 has at least one mapped scenario, and
each of AC-004, AC-006, AC-008 and AC-010 carries an explicit **control** so that
its negative assertion cannot pass vacuously.

No mandatory Acceptance Criterion is unmapped.
