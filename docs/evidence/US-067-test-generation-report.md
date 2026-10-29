---
artifact_type: test_generation_report
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T09:15:59Z
updated_at: 2026-10-29T09:15:59Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-067-spec.md
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
---

# US-067 Test Generation Report

## 1. Result

**Red phase verified.** The solution builds with 0 warnings and 0 errors. The full suite has 6882 tests: 6805 passed,
77 failed, 0 skipped. All 77 failures are new US-067 cases, and each fails because the implementation is missing. No
existing test went red, and there were no environment failures (no Npgsql timeout, no Ryuk, no WSL timeout).

There are 80 new cases: 77 red and 3 green by design (§5). Every Acceptance Criterion, AC-001 to AC-010, has at least
one mapped test (`docs/tests/US-067-ac-test-matrix.md`). SPEC_REVIEW m-1 (the read-only refusal row with
`DeanLoginChanged`) is covered at three levels: the use case, the CHECK and HTTP.

## 2. Files

Created:

- `tests/ClassroomAgent.Tests/Application/UseCases/ChangeDeanLoginUseCaseTests.cs`: 41 cases (unit, TC-1)
- `tests/ClassroomAgent.Tests/Web/Pages/ChangeDeanLoginPageTests.cs`: 29 cases (HTTP, real PostgreSQL)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/DeanLoginChangedAuditSchemaTests.cs`: 6 cases (CHECK, migration)

Modified (test infrastructure):

- `TestInfrastructure/DeanAccountTestData.cs`: adds `Paths.Login`, `Fields.NewEmail`, `Audit.LoginChanged`, and four
  `TextKeys` that are also added to `TextKeys.All`. This adds 4 cases to the existing `DeanAccountTranslationTests`.
- `TestInfrastructure/DeanAccountHostExtensions.cs`: adds `ChangeDeanLoginAsync`.
- `TestInfrastructure/DeanAccountWorld.cs`: adds `ChangeLogin`, plus `UnitOfWork.NextCommitFailure`, a one-shot commit
  failure used for FR-004.

## 3. Compile-only skeleton

Every new member throws `NotImplementedException`. Nothing is registered in DI, there is no migration, route or
translation entry, and existing behaviour is unchanged.

| Declaration | File |
|---|---|
| `AuditAction.DeanLoginChanged` (appended) | `src/ClassroomAgent.Domain/Enums/AuditAction.cs` |
| `DeanAccountRefusal.LoginUnchanged` (appended) | `src/ClassroomAgent.Application/Models/DeanAccountRefusal.cs` |
| `AppUser.ChangeLogin(string email, DateTimeOffset at)` (throws) | `src/ClassroomAgent.Domain/Entities/AppUser.cs` |
| `ChangeDeanLoginUseCase` (constructor: read-only guard, users, legitimacy states, audit, unit of work, write scope, time; `ExecuteAsync(adminId, deanId, newEmail, requestId, ct)` throws) | `src/ClassroomAgent.Application/UseCases/ChangeDeanLoginUseCase.cs` |

The use case's dependencies are touched in a discard tuple only so that CS9113 does not fail the build. IMPLEMENTATION
owns the skeleton from here. It still has to widen the `DeanAccountManaged` guard, add the code mapping, add the
migration `AddDeanLoginChangedAuditAction`, the route, the controller action, the view form and the translations. The
use-case name is the one the entity model suggests, and the tests now use it.

## 4. Commands

- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test --filter FullyQualifiedName~ChangeDeanLoginPageTests`: run once by the HTTP-class worker, 27 failed
  and 2 passed.
- `dotnet test ClassroomAgent.sln`: full suite, 10 min 06 s, Docker running, 7.6 GB RAM free. Totals are in §1. The
  log was kept in the session scratchpad.

## 5. Red-phase classification

| Class | Red | Green | Why red |
|---|---|---|---|
| ChangeDeanLoginUseCaseTests | 41 | 0 | Most cases throw `NotImplementedException` from the use case or `ChangeLogin`. `TheManagementAuditFactory_AcceptsTheLoginChange` fails on the four-action guard (`ArgumentOutOfRangeException`). The `Throws` tests see `NotImplementedException` where they expect `ReadOnlyModeException` / `InvalidOperationException` / `ArgumentException`. |
| ChangeDeanLoginPageTests | 27 | 2 | The route is absent, so every post answers `404`. `TheScreen_OffersALoginChangeFormForEachDean`: there is no form in the view. |
| DeanLoginChangedAuditSchemaTests | 5 | 1 | CHECK `ck_audit_event_action` rejects `dean_login_changed` (23514), and the migration `_AddDeanLoginChangedAuditAction` does not exist. |
| DeanAccountTranslationTests (4 new cases) | 4 | 0 | "Translation key '…' is missing for 'uk'". |

Green by design. These are regression guards, not vacuous tests:

- `ChangeDeanLoginPageTests.AnUnknownIdAndAnAdminsId_Answer404`: today an absent route also answers `404`. The test
  first asserts that the screen answers `200`. It becomes meaningful once the route exists, and then it guards
  against an id leak through a `403` (the US-012 pattern).
- `ChangeDeanLoginPageTests.ANonNumericId_MatchesNoRoute`: guards the `{deanId:long}` constraint (spec §6).
- `DeanLoginChangedAuditSchemaTests.AnActionOutsideTheList_IsStillRejected`: the list stays closed after the
  migration.

The two log tests (`TheRejectedValue_IsNotWrittenToTheLog`, `ASuccessfulChange_WritesNeitherAddressToTheLog`) are red
on their status assertion, so they are not vacuous.

## 6. Untested Acceptance Criteria

None. The excluded scenarios and their reasons are in the test strategy, §5: the HTTP-level race, a concurrent edit,
and the structural "same code" of FR-002.

## 7. Open Decisions

None.

## 8. Delegation

- Kept inline: the test strategy, the AC matrix, the skeleton, `ChangeDeanLoginUseCaseTests`,
  `DeanLoginChangedAuditSchemaTests`, the fixture extensions, and the red-phase verdict.
- Delegated to `cheap-worker`: `ChangeDeanLoginPageTests`, written against a fixed brief of 20 named scenarios with
  their assertions, and reviewed here against the matrix; plus the full build and suite run.

## 9. Notes for IMPLEMENTATION

- `AnAnonymousRequest_IsSentToSignIn` posts without a token and expects `302 /sign-in`. That assumes the
  authorization middleware challenges before the antiforgery filter runs. It does on the other `/settings/deans` POSTs,
  but this has not been seen yet for the new route. If the test answers `400` once the route exists, report it. Do not
  change the expectation.
- `TheDeansTemplates_ShowTheNewAddressAsAuthor` already passes its "before" assertion: the template list shows the
  author's current email, which confirms spec I-6.
