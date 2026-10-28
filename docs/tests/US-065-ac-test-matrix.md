---
artifact_type: ac_test_matrix
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T12:13:33Z
updated_at: 2026-10-28T12:13:33Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
  - path: docs/tests/US-065-test-strategy.md
    version: 1
supersedes: null
---

# US-065 Acceptance Criteria → Test Matrix

| Abbreviation | Class (namespace `ClassroomAgent.Tests`) |
|---|---|
| **U** | `Application.UseCases.EnterErasureExclusionUseCaseTests` (unit) |
| **DB** | `Infrastructure.Persistence.ErasureExclusionInAdvanceSchemaTests` (persistence) |
| **EN** | `Web.UseCases.ErasureExclusionEntryTests` (integration) |
| **SY** | `Web.UseCases.ErasureExclusionInAdvanceSyncTests` (integration, synchronization and Meet) |
| **P** | `Web.Pages.ErasureExclusionEntryPageTests` (HTTP) |
| **A** | `Web.Security.ErasureExclusionEntryAccessTests` (HTTP) |
| **T** | `Web.Localization.ErasureExclusionEntryTranslationTests` |
| **L** | `Web.Logging.ErasureExclusionEntryLoggingTests` |
| **PM** | `Web.Security.PermissionMatrixTests` (adapted) |
| **PS** | `Infrastructure.Persistence.ParticipantErasureSchemaTests` (adapted) |

Status: **Red** — fails until IMPLEMENTATION. The red-phase result of every test
is in the test generation report.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Stores fingerprint, no id, entry date | Unit | U | `WithNoSaltYet_TheEntryCreatesIt_AndStoresTheExclusion_WithTheFingerprint_AndNoGoogleUserId` | Entered; one exclusion; id null | Red |
| AC-001 | Entity shape | Unit | U | `AnExclusionEnteredInAdvance_HoldsTheFingerprint_NoGoogleUserId_AndTheEntryDate` | id null, fingerprint, date | Red |
| AC-001 | One audit row naming the exclusion | Unit | U | `TheEntry_WritesOneAuditRow_NamingTheNewExclusion` | action entered, target exclusion id | Red |
| AC-001 | Over PostgreSQL, audit with no personal value | Integration | EN | `OnAnEmptyInstallation_TheEntryStoresOneExclusion_WithTheFingerprint_AndCreatesTheSalt` | HMAC with stored salt; audit row; no email/hex | Red |
| AC-001 | PRG, one-time message, row stored | HTTP | P | `AnEntry_RedirectsToThePage_WithTheMessageOnce_AndStoresTheExclusion` | `302 /settings/erasure`, `#erasure-message` once | Red |
| AC-001 | Existing salt reused | Unit | U | `AnExistingSalt_IsReused` | no new salt | Red |
| AC-002 | Works with no connection and no salt | Integration | EN | `OnAnEmptyInstallation_TheEntryStoresOneExclusion_WithTheFingerprint_AndCreatesTheSalt` | salt created, row stored | Red |
| AC-002 | The block shows with no connection saved | HTTP | P | `ThePage_ShowsTheEntryForm_WithNoGoogleConnectionSaved` | `#erasure-enter-form`, field `email`, token | Red |
| AC-003 | First run: nothing of the student, rest stored, id filled | Integration | SY | `TheFirstRun_StoresNothingOfTheExcludedStudent_AndFillsTheExclusionWithTheirGoogleUserId` | 0 participant/membership/submission; filled | Red |
| AC-004 | Renamed address after the match stays out | Integration | SY | `AfterTheMatch_ARenamedAddress_DoesNotBringTheStudentBack_AndTheExclusionStaysAsFilled` | 0 rows of the student; row unchanged | Red |
| AC-005 | Meet skips connections, clears organizer, before and after the match | Integration | SY | `TheMeetLoad_SkipsTheConnections_AndClearsTheOrganizer_OfAnEmailEnteredInAdvance` | 0 participations; organizer null | Red |
| AC-006 | Already loaded, nothing stored, salt untouched | Unit | U | `ALoadedParticipantWithTheEmail_IsAnsweredAlreadyLoaded_AndNothingIsStored` | AlreadyLoaded with list | Red |
| AC-006 | Over the real read source, search shape | Integration | EN | `LoadedParticipantsWithTheEmail_AreAlreadyLoaded_ListedAsTheSearchListsThem_AndNothingChanges` | both ids; equal to search result | Red |
| AC-006 | Page lists them with links | HTTP | P | `ALoadedEmail_Is200_AlreadyLoaded_ListingThePeople_WithLinks` | `200`, `#erasure-already-loaded`, link | Red |
| AC-006 | Loaded and excluded → loaded (I-5) | Unit | U | `LoadedAndExcludedAtOnce_IsAnsweredAlreadyLoaded` | AlreadyLoaded | Red |
| AC-006 | Over 50 → 50 + truncated | Unit | U | `MoreThan50LoadedParticipants_Show50_AndAreTruncated` | 50, flag | Red |
| AC-006 | Longer address is not "loaded" | Integration | EN | `AParticipantWithALongerEmail_IsNotAlreadyLoaded` | Entered | Red |
| AC-007 | Exclusion from erasure → already excluded | Unit | U | `AnExclusionFromAnErasure_IsAnsweredAlreadyExcluded` | AlreadyExcluded | Red |
| AC-007 | Entered in advance, typed again | Unit | U | `AnExclusionEnteredInAdvance_TypedAgainInAnotherCase_IsAnsweredAlreadyExcluded` | AlreadyExcluded | Red |
| AC-007 | Both kinds over PostgreSQL | Integration | EN | `AnExistingExclusionOfTheEmail_IsAlreadyExcluded_AndNothingChanges` | nothing changed | Red |
| AC-007 | Page message | HTTP | P | `AnExcludedEmail_Is200_AlreadyExcluded_AndNothingChanges` | `200`, `#erasure-already-excluded` | Red |
| AC-007 | Other address / no fingerprint does not count | Unit | U | `AnExclusionOfAnotherAddress_DoesNotCount`, `AnExclusionWithoutFingerprint_DoesNotCount` | Entered | Red |
| AC-008 | Lifting after the entry | Unit | U | `AfterTheFirstEntry_ALiftingOfTheSameEmail_RemovesIt` | Lifted 1 | Red |
| AC-008 | Lifting after the match, next run loads as new | Integration | SY | `ALiftingAfterTheMatch_RemovesTheExclusion_AndTheNextRunLoadsThePerson` | rows gone; person loaded | Red |
| AC-009 | Read-only refused in Application, three causes | Unit | U | `InReadOnlyMode_TheEntryIsRefused_WithOneRefusedAuditRow_AndNothingElse` | exception; one refused audit row | Red |
| AC-009 | Guard before validation | Unit | U | `InReadOnlyMode_AnInvalidEmail_IsStillARefusal` | refusal | Red |
| AC-009 | `409` over HTTP, refusal audited | HTTP | A | `InReadOnlyMode_TheEntryIs409_NothingIsStored_AndOnlyTheRefusalIsAudited`, `InReadOnlyMode_AnInvalidEmail_IsStill409` | `409`; nothing stored | Red |
| AC-010 | Admin allowed | HTTP | A | `AnAdmin_EntersAnExclusion` | `302` | Red |
| AC-010 | Dean `403`; temporary password; anonymous | HTTP | A | `ADean_IsForbidden_AndNothingChanges`, `ADeanWithATemporaryPassword_IsSentToTheForcedChange`, `Anonymous_IsSentToSignIn` | `403` / change / sign-in | Red |
| AC-010 | Endpoint declares the policy; matrix row | HTTP | A, PM | `TheEntryPath_IsAProtectedEndpoint_WithTheErasurePolicy`, `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint` | policy `EraseParticipantData` | Red |
| AC-010 | GET stores nothing | HTTP | A | `AGetToTheEntryAddress_StoresNothing` | ≥ 400 | Red |
| AC-011 | Required, too long, malformed | Unit | U | `AMissingEmail_IsRequired_AndNothingIsTouched`, `AnEmailOver254Characters_IsTooLong`, `AMalformedEmail_IsInvalid` | Invalid + field error; only the guard | Red |
| AC-011 | Shared rule with the lifting | Unit | U | `TheEntryAndTheLifting_GiveTheSameFieldError` | equal errors | Red |
| AC-011 | 254 accepted; any domain | Unit | U | `An254CharacterEmail_WithSurroundingSpaces_IsEntered`, `AnEmailOfAnotherDomain_IsEntered` | Entered | Red |
| AC-011 | `400`, value kept in the entry field | HTTP | P | `AnInvalidEmail_Is400_WithTheFieldError_AndKeepsTheValue` | `400`, `#erasure-enter-error` | Red |
| AC-011 | Value not logged | HTTP | L | `TheEnteredEmail_IsNeverLogged` | no "o.koval" in the log | Red |
| AC-012 | Entry waits for a course write, answers loaded | Integration | EN | `AnEntryDuringACourseWrite_WaitsForIt_AndAnswersAlreadyLoaded` | waits; AlreadyLoaded | Red |
| AC-012 | Run waits for an entry, sees it (salt before / created by the entry) | Integration | SY | `AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite` | person not stored; filled | Red |
| AC-012 | Fence first, exclusive, then loaded check | Unit | U | `TheEntry_RunsInOneTransaction_FenceFirst_ThenLoaded_ThenSalt_ThenExcluded_ThenTheInsert` | order | Red |
| AC-013 | Owner on the roster matched → no owner | Integration | SY | `TheCoursesOwner_MatchedOnItsRoster_LeavesTheCourseWithoutOwner` | owner null | Red |
| AC-014 | Case and spaces in the entry lookup | Unit | U | `TheLoadedLookup_ReceivesTheNormalizedEmail` | normalized | Red |
| AC-014 | Case in the loaded check over PostgreSQL | Integration | EN | `LoadedParticipantsWithTheEmail_AreAlreadyLoaded_ListedAsTheSearchListsThem_AndNothingChanges` | stored mixed case found | Red |
| AC-014 | Case in the sync match | Integration | SY | `GooglesAddressInAnotherLetterCase_StillMatches` | skipped, filled | Red |
| AC-015 | Erasure exclusion not matched or changed by sync | Integration | SY | `AnExclusionFromAnErasure_IsNotMatchedByFingerprint_NorChanged` | person loaded; row unchanged | Red |
| AC-015 | Filled once, never again (sync) | Integration | SY | `AfterTheMatch_ARenamedAddress_DoesNotBringTheStudentBack_AndTheExclusionStaysAsFilled` | row unchanged | Red |
| AC-015 | Trigger: exists; one fill; no second change; erasure row immutable; other columns frozen | Persistence | DB | `TheFillOnlyTrigger_ExistsOnTheTable_BeforeUpdate_ForEachRow`, `TheOneFill_OfAnExclusionWithoutId_IsAccepted`, `AFilledExclusion_CannotBeChangedAgain`, `AnExclusionFromAnErasure_CannotBeUpdated`, `AFillThatAlsoChangesAnotherColumn_IsRejected`, `AnUpdateOfAnUnmatchedExclusion_ThatDoesNotFillIt_IsRejected` | accepted / `check_violation` | Red |
| AC-015 | Values check; many without id; immutability check replaced; delete allowed | Persistence | DB, PS | `AnExclusionWithAFingerprint_AndNoGoogleUserId_IsAccepted`, `AnExclusionWithNeitherIdNorFingerprint_IsRejected`, `AnExclusionWithoutId_AndAFingerprintOfAnotherLength_IsRejected`, `ManyExclusionsWithoutId_Coexist`, `TheImmutabilityCheck_IsReplacedByTheTrigger`, `UnmatchedAndFilledExclusions_CanBeDeleted`, `TheExclusionTable_HasExactlyTheDesignedColumns`, `AnExclusion_CannotBeChanged_ButCanBeDeleted` | as designed | Red |
| AC-016 | Id held by another exclusion → skipped, entry stays without id | Integration | SY | `AMatchedIdAlreadyHeldByAnotherExclusion_SkipsThePerson_AndLeavesTheEntryWithoutId` | run completes | Red |
| AC-016 | Unique index refuses a second holder | Persistence | DB | `TheFill_CannotTakeAnIdHeldByAnotherExclusion` | unique violation | Red |
| AC-016 | Two ids, one fingerprint → both skipped, ordinal first fills (I-2) | Integration | SY | `TwoEntriesWithOneFingerprint_AreBothSkipped_AndTheOrdinallyFirstIdFillsTheExclusion` | first id | Red |
| AC-017 | Stored id → synced as usual, exclusion without id, re-entry "loaded" | Integration | SY | `AMatchedIdAlreadyStoredAsAParticipant_IsSynchronizedAsUsual_AndTheExclusionStaysWithoutId` | participant updated; AlreadyLoaded | Red |
| AC-018 | No antiforgery token → `400`, nothing stored | HTTP | A | `WithoutAnAntiforgeryToken_TheEntryIs400_AndNothingChanges` | `400` | Red |
| AC-019 | Notice and disabled button in read-only mode | HTTP | P | `InReadOnlyMode_TheEntryBlock_ShowsTheNotice_AndADisabledButton`, `OutsideReadOnlyMode_TheEntryButtonIsEnabled` | `disabled` only in read-only | Red |
| AC-020 | Keys in both languages | HTTP | T | `EveryEntryKey_ExistsInBothLanguages_NonEmpty_AndTheTextsDiffer` | non-empty, differing | Red |
| AC-020 | Block and outcomes rendered in uk and en | HTTP | T | `ThePage_RendersTheEntryBlock_InTheChosenLanguage`, `TheOutcomes_RenderInTheChosenLanguage` | texts present | Red |
| AC-021 | Lifting finds the first entry (salt created by the entry) | Unit, Integration | U, EN | `AfterTheFirstEntry_ALiftingOfTheSameEmail_RemovesIt` (both) | Lifted 1 | Red |

Further scenarios without an AC of their own: `AFailureInsideTheTransaction_Propagates_AndNothingIsCommitted`,
`TheExclusion_StillHasNoPropertyForANameOrAnEmail` (U); `ARosterEntryWithoutEmail_IsNotMatched`,
`AfterTheFill_NoLogLine_CarriesThePersonsEmail_GoogleUserId_OrFingerprint` (SY);
`AQueryStringEmail_IsNotAnEntry` (P); `AfterAnEntry_NoLogLine_CarriesTheFingerprint` (L);
`TheDesignedEntryAuditRows_AreAccepted`, `AnEntryAuditRowOfTheWrongShape_IsRejected`,
`AnEntryAuditRowWithErasureCounts_IsRejected`, `TheMigration_IsApplied` (DB).
