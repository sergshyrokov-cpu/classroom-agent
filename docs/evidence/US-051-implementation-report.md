---
artifact_type: implementation_report
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T21:30:00Z
updated_at: 2026-10-16T00:10:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-051-google-sign-in-admin-accounts-only.md
    version: null
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
  - path: docs/designs/api/US-051-api-design.md
    version: 1
  - path: docs/designs/database/US-051-db-design.md
    version: 1
  - path: docs/tests/US-051-test-strategy.md
    version: 1
  - path: docs/tests/US-051-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-051 Implementation Report

## 1. Summary

The Google sign-in now admits only an Admin account. `CompleteGoogleSignInUseCase`
checks, after the Control Plane's `Allowed`, first the role and then the disabled
state through one helper, `RefusalOf`. It applies the same helper on the
concurrent-first-sign-in path after the unique-index conflict (FR-005). A refused
account is never modified. The new category `not_an_admin_account` is mapped, it is
allowed by the CHECK constraint (migration `AddNotAnAdminAccountRefusal`), and it is
logged as event 2205 at Warning. The person sees the translated text
`SignIn.Refused.NotAdminAccount`. Build, the full suite (5962/5962) and the format
check pass. Security-sensitive: SC-2, SC-3 and SC-11.

## 2. Source Artifacts

Story `docs/stories/US-051-google-sign-in-admin-accounts-only.md`; spec
`docs/specifications/US-051-spec.md` v1 (APPROVED); open decisions
`docs/decisions/US-051-open-decisions.md` v1 (none); API design v1 (NOT_APPLICABLE);
DB design v1; test strategy v1; AC matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `CompleteGoogleSignInUseCase.ExecuteAsync` + `RefusalOf`; `GoogleSignInEvents.TextKey`; resx `SignIn.Refused.NotAdminAccount` uk/en | R `AnApprovedDeanAccount_…`, `TheRefusal_LeavesTheDeanAccountExactlyAsItWas`; T ×4 | PASS |
| AC-002 | `RefuseWithAsync` → `AuditEvent.AdminSignInRefused(id, role, NotAnAdminAccount, …)`; `AuditEventConfiguration` code mapping | R `TheRefusal_IsAuditedAgainstTheDeanAccount_WithTheNewCategory` | PASS |
| AC-003 | order in `ExecuteAsync`: Control Plane reply → `RefusalOf` (role, then disabled) | R 4 order tests | PASS |
| AC-004 | `RefusalOf` returns null for an enabled Admin; disabled Admin → `AccountDisabled` | R 3 regression tests | PASS |
| AC-005 | refusal written through the existing `PermittedServiceWrite.AuditEvent` | R `InReadOnlyMode_…` ×3 | PASS |
| AC-006 | Dean account untouched; password path unchanged | R `AfterTheRefusal_TheDeanStillSignsIn…` | PASS |
| AC-007 | `AdmitAsync`, `UniqueEmailViolationException` branch: re-read → `RefusalOf` → refusal row committed in the same transaction, the account not stamped | G ×3 | PASS |
| AC-008 | `ck_audit_event_refusal_category_value` + migration `20261015203416_AddNotAnAdminAccountRefusal` | P ×4; `AppUserMigrationTests`; `TheModel_HasNoPendingChangesAgainstTheMigrations` | PASS |
| AC-009 | `SignInLog`: `EventId(2205, "AdminSignInNotAnAdminAccount")`, Warning, `{RequestId}` only (review M-4) | R `TheRefusal_IsLoggedAsAWarningWithItsOwnEvent_AndWithoutTheEmail` | PASS |

R/G/P/T: see the class key in `docs/tests/US-051-ac-test-matrix.md`.

## 4. Change Set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Enums/AuditRefusalCategory.cs` | skeleton (TEST_WRITING): `NotAnAdminAccount` — db-design §2 |
| `src/ClassroomAgent.Application/Models/SignInRefusal.cs` | skeleton (TEST_WRITING): `NotAnAdminAccount` — FR-001, FR-006 |
| `src/ClassroomAgent.Application/UseCases/CompleteGoogleSignInUseCase.cs` | FR-001…FR-005; AC-001…AC-007 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | FR-008, db-design §2–§3 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261015203416_AddNotAnAdminAccountRefusal.cs` (+ `.Designer.cs`) | FR-008, db-design §4 (supporting: migration) |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | db-design §4 (generated) |
| `src/ClassroomAgent.Web/Security/GoogleSignInEvents.cs` | FR-006 |
| `src/ClassroomAgent.Web/Security/SignInLog.cs` | FR-007, AC-009 |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-006, NFR-073 (supporting: translation entries) |
| `tests/…/Web/UseCases/DeanGoogleSignInRefusalTests.cs` | ac_test_matrix (R); see §7 D-1 |
| `tests/…/Application/UseCases/GoogleFirstSignInRaceTests.cs` | ac_test_matrix (G) |
| `tests/…/Infrastructure/Persistence/NotAnAdminAccountRefusalTests.cs` | ac_test_matrix (P); see §7 D-1 |
| `tests/…/Web/Localization/NotAdminAccountTranslationTests.cs` | ac_test_matrix (T) |
| `tests/…/TestInfrastructure/SignInTestData.cs` | test constants (TEST_WRITING) |
| `tests/…/Infrastructure/Persistence/AppUserMigrationTests.cs` | migration registry follows db-design §4: count 19 → 20, last name asserted (§7 D-2) |

`.claude/settings.local.json` is modified in the working tree but is not part of this
Story and must stay out of the commit.

## 5. Validation Evidence

All runs were delegated to `cheap-worker` subagents. Production edits and test fixes
were made inline, through a scratchpad script.

| Run | Command | Result |
|---|---|---|
| Migration | `dotnet ef migrations add AddNotAnAdminAccountRefusal --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web` | exit 0; Up/Down only drop and re-add `ck_audit_event_refusal_category_value` |
| Build #1 | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings |
| Story tests #1 | filtered 4 classes | 24/26: 2 failures from the purge row (§7 D-1) |
| Full suite #1 | `dotnet test ClassroomAgent.sln` | 5959/5962: the 2 above + `AppUserMigrationTests` count (§7 D-2) |
| Build #2 | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Story tests #2 | filtered 4 classes + `AppUserMigrationTests` | 32/32 passed |
| Full suite #2 | `dotnet test ClassroomAgent.sln` | exit 0: **5962 / 5962 passed, 0 failed, 0 skipped** |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <changed files>` | exit 0, no diagnostics (two runs, covering every changed .cs file) |

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1 (test fixture defect, fixed).** Two TEST_WRITING reads did not exclude the row
  that the background retention purge writes at host start (`retention_purge_run`).
  `InstallationTestHost.AuditRowsAsync` excludes that row, but `AuditRowsAsJsonAsync`
  and a bare `DbSet.SingleAsync` do not. The result depended on timing. Fixes:
  - `DeanGoogleSignInRefusalTests` now reads the JSON of `admin_sign_in` rows only;
  - `NotAnAdminAccountRefusalTests` reads back the row with its own request id.

  The assertions are not weakened. AC-002 is still "exactly one Admin sign-in row,
  with no email", and the round trip still proves the member read back.
- **D-2 (registry test).** `AppUserMigrationTests` fixes the migration count (19). It
  now expects 20 and asserts the new migration's name, with the comment extended per
  the established pattern.
- Review M-3 (race-path audit row committed): implemented as a commit inside the
  existing transaction, before the transaction commits. Proved by
  `ADeanCreatedInTheMeantime_…`, which reads on a separate connection after the use
  case returns.
- Review M-4: the event id is **2205**, name `AdminSignInNotAnAdminAccount`.

## 8. Open Decisions

None touched or required.
