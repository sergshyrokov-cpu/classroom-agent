---
artifact_type: ac_test_matrix
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T08:40:10Z
updated_at: 2026-09-21T08:40:10Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-011-check-access.md
    version: null
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 2
  - path: docs/designs/api/US-011-api-design.md
    version: 1
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/designs/database/US-011-db-design.md
    version: 1
  - path: docs/tests/US-011-test-strategy.md
    version: 1
supersedes: null
---

# US-011 Acceptance Criteria → Test Matrix

Owned by `test-writer`. `dotnet-implementor` and `security-reviewer` read this table; they do not rebuild it. There
is no separate verification stage: **these tests are the verification**.

| Short name | Class (under `tests/ClassroomAgent.Tests/`) |
|---|---|
| Auth | `Web.Security.AccessCheckAuthorizationTests` |
| Page | `Web.Pages.AccessCheckPageTests` |
| Run | `Web.UseCases.AccessCheckRunTests` |
| Fail | `Web.UseCases.AccessCheckFailureTests` |
| Refuse | `Web.UseCases.AccessCheckRefusalTests` |
| Audit | `Web.UseCases.AccessCheckAuditTests` |
| Self | `Web.BackgroundServices.StartupSelfCheckTests` |
| L10n | `Web.Localization.AccessCheckTranslationTests` |
| Schema | `Web.Persistence.AccessCheckAuditSchemaTests` |
| Migr | `Infrastructure.Persistence.AppUserMigrationTests` (modified) |
| Probe | `Infrastructure.Google.GoogleAccessProbeTests` |

Status: **RED** — fails for missing production behaviour (verified); **GUARD** — passes before implementation as a
regression guard, must stay green.

| AC | Scenario | Level | Class | Method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | policy admits Admin | security | Auth | ThePolicy_AdmitsAnAdmin | succeeded | RED |
| AC-001 | policy refuses Dean (synthetic principal) | security | Auth | ThePolicy_RefusesADean | refused | RED |
| AC-001 | policy refuses anonymous | security | Auth | ThePolicy_RefusesAnAnonymousPrincipal | refused | RED |
| AC-001 | own policy (I-9) | security | Auth | ThePolicy_IsSeparateFromTheOtherSettingsPolicies | exists, distinct | RED |
| AC-001 | GET and POST, neither anonymous | security | Auth | TheEndpoints_AreGetAndPost_AndNeitherIsAnonymous | both declared | RED |
| AC-001 | SC-4 list unchanged | security | Auth | TheAnonymousList_GainsNothing | path exists, not anonymous | RED |
| AC-001 | POST not antiforgery-exempt | security | Auth | ThePost_IsNotExemptFromAntiforgery | not exempt | RED |
| AC-001 | anonymous GET → sign-in | integration | Auth | AnAnonymousVisitor_OpeningThePage_IsSentToSignIn | 302 /sign-in | RED |
| AC-001 | anonymous POST → sign-in, nothing runs | integration | Auth | AnAnonymousVisitor_RunningTheCheck_IsSentToSignIn_AndNothingRuns | 302, 0 calls, 0 rows | RED |
| AC-001 | Admin reaches the page | integration | Auth | ASignedInAdmin_ReachesThePage | 200 | RED |
| AC-001 | POST without token → 400, nothing runs | security | Auth | ARunWithoutTheAntiforgeryToken_IsRefused_AndNothingRuns | 400, 0 calls, 0 rows | RED |
| AC-001 | page explains and offers the run | integration | Page | ThePage_ExplainsTheCheck_AndOffersTheRun | 200, texts, token field | RED |
| AC-001 | page shows the technical account | integration | Page | ThePage_ShowsTheTechnicalAccountItWouldCheck | label + address | RED |
| AC-001 | GET runs nothing | security | Page | OpeningThePage_RunsNothing_AndRendersNoResult | 0 calls, no result | RED |
| AC-001 | GET writes no audit row | integration | Page | OpeningThePage_WritesNoAuditRow | row count unchanged | RED |
| AC-001 | landing links to the page | integration | Page | TheLandingPage_LinksToThePage | href + nav text | RED |
| AC-002 | exactly six scopes, one per request, in order | integration | Run | TheRun_RequestsExactlyTheSixScopes_OnePerRequest_InOrder | list equal, no forbidden | RED |
| AC-002 | technical account impersonated, not the Admin | security | Run | EveryDelegation_ImpersonatesTheStoredTechnicalAccount_NotTheAdmin | 6 × account | RED |
| AC-002 | posted account/scope ignored | validation | Run | APostedAccountOrScope_IsIgnored | stored account, six scopes | RED |
| AC-002 | eight rendered steps, scopes in order | contract | Run | ThePage_RendersEightSteps_WithScopesInOrder | 8 steps, kinds, scopes | RED |
| AC-002 | each scope rendered as full URI | contract | Run | ThePage_ShowsEveryScopeAsItsFullUri (×6) | URI in page | RED |
| AC-002 | token request: one scope, sub = account, iss = key | unit (adapter) | Probe | ATokenRequest_IsForThatOneScope_ImpersonatingTheTechnicalAccount | JWT claims | RED |
| AC-002 | full sequence reaches only Google, writes nothing | security (adapter) | Probe | AFullSequence_OnlyReachesGoogle_AndWritesNothing | 8 requests, Google hosts, GET reads | RED |
| AC-003 | access in place → 200 AccessInPlace | integration | Run | WhenEverythingIsInPlace_TheRunAnswers200_WithTheVerdictAccessInPlace | 200, verdict | RED |
| AC-003 | reads after delegations, once each | integration | Run | TheRun_MakesTheTwoReadsAfterTheDelegations_EachOnce | order | RED |
| AC-003 | reads use the issued token | integration | Run | TheReads_UseTheIssuedToken | token | RED |
| AC-003 | reads labelled by translated text | contract | Run | ThePage_LabelsTheTwoReads | labels | RED |
| AC-003 | Classroom read: GET, pageSize=1, bearer | unit (adapter) | Probe | TheClassroomRead_IsOneGetOfAtMostOneCourse_WithTheToken | request shape | RED |
| AC-003 | Reports read: GET, meet, maxResults=1 | unit (adapter) | Probe | TheReportsRead_IsOneGetOfAtMostOneMeetEvent_ForAllUsers | request shape | RED |
| AC-003 | read returning data is success | unit (adapter) | Probe | AReadThatReturnsData_IsSucceeded | Succeeded | RED |
| AC-003 | token never reaches the page | security | Run | TheIssuedToken_NeverReachesThePage | absent in body, cookies | RED |
| AC-003 | no PRG; result in the answer | contract | Run | TheRun_IsNotARedirect_TheResultIsInTheAnswer | 200, no Location | RED |
| AC-003 | result not stored | persistence | Run | AfterARun_ThePageShowsNoStoredResult | no verdict on GET | RED |
| AC-004 | forgotten scope named, only its step fails | integration | Fail | AForgottenScope_IsNamed_AndOnlyItsStepFails | NotConfigured, URI, message | RED |
| AC-004 | Classroom read NotAttempted without its scope | integration | Fail | WhenTheCoursesScopeIsMissing_TheClassroomReadIsNotAttempted_ButTheReportsReadRuns | NotAttempted | RED |
| AC-004 | Reports read NotAttempted without its scope | integration | Fail | WhenTheReportsScopeIsMissing_TheReportsReadIsNotAttempted | NotAttempted | RED |
| AC-004 | run-wide cause stops after one call (×3) | integration | Fail | ARunWideCause_StopsTheRunAfterTheFirstCall | 1 call, 7 NotAttempted | RED |
| AC-004 | read causes on their read (×2) | integration | Fail | AReadFailure_IsShownOnThatRead_WithItsMessage | outcome + message | RED |
| AC-004 | GoogleUnavailable alone → Inconclusive | integration | Fail | GoogleUnavailableAlone_MakesTheVerdictInconclusive | Inconclusive | RED |
| AC-004 | configuration failure outranks unavailable | integration | Fail | AConfigurationFailure_OutranksGoogleUnavailable | NotConfigured | RED |
| AC-004 | GoogleUnavailable not retried | integration | Fail | GoogleUnavailable_IsNotRetried | one call per step | RED |
| AC-004 | permission failure not retried | security | Fail | APermissionFailure_IsNotRetried | one call | RED |
| AC-004 | Owner-side cause reveals nothing about the key | security | Fail | AnOwnerSideCause_RevealsNothingAboutTheKey | no reference, setting, private_key | RED |
| AC-004 | 30-second limit (I-2) | boundary | Fail | WhenGoogleNeverAnswers_TheRunEndsAtTheTimeLimit_AsInconclusive | Inconclusive | RED |
| AC-004 | unauthorized_client → ScopeNotAuthorized | unit (adapter) | Probe | UnauthorizedClient_IsScopeNotAuthorized | mapping | RED |
| AC-004 | unknown user → TechnicalAccountUnknown | unit (adapter) | Probe | AnUnknownImpersonatedUser_IsTechnicalAccountUnknown | mapping | RED |
| AC-004 | invalid signature → KeyRejected | unit (adapter) | Probe | AnInvalidSignature_IsKeyRejected | mapping | RED |
| AC-004 | 5xx/429 on token → GoogleUnavailable (×4) | unit (adapter) | Probe | AServerErrorOrThrottling_OnTheTokenEndpoint_IsGoogleUnavailable | mapping | RED |
| AC-004 | network failure → GoogleUnavailable | unit (adapter) | Probe | ANetworkFailure_IsGoogleUnavailable | mapping | RED |
| AC-004 | unrecognised error → GoogleUnavailable | unit (adapter) | Probe | AnUnrecognisedError_IsGoogleUnavailable | never a diagnosis | RED |
| AC-004 | no reference → KeyUnavailable, nothing sent (×3) | validation (adapter) | Probe | WithoutAReference_TheKeyIsUnavailable_AndNothingIsSent | 0 requests | RED |
| AC-004 | unresolvable reference → KeyUnavailable | validation (adapter) | Probe | AReferenceTheStoreDoesNotHold_IsKeyUnavailable_AndNothingIsSent | 0 requests | RED |
| AC-004 | not a service-account key → KeyUnavailable (×4) | validation (adapter) | Probe | ContentThatIsNotAServiceAccountKey_IsKeyUnavailable_AndNothingIsSent | 0 requests | RED |
| AC-004 | read 403 → TechnicalAccountCannotRead | unit (adapter) | Probe | APermissionDeniedRead_IsTechnicalAccountCannotRead | mapping | RED |
| AC-004 | disabled API → ApiNotEnabled (×2) | unit (adapter) | Probe | AReadAgainstADisabledApi_IsApiNotEnabled | mapping | RED |
| AC-004 | read 5xx/429 → GoogleUnavailable, not retried (×4) | unit (adapter) | Probe | AServerErrorOrThrottling_OnARead_IsGoogleUnavailable_AndNotRetried | 1 request | RED |
| AC-005 | no connection → 409, message, no call | integration | Refuse | WithoutASavedConnection_TheRunIsRefused_AndCallsNothing | 409, 0 calls | RED |
| AC-005 | other-domain connection → 409, no call | integration | Refuse | WithAConnectionForAnotherDomain_TheRunIsRefused_AndCallsNothing | 409, 0 calls | RED |
| AC-006 | read-only → 409, zero calls (×3) | security | Refuse | InReadOnlyMode_TheRunIsRefusedWith409_AndNoCallReachesGoogle | 409, 0 calls | RED |
| AC-006 | read-only refusal names the reason (×3) | contract | Refuse | InReadOnlyMode_TheRefusalNamesTheReason | reason text | RED |
| AC-006 | guard first: read-only + unconfigured | security | Refuse | ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason | read_only_mode | RED |
| AC-006 | refusal leaves the connection untouched (×3) | persistence | Refuse | ARefusal_LeavesTheConnectionUntouched | rows equal | RED |
| AC-006 | page viewable in read-only, button kept, 0 calls (×3) | security | Page | InReadOnlyMode_ThePageIsServed_WithTheButton_AndCallsNothing | 200, 0 calls | RED |
| AC-007 | succeeded row with actor, target, request id | persistence | Audit | ACarriedOutRun_WritesOneSucceededRow_NamingTheConnection | row shape | RED |
| AC-007 | run with findings still succeeded (I-4) | persistence | Audit | ARunThatFindsProblems_IsStillAuditedAsSucceeded | succeeded | RED |
| AC-007 | rows carry no personal data or findings | security | Audit | TheAuditRows_CarryNoPersonalDataAndNoFindings | absences | RED |
| AC-007 | every run its own row (OD-005) | persistence | Audit | EveryRun_IsItsOwnRow | 2 rows | RED |
| AC-007 | a run writes only its audit row (OD-003) | persistence | Audit | ARun_WritesNothingButTheAuditRow | counts | RED |
| AC-007 | read-only refusal audited with connection id (×3) | persistence | Refuse | InReadOnlyMode_TheRefusalIsAudited | refused/read_only_mode | RED |
| AC-007 | no-connection refusal audited, no id | persistence | Refuse | WithoutASavedConnection_TheRefusalIsAudited_WithoutATargetId | refused/connection_not_usable | RED |
| AC-007 | mismatch refusal audited with id | persistence | Refuse | WithAConnectionForAnotherDomain_TheRefusalIsAudited_WithTheConnectionId | id | RED |
| AC-007 | schema accepts succeeded run | persistence | Schema | TheAuditTable_AcceptsASucceededRun | 1 row | RED |
| AC-007 | schema accepts refused run (×2) | persistence | Schema | TheAuditTable_AcceptsARefusedRun | 1 row | RED |
| AC-007 | unknown action still rejected | persistence | Schema | AnUnknownAction_IsStillRejected | ck_audit_event_action | GUARD |
| AC-007 | unknown category still rejected | persistence | Schema | AnUnknownRefusalCategory_IsStillRejected | ck_audit_event_refusal_category_value | RED |
| AC-007 | no table added | persistence | Schema | NoTableIsAdded | five tables | GUARD |
| AC-007 | no audit column added | persistence | Schema | TheAuditTable_GainsNoColumn | same columns | GUARD |
| AC-007 | fourth migration AddAccessCheckAudit | persistence | Migr | TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent | 4 migrations | RED |
| AC-008 | self-check: eight steps, Information | integration | Self | WithAccessInPlace_TheSelfCheckRunsTheEightSteps_AndLogsInformation | event, 8 calls | RED |
| AC-008 | missing scope → Error with verdict | integration | Self | WithAMissingScope_TheSelfCheckLogsAnError | Error | RED |
| AC-008 | key unavailable → Error naming it | integration | Self | WithTheKeyUnavailable_TheErrorLineNamesTheOutcome | Error, 1 call | RED |
| AC-008 | read-only → skipped Warning, 0 calls (×3) | security | Self | InReadOnlyMode_TheSelfCheckIsSkipped_WithAWarning_AndCallsNothing | Warning | RED |
| AC-008 | no usable connection → skipped Warning (×2) | integration | Self | WithoutAUsableConnection_TheSelfCheckIsSkipped_WithAWarning | Warning | RED |
| AC-008 | two skip reasons distinguishable | integration | Self | TheTwoSkipReasons_AreDifferent | reasons differ | RED |
| AC-008 | failing port does not prevent start | integration | Self | AFailingPort_DoesNotPreventTheStart | 200 after failure | RED |
| AC-008 | self-check writes nothing | persistence | Self | TheSelfCheck_WritesNothingToTheDatabase | 0 audit rows | RED |
| AC-008 | log has no address, domain, token | security | Self | TheLog_CarriesNoAddressDomainOrToken | absences | RED |
| AC-009 | every test runs against the substituted port or the scripted transport | structural | all | — (the port has no live registration in any test; the adapter takes a transport) | no network | by construction |
| AC-010 | every key in both languages (×21) | integration | L10n | EveryKey_ExistsInBothLanguages | non-empty | RED |
| AC-010 | every key really translated (×21) | integration | L10n | EveryKey_IsReallyTranslated | uk ≠ en ≠ key | RED |
| AC-010 | no Workspace admin-role name (×21) | security | L10n | NoMessage_NamesAWorkspaceAdminRole | absent | RED |
| AC-010 | English account → English result, same data | integration | L10n | TheResult_RendersInEnglishForAnEnglishAccount | en text, URIs | RED |

Every Acceptance Criterion has at least one mapped test. AC-009 is a property of the whole suite: the fake port
and the scripted transport are the only Google seams any test uses, and neither has a network path.
