---
artifact_type: ac_test_matrix
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-20T20:40:00Z
updated_at: 2026-09-20T20:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-010-connection-instructions.md
    version: null
  - path: docs/specifications/US-010-spec.md
    version: 1
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
  - path: docs/designs/api/US-010-api-design.md
    version: 1
  - path: docs/designs/api/US-010-openapi.yaml
    version: 1
  - path: docs/designs/database/US-010-db-design.md
    version: 1
  - path: docs/tests/US-010-test-strategy.md
    version: 1
supersedes: null
---

# US-010 Acceptance Criteria → Test Matrix

Owned by `test-writer`. `dotnet-implementor` and `security-reviewer` read this
table; they do not rebuild it.

In this workflow variant there is no separate verification stage: **these tests
are the verification**. A green suite must be sufficient evidence that every
Acceptance Criterion holds.

Test classes, all under `tests/ClassroomAgent.Tests/`:

| Short name | Class |
|---|---|
| Page | `Web.Pages.ConnectionInstructionPageTests` |
| Scopes | `Web.Pages.ConnectionInstructionScopeTests` |
| Account | `Web.Pages.ConnectionInstructionTechnicalAccountTests` |
| Handover | `Web.Pages.ConnectionInstructionHandoverTests` |
| Auth | `Web.Security.ConnectionInstructionAuthorizationTests` |
| i18n | `Web.Localization.ConnectionInstructionTranslationTests` |

Status vocabulary: **RED** — fails now, for missing production behaviour;
**DEFERRED** — must be added by IMPLEMENTATION, with the reason given.

## AC-001 Only an Admin sees the instruction

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| The policy admits an Admin | security | Auth | `ThePolicy_AdmitsAnAdmin` | succeeds | RED |
| The policy refuses a Dean | security | Auth | `ThePolicy_RefusesADean` | refused | RED |
| The policy refuses an anonymous principal | security | Auth | `ThePolicy_RefusesAnAnonymousPrincipal` | refused | RED |
| The policy is separate from US-009's | security | Auth | `ThePolicy_IsSeparateFromTheConnectionSettingsPolicy` | both exist, different names | RED |
| The endpoint declares a policy, allows no anonymous access | security | Auth | `TheEndpoint_DeclaresAPolicyAndAllowsNoAnonymousAccess` | no `IAllowAnonymous` | RED |
| The path accepts only `GET` | security | Auth | `TheEndpoint_AcceptsOnlyGet` | no unsafe method | RED |
| The SC-4 anonymous closed list gains nothing | security | Auth | `TheAnonymousList_GainsNothing` | endpoint exists and is not anonymous | RED |
| An anonymous visitor is sent to sign in | security | Auth | `AnAnonymousVisitor_IsSentToSignIn` | `302` → `/sign-in` | RED |
| An anonymous visitor sees no part of the instruction | security | Auth | `AnAnonymousVisitor_SeesNoPartOfTheInstruction` | `302`, and no client ID or scope in the body | RED |
| A signed-in Admin reaches it | integration | Auth | `ASignedInAdmin_ReachesTheInstruction` | `200` | RED |
| The settings section carries the entry | integration | Page | `TheLandingPage_LinksToTheInstruction` | entry and path present | RED |
| The section carries both entries | integration | Page | `TheSettingsSection_CarriesBothEntries` | US-009's path and this one | RED |

## AC-002 The instruction carries this school's own client ID

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| The client ID from `LegitimacyState` is shown, with its label | integration | Page | `ThePage_ShowsThisSchoolsServiceAccountClientId` | `200`, value and label present | RED |
| The page says the value is this school's alone | integration | Page | `ThePage_SaysTheClientIdBelongsToThisSchoolAlone` | statement present | RED |
| The school's domain is shown, with its label | integration | Page | `ThePage_ShowsTheSchoolsDomain` | value and label present | RED |
| The OAuth web client id is **not** shown | security | Page | `ThePage_DoesNotShowTheOAuthWebClientId` | `200` and absent | RED |
| The `Installation` UUID is **not** shown | security | Page | `ThePage_DoesNotShowTheInstallationIdentifier` | `200` and absent | RED |

## AC-003 The scope list is exactly the one the requirements fix

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| Each of the six scopes is rendered (6 cases) | integration | Scopes | `EveryRequiredScope_IsRendered` | present as a full URI | RED |
| The rendered list is exactly the six, in order | integration | Scopes | `TheRenderedList_IsExactlyTheSixScopesInOrder` | sequence equal | RED |
| Every rendered scope is one of the six | integration | Scopes | `EveryRenderedScope_IsOneOfTheRequirementsReadOnlyScopes` | count 6, all members | RED |
| A forbidden scope appears nowhere (5 cases: `drive.file`, `classroom.profile.photos`, `openid`, `userinfo.email`, `userinfo.profile`) | security | Scopes | `AForbiddenScope_AppearsNowhere` | `200`, list non-empty, fragment absent | RED |
| The list is the same with no successful check | integration | Scopes | `TheScopeList_IsTheSameWithoutASuccessfulCheck` | same six | RED |
| The scopes are not translated | i18n | Scopes | `TheScopesAreNotTranslated` | same six in English | RED |

## AC-004 The instruction states what the technical account must be

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| Each of the five FR-005 statements is present (5 cases) | integration | Account | `EveryTechnicalAccountStatement_IsOnThePage` | statement present | RED |
| No Workspace admin-role name in either translation file (9 cases) | i18n | Account | `NoWorkspaceAdminRoleName_AppearsInEitherLanguage` | absent from all 18 keys, both languages | RED |
| No Workspace admin-role name reaches the page (9 cases) | security | Account | `NoWorkspaceAdminRoleName_ReachesThePage` | `200`, roles statement present, role name absent | RED |
| The two super-admin actions are stated | integration | Account | `ThePage_StatesWhatTheSuperAdminDoes` | both present | RED |
| A super-admin or write-access account is refused in words | integration | Account | `ThePage_SaysASuperAdminAccountIsNotAcceptable` | both statements present | RED |
| The page invents no technical-account address | integration | Account | `ThePage_DoesNotInventATechnicalAccountAddress` | saved address absent, "entered in settings" present | RED |
| No personal data on the page | security | Account | `ThePage_ShowsNoPersonalData` | `200`, Admin's own address absent | RED |

## AC-005 The instruction is readable when nothing else works

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| Each read-only cause serves the page with its reason (3 cases) | integration | Page | `InReadOnlyMode_TheInstructionIsServedWithItsReason` | `200` + reason | RED |
| The instruction stays complete in read-only mode (3 cases) | integration | Page | `InReadOnlyMode_TheInstructionIsStillComplete` | six scopes + label | RED |
| A suspended school still sees its client ID and domain | integration | Page | `InReadOnlyMode_TheClientIdIsStillShown` | both present | RED |
| Read-only mode is never a `409` here (3 cases) | security | Auth | `InReadOnlyMode_TheAnswerIsNeverAConflict` | `200`, not `409` | RED |
| An `upgrade_required` answer is not a confirmed state | integration | Page | `ADomainRecordedWithoutASuccessfulCheck_IsNotTreatedAsKnown` | not-confirmed statement, no client ID | RED |

An unreachable Control Plane needs no test of its own: the page performs no
Control Plane call at all, which the whole class proves by never scripting one —
the channel answers only the Admin login check.

## AC-006 A rotated client ID appears without anyone visiting the school

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| The stored client ID changes between two renders | integration | Page | `WhenTheClientIdIsRotated_TheNextRenderShowsTheNewOne` | second render shows the new value, not the old | RED |

## AC-007 Reading the instruction changes nothing

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| Three renders write nothing to any table, audit included | persistence | Page | `OpeningThePage_WritesNothingAtAll` | all row counts and audit rows unchanged | RED |
| Rendering with no `legitimacy_state` row writes nothing | persistence | Page | `OpeningThePageWithNoLegitimacyState_WritesNothing` | unchanged, still no row | RED |
| Rendering in each read-only cause writes nothing (3 cases) | persistence | Page | `OpeningThePageInReadOnlyMode_WritesNothing` | unchanged | RED |
| A query string is ignored and writes nothing | validation | Auth | `AQueryStringIsIgnored` | same handed-over text, nothing written | RED |

The migration and table lists are **not** re-asserted here: `AppUserMigrationTests`
already pins them at three migrations and five tables (db-design §6), and US-010
must leave that test untouched and passing.

## AC-008 Before the first successful check the instruction says what is missing

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| No successful check: the page says so and invents nothing | integration | Page | `WithNoSuccessfulLegitimacyCheck_ThePageSaysWhatIsMissing` | `200`, statement present, no client ID, no domain | RED |
| The school-independent parts are still complete | integration | Page | `WithNoSuccessfulLegitimacyCheck_TheSchoolIndependentPartsAreComplete` | six scopes + five statements | RED |
| A stored row whose client ID is empty (`InstallationNotConfirmed` with `readOnly = false`) | unit, ports substituted | — | — | state is `InstallationNotConfirmed`; the page does not fall over | **DEFERRED** |

**The deferred row is the one finding DB_DESIGN §2.1 forced.** That combination
cannot be produced in PostgreSQL — `ck_legitimacy_state_domain_length` and
`ck_legitimacy_state_client_id_format` forbid an empty value and neither column is
nullable — so it needs a unit test with `ILegitimacyStateRepository` substituted.
Such a test must name the query type spec FR-002 describes, which does not exist
yet, so writing it now would not compile. **IMPLEMENTATION must add it**, in the
shape of `SaveWorkspaceConnectionBranchTests` (the same limitation US-009 recorded
and satisfied as its finding D-2).

## AC-009 The instruction is ready to hand over

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| The handed-over text carries the client ID and the domain | integration | Handover | `TheHandedOverText_CarriesTheClientIdAndTheDomain` | both present | RED |
| …and every scope | integration | Handover | `TheHandedOverText_CarriesEveryScope` | six present | RED |
| …and the technical-account requirements | integration | Handover | `TheHandedOverText_CarriesTheTechnicalAccountRequirements` | five present | RED |
| It carries no secret, no session cookie name, no token | security | Handover | `TheHandedOverText_CarriesNoSecret` | all absent | RED |
| Nor does the page as a whole | security | Handover | `ThePage_CarriesNoSecretAnywhere` | `200`, secret and reference absent | RED |
| The copy affordance is present with its label | integration | Handover | `ThePage_CarriesTheCopyAffordance` | control + label | RED |
| The affordance is a static file, not inline script | security | Handover | `TheCopyAffordance_IsAStaticScriptFileAndNotInlineScript` | no inline script, a source present | RED |
| The referenced script is really served from `/js/` | integration | Handover | `TheReferencedScript_IsServedAsAStaticFile` | `200` | RED |
| The page is complete without scripting | integration | Handover | `WithoutScripting_TheInstructionIsStillComplete` | full text in markup, no inline script | RED |
| No download route exists (4 cases: `.txt`, `.pdf`, `/download`, `/export`) | security | Handover | `NoDownloadRouteExists` | instruction `200`, suffix `404` | RED |
| The path accepts no `POST` | security | Handover | `TheInstructionPath_AcceptsNoPost` | instruction `200`, `POST` neither `200` nor a redirect | RED |

## AC-010 Every string is translated

| Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|
| Every key exists in both languages (18 cases) | i18n | i18n | `EveryKey_ExistsInBothLanguages` | present and non-empty | RED |
| Every key is really translated (18 cases) | i18n | i18n | `EveryKey_IsReallyTranslated` | the two values differ, neither echoes the key | RED |
| No single entry carries the whole instruction | i18n | i18n | `NoSingleEntry_CarriesTheWholeInstruction` | each entry ≤ 600 characters | RED |
| The instruction renders in English for an English account | i18n | i18n | `TheInstruction_RendersInEnglishForAnEnglishAccount` | English title and statement | RED |
| The data is identical in both languages | i18n | i18n | `TheDataIsTheSameInBothLanguages` | client ID, domain, scopes present | RED |
| The client ID is rendered exactly as stored | validation | i18n | `TheClientId_IsRenderedExactlyAsStored` | verbatim; no grouped variant in any separator | RED |

## Coverage summary

| Acceptance Criterion | Scenarios | Fully covered by this stage |
|---|---|---|
| AC-001 | 12 | yes (forbidden role via a synthetic Dean — US-009 F-1 carried forward) |
| AC-002 | 5 | yes |
| AC-003 | 6 methods, 12 cases | yes |
| AC-004 | 7 methods, 25 cases | yes |
| AC-005 | 5 methods, 11 cases | yes |
| AC-006 | 1 | yes |
| AC-007 | 4 methods, 6 cases | yes |
| AC-008 | 2 of 3 | **no** — one scenario DEFERRED to IMPLEMENTATION (compile constraint, above) |
| AC-009 | 11 methods, 16 cases | yes, except that no clipboard call is executed (strategy §8.4) |
| AC-010 | 6 methods, 39 cases | yes |

Every Acceptance Criterion has at least one mapped scenario. The single DEFERRED
row is an addition IMPLEMENTATION owes, not a gap in coverage of an
externally observable behaviour: what a user can reach through the database is
covered by `WithNoSuccessfulLegitimacyCheck_*`.
