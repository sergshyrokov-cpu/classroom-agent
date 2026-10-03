---
artifact_type: test_generation_report
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T09:35:00Z
updated_at: 2026-10-03T09:35:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-039-test-strategy.md
    version: 1
  - path: docs/tests/US-039-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
  - path: docs/designs/database/US-039-db-design.md
    version: 1
  - path: docs/decisions/US-039-open-decisions.md
    version: 2
supersedes: null
---

# US-039 Test Generation Report

**Overall result: PASS.** 117 new test cases in 9 classes; build 0 errors / 0
warnings; the full suite runs with every failure accounted for by missing
production behaviour; no regression outside the Story.

## 1. Files

Created — tests:

- `tests/ClassroomAgent.Tests/Application/UseCases/ChooseUiLanguageUseCaseTests.cs` (25)
- `tests/ClassroomAgent.Tests/Application/UseCases/UiLanguageReadOnlyTests.cs` (6)
- `tests/ClassroomAgent.Tests/Web/Pages/LanguageSwitcherTests.cs` (16)
- `tests/ClassroomAgent.Tests/Web/Security/UiLanguageChoiceSecurityTests.cs` (34)
- `tests/ClassroomAgent.Tests/Web/Localization/DateFormatTests.cs` (5)
- `tests/ClassroomAgent.Tests/Web/Localization/UiLanguageTranslationTests.cs` (5)
- `tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerUiLanguageTests.cs` (21)
- `tests/ClassroomAgent.Tests/ControlPlane/Localization/UiLanguageTranslationTests.cs` (5)

Created — fixtures:

- `tests/ClassroomAgent.Tests/TestInfrastructure/UiLanguageTestData.cs`
- `tests/ClassroomAgent.Tests/TestInfrastructure/UiLanguageHostExtensions.cs`

Modified — tests:

- `tests/ClassroomAgent.Tests/Application/UseCases/PermittedServiceWriteTests.cs`
  — `TheRegistry_DeclaresOnlyWritesOnTheClosedList` names
  `ChooseUiLanguageUseCase` as the seventh registry entry (spec FR-007).

Created / modified — compile-only skeleton (OD-008, option 1):

- `src/ClassroomAgent.Application/UseCases/ChooseUiLanguageUseCase.cs` (new;
  `ExecuteAsync` throws `NotImplementedException`; not registered in DI)
- `src/ClassroomAgent.Domain/Entities/AppUser.cs` — `ChooseUiLanguage(UiLanguage)`
  added, body throws `NotImplementedException`

## 2. Commands

- `dotnet build ClassroomAgent.sln` → 0 errors, 0 warnings.
- Per class: `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~<Class>"`
  (a `|`-combined filter selected zero tests under Microsoft.Testing.Platform, so
  classes were run one by one).
- Full suite: `dotnet test ClassroomAgent.sln --no-build` (Docker running,
  Testcontainers PostgreSQL).

## 3. Results

Full suite: **2545 total, 2428 passed, 117 failed, 0 skipped** (5 min 54 s).

| Class | Total | Failed | Passed |
|---|---|---|---|
| ChooseUiLanguageUseCaseTests | 25 | 25 | 0 |
| UiLanguageReadOnlyTests | 6 | 6 | 0 |
| LanguageSwitcherTests | 16 | 16 | 0 |
| UiLanguageChoiceSecurityTests | 34 | 34 | 0 |
| DateFormatTests | 5 | 3 | 2 |
| Web UiLanguageTranslationTests | 5 | 5 | 0 |
| ControlPlane UiLanguageTranslationTests | 5 | 5 | 0 |
| OwnerUiLanguageTests | 21 | 21 | 0 |
| PermittedServiceWriteTests (modified) | 6 | 1 | 5 |
| ReadOnlyEnforcementTests (existing, US-007) | — | 1 | — |

All 2428 tests outside these lines pass: **no regression.**

## 4. Expected failures, by cause

- `NotImplementedException` from the skeleton — every unit test of
  `ChooseUiLanguageUseCaseTests` (24) and the undefined-enum test, which gets the
  skeleton's exception instead of `ArgumentOutOfRangeException`.
- `No service for type ChooseUiLanguageUseCase` — the five PostgreSQL tests of
  `UiLanguageReadOnlyTests` (not registered in DI yet); the sixth fails on the
  missing registry entry.
- `404` where `302`/`400` is expected — every HTTP POST to `/account/language` on
  both hosts: the action does not exist, so the anonymous catch-all answers.
  Each such test first passes its fixture steps (sign-in, page loads, the
  anonymous assertions), verified on the failure lines (e.g.
  `AnonymousPages_HaveNoSwitcher…` fails only at the signed-in presence
  assertion, `ADeanWithATemporaryPassword…` only at the switcher on the form).
- Missing switcher (`HasSwitcher` false, no `language` inputs) — page tests.
- Missing translation keys — both translation classes.
- Date strings not found — the English landing date and both Dean-list dates.
- Endpoint enumeration finds no `account/language` endpoint — both hosts.
- `PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList` —
  expects `ChooseUiLanguageUseCase` at index 3; IMPLEMENTATION adds the entry.
- `Architecture.ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`
  (existing US-007 structural test) — now sees the skeleton use case, which takes
  `IUnitOfWork` but is neither guarded nor registered. Exactly the guardrail this
  test exists for; it turns green when IMPLEMENTATION registers the use case in
  `PermittedServiceWrites` (spec FR-007). Not weakened.

No failure is caused by syntax, imports, host configuration, fixtures or
assertions.

## 5. Tests passing before implementation, explained

- `DateFormatTests.TheLastSuccessfulCheck_FollowsTheChosenLanguage(uk)` — the
  landing page already formats this time as `dd.MM.yyyy HH:mm` (invariant), which
  is exactly the Ukrainian short date. Existing behaviour satisfies the Ukrainian
  half; its English twin is red, so the pair still detects the defect FR-009
  names.
- `DateFormatTests.TheTwoLanguages_FormatTheTestDatesDifferently` — a guard on
  the test data itself (no production code involved): it proves the theory above
  cannot pass with one format for both languages.
- `PermittedServiceWriteTests.TheList_HasExactlyTheFourMembersOfBr026`
  (unchanged, existing) — structural invariant: the BR-026 enum must not grow.

Every refusal test ("nothing stored", "no switcher", "GET changes nothing") is
paired in the same test with the allowed request or a presence assertion, so none
of them passes vacuously; all are red now.

## 6. Untested Acceptance Criteria

None. AC-001 … AC-010 and OD-007 each map to at least one RED test
(`docs/tests/US-039-ac-test-matrix.md`). Excluded scenarios and their reasons:
test strategy §6.

## 7. Open Decisions

OD-008 (compile-only skeleton) — raised by this stage, resolved by the Owner on
2026-10-03 (option 1); `open_decisions` is now version 2. None open.

## 8. Notes for IMPLEMENTATION

- The fixtures fix the switcher markup contract (test strategy §4): one form per
  offered language with `<input name="language">` and `<input name="returnPath">`,
  `aria-current="true"` on the current one, keys `Layout.Language.Uk`,
  `Layout.Language.En`, `Layout.Language.Switcher` on both hosts.
- The `|`-combined `--filter` selects nothing on this runner; run classes one by
  one or the full suite.
