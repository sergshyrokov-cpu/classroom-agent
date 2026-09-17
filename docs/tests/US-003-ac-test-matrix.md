---
artifact_type: ac_test_matrix
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T10:05:00Z
updated_at: 2026-09-17T10:05:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/designs/api/US-003-api-design.md
    version: 1
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: docs/designs/database/US-003-entity-model.md
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
supersedes: null
---

# US-003 Acceptance Criteria → Test Matrix

Test classes live under `tests/ClassroomAgent.Tests/ControlPlane/`, namespace
`ClassroomAgent.Tests.ControlPlane.<Namespace>`. Levels: **H** HTTP integration,
**S** security, **P** persistence. Every test starts its own Control Plane host
over its own migrated PostgreSQL database and drops it on disposal.

**Status:** `RED` — compiles, fails for missing production behaviour (evidence:
test-generation report v1). `GUARD` — passes already because existing host
behaviour satisfies it; must stay green. *(N)* = theory cases.

| AC | Scenario | Lvl | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | empty section: empty message, warning, add link | H | `Controllers.AllowedAdminListTests` | `Detail_NoEntries_ShowsEmptyMessageWarningAndAddLink` | 200; `AllowedAdmins.Title`, `.Empty`, warning element, add link | RED |
| AC-001 | only this installation's entries, by email, with UTC date | H | `AllowedAdminListTests` | `Detail_ListsOnlyThisInstallationsEntries_OrderedByEmail_WithDateAdded` | other installation's email absent; ivan before olena; both `dd.MM.yyyy HH:mm UTC` | RED |
| AC-001 | revoke link per entry, add link | H | `AllowedAdminListTests` | `Detail_EveryEntryHasRevokeLink_AndAddLinkStays` | `href` to each `…/revocation` and `…/admins/new` | RED |
| AC-001 | US-002 content stays; list page unchanged | H | `AllowedAdminListTests` | `Detail_US002ContentStays` | identifier, client ID, name link; no email on list | RED |
| AC-001, AC-012 | email HTML-encoded | S | `AllowedAdminListTests` | `Detail_EmailWithApostrophe_IsHtmlEncoded` | decoded text has email; raw body does not | RED |
| AC-002 | add form content | H | `Controllers.AllowedAdminAddTests` | `AddForm_ShowsInstallationDomainHintEmptyEmailAndToken` | name, domain, empty `email`, token, back link, form action | RED |
| AC-002 | add mixed case: one lower-case row, owner, time, redirect | H | `AllowedAdminAddTests` | `Add_MixedCaseEmail_RedirectsToDetail_StoresOneLowerCaseEntry_WithOwnerAndTime` | 302 detail; row email lower, installation id, owner id, fake-clock time; listed | RED |
| AC-002, AC-003 | valid name-part shapes | H | `AllowedAdminAddTests` | `Add_ValidNamePartShapes_AreAccepted` *(3)* | 302; stored as given | RED |
| AC-003 | name part of 64 accepted | H | `AllowedAdminAddTests` | `Add_NamePartOf64Characters_IsAccepted` | 302; stored | RED |
| AC-002 | over-posting ignored | S | `AllowedAdminAddTests` | `Add_OverpostedFields_AreIgnored` | identifier, owner id, time not taken from the form | RED |
| AC-002 | no limit | H | `AllowedAdminAddTests` | `Add_ManyEntries_AreAllAccepted` | 12 entries | RED |
| AC-006 | add to suspended installation | H | `AllowedAdminAddTests` | `Add_ToSuspendedInstallation_Succeeds_InstallationUnchanged` | 302; installation row equal | RED |
| AC-002 | installation untouched by adding | P | `AllowedAdminAddTests` | `Add_DoesNotChangeTheInstallation` | installation row equal | RED |
| AC-003 | format rules, first failing key, value refilled, nothing stored | H | `Controllers.AllowedAdminValidationTests` | `Add_InvalidFormat_Returns400WithRuleKey_KeepsValue_CreatesNothing` *(16)* | 400; key per rule (Required … NameDots); input keeps value; no row; no audit | RED |
| AC-003 | foreign domains | H | `AllowedAdminValidationTests` | `Add_EmailOutsideInstallationDomain_Returns400WrongDomain_NamingExpectedDomain` *(6)* | 400; `WrongDomain` text + `school-one.example.test`; no row | RED |
| AC-003, AC-008 | unknown installation before validation | H | `AllowedAdminValidationTests` | `Add_UnknownInstallation_WithInvalidEmail_Returns404` | 404 error page | RED |
| AC-003 | refilled value encoded | S | `AllowedAdminValidationTests` | `RejectedValues_AreHtmlEncodedWhenRefilled` | no raw `<script>`; input value intact | RED |
| AC-003 | emails never logged | S | `AllowedAdminValidationTests` | `RejectedAddedAndRevokedEmails_NeverReachTheLogFile` | log files contain none of the rejected, added, duplicate or revoked emails | RED |
| AC-004 | duplicate, any case | H | `Controllers.AllowedAdminUniquenessTests` | `Add_EmailAlreadyAnEntry_AnyCase_Returns409Taken_KeepsValue_CreatesNothing` *(2)* | 409 `Taken`; value refilled; one row; no audit | RED |
| AC-004 | concurrent additions | H | `AllowedAdminUniquenessTests` | `ConcurrentAdditions_SameEmailDifferentCase_OneCreated_Other409` | {302, 409}; one row; one `allowed_admin_added` | RED |
| AC-004 | same name part in two installations | H | `AllowedAdminUniquenessTests` | `SameNamePart_InTwoInstallations_IsTwoIndependentEntries` | one entry each | RED |
| AC-005 | confirmation page, GET deletes nothing | H | `Controllers.AllowedAdminRevocationTests` | `Confirmation_ShowsEmailInstallationExplanationFormAndCancel_DeletesNothing` | email, name, explanation, confirm, cancel link, POST form with token, no inline script; row kept; no audit | RED |
| AC-005, AC-007 | fewer-than-two note on confirmation (I-10) | H | `AllowedAdminRevocationTests` | `Confirmation_NotesWhenRevokingLeavesFewerThanTwo` *(3)* | note with 1, 2 entries; absent with 3 | RED |
| AC-005 | revoke deletes only that entry | H | `AllowedAdminRevocationTests` | `Revoke_DeletesOnlyThatEntry_RedirectsToDetail_WhichNoLongerListsIt` | 302 detail; other row kept; email gone from page | RED |
| AC-005, AC-007 | revoke last entry | H | `AllowedAdminRevocationTests` | `Revoke_LastEntry_IsAllowed_AndWarningAppears` | 302; no rows; empty message and warning | RED |
| AC-006 | revoke on suspended installation | H | `AllowedAdminRevocationTests` | `Revoke_OnSuspendedInstallation_Succeeds_InstallationUnchanged` | 302; row gone; installation equal | RED |
| AC-005 | re-add after revoke | H | `AllowedAdminRevocationTests` | `Revoke_ThenAddSameEmail_CreatesNewEntryWithNewIdentifierAndTime` | new id, identifier, time | RED |
| AC-008, AC-009 | revoke twice | H | `AllowedAdminRevocationTests` | `Revoke_SubmittedTwice_SecondReturns404_OneAuditRow` | 302 then 404; one audit row | RED |
| AC-008, AC-009 | concurrent revocations | H | `AllowedAdminRevocationTests` | `ConcurrentRevocations_OneRevoked_Other404_OneAuditRow` | {302, 404}; one `allowed_admin_revoked` | RED |
| AC-008 | unknown installation / entry / foreign entry | S | `AllowedAdminRevocationTests` | `UnknownInstallationOrEntry_OrEntryOfAnotherInstallation_Returns404_ChangesNothing` | GET and POST 404 error page without the email; row kept; no audit | RED |
| AC-008 | add form / add for unknown installation | H | `AllowedAdminRevocationTests` | `AddForm_UnknownInstallation_GetAndPostReturn404` | 404; no row | RED |
| AC-008 | non-GUID route values | H | `AllowedAdminRevocationTests` | `NonUuidRouteValues_Return404ErrorPage` *(4)* | 404 error page | GUARD (catch-all) |
| AC-009 | added row | H | `Controllers.AllowedAdminAuditTests` | `Add_WritesAllowedAdminAddedRow` | owner actor, `allowed_admin_added`, target `allowed_admin` + entry id, succeeded, request id, time | RED |
| AC-009 | revoked row with deleted id | H | `AllowedAdminAuditTests` | `Revoke_WritesAllowedAdminRevokedRow_WithIdOfDeletedEntry` | as above, `allowed_admin_revoked`; row deleted | RED |
| AC-009 | refusals and GETs write nothing | H | `AllowedAdminAuditTests` | `RefusedOrNoOpRequests_WriteNoAuditRow` | statuses 400, 400, 409, 404, 200, 200, 404, 400, 400; no new rows | RED |
| AC-009 | no email or values in rows | S | `AllowedAdminAuditTests` | `AuditRows_CarryNoEmailInstallationValuesOrIdentifiers` | JSON of every row free of emails, name, domain, UUIDs | RED |
| AC-009 | rows immutable | P | `AllowedAdminAuditTests` | `AuditRowOfRevokedEntry_CannotBeUpdatedOrDeleted` | update and delete throw; row equal | RED |
| AC-010 | unauthenticated | S | `Security.AllowedAdminAuthorizationTests` | `Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing` *(5)* | 302 `/sign-in`; no email; rows and audit unchanged | RED |
| AC-010 | before setup | S | `AllowedAdminAuthorizationTests` | `BeforeSetup_RedirectsToSetup` *(5)* | 302 `/setup` | RED (GET detail: GUARD) |
| AC-010 | allowed role | S | `AllowedAdminAuthorizationTests` | `SignedInOwner_Returns200` *(2)* | 200 | RED |
| AC-010 | forbidden role | S | `AllowedAdminAuthorizationTests` | `PrincipalWithoutOwnerRole_Returns403_ChangesNothing` *(5)* | 403 error page; nothing changed | RED |
| AC-010 | routes exist, none anonymous, no PUT/PATCH/DELETE | S | `AllowedAdminAuthorizationTests` | `AllowedAdminEndpoints_ExistAndNoneAllowsAnonymous` | exact three patterns | RED |
| AC-010 | enumeration covers new endpoints | S | `Security.AnonymousEndpointTests`, `Security.AntiforgeryTests` (US-001) | `OnlySc4EndpointsAllowAnonymous`, `EveryNonGetEndpoint_WithoutToken_Returns400` | new endpoints included via `SamplePath` | GUARD (existing) |
| AC-011 | add without token | S | `Security.AllowedAdminAntiforgeryTests` | `Add_WithoutToken_Returns400PageExpired_CreatesNothing` | 400 page expired; no row | RED |
| AC-011 | revoke without token | S | `AllowedAdminAntiforgeryTests` | `Revoke_WithoutToken_Returns400PageExpired_DeletesNothing` | 400; row kept | RED |
| AC-011 | token of another session | S | `AllowedAdminAntiforgeryTests` | `Revoke_WithTokenFromAnotherSession_Returns400_DeletesNothing` | 400; row kept | RED |
| AC-011 | GET changes nothing | S | `AllowedAdminAntiforgeryTests` | `GetRequestsWithFormValues_ChangeNothing` | no redirect; rows and audit unchanged | RED |
| AC-011 | other methods | S | `AllowedAdminAntiforgeryTests` | `OtherMethods_DoNotAddOrRevoke` *(3)* | ≥ 400; rows unchanged | RED |
| AC-012 | contract keys uk + en | H | `Localization.AllowedAdminTranslationTests` | `ContractKey_ExistsInUkrainianAndEnglish_AndDiffers` *(18)* | both exist, differ | RED |
| AC-012 | domain argument in both languages | H | `AllowedAdminTranslationTests` | `DomainArgumentKeys_TakeTheDomainInBothLanguages` *(2)* | `{0}` in uk and en | RED |
| AC-012 | Ukrainian by default, emails untranslated | H | `AllowedAdminTranslationTests` | `AllowedAdminPages_AreUkrainianByDefault_EmailsUntranslated` | `lang="uk"` with `Accept-Language: en`; uk texts; email shown | RED |
| AC-002, AC-004, AC-005 | columns, PK | P | `Persistence.AllowedAdminSchemaTests` | `AllowedAdminColumns_MatchDesign_NoPasswordOrSecretColumn` | exact seven columns | RED |
| AC-004 | indexes | P | `AllowedAdminSchemaTests` | `Indexes_Exist` *(3)* | definitions present | RED |
| AC-008 | FKs RESTRICT | P | `AllowedAdminSchemaTests` | `ForeignKeys_ReferenceInstallationAndOwner_WithRestrict` | two FKs, `r` | RED |
| AC-002 | valid row | P | `AllowedAdminSchemaTests` | `ValidRow_IsAccepted` | inserted | RED |
| AC-004 | named unique violations | P | `AllowedAdminSchemaTests` | `DuplicateValue_ViolatesNamedUniqueConstraint` *(2)* | 23505 with constraint name | RED |
| AC-003 | check constraints | P | `AllowedAdminSchemaTests` | `CheckConstraints_RejectBadlyShapedEmail` *(4)*, `CheckConstraints_AllDesignedConstraintsExist` | 23514 `ck_allowed_admin_email_*`; exact two constraints | RED |
| AC-003 | domain-match trigger | P | `AllowedAdminSchemaTests` | `EmailOutsideInstallationDomain_IsRefusedByTrigger` *(3)* | insert throws; no row | RED |
| AC-002 | FK violations | P | `AllowedAdminSchemaTests` | `UnknownInstallationOrOwner_ViolatesForeignKey` | 23503 `fk_allowed_admin_added_by_owner`; unknown installation throws | RED |
| AC-005 | no-update trigger, delete allowed, triggers exist | P | `AllowedAdminSchemaTests` | `AnyUpdate_IsRefusedByTrigger` *(5)*, `Delete_IsAllowed`, `Triggers_ExistOnAllowedAdmin` | updates throw; delete 1 row; two triggers | RED |
| — (PC-2) | migration order and drift | P | `Persistence.MigrationTests` (US-001, changed) | `Migrations_CreateOwnerAuditEventInstallationAndAllowedAdmin_InOrder`, `Model_HasNoPendingChangesAgainstMigrations` | `allowed_admin` table; third migration `_AddAllowedAdmin`; no pending changes | RED; drift GUARD |

Every Acceptance Criterion AC-001 … AC-012 has at least one RED test.
