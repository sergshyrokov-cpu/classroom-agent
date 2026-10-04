---
artifact_type: ac_test_matrix
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T07:00:00Z
updated_at: 2026-10-04T07:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-019-trigger-sync-from-ui.md
    version: null
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/database/US-019-db-design.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
supersedes: null
---

# US-019 Acceptance Criteria → Test Matrix

Namespaces: `ClassroomAgent.Tests.Web.<Folder>`. Shared fixtures:
`SynchronizationRequestHostExtensions`, `FakeSynchronizationRequests`,
`SynchronizationRequestTestData` (TestInfrastructure). "Port calls" = calls
recorded by the fake synchronization-request port. Status is the expected red-phase
state; the test-generation report records the actual one.

| AC | Scenario | Level | Test class | Test method | Expected | Status |
|---|---|---|---|---|---|---|
| AC-001 | Admin press, idle → redirect to connection page, one port call | Integration | `SynchronizationRequestTests` | `AdminPress_RedirectsToTheConnectionPage_AndRequestsOnce` | `302` `Location` = `/settings/workspace-connection`; port calls = 1 | RED |
| AC-001 | Following the redirect shows "requested" once | Integration | `SynchronizationRequestTests` | `AdminPress_TheConnectionPageShowsRequested_Once` | message text (uk) present on first GET, absent on second | RED |
| AC-002 | Dean press → redirect to `/`, one port call | Integration | `SynchronizationRequestTests` | `DeanPress_RedirectsToTheHomePage_AndRequestsOnce` | `302` `Location` = `/`; port calls = 1 | RED |
| AC-002 | Dean home after press: message, no run status/time/diagnosis | Integration | `SynchronizationRequestTests` | `DeanPress_TheHomePageShowsRequested_AndNothingAboutAnyRun` | message present; no `LastSync.*` text | RED |
| AC-003 | Port says other work in progress → "after the current synchronization" | Integration | `SynchronizationRequestTests` | `PressDuringOtherWork_ShowsRequestedAfterCurrentWork` (Theory: Admin, Dean) | `302`; message `RequestedAfterCurrentWork` | RED |
| AC-003 | Adapter, idle → `StartsNow`, request startable | Unit | `CoordinatorSynchronizationRequestsTests` | `WhenIdle_AnswersStartsNow_AndTheRequestIsStartable` | `StartsNow`; `IsRequested`; `TryStartRequestedRun` true | RED |
| AC-003 | Adapter, run in progress → `AfterCurrentWork`; starts after completion | Unit | `CoordinatorSynchronizationRequestsTests` | `DuringARun_AnswersAfterCurrentWork_AndStartsWhenTheRunEnds` | `AfterCurrentWork`; `TryStartRequestedRun` false until `RunCompleted`, then true | RED |
| AC-003 | Adapter, purge in progress → `AfterCurrentWork`; remembered | Unit | `CoordinatorSynchronizationRequestsTests` | `DuringAPurge_AnswersAfterCurrentWork_AndStartsWhenThePurgeEnds` | as above with `PurgeCompleted` | RED |
| AC-004 | Adapter, three requests during a run → exactly one run after | Unit | `CoordinatorSynchronizationRequestsTests` | `SeveralRequestsDuringARun_StartExactlyOneRunAfterIt` | after `RunCompleted`: one `TryStartRequestedRun` true, next false | RED |
| AC-004 | Two presses → each answered and audited | Integration | `SynchronizationRequestAuditTests` | `TwoPresses_AreEachAnsweredAndAudited` | two `302`; port calls = 2; two `succeeded` rows | RED |
| AC-001 | Accepted Admin press audited | Integration | `SynchronizationRequestAuditTests` | `AnAcceptedAdminPress_IsAuditedWithTheAdminAsActor` | one row: `app_user`, admin id, `admin`, `succeeded`, no target, no category, request id set | RED |
| AC-002 | Accepted Dean press audited | Integration | `SynchronizationRequestAuditTests` | `AnAcceptedDeanPress_IsAuditedWithTheDeanAsActor` | one row: `app_user`, dean id, `dean`, `succeeded`, no target | RED |
| AC-005 | Read-only (3 causes): `409`, nothing enqueued | Integration | `SynchronizationRequestRefusalTests` | `InReadOnlyMode_ThePressIsRefusedWith409_AndNothingIsEnqueued` (Theory: causes × Admin/Dean) | `409`; port calls = 0 | RED |
| AC-005 | Read-only refusal names the reason | Integration | `SynchronizationRequestRefusalTests` | `InReadOnlyMode_TheRefusalNamesTheReason` (Theory: causes) | `409` body contains reason text (uk) | RED |
| AC-005 | Read-only refusal audited | Integration | `SynchronizationRequestRefusalTests` | `InReadOnlyMode_TheRefusalIsAudited` (Theory: causes) | one row `refused` / `read_only_mode`, no target | RED |
| AC-005 | Read-only and unconfigured → read-only reason first | Integration | `SynchronizationRequestRefusalTests` | `ReadOnlyAndUnconfigured_AnswersWithTheReadOnlyReason` | `409`; category `read_only_mode`; port calls = 0; no `ConnectionNotUsableAdmin` text | RED |
| AC-009 | No connection, Admin → `409` connection page with Admin message | Integration | `SynchronizationRequestRefusalTests` | `WithoutAConnection_AnAdminPressIsRefused_WithTheAdminMessage` | `409`; `ConnectionNotUsableAdmin` text; port calls = 0 | RED |
| AC-009 | No connection, Dean → `409` home page with Dean message | Integration | `SynchronizationRequestRefusalTests` | `WithoutAConnection_ADeanPressIsRefused_WithTheDeanMessage` | `409`; `ConnectionNotUsableDean` text; port calls = 0 | RED |
| AC-009 | Connection for another domain → refused | Integration | `SynchronizationRequestRefusalTests` | `WithAConnectionForAnotherDomain_ThePressIsRefused` | `409`; port calls = 0 | RED |
| AC-009 | Unusable-connection refusal audited | Integration | `SynchronizationRequestRefusalTests` | `WithoutAConnection_TheRefusalIsAudited` | one row `refused` / `connection_not_usable`, no target | RED |
| AC-006 | Anonymous → sign-in, nothing enqueued or audited | Integration / Security | `SynchronizationRequestAuthorizationTests` | `Anonymous_IsSentToSignIn_AndNothingHappens` | `302` `/sign-in`; port calls = 0; no row | RED (404 today) |
| AC-006 | Restricted Dean session → forced change | Integration / Security | `SynchronizationRequestAuthorizationTests` | `ADeanWithATemporaryPassword_IsSentToTheForcedChange_AndNothingHappens` | `302` `/sign-in/change-password`; port calls = 0; no row | see report |
| AC-006 | No antiforgery token → `400` | Integration / Security | `SynchronizationRequestAuthorizationTests` | `WithoutAnAntiforgeryToken_TheRequestIs400_AndNothingHappens` (Theory: Admin, Dean) | `400`; port calls = 0; no row | RED |
| AC-006 | A `GET` requests nothing | Integration / Security | `SynchronizationRequestAuthorizationTests` | `AGet_RequestsNothing` | not `302` to a role page; port calls = 0; no row | see report |
| AC-006 | A submitted `returnUrl` is ignored | Integration / Security | `SynchronizationRequestAuthorizationTests` | `ASubmittedReturnUrl_IsIgnored` (Theory: Admin, Dean) | `Location` = the role page, never the submitted URL | RED |
| AC-006 | Both roles allowed | Integration | `SynchronizationRequestTests` | AC-001/AC-002 rows above | `302` to role page | RED |
| FR-004 | Admin connection page carries the form | Integration | `SynchronizeButtonTests` | `TheConnectionPage_CarriesTheButton_ForAnAdmin` | form id present, posts to path, carries token | RED |
| FR-004 | Dean home page carries the form | Integration | `SynchronizeButtonTests` | `TheHomePage_CarriesTheButton_ForADean` | form present | RED |
| FR-004 | Admin home page has no form (I-6) | Integration | `SynchronizeButtonTests` | `TheHomePage_HasNoButton_ForAnAdmin` | form absent | PASS (vacuous today; see report) |
| FR-004 | Button visible in read-only mode (OD-006) | Integration | `SynchronizeButtonTests` | `InReadOnlyMode_TheButtonIsStillShown` (Theory: Admin page, Dean page) | form present | RED |
| AC-007 | Keys exist in uk and en and differ | Unit | `SynchronizationRequestTranslationTests` | `EveryKey_ExistsInBothLanguages` | each of `TextKeys.All` non-empty in both, uk ≠ en | RED |
| AC-007 | English user sees English message | Integration | `SynchronizationRequestTranslationTests` | `AnEnglishUser_SeesTheEnglishMessage` | en text present after press | RED |
| FR-010 | Accepted press logs `Information` | Integration | `SynchronizationRequestLoggingTests` | `AnAcceptedPress_LogsOneInformationLine` | one `SynchronizationRequested` event, level Information, no email | RED |
| FR-010 | Refused press logs `Warning` | Integration | `SynchronizationRequestLoggingTests` | `ARefusedPress_LogsOneWarningLine` | one `SynchronizationRequestRefused` event, level Warning | RED |
| db §2.1–2.3 | Factory rows round-trip | Integration (DB) | `SynchronizationRequestAuditSchemaTests` | `TheTwoFactoryRows_AreStored` | insert via factories + save succeeds | RED |
| db §2.3 | Shape constraint rejects a target / system actor / other category | Integration (DB) | `SynchronizationRequestAuditSchemaTests` | `TheShapeConstraint_RejectsAMalformedRow` (Theory) | raw INSERT fails with `ck_audit_event_sync_request_shape` | RED |
| entity §2.2 | Refused factory accepts only two categories | Unit | `SynchronizationRequestAuditSchemaTests` | `TheRefusedFactory_RejectsOtherCategories` | `ArgumentOutOfRangeException` | RED |
| entity §2.2 | Factories take the role | Unit | `SynchronizationRequestAuditSchemaTests` | `TheFactories_RecordTheGivenRole` (Theory: Admin, Dean) | `ActorRole` = given | RED |
| AC-008 | Tests never reach Google | — | every class above | port substitution (`FakeSynchronizationRequests`; host Google ports already substituted, TC-4) | — | n/a |

Every Acceptance Criterion AC-001 … AC-009 maps to at least one test.
