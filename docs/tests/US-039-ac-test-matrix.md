---
artifact_type: ac_test_matrix
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T09:20:00Z
updated_at: 2026-10-03T09:20:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
  - path: docs/designs/database/US-039-db-design.md
    version: 1
  - path: docs/tests/US-039-test-strategy.md
    version: 1
supersedes: null
---

# US-039 Acceptance Criteria → Test Matrix

Status column: **RED** = fails before IMPLEMENTATION for the expected reason;
**GREEN-EXPLAINED** = passes before IMPLEMENTATION for a stated, legitimate
reason (test-generation report §5). Class prefixes: `A/` =
`Application/UseCases`, `W/` = `Web/…`, `C/` = `ControlPlane/…`.

| AC | Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Admin pages render the switcher (4 pages) | Integration HTTP | W/Pages/LanguageSwitcherTests | EverySignedInAdminPage_RendersTheSwitcher | switcher, offers `en`, `aria-current`, return path = page | RED |
| AC-001 | Dean pages render the switcher | Integration HTTP | W/Pages/LanguageSwitcherTests | EverySignedInDeanPage_RendersTheSwitcher | switcher, offers `en` | RED |
| AC-001 | Return path keeps the query | Integration HTTP | W/Pages/LanguageSwitcherTests | TheSwitcher_CarriesThePathAndQuery | `returnPath` = path?query | RED |
| AC-001 | Same page back in English, next pages too, no re-sign-in | Integration HTTP | W/Pages/LanguageSwitcherTests | ChoosingEnglish_ShowsTheSamePageInEnglish_WithoutSigningInAgain | 302 → page; `lang="en"`; switcher offers `uk` | RED |
| AC-001 | Back to Ukrainian | Integration HTTP | W/Pages/LanguageSwitcherTests | ChoosingUkrainianAfterEnglish_SwitchesBack | `uk` stored and rendered | RED |
| AC-001 | Admin may choose (role-agnostic use case) | Unit | A/ChooseUiLanguageUseCaseTests | AnAdmin_CanChooseToo | returns `En`, row changed | RED |
| AC-001 | Owner pages render the switcher (3 pages) | Integration HTTP | C/Security/OwnerUiLanguageTests | EveryOwnerPage_RendersTheSwitcher | as installation | RED |
| AC-001 | Owner: same page in English, stored | Integration HTTP | C/Security/OwnerUiLanguageTests | ChoosingEnglish_ShowsTheSamePageInEnglish_AndStoresIt | 302 → page; `lang="en"`; `owner.ui_language = en` | RED |
| AC-002 | Valid code stored, only language moves | Unit | A/ChooseUiLanguageUseCaseTests | AValidCode_IsStoredOnThatAccount (uk, en) | language set; stamp, hash unchanged; 1 commit; no audit | RED |
| AC-002 | Another account untouched (unit) | Unit | A/ChooseUiLanguageUseCaseTests | AnotherAccount_KeepsItsLanguage | other row `Uk` | RED |
| AC-002 | Follows the Dean to a new sign-in and another browser | Integration HTTP | W/Pages/LanguageSwitcherTests | TheChoice_FollowsTheDeanToANewSignIn_AndAnotherBrowser | both sessions `lang="en"` | RED |
| AC-002 | Follows the Admin to a new Google sign-in | Integration HTTP | W/Pages/LanguageSwitcherTests | TheChoice_FollowsTheAdminToANewSignIn | `lang="en"` | RED |
| AC-002 | Another user's language and pages unchanged | Integration HTTP | W/Pages/LanguageSwitcherTests | AnotherUsersLanguage_IsUnchanged | second Dean `uk` stored and rendered | RED |
| AC-002 | Follows the Owner to a new sign-in | Integration HTTP | C/Security/OwnerUiLanguageTests | TheChoice_FollowsTheOwnerToANewSignIn | `lang="en"` | RED |
| AC-003 | Stored in each read-only cause (Application + PostgreSQL) | Integration App | A/UiLanguageReadOnlyTests | ChoosingALanguage_IsStoredInReadOnlyMode (3 causes) | `en` stored through the real backstop | RED |
| AC-003 | Nothing else opened in the same scope | Integration App | A/UiLanguageReadOnlyTests | AfterTheChoice_AnUndeclaredWriteInTheSameScope_IsStillRefused | `ReadOnlyModeException`; legitimacy rows unchanged | RED |
| AC-003 | Control: outside read-only mode | Integration App | A/UiLanguageReadOnlyTests | OutsideReadOnlyMode_ChoosingALanguage_IsStored | `en` stored | RED |
| AC-003 | Registry entry is sign-in bookkeeping | Unit (structural) | A/UiLanguageReadOnlyTests | TheRegistry_DeclaresTheLanguageChoiceAsSignInBookkeeping | entry present, `SignInBookkeeping` | RED |
| AC-003 | Registry grows by exactly this entry | Unit (structural) | A/PermittedServiceWriteTests (modified) | TheRegistry_DeclaresOnlyWritesOnTheClosedList | seven named entries | RED |
| AC-003 | Enum (BR-026 list) unchanged | Unit (structural) | A/PermittedServiceWriteTests | TheList_HasExactlyTheFourMembersOfBr026 | four members | GREEN-EXPLAINED |
| AC-003 | Commit made under the declaration, closed after | Unit | A/ChooseUiLanguageUseCaseTests | TheCommit_IsDeclaredAsSignInBookkeeping_AndTheDeclarationCloses | `[SignInBookkeeping]`; scope `null` after | RED |
| AC-003 | Over HTTP, never 409 (3 causes) | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | InReadOnlyMode_TheChoiceIsStored_NotRefused | 302; stored; rendered `en` | RED |
| AC-004 | Anonymous challenged, nothing stored (installation) | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AnAnonymousRequest_IsChallenged_AndChangesNothing | 302 `/sign-in`; control succeeds | RED |
| AC-004 | Authenticated, POST only, not exempt (installation) | Integration (enumeration) | W/Security/UiLanguageChoiceSecurityTests | TheAction_IsAuthenticated_PostOnly_AndNotExemptFromAntiforgery | single endpoint, `[POST]` | RED |
| AC-004 | GET changes nothing | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AGet_ChangesNothing | 404/405; `uk` kept; control POST stores | RED |
| AC-004 | No token → 400 page expired | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | WithoutTheToken_TheChoiceIsRefused | 400 + `Error.PageExpired`; control succeeds | RED |
| AC-004 | Forged account id ignored | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AnAccountIdInTheRequest_IsIgnored | own row `en`, other `uk` | RED |
| AC-004 | Anonymous challenged (Control Plane) | Integration HTTP | C/Security/OwnerUiLanguageTests | AnAnonymousRequest_IsChallenged_AndChangesNothing | 302 `/sign-in`; control succeeds | RED |
| AC-004 | Authenticated, POST only, not exempt (Control Plane) | Integration (enumeration) | C/Security/OwnerUiLanguageTests | TheAction_IsAuthenticated_PostOnly_AndNotExemptFromAntiforgery | single endpoint, `[POST]` | RED |
| AC-004 | GET / no token change nothing (Control Plane) | Integration HTTP | C/Security/OwnerUiLanguageTests | AGetOrAPostWithoutToken_ChangesNothing | 404/405; 400; control stores | RED |
| AC-005 | Invalid values refused, nothing committed (15 values) | Unit | A/ChooseUiLanguageUseCaseTests | AnyOtherValue_IsRefused_AndNothingIsCommitted | `null`; 0 commits; control commits | RED |
| AC-005 | Oversized value refused | Unit | A/ChooseUiLanguageUseCaseTests | AnOversizedValue_IsRefused | `null`; control commits | RED |
| AC-005 | Invalid codes over HTTP, marker not echoed or logged (10 values) | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AnInvalidCode_IsRefused_NotStored_NotLogged | 400 + page-expired text; `uk` kept; marker absent | RED |
| AC-005 | Missing or oversized code over HTTP | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AMissingOrOversizedCode_IsRefused | 400 / 400; `uk` kept | RED |
| AC-005 | Invalid codes on the Control Plane (5 values) | Integration HTTP | C/Security/OwnerUiLanguageTests | AnInvalidCode_IsRefused_NotStored_NotLogged | 400; `uk` kept; marker absent from logs | RED |
| AC-006 | No switcher on anonymous pages; school default (uk, en) | Integration HTTP | W/Pages/LanguageSwitcherTests | AnonymousPages_HaveNoSwitcher_AndUseTheSchoolDefault | sign-in and error page without switcher, `lang` = default; a signed-in page has it | RED |
| AC-006 | Error page without switcher even signed in, in the user's language (I-5) | Integration HTTP | W/Pages/LanguageSwitcherTests | TheErrorPage_HasNoSwitcher_EvenWhenSignedIn_ButUsesTheUsersLanguage | no switcher; `lang="en"` | RED |
| AC-006 | Control Plane: setup, sign-in, error page | Integration HTTP | C/Security/OwnerUiLanguageTests | AnonymousPages_HaveNoSwitcher_AndStayUkrainian | no switcher, `uk`; signed-in home has it; signed-in error page has none, `en` | RED |
| AC-007 | Last successful check in the chosen language | Integration HTTP | W/Localization/DateFormatTests | TheLastSuccessfulCheck_FollowsTheChosenLanguage (uk) | short date + `HH:mm UTC` | GREEN-EXPLAINED |
| AC-007 | — same, English | Integration HTTP | W/Localization/DateFormatTests | TheLastSuccessfulCheck_FollowsTheChosenLanguage (en) | en short date | RED |
| AC-007 | Dean list last sign-in (uk, en) | Integration HTTP | W/Localization/DateFormatTests | TheDeansLastSignIn_FollowsTheChosenLanguage | culture short date; no ISO | RED |
| AC-007 | The two formats differ for the test dates | Unit | W/Localization/DateFormatTests | TheTwoLanguages_FormatTheTestDatesDifferently | not equal | GREEN-EXPLAINED |
| AC-007 | Control Plane dates follow the culture | Integration HTTP | existing C/Controllers/InstallationLastCheckTests, AllowedAdminListTests | (existing) | unchanged, regression | GREEN-EXPLAINED |
| AC-008 | Switcher keys in both files (installation) | Integration | W/Localization/UiLanguageTranslationTests | EverySwitcherKey_IsTranslated (uk, en) | three keys, non-empty | RED |
| AC-008 | Labels are self-names (installation) | Integration | W/Localization/UiLanguageTranslationTests | TheLanguageLabels_AreTheSelfNames_InBothFiles | «УКР», «ENG» in both | RED |
| AC-008 | Accessible name translated (installation) | Integration | W/Localization/UiLanguageTranslationTests | TheSwitchersAccessibleName_DiffersBetweenTheLanguages | differ | RED |
| AC-008 | Same three for the Control Plane | Integration | C/Localization/UiLanguageTranslationTests | (same three methods) | as installation | RED |
| AC-008 | Labels rendered on the page in both languages | Integration HTTP | W/Pages/LanguageSwitcherTests | TheSwitcherLabels_AreTheSelfNames_InBothLanguages | «УКР» and «ENG» on uk and en pages | RED |
| AC-009 | Installation session still ends 8 h after original sign-in | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | TheReissuedSession_StillEndsEightHoursAfterTheOriginalSignIn | 200 at 7:59 (`en`), 302 at 8:01 | RED |
| AC-009 | Stamp not rotated; kept copy dies at sign-out | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | TheReissuedCookie_StillDiesAtSignOut_AndTheStampIsNotRotated | stamp equal; replay → `/sign-in` | RED |
| AC-009 | Re-issued session keeps the role | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | TheReissuedSession_KeepsTheRole | Admin 200, Dean 403 | RED |
| AC-009 | Re-issued cookie keeps SC-2 attributes (Lax) | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | TheReissuedCookie_KeepsTheSc2Attributes | Secure, HttpOnly, Lax, no expiry | RED |
| AC-009 | Entity method keeps the stamp | Unit | A/ChooseUiLanguageUseCaseTests | ChooseUiLanguage_SetsTheLanguage_AndKeepsTheSecurityStamp | stamp equal, concurrency stamp renewed | RED |
| AC-009 | Undefined enum rejected | Unit | A/ChooseUiLanguageUseCaseTests | ChooseUiLanguage_RejectsAnUndefinedValue | `ArgumentOutOfRangeException` | RED |
| AC-009 | Control Plane: 8 h limit, stamp, cookie (Strict) | Integration HTTP | C/Security/OwnerUiLanguageTests | TheReissuedSession_StillEndsEightHoursAfterTheOriginalSignIn; TheReissuedCookie_StillDiesAtSignOut_AndTheStampIsNotRotated; TheReissuedCookie_KeepsTheSc2Attributes | as installation, `SameSite=Strict` | RED |
| AC-010 | Foreign return paths → `/` (8 shapes) | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AForeignReturnPath_LeadsToTheLandingPage | `Location: /`; choice stored | RED |
| AC-010 | Over-long return path → `/` | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | AnOverLongReturnPath_LeadsToTheLandingPage | `Location: /` | RED |
| AC-010 | Local path with query kept | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | ALocalReturnPath_WithAQuery_IsKept | `Location` = path?query | RED |
| AC-010 | Control Plane foreign return paths (4 shapes) | Integration HTTP | C/Security/OwnerUiLanguageTests | AForeignReturnPath_LeadsToHome | `Location: /` | RED |
| OD-007 / FR-012 | Temporary-password Dean chooses on the forced form and stays held | Integration HTTP | W/Security/UiLanguageChoiceSecurityTests | ADeanWithATemporaryPassword_ChoosesOnTheForcedChangePage_AndStaysHeldThere | switcher on the form; stored; other path → form; form in `en` | RED |
| FR-003 | Unknown account refused | Unit | A/ChooseUiLanguageUseCaseTests | AnUnknownAccount_IsRefused_AndNothingIsCommitted | `null`; control commits | RED |
| FR-003 | Stored language chosen again | Unit | A/ChooseUiLanguageUseCaseTests | TheStoredLanguage_CanBeChosenAgain | returns `Uk` | RED |

Every Acceptance Criterion AC-001 … AC-010 of the Specification has at least one
RED test; no mandatory criterion is untested.
