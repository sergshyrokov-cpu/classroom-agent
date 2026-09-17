---
artifact_type: implementation_report
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T10:25:00Z
updated_at: 2026-09-17T10:25:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
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
  - path: docs/tests/US-003-test-strategy.md
    version: 1
  - path: docs/tests/US-003-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 1
---

# US-003 Implementation Report — Manage AllowedAdmin entries

## 1. Summary

The Control Plane now keeps each installation's `AllowedAdmin` entries: an Admins
section on the installation detail page (list, fewer-than-two warning, add and
revoke links), an add form with format validation, domain match and uniqueness
under concurrency, a revoke confirmation page and a revocation that deletes the
entry, two audit events, the `allowed_admin` table with its migration and
triggers, and Ukrainian/English translations.

Build: 0 warnings, 0 errors. Tests: 499/499 pass, 0 skipped. `dotnet format
--verify-no-changes`: clean. One story-level test was corrected (§7).

Security-sensitive: yes — it stores school employees' emails, adds
Owner-only state-changing endpoints and audit events.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-003-manage-allowed-admins.md` | — |
| Open Decisions | `docs/decisions/US-003-open-decisions.md` | 1 (none) |
| Specification | `docs/specifications/US-003-spec.md` | 1 (APPROVED) |
| API design | `docs/designs/api/US-003-api-design.md`, `US-003-openapi.yaml` | 1 |
| DB design | `docs/designs/database/US-003-db-design.md`, `US-003-entity-model.md` | 1 |
| Tests | `docs/tests/US-003-test-strategy.md`, `US-003-ac-test-matrix.md` | 1 |
| Requirements | `trebovaniya.md` | 70 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `InstallationRegistry.GetAsync` (entries of the installation, ordered by email ordinal); `Views/Installations/Detail.cshtml` Admins section | `AllowedAdminListTests` | PASS |
| AC-002 | `AllowedAdminsController.New/Add`; `AllowedAdminRegistry.AddAsync`; `AllowedAdmin.Add` | `AllowedAdminAddTests` | PASS |
| AC-003 | `AllowedAdminEmailAttribute` + `AddAllowedAdminRequest` (binding rules, first failing key); domain match in `AddAsync` → `WrongDomain`; `Views/AllowedAdmins/New.cshtml` | `AllowedAdminValidationTests`, `AllowedAdminSchemaTests` | PASS |
| AC-004 | pre-check in `AddAsync`; `uq_allowed_admin_installation_email` mapped to `Taken` on a lost race | `AllowedAdminUniquenessTests` | PASS |
| AC-005 | `AllowedAdminsController.Revocation/Revoke`; `AllowedAdminRegistry.GetRevokeConfirmationAsync/RevokeAsync`; `Views/AllowedAdmins/Revocation.cshtml` | `AllowedAdminRevocationTests` | PASS |
| AC-006 | no status check anywhere in `AllowedAdminRegistry`; installation row never touched | `AllowedAdminAddTests`, `AllowedAdminRevocationTests` | PASS |
| AC-007 | `Detail.cshtml` warning when `Admins.Count < 2`; confirmation note when count ≤ 2 | `AllowedAdminListTests`, `AllowedAdminRevocationTests` | PASS |
| AC-008 | `guid` route constraints; registry joins the entry to its installation; `ExecuteDeleteAsync` row count 0 → `NotFound` | `AllowedAdminRevocationTests` | PASS |
| AC-009 | `AuditEvent.AllowedAdminAdded/AllowedAdminRevoked`, codes in `AuditEventConfiguration`, written in the change's transaction | `AllowedAdminAuditTests` | PASS |
| AC-010 | `[Authorize(Policy = OwnerSession.OwnerPolicy)]` on `AllowedAdminsController`; US-001 setup gate and fallback unchanged | `AllowedAdminAuthorizationTests`, `AnonymousEndpointTests` | PASS |
| AC-011 | POST actions under the global antiforgery filter; GET actions read only | `AllowedAdminAntiforgeryTests`, `AntiforgeryTests` | PASS |
| AC-012 | 22 keys added to `SharedResource.uk.resx` and `.en.resx`; values rendered with Razor encoding | `AllowedAdminTranslationTests`, `TranslationCompletenessTests` | PASS |

## 4. Change Set

### Production — created

| File | Trace |
|---|---|
| `Persistence/AllowedAdmin.cs` | entity model §2.1; FR-011 |
| `Persistence/Configurations/AllowedAdminConfiguration.cs` | db-design §3, §3.1, §3.4; entity model §3 |
| `Persistence/Migrations/20260917101243_AddAllowedAdmin.cs` (+ `.Designer.cs`) | db-design §7 (table, constraints, indexes, `trg_allowed_admin_domain_match`, `trg_allowed_admin_no_update`); PC-2 |
| `Services/AllowedAdminRegistry.cs` | entity model §4; FR-003, FR-004, FR-005, FR-007, FR-008; db-design §5 |
| `Services/AllowedAdminItemDto.cs`, `AddAllowedAdminFormDto.cs`, `RevokeAllowedAdminConfirmationDto.cs` | api-design §6; AD-8 |
| `Services/AddAllowedAdminOutcome.cs`, `AddAllowedAdminResult.cs`, `RevokeAllowedAdminResult.cs` | entity model §4 result cases; AD-9 |
| `Controllers/AllowedAdminsController.cs` | api-design §2, §4; FR-002 … FR-005, FR-008 … FR-010 |
| `Controllers/AddAllowedAdminRequest.cs` | api-design §5 (only `email` bound) |
| `Controllers/AllowedAdminEmailAttribute.cs` | VR-001; api-design §5 key order |
| `Controllers/AddAllowedAdminPageModel.cs` | view model of the add form (api-design §4, WrongDomain argument) |
| `Views/AllowedAdmins/New.cshtml` | FR-002, VR-003 |
| `Views/AllowedAdmins/Revocation.cshtml` | FR-004, I-2, I-10 |

(All paths under `src/ClassroomAgent.ControlPlane/`.)

### Production — modified

| File | Trace |
|---|---|
| `Persistence/AuditAction.cs`, `AuditTargetType.cs`, `AuditEvent.cs`, `Configurations/AuditEventConfiguration.cs` | entity model §2.2; FR-007 |
| `Persistence/ControlPlaneDbContext.cs` | entity model §2.4 |
| `Persistence/TimestampInterceptor.cs` | entity model §2.3 (stamp `AllowedAdmin`) |
| `Persistence/Migrations/ControlPlaneDbContextModelSnapshot.cs` | generated with the migration |
| `Services/InstallationDetailDto.cs`, `Services/InstallationRegistry.cs` | api-design §6 (`Admins`); entity model §4 `GetAsync` changed |
| `Views/Installations/Detail.cshtml` | FR-001, FR-006; api-design §4 GET detail |
| `Program.cs` | DI registration of `AllowedAdminRegistry` (supporting) |
| `Localization/SharedResource.uk.resx`, `SharedResource.en.resx` | FR-012; openapi `ValidationMessageKeys`, `PageTextKeys`; supporting labels `AllowedAdmin.Add.Title`, `AllowedAdmin.Email.Label`, `AllowedAdmin.AddedAt.Label`, `AllowedAdmin.Revoke.Title` |

### Tests

Created and modified by TEST_WRITING (listed in the test-generation report). Modified here:

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminRevocationTests.cs` | `Revoke_ThenAddSameEmail_CreatesNewEntryWithNewIdentifierAndTime` — clock advance 3 h → 10 min (§7) |

No secret, generated database file or IDE-local config is in the change set.

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Baseline (red) | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us003-red.trx` | 2 | 499 cases, 363 pass, 136 fail — as the test-generation report records |
| Persistence step | `dotnet test --project tests/ClassroomAgent.Tests --no-build --filter-class "*AllowedAdminSchemaTests" --filter-class "*MigrationTests"` | 0 | 27/27 pass |
| First full run | `dotnet test --solution ClassroomAgent.sln --no-build … us003-green.trx` | 2 | 498 pass, 1 fail (`Revoke_ThenAddSameEmail…` → `/sign-in`, see §7) |
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us003-green.trx` | 0 | 499 total, 499 pass, 0 fail, 0 skipped (58.3 s) |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |

## 6. Configuration Changes

None. No `appsettings` change; the only startup change is the DI registration in
`Program.cs`. No package added.

## 7. Deviations and Discovered Problems

- **Test corrected.** `AllowedAdminRevocationTests.Revoke_ThenAddSameEmail_CreatesNewEntryWithNewIdentifierAndTime`
  advanced the fake clock by 3 hours between revoking and re-adding. The Owner
  session ends after 30 minutes of inactivity (`trebovaniya.md` §8, v68; SC-2),
  so the re-add was redirected to `/sign-in` — a test defect, not a production
  one. The advance is now 10 minutes, with a comment; every assertion (new id, new
  identifier, new time different from the first) is unchanged. Reviewer: please
  confirm this correction.
- **`AllowedAdminRegistry` has no list method.** The entity model placed the
  detail-page entries in `InstallationRegistry.GetAsync`; they are loaded there
  and nowhere else.
- **Lost-race add** is caught and mapped to `409`; EF Core logs the failed save
  without parameter values (sensitive data logging is off), as in US-002. As in
  US-002 (MIN-1), no test reads the log after a race — the log test covers the
  non-racing paths.
- **Revoke POST without an Owner id claim** returns `Forbid()` like the US-002
  actions; unreachable for a principal that passed the `Owner` policy.

## 8. Open Decisions

None touched; none required.
