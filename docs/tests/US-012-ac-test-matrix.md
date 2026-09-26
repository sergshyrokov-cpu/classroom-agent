---
artifact_type: ac_test_matrix
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T17:38:32Z
updated_at: 2026-09-26T17:38:32Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: docs/designs/database/US-012-entity-model.md
    version: 1
  - path: docs/tests/US-012-test-strategy.md
    version: 1
supersedes: null
---

# US-012 Acceptance Criteria → Test Matrix

Owned by TEST_WRITING. `dotnet-implementor` and `security-reviewer` read it; they
do not rebuild it. In this workflow variant there is no separate verification
stage — a green suite **is** the evidence that the Acceptance Criteria hold
(TC-7).

Status column: `RED` = fails now, by design, because the production behaviour
does not exist yet (OD-005 skeleton).

## AC-001 The screen is Admin-only, forbidden to a Dean

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| The list carries every Dean, active and disabled | unit | `ListDeanAccountsQueryTests` | `ItLists_ActiveAndDisabledDeansAlike` | both rows, with their state | RED |
| No Admin account appears | unit | `ListDeanAccountsQueryTests` | `ItLists_NoAdminAccount` | the Admin is absent | RED |
| The four OD-003 columns | unit | `ListDeanAccountsQueryTests` | `EachRow_CarriesTheFourColumnsOd003Fixed` | id, email, state, temporary, last sign-in | RED |
| The list carries no secret | unit | `ListDeanAccountsQueryTests` | `NoRow_CarriesASecret` | no hash, no stamp | RED |
| The policy admits an Admin | integration | `DeanAccountsAuthorizationTests` | `TheManagementPolicy_AdmitsAnAdmin` | succeeded | RED |
| The policy refuses a Dean | integration | `DeanAccountsAuthorizationTests` | `TheManagementPolicy_RefusesADean` | refused | RED |
| An Admin reaches the screen | integration | `DeanAccountsAuthorizationTests` | `AnAdmin_ReachesTheScreen` | `200` | RED |
| An anonymous visitor is challenged | integration | `DeanAccountsAuthorizationTests` | `AnAnonymousVisitor_IsChallenged` | `302` to `/sign-in` | RED |
| The screen lists the seeded Dean | integration | `DeanAccountsPageTests` | `TheScreen_ListsTheDeans` | `200`, the email rendered | RED |

## AC-002 An Admin creates a Dean account

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| The account is a Dean with a temporary password | unit | `CreateDeanAccountUseCaseTests` | `AValidSubmission_CreatesADeanAccount` | role Dean, password method, not disabled | RED |
| Only a hash is stored | unit | `CreateDeanAccountUseCaseTests` | `ThePassword_IsStoredOnlyAsAHash` | hashed through the port | RED |
| The address is stored lower-cased | unit | `CreateDeanAccountUseCaseTests` | `AMixedCaseAddress_IsStoredLowerCased` | normalized | RED |
| One audit row names Admin and account | unit | `CreateDeanAccountUseCaseTests` | `TheCreation_IsAudited` | `dean_account_created`, succeeded | RED |
| Account and row commit together | unit | `CreateDeanAccountUseCaseTests` | `TheAccountAndItsAuditRow_CommitTogether` | one transaction, one commit | RED |
| The POST redirects back | contract | `DeanAccountsPageTests` | `AValidCreation_RedirectsBackToTheScreen` | `302` to `/settings/deans` | RED |

## AC-003 Creation is refused: wrong domain, not an email, already taken

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Another domain | unit | `CreateDeanAccountUseCaseTests` | `AnAddressOutsideTheSchoolDomain_IsRefused` | `OutsideSchoolDomain` | RED |
| A subdomain is not the domain | unit | `CreateDeanAccountUseCaseTests` | `ASubdomainOfTheSchoolDomain_IsRefused` | refused | RED |
| Free-form / empty local part | unit | `CreateDeanAccountUseCaseTests` | `AnAddressThatIsNotAnEmail_IsRefused` | `NotAnEmailAddress` | RED |
| Already used by a Dean | unit | `CreateDeanAccountUseCaseTests` | `AnAddressAlreadyUsedByADean_IsRefused` | `EmailAlreadyUsed` | RED |
| Already used by an **Admin**, same answer | unit | `CreateDeanAccountUseCaseTests` | `AnAddressAlreadyUsedByAnAdmin_IsRefusedTheSameWay` | the same refusal | RED |
| Case-insensitive uniqueness | unit | `CreateDeanAccountUseCaseTests` | `AnAddressDifferingOnlyInCase_IsAlreadyUsed` | refused | RED |
| No fallback to any domain | unit | `CreateDeanAccountUseCaseTests` | `WithNoConfirmedDomain_NothingFallsBackToAcceptingAnyDomain` | nothing created | RED |
| The form re-renders with the email, never the password | contract | `DeanAccountsPageTests` | `ARefusedCreation_PreservesTheEmailAndNeverThePassword` | `400`, email kept | RED |
| The unique index refuses a second row | integration | `DeanAccountSchemaTests` | `ASecondAccountOnTheSameAddress_IsRejected` | `uq_app_user_normalized_email` | RED |

## AC-004 Every password obeys the SC-2 policy

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| 14 characters | unit | `DeanPasswordPolicyTests` | `FourteenCharacters_AreTooShort` | `TooShort` | RED |
| 15 characters | unit | `DeanPasswordPolicyTests` | `FifteenCharacters_AreAccepted` | accepted | RED |
| 128 / 129 characters | unit | `DeanPasswordPolicyTests` | `OneHundredAndTwentyEightCharacters_AreAccepted`, `OneHundredAndTwentyNineCharacters_AreTooLong` | the boundary | RED |
| Counted in characters, not bytes | unit | `DeanPasswordPolicyTests` | `FifteenNonAsciiCharacters_AreAccepted`, `FourteenNonAsciiCharacters_AreTooShort` | accepted / `TooShort` | RED |
| Spaces allowed | unit | `DeanPasswordPolicyTests` | `FifteenSpaces_AreAccepted` | accepted | RED |
| No composition rule | unit | `DeanPasswordPolicyTests` | `NoCompositionRule_IsApplied` | accepted | RED |
| Equal to the login, any case | unit | `DeanPasswordPolicyTests` | `APasswordEqualToTheLogin_IsRefused` | `EqualsLogin` | RED |
| Contains the login / local part | unit | `DeanPasswordPolicyTests` | `APasswordContainingTheLogin_IsRefused`, `APasswordContainingTheLocalPart_IsRefused` | `ContainsLogin` | RED |
| The 4-character boundary of the containment rule | unit | `DeanPasswordPolicyTests` | `AFourCharacterLocalPart_IsCheckedForContainment`, `AThreeCharacterLocalPart_IsNotCheckedForContainment`, `AShortLoginIsStillRefusedWhenThePasswordEqualsIt` | SC-2 v65 | RED |
| Enforced at creation | unit | `CreateDeanAccountUseCaseTests` | `APasswordThatBreaksThePolicy_IsRefused` | `PasswordPolicy` + violation | RED |
| Enforced at a reset | unit | `DeanAccountAdministrationTests` | `AResetPasswordThatBreaksThePolicy_IsRefused` | `EqualsLogin` | RED |
| Enforced at the forced change | unit | `DeanPasswordChangeTests` | `ANewPasswordThatBreaksThePolicy_IsRefused` | `TooShort` | RED |

## AC-005 An Admin disables an account

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Disabled, stamp rotated | unit | `DeanAccountAdministrationTests` | `Disabling_MarksTheAccountAndRotatesTheStamp` | disabled, new stamp | RED |
| Nothing else touched | unit | `DeanAccountAdministrationTests` | `Disabling_TouchesNothingElse` | hash, counter, lockout intact | RED |
| Audited as its own action | unit | `DeanAccountAdministrationTests` | `EachDirection_WritesItsOwnAuditAction` | `dean_account_disabled` | RED |
| Already in that state | unit | `DeanAccountAdministrationTests` | `AnAccountAlreadyInThatState_IsRefused` | `AlreadyInThatState`, nothing written | RED |
| A non-Dean id and an unknown id look alike | unit | `DeanAccountAdministrationTests` | `AnAdminIdAndAnUnknownId_AreRefusedIdentically` | `NoSuchDeanAccount` both | RED |
| Over HTTP: redirect and the row is disabled | contract | `DeanAccountsPageTests` | `Disabling_RedirectsBackAndMarksTheRow` | `302`, disabled | RED |
| Over HTTP: `404`, not `403` | contract | `DeanAccountsPageTests` | `AManagementActionOnANonDeanId_Answers404` | `404` for both | RED |

## AC-006 An Admin re-enables an account

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| State cleared, nothing else | unit | `DeanAccountAdministrationTests` | `ReEnabling_ClearsTheStateAndNothingElse` | active, same hash | RED |
| No lockout cleared | unit | `DeanAccountAdministrationTests` | `ReEnabling_DoesNotClearALockout` | still locked out | RED |
| Its own audit action | unit | `DeanAccountAdministrationTests` | `EachDirection_WritesItsOwnAuditAction` | `dean_account_reenabled` | RED |

## AC-007 A reset clears the lockout and never re-enables

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| New hash, temporary again, stamp rotated | unit | `DeanAccountAdministrationTests` | `AReset_ReplacesTheHashAndMarksItTemporary` | temporary true | RED |
| Lockout and counter cleared | unit | `DeanAccountAdministrationTests` | `AReset_ClearsTheLockout` | zero, null | RED |
| A disabled account stays disabled | unit | `DeanAccountAdministrationTests` | `AReset_LeavesADisabledAccountDisabled` | still disabled | RED |
| Over HTTP | contract | `DeanAccountsPageTests` | `AReset_RedirectsBack` | `302` | RED |

## AC-008 No path deletes an account

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| No DELETE on the collection or on one account | contract | `DeanAccountsPageTests` | `NoDeleteMethod_IsExposed` | not `200`, row still there | RED |
| No delete member on the repository surface | — | enforced by the entity model and reviewed at SECURITY_REVIEW | — | — | n/a |

## AC-009 Read-only refuses all four management actions

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| The guard runs before any repository call | unit | `CreateDeanAccountUseCaseTests` | `InReadOnlyMode_TheGuardRefusesBeforeAnyRepositoryCall` | no read, no hash | RED |
| Exactly one audit row, nothing else staged | unit | `CreateDeanAccountUseCaseTests` | `InReadOnlyMode_TheRefusalWritesExactlyOneAuditRow` | one row, category read-only | RED |
| Disabling refused and audited | unit | `DeanAccountAdministrationTests` | `InReadOnlyMode_DisablingIsRefusedAndAudited` | unchanged + row | RED |
| A reset refused before any hashing | unit | `DeanAccountAdministrationTests` | `InReadOnlyMode_AResetIsRefusedBeforeAnyHashing` | no hash computed | RED |
| The screen is still served; the action answers `409` | contract | `DeanAccountsPageTests` | `InReadOnlyMode_TheScreenIsServedAndTheActionsAreRefused` (3 causes) | `200` + `409` | RED |
| Viewing works in read-only | unit | `ListDeanAccountsQueryTests` | `ItWorks_InReadOnlyMode` | the list, no guard call | RED |

## AC-010 The six-step sign-in sequence, in order

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Step 1, anonymous audit row | unit | `DeanSignInSequenceTests` | `Step1_AnUnknownLogin_IsRefusedAndAuditedWithoutAnIdentifier` | `unknown_login`, no identifier | RED |
| Step 1 verifies no password | unit | `DeanSignInSequenceTests` | `Step1_VerifiesNoPassword` | hasher untouched | RED |
| Step 2 does not verify the password | unit | `DeanSignInSequenceTests` | `Step2_ALockedOutAccount_IsRefusedWithoutVerifyingThePassword` | `LockedOut` | RED |
| Step 2 does not move the counter | unit | `DeanSignInSequenceTests` | `Step2_DoesNotMoveTheCounter` | unchanged | RED |
| Step 3 counts the failure | unit | `DeanSignInSequenceTests` | `Step3_AWrongPassword_IsRefusedAndCounted` | counter +1 | RED |
| Step 4 with the correct password only | unit | `DeanSignInSequenceTests` | `Step4_ADisabledAccountWithTheCorrectPassword_IsToldSo` | counter unchanged | RED |
| A wrong password on a disabled account is step 3 | unit | `DeanSignInSequenceTests` | `ADisabledAccountWithAWrongPassword_IsAnOrdinaryWrongPassword` | `WrongPassword` | RED |
| Lockout is checked before the disabled state | unit | `DeanSignInSequenceTests` | `ALockedOutDisabledAccount_AnswersLockedOut` | `LockedOut` | RED |
| Step 6 signs in and records the time | unit | `DeanSignInSequenceTests` | `Step6_ACorrectPassword_SignsTheDeanIn` | signed in, audited | RED |
| Case-insensitive login | unit | `DeanSignInSequenceTests` | `TheLogin_IsMatchedCaseInsensitively` | found | RED |
| Over HTTP: every outcome is `302` | contract | `DeanSignInPageTests` | `AnUnknownLogin_RedirectsBackToTheSignInPage`, `ACorrectPassword_SignsTheDeanIn` | `302` | RED |
| **Steps 1, 2 and 3 are indistinguishable** | security | `DeanSignInPageTests` | `TheThreeCommonRefusals_AreIndistinguishable` | same status and `Location` | RED |
| The page stays anonymous | security | `DeanSignInPageTests` | `TheSignInPage_StaysAnonymous` | `200` | RED |
| No token → `400`, no counter moved | security | `DeanSignInPageTests` | `ASignInWithoutAnAntiforgeryToken_IsRefused` | `400`, counter 0 | RED |
| The typed password is never echoed | security | `DeanSignInPageTests` | `NoResponse_CarriesTheTypedPassword` | absent | RED |
| Two different message keys for the two cases | integration | `DeanAccountTranslationTests` | `TheCommonRefusalAndTheDisabledMessage_AreDifferent` | different texts | RED |

## AC-011 Lockout: five attempts, fifteen minutes, never permanent

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Four failures do not lock | unit | `DeanLockoutTests` | `FourFailures_DoNotLock` | not locked | RED |
| The fifth locks | unit | `DeanLockoutTests` | `TheFifthFailure_Locks` | locked until +15 min | RED |
| The correct password is refused while locked | unit | `DeanLockoutTests` | `WhileLocked_EvenTheCorrectPasswordIsRefused` | `LockedOut` | RED |
| One second before the limit | unit | `DeanLockoutTests` | `OneSecondBeforeTheLimit_TheLockHolds` | still locked | RED |
| At the limit it lifts by itself | unit | `DeanLockoutTests` | `AtTheLimit_TheLockLiftsByItself` | sign-in proceeds | RED |
| The numbers are SC-2's | unit | `DeanLockoutTests` | `ThePolicyNumbers_AreTheOnesSc2Fixes` | 5 and 15 minutes | RED |
| A success resets the counter | unit | `DeanSignInSequenceTests` | `ASuccess_ResetsTheCounter` | zero | RED |

## AC-012 A temporary password must be changed at the next sign-in

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Step 5 stops before a session | unit | `DeanSignInSequenceTests` | `Step5_ATemporaryPassword_StopsBeforeASession` | `TemporaryPassword` | RED |
| The change clears the mark and rotates the stamp | unit | `DeanPasswordChangeTests` | `TheForcedChange_ClearsTheTemporaryMark` | cleared | RED |
| New ≠ temporary | unit | `DeanPasswordChangeTests` | `ANewPasswordEqualToTheTemporaryOne_IsRefused` | `EqualsTemporaryPassword` | RED |
| Audited as the Dean's own | unit | `DeanPasswordChangeTests` | `TheForcedChange_IsAuditedAsTheDeansOwn` | `dean_password_changed` | RED |
| Over HTTP: sent to the form, then signed in | contract | `DeanSignInPageTests`, `DeanPasswordPageTests` | `ATemporaryPassword_SendsTheDeanToTheForcedChange`, `TheForcedChange_SignsTheDeanIn` | `302` each | RED |
| The restricted session may reach nothing else | security | `DeanPasswordPageTests` | `WhileThePasswordIsTemporary_EveryOtherPageSendsTheDeanBack` | back to the form | RED |
| Unreachable without that session | security | `DeanPasswordPageTests` | `WithNoSession_TheFormSendsTheVisitorToSignIn`, `WithAnOrdinarySession_TheFormSendsTheDeanAway` | `302` | RED |

## AC-013 A Dean changes their own password, in read-only too

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| The current password is required | unit | `DeanPasswordChangeTests` | `TheVoluntaryChange_NeedsTheCurrentPassword` | refused, hash intact | RED |
| A wrong current password is not a sign-in attempt | unit | `DeanPasswordChangeTests` | `AWrongCurrentPassword_IsNotAFailedSignInAttempt` | counter 0, no lockout | RED |
| Success replaces the hash and rotates the stamp | unit | `DeanPasswordChangeTests` | `TheVoluntaryChange_ReplacesTheHashAndRotatesTheStamp` | new hash | RED |
| New = current is **accepted** (I-7) | unit | `DeanPasswordChangeTests` | `ANewPasswordEqualToTheCurrentOne_IsAccepted` | accepted | RED |
| Both changes work in read-only | unit | `DeanPasswordChangeTests` | `BothChanges_WorkInReadOnlyMode` | no guard consulted | RED |
| Audited | unit | `DeanPasswordChangeTests` | `TheVoluntaryChange_IsAudited` | one row each | RED |
| The page: Dean `200`, Admin `403` | contract | `DeanPasswordPageTests` | `TheOwnPasswordPage_IsServedToADean`, `TheOwnPasswordPage_IsForbiddenToAnAdmin` | `200` / `403` | RED |
| PRG on success; `400` and no echo on failure | contract | `DeanPasswordPageTests` | `AChange_RedirectsBackToThePage`, `AWrongCurrentPassword_Answers400AndEchoesNothing` | `302` / `400` | RED |
| The policies admit and refuse the right roles | integration | `DeanAccountsAuthorizationTests` | `TheOwnPasswordPolicy_AdmitsADean`, `TheOwnPasswordPolicy_RefusesAnAdmin` | succeeded / refused | RED |
| The sign-in path works in read-only | contract | `DeanSignInPageTests` | `TheSignIn_WorksInReadOnlyMode` (3 causes) | `302` to the form | RED |
| The sequence consults no guard | unit | `DeanSignInSequenceTests` | `TheSequence_NeverConsultsTheReadOnlyGuard` | no operation recorded | RED |

## AC-014 A Dean is forbidden from the three settings pages over HTTP

**The criterion that closes the finding carried from US-010 (F-1) through US-011
(F-1).** Every row below uses a real Dean session, not a synthetic principal.

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| `GET` on each of the three pages | security | `DeanSessionBoundaryTests` | `ASignedInDean_IsForbiddenFromTheAdminOnlySettingsPages` (3 cases) | `403` | RED |
| `POST` on the two that accept one | security | `DeanSessionBoundaryTests` | `ASignedInDean_IsForbiddenFromPostingToThem` (2 cases) | `403` | RED |
| And from the Dean accounts screen itself | security | `DeanSessionBoundaryTests` | `ASignedInDean_IsForbiddenFromTheDeanAccountsScreen` | `403` | RED |
| The same Dean still sees what both roles may see | security | `DeanSessionBoundaryTests` | `TheSameDean_StillSeesWhatBothRolesMaySee` | `200` | RED |
| A management POST without a token | security | `DeanAccountsAuthorizationTests` | `AManagementPost_WithoutAnAntiforgeryToken_IsRefused` | `400`, nothing written | RED |

## AC-015 Every user-visible string is translated

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| All 39 contract keys in `uk` and `en` | integration | `DeanAccountTranslationTests` | `EveryKey_IsTranslatedInBothLanguages` (39 cases) | both resolve | RED |
| Both files hold the same key set | integration | `DeanAccountTranslationTests` | `BothFiles_HoldTheSameKeys` | equal sets | RED |

## AC-016 The audit trail carries what SC-11 requires, and no personal data

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| Creation row | unit | `CreateDeanAccountUseCaseTests` | `TheCreation_IsAudited` | the §4.4 shape | RED |
| Disable / re-enable rows | unit | `DeanAccountAdministrationTests` | `EachDirection_WritesItsOwnAuditAction` | two actions | RED |
| Read-only refusal rows | unit | `CreateDeanAccountUseCaseTests`, `DeanAccountAdministrationTests` | `InReadOnlyMode_*` | outcome refused | RED |
| Password-change rows | unit | `DeanPasswordChangeTests` | `TheForcedChange_IsAuditedAsTheDeansOwn`, `TheVoluntaryChange_IsAudited` | `dean_password_changed` | RED |
| Sign-in rows, succeeded and refused | unit | `DeanSignInSequenceTests` | `Step1_…`, `Step3_…`, `Step6_…` | the four categories | RED |
| Exactly one row per refusal | unit | `DeanSignInSequenceTests` | `EachRefusal_CommitsExactlyOneAuditRow` | one, at the commit | RED |
| The six new actions are accepted by the constraint | integration | `DeanAccountAuditSchemaTests` | `EachNewAction_IsAccepted` (6 cases) | insert succeeds | RED |
| The three new categories are accepted | integration | `DeanAccountAuditSchemaTests` | `EachNewRefusalCategory_IsAccepted` (3 cases) | insert succeeds | RED |
| The unknown-login shape is legal | integration | `DeanAccountAuditSchemaTests` | `TheUnknownLoginShape_IsLegal` | insert succeeds | RED |
| The new target type is accepted | integration | `DeanAccountAuditSchemaTests` | `TheAccountTargetType_IsAccepted` | insert succeeds | RED |
| The list stays closed | integration | `DeanAccountAuditSchemaTests` | `AnActionOutsideTheList_IsStillRejected` | constraint violation | RED |
| No column added to the table | integration | `DeanAccountAuditSchemaTests` | `TheTable_GainsNoColumn` | twelve columns | RED |

## Persistence, beyond the Acceptance Criteria

| Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|
| The column: not null, default false | integration | `DeanAccountSchemaTests` | `TheColumn_IsNotNullableAndDefaultsToFalse` | declared so | RED |
| The constraint exists | integration | `DeanAccountSchemaTests` | `TheConstraint_IsDeclared` | present | RED |
| An Admin row with the flag is rejected by PostgreSQL | integration | `DeanAccountSchemaTests` | `AnAdminRowWithATemporaryPassword_IsRejected` | constraint violation | RED |
| A Dean row may carry either state | integration | `DeanAccountSchemaTests` | `ADeanRow_MayCarryEitherState` (2 cases) | insert succeeds | RED |
| No table, no index added | integration | `DeanAccountSchemaTests` | `TheStory_AddsNoTableAndNoIndex` | five tables, two indexes | RED |
| Five migrations, `_AddDeanAccounts` last | integration | `AppUserMigrationTests` (**modified**) | `TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` | 4 → 5 | RED |

## Coverage

All sixteen Acceptance Criteria are mapped. No mandatory criterion is without a
test, so there is no blocking coverage finding.

The one criterion partly outside the suite is AC-008: the absence of a delete
path is asserted over HTTP and by the repository surface the entity model fixes,
but no test can prove that a **future** Story adds none.
