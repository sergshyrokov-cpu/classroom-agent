---
artifact_type: implementation_report
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T08:38:57Z
updated_at: 2026-10-04T08:38:57Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-019-trigger-sync-from-ui.md
    version: null
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-api-design.md
    version: 1
  - path: docs/designs/database/US-019-db-design.md
    version: 1
  - path: docs/designs/database/US-019-entity-model.md
    version: 1
  - path: docs/tests/US-019-test-strategy.md
    version: 1
  - path: docs/tests/US-019-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-019 Implementation Report — Trigger a synchronization from the UI

## 1. Summary

The "Synchronize" button is implemented for Admin (connection page) and Dean
(home page): one form `POST /synchronization/requests` under its own policy
`StartSynchronization` (Admin + Dean). The use case runs the read-only guard
first, refuses an unusable connection (OD-009 a), enqueues through the new
`ISynchronizationRequests` port over `SyncRunCoordinator`, and writes one audit
row per press, accepted or refused. Success is Post-Redirect-Get to the role's
page with a one-time message; refusals answer `409`. All 50 red tests are green;
full suite 2835/2835, build 0/0, format clean.

Security-sensitive: a new state-changing endpoint, a new policy, a new audited
action.

## 2. Source Artifacts

Front matter `inputs` lists every path and version. Open Decisions OD-001 …
OD-010, all resolved.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `SynchronizationRequestController.Submit`; `RequestSynchronizationUseCase.ExecuteAsync`; `Views/WorkspaceConnection/Index.cshtml` form | `SynchronizationRequestTests.AdminPress_*`, `SynchronizationRequestAuditTests.AnAcceptedAdminPress_*` | PASS |
| AC-002 | same; `InstallationPages.LandingAsync` (`CanRequestSynchronization` for Dean); `Views/Home/Index.cshtml` | `SynchronizationRequestTests.DeanPress_*`, `SynchronizationRequestAuditTests.AnAcceptedDeanPress_*` | PASS |
| AC-003 | `CoordinatorSynchronizationRequests.RequestAsync` (busy read + `Request`); outcome `RequestedAfterCurrentWork` | `CoordinatorSynchronizationRequestsTests.*`, `SynchronizationRequestTests.PressDuringOtherWork_*` | PASS |
| AC-004 | coordinator remembers one request (US-013); each press audited | `CoordinatorSynchronizationRequestsTests.SeveralRequestsDuringARun_*`, `SynchronizationRequestAuditTests.TwoPresses_*` | PASS |
| AC-005 | use case step 1: guard, refusal row under `PermittedServiceWrite.AuditEvent`, rethrow → host `409` | `SynchronizationRequestRefusalTests.InReadOnlyMode_*`, `ReadOnlyAndUnconfigured_*` | PASS |
| AC-006 | `[Authorize(Policy = StartSynchronization)]`; global antiforgery; restricted session; role-derived redirect | `SynchronizationRequestAuthorizationTests.*` | PASS |
| AC-007 | five `Synchronization.Request.*` keys in uk and en | `SynchronizationRequestTranslationTests.*` | PASS |
| AC-008 | port substitution; the use case touches no Google port | all US-019 tests | PASS |
| AC-009 | use case step 2: `GetWorkspaceConnectionQuery.IsUsable`; `409` re-render with role message | `SynchronizationRequestRefusalTests.WithoutAConnection_*`, `WithAConnectionForAnotherDomain_*` | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Domain/Enums/AuditAction.cs` | `SynchronizationRequested` | spec FR-005; entity model §2.1 |
| `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` | two factories + shared row builder (role guard, category guard) | entity model §2.2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | code mapping; `ck_audit_event_action` 11 values; `ck_audit_event_sync_request_shape` | db-design §2.1, §2.3 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261004065854_AddSynchronizationRequestAudit.cs` (+ `.Designer.cs`, snapshot) | migration | db-design §3, PC-2 |
| `src/ClassroomAgent.Application/Models/SynchronizationRequestTiming.cs` | port answer | spec FR-002, FR-003 |
| `src/ClassroomAgent.Application/Models/RequestSynchronizationOutcome.cs` | use case outcome | spec FR-001 step 5 |
| `src/ClassroomAgent.Application/Ports/ISynchronizationRequests.cs` | port | spec FR-002 |
| `src/ClassroomAgent.Application/UseCases/RequestSynchronizationUseCase.cs` | use case | spec FR-001, FR-005, FR-006 |
| `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` | `StartSynchronization` | spec FR-007 |
| `src/ClassroomAgent.Application/Localization/SharedResource.{uk,en}.resx` | 5 keys each | spec FR-008 |
| `src/ClassroomAgent.Web/BackgroundServices/CoordinatorSynchronizationRequests.cs` | adapter | api-design §2.7 |
| `src/ClassroomAgent.Web/Controllers/SynchronizationRequestController.cs` | endpoint | openapi `requestSynchronization` |
| `src/ClassroomAgent.Web/Controllers/InstallationPages.cs` | builds both page models | api-design §2.4 (the `409` re-render equals the `GET`) |
| `src/ClassroomAgent.Web/Controllers/WorkspaceConnectionController.cs` | uses `InstallationPages`; reads the one-time message | openapi `getWorkspaceConnectionSettings` (additive) |
| `src/ClassroomAgent.Web/Controllers/HomeController.cs` | uses `InstallationPages`; reads the one-time message | openapi `getLanding` (additive) |
| `src/ClassroomAgent.Web/Controllers/WorkspaceConnectionPageModel.cs` | `SynchronizationMessageKey`, `CanRequestSynchronization` | openapi `WorkspaceConnectionPageModelAddition` |
| `src/ClassroomAgent.Web/Controllers/LandingPageModel.cs` | same | openapi `LandingPageModelAddition` |
| `src/ClassroomAgent.Web/Views/WorkspaceConnection/Index.cshtml` | message + form | spec FR-004 |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | Dean message + form | spec FR-004, I-6 |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | `SynchronizationRequests` path | openapi path |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | policy registration | spec FR-007 |
| `src/ClassroomAgent.Web/Security/SynchronizationRequestLog.cs` | events 5140 / 5141 | spec FR-010 |
| `src/ClassroomAgent.Web/Security/SynchronizationRequestTextKeys.cs` | keys + outcome → key | spec FR-008, OD-009 a |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI: port (singleton), use case, page builder | supporting (DI) |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | migration count 9 → 10, new name | supporting (migration), §7 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | `ck_audit_event_sync_request_shape` in the pinned list | supporting (migration), §7 |
| tests and fixtures of TEST_WRITING | unchanged here except formatting of `SynchronizationRequestHostExtensions.cs` (`dotnet format`, whitespace only) | ac_test_matrix |

No secret, generated database file or IDE-local config is in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0; 0 warnings, 0 errors |
| US-019 tests | `dotnet test ClassroomAgent.sln --no-build --filter "…Synchroniz…"` | 160/160 passed (filter also matches existing sync classes) |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | exit 0; **2835 total, 2835 passed, 0 failed, 0 skipped**, 4 m 13 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no diagnostics |

One intermediate full run (not counted) failed with Npgsql timeouts in two
unrelated classes after 43 minutes of wall-clock — the machine had slept during
the run; it was stopped and rerun clean (above).

## 6. Configuration Changes

None. No setting, no `appsettings` change.

## 7. Deviations and Discovered Problems

- **Two pinned schema tests updated** (`AppUserMigrationTests`,
  `InstallationAuditEventSchemaTests`): they enumerate every migration and every
  `audit_event` check by name, so the migration db-design §3 requires changes
  their expected lists. Each expectation was extended by exactly the new
  element; nothing was removed or loosened — the same update US-037 made.
- **`InstallationPages`** is a small page builder used by three controllers. It
  replaces the private `PageAsync` of `WorkspaceConnectionController` and the
  inline model of `HomeController` (behaviour unchanged, their tests green), so a
  refused press re-renders exactly the page its `GET` renders (api-design §2.4).
- **TempData carries the translation key** of the message (as US-009 and US-012
  do), not the bare `SynchronizationMessageKey` code; the key is
  `Synchronization.Request.<code>`, so the closed list of the contract is kept.
- Log event ids 5140 / 5141 were chosen next to the synchronization events
  (51xx); no artifact fixed them.

## 8. Open Decisions

None touched or newly required. OD-001 … OD-010 resolved.
