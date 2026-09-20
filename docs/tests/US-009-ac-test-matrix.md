---
artifact_type: ac_test_matrix
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T14:05:00Z
updated_at: 2026-09-20T14:05:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-009-configure-workspace-connection.md
    version: null
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/tests/US-009-test-strategy.md
    version: 1
supersedes: null
---

# US-009 Acceptance Criteria → Test Matrix

Every test class is under `tests/ClassroomAgent.Tests/`. Status `RED` means the
test fails before implementation because the behaviour does not exist yet;
`GREEN` means it passes already and locks an invariant the Story must not break.

## AC-001 Only an Admin reaches the connection settings

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| the policy admits an Admin | security | `WorkspaceConnectionAuthorizationTests` | `ThePolicy_AdmitsAnAdmin` | authorised | RED |
| the policy refuses a Dean | security | `WorkspaceConnectionAuthorizationTests` | `ThePolicy_RefusesADean` | refused | RED |
| the policy refuses anonymous | security | `WorkspaceConnectionAuthorizationTests` | `ThePolicy_RefusesAnAnonymousPrincipal` | refused | RED |
| both endpoints declare it | security | `WorkspaceConnectionAuthorizationTests` | `BothEndpoints_DeclareTheAdminPolicy` | GET and POST, neither anonymous | RED |
| the SC-4 list is unchanged | security | `WorkspaceConnectionAuthorizationTests` | `TheAnonymousList_GainsNothing` | path not anonymous | GREEN |
| anonymous is sent to sign in | integration | `WorkspaceConnectionAuthorizationTests` | `AnAnonymousVisitor_IsSentToSignIn` | `302 /sign-in` | RED |
| a signed-in Admin reaches it | integration | `WorkspaceConnectionAuthorizationTests` | `ASignedInAdmin_ReachesThePage` | `200` | RED |
| no antiforgery token | security | `WorkspaceConnectionAuthorizationTests` | `TheSaveWithoutAnAntiforgeryToken_IsRefused` | `400`, nothing written | RED |
| no antiforgery exemption | security | `WorkspaceConnectionAuthorizationTests` | `TheSaveEndpoint_IsNotExemptFromAntiforgery` | no exemption metadata | RED |
| a GET saves nothing | security | `WorkspaceConnectionAuthorizationTests` | `AGetWithTheFieldInTheQuery_SavesNothing` | no row, no audit row | RED |

## AC-002 An unconfigured installation says so plainly

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| the state is stated | integration | `WorkspaceConnectionPageTests` | `WithNoConnectionSaved_ThePageSaysItIsNotConfigured` | the "not configured" text | RED |
| the form is empty | integration | `WorkspaceConnectionPageTests` | `WithNoConnectionSaved_TheFormIsEmpty` | empty field, token present | RED |
| the domain is shown, not asked for | integration | `WorkspaceConnectionPageTests` | `ThePage_ShowsTheInstallationDomainAndDoesNotAskForIt` | domain rendered, no domain input | RED |
| the two explanations | integration | `WorkspaceConnectionPageTests` | `ThePage_ExplainsTheTechnicalAccountAndThatSavingDoesNotCheckIt` | both sentences | RED |
| opening writes nothing | integration | `WorkspaceConnectionPageTests` | `OpeningThePage_WritesNothing` | tables unchanged | RED |
| the navigation entry | integration | `WorkspaceConnectionPageTests` | `TheLandingPage_LinksToTheConnectionSettings` | entry and path on the landing page | RED |

## AC-003 Saving a valid connection stores the domain and the technical account

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| success redirects | contract | `SaveWorkspaceConnectionTests` | `AValidSave_RedirectsBackToThePage` | `302` to the page | RED |
| both values stored | integration | `SaveWorkspaceConnectionTests` | `AValidSave_StoresTheDomainAndTheTechnicalAccount` | one row, domain and address | RED |
| shown on reopening | integration | `SaveWorkspaceConnectionTests` | `AfterASave_ThePageShowsTheStoredValuesAndAConfirmation` | values and confirmation | RED |
| normalisation | integration | `SaveWorkspaceConnectionTests` | `TheAddress_IsTrimmedAndLowerCased` | stored lower-cased, trimmed | RED |
| no outbound call | security | `SaveWorkspaceConnectionTests` | `TheSave_CallsNothingOutsideTheInstallation` | transport unchanged | GREEN |
| no credential column | security | `SaveWorkspaceConnectionTests` | `TheTable_HoldsNoCredentialColumn` | no such column | RED |

## AC-004 A domain the Owner did not approve is refused

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| equivalent spellings accepted | integration | `SaveWorkspaceConnectionTests` | `AnEquivalentSpellingOfTheDomain_IsAccepted` | `302`, bound to the allowed domain | RED |
| a domain in the request is ignored | security | `WorkspaceConnectionInvariantTests` | `ADomainSentInTheRequest_IsIgnored` | row bound to the installation domain | RED |
| a mismatched stored connection grants nothing | integration | `WorkspaceConnectionInvariantTests` | `AMismatchedStoredConnection_IsNotUsedToAcceptAForeignAddress` | `409`, row untouched | RED |
| a refusal changes nothing | integration | `WorkspaceConnectionInvariantTests` | `ARefusedSave_LeavesAStoredConnectionUntouched` | table unchanged | RED |

## AC-005 A technical account outside the domain is refused

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| three foreign addresses | integration | `WorkspaceConnectionInvariantTests` | `AnAddressOutsideTheDomain_IsRefused` | `409`, nothing written | RED |
| the refusal names the domain | integration | `WorkspaceConnectionInvariantTests` | `TheRefusal_NamesTheAllowedDomain` | message and domain | RED |
| the typed value survives | integration | `WorkspaceConnectionInvariantTests` | `AfterARefusal_TheTypedAddressIsStillInTheForm` | field keeps the value | RED |
| a subdomain is not the domain | integration | `WorkspaceConnectionInvariantTests` | `ASubdomainOfTheSchoolDomain_IsNotTheDomain` | `409` | RED |
| the Admin's own address is not refused by this rule | integration | `WorkspaceConnectionInvariantTests` | `TheAdminsOwnAddress_IsNotRefusedByTheDomainRule` | `302` | RED |

## AC-006 Malformed input is rejected before business logic

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| eleven malformed addresses | validation | `WorkspaceConnectionValidationTests` | `AMalformedAddress_IsRejectedWithoutWriting` | `400`, nothing written | RED |
| a per-field message | validation | `WorkspaceConnectionValidationTests` | `AMissingAddress_IsRejectedWithAMessageOnTheField` | translated message | RED |
| over the length limit | boundary | `WorkspaceConnectionValidationTests` | `AnAddressLongerThanTheLimit_IsRejected` | `400` | RED |
| exactly the length limit | boundary | `WorkspaceConnectionValidationTests` | `AnAddressOfExactlyTheLimit_IsAccepted` | `302`, stored | RED |
| the field absent | validation | `WorkspaceConnectionValidationTests` | `ARequestWithoutTheField_IsRejected` | `400` | RED |
| validation precedes the rules | validation | `WorkspaceConnectionValidationTests` | `AMalformedForeignAddress_IsRejectedAsMalformed` | `400`, not `409` | RED |
| the rejected value is not logged | security | `WorkspaceConnectionValidationTests` | `TheRejectedValue_IsNotLogged` | absent from the log | GREEN |
| a refusal logs no address | security | `WorkspaceConnectionValidationTests` | `ARefusedSave_LogsNoAddress` | absent from the log | GREEN |
| a success logs no address | security | `WorkspaceConnectionValidationTests` | `ASuccessfulSave_LogsNoAddress` | absent from the log | GREEN |

## AC-007 Changing the connection updates the one record

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| a change keeps the identity | integration | `SaveWorkspaceConnectionTests` | `ChangingTheAccount_UpdatesTheOneRecord` | same id, new address | RED |
| the previous value is gone | integration | `SaveWorkspaceConnectionTests` | `AfterAChange_ThePreviousAddressIsNotInTheTable` | not present | RED |
| an identical save is accepted | integration | `SaveWorkspaceConnectionTests` | `SavingTheSameValuesAgain_IsAccepted` | `302`, same row | RED |
| a pre-existing row is updated | integration | `SaveWorkspaceConnectionTests` | `WithAConnectionAlreadyStored_TheSaveUpdatesIt` | one row | RED |
| a second row is impossible | persistence | `WorkspaceConnectionSchemaTests` | `ASecondRow_IsRejected` | `23505` on the singleton index | RED |

## AC-008 Saving and changing are audited without personal data

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| a first save | integration | `WorkspaceConnectionAuditTests` | `AFirstSave_WritesOneRowNamingTheConnection` | one row, target id set | RED |
| the actor | integration | `WorkspaceConnectionAuditTests` | `TheActor_IsTheAdminsAccountAndRole` | account id and `admin` | RED |
| the request id | integration | `WorkspaceConnectionAuditTests` | `TheRow_CarriesARequestId` | non-empty | RED |
| a change | integration | `WorkspaceConnectionAuditTests` | `AChange_IsAuditedAsTheSameActionOnTheSameConnection` | two rows, one target | RED |
| three refusals | integration | `WorkspaceConnectionAuditTests` | `ARefusedSave_IsAuditedWithItsCategory` | category, no target id | RED |
| one row per attempt | integration | `WorkspaceConnectionAuditTests` | `EachAttempt_WritesExactlyOneRow` | three rows | RED |
| validation writes none | integration | `WorkspaceConnectionAuditTests` | `AMalformedRequest_WritesNoAuditRow` | none | GREEN |
| no personal datum | security | `WorkspaceConnectionAuditTests` | `NoRow_CarriesTheAddressOrTheDomain` | no `@`, no domain | GREEN |
| rows are immutable | persistence | `WorkspaceConnectionAuditTests` | `AnAuditRow_CannotBeUpdated` | `PostgresException` | RED |

## AC-009 Read-only mode blocks the save and keeps the view

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| the save is refused, three causes | integration | `WorkspaceConnectionReadOnlyTests` | `InReadOnlyMode_TheSaveIsRefused` | `409` | RED |
| nothing is written | integration | `WorkspaceConnectionReadOnlyTests` | `InReadOnlyMode_NothingIsWrittenToTheConnection` | no row | RED |
| a stored connection is untouched | integration | `WorkspaceConnectionReadOnlyTests` | `InReadOnlyMode_AStoredConnectionIsUntouched` | unchanged | RED |
| the refusal is audited | integration | `WorkspaceConnectionReadOnlyTests` | `InReadOnlyMode_TheRefusalIsAudited` | refused row | RED |
| the reason is named | integration | `WorkspaceConnectionReadOnlyTests` | `TheRefusal_NamesTheReason` | BR-025 reason | RED |
| viewing still works | integration | `WorkspaceConnectionReadOnlyTests` | `InReadOnlyMode_ViewingWorksWhileSavingDoesNot` | `200` and `409` | RED |
| the page is served with its reason | integration | `WorkspaceConnectionPageTests` | `InReadOnlyMode_ThePageIsServedWithItsReason` | `200`, reason | RED |
| the form is not hidden | integration | `WorkspaceConnectionPageTests` | `InReadOnlyMode_TheFormIsNeitherHiddenNorDisabled` | field present | RED |
| the BR-026 list is unchanged | security | `WorkspaceConnectionReadOnlyTests` | `ThePermittedServiceWriteList_IsUnchanged` | four members | GREEN |
| no new permitted write | security | `WorkspaceConnectionReadOnlyTests` | `NoNewUseCase_IsRegisteredAsAPermittedServiceWrite` | three entries | GREEN |

## AC-010 An installation that has never been legitimated cannot be configured

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| the page says the domain is unknown | integration | `WorkspaceConnectionPageTests` | `WithNoSuccessfulLegitimacyCheck_ThePageSaysTheDomainIsUnknown` | the "unknown" text | RED |
| the save is refused | integration | `WorkspaceConnectionInvariantTests` | `WithNoKnownDomain_TheSaveIsRefused` | `409`, nothing written | RED |
| a stale domain licenses nothing | integration | `WorkspaceConnectionInvariantTests` | `WithADomainButNoSuccessfulCheck_TheSaveIsStillRefused` | `409` | RED |
| the refusal is audited | integration | `WorkspaceConnectionAuditTests` | `ASaveWithNoKnownDomain_IsAudited` | refused row | RED |

## AC-011 Every new string is translated

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| nineteen keys in both languages | integration | `WorkspaceConnectionTranslationTests` | `EveryKey_ExistsInBothLanguages` | present, non-empty | RED |
| the files carry the same keys | integration | `WorkspaceConnectionTranslationTests` | `TheTwoFiles_CarryTheSameKeys` | equal sets | GREEN |
| the page in English | integration | `WorkspaceConnectionTranslationTests` | `ThePage_RendersInEnglishForAnEnglishAccount` | English title | RED |
| data is not translated | integration | `WorkspaceConnectionTranslationTests` | `TheStoredAddress_IsRenderedAsStored` | address as stored | RED |
| refusals come from the files | integration | `WorkspaceConnectionTranslationTests` | `EveryRefusalMessage_ComesFromTheTranslationFiles` | three keys, both languages | RED |

## AC-012 The schema change ships as a migration

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| the table and its columns | persistence | `WorkspaceConnectionSchemaTests` | `TheMigration_CreatesTheTable` | six columns, lengths, nullability | RED |
| inconsistent rows rejected | persistence | `WorkspaceConnectionSchemaTests` | `AnInconsistentRow_IsRejected` | `23514` | RED |
| mixed case rejected | persistence | `WorkspaceConnectionSchemaTests` | `AMixedCaseRow_IsRejected` | named lower-case constraints | RED |
| the only index | persistence | `WorkspaceConnectionSchemaTests` | `TheOnlyIndex_IsTheSingletonOne` | two entries | RED |
| no foreign key | persistence | `WorkspaceConnectionSchemaTests` | `TheTable_HasNoForeignKey` | none | RED |
| the new audit codes | persistence | `WorkspaceConnectionSchemaTests` | `TheAuditTable_AcceptsTheNewActionAndTargetType` | insert succeeds | RED |
| target pairing still guarded | persistence | `WorkspaceConnectionSchemaTests` | `AnAuditRowWithATargetIdButNoType_IsRejected` | `ck_audit_event_target` | RED |
| unknown target type | persistence | `WorkspaceConnectionSchemaTests` | `AnUnknownTargetType_IsRejected` | `ck_audit_event_target_type_value` | RED |
| the expected tables | persistence | `WorkspaceConnectionSchemaTests` | `TheInstallationDatabase_HasTheExpectedTables` | four tables | RED |

## Coverage summary

All twelve Acceptance Criteria are mapped. 84 test methods, 141 executed cases,
across nine classes.

One clause is not separable at this level and is recorded as a known limitation
in the test strategy §8: AC-010's requirement that the domain refusal be proven
independently of the read-only refusal. An installation that never confirmed its
legitimacy is always read-only, and the guard runs first, so the tests accept
either audit category in that one assertion and IMPLEMENTATION must add a direct
unit test of the use case for the `DomainNotConfirmed` branch.
