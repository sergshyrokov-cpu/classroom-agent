---
artifact_type: ac_test_matrix
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T07:00:00Z
updated_at: 2026-10-15T07:00:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
  - path: docs/tests/US-038-test-strategy.md
    version: 1
supersedes: null
---

# US-038 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Abbreviation | Class |
|---|---|
| **E** | `Application.UseCases.EraseParticipantUseCaseTests` (unit) |
| **LU** | `Application.UseCases.LiftErasureExclusionUseCaseTests` (unit) |
| **Q** | `Application.UseCases.ErasureQueryTests` (unit) |
| **I** | `Application.UseCases.ErasureInvariantTests` (unit) |
| **PE** | `Web.UseCases.ParticipantErasureTests` (integration) |
| **S** | `Web.UseCases.ErasureSearchTests` (integration) |
| **SY** | `Web.UseCases.ErasureExclusionSyncTests` (integration) |
| **M** | `Web.UseCases.ErasureExclusionMeetTests` (integration) |
| **DB** | `Infrastructure.Persistence.ParticipantErasureSchemaTests` (persistence) |
| **A** | `Web.Security.ErasureAuthorizationTests` (HTTP) |
| **P** | `Web.Pages.ErasurePageTests` (HTTP) |
| **RO** | `Web.UseCases.ErasureReadOnlyHttpTests` (HTTP) |
| **T** | `Web.Localization.ErasureTranslationTests` |
| **L** | `Web.Logging.ErasureLoggingTests` |

Status: **Red** — fails until IMPLEMENTATION. The red-phase result of each test
is in the test generation report.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Confirmation shows name, email, courses, five counts, export warning | HTTP | P | `TheConfirmation_ShowsWhatWillBeErased` | counts 3/2/3/1/1, both course names, warning, mark, nothing changed | Red |
| AC-001 | Same, over the real read source | Integration | S | `TheConfirmation_ShowsTheCoursesAndTheFiveCounts` | DTO with courses and counts | Red |
| AC-001 | Mapping of source data to the page DTO | Unit | Q | `TheConfirmation_MapsTheCoursesAndCounts` | roles mapped, notices false | Red |
| AC-001 | The person is found by search before the confirmation | HTTP | P | `ASearchByName_ListsThePerson_LinkingToTheConfirmation` | one result linking to the confirmation | Red |
| AC-002 | Participant, memberships, submissions, Meet participations gone | Integration | PE | `TheErasure_DeletesTheParticipant_TheirMemberships_SubmissionsAndMeetParticipations` | zero rows of each | Red |
| AC-002 | Organized meeting kept without organizer | Integration | PE | `TheMeetingTheyOrganized_RemainsWithoutTheOrganizerEmail` | `organizer_email` null, other connection kept | Red |
| AC-002 | Owned course kept without owner | Integration | PE | `TheCourseTheyOwned_RemainsWithoutTheOwner` | owner null; other owners unchanged | Red |
| AC-002 | Other people's data unchanged | Integration | PE | `OtherPeoplesData_IsUnchanged` | digest of others' rows equal | Red |
| AC-002 | A failure erases nothing | Integration | PE | `AFailureInsideTheTransaction_ErasesNothing` | database fingerprint equal, no exclusion, no audit | Red |
| AC-002 | A failure is not committed | Unit | E | `AFailureInsideTheTransaction_Propagates_AndNothingIsCommitted` | rolled back | Red |
| AC-002 | One transaction, fence first, target after the fence | Unit | E | `TheErasure_RunsInOneTransaction_FenceFirst_ThenTheTarget_ThenTheDeletes` | call order | Red |
| AC-002 | The store gets the normalized email | Unit | E | `TheStore_ReceivesTheIdTheGoogleUserIdAndTheNormalizedEmail` | `o.koval@school-one.example.test` | Red |
| AC-002 | Erased result with counts | Unit | E | `ASucceededErasure_ReturnsErased_WithTheStoresCounts` | outcome Erased | Red |
| AC-002 | PRG to the page with the message; person gone | HTTP | P | `ConfirmedErasure_RedirectsToThePage_WithTheMessage_AndThePersonIsGone` | `302 /settings/erasure`, `#erasure-message` | Red |
| AC-002 | One-time message | HTTP | P | `TheMessage_IsShownOnce` | absent on the second load | Red |
| AC-003 | Without the mark: refused, nothing touched | Unit | E | `WithoutTheMark_TheErasureIsRefused_AndNothingIsTouched` | IrreversibleMarkRequired, only the guard called | Red |
| AC-003 | Without the mark (absent, `on`, `false`) over HTTP | HTTP | P | `WithoutTheMark_TheErasureIs400_AndNothingChanges` | `400`, `#erasure-mark-error`, nothing changed, no audit | Red |
| AC-003 | Only `true` sets the mark | Unit | I | `OnlyTrue_SetsTheMark` | 9 cases | Red |
| AC-004 | After erasure the next run does not bring the person back | Integration | SY | `AfterTheErasure_TheNextRunDoesNotBringThePersonBack_AndSynchronizesEveryoneElse` | no participant, membership, submission, owner; others synced | Red |
| AC-004 | Excluded person never created; course without owner | Integration | SY | `AnExcludedPerson_IsNeverCreated_AndTheirCourseIsStoredWithoutOwner` | as above | Red |
| AC-004 | A skip is not a failure | Integration | SY | `ASkippedPerson_IsNotAFailure` | run completed, no error | Red |
| AC-005 | Excluded email: no participation in any letter case | Integration | M | `AnExcludedEmail_HasNoParticipationStored_InAnyLetterCase` | zero target rows; others stored | Red |
| AC-005 | Their meeting stored without organizer | Integration | M | `AMeetingOrganizedByAnExcludedPerson_IsStoredWithoutTheOrganizer` | `organizer_email` null | Red |
| AC-005 | The 3-day re-read after erasure brings nothing back | Integration | M | `AfterTheErasure_TheOverlapReRead_BringsNothingBack` | zero target rows, organizer stays null | Red |
| AC-006 | Exclusion holds Google id, fingerprint, date | Integration | PE | `TheExclusion_HoldsTheGoogleUserId_TheFingerprint_AndTheErasureDate` | HMAC with stored salt | Red |
| AC-006 | No name or email column or value | Integration | PE | `TheExclusionTable_HoldsNoNameAndNoEmail` | six designed columns, no email text | Red |
| AC-006 | Two installations, different fingerprints | Integration | PE | `TwoInstallations_GiveTheSameEmailDifferentFingerprints` | fingerprints differ | Red |
| AC-006 | Two salts, different fingerprints | Unit | I | `TwoSalts_GiveTheSameEmailDifferentFingerprints` | not equal | Red |
| AC-006 | The fingerprint is HMAC-SHA-256 of the normalized email | Unit | I | `TheFingerprint_IsHmacSha256_OfTheNormalizedEmail_KeyedWithTheSalt` | 32 bytes, equal to independent HMAC | Red |
| AC-006 | Exclusion created by the use case | Unit | E | `TheExclusion_HoldsTheGoogleUserId_TheFingerprintWithTheSalt_AndTheInstant` | id, fingerprint, clock instant | Red |
| AC-006 | Exclusion entity has no name or email property | Unit | I | `TheExclusion_HasNoPropertyForANameOrAnEmail` | only `GoogleUserId` is text | Red |
| AC-006 | Exclusion schema | Persistence | DB | `TheExclusionTable_HasExactlyTheDesignedColumns` | six columns | Red |
| AC-006 | Salt created once and reused | Integration | PE | `TheSalt_IsCreatedOnce_AndReused` | one salt row | Red |
| AC-007 | Lifting in any letter case | Unit | LU | `TheEmailInAnyLetterCase_LiftsTheExclusion_AndOnlyIt` | one removed, other kept | Red |
| AC-007 | Unknown email: "no exclusion", nothing changes | Unit | LU | `AnEmailWithNoExclusion_IsAnsweredNoExclusion_AndNothingChanges` | no audit, nothing removed | Red |
| AC-007 | No salt: "no exclusion" | Unit | LU | `WithNoSaltStored_TheAnswerIsNoExclusion_AndNothingIsWritten` | nothing written | Red |
| AC-007 | Next run loads the person as new | Integration | SY | `AfterTheLifting_TheNextRunLoadsThePersonAsNew` | participant, membership, submission, owner back | Red |
| AC-007 | Lifting over HTTP | HTTP | P | `LiftingByEmailInAnyCase_RedirectsWithTheMessage_AndRemovesTheExclusion` | `302`, message, no exclusion | Red |
| AC-007 | Unknown email over HTTP | HTTP | P | `LiftingAnEmailWithNoExclusion_SaysSo_KeepsTheEmail_AndChangesNothing` | `200`, `#erasure-no-exclusion`, email kept | Red |
| AC-007 | Fingerprint ignores case and spaces | Unit | I | `TheFingerprint_IgnoresLetterCaseAndSurroundingSpaces` | equal | Red |
| AC-008 | Dean `403` on all five operations | HTTP | A | `ADean_IsForbidden_EveryOperation` | `403`, nothing changes | Red |
| AC-008 | Policy admits Admin, refuses Dean | HTTP | A | `TheErasurePolicy_AdmitsAnAdmin`, `TheErasurePolicy_RefusesADean` | as named | Red |
| AC-008 | Admin allowed | HTTP | A | `AnAdmin_ReachesThePage_TheSearch_AndTheConfirmation` | `200` with content | Red |
| AC-008 | Anonymous and restricted Dean redirected | HTTP | A | `Anonymous_IsSentToSignIn`, `ADeanWithATemporaryPassword_IsSentToTheForcedChange` | `302` | Red |
| AC-008 | Every route declares the policy | HTTP | A | `TheFivePaths_AreProtectedEndpoints_WithThePolicy` | five protected endpoints | Red |
| AC-008 | No menu link for the Dean; link for the Admin | HTTP | P | `TheHomePage_LinksToTheErasurePage_ForTheAdminOnly` | link inside `nav#admin-settings` for Admin only | Red |
| AC-008 | Read-only: erasure refused in Application, all causes | Unit | E | `InReadOnlyMode_TheErasureIsRefused_WithOneRefusedAuditRow_AndNothingElse` | exception, one refused audit row | Red |
| AC-008 | Read-only: refusal before the mark | Unit | E | `InReadOnlyMode_AnUnsetMark_IsStillARefusal` | exception | Red |
| AC-008 | Read-only: lifting refused, all causes | Unit | LU | `InReadOnlyMode_TheLiftingIsRefused_WithOneRefusedAuditRow_AndNothingElse` | exception, one refused audit row | Red |
| AC-008 | Read-only: refusal before validation | Unit | LU | `InReadOnlyMode_AnInvalidEmail_IsStillARefusal` | exception | Red |
| AC-008 | Read-only erasure through the container and PostgreSQL | Integration | PE | `InReadOnlyMode_TheErasureIsRefused_AndOnlyTheRefusalIsAudited` | data unchanged, one refused row | Red |
| AC-008 | Read-only over HTTP: `409` | HTTP | RO | `AnErasure_Is409_AndOnlyTheRefusalIsAudited`, `AnErasureWithoutTheMark_IsStill409`, `ALifting_Is409_AndTheExclusionStays` | `409`, nothing changes | Red |
| AC-009 | One audit row with internal id and counts | Integration | PE | `TheErasure_WritesOneAuditRow_WithTheInternalIdAndTheCounts` | 2/3/3/1/1 | Red |
| AC-009 | No audit row carries personal data or the fingerprint | Integration | PE | `NoAuditRow_CarriesPersonalData` | none found | Red |
| AC-009 | Audit row from the use case | Unit | E | `ASucceededErasure_AddsOneAuditRow_WithTheParticipantIdAndTheCounts` | shape and counts | Red |
| AC-009 | Lifting: one audit row per exclusion id | Unit | LU | `TwoExclusionsOfOneEmail_AreBothLifted_InOneTransaction_WithOneAuditRowEach` | two rows, target ids | Red |
| AC-009 | Audit factories | Unit | I | `TheErasureAuditRow_CarriesTheParticipantIdAndTheCounts`, `TheLiftingAuditRow_CarriesTheExclusionId`, `TheErasureAuditRow_NeedsTheCounts` | shapes | Red |
| AC-009 | Audit schema checks | Persistence | DB | `TheDesignedAuditRows_AreAccepted`, `AnAuditRowWithTheWrongCounts_IsRejected`, `AnAuditRowOfTheWrongShape_IsRejected` | accepted / rejected by name | Red |
| AC-009 | One `Information` log line with id and counts | HTTP | L | `AnErasure_WritesOneInformationLine_WithTheInternalIdAndTheCounts` | event `ParticipantErased` | Red |
| AC-009 | No log line carries the person's data | HTTP | L | `NoLogLine_CarriesThePersonsData_AfterAnErasure` | none found | Red |
| AC-010 | Sync course transaction waits for an erasure and sees its exclusion | Integration | SY | `ARunThatReachesItsWriteDuringAnErasure_WaitsForIt_AndStoresNothingOfThePerson` | waits on the fence; no participant | Red |
| AC-010 | Meet page transaction likewise | Integration | M | `AMeetPageThatReachesItsWriteDuringAnErasure_WaitsForIt_AndStoresNothingOfThePerson` | waits; no participation; organizer null | Red |
| AC-010 | The lifting takes no fence | Unit | LU | `TheLifting_DoesNotTakeTheFence` | no fence call | Red |
| AC-011 | Excluded submitter off the roster not created | Integration | SY | `AnExcludedSubmitterOffTheRoster_IsNotCreated` | no participant; another submitter created | Red |
| AC-012 | Meet row in another case erased | Integration | PE | `AMeetParticipationInAnotherLetterCase_IsErased` | zero rows | Red |
| AC-012 | And later skipped | Integration | M | `AnExcludedEmail_HasNoParticipationStored_InAnyLetterCase` | zero rows | Red |
| AC-013 | Part of name or email, any case | Integration | S | `APartOfANameOrEmail_InAnyLetterCase_FindsThePerson` | 5 cases | Red |
| AC-013 | Off every roster found | Integration | S | `APersonOffEveryRoster_IsFound` | found with role counts | Red |
| AC-013 | Wildcards literal | Integration | S | `WildcardCharacters_AreTakenLiterally`, `ALiteralPercent_FindsANameContainingIt` | as named | Red |
| AC-013 | Ordering | Integration | S | `Results_AreOrderedByFullNameThenEmailThenId` | name, email, id | Red |
| AC-013 | Term never in the address | HTTP | P | `ASearchByName_ListsThePerson_LinkingToTheConfirmation`, `AQueryStringTerm_IsIgnored` | no `Location`; query ignored | Red |
| AC-013 | Term validation | Unit | Q | `AMissingTerm_IsRequired_AndNoSearchRuns`, `AOneCharacterTerm_IsTooShort`, `TheTermLengthBound_Is254` | errors, no search | Red |
| AC-013 | Term validation over HTTP | HTTP | P | `AnInvalidTerm_Is400_WithTheFieldError` | `400`, `#erasure-search-error` | Red |
| AC-014 | 51 matches → 50 and refine | Unit | Q | `MoreThan50Matches_Show50_AndAreTruncated`, `Exactly50Matches_AreNotTruncated`, `TheSource_IsAskedForTheTrimmedTerm_AndOneRowMoreThanShown` | 50, flag | Red |
| AC-014 | Same over PostgreSQL | Integration | S | `MoreThan50Matches_Show50_AndAreTruncated` | first 50 in order | Red |
| AC-014 | Same over HTTP | HTTP | P | `MoreThan50Matches_Show50_AndAskToRefine` | 50 results, `#erasure-truncated` | Red |
| AC-015 | Unknown id: not found, nothing changes | Unit | E | `AnUnknownParticipant_IsNotFound_AndNothingIsWritten`, `AParticipantErasedMeanwhile_IsNotFound_WithNoExclusionAndNoAuditRow` | not found | Red |
| AC-015 | Same over PostgreSQL; second and concurrent erasures | Integration | PE | `AnUnknownId_IsNotFound_AndNothingChanges`, `ASecondErasureOfTheSameId_IsNotFound`, `TwoConcurrentErasures_OneSucceeds_TheOtherIsNotFound_AndOneExclusionExists` | one exclusion | Red |
| AC-015 | Over HTTP: `404` | HTTP | A | `AnUnknownParticipant_Is404_ForTheConfirmationAndTheErasure_AndNothingChanges`, `AMalformedParticipantId_Is404` | `404` | Red |
| AC-015 | Confirmation of unknown id | Unit, Integration | Q, S | `TheConfirmationOfAnUnknownId_IsNull` | null | Red |
| AC-016 | Two exclusions lifted, one audit row each | Unit | LU | `TwoExclusionsOfOneEmail_AreBothLifted_InOneTransaction_WithOneAuditRowEach` | 2 removed, 2 rows | Red |
| AC-016 | Two exclusions may share a fingerprint | Persistence | DB | `TwoExclusionsWithOneFingerprint_AreAccepted` | accepted | Red |
| AC-017 | Page and search open in read-only mode with the notice | HTTP | RO | `ThePageAndTheSearch_StillWork_WithTheNotice` | `200`, notice, results | Red |
| AC-017 | Confirmation opens with the notice and a disabled button | HTTP | RO | `TheConfirmation_StillOpens_WithTheNotice_AndADisabledButton` | `200`, notice, `disabled` | Red |
| AC-017 | No notice outside read-only mode | HTTP | RO | `OutsideReadOnlyMode_ThereIsNoNotice` | no notice | Red |
| AC-017 | Confirmation DTO notice | Unit | Q | `InReadOnlyMode_TheConfirmationOpens_WithTheNotice` | flag set | Red |
| AC-017 | Refused actions audited once | Unit, HTTP | E, LU, RO | the read-only tests above | one refused row each | Red |
| AC-018 | Purge leaves exclusions and salt untouched | Integration | PE | `TheRetentionPurge_LeavesExclusionsUntouched` | rows equal | Red |
| AC-019 | No email: erased, no Meet row touched, no fingerprint | Integration | PE | `AParticipantWithNoEmail_IsErased_AndNoMeetRowIsTouched` | Meet unchanged, fingerprint null | Red |
| AC-019 | Same in the use case | Unit | E | `AParticipantWithNoEmail_IsErased_WithNoMeetMatch_AndAnExclusionWithoutFingerprint` | null email to the store | Red |
| AC-019 | Sync skips the Google id | Integration | SY | `AnExclusionWithoutFingerprint_StillSkipsTheGoogleUserId` | not created | Red |
| AC-019 | Meet load unaffected | Integration | M | `AnExclusionWithoutFingerprint_DoesNotAffectTheMeetLoad` | rows stored | Red |
| AC-019 | Confirmation notice | Unit, Integration, HTTP | Q, S, P | `TheConfirmationOfAParticipantWithNoEmail_SetsTheNotice`, `TheConfirmationOfAParticipantWithNoEmail_CountsNoMeetRow`, `TheConfirmationOfAParticipantWithNoEmail_ShowsTheNotice` | notice | Red |
| AC-020 | Three POSTs without a token refused, nothing changes | HTTP | A | `WithoutAnAntiforgeryToken_ThePostIs400_AndNothingChanges` | `400`, no audit | Red |
| AC-020 | A GET to the erasure address erases nothing | HTTP | A | `AGetToTheErasureAddress_ErasesNothing` | nothing changes | Red |
| AC-021 | Every key in `uk` and `en` | Translation | T | `EveryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer` | 37 keys | Red |
| AC-021 | Page, confirmation and home entry render in both languages; names as stored | HTTP | T | `ThePage_RendersInTheChosenLanguage`, `TheConfirmation_RendersInTheChosenLanguage_AndTheNamesAsStored`, `TheHomePageEntry_RendersInTheChosenLanguage` | texts present | Red |

Supporting tests with no AC of their own: schema tests of `erasure_salt`, the
indexes and the relaxed `meet_session` check (DB); `ErasureCounts`, salt and
fingerprint invariants (I); `TheGuard_IsAskedFirst_WithTheOperationName`,
`AnExistingSalt_IsReused` (E); the VR-003 validation cases (LU); the logging of
the search term and the email to lift (L); `TheQueries_TouchNoWritingPort` (Q);
`SearchAndConfirmation_WriteNothing` (S).
