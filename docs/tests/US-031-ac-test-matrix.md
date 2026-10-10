---
artifact_type: ac_test_matrix
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T06:31:21Z
updated_at: 2026-10-10T06:31:21Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: docs/designs/database/US-031-entity-model.md
    version: 1
  - path: docs/tests/US-031-test-strategy.md
    version: 1
supersedes: null
---

# US-031 Acceptance Criteria → Tests

Class paths are under `tests/ClassroomAgent.Tests/`. Levels: U = unit
(Application/Domain, ports in memory), I = integration on PostgreSQL, A =
adapter (scripted HTTP), H = host run, S = HTTP/security, L = logging /
localization. Status is the red-phase state at TEST_WRITING (R = red, awaits
implementation; G = green by construction, see the report §4).

| AC | Scenario | Level | Test class | Test method(s) | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Domain meeting stored with conference id, code, organizer, start, end, one participation per endpoint | U | `Application/UseCases/MeetPullTests` | `ADomainMeeting_IsStoredWithItsValues_AndOneParticipationPerEndpoint`, `AParticipation_JoinsAtTheEventTimeMinusTheDuration` | values stored; join = time − duration | R |
| AC-001 | Meet step after the Classroom step, once, as the technical account | U | `MeetPullTests` | `TheMeetStep_RunsOnceAfterTheClassroomStep`, `TheMeetEvents_AreReadAsTheTechnicalAccount` | one call, after the course is stored | R |
| AC-001 | Through the real host and PostgreSQL | H | `Web/BackgroundServices/MeetPullHostTests` | `ARun_StoresTheMeeting_AndMovesTheWatermarkToTheRunsInstant`, `ThePort_IsCalledAsTheTechnicalAccount_WithTheFirstWindow` | rows written, watermark set | R |
| AC-001 | Repository round trip | I | `Infrastructure/Persistence/MeetSessionRepositoryTests` | `AStoredSession_RoundTripsWithItsParticipations`, `GetByConferenceIds_ReturnsOnlyTheStoredIdsOfAMixedList`, `GetByConferenceIds_ReturnsNothingForUnknownIds` | values round-trip | R |
| AC-002 | Start = earliest join, end = latest leave; recomputed by a later run | U | `MeetPullTests` | `ALaterRun_AddsLateConnections_AndRecomputesStartAndEnd`, `AConnectionReadAgainWithOtherValues_IsUpdated_AndTheMeetingFollows`, `AConferenceSpreadOverTwoPages_IsOneMeetingOverBoth` | bounds over all connections | R |
| AC-002 | Entity invariants | U | `MeetSessionInvariantTests` | `ANewMeeting_TakesItsStartAndEndFromItsFirstConnection`, `RecordingConnections_RecomputesStartAndEnd`, `RecordingAKnownEndpoint_UpdatesIt_AndRecomputes` | recomputed, also shrinking | R |
| AC-002 | Recompute persisted | I | `MeetSessionRepositoryTests` | `ALaterConnection_IsAddedAndTheBoundsAreRecomputed` | database bounds updated | R |
| AC-003 | Same events twice → one meeting, one row per endpoint | U | `MeetPullTests` | `TheSameEventsReadTwice_LeaveOneMeetingAndOneRowPerEndpoint`, `OneEndpointTwiceInOneRun_IsOneRow` | no duplicates | R |
| AC-003 | At database level | H, I | `MeetPullHostTests`, `MeetSessionRepositoryTests`, `MeetSchemaTests` | `ASecondRunReadingTheSameEvents_LeavesOneSessionAndTwoParticipations`, `RecordingAStoredEndpointAgain_LeavesOneRowForIt`, `ADuplicateConferenceId_IsRejected`, `ADuplicateEndpointInOneSession_IsRejected`, `TheSameEndpointInAnotherSession_IsAccepted` | unique keys hold | R |
| AC-004 | Other domain, subdomain, malformed, no organizer → nothing stored | U | `MeetPullTests` | `AMeetingNotOrganizedFromTheDomain_IsNotStored` (4 cases, with control) | none stored, counted not of the school | R |
| AC-004 | Per-conference decision (I-4) | U | `MeetPullTests` | `OneDomainOrganizerAmongTheEventsOfAConference_StoresItWithAllItsConnections`, `AStoredConference_TakesLaterConnections_AndKeepsItsCodeAndOrganizer`, `AConferenceSkippedEarlier_IsStoredWhenALaterRunReadsADomainOrganizer`, `AnOrganizerInOtherCase_IsADomainAccount_AndIsStoredAsReturned` | as FR-005 | R |
| AC-004, AC-005 | Domain-account rule (FR-006) | U | `SchoolDomainAccountTests` | `AnAddressOfTheSchoolsDomain_IsADomainAccount`, `TheSchoolsDomainInOtherCase_StillMatches`, `AnyOtherValue_IsNotADomainAccount` | exact, case-insensitive, no subdomain | R |
| AC-005 | Only a domain account's connection holds an email | U | `MeetPullTests`, `MeetSessionInvariantTests` | `OnlyADomainAccountsConnection_HoldsAnEmail`, `AConnectionWithoutADomainEmail_IsAnOtherParticipant` | others: no email, other participant | R |
| AC-006 | First window now − 180 d + 1 h; later T − 3 d; clamp; watermark | U | `MeetWindowTests` | `TheFirstPull_AsksFrom180DaysLessAnHourAgo_UpToNow`, `ACompletedRun_StoresTheEndOfItsWindow`, `ALaterPull_AsksFromThreeDaysBeforeTheWatermark`, `AWatermarkOlderThanTheHorizon_IsClampedToIt`, `AWatermarkInsideTheHorizon_IsNotClamped`, `ARunOfManyPages_AsksForOneWindowOnce` | exact windows | R |
| AC-006 | A failed Meet step / Classroom stop leaves T | U | `MeetWindowTests` | `AFailedMeetStep_LeavesTheWatermark_AndTheNextWindowStartsFromIt`, `ARunStoppedByTheClassroomStep_NeverReachesTheMeetStep` | T unchanged | R |
| AC-006 | Watermark rules (VR-002) | U | `SyncStateMeetTests` | all 8 | never later than finish, never backwards | R |
| AC-007 | Permission failure in the Meet step: no retry, code + step, Classroom data kept, T unchanged | U | `MeetStepFailureTests` | `AConfigurationFailureOfTheMeetStep_FailsTheRun_NamingTheMeetStep` (6 diagnoses), `AClassroomFailure_RecordsTheClassroomStep`, `AFailureOnTheSecondPage_KeepsTheFirstPage_AndLeavesTheWatermark`, `ACompletedRun_AfterAMeetFailure_ClearsTheStep`, `AnUnexpectedExceptionInTheMeetStep_IsStoredAsUnexpected_WithNoDetail`, `AHostStopDuringTheMeetStep_IsNotAFailure_AndWritesNoWatermark` | as FR-010 | R |
| AC-007 | Through the host | H | `MeetPullHostTests` | `AConfigurationFailureOfTheMeetStep_FailsTheRunAtTheMeetStep` | `failed`, code, `meet` | R |
| AC-007 | Adapter classification | A | `Infrastructure/Google/GoogleMeetReportsReaderRetryTests` | `AConfigurationAnswer_IsFinalAtOnce_WithoutARetry` | Configuration, one request | R |
| AC-007 | Shown on the block | U, S | `LastSynchronizationMeetQueryTests`, `Web/Pages/LastSynchronizationMeetBlockTests` | `AFailedRun_CarriesItsStep_AndThePreviousWatermark`, `AFailedMeetStep_NamesTheMeetStepAndTheDiagnosis`, `AFailedClassroomStep_NamesTheClassroomStep`, `AFailedRowWithNoStep_ShowsTheDiagnosisAndNeitherStep` | step + diagnosis | R |
| AC-008 | Transient failure then success: retried with US-017 parameters | A | `GoogleMeetReportsReaderRetryTests` | `A503_IsRetriedAfterAPause_AndTheSecondAnswerIsReturned`, `ARetriedPage_CarriesItsPageToken`, `RetryAfter_ReplacesTheNominalPause`, `FourFailingAttempts_EndInATransientFailure`, `ThePauses_AreTwo_Eight_AndThirtySeconds` | 4 attempts, 2/8/30 s | R |
| AC-008 | Final transient → GoogleUnavailable, step Meet | U | `MeetStepFailureTests` | `AFinalTransientFailureOfTheMeetStep_IsGoogleUnavailable` | run failed | R |
| AC-009 | Watermark in the school's time zone, or "not loaded yet" | U | `LastSynchronizationMeetQueryTests` | `TheWatermark_IsTheSchoolsLocalDateTime_InSummer`, `…_InWinter`, `ARunningRun_KeepsThePreviousWatermarkVisible`, `ACompletedRun_CarriesNoStep`, `WithNoWatermark_TheViewCarriesNone` | Kyiv local; null = none | R (last G) |
| AC-009 | On the Admin's page; Dean refused | S | `LastSynchronizationMeetBlockTests` | `AWatermark_IsShownInTheSchoolTimeZone_NotInUtc`, `ACompletedRunWithNoWatermark_SaysNotLoaded`, `WithNoSyncStateRow_TheBlockSaysNotLoaded`, `ARunningRun_StillShowsTheWatermark`, `ADean_IsRefusedAndSeesNothingOfTheMeetLines` | local time, no UTC rendering; Dean 403 | R |
| AC-010 | Read-only mode (each BR-025 reason): no Meet call | U | `MeetReadOnlyTests` | `InReadOnlyMode_TheMeetPortReceivesNoCall` (3 reasons), `WithNoUsableConnection_TheMeetPortReceivesNoCall`, control `OutsideReadOnlyMode_TheMeetPortIsCalled` | zero calls; control one call | G (control R) |
| AC-011 | Purge deletes old meetings with all participations, keeps newer, records both counts | I | `Application/UseCases/RetentionPurgeMeetTests` | all 9 (incl. cutoff boundary, 501-meeting batch, zero counts, read-only ×3) | as FR-013 | R |
| AC-011 | Audit factory counts | U | `RetentionPurgeMeetAuditEventTests` | all 5 | counts written, non-negative | R |
| AC-011 | Audit columns and constraints | I | `MeetSchemaTests` | `AuditEvent_GainsTwoNullableCounts`, `OnlyOneOfTheTwoMeetCounts_IsRejected`, `MeetCounts_OnAnotherAction_AreRejected`, `ANegativeMeetCount_IsRejected`, `APurgeRow_WithBothMeetCountsNull_IsAccepted`, `APurgeRow_WithBothMeetCounts_IsAccepted` | db-design §5 | R |
| AC-012 | Invalid event skipped and counted, pull continues | U | `MeetEventValidationTests` | `AnInvalidEvent_IsSkippedAndCounted_AndThePullContinues` (15), `ABoundaryValue_IsAccepted` (7), `Whitespace_IsTrimmed`, `AnOverlongOrganizer_IsNotADomainAccount_NotAnInvalidEvent`, `AnOverlongParticipant_IsAnOtherParticipant`, `UnreadableEvents_AreCountedAsSkipped_AndThePullContinues` | VR-001 | R |
| AC-012 | No event content logged | L | `Web/Logging/MeetPullLoggingTests` | `ACompletedMeetStep_IsLoggedOnce_WithTheWindowAndTheCounts`, `NoLogLine_CarriesAnyValueOfTheEvents`, `AnInvalidEvent_IsLoggedAsOneWarning_WithTheReasonAndTheCountOnly`, `AConfigurationFailureOfTheMeetStep_IsLoggedAtError_WithTheStepAndTheCode` | FR-012, SC-10 | R |
| AC-013 | No telemetry, device, location or display-name column | I | `Infrastructure/Persistence/MeetSchemaTests` | `MeetSession_HasExactlyTheDesignedColumns`, `MeetParticipation_HasExactlyTheDesignedColumns` (+ constraints, indexes, FK, `sync_state` tests of the class) | exact column sets | R |
| AC-013 | Nothing else leaves the adapter | A | `GoogleMeetReportsReaderTests` | `NothingElse_LeavesTheAdapter`, `AnEvent_IsMappedToTheModel` | only FR-003 values | R |
| AC-014 | New line and step names in uk and en | L, S | `Web/Localization/MeetPullTranslationTests`, `LastSynchronizationMeetBlockTests` | `EveryKey_ExistsInBothLanguages_AndTheTextsDiffer` (4 keys), the block tests per language | both languages | R |
| AC-015 | Substituted port, synthetic events; adapter paging and mapping on scripted responses | A, H | `GoogleMeetReportsReaderTests`, `InstallationFactory` substitution | `TheRequest_IsActivitiesListForAllUsersAndMeet_OverTheWindow`, `EveryPage_IsFollowed_InOrder`, `MissingOptionalParameters_AreAbsentValues_AndTheEventIsStillReturned`, `TheTokenRequest_ImpersonatesTheTechnicalAccount_WithTheReportsScopeOnly`, `EveryApiRequest_IsAGet` | no live call | R |

Every AC has at least one mapped scenario. No mandatory AC is untested.
