---
artifact_type: test_generation_report
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T19:23:33Z
updated_at: 2026-09-26T19:23:33Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: docs/designs/database/US-012-entity-model.md
    version: 1
  - path: docs/decisions/US-012-open-decisions.md
    version: 2
  - path: docs/tests/US-012-test-strategy.md
    version: 1
  - path: docs/tests/US-012-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-012 Test Generation Report

## 1. Story

US-012 — Create and manage Dean accounts (EPIC-6). Tests written against the
approved Specification, API design and database design, before any production
behaviour exists (TC-1).

## 2. Test files created

Fifteen new test classes, 130 test methods, 195 cases.

| File | Methods |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/DeanPasswordPolicyTests.cs` | 15 |
| `tests/ClassroomAgent.Tests/Application/UseCases/CreateDeanAccountUseCaseTests.cs` | 15 |
| `tests/ClassroomAgent.Tests/Application/UseCases/DeanAccountAdministrationTests.cs` | 13 |
| `tests/ClassroomAgent.Tests/Application/UseCases/DeanSignInSequenceTests.cs` | 14 |
| `tests/ClassroomAgent.Tests/Application/UseCases/DeanLockoutTests.cs` | 6 |
| `tests/ClassroomAgent.Tests/Application/UseCases/DeanPasswordChangeTests.cs` | 11 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ListDeanAccountsQueryTests.cs` | 6 |
| `tests/ClassroomAgent.Tests/Web/Security/DeanAccountsAuthorizationTests.cs` | 7 |
| `tests/ClassroomAgent.Tests/Web/Security/DeanSessionBoundaryTests.cs` | 4 |
| `tests/ClassroomAgent.Tests/Web/Pages/DeanAccountsPageTests.cs` | 8 |
| `tests/ClassroomAgent.Tests/Web/Pages/DeanSignInPageTests.cs` | 8 |
| `tests/ClassroomAgent.Tests/Web/Pages/DeanPasswordPageTests.cs` | 8 |
| `tests/ClassroomAgent.Tests/Web/Persistence/DeanAccountAuditSchemaTests.cs` | 6 |
| `tests/ClassroomAgent.Tests/Web/Localization/DeanAccountTranslationTests.cs` | 3 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/DeanAccountSchemaTests.cs` | 6 |

Test infrastructure created:

- `tests/ClassroomAgent.Tests/TestInfrastructure/DeanAccountTestData.cs` — paths,
  policy names, form fields, audit codes and the 39 translation keys, in one
  place so the tests and the future screens cannot drift.
- `tests/ClassroomAgent.Tests/TestInfrastructure/DeanAccountWorld.cs` — the six
  use cases with every port in memory (accounts, audit, unit of work, hasher,
  read-only guard, legitimacy state).
- `tests/ClassroomAgent.Tests/TestInfrastructure/DeanAccountHostExtensions.cs` —
  the HTTP journeys, including **the Dean sign-in by password**, which no helper
  could perform before this Story.

## 3. Test files modified

| File | Change | Traced to |
|---|---|---|
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | installation migrations 4 → 5, `_AddDeanAccounts` last | db-design §8, foreseen there |
| `tests/ClassroomAgent.Tests/Web/UseCases/WorkspaceConnectionReadOnlyTests.cs` | the permitted-service-write registry grows from 3 entries to 6 | **not foreseen** — see below |
| `tests/ClassroomAgent.Tests/Application/UseCases/PermittedServiceWriteTests.cs` | the registry is named, not counted: three type names added | **not foreseen** — the same cause |

Two US-007/US-009 guards pin the permitted-service-write registry — one by count
(`WorkspaceConnectionReadOnlyTests.NoNewUseCase_IsRegisteredAsAPermittedServiceWrite`),
one by the names it holds
(`PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList`).
Both had to be updated, and both kept their assertions: the first still refuses
any US-009 type, the second still requires every entry to live in the
Application use-case namespace and to name a member of the closed list. US-012
adds three entries:
the Dean's sign-in and the two password changes, declared as `SignInBookkeeping`
— the BR-026 member that already names sign-in bookkeeping and "a Dean changing
their own password". The **closed list itself did not grow**, which the
neighbouring test `ThePermittedServiceWriteList_IsUnchanged` still asserts, and
that test is green untouched. The guard's own point — that nothing of US-009 is
exempt — is kept: only the count changed, with the reason recorded in its XML
doc. Neither the Specification nor db-design foresaw this change; it is recorded
here so SECURITY_REVIEW can judge it rather than meet it as a surprise.

No other existing test was changed, and none was disabled, weakened or deleted.

## 4. Production files created or changed — the OD-005 skeleton only

OD-005 was raised at this stage and resolved by the Owner on 2026-09-26 as
option 1 (compile-only skeleton). Every member below throws
`NotImplementedException`; **nothing is registered in dependency injection**.

Created (16): `Application/Models/DeanAccountRow`, `DeanAccountRefusal`,
`PasswordPolicyViolation`, `DeanAccountActionOutcome`, `DeanSignInResult`,
`DeanSignInOutcome`, `PasswordChangeOutcome`; `Application/Ports/IPasswordHasher`;
`Application/UseCases/DeanPasswordPolicy`, `ListDeanAccountsQuery`,
`CreateDeanAccountUseCase`, `SetDeanAccountStateUseCase`,
`ResetDeanPasswordUseCase`, `SignInDeanUseCase`,
`CompleteTemporaryPasswordChangeUseCase`, `ChangeOwnPasswordUseCase`.

Changed (7): `Domain/Entities/AppUser` (the `PasswordIsTemporary` property and
seven methods), `Domain/Entities/AuditEvent` (six factories),
`Domain/Enums/AuditAction` (+6), `AuditTargetType` (+1),
`AuditRefusalCategory` (+3), `Application/Ports/IAppUserRepository` (+2
members, with skeleton implementations in
`Infrastructure/Persistence/Repositories/AppUserRepository`),
`Application/Authorization/InstallationPolicies` (+3 policy names).

Two changes are **not** skeleton and are explained here because a reviewer will
meet them:

- `Infrastructure/Persistence/Configurations/AppUserConfiguration` — one
  `Ignore` line excluding `PasswordIsTemporary` from the EF Core model. Without
  it EF maps the property by convention and every existing test touching
  `app_user` fails against a column the migration has not created. IMPLEMENTATION
  replaces that line with the mapping, the check constraint and the migration.
- `Application/UseCases/PermittedServiceWrites` — the three use cases that write
  without a read-only guard are declared as `SignInBookkeeping`, the BR-026
  member that already names sign-in bookkeeping and "a Dean changing their own
  password". Without the declaration the **existing** architecture test
  `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`
  fails — and it should, because an undeclared write path is exactly what it
  exists to catch. The BR-026 list is not widened; no new member was added.

## 5. Commands used

```
dotnet build ClassroomAgent.sln
dotnet test ClassroomAgent.sln
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~Dean"
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName!~Dean&FullyQualifiedName!~AppUserMigrationTests"
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~ReadOnlyEnforcementTests"
```

Docker Desktop was running throughout (server 29.8.0), so the Testcontainers
PostgreSQL fixture started without the interruption US-011 met twice (TC-2).

## 6. Execution evidence

Build: **0 errors, 0 warnings** (`TreatWarningsAsErrors` is on, so the tests are
fully nullable-annotated and analyzer-clean).

Whole suite, after the two strengthenings of §8:

```
итог: 2163   сбой: 180   успешно: 1983   пропущено: 0
```

Of the 180 failures, **179 are US-012 tests** and one is the modified
`AppUserMigrationTests`. Every other test in the solution is green.

Breakdown of the first complete run (before those strengthenings, and before the
three guards of §3 and §4 were updated):

| Set | Total | Failed | Passed |
|---|---|---|---|
| US-012 tests (`~Dean`) | 195 | 177 | 18 |
| `AppUserMigrationTests` | 6 | 1 | 5 |
| Everything else | 1962 | 1 | 1961 |

Three existing guards failed on that first run, each correctly:

1. `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` —
   three new use cases wrote without a guard and without a declaration. Fixed by
   **declaring** them (§4), not by changing the test; green again.
2. `WorkspaceConnectionReadOnlyTests.NoNewUseCase_IsRegisteredAsAPermittedServiceWrite`
   — the registry count. Updated with its reason (§3); green.
3. `PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList` —
   the registry by name. Updated with its reason (§3); green.

That all three fired is the system working: a write path that is neither guarded
nor declared is exactly what US-007 built them to catch.

## 7. Red-phase verification

Every failing test compiles, starts a valid host or world, and fails for missing
production behaviour — `NotImplementedException` from a skeleton member, a route
that does not exist yet, a translation key nobody has written, or the
`password_is_temporary` column the migration has not created. No failure is a
syntax error, a bad import, a broken fixture or a wrong assertion.

The expected failure of `AppUserMigrationTests` is the one foreseen in db-design
§8: it now expects five migrations and turns green when `AddDeanAccounts` lands.

## 8. Tests that passed before the implementation, and why

Nine methods (18 cases) passed. Each was examined, as the Skill requires.

| Test | Why it passes | Verdict |
|---|---|---|
| `DeanLockoutTests.ThePolicyNumbers_AreTheOnesSc2Fixes` | reads the two constants of the skeleton | a **guard**: it fails if IMPLEMENTATION changes 5 attempts or 15 minutes |
| `DeanAccountTranslationTests.BothFiles_HoldTheSameKeys` | both files are equally complete today | a guard; it becomes meaningful when the 39 keys arrive |
| `DeanAccountAuditSchemaTests.AnActionOutsideTheList_IsStillRejected` | the closed list already rejects an unknown action | a guard: the list must stay closed after the migration widens it |
| `DeanAccountSchemaTests.TheStory_AddsNoTableAndNoIndex` | five tables, two indexes today | a guard (db-design §3.3) |
| `DeanAccountsPageTests.NoDeleteMethod_IsExposed` | no route exists, so nothing answers `200` | a guard; weak until the screen exists, meaningful afterwards (AC-008) |
| `DeanSignInPageTests.TheSignInPage_StaysAnonymous` | `/sign-in` exists since US-008 and is anonymous | real coverage |
| `DeanSignInPageTests.NoResponse_CarriesTheTypedPassword` | nothing renders yet | weak now, real once the form renders |
| `DeanAccountsPageTests.AManagementActionOnANonDeanId_Answers404` | **passed vacuously**: every path answered `404` because no route existed | **strengthened** — it now asserts first that the screen answers `200`, so the two `404`s say something about the id. It is red until the screen exists |
| `DeanSignInPageTests.TheThreeCommonRefusals_AreIndistinguishable` | **passed vacuously**: three identical `404`s | **strengthened** — it now asserts the answer is a `302` to `/sign-in` before comparing the three. It is red until the sequence exists |

The two strengthened tests are the reason §6 reports the run twice: the first
run is the honest record of what was found, the second of what is now in the
tree.

## 9. Untested Acceptance Criteria

None. All sixteen are mapped in the `ac_test_matrix`.

The one partial is AC-008: the suite proves no delete path exists **today** —
over HTTP and through the repository surface the entity model fixes — but no
test can prove a later Story adds none. SECURITY_REVIEW is the check for that.

## 10. Known limitations, carried into IMPLEMENTATION

- **Timing** of the three common refusals is not asserted (test strategy §11):
  step 2 skips the password verification by design, which is observable in
  principle as a faster answer. SC-2 accepts that trade; a deterministic timing
  assertion is not available in this suite.
- **The restricted session** of step 5 is asserted through the pages it may
  reach, not through the claim that carries it — the claim is an implementation
  detail the tests deliberately do not pin (api-design §2.6).
- **The hasher's algorithm** is not tested: `IPasswordHasher<AppUser>` is a
  framework primitive (OD-002). The tests assert that a hash is stored and
  verified through the port, never how it is computed.
- Two tests in §8 stay weak until the screens exist; both are listed so
  SECURITY_REVIEW can confirm they became meaningful.

## 11. Names IMPLEMENTATION must honour or correct together with the tests

- Routes: `/settings/deans`, `/settings/deans/{id}/state`,
  `/settings/deans/{id}/password`, `POST /sign-in`, `/sign-in/change-password`,
  `/account/password`.
- Form fields: `email`, `temporaryPassword`, `password`, `desiredState`
  (`Active` / `Disabled`), `currentPassword`, `newPassword`.
- Policy names: `ManageDeanAccounts`, `ChangeOwnPassword`,
  `CompleteTemporaryPasswordChange`.
- Audit codes: `dean_account_created`, `dean_account_disabled`,
  `dean_account_reenabled`, `dean_account_password_reset`,
  `dean_password_changed`, `dean_sign_in`; target `app_user`; categories
  `unknown_login`, `wrong_password`, `locked_out`.
- Column and constraint: `password_is_temporary`,
  `ck_app_user_password_temporary`; migration `AddDeanAccounts`.
- The 39 translation keys of `DeanAccountTestData.TextKeys`.
- `InsertAppUserAsync` in `InstallationTestHost` does **not** yet write
  `password_is_temporary`; IMPLEMENTATION adds the parameter when the column
  exists, or the seeded rows keep the column's `false` default.

## 12. Open Decisions

OD-005, raised here and resolved by the Owner on 2026-09-26 as option 1
(compile-only skeleton). `open_decisions` is now version 2; it adds OD-005 only
and changes nothing the Specification or the designs consumed, so those
artifacts are not stale in substance.

No other Open Decision was raised, and none remains unresolved.

## 13. Result

`PASS` — the red phase is verified: the suite compiles with no warnings, every
US-012 test fails for missing production behaviour, the one modified existing
test fails exactly as db-design §8 foresaw, and every other test in the solution
is green.
