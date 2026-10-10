---
artifact_type: ac_test_matrix
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T16:14:25Z
updated_at: 2026-10-10T16:14:25Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/database/US-032-entity-model.md
    version: 1
  - path: docs/tests/US-032-test-strategy.md
    version: 1
supersedes: null
---

# US-032 Acceptance Criteria → Test Matrix

Status: **Red** = compiles, fails now for the missing feature (expected before
IMPLEMENTATION). Namespaces: `ClassroomAgent.Tests.<folder>`. "Unit" classes run
without a database; every other class uses PostgreSQL (TC-2).

| AC | Scenario | Level | Test class | Test method(s) | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | 85 % / 20 % linked to A, automatic, unconfirmed, audit `system` | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `AnUnambiguousCode_IsLinkedAutomatically_WithOneAuditRow` | one link row, one auto-link audit row | Red |
| AC-001 | decision for 85 / 20 | Unit | `Application.MeetLinking.MeetCodeScorerTests` | `EightyFiveAgainstTwenty_LinksTheBest`, `Shares_AreStudentsOfTheCourse_OverDistinctCountedAccounts` | unambiguous → A | Red |
| AC-001 | audit row shape of the automatic link | Unit | `Application.UseCases.MeetCodeAuditEventTests` | `TheAutomaticLink_IsTheSystems_TargetsTheCourse_AndCarriesTheCode` | actor system, target course, code | Red |
| AC-001 | link entity of an automatic link | Unit | `Application.UseCases.MeetingCodeLinkInvariantTests` | `AnAutomaticLink_IsLinked_ByNobody_Unconfirmed_AndConfirmable` | shape per db-design §2 | Red |
| AC-002 | roster on the meeting date; organizers excluded; candidate rule | Unit | `Application.MeetLinking.MeetCodeScorerTests` | `TheOrganizer_IsNotCounted`, `AnAccountThatOrganizedAnyMeetingOfTheCode_IsNotCountedInAnyMeeting`, `AnAccountInSeveralMeetings_CountsOnce`, `Emails_AreMatchedIgnoringCase`, `AStudentFirstSeenAfterTheMeetingDate_DoesNotCount`, `AStudentWhoLeftBeforeTheMeetingDate_DoesNotCount`, `AStudentOnTheRosterForOneOfTheirMeetings_Counts`, `AStudentOnTheRosterOnlyOnTheDateOfAMeetingTheyMissed_DoesNotCount`, `ATeacherOfTheCourseAmongParticipants_IsNotAStudent`, `NoCountedAccount_GivesZeroShares`, `ACourseNoneOfWhoseTeachersOrganized_IsNotACandidate`, `AnOrganizerWhoBecameTeacherAfterTheirMeeting_DoesNotMakeACandidate`, `AnOrganizerWhoIsOnlyAStudentOfTheCourse_DoesNotMakeACandidate`, `OneOrganizerTeacherOnOneMeeting_IsEnough`, `MeetingAndRosterDates_AreTakenInTheSchoolsZone`, `Candidates_AreOrderedBestShareFirst`, `EqualShares_AreOrderedByCourseId` | per spec FR-003 | Red |
| AC-002 | roster-on-a-date rule incl. Kyiv boundaries (TC-8) | Unit | `Application.MeetLinking.RosterOnDateTests` | all 9 | per spec FR-002 | Red |
| AC-002 | meetings before every first-seen date link nothing (I-1) | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `MeetingsBeforeEveryFirstSeenDate_FindNobodyOnTheRoster_AndLinkNothing` | no link row | Red |
| AC-002 | SQL candidate test at the Kyiv day boundary | Integration (HTTP) | `Web.Pages.MeetCodesPageTests` | `AtTheKyivDayBoundary_TheTeacherIsStillACandidate` | candidate shown | Red |
| AC-003 | 70 / 55 and best < 60 stay unassigned | Unit | `Application.MeetLinking.MeetCodeScorerTests` | `SeventyAgainstFiftyFive_IsAmbiguous`, `BestBelowSixty_IsAmbiguous`, `ExactlySixtyWithAGapOfExactlyThirty_Links`, `SixtyWithAGapOfTwenty_IsAmbiguous`, `JustBelowSixty_IsAmbiguous_ThoughItRoundsToSixty`, `AGapJustBelowThirty_IsAmbiguous_ThoughRoundedPercentsSayThirty`, `ASingleCandidateAtOneHundred_Links`, `ASingleCandidateBelowTheGap_IsAmbiguous`, `TwoEqualBestShares_AreAmbiguous_EvenWithTheSmallestGap` | ambiguous / boundaries inclusive | Red |
| AC-003 | through a run | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `AnAmbiguousCode_StaysUnassigned_AndTheRunCompletes` | no link, run completed | Red |
| AC-003 | listed with candidates and shares | Integration (HTTP) | `Web.Pages.MeetCodesPageTests` | `Unassigned_ShowsCodeOrganizerCountAndCandidateWithShareRoundedDown` | code, organizer, count, course, share | Red |
| AC-004 | re-scored and linked by a later run | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `AnAmbiguousCode_IsLinkedByALaterRun_WhenNewMeetingsMakeItUnambiguous` | linked after run 2 | Red |
| AC-005 | automatic link never revised; person's link and mark untouched | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `AnAutomaticLink_StaysWithA_WhenLaterMeetingsFavourB`, `APersonsLink_IsNeverRevised_WhenTheSharesFavourAnotherCourse` | link unchanged, one audit row | Red |
| AC-006 | thresholds from configuration; defaults; invalid stops startup | Integration (host) | `Web.Configuration.MeetLinkingConfigurationTests` | `AbsentSettings_ResolveToTheDefaultThresholds`, `ValidSettings_ResolveToThoseThresholds`, `OneSetting_LeavesTheOtherAtItsDefault`, `BoundaryValidSetting_HostStarts`, `InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue` (16 cases) | per spec FR-005 | Red |
| AC-006 | configured thresholds used | Unit + host | `MeetCodeScorerTests`; `MeetLinkingHostTests` | `ConfiguredThresholds_AreTheOnesUsed`; `ConfiguredThresholds_LinkACodeTheDefaultsLeaveAlone` | 55 / 10 links 70 / 55 | Red |
| AC-007 | no linking after a failed Meet step | Integration (host) | `Web.BackgroundServices.MeetLinkingHostTests` | `AFailedMeetStep_LinksNothing_AndTheRunFailsAtTheMeetStep` | no link, failed step meet | Red |
| AC-007 (FR-006) | linking-step failure: run failed at step linking, meetings kept; Admin's block names the step | Integration | `MeetLinkingHostTests`; `Web.Pages.MeetCodesPageTests` | `AFailureOfTheLinkingStep_FailsTheRunAtThatStep_AndKeepsTheStoredMeetings`; `AFailedLinkingStep_IsNamedInTheAdminsLastSynchronizationBlock` | failed_step linking; translated step | Red |
| AC-008 | pick any course for an unassigned code | Unit (use case) | `Application.UseCases.MeetCodeChangeTests` | `PickingACourseForAnUnassignedCode_LinksIt_AndAuditsIt`, `AnAdmin_CanPick_AndTheRoleIsRecorded`, `AnUnknownCourse_IsNotFound`, `AnUnknownCode_IsNotFound` | CoursePicked; audit; 404s | Red |
| AC-008 | over HTTP; course-choice form | Integration (HTTP) | `Web.Pages.MeetCodeActionsHttpTests`; `Web.Pages.MeetCodesPageTests` | `PickingACourse_LinksTheCode_Audits_AndRedirectsWithTheMessage`, `AnUnknownCode_Is404_WithCodeNotFound`, `AnUnknownCourse_Is404_WithCourseNotFound`; `TheChoiceForm_ListsCandidatesFirst_ThenOtherCoursesByName`, `TheChoiceForm_OfAnUnknownCode_Is404_WithTheCodeNotFoundMessage` | 302 + message; DB row; audit | Red |
| AC-009 | confirm | Unit + HTTP | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetingCodeLinkInvariantTests` | `ConfirmingAnAutomaticLink_RecordsWhoAndWhen_AndAuditsIt`, `ConfirmingWhatIsNotAnUnconfirmedAutomaticLinkOnTheCourse_IsStale` (5), `ConfirmingAnUnknownCode_IsNotFound`, `ConfirmingWithAMalformedExpectedCourse_IsMalformed`; `Confirming_RecordsWhoAndWhen_AndNothingElse`; `Confirming_RecordsWhoAndWhen_AndChangesNothingElse` | who/when; nothing else | Red |
| AC-010 | re-link A → B | Unit + HTTP | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetingCodeLinkInvariantTests` | `Relinking_MovesTheCode_AndAuditsOldAndNew`, `ALinkedCodeWithNoMeetingLeft_CanStillBeRelinked`, `RelinkingToTheCurrentCourse_IsSameCourse`; `Relinking_MovesTheCode_ClearsTheConfirmation_AndAuditsBothCourses`, `RelinkingToTheCurrentCourse_Is400_WithSameCourse`; `Relinking_MovesToTheCourse_ByThePerson_AndClearsTheConfirmation` | old + new in audit | Red |
| AC-011 | mark "not a course" (unassigned, automatic, person's) | Unit + HTTP + host | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetCodesPageTests`; `MeetLinkingHostTests` | `MarkingAnUnassignedCode_MarksIt_AndAuditsIt`, `MarkingALinkedCode_ClearsTheCourse_AndAuditsThePreviousOne` (2), `MarkingWithExpectedStateMarked_IsMalformed`, `MarkingAnAlreadyMarkedCode_IsStale`, `MarkingAnUnknownCode_IsNotFound`; `MarkingALinkedCode_ClearsTheCourse_AndAuditsThePreviousCourse`, `MarkingAnUnassignedCode_StoresTheMark_WithNoPreviousCourse`; `NotACourse_ShowsTheMarkersEmail`; `AMarkedCode_IsNeverLinked_AndItsRowDoesNotChange` | mark stored; listed; never auto-linked | Red |
| AC-012 | pick a course for a marked code removes the mark | Unit + HTTP | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetCodesPageTests`; `MeetingCodeLinkInvariantTests` | `PickingACourseForAMarkedCode_RemovesTheMark`, `RemovingAMarkThatIsGone_IsStale_AndShowsTheUnassignedList`; `PickingACourse_ForAMarkedCode_RemovesTheMark`; `TheChoiceForm_OfAMarkedCode_CarriesTheMarkedState`; `LinkingFromAMark_RemovesTheMark` | mark gone; audit | Red |
| AC-013 | no-candidate codes last, "no candidates" | Unit + host + HTTP | `MeetCodeScorerTests`; `MeetLinkingHostTests`; `MeetCodesPageTests` | `NoCandidate_IsAnEmptyList_AndNeverUnambiguous`; `ACodeWithNoCandidate_StaysUnassigned`; `ACodeWithNoCandidates_ComesAfterCodesWithCandidates_AndSaysSo` | after candidates; translated text | Red |
| AC-014 | Dean and Admin allowed; unauthenticated and restricted session refused | Security | `Web.Security.MeetCodesAuthorizationTests`; `MeetCodesPageTests` | `AnAdminAndADean_CanUseEveryOperation` (10), `Anonymous_IsSentToSignIn_OnEveryOperation` (5), `ARestrictedDean_IsSentToTheForcedChange_OnEveryOperation` (5); `TheLandingPage_LinksToTheMeetCodesPage` (2) | allowed / 302 sign-in / 302 change-password | Red |
| AC-014 | antiforgery | Security | `MeetCodeActionsHttpTests` | `AWriteWithoutTheAntiforgeryToken_Is400` | 400 | Red |
| AC-015 | read-only: lists viewable; writes refused in Application, refused row, no change | Unit (TC-5) + HTTP | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetCodesPageTests` | `InReadOnlyMode_EveryWriteIsRefusedFirst_WithOneRefusedRow` (8), `InReadOnlyMode_AMalformedCode_IsRefusedWithNoCodeOnTheRow`; `InReadOnlyMode_EachWriteIsRefused_WithTheReason_AndOneRefusedAuditRow`; `InReadOnlyMode_TheListsAreStillShown` | 409 + reason; guard first | Red |
| AC-015 | refused audit row shape | Unit | `MeetCodeAuditEventTests` | `ARefusal_HasNoCourses_AndTheCodeOnlyWhenGiven`, `ARefusalOfTheSystemsAction_IsRejected` | no courses | Red |
| AC-016 | purge: expired course with links; recent linked meeting keeps course; leaver; orphaned mark; counts | Integration (purge) | `Application.UseCases.RetentionPurgeMeetLinkTests` | all 7 | per spec FR-016 | Red |
| AC-016 | purge count on the audit row | Unit | `MeetCodeAuditEventTests` | `APurgeRow_CarriesNoMeetCode_ButCarriesTheLinksCount`, `APurgeRowWithNoLinksRemoved_RecordsZero`, `ANegativeLinksCount_IsRejected`, `ALinkChange_CarriesNoPurgeCount` | count recorded, never null | Red |
| AC-017 | UI language; data verbatim | Localization | `Web.Localization.MeetCodesTranslationTests` | `EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer`, `ThePage_RendersInTheChosenLanguage_AndShowsDataAsStored` (2) | keys in uk and en; data unchanged | Red |
| AC-018 | no Google call; synthetic data | All | every class above | — | Google ports substituted (`FakeMeetReportsReader`, `FakeClassroomReader`); write use cases take no Google port | by construction |

## Specification items beyond the ACs

| Item | Test class | Test method(s) | Status |
|---|---|---|---|
| FR-001 / db-design §2 shapes and transitions | `MeetingCodeLinkInvariantTests` | all 21 | Red |
| FR-007 lists, ordering, pagination, invalid query | `MeetCodesPageTests` | `Linked_ShowsCourseMakerConfirmerAndTheConfirmFormOnlyWhereOffered`, `Unassigned_IsPaginated`, `AnInvalidQuery_Is400_WithThePageAndTheMessage`, `TheChoiceForm_OfALinkedCode_OmitsTheCurrentCourse_AndCarriesTheExpectedState` | Red |
| FR-013 stale state and concurrency | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests`; `MeetingCodeLinkRepositoryTests` | `PickingForACodeThatIsNoLongerUnassigned_IsStale`, `RelinkingWhenTheLinkNamesAnotherCourse_IsStale`, `AConcurrentChangeAtSave_IsStale_AndCommitsNothing`; `AStaleState_Is409_WithStateChanged_AndNothingChanges`; `TwoChangesOfOneLink_TheSecondSaveIsAConflict`, `TwoFirstDecisionsOnOneCode_TheSecondSaveIsAConflict` | Red |
| VR-001 … VR-003 malformed input, `returnPage` | `MeetCodeChangeTests`; `MeetCodeActionsHttpTests` | `AMalformedRequest_IsMalformed_AndWritesNothing` (19), `ASixtyFourCharacterCode_IsWellFormed`, `AnUnknownField_IsIgnored`, `ReturnPage_IsKeptWhenValid_AndZeroOtherwise` (7); `MalformedInput_Is400_WithPageExpired_AndNothingIsWritten` | Red |
| FR-014 audit shapes | `MeetCodeAuditEventTests` | `APersonsChange_RecordsActorCodeAndCourses` (6), `AShapeTheConstraintForbids_IsRejected` (8) | Red |
| db-design §2 … §4, §8 schema | `Infrastructure.Persistence.MeetingCodeLinkSchemaTests` | all (≈ 56 cases) | Red (one pair member passes, report §4) |
| entity model §5 repository | `Infrastructure.Persistence.MeetingCodeLinkRepositoryTests` | all 10 | Red |
| spec §9 logging, SC-10 | `Web.Logging.MeetLinkingLoggingTests` | `ACompletedLinkingStep_IsLoggedOnce_WithTheCounts`, `NoLogLine_CarriesTheCodeAnEmailOrACourseName` | Red |
| PC-2 migration count | `Infrastructure.Persistence.AppUserMigrationTests` (modified) | `…` count 15, last `_AddMeetingCodeLinks` | Red |
