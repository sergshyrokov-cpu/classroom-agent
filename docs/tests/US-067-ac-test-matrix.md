---
artifact_type: ac_test_matrix
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T09:01:01Z
updated_at: 2026-10-29T09:01:01Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/designs/api/US-067-openapi.yaml
    version: 1
  - path: docs/designs/database/US-067-db-design.md
    version: 1
  - path: docs/tests/US-067-test-strategy.md
    version: 1
supersedes: null
---

# US-067 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests.*`): **U** = `Application.UseCases.ChangeDeanLoginUseCaseTests`,
**P** = `Web.Pages.ChangeDeanLoginPageTests`, **S** = `Infrastructure.Persistence.DeanLoginChangedAuditSchemaTests`,
**T** = `Web.Localization.DeanAccountTranslationTests` (existing; key list extended). Status is the TEST_WRITING
red-phase state; the generation report holds the run.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Same account, new login lower-cased and trimmed | Unit | U | AValidChange_GivesTheSameAccountTheNewLogin_LowerCased | no refusal, same id, Email/NormalizedEmail new, nothing added | RED |
| AC-001 | PRG, screen shows new login + confirmation, row same id | Integration | P | AValidChange_RedirectsBack_AndTheScreenShowsTheNewLoginAndTheConfirmation | 302 → `/settings/deans`; new email + confirmation text; old email absent | RED |
| AC-001 | Templates show the new address as author (I-6) | Integration | P | TheDeansTemplates_ShowTheNewAddressAsAuthor | before: old email; after: new email, old absent | RED |
| AC-001 | Earlier audit rows refer to the same account | Integration | P | EarlierAuditRows_StillReferToTheSameAccount | sign-in rows keep actor id = Dean id | RED |
| AC-001 | Disabled account may be changed (OD-001 a) | Unit | U | ADisabledAccount_CanHaveItsLoginChanged | changed, still disabled | RED |
| AC-002 | New address (any case) + old password signs in; old address refused, `unknown_login` | Integration | P | TheNewAddressSignsInWithTheOldPassword_AndTheOldAddressIsRefused | new: 302 not sign-in/forced change; old: 302 `/sign-in` + refused audit row | RED |
| AC-003 | Stamp rotated | Unit | U | AValidChange_RotatesTheSecurityStamp | SecurityStamp differs | RED |
| AC-003 | Pre-change cookie replayed after the change | Integration / security | P | AnOpenSessionOfTheDean_EndsOnItsNextRequest | before 200; after 302 `/sign-in` | RED |
| AC-004 | Malformed / empty / whitespace | Unit | U | AMalformedAddress_IsRefused_AndNothingChanges (6 cases) | NotAnEmailAddress; nothing written | RED |
| AC-004 | Outside domain / subdomain / suffix domain | Unit | U | AnAddressOutsideTheSchoolDomain_IsRefused_AndNothingChanges (3 cases) | OutsideSchoolDomain; nothing written | RED |
| AC-004 | No school domain known | Unit | U | WithNoSchoolDomainKnown_TheAddressIsRefused | OutsideSchoolDomain | RED |
| AC-004 | Same refusal as creation for the same input (FR-002) | Unit | U | EveryRefusedAddress_IsRefusedExactlyAsCreationRefusesIt (7 cases) | equal outcomes | RED |
| AC-004 | Page: 400, creation message, nothing changes, value not echoed, creation form same message | Integration / contract | P | ARefusedAddress_ChangesNothing_AndShowsTheCreationMessage (6 cases incl. missing field) | 400; message text; row unchanged; no audit | RED |
| AC-004 | Rejected value not logged (SC-10) | Integration / security | P | TheRejectedValue_IsNotWrittenToTheLog | 400 ×2; marker absent from logs | RED |
| AC-005 | Another Dean's address, any case | Unit | U | AnotherDeansAddress_IsAlreadyUsed_AndNothingChanges (2 cases) | EmailAlreadyUsed | RED |
| AC-005 | An Admin's address | Unit | U | AnAdminsAddress_IsAlreadyUsed_AndNothingChanges | EmailAlreadyUsed | RED |
| AC-005 | Race on the unique index (FR-004) | Unit | U | AnAddressTakenBetweenTheCheckAndTheSave_IsAlreadyUsed_AndNothingIsCommitted | EmailAlreadyUsed, no commit | RED |
| AC-005 | Page: 400 "already in use" (Dean, mixed case, Admin) | Integration / contract | P | AnAddressInUse_ChangesNothing_AndSaysSo (3 cases) | 400; message; unchanged | RED |
| AC-006 | Temporary, locked, disabled, retention clock untouched | Unit | U | AValidChange_LeavesEverythingElseOfTheAccountUntouched | all fields equal, stamp/login changed | RED |
| AC-006 | Same, column by column in PostgreSQL | Integration / persistence | P | AllElseOfTheAccountStays_WhenTheLoginChanges | only email/normalized/stamp differ | RED |
| AC-007 | One audit row, Admin actor, account object, one transaction | Unit | U | AValidChange_WritesOneAuditRow_InOneTransactionWithTheChange | one row, fields as FR-007 | RED |
| AC-007 | No property holds an address | Unit / security | U | TheAuditRow_HoldsNeitherAddress | no address in any property | RED |
| AC-007 | Stored row, `row_to_json` free of addresses | Integration / persistence | P | TheChange_IsAuditedOnce_WithoutEitherAddress | one `dean_login_changed` row, no address | RED |
| AC-007 | Factory accepts the action | Unit | U | TheManagementAuditFactory_AcceptsTheLoginChange | row built | RED |
| AC-007 | CHECK accepts the code; list stays closed | Persistence | S | TheSuccessRow_IsAcceptedByTheActionList; AnActionOutsideTheList_IsStillRejected | insert 1 row; unknown code → `ck_audit_event_action` | RED; GREEN guard |
| AC-007 | Migration under the designed name; Down guard; Down/Up | Persistence / migration | S | TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne; Down_IsRefused_WhileALoginChangeRowExists; DownAndUp_WithoutSuchRows_RemoveAndRestoreTheCode | as db-design §4 | RED |
| AC-008 | Guard first, no repository read | Unit | U | InReadOnlyMode_TheGuardRefusesBeforeAnyRepositoryCall | ReadOnlyModeException; 0 reads | RED |
| AC-008 | Refused audit row with `DeanLoginChanged` (SPEC_REVIEW m-1) | Unit | U | InReadOnlyMode_TheRefusalWritesOneRefusedAuditRow_WithTheLoginChangeAction | one refused row, read-only category, Dean id | RED |
| AC-008 | CHECK accepts the refusal row | Persistence | S | TheReadOnlyRefusalRow_IsAcceptedByTheActionList | insert 1 row | RED |
| AC-008 | Page: 409 per read-only cause, only the refusal audited | Integration | P | InReadOnlyMode_TheChangeIsRefused_AndOnlyTheRefusalIsAudited (per cause) | 409; row unchanged; one refused row | RED |
| AC-009 | Dean forbidden, other and own account | Integration / security | P | ADean_IsForbidden_ForAnotherAccountAndForTheirOwn | 403 ×2; unchanged | RED |
| AC-009 | Anonymous → sign-in | Integration / security | P | AnAnonymousRequest_IsSentToSignIn | 302 `/sign-in` | RED (route absent → 404) |
| AC-010 | Old address free for a new account | Integration | P | TheOldAddress_IsFreeForANewAccount | 302; two Deans, ids differ | RED |
| (FR-006, OD-002 a) | Own address, any case/spaces → unchanged, nothing written | Unit | U | TheAccountsOwnAddress_IsLoginUnchanged_AndNothingIsWritten (3 cases) | LoginUnchanged; no write | RED |
| (FR-006) | Page 409 "login unchanged" | Integration / contract | P | TheAccountsOwnAddress_IsLoginUnchanged_AndNothingIsWritten | 409; message; stamp unchanged | RED |
| (FR-002, I-1) | Order: account → address; domain → same; same → uniqueness | Unit | U | TheAccountIsCheckedBeforeTheAddress; TheDomainIsCheckedBeforeTheSameAddress; TheSameAddressIsCheckedBeforeUniqueness | as named | RED |
| (FR-002 step 2) | Unknown id, Admin's id → 404 | Unit + Integration | U, P | AnUnknownId_IsNoSuchDeanAccount; AnAdminsId_IsNoSuchDeanAccount_AndTheAdminsLoginStays; AnUnknownIdAndAnAdminsId_Answer404 | NoSuchDeanAccount / 404 | U: RED; P: GREEN by design (absent route also answers 404) |
| (Entity model §2) | Entity refuses an Admin and a blank login | Unit | U | TheEntity_RefusesToChangeAnAdminsLogin; TheEntity_RefusesABlankLogin (2) | exceptions; login unchanged | RED |
| (FR-010, D-1, D-2) | Form per Dean row | Integration | P | TheScreen_OffersALoginChangeFormForEachDean | action path, `newEmail`, label + button text | RED |
| (FR-011, D-5) | Four new keys in uk and en | Translation | T | EveryKey_IsTranslatedInBothLanguages (4 new cases) | non-empty, not the key | RED |
| (§7, SC-10) | Success logs neither address | Integration / security | P | ASuccessfulChange_WritesNeitherAddressToTheLog | 302; addresses absent | RED |
| (§8) | No antiforgery token → 400; non-numeric id → 404 | Integration / security | P | APostWithoutAnAntiforgeryToken_IsRefused; ANonNumericId_MatchesNoRoute | 400; 404 | RED; GREEN guard |

Every Acceptance Criterion AC-001 … AC-010 has at least one mapped test.
