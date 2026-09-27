---
artifact_type: implementation_report
story: US-012
version: 3
status: APPROVED
created_at: 2026-09-27T06:45:10Z
updated_at: 2026-09-27T07:28:24Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/decisions/US-012-open-decisions.md
    version: 2
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: docs/designs/database/US-012-entity-model.md
    version: 1
  - path: docs/tests/US-012-test-strategy.md
    version: 1
  - path: docs/tests/US-012-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 79
  - path: docs/reviews/security/US-012-security-review.md
    version: 2
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 3
---

# US-012 Implementation Report

## 0. Version 3 — the residual timing finding (F-3)

The second review closed F-2 but found that version 2's fix of F-1 was
incomplete: `_timingEqualisationHash` was a private **instance** field while
`SignInDeanUseCase` is registered `AddScoped`, so the dummy hash was computed on
every unknown-login request. That path paid one `Hash` **plus** one `Verify`
while a real check paid one `Verify` — the same oracle with its sign flipped.

**Fix:** the field is now `private static string?`, filled at most once for the
process through `TimingEqualisationHashOf`, which is exactly how
`ControlPlane.Services.OwnerSignInService` declares its equivalent. A race
between two threads would make each hash once and one value win — bounded,
harmless, and it keeps a lock off the sign-in path.

**Verification:** `DeanSignInSequenceTests.Step1_DoesNotRehashOnEveryAttempt`
performs two unknown-login attempts and asserts the hash count does not grow
between them. The assertion is deterministic whichever test populated the static
first, unlike an absolute count would be.

Files changed in version 3: `Application/UseCases/SignInDeanUseCase.cs`,
`tests/…/DeanSignInSequenceTests.cs`.

## 0a. Version 2 — the two security-review findings

Version 1 was reviewed and returned `CHANGES_REQUIRED` with one Major finding.
Both findings are fixed here; nothing else changed.

**F-1 (Major, SC-2) — the unknown-login timing oracle.** Step 1 of
`SignInDeanUseCase` returned without calling `IPasswordHasher.Verify`, while
steps 3 to 6 called it, so the response time revealed whether a login existed —
on a page SC-2 itself describes as internet-facing with a guessable work email,
and without moving any failed-attempt counter. The path now verifies a hash of
a value nobody knows, computed once per use-case instance, and discards the
result: the same countermeasure, and the same reason, as
`ControlPlane.Services.OwnerSignInService`. **Step 2 still skips verification**,
which SC-2 v66 requires, and a new test pins that so the equalisation cannot
leak into it.

Verification: `DeanSignInSequenceTests.Step1_SpendsTheSameHashingWorkAsARealCheck`
compares the number of hasher calls on an unknown login with the number on a
known one; `Step2_StillVerifiesNoPassword` asserts the lockout path calls it
zero times. The old `Step1_VerifiesNoPassword`, which asserted the opposite of
the fix, is replaced by the first of these.

**F-2 (Minor) — the session died on a voluntary password change.** The change
rotates the security stamp (FR-019), but the session was not re-issued, so the
Post-Redirect-Get landed on a page the Dean could no longer reach and the
confirmation was never shown. `DeanPasswordController` now re-issues the session
in both password paths through one `ReIssueSessionAsync` helper.

Verification: `DeanPasswordPageTests.AfterAChange_TheSessionSurvivesAndTheConfirmationIsShown`
follows the redirect and asserts `200` with the confirmation text;
`AfterAChange_TheNewPasswordIsTheOneThatWorks` signs in with the new password in
a fresh client.

Files changed in version 2: `Application/UseCases/SignInDeanUseCase.cs`,
`Web/Controllers/DeanPasswordController.cs`,
`tests/…/DeanSignInSequenceTests.cs`, `tests/…/DeanPasswordPageTests.cs`.

## 1. Summary

The installation now has its second role. An Admin creates a Dean account on a
work email in the school's domain and afterwards disables it, re-enables it or
resets its password; a Dean signs in with that password through the six-step
sequence of SC-2, is forced to replace the temporary password an Admin typed,
and may change their own password later — including while the school is in
read-only mode.

All sixteen Acceptance Criteria are implemented. Build is clean, the whole
suite is green, the format check passes. The skeleton OD-005 left behind is
gone: no `NotImplementedException` remains in the US-012 surface.

Limitations carried forward, none blocking: the timing difference between the
three common sign-in refusals is not asserted (SC-2 accepts it), and the
restricted session is proved through the pages it may reach rather than through
the claim that carries it.

## 2. Source artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-012-manage-dean-accounts.md` | — |
| Specification | `docs/specifications/US-012-spec.md` | 1, APPROVED |
| Open Decisions | `docs/decisions/US-012-open-decisions.md` | 2 |
| API design | `docs/designs/api/US-012-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-012-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-012-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-012-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-012-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-012-ac-test-matrix.md` | 1 |

`HUMAN_SPEC_APPROVAL` is recorded (2026-09-26T17:19:45Z). No input is
`SUPERSEDED`; OD-001 … OD-005 are all resolved.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test |
|---|---|---|
| AC-001 | `ListDeanAccountsQuery`, `DeanAccountsController.Index`, policy `ManageDeanAccounts` | `ListDeanAccountsQueryTests`, `DeanAccountsAuthorizationTests` |
| AC-002 | `CreateDeanAccountUseCase`, `AppUser.CreateDean` | `CreateDeanAccountUseCaseTests`, `DeanAccountsPageTests.AValidCreation_…` |
| AC-003 | `CreateDeanAccountUseCase` steps 2, 3 and 5 | `CreateDeanAccountUseCaseTests`, `DeanAccountSchemaTests.ASecondAccount…` |
| AC-004 | `DeanPasswordPolicy` | `DeanPasswordPolicyTests` (15 methods) |
| AC-005 | `SetDeanAccountStateUseCase`, `AppUser.Disable` | `DeanAccountAdministrationTests`, `DeanAccountsPageTests.Disabling_…` |
| AC-006 | `AppUser.ReEnable` | `DeanAccountAdministrationTests.ReEnabling_…` |
| AC-007 | `ResetDeanPasswordUseCase`, `AppUser.ResetPassword` | `DeanAccountAdministrationTests.AReset_…` |
| AC-008 | no delete path anywhere — no use case, no repository member, no route | `DeanAccountsPageTests.NoDeleteMethod_IsExposed` |
| AC-009 | the guard as the first statement of the three management use cases | `…UseCaseTests.InReadOnlyMode_…`, `DeanAccountsPageTests.InReadOnlyMode_…` |
| AC-010 | `SignInDeanUseCase`, `SignInController.SignInWithPassword` | `DeanSignInSequenceTests`, `DeanSignInPageTests` |
| AC-011 | `AppUser.RecordFailedSignIn`, `IsLockedOut`, the two constants | `DeanLockoutTests` |
| AC-012 | `CompleteTemporaryPasswordChangeUseCase`, `TemporaryPasswordMiddleware`, the `PasswordIsTemporary` claim | `DeanPasswordChangeTests`, `DeanPasswordPageTests` |
| AC-013 | `ChangeOwnPasswordUseCase`, `DeanPasswordController.Own` | `DeanPasswordChangeTests`, `DeanPasswordPageTests` |
| AC-014 | the three existing settings policies, now exercised by a real Dean session | `DeanSessionBoundaryTests` (**closes the carried F-1**) |
| AC-015 | `DeanAccountTextKeys` + 38 new keys in both `.resx` files | `DeanAccountTranslationTests` |
| AC-016 | the six `AuditEvent` factories and the widened check constraints | `DeanAccountAuditSchemaTests`, the audit assertions in every use-case test |

## 4. Change set

### Domain (2 files)

| File | Trace |
|---|---|
| `Domain/Entities/AppUser.cs` | `CreateDean`, `Disable`, `ReEnable`, `ResetPassword`, `SetOwnPassword`, `RecordFailedSignIn`, `IsLockedOut`, `PasswordIsTemporary` — entity model §1.1. `RecordSuccessfulSignIn` also resets the counter and clears the lockout (SC-2 step 6) |
| `Domain/Entities/AuditEvent.cs` | the six factories of entity model §1.2 (written at TEST_WRITING as skeleton, unchanged here) |

### Application (9 files)

`DeanPasswordPolicy` (FR-005), `ListDeanAccountsQuery` (FR-011),
`CreateDeanAccountUseCase` (FR-003), `SetDeanAccountStateUseCase` (FR-007,
FR-008), `ResetDeanPasswordUseCase` (FR-009), `SignInDeanUseCase` (FR-012,
FR-013), `CompleteTemporaryPasswordChangeUseCase` (FR-006),
`ChangeOwnPasswordUseCase` (FR-014), plus the 38 translation entries in
`Localization/SharedResource.{uk,en}.resx` (FR-020).

### Infrastructure (6 files)

| File | Trace |
|---|---|
| `Security/PasswordHasherAdapter.cs` | FR-018, OD-002 — Identity's hasher behind the port |
| `Persistence/Repositories/AppUserRepository.cs` | `FindDeanByIdAsync`, `ListDeansAsync` — entity model §4 |
| `Persistence/Configurations/AppUserConfiguration.cs` | the column and `ck_app_user_password_temporary` — db-design §3 |
| `Persistence/Configurations/AuditEventConfiguration.cs` | the three widened lists and their code mappings — db-design §4 |
| `Persistence/Migrations/20260926195656_AddDeanAccounts.cs` (+ Designer, snapshot) | db-design §5, PC-2 |
| `ClassroomAgent.Infrastructure.csproj` | `Microsoft.Extensions.Identity.Core` — the package OD-002 approves |

### Web (12 files)

Controllers `DeanAccountsController` and `DeanPasswordController`; the Dean
sign-in action on the existing `SignInController`; view models
`DeanAccountsPageModel` and `ChangePasswordPageModel`; `DeanAccountTextKeys`;
the six routes on `SignInRoutes`; the `PasswordIsTemporary` claim;
`TemporaryPasswordMiddleware` and its line in `Program.cs`; the three policies
in `InstallationSecurityServices`; the DI registrations in
`InstallationServices`; views `DeanAccounts/Index`, `DeanPassword/Forced`,
`DeanPassword/Own`, the Dean form on `SignIn/Index`, and one `@using` in
`_ViewImports`.

### Tests (7 files modified — all traced)

| File | Change | Why |
|---|---|---|
| `TestInfrastructure/DeanAccountWorld.cs` | passes `SchoolDefaults` to the create use case | the use case takes the installation's default language (I-9) |
| `TestInfrastructure/DeanAccountHostExtensions.cs` | seeds a **real** hash through the host's own hasher port, and the temporary mark | the fixture's `"seeded-hash"` could never verify |
| `TestInfrastructure/HostEndpoint.cs` | a numeric sample for `{deanId:long}` | otherwise the route constraint answers 404 and the antiforgery assertion proves nothing |
| `Application/UseCases/CreateDeanAccountUseCaseTests.cs` | asserts the transaction boundary, not the commit count | see §7, deviation D-1 |
| `Application/UseCases/DeanAccountAdministrationTests.cs` | re-enabling keeps the temporary mark | see §7, deviation D-2 |
| `Web/Persistence/DeanAccountAuditSchemaTests.cs` | `audit_event` has 13 columns, not 12 | an arithmetic error of TEST_WRITING |
| `Infrastructure/Persistence/AppUserSchemaTests.cs` | one column and one constraint added to the expected lists | db-design §3 |
| `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | the "no Story performs this" examples moved on | US-012 now performs `dean_sign_in`, `wrong_password` and the `app_user` target |
| `Web/Security/InstallationCookieTests.cs` | the antiforgery enumeration signs in as a Dean for the two Dean-owned endpoints | TC-5 asks for "a user with an allowed role"; as the Admin they answered 403 before the token was checked |

No test was disabled, deleted or weakened. No file outside the Story's scope
was touched.

## 5. Validation evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| Whole suite | `dotnet test ClassroomAgent.sln` | **2167 total, 2167 passed, 0 failed, 0 skipped** (version 3; v1 was 2163/2163 and v2 2166/2166 — the four added tests verify the three security-review findings) |
| US-012 set | `dotnet test --filter "FullyQualifiedName~Dean"` | 195 total, 195 passed (v1); 198 in v2 |
| Application layer | `dotnet test --filter "…Application.UseCases.Dean…"` | 84 total, 84 passed |
| Schema and migration | `dotnet test --filter "…SchemaTests\|…MigrationTests"` | 48 total, 48 passed |
| Format | `dotnet format --verify-no-changes` | clean, no output |
| Migration | `dotnet ef migrations add AddDeanAccounts …` | created; one column, one `app_user` constraint, three `audit_event` constraints |

Docker Desktop (server 29.8.0) was running throughout, so the Testcontainers
PostgreSQL fixture started without interruption (TC-2).

## 6. Configuration changes

None. No `appsettings` key was added or changed: the Story needs no deployment
setting. The only project-file change is the `Microsoft.Extensions.Identity.Core`
package reference OD-002 approves, in `ClassroomAgent.Infrastructure` alone.

## 7. Deviations and discovered problems

**D-1 — `CreateDeanAccountUseCaseTests.TheAccountAndItsAuditRow_CommitTogether`
asserted a commit count that EF Core cannot produce.** The test required one
`SaveChanges` with both the account and its audit row staged. The audit row
names the account, so the identity has to exist first: US-008's own pattern is
one transaction with two saves (db-design §4.4). The assertion was corrected to
the transaction boundary plus the final staged state. No assertion was removed
and the requirement — account and row land together or not at all — is still
proved.

**D-2 — `DeanAccountAdministrationTests.ReEnabling_ClearsTheStateAndNothingElse`
expected the temporary mark to be false.** FR-008 says re-enabling sets no
temporary password and clears none; the seeded account's mark was true, so
"nothing else changed" means it stays true. The assertion was inverted to match
the Specification.

**D-3 — `RecordSuccessfulSignIn` had to change.** SC-2 step 6 resets the
failed-attempt counter on success, and the existing method (US-008) only
recorded the time. The reset and the lockout clearing were added there rather
than in the Dean use case, so both sign-in paths obey SC-2; for the Admin path,
whose counter is never touched, both are no-ops.

**D-4 — the password hasher had to be hardened.** A stored value that is not in
Identity's format made `VerifyHashedPassword` throw, which surfaced as `500` on
the sign-in page. A malformed hash cannot match any password, so it is now a
failed verification: the caller gets the common refusal, and the value is never
logged (SC-10, spec S-05).

**D-5 — the forced-change page answers a redirect, not `403`, to an ordinary
session.** The claim policy alone gave `403`, while api-design §2.6 requires
`302` to the landing page. The `GET` now requires only an authenticated user and
redirects on the claim; the `POST` keeps the strict claim policy.

**D-6 — four existing guard tests had to be updated** (§4). Three pinned audit
values US-012 now performs; one enumerated antiforgery with an Admin caller for
endpoints that belong to a Dean. Each kept its own assertion; only the sample
data or the caller changed.

## 8. Open Decisions

No new Open Decision. OD-001 … OD-005 were resolved by the Owner before this
stage and are implemented as resolved:

- **OD-001** — one Story: both halves are in this change set.
- **OD-002** — `Microsoft.Extensions.Identity.Core` in `Infrastructure` only,
  used for `IPasswordHasher<AppUser>` alone. No `UserManager`, no
  `SignInManager`, no `IdentityDbContext`, no Identity tables.
- **OD-003** — the list shows email, state, temporary mark and last sign-in; no
  search, no paging.
- **OD-004** — no edit-email path exists.
- **OD-005** — the skeleton is fully replaced; nothing throws
  `NotImplementedException`.
