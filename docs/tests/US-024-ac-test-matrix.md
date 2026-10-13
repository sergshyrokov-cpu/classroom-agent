---
artifact_type: ac_test_matrix
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-13T10:00:00Z
updated_at: 2026-10-13T10:00:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/designs/api/US-024-api-design.md
    version: 1
  - path: docs/designs/database/US-024-db-design.md
    version: 1
  - path: docs/tests/US-024-test-strategy.md
    version: 1
supersedes: null
---

# US-024 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Abbreviation | Class |
|---|---|
| **Q** | `Application.UseCases.DatabaseStatisticsQueryTests` (unit) |
| **S** | `Infrastructure.Persistence.DatabaseStatisticsReadSourceTests` (integration) |
| **P** | `Web.Pages.DatabaseStatisticsPageTests` |
| **A** | `Web.Security.DatabaseStatisticsAuthorizationTests` |
| **R** | `Web.UseCases.DatabaseSynchronizationRequestTests` |
| **RO** | `Web.UseCases.DatabaseStatisticsReadOnlyTests` |
| **T** | `Web.Localization.DatabaseStatisticsTranslationTests` |
| **L** | `Web.Logging.DatabaseStatisticsLoggingTests` |

Status: **Red** means the test fails until IMPLEMENTATION. **Structural** means it passes on the skeleton by design.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Home page section, last both roles see, one link | HTTP | P | `TheHomePage_HasTheDatabaseSection_AfterTheReportTemplates` | `nav#database-section` after report templates, before `admin-settings`; read-only too | Red |
| AC-002 | Five states and total | Unit | Q | `TheCourseCounters_AreTheFiveStates_AndTheirSum`, `AStateWithNoCourse_IsZero_AndTheTotalIsStillTheSum` | `CourseCounts(5,4,3,2,1,15)`; zero state, sum kept | Red |
| AC-002 | Counts over PostgreSQL | Integration | S | `EveryCounter_HasItsSeededValue` | 5/4/3/2/1 | Red |
| AC-002 | Rendered | HTTP | P | `TheSeededSchool_ShowsEveryCounter` | every `data-counter`, total 15 | Red |
| AC-003 | Teacher, student, both, leaver | Integration | S | `TheFourPeopleOfAc003_AreCountedByTheCurrentRoster` | teachers 2, students 2 | Red |
| AC-003 | Any course state; no membership not counted | Integration | S | `TeachersOfAnyState_AreCounted_AndAParticipantWithoutMembership_IsNot` | 2 teachers, 0 students | Red |
| AC-004 | Coursework, submissions, meetings, codes | Integration | S | `EveryCounter_HasItsSeededValue` | 5, 7, 6, 2 | Red |
| AC-004 | Pass-through | Unit | Q | `TheOtherCounters_ArePassedThrough_EachToItsOwnField` | each field its own value | Red |
| AC-005 | State mapping, times, same block both roles | Unit | Q | `TheState_IsMappedFromTheStoredStatus`, `TheBlock_IsTheSameForBothRoles` | in progress/completed; equal blocks | Red |
| AC-005 | No processed counter | Unit | Q | `TheDtos_HaveNoFieldForADiagnosis_AStep_OrTheProcessedCounter` | no such member | Structural |
| AC-005 | Rendered, both roles equal, no processed count | HTTP | P | `ACompletedRun_ShowsTheTimesInTheSchoolZone_AndTheState_AndNoProcessedCount`, `TheSynchronizationBlock_IsTheSame_ForAdminAndDean` | four lines; `987654` absent | Red |
| AC-006 | Never run | Unit | Q | `WithNoRow_TheBlock_IsNotYet_WithNoTimes` | `NotYet`, null times, guidance none | Red |
| AC-006 | Empty database | Integration | S | `AnEmptySchool_CountsZeroEverywhere` | all zeros | Red |
| AC-006 | Rendered | HTTP | P | `AnEmptySchool_NeverSynchronized_ShowsZerosAndNotYet` | 12 zeros, four "not yet" | Red |
| AC-007 | Failed: guidance by role | Unit | Q | `AFailedRun_ReadsFailed_WithTheRolesGuidance`, `AFirstRunThatFailed_HasNoTimes_AndTheRolesGuidance` | Dean `AskAdmin`, Admin `ConnectionPageLink` | Red |
| AC-007 | Rendered | HTTP | P | `AFailedRun_ShowsFailed_AndTheGuidanceOfTheRole_AndNoCause` | Dean hint, Admin link to `/settings/workspace-connection` | Red |
| AC-007 | Hint and link only for failed (I-7) | HTTP | P | `ARunningOrCompletedRun_ShowsNoFailureGuidance` | neither shown | Red |
| AC-008 | Accepted press | HTTP | R | `AnAcceptedPress_ReturnsToTheDatabasePage_RequestsOnce_AndShowsTheMessageOnce` | `302 /database`, 1 request, 1 `succeeded` audit row, message once | Red |
| AC-008 | Other work in progress | HTTP | R | `APressDuringOtherWork_ShowsRequestedAfterCurrentWork` | US-019 message | Red |
| AC-008 | No open redirect (S-07) | HTTP | R | `ASubmittedReturnUrl_IsIgnored_NoOpenRedirect` | `Location` = `/database` | Red |
| AC-008 | US-019 endpoint unchanged | HTTP | R | `TheUs019Endpoint_StillReturnsToTheRolesOwnPage` | Dean `/`, Admin connection page | Regression (passes) |
| AC-009 | Read-only: query and page work, write nothing | HTTP / use case | RO | `TheUseCase_ReturnsTheSeededCounters_WritesNothing_AndCallsNoGoogle`, `ThePage_ShowsTheCountersAndTheForm_WritesNothing_AndCallsNoGoogle` | all causes; fingerprint unchanged; 0 Google calls | Red |
| AC-009 | Read-only: press refused | HTTP | R | `InReadOnlyMode_ThePressIs409_WithTheReason_AndNothingIsEnqueued`, `ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason` | `409`, reason, 0 requests, `read_only_mode` row | Red |
| AC-009 | Button visible in read-only | HTTP | P | `ThePage_CarriesTheSynchronizationForm_InEveryMode` | form with token | Red |
| AC-009 | No guard, no write | Unit | Q | `OnePage_IsOneReadOfEach_AndWritesNothing`, `TheQuery_DependsOnReadPortsOnly` | one read each; dependencies: read ports only | Red / Structural |
| AC-010 | Admin and Dean open the page | HTTP | A | `AnAdminAndADean_CanOpenTheDatabasePage` | `200` with counters | Red |
| AC-010 | Unauthenticated / restricted refused | HTTP | A | `Anonymous_IsSentToSignIn_ForTheGet`, `ADeanWithATemporaryPassword_IsSentToTheForcedChange_ForTheGet`, `Anonymous_IsSentToSignIn_ForThePress`, `ADeanWithATemporaryPassword_IsSentToTheForcedChange_ForThePress` | `302` sign-in / change-password; nothing requested | Red (GET pair may pass early, see strategy §8) |
| AC-010 | Antiforgery | HTTP | A | `WithoutAnAntiforgeryToken_ThePressIs400_AndNothingHappens` | `400`, 0 requests, no audit | Red |
| AC-010 | Policies | HTTP | A | `TheTwoPaths_AreProtectedEndpoints_WithTheirPolicies` | `ViewDatabaseStatistics`, `StartSynchronization` | Red |
| AC-011 | Keys in both languages | HTTP | T | `EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer` | new and reused keys | Red |
| AC-011 | Page and guidance in UI language | HTTP | T | `ThePage_RendersInTheChosenLanguage`, `TheFailureGuidance_IsShownInTheUiLanguage` | uk and en | Red |
| AC-012 | No Google; synthetic data | HTTP / all | RO, P | `TheUseCase_…_AndCallsNoGoogle`, `ThePage_…_AndCallsNoGoogle`, `TheSeededSchool_ShowsNoPersonalData` | 0 Google calls; invented data only | Red |
| AC-013 | No diagnosis, key or step on the page | HTTP | P | `AFailedRun_ShowsFailed_AndTheGuidanceOfTheRole_AndNoCause` | stored diagnosis and every `LastSync.Diagnosis.*` / `LastSync.Step.*` text absent | Red |
| AC-013 | DTO cannot carry it | Unit | Q | `TheDtos_HaveNoFieldForADiagnosis_AStep_OrTheProcessedCounter` | no string member | Structural |
| AC-014 | Time zone | Unit | Q | `TheThreeTimes_AreInTheSchoolsZone`, `InSummer_TheOffset_IsThreeHours` | 2026-03-02 00:30 / 00:00 / 00:15; summer +3 | Red |
| AC-014 | Formatted on the page | HTTP | P | `ACompletedRun_ShowsTheTimesInTheSchoolZone_AndTheState_AndNoProcessedCount` | short date + `HH:mm`, uk and en | Red |
| AC-015 | Once per counter; archived counts | Integration | S | `APersonInSeveralCourses_IsCountedOncePerCounter_AndAnArchivedCourseCounts` | 1 teacher, 1 student | Red |
| AC-016 | Connection not usable | HTTP | R | `WithoutAUsableConnection_ThePressIs409_OnTheDatabasePage_WithTheRolesMessage` | `409`, Database page, role message, 0 requests, `connection_not_usable` row | Red |
| AC-017 | Fixed command count | Integration | S | `ThePage_IsAFixedNumberOfCommands_WithOneAndWithTwoHundredCourses`, `TheSizedSchool_IsCountedInFull` | counters 1 command; page equal for 1 and 200 courses, ≤ 2 | Red |
| AC-018 | Agrees with Meet codes Unassigned | Integration | S | `TheCodesAwaitingDecision_EqualTheMeetCodesPagesUnassignedCount`, `OnceEveryCodeIsDecided_NoneAwaits_AndTheMeetingsStillCount` | equal; 2 then 0 | Red |
| AC-019 | Query parameters ignored | HTTP | P | `AnyQueryString_IsIgnored_AndEchoedNowhere` | `200`; marker and `evil.example` absent; same counters | Red |
| AC-019 | Not logged | HTTP | L | `AnIgnoredParameter_IsNeverLogged` | marker in no log file | Red |

Supporting (FR-013, S-10):
- **L** — `OpeningThePage_LogsTheActor_AndNoSchoolData` and `AnAcceptedPress_LogsTheRequestedEvent_AndNoSchoolData`.
- **P** — `TheSeededSchool_ShowsNoPersonalData`.
- **S** — `Reading_WritesNothing`.
- **Q** — `AnUnknownStatus_Throws` (VR-003).

Every AC-001 … AC-019 has at least one mapped test.
