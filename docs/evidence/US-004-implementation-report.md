---
artifact_type: implementation_report
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T11:15:05Z
updated_at: 2026-09-17T11:15:05Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-004-suspend-resume-installation.md
    version: null
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
  - path: docs/designs/api/US-004-api-design.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
  - path: docs/designs/database/US-004-entity-model.md
    version: 1
  - path: docs/tests/US-004-test-strategy.md
    version: 1
  - path: docs/tests/US-004-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 1
---

# US-004 Implementation Report — Suspend and resume an Installation

## 1. Summary

The Owner can suspend an active installation and resume a suspended one from its
detail page, each through a confirmation page. The status is changed by one
conditional set-based update with the audit row in the same transaction; an action
that would not change the status writes nothing and redirects with a notice that is
rendered only when it matches the current status. No schema change, no migration.

Build 0 warnings; 571/571 tests pass, 0 skipped; `dotnet format --verify-no-changes`
clean. Security-sensitive: new state-changing Owner endpoints and audit events.

## 2. Source artifacts

As in `inputs` above; `trebovaniya.md` v71. HUMAN_SPEC_APPROVAL recorded
2026-09-17T10:53:50Z. No Open Decisions.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (ac_test_matrix) | Status |
|---|---|---|---|
| AC-001 | `Views/Installations/Detail.cshtml` (status action by `InstallationStatus`) | `InstallationStatusChangeTests.Detail_*` | pass |
| AC-002, AC-004 | `InstallationStatusController.Suspension` / `Resumption` → `Confirmation`; `Views/InstallationStatus/Confirmation.cshtml`; `InstallationStatusService.GetConfirmationAsync` | `…Confirmation_ShowsNameDomainExplanationFormAndCancel_ChangesNothing`, `Cancel_IsALinkToTheDetailPage_StatusUnchanged` | pass |
| AC-003, AC-005 | `InstallationStatusController.Suspend` / `Resume` → `Change`; `InstallationStatusService.ChangeStatusAsync` | `Suspend_ChangesOnlyStatus_…`, `Resume_ChangesOnlyStatus_…`, `SuspendAndResume_CanRepeat_…`, `PostedFields_AreIgnored_…`, `OtherInstallation_IsNotAffected` | pass |
| AC-006 | conditional `ExecuteUpdateAsync` (0 rows → `Unchanged`, rollback, no audit); redirect `?notice=…`; `InstallationStatusNotice.KeyFor` (single matching value only); confirmation GET redirects when status already the target | `InstallationStatusUnchangedTests` (17 cases, run 4× green) | pass |
| AC-007 | route constraint `{id:guid}`; `NotFound` result | `UnknownInstallation_AllFourOperations_…`, `NonUuidIdentifier_Returns404ErrorPage` | pass |
| AC-008 | `AuditAction.InstallationSuspended` / `InstallationResumed`, codes in `AuditEventConfiguration`, factories in `AuditEvent`; row added in the change transaction | `InstallationStatusAuditTests` | pass |
| AC-009 | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `InstallationStatusController` | `InstallationStatusAuthorizationTests`, US-001 enumeration tests | pass |
| AC-010 | global antiforgery filter (US-001); POST-only changes; nothing bound from the body | `InstallationStatusAntiforgeryTests`, US-001 `AntiforgeryTests` | pass |
| AC-011 | 12 keys added to `SharedResource.uk.resx` and `.en.resx` | `InstallationStatusTranslationTests`, `TranslationCompletenessTests` | pass |

## 4. Change set

Production (`src/ClassroomAgent.ControlPlane/`):

| File | Change | Trace |
|---|---|---|
| `Persistence/AuditAction.cs` | +2 members | db-design §4.1, entity model §2.3, AC-008 |
| `Persistence/Configurations/AuditEventConfiguration.cs` | +2 code mappings each way | entity model §2.3 |
| `Persistence/AuditEvent.cs` | +`InstallationSuspended`, `InstallationResumed` factories | FR-006, AC-008 |
| `Services/InstallationStatusTransition.cs` | new enum | entity model §4 |
| `Services/InstallationStatusChangeResult.cs` | new enum | entity model §4, AD-9 |
| `Services/InstallationStatusConfirmationDto.cs` | new DTO | api-design §7, AD-8 |
| `Services/InstallationStatusService.cs` | new use case (confirmation read, conditional status change + audit) | FR-002 … FR-006, db-design §5.1 |
| `Controllers/InstallationStatusController.cs` | new controller, 4 routes | openapi paths `/suspension`, `/resumption`, FR-007 … FR-009 |
| `Controllers/InstallationStatusConfirmationPageModel.cs` | new view model | api-design §5 |
| `Controllers/InstallationStatusNotice.cs` | notice values, redirect path, validated key | api-design §4, FR-004 |
| `Controllers/InstallationDetailPageModel.cs` | new view model (DTO + notice key) | openapi `InstallationDetailPage` |
| `Controllers/InstallationsController.cs` | `Detail` passes the page model with the validated notice | api-design §5 GET detail (changed) |
| `Views/Installations/Detail.cshtml` | model → page model; suspend/resume link; notice element | FR-001, FR-004 |
| `Views/InstallationStatus/Confirmation.cshtml` | new confirmation page | FR-002 |
| `Localization/SharedResource.uk.resx`, `.en.resx` | +12 keys each (10 contract keys + 2 page titles) | FR-011, NFR-073 |
| `Program.cs` | register `InstallationStatusService` (scoped) | supporting: DI registration |

Tests and test artifacts (from TEST_WRITING, unchanged here): listed in
`docs/evidence/US-004-test-generation-report.md`.

No secret, database file, generated export or IDE-local file in the change set. No
migration (db-design §6). No package added.

## 5. Validation evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Baseline (red) | from TEST_WRITING, `us004-red.trx` | 2 | 571: 510 pass / 61 fail, all failures in new classes |
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us004-green.trx` | 0 | 571 total, 571 passed, 0 failed, 0 skipped; 44.7 s |
| Concurrency repeat | `dotnet test --solution ClassroomAgent.sln --no-build --filter-class ClassroomAgent.Tests.ControlPlane.Controllers.InstallationStatusUnchangedTests` ×3 | 0 | 17/17 each run |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |

## 6. Configuration changes

None (DI registration only, §4).

## 7. Deviations and discovered problems

- Service method named `GetConfirmationAsync` instead of the indicative
  `GetStatusConfirmationAsync` (entity model §4 marks names indicative).
- The detail view model changed from `InstallationDetailDto` to
  `InstallationDetailPageModel` wrapping it, as the openapi `InstallationDetailPage`
  schema describes; the DTO itself is unchanged.
- `ExecuteUpdateAsync` bypasses the timestamp interceptor; `updated_at` is set in the
  same statement from the injected `TimeProvider` (db-design §5.1). Verified by the
  whole-row equality assertions with the fake clock.
- The notice is resolved in the controller (presentation) from the query string and
  the DTO status; it holds no business rule beyond display (AD-3).

## 8. Open Decisions

None touched or required.
