---
artifact_type: implementation_report
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T09:49:10Z
updated_at: 2026-10-29T09:59:55Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-067-admin-changes-dean-login.md
    version: null
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
  - path: docs/designs/api/US-067-api-design.md
    version: 1
  - path: docs/designs/api/US-067-openapi.yaml
    version: 1
  - path: docs/designs/database/US-067-db-design.md
    version: 1
  - path: docs/designs/database/US-067-entity-model.md
    version: 1
  - path: docs/tests/US-067-test-strategy.md
    version: 1
  - path: docs/tests/US-067-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-067 Implementation Report — An Admin changes a Dean's login

## 1. Summary

An Admin can now change a Dean account's login from `/settings/deans`, using a per-row form that posts to
`POST /settings/deans/{deanId}/login` with the field `newEmail`.

- **Evaluation order.** The use case `ChangeDeanLoginUseCase` follows spec FR-002 in order: read-only mode → the
  account → format → school domain → same address → uniqueness. The format and domain rules are now one shared helper
  for creation and the change (`DeanLoginAddress`), not a copy.
- **Success.** `AppUser.ChangeLogin` sets the lower-cased login and rotates the security stamp. One audit row
  `dean_login_changed` is committed in the same transaction.
- **Unique-index race.** A violation is answered "already in use" (FR-004).
- **Read-only mode.** The change is refused in Application, with a refused audit row carrying the new action.
- **Same address.** The page is re-rendered with `409` "login unchanged" and nothing is written.
- **Migration.** `AddDeanLoginChangedAuditAction` extends `ck_audit_event_action`. Its Down is refused while rows with
  the code exist.

Validation:

- build 0/0;
- the 4 Story classes are green (§5);
- the full suite at 09:3x was 6880/6882, and its 2 failures were registries that needed the new endpoint and
  migration (D-3); both were fixed and their classes are green;
- `dotnet format --verify-no-changes` is clean.

The final full rerun is green: 6882/6882 (§5.4).

## 2. Source artifacts

These are the `inputs` in the front matter: Story, spec v1, OD v1 (OD-001 a, OD-002 a), API design v1 + OpenAPI v1,
db-design v1 + entity model v1, test strategy v1, AC matrix v1, and the test generation report.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (green) |
|---|---|---|
| AC-001 | `ChangeDeanLoginUseCase.ExecuteAsync`, `AppUser.ChangeLogin`, `DeanAccountsController.ChangeLogin` (PRG, `LoginChangedConfirmation`) | U `AValidChange_GivesTheSameAccountTheNewLogin_LowerCased`, `ADisabledAccount_CanHaveItsLoginChanged`; P `AValidChange_RedirectsBack_…`, `TheDeansTemplates_ShowTheNewAddressAsAuthor`, `EarlierAuditRows_StillReferToTheSameAccount` |
| AC-002 | follows from the changed `normalized_email` (FR-005); no sign-in change | P `TheNewAddressSignsInWithTheOldPassword_AndTheOldAddressIsRefused` |
| AC-003 | `RotateSecurityStamp` inside `ChangeLogin` | U `AValidChange_RotatesTheSecurityStamp`; P `AnOpenSessionOfTheDean_EndsOnItsNextRequest` |
| AC-004 | `DeanLoginAddress.IsWellFormed` / `IsInDomain` / `SchoolDomainOf` (shared with `CreateDeanAccountUseCase`); `400` with creation keys | U `AMalformedAddress_…`, `AnAddressOutsideTheSchoolDomain_…`, `WithNoSchoolDomainKnown_…`, `EveryRefusedAddress_IsRefusedExactlyAsCreationRefusesIt`; P `ARefusedAddress_…` (6), `TheRejectedValue_IsNotWrittenToTheLog` |
| AC-005 | step 6 `FindByNormalizedEmailAsync`; `catch (UniqueEmailViolationException)` → `EmailAlreadyUsed` | U `AnotherDeansAddress_…`, `AnAdminsAddress_…`, `AnAddressTakenBetweenTheCheckAndTheSave_…`; P `AnAddressInUse_…` (3) |
| AC-006 | `ChangeLogin` writes only the login and the stamps | U `AValidChange_LeavesEverythingElseOfTheAccountUntouched`; P `AllElseOfTheAccountStays_WhenTheLoginChanges` |
| AC-007 | `AuditEvent.DeanAccountManaged` accepts `DeanLoginChanged`; code mapping + CHECK + migration | U `AValidChange_WritesOneAuditRow_…`, `TheAuditRow_HoldsNeitherAddress`, `TheManagementAuditFactory_AcceptsTheLoginChange`; P `TheChange_IsAuditedOnce_WithoutEitherAddress`; S all 6 |
| AC-008 | guard first, `AuditRefusalAsync` with `DeanLoginChanged` under `PermittedServiceWrite.AuditEvent` | U `InReadOnlyMode_…` (2); P `InReadOnlyMode_TheChangeIsRefused_…` (3 causes); S `TheReadOnlyRefusalRow_…` |
| AC-009 | the controller's `ManageDeanAccounts` policy; route `SignInRoutes.DeanLogin` | P `ADean_IsForbidden_…`, `AnAnonymousRequest_IsSentToSignIn` (302 → `/sign-in`, as test report §9 expected); `PermissionMatrixTests` |
| AC-010 | the old normalized email is no longer on any row | P `TheOldAddress_IsFreeForANewAccount` |
| FR-006 | step 5 → `DeanAccountRefusal.LoginUnchanged` → `409` re-render | U `TheAccountsOwnAddress_…` (3); P `TheAccountsOwnAddress_…` |

U = `ChangeDeanLoginUseCaseTests`, P = `ChangeDeanLoginPageTests`, S = `DeanLoginChangedAuditSchemaTests`.

## 4. Change set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Entities/AppUser.cs` | entity model §2 `ChangeLogin` (skeleton completed) |
| `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` | db-design §3: guard widened to five actions |
| `src/ClassroomAgent.Domain/Enums/AuditAction.cs` | entity model §3 `DeanLoginChanged` (skeleton) |
| `src/ClassroomAgent.Application/Models/DeanAccountRefusal.cs` | entity model §5 `LoginUnchanged` (skeleton) |
| `src/ClassroomAgent.Application/UseCases/ChangeDeanLoginUseCase.cs` (new) | spec FR-001 … FR-008 |
| `src/ClassroomAgent.Application/UseCases/DeanLoginAddress.cs` (new) | spec FR-002 "the same code as creation" — extracted from creation |
| `src/ClassroomAgent.Application/UseCases/CreateDeanAccountUseCase.cs` | FR-002: uses the shared helper; behaviour unchanged (all `CreateDeanAccountUseCaseTests` green in the full run) |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | api-design D-5, NFR-073: 4 keys |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs` | db-design §3: CHECK list + code mapping both ways |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261029092536_AddDeanLoginChangedAuditAction.cs` + `.Designer.cs` (new), `ClassroomAgentDbContextModelSnapshot.cs` | db-design §4, PC-2 (generated by `dotnet ef`; Down guard added by hand) |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | api-design D-1 `DeanLogin` |
| `src/ClassroomAgent.Web/Security/DeanAccountTextKeys.cs` | api-design D-5; `Of(LoginUnchanged)` |
| `src/ClassroomAgent.Web/Controllers/DeanAccountsController.cs` | api-design D-1 … D-4: action `ChangeLogin`, `LoginUnchanged` → `409` |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI registration (supporting) |
| `src/ClassroomAgent.Web/Views/DeanAccounts/Index.cshtml` | spec FR-010, D-2, D-6: per-row form |
| `tests/…/Web/Pages/ChangeDeanLoginPageTests.cs` | test fix D-1 (§7) |
| `tests/…/Infrastructure/Persistence/DeanLoginChangedAuditSchemaTests.cs` | test fix D-2 (§7) |
| `tests/…/Web/Security/PermissionMatrixTests.cs` | D-3: matrix row for the new endpoint (§2 matrix v104) |
| `tests/…/Infrastructure/Persistence/AppUserMigrationTests.cs` | D-3: migration count 26 + name |
| TEST_WRITING files (tests, fixtures, test docs) | unchanged here except as listed; traced in the test generation report |

There are no secrets, generated database files or IDE-local files in the change set.

## 5. Validation evidence

1. `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors. Rerun after every fix, last at 09:49Z.
2. Story classes, run one filter term per run:
   - `ChangeDeanLoginUseCaseTests`: 41/41.
   - `DeanAccountTranslationTests`: 45/45.
   - `ChangeDeanLoginPageTests`: 28/29 on the first run; D-1 fixed; then 29/29.
   - `DeanLoginChangedAuditSchemaTests`: 4/6 on the first run; D-2 fixed; then 6/6.
   - After the line-ending fix, `FullyQualifiedName~ChangeDeanLogin` gave 70/70.
3. Full suite `dotnet test ClassroomAgent.sln`: 6882 total, 6880 passed, 2 failed (D-3), 0 skipped, 10 min 23 s. There
   were no environment failures. After D-3, `PermissionMatrixTests` gave 3/3 and `AppUserMigrationTests` 6/6.
4. Final full rerun after all fixes (D-1 … D-4): **6882 total, 6882 passed, 0 failed, 0 skipped, 10 min 14 s** (§5.4).
5. `dotnet format ClassroomAgent.sln --verify-no-changes`: exit 0. It was exit 2 once, after D-3, because of mixed
   line endings (D-4).

Delegated to `cheap-worker`:

- the first validation run (build, the 4 classes, format);
- the second run (build, the 2 fixed classes, format, full suite).

Kept inline:

- all production C#: use case, domain, controller, view, resx, migration Down guard;
- the test fixes;
- the 2 registry fixes and the single-class reruns.

### 5.4 Final full rerun

`dotnet test ClassroomAgent.sln` finished 2026-10-29T09:59:55Z: 6882/6882 passed, 0 skipped, exit code 0, no environment failures.

## 6. Configuration changes

None.

## 7. Deviations and discovered problems

- **D-1 (test defect, fixed).** `ADean_IsForbidden_ForAnotherAccountAndForTheirOwn` took the Dean's own row before
  the Dean signed in. The sign-in records `last_successful_sign_in_at` itself, so the comparison failed with no
  production fault. The snapshot is now taken after the sign-in, and the assertions are unchanged.
- **D-2 (test defect, fixed).** The two Down tests of `DeanLoginChangedAuditSchemaTests` migrated down through the
  snake_case context a database that the host had migrated without it. The history column names differ
  (`migration_id` vs `MigrationId`). They now create the database with `migrate: false` and migrate it through the one
  context, as `DataHealthPersistenceTests` does. The assertions are unchanged.
- **D-3 (registries, updated).** `PermissionMatrixTests` gained the row `POST settings/deans/{deanid}/login →
  ManageDeanAccounts`, following the §2 matrix v104. `AppUserMigrationTests` now expects 26 migrations, the last one
  `_AddDeanLoginChangedAuditAction`. Both follow from the approved designs.
- **D-4 (tooling).** The scripted edits wrote LF lines into CRLF files. All edited files were normalized back to CRLF.
  For `docs/workflow/history.jsonl` only the line endings changed: `git diff` shows the one line TEST_WRITING appended
  and nothing else.
- **INFO-1.** `CreateDeanAccountUseCase` now calls the shared `DeanLoginAddress` helper instead of its own two private
  methods. This is the behaviour-preserving extraction that FR-002 requires ("same code, not a copy"). The creation
  tests stay green.

## 8. Open Decisions

None touched or newly required.
