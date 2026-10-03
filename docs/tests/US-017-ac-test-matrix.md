---
artifact_type: ac_test_matrix
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:34:54Z
updated_at: 2026-10-03T15:34:54Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-017-retry-backoff-permission-errors.md
    version: null
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 2
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
  - path: docs/designs/database/US-017-db-design.md
    version: 1
supersedes: null
---

# US-017 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests.`):
**R** `Infrastructure.Google.GoogleClassroomReaderRetryTests`,
**F** `Application.UseCases.SynchronizationFailureHandlingTests`,
**Q** `Application.UseCases.GetLastSynchronizationQueryTests`,
**S** `Application.UseCases.SyncStateInvariantTests`,
**P** `Infrastructure.Persistence.SyncDiagnosisPersistenceTests`,
**B** `Web.Pages.LastSynchronizationBlockTests`,
**L** `Web.Logging.SynchronizationFailureLoggingTests`.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | 429/500/503 retried after 2 s | adapter | R | `ATransientStatus_IsRetriedAfterTwoSeconds_AndTheSecondAnswerIsReturned` | success, 2 attempts | red |
| AC-001 | dropped connection, timeout retried | adapter | R | `ADroppedConnection_IsRetried`, `ATimeout_ThatIsNotTheCallersCancellation_IsRetried` | success | red |
| AC-001 | token 503 retried | adapter | R | `ATokenRequest_Answering503_IsRetried` | success | red |
| AC-001 | retried page keeps its token | adapter | R | `ARetriedPage_CarriesItsPageToken_AndEarlierPagesAreNotRepeated` | page 1 once | red |
| AC-001 | one Warning per retried attempt | adapter | R | `OneRetriedAttempt_WritesOneWarning_AndNoRetryWritesNone` | 1 vs 0 `SyncGoogleRetry` | red |
| AC-002 | exactly 4 attempts, pauses × factor | adapter | R | `EveryAttemptFailing_StopsAtFourAttempts_WithThePausesTimesTheFactor` | Transient / GoogleUnavailable | red |
| AC-002 | `Retry-After` honoured, capped, invalid ignored | adapter | R | `RetryAfter_ReplacesTheNominalPause_WhenUsable` | pause as VR-001 | red |
| AC-002 | run fails, earlier courses kept | unit | F | `AFinalTransientFailure_FailsTheRun_AsGoogleUnavailable_AndKeepsEarlierCourses`, `AFinalTransientFailureOfTheListing_AfterOneCourse_KeepsThatCourse` | `failed`, `GoogleUnavailable` | red |
| AC-002 | logged at Warning | host | L | `AFinalTransientFailure_IsLoggedAtWarning` | `SyncRunFailed` Warning | red |
| AC-002, AC-003 | next run after the normal interval | host | L | `AfterAConfigurationFailure_TheNextRunIsDueAfterTheNormalInterval` | due after interval | green — regression guard (US-013 OD-003) |
| AC-003 | token refusals final at once | adapter | R | `AConfigurationRefusal_AtTheTokenEndpoint_IsFinalAtOnce` | 1 token request, 0 API | red |
| AC-003 | 403 API not enabled / cannot read | adapter | R | `A403_ForADisabledApi_IsApiNotEnabled_AfterOneAttempt`, `AnotherA403_IsTechnicalAccountCannotRead_AfterOneAttempt` | 1 attempt | red |
| AC-003 | missing key | adapter | R | `AMissingKey_IsKeyUnavailable_AndNothingIsSent` | 0 requests | red |
| AC-003 | run stops, code and count stored | unit | F | `AConfigurationFailureOfTheListing_StopsTheRun_WithTheDiagnosisName`, `AConfigurationFailureOnTheSecondRoster_KeepsTheFirstCourse_AndStops` | `failed`, code, count 1 | red |
| AC-003 | logged at Error with RunId and code | host | L | `AConfigurationFailure_IsLoggedAtError_WithTheRunIdAndTheCode` | Error | red |
| AC-004 | 404 per course → course gone | adapter | R | `A404_OnAPerCourseRead_IsCourseGone_AfterOneAttempt` | CourseGone, 1 attempt | red |
| AC-004 | course skipped, run completes | unit | F | `ACourseGone_IsSkipped_AndTheRunCompletes`, `AStoredCourse_ThatIsGone_IsLeftAsItWas` | `completed` | red |
| AC-004 | `SyncCourseGone` Warning | host | L | `ACourseGone_IsLoggedAtWarning_AndTheRunCompletesWithTheOtherCourse` | Warning, RunId | red |
| AC-005 | 404 listing / 400 / malformed → unexpected | adapter | R | `A404_OnTheCourseListing_IsUnexpected`, `A400_IsUnexpected_AfterOneAttempt`, `A200_WithAMalformedBody_IsUnexpected_AfterOneAttempt` | Unexpected | red |
| AC-005 | no Google text in the failure | adapter | R | `TheFailure_CarriesNoTextGoogleSent` | marker absent | red |
| AC-005 | stored as Unexpected, no detail | unit | F | `AnUnexpectedException_IsStoredAsUnexpected_WithNoDetail`, `AnUnexpectedClassFromTheAdapter_IsStoredAsUnexpected` | `Unexpected` | red |
| AC-005 | logged at Error, no message leak | host | L | `AnUnexpectedFailure_IsLoggedAtError_AndLeaksNoExceptionMessage` | marker absent | red |
| AC-005 | existing failure tests re-expressed | unit | — | `CourseWorkFailureTests` (1 case), `CourseImportTests.AFailedImport_StoresADiagnosisWithNoPayload`, `SynchronizationRunTests.AFailedRun_StoresACodeOfTheClosedList` | `Unexpected` / closed list | red |
| AC-006 | no row → never run | HTTP | B | `WithNoSyncStateRow_TheBlockSaysNoRunHasHappened` | uk, en | red |
| AC-006 | each diagnosis, "Check access" wording | HTTP | B | `AFailedRun_ShowsItsDiagnosisInTheRequestLanguage` | 8 × 2 | red |
| AC-006 | times in the request culture | HTTP | B | `AFailedRun_ShowsItsFinishAndTheLastSuccess_InTheRequestCulture`, `ACompletedRun_ShowsItsFinishInTheRequestCulture`, `ARunningRun_ShowsItsStart` | culture format | red |
| AC-006 | translations complete and distinct | resx | B | `EveryKeyOfTheBlock_IsTranslated`, `TheTitleDiffersBetweenLanguages_AndGoogleUnavailableHasItsOwnWording` | keys present | red |
| AC-006 | fixture sanity | — | B | `TheTwoCultures_FormatTheTestInstantDifferently` | formats differ | green — fixture guard |
| AC-006 | query mapping | unit | Q | `WithNoRow_TheView_IsNeverRun`, `ARunInProgress_TheView_IsRunning_WithItsStart`, `ACompletedRun_TheView_IsCompleted_WithItsEndAndTheLastSuccess`, `AFailedRun_TheView_IsFailed_WithThatDiagnosis`, `AFailedRun_AfterASuccess_KeepsTheLastSuccess` | DTO per API design §3 | red |
| AC-007 | Dean 403, nothing of the block | HTTP | B | `ADean_IsRefusedAndSeesNothingOfTheBlock` | 403, Admin control sees block | red |
| AC-008 | block viewable in read-only mode | HTTP | B | `InReadOnlyMode_TheBlockIsStillShown` | 200 + block, 3 causes | red |
| AC-008 | no run, no Google call in read-only | — | — | existing US-007 / US-013 read-only sync tests | unchanged | green — regression |
| AC-009 | course id / state ≤ 64 | host | L | `ALongCourseIdAndState_AreTruncatedToTheBound`, `AShortCourseIdAndState_AreLoggedUnchanged` | 64-char prefix; short unchanged | red / green control |
| AC-009 | submission id / state ≤ 64 | host | L | `ALongSubmissionIdAndRawState_AreTruncatedToTheBound`, `AShortSubmissionIdAndRawState_AreLoggedUnchanged` | same | red / green control |
| AC-010 | blank name skipped | unit | F | `ABlankCourseName_IsSkipped_AndTheRunCompletes`, `AStoredCourse_ArrivingBlank_KeepsItsName` | `completed`, name kept | red |
| AC-010 | `SyncCourseNameBlank` Warning | host | L | `ABlankCourseName_IsLoggedAtWarning_AndTheRunCompletesWithTheOtherCourse` | Warning | red |
| AC-011 | no Google, manual clock | all | R, F, L | every test substitutes the port or the HTTP handler; R advances a manual `TimeProvider` | — | by construction |
| AC-012 | shutdown during a pause | adapter | R | `ShutdownDuringAPause_EndsTheReadPromptly_AndSendsNothingFurther` | cancelled, no 2nd request | red |
| AC-013 | legacy value → Unexpected | unit | Q | `AValueThatIsNotADeclaredName_IsShownAsUnexpected` | Unexpected | red |
| AC-013 | legacy raw rows in PostgreSQL | integration | P | `ALegacyOrMisspelledValue_IsReadAsUnexpected` | Unexpected; exact-name control | red |
| AC-013 | page leaks nothing of a legacy value | HTTP | B | `ALegacyRunFailedValue_IsShownAsUnexpected_AndLeaksNothing` | no "RunFailed" | red |
| AC-014 | through the real adapter | adapter | R | the whole class R | — | red |
| FR-006 | `FailRun` exact name; undeclared refused | unit | S | `AFailure_StoresExactlyTheDiagnosisName`, `AnUndeclaredDiagnosis_IsRefused_AndTheStateIsUnchanged`, `TheClosedList_IsExactlyTheEightNames` | name / throws | red / green guard |
| FR-006 | round trip and clearing in PostgreSQL | integration | P | `AFailedRun_StoresTheCodeAndTheCount_AndTheyRoundTrip`, `ACompletedRun_AfterAFailedOne_NullsTheError` | as stored | red |
| FR-006 | clearing at unit level | unit | F | `ACompletedRun_AfterAFailedOne_ClearsTheDiagnosis` | null | red |
| db design | no migration; empty error unstorable | integration | P | `Model_HasNoPendingChanges_SoNoMigrationIsNeeded`, `AnEmptyError_CannotBeStored_SoItIsNeverRead` | — | green — characterisation |
