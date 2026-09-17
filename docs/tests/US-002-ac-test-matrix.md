---
artifact_type: ac_test_matrix
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T14:02:00Z
updated_at: 2026-09-16T14:36:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-002-register-installation.md
    version: null
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-002-entity-model.md
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
supersedes: null
---

# US-002 Acceptance Criteria → Test Matrix

Test classes live under `tests/ClassroomAgent.Tests/ControlPlane/`, namespace
`ClassroomAgent.Tests.ControlPlane.<Namespace>`. Levels: **H** HTTP integration,
**S** security, **P** persistence. Every test starts its own Control Plane host
over its own migrated PostgreSQL database (Testcontainers) and drops the database
on disposal.

**Status:** `RED` — compiles, fails for missing production behaviour (evidence:
test-generation report v1). `GUARD` — passes already because existing host
behaviour satisfies it; must stay green. A row with *(N cases)* is a theory.

| AC | Scenario | Lvl | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | empty list | H | `Controllers.InstallationListTests` | `List_NoInstallations_ShowsEmptyMessageAndRegisterLink` | `200`; `Installations.Empty`; link `/installations/new` | RED |
| AC-001 | row content | H | `Controllers.InstallationListTests` | `List_ShowsNameDomainStatusCreationTimeAndDetailLink` | name, domain, `Installation.Status.Active`, `07.03.2026 21:05 UTC`, link to `/installations/{uuid}` | RED |
| AC-001 | time stays UTC | H | `Controllers.InstallationListTests` | `List_CreationTimeIsUtcNotConvertedToAnotherZone` | `31.12.2026 23:30 UTC`, not next day (OD-003) | RED |
| AC-001 | suspended label | H | `Controllers.InstallationListTests` | `List_SuspendedInstallation_ShowsSuspendedLabel` | `Installation.Status.Suspended` | RED |
| AC-001 | order | H | `Controllers.InstallationListTests` | `List_OrdersByNameIgnoringCase_ThenByDomain` | name ignore-case, then domain (I-1) | RED |
| AC-001 | home link | H | `Controllers.InstallationListTests` | `Home_LinksToInstallations` | `/` links to `/installations` with `Home.Installations` | RED |
| AC-002 | form | H | `Controllers.InstallationRegistrationTests` | `RegistrationForm_HasNameDomainClientIdAndToken_NoStatusField` | empty `name`, `domain`, `clientId`, token; no `status`; posts to `/installations` | RED |
| AC-002 | redirect to detail | H | `Controllers.InstallationRegistrationTests` | `ValidRegistration_RedirectsToDetailPageOfNewInstallation` | `302 /installations/{uuid}`; detail `200`; one row with that UUID | RED |
| AC-002 | stored values | P | `Controllers.InstallationRegistrationTests` | `ValidRegistration_StoresActiveInstallationWithSubmittedValuesAndUtcCreationTime` | values as submitted; `active`; `created_at` = clock, UTC | RED |
| AC-002 | lower-case domain | P | `Controllers.InstallationRegistrationTests` | `ValidRegistration_MixedCaseDomain_IsStoredInLowerCase` | `School-One.Example.TEST` → lower | RED |
| AC-002 | UUIDv4 | H | `Controllers.InstallationRegistrationTests` | `ValidRegistration_GeneratesDistinctVersion4Identifiers` | two distinct version-4 UUIDs | RED |
| AC-002 | over-posting | H | `Controllers.InstallationRegistrationTests` | `PostedStatusIdentifierAndCreationTime_AreIgnored` | posted `status`, `identifier`, `id`, `createdAt` ignored | RED |
| AC-002 | columns, no secret column | P | `Persistence.InstallationSchemaTests` | `InstallationColumns_MatchDesign_NoKeyOrSecretColumn` | exactly the 8 designed columns; `pk_installation` | RED |
| AC-002 | migration | P | `Persistence.MigrationTests` | `Migrations_CreateOwnerAuditEventAndInstallation_InOrder` | tables incl. `installation`; `InitialOwnerAndAudit` then `AddInstallation` | RED |
| AC-003 | invalid name *(10 cases)* | H | `Controllers.InstallationValidationTests` | `Register_InvalidName_Returns400WithMessage_CreatesNothing` | `400`; key per rule (Required, Length BMP/astral, EdgeWhitespace space/NBSP, InvalidCharacters `\n` `\t` ZWSP RLM); values refilled; no row | RED |
| AC-003 | valid names *(4 cases)* | H | `Controllers.InstallationValidationTests` | `Register_NameAtBoundaryOrWithAnyScript_IsAccepted` | 1 char, 200 BMP, 200 astral code points, punctuation — stored | RED |
| AC-003 | invalid domain *(19 cases)* | H | `Controllers.InstallationValidationTests` | `Register_InvalidDomain_Returns400WithMessage_CreatesNothing` | Required, Length (2, 254), Characters (scheme, path, `@`, Cyrillic, space, `_`), NoDot, Labels (edge/double dots, edge hyphens, 64-char label), Idn (`xn--`, any case) | RED |
| AC-003 | valid domains *(6 cases)* | H | `Controllers.InstallationValidationTests` | `Register_DomainAtBoundary_IsAcceptedAndStoredLowerCase` | `a.b`, 253 chars, 63-char label, inner hyphen, leading digit, mixed case → lower | RED |
| AC-003 | invalid client ID *(7 cases)* | H | `Controllers.InstallationValidationTests` | `Register_InvalidClientId_Returns400WithMessage_CreatesNothing` | Required; Format for 9/33 digits, space, letter, `+`, Arabic-Indic digits | RED |
| AC-003 | valid client IDs *(4 cases)* | H | `Controllers.InstallationValidationTests` | `Register_ClientIdAtBoundary_IsAcceptedAsDigitString` | 10, 32, 21 digits; leading zero kept | RED |
| AC-003 | all fields at once | H | `Controllers.InstallationValidationTests` | `Register_AllFieldsInvalid_ReportsEveryField` | three messages in one `400` | RED |
| AC-003 | rename invalid *(10 cases)* | H | `Controllers.InstallationValidationTests` | `Rename_InvalidName_Returns400WithMessage_NameUnchanged` | `400`, key, value refilled, name unchanged | RED |
| AC-003 | client ID change invalid *(7 cases)* | H | `Controllers.InstallationValidationTests` | `ChangeClientId_InvalidClientId_Returns400WithMessage_ClientIdUnchanged` | `400`, key, value refilled, unchanged | RED |
| AC-003 | values not logged | S | `Controllers.InstallationValidationTests` | `RejectedAndStoredValues_NeverReachTheLogFile` | no rejected or stored name, domain, client ID in the log file (SC-10) | RED |
| AC-003 | DB check constraints *(6 cases)* | P | `Persistence.InstallationSchemaTests` | `CheckConstraints_RejectViolatingRows` | named `ck_installation_*` for empty name, bad domain shape, bad client ID, bad status | RED |
| AC-003 | constraints present | P | `Persistence.InstallationSchemaTests` | `CheckConstraints_AllDesignedConstraintsExist` | the five designed check constraints | RED |
| AC-003 | upper-case domain in DB | P | `Persistence.InstallationSchemaTests` | `UpperCaseDomain_IsRejectedByACheckConstraint` | check violation on a `ck_installation_domain_*` | RED |
| AC-003 | upper bounds in DB | P | `Persistence.InstallationSchemaTests` | `ValidRow_AtUpperBounds_IsAccepted` | 200 astral chars, 253-char domain, 32 digits | RED |
| AC-004 | duplicate domain *(2 cases: same, other case)* | H | `Controllers.InstallationUniquenessTests` | `Register_DomainAlreadyRegistered_AnyCase_Returns409OnDomain` | `409`; `Installation.Domain.Taken` only; values refilled; other name not shown; no row, no audit | RED |
| AC-004 | suspended holder | H | `Controllers.InstallationUniquenessTests` | `Register_DomainOfSuspendedInstallation_Returns409` | `409`, domain taken | RED |
| AC-004 | duplicate client ID | H | `Controllers.InstallationUniquenessTests` | `Register_ClientIdAlreadyRegistered_Returns409OnClientId` | `409`; `Installation.ClientId.Taken` only; no row, no audit | RED |
| AC-004 | both taken, same holder | H | `Controllers.InstallationUniquenessTests` | `Register_DomainAndClientIdBothRegistered_ReportsBothFields` | both messages | RED |
| AC-004 | both taken, different holders | H | `Controllers.InstallationUniquenessTests` | `Register_DomainAndClientIdHeldByDifferentInstallations_ReportsBothFields` | both messages | RED |
| AC-004 | change to another's client ID | H | `Controllers.InstallationUniquenessTests` | `ChangeClientId_ToAnotherInstallationsClientId_Returns409_Unchanged` | `409`; unchanged; no audit; other not named | RED |
| AC-004 | concurrent same domain | H | `Controllers.InstallationUniquenessTests` | `ConcurrentRegistrations_SameDomainDifferentCase_OneCreated_Other409` | one `302`, one `409` (domain taken); one row; one `installation_created` | RED |
| AC-004 | concurrent same client ID | H | `Controllers.InstallationUniquenessTests` | `ConcurrentRegistrations_SameClientId_OneCreated_Other409` | one `302`, one `409` (client ID taken); one row; one audit row | RED |
| AC-004 | unique indexes *(3 cases)* | P | `Persistence.InstallationSchemaTests` | `UniqueIndexes_Exist` | `uq_installation_identifier`, `_domain`, `_client_id` unique | RED |
| AC-004 | constraint names *(3 cases)* | P | `Persistence.InstallationSchemaTests` | `DuplicateValue_ViolatesNamedUniqueConstraint` | `23505` with the designed constraint name (conflict mapping contract) | RED |
| AC-005 | detail values | H | `Controllers.InstallationDetailTests` | `Detail_ShowsIdentifierNameDomainStatusCreationTimeAndClientId` | six values; `16.09.2026 13:29 UTC` | RED |
| AC-005 | copy and note | H | `Controllers.InstallationDetailTests` | `Detail_IdentifierIsSelectableText_WithCopyButtonScriptAndConfigurationNote` | `#installation-identifier` text; button `data-copy-target`; script `/js/copy-identifier.js`; configuration note; copied text | RED |
| AC-005 | no domain/status/delete control | H | `Controllers.InstallationDetailTests` | `Detail_HasNameAndClientIdActions_NoDomainStatusOrDeleteControl` | links to name, client-id, list; no form to `/installations…`; no domain/status/delete link or input | RED |
| AC-005 | no inline script | S | `Controllers.InstallationDetailTests` | `Detail_NoInlineScriptOrInlineEventHandler` | every `<script>` has `src`; no `on*=` | RED |
| AC-005 | output encoding | S | `Controllers.InstallationDetailTests` | `EnteredValues_AreHtmlEncoded_OnDetailListAndForms` | markup in a name encoded on detail, list, name form | RED |
| AC-005 | unknown UUID | H | `Controllers.InstallationDetailTests` | `Detail_UnknownIdentifier_Returns404ErrorPage` | `404`, `Error.NotFound`, no data | RED |
| AC-005 | non-UUID *(4 cases)* | H | `Controllers.InstallationDetailTests` | `NonUuidIdentifier_Returns404ErrorPage` | `404`, `Error.NotFound` | GUARD |
| AC-005 | unknown UUID on edit pages *(2 cases)* | H | `Controllers.InstallationDetailTests` | `EditPages_UnknownIdentifier_GetAndPostReturn404` | GET and POST `404` (before validation); no audit | RED |
| AC-005 | copy script | S | `Controllers.InstallationDetailTests` | `CopyScript_IsServedAsStaticFile_WithoutHardCodedText` | anonymous `200`; uses clipboard and `data-copy-target`; no Cyrillic text | RED |
| AC-005 | identifier/domain immutable *(2 cases)* | P | `Persistence.InstallationSchemaTests` | `UpdatingIdentifierOrDomain_IsRefusedByTrigger` | `UPDATE` refused; row unchanged | RED |
| AC-005 | other columns mutable | P | `Persistence.InstallationSchemaTests` | `UpdatingNameClientIdAndStatus_IsAllowed` | name, client ID, status update | RED |
| AC-005 | no delete | P | `Persistence.InstallationSchemaTests` | `DeletingInstallation_IsRefusedByTrigger` | `DELETE` refused | RED |
| AC-005 | triggers present | P | `Persistence.InstallationSchemaTests` | `Triggers_ExistOnInstallation` | both designed triggers | RED |
| AC-006 | form prefilled | H | `Controllers.InstallationRenameTests` | `NameForm_IsPrefilledWithStoredName` | `name` = stored; token; no domain/client ID input | RED |
| AC-006 | rename | H | `Controllers.InstallationRenameTests` | `ValidRename_ChangesOnlyName_RedirectsToDetail` | `302` detail; only `name` and `updated_at` change | RED |
| AC-006 | duplicate name allowed | H | `Controllers.InstallationRenameTests` | `Rename_ToNameOfAnotherInstallation_IsAccepted` | `302`; two rows with one name | RED |
| AC-006 | unchanged | H | `Controllers.InstallationRenameTests` | `UnchangedName_RedirectsToDetail_WritesNothing` | `302`; row identical (`updated_at` too); no audit | RED |
| AC-006 | case-only change | H | `Controllers.InstallationRenameTests` | `NameDifferingOnlyInCase_IsAChange` | ordinal comparison: stored, audited | RED |
| AC-006 | suspended | H | `Controllers.InstallationRenameTests` | `Rename_SuspendedInstallation_IsAllowed` | renamed; still suspended (I-7) | RED |
| AC-007 | form and note | H | `Controllers.InstallationClientIdTests` | `ClientIdForm_IsPrefilled_AndExplainsTheChange` | `clientId` = stored; `Installation.ClientId.ChangeNote`; no name/domain input | RED |
| AC-007 | change | H | `Controllers.InstallationClientIdTests` | `ValidChange_ChangesOnlyClientId_RedirectsToDetail` | `302` detail; only `client_id` and `updated_at`; leading zero kept | RED |
| AC-007 | unchanged | H | `Controllers.InstallationClientIdTests` | `UnchangedClientId_RedirectsToDetail_WritesNothing` | `302`; row identical; no audit | RED |
| AC-007 | suspended | H | `Controllers.InstallationClientIdTests` | `ChangeClientId_SuspendedInstallation_IsAllowed` | changed; still suspended (I-7) | RED |
| AC-008 | created row | H | `Controllers.InstallationAuditTests` | `Registration_WritesInstallationCreatedRow` | one row: owner/id, `installation_created`, `installation`/internal id, succeeded, no category, request id, clock time | RED |
| AC-008 | renamed row | H | `Controllers.InstallationAuditTests` | `Rename_WritesInstallationRenamedRow` | one `installation_renamed` row, same shape | RED |
| AC-008 | client ID row | H | `Controllers.InstallationAuditTests` | `ClientIdChange_WritesInstallationClientIdChangedRow` | one `installation_client_id_changed` row, same shape | RED |
| AC-008 | refusals not audited | H | `Controllers.InstallationAuditTests` | `RefusedSubmissions_WriteNoAuditRow` | `400`/`409`/unchanged `302`/`404`/no-token `400` — no row | RED |
| AC-008 | no values in rows | S | `Controllers.InstallationAuditTests` | `AuditRows_CarryNoNameDomainClientIdOrIdentifier` | row JSON without names, domain, client IDs, UUID | RED |
| AC-009 | anonymous *(8 cases)* | S | `Security.InstallationAuthorizationTests` | `Anonymous_WithOwnerAccount_RedirectsToSignIn_ChangesNothing` | `302 /sign-in`; no data; nothing changed | RED |
| AC-009 | before setup *(8 cases)* | S | `Security.InstallationAuthorizationTests` | `BeforeSetup_RedirectsToSetup` | `302 /setup` | RED |
| AC-009 | allowed role *(5 cases)* | S | `Security.InstallationAuthorizationTests` | `SignedInOwner_Returns200` | `200` on every page | RED |
| AC-009 | forbidden role *(8 cases)* | S | `Security.InstallationAuthorizationTests` | `PrincipalWithoutOwnerRole_Returns403_ChangesNothing` | `403`, `Error.Forbidden`; nothing changed | RED |
| AC-009 | enumeration | S | `Security.InstallationAuthorizationTests` | `InstallationEndpoints_ExistAndNoneAllowsAnonymous` | exactly the 5 patterns; none anonymous; no PUT/PATCH/DELETE | RED |
| AC-009 | host-wide anonymous list | S | `Security.AnonymousEndpointTests` (US-001) | `OnlySc4EndpointsAllowAnonymous` | still only SC-4 entries, now including the new endpoints | GUARD |
| AC-010 | register without token | S | `Security.InstallationAntiforgeryTests` | `Register_WithoutToken_Returns400PageExpired_CreatesNothing` | `400`, `Error.PageExpired`; no row, no audit | RED |
| AC-010 | edit without token *(2 cases)* | S | `Security.InstallationAntiforgeryTests` | `EditForm_WithoutToken_Returns400PageExpired_ChangesNothing` | `400`; unchanged; no audit | RED |
| AC-010 | foreign token | S | `Security.InstallationAntiforgeryTests` | `EditForm_WithTokenFromAnotherSession_Returns400_ChangesNothing` | `400`; unchanged | RED |
| AC-010 | GET changes nothing | S | `Security.InstallationAntiforgeryTests` | `GetRequestsWithFormValues_ChangeNothing` | query values on every GET page change nothing | RED |
| AC-010 | other methods *(3 cases)* | S | `Security.InstallationAntiforgeryTests` | `OtherMethodsOnInstallation_DoNotChangeOrDelete` | PUT/PATCH/DELETE → ≥ `400`; rows unchanged | RED |
| AC-010 | host-wide antiforgery | S | `Security.AntiforgeryTests` (US-001) | `EveryNonGetEndpoint_WithoutToken_Returns400` | every POST incl. the new ones → `400` page expired (sample `{id}` is a UUID) | GUARD |
| AC-011 | contract keys *(21 cases)* | H | `Localization.InstallationTranslationTests` | `ContractKey_ExistsInUkrainianAndEnglish_AndDiffers` | each key in `uk` and `en`, different texts | RED |
| AC-011 | Ukrainian default | H | `Localization.InstallationTranslationTests` | `InstallationPages_AreUkrainianByDefault` | `lang="uk"`, `uk` texts despite `Accept-Language: en` | RED |
| AC-011 | values untranslated | H | `Localization.InstallationTranslationTests` | `EnteredValues_AreShownExactlyAsStored` | name, domain, client ID as stored | RED |
| AC-011 | key completeness | H | `Localization.TranslationCompletenessTests` (US-001) | `EveryKey_ExistsInUkrainianAndEnglish` | `uk` and `en` key sets equal, none empty | GUARD |

## Coverage summary

| AC | Mapped scenarios | Tests (methods) |
|---|---|---|
| AC-001 | 6 | 6 |
| AC-002 | 8 | 8 |
| AC-003 | 14 | 14 |
| AC-004 | 10 | 10 |
| AC-005 | 14 | 14 |
| AC-006 | 6 | 6 |
| AC-007 | 4 | 4 |
| AC-008 | 5 | 5 |
| AC-009 | 6 | 6 |
| AC-010 | 6 | 6 |
| AC-011 | 4 | 4 |

Every Acceptance Criterion has at least one `RED` test. The `GUARD` rows are
US-001 tests or host behaviour that must keep holding once the new endpoints
exist.
