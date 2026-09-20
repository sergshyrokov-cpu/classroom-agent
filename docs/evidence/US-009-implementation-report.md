---
artifact_type: implementation_report
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T15:10:00Z
updated_at: 2026-09-20T15:10:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-009-configure-workspace-connection.md
    version: null
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
  - path: docs/designs/api/US-009-api-design.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/database/US-009-db-design.md
    version: 1
  - path: docs/designs/database/US-009-entity-model.md
    version: 1
  - path: docs/tests/US-009-test-strategy.md
    version: 1
  - path: docs/tests/US-009-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-009 Implementation Report — Configure WorkspaceConnection

## 1. Summary

The Admin can record which Google Workspace domain the installation serves and
which technical account it will read the school's data on behalf of, and the
program refuses any domain the Owner did not approve (BR-020) — the second of
the Owner's three points of control now fires.

Implemented: the `WorkspaceConnection` entity and its singleton table, the
Admin-only settings page and save, the BR-020 invariant in `Application`, the
four-state connection view including the OD-002 mismatch, validation, the audit
rows for a save, a change and every refusal, the read-only refusal, nineteen
translation entries in both languages, and one EF Core migration.

Validation: build clean (0 errors, 0 warnings), **1651 tests pass, 0 fail, 0
skipped**, `dotnet format --verify-no-changes` clean.

Two things worth a reviewer's attention, both detailed in §7: a **defect in the
US-008 baseline** this Story exposed and fixed (the translated error page came
out in the server's culture, not the user's), and the reason the
`DomainNotConfirmed` branch now has its own unit test.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-009-configure-workspace-connection.md` | — |
| Specification | `docs/specifications/US-009-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-009-open-decisions.md` | 1 (both RESOLVED) |
| API design | `docs/designs/api/US-009-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-009-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-009-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-009-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-009-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-009-ac-test-matrix.md` | 1 |
| Requirements | `trebovaniya.md` | 79 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 Admin only | `InstallationPolicies.ConfigureWorkspaceConnection`, `InstallationSecurityServices`, `[Authorize]` on `WorkspaceConnectionController` | `WorkspaceConnectionAuthorizationTests` (10) | PASS |
| AC-002 Not configured says so | `GetWorkspaceConnectionQuery`, `Views/WorkspaceConnection/Index.cshtml` | `WorkspaceConnectionPageTests` (11) | PASS |
| AC-003 A valid save stores both values | `SaveWorkspaceConnectionUseCase.StoreAsync`, `WorkspaceConnection.Create` | `SaveWorkspaceConnectionTests` (12) | PASS |
| AC-004 A foreign domain is refused | `SaveWorkspaceConnectionUseCase` steps 2–4, `DomainComparison` | `WorkspaceConnectionInvariantTests` | PASS |
| AC-005 A foreign technical account is refused | `DomainComparison.BelongsTo` | `WorkspaceConnectionInvariantTests` (10) | PASS |
| AC-006 Malformed input is rejected first | `SaveWorkspaceConnectionRequest`, `ServiceAccountEmailAttribute`, `WorkspaceDomainAttribute` | `WorkspaceConnectionValidationTests` (9) | PASS |
| AC-007 One record, updated | `WorkspaceConnection.ChangeTo`, the singleton index | `SaveWorkspaceConnectionTests`, `WorkspaceConnectionSchemaTests` | PASS |
| AC-008 Audited without personal data | `AuditEvent.WorkspaceConnectionSaved` / `…SaveRefused` | `WorkspaceConnectionAuditTests` (10) | PASS |
| AC-009 Read-only blocks the save, keeps the view | the guard call in `SaveWorkspaceConnectionUseCase`, the US-008 `409` mapping | `WorkspaceConnectionReadOnlyTests` (8) | PASS |
| AC-010 Never legitimated cannot be configured | `GetWorkspaceConnectionQuery.KnownDomainAsync` | `WorkspaceConnectionInvariantTests`, `SaveWorkspaceConnectionBranchTests` | PASS |
| AC-011 Everything translated | 19 keys in both `SharedResource` files, `WorkspaceConnectionTextKeys` | `WorkspaceConnectionTranslationTests` (5) | PASS |
| AC-012 A migration ships the schema | `20260920181834_AddWorkspaceConnection` | `WorkspaceConnectionSchemaTests` (10) | PASS |

## 4. Change Set

### Created — production

| File | Trace |
|---|---|
| `Domain/Entities/WorkspaceConnection.cs` | entity model §2.1; FR-001 |
| `Application/Models/WorkspaceConnectionState.cs` | FR-002 |
| `Application/Models/Dtos/WorkspaceConnectionView.cs` | FR-002; openapi `WorkspaceConnectionPageModel` |
| `Application/Models/SaveWorkspaceConnectionOutcome.cs` | FR-006 (a refusal is data, AD-9) |
| `Application/Models/Requests/SaveWorkspaceConnectionRequest.cs` | FR-005, VR-003 |
| `Application/Validation/ServiceAccountEmailAttribute.cs` | VR-001 |
| `Application/Validation/WorkspaceDomainAttribute.cs` | VR-002 |
| `Application/Ports/IWorkspaceConnectionRepository.cs` | entity model §3.4; AD-4 |
| `Application/UseCases/DomainComparison.cs` | FR-007 |
| `Application/UseCases/GetWorkspaceConnectionQuery.cs` | FR-002, FR-003 |
| `Application/UseCases/SaveWorkspaceConnectionUseCase.cs` | FR-006, FR-008, FR-009 |
| `Infrastructure/Persistence/Configurations/WorkspaceConnectionConfiguration.cs` | db-design §3.1, §3.2 |
| `Infrastructure/Persistence/Repositories/WorkspaceConnectionRepository.cs` | entity model §3.4 |
| `Infrastructure/Persistence/Migrations/20260920181834_AddWorkspaceConnection*.cs` | db-design §7.1; AC-012 |
| `Web/Controllers/WorkspaceConnectionController.cs` | FR-004, FR-005; openapi |
| `Web/Controllers/WorkspaceConnectionPageModel.cs` | openapi `WorkspaceConnectionPageModel`; AD-8 |
| `Web/Views/WorkspaceConnection/Index.cshtml` | FR-004 |
| `Web/Security/WorkspaceConnectionTextKeys.cs` | FR-011 |
| `Web/Security/WorkspaceConnectionLog.cs` | FR-014 |
| `Web/Security/RequestCultureScope.cs` | supporting change for AC-009 + NFR-073 — see §7 D-1 |

### Modified — production

| File | Trace |
|---|---|
| `Domain/Enums/AuditAction.cs`, `AuditTargetType.cs`, `AuditRefusalCategory.cs` | FR-009; db-design §4 |
| `Domain/Entities/AuditEvent.cs` | FR-009 (two factories) |
| `Application/Authorization/InstallationPolicies.cs` | FR-010 |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-011 (19 keys each) |
| `Infrastructure/Persistence/ClassroomAgentDbContext.cs` | entity model §3.3 |
| `Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | db-design §4, §4.1 |
| `Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | generated with the migration |
| `Web/Configuration/InstallationServices.cs` | FR-015 (DI) |
| `Web/Security/InstallationSecurityServices.cs` | FR-010 (the policy) |
| `Web/Security/SignInRoutes.cs` | openapi path |
| `Web/Views/Home/Index.cshtml` | FR-013 (the navigation entry) |
| `Web/Exceptions/InstallationExceptionHandler.cs` | AC-009 + NFR-073 — see §7 D-1 |

### Created / modified — tests

Created by TEST_WRITING (unchanged here except where §7 says otherwise): the nine
US-009 test classes and two test-infrastructure files.

Added by this stage:

| File | Trace |
|---|---|
| `tests/…/Application/UseCases/SaveWorkspaceConnectionBranchTests.cs` | AC-010; the limitation TEST_WRITING recorded (§7 D-2) |

Corrected by this stage — three prior-Story tests that enumerate a closed list
US-009 legitimately grows, and one US-009 test that over-specified PostgreSQL:

| File | Why |
|---|---|
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | the installation now has four tables and three migrations |
| `Web/UseCases/AdminLoginCheckEveryTimeTests.cs` | the same table list |
| `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | `audit_event` gains `ck_audit_event_target_type_value` |
| `Web/Persistence/WorkspaceConnectionSchemaTests.cs` | see §7 D-3 |

No assertion was weakened and no test was disabled or deleted.

### Workflow artifacts

`docs/workflow/*` and `docs/stories/US-009-*.md` (the Owner's resolutions) were
written by the orchestrator and the human, not by this stage.

## 5. Validation Evidence

```
dotnet build ClassroomAgent.sln
  Предупреждений: 0   Ошибок: 0

dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~WorkspaceConnection"
  итог: 141   сбой: 0   успешно: 141   пропущено: 0

dotnet test ClassroomAgent.sln
  Пройден!  итог: 1651  сбой: 0  успешно: 1651  пропущено: 0

dotnet format --verify-no-changes
  exit 0
```

Progress across the stage: 131 red → 23 → 4 → 0, then 3 prior-Story failures
from the grown table and constraint lists → 0.

Integration tests ran against PostgreSQL in Testcontainers (TC-2). No test calls
Google — this Story adds no Google port at all (TC-4).

## 6. Configuration Changes

None. US-009 adds no setting: the domain comes from `LegitimacyState` and the
technical account from the Admin. No NuGet package was added; `Application` still
references none and `Domain` keeps zero package references (AD-3).

## 7. Deviations and Discovered Problems

### D-1 (security-relevant, for SECURITY_REVIEW): the error page was rendered in the server's culture

The first **real** read-only refusal over HTTP in this codebase exposed a defect
in the US-008 baseline: `InstallationExceptionHandler` runs in the outermost
middleware, and `CurrentUICulture` — set by the localization middleware deeper in
the pipeline — does not flow back out of that async scope when an exception
unwinds. The translated error page therefore came out in the **server's**
culture (`lang="ru"` on the test machine), not the user's, which NFR-073 forbids.

US-008 could not have caught it: its own tests handed the exception to the
handler directly (US-008 TEST_WRITING deviation, api-design §2.5), so the
end-to-end path was never exercised.

Fix: `RequestCultureScope` re-applies the request's culture from
`IRequestCultureFeature` — which does survive on `HttpContext` — for the duration
of the handler, and restores the previous culture on dispose. The decision of
`AccountCultureProvider` is reused, not made a second time. Proven by
`WorkspaceConnectionReadOnlyTests.TheRefusal_NamesTheReason` in all three BR-025
causes.

### D-2: the `DomainNotConfirmed` branch got the unit test TEST_WRITING could not write

Over HTTP the branch is unreachable: an installation that never confirmed its
legitimacy is always read-only and the guard runs first.
`SaveWorkspaceConnectionBranchTests` constructs the use case with the guard
substituted and the ports in memory, and proves the branch on its own — including
that an `upgrade_required` domain is not a confirmed domain, and that the same
call saves once a domain is confirmed. AC-010's "neither hides the other" is now
satisfied.

### D-3: one US-009 test over-specified PostgreSQL

`AMixedCaseRow_IsRejected` asserted a single constraint name. An upper-case
domain breaks **two** constraints of db-design §3.1 at once — the lower-case rule
and the format rule, whose character class is lower-case ASCII — and which one
PostgreSQL reports is its choice, not something an artifact fixes. The test now
accepts either of those two and still rejects an unrelated constraint. The
email row keeps its exact single name.

### D-4: the singleton column is set explicitly in the repository

EF Core sends the CLR default `false` for the shadow `singleton` property rather
than letting the column default apply, and `ck_workspace_connection_singleton`
then rejects the row. `WorkspaceConnectionRepository.Add` sets it explicitly —
the same line, with the same reason, that `LegitimacyStateRepository` has carried
since US-005 (AD-11: the existing pattern was reused, not re-invented).

### D-5: the fourth check of the save is a shape check, not a tautology

Specification FR-006 step 5 asks for the domain about to be written to be checked
against the `Installation` domain. Since OD-001 makes that value *come from*
`LegitimacyState`, comparing it with itself would be dead code. It is implemented
as spec VR-004 describes instead: the value must satisfy VR-002, so a
hand-edited database produces a refusal rather than a row no rule would accept.
The refusal category and status are the ones FR-006 fixes.

### D-6: `AuditTargetType`'s converter became reachable

US-008 left the enum empty with a converter that threw on any value. US-009 adds
its first member, so the converter is now a real mapping in both directions and
an unknown code still throws (US-008 spec I-11 anticipated exactly this).

## 8. Open Decisions

None outstanding. OD-001 and OD-002 were resolved by the Owner before
SPECIFICATION and the implementation follows both: the save request carries no
domain field, and a connection whose domain no longer matches is reported as
`DomainMismatch`, never used and never corrected automatically.

No new Open Decision was raised. D-1 is a defect fix inside an approved mapping,
not a new decision: NFR-073 already required the user's language.
