---
artifact_type: test_generation_report
story: US-054
version: 1
status: DRAFT
created_at: 2026-10-24T21:30:00Z
updated_at: 2026-10-24T21:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/designs/api/US-054-api-design.md
    version: 1
  - path: docs/designs/database/US-054-db-design.md
    version: 1
  - path: docs/tests/US-054-test-strategy.md
    version: 1
  - path: docs/tests/US-054-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-054 Test Generation Report

## 1. Files

| Change | File |
|---|---|
| Created | `tests/ClassroomAgent.Tests/Web/Pages/TechnicalAccountSuperAdminTextsTests.cs` (8 methods, 34 cases) |
| Created | `tests/ClassroomAgent.Tests/TestInfrastructure/TechnicalAccountTextsTestData.cs` (binding wording §4.1, removed keys, retired values) |
| Modified | `tests/ClassroomAgent.Tests/TestInfrastructure/ConnectionInstructionTestData.cs` — new keys, `Measures`, ten-statement order, `All`; `ReadOnlyRoles`/`NoWriteAccess` removed; "Super Admin" dropped from `ForbiddenRoleNames` with a §9 v97 comment (I-2, m-2) |
| Modified | `tests/ClassroomAgent.Tests/Web/Pages/ConnectionInstructionTechnicalAccountTests.cs` — class summary; `ThePage_SaysASuperAdminAccountIsNotAcceptable` → `ThePage_SaysTheAccountGetsTheSuperAdminRoleWithSignInClosed`; `NoWorkspaceAdminRoleName_ReachesThePage` asserts `SuperAdminRole`; guard comments |
| Modified | `tests/ClassroomAgent.Tests/Web/Localization/ConnectionInstructionTranslationTests.cs` — English-page test asserts `SuperAdminRole` |
| Modified (comment only) | `tests/ClassroomAgent.Tests/Web/Localization/AccessCheckTranslationTests.cs` (line 53, Owner's m-1) |
| Modified (comment only) | `tests/ClassroomAgent.Tests/TestInfrastructure/WorkspaceConnectionTestData.cs` (line 19, Owner's m-1) |
| Modified (comment only) | `tests/ClassroomAgent.Tests/Infrastructure/Google/GoogleClassroomReaderTests.cs` — summary of `TheTokenRequest_ImpersonatesTheTechnicalAccount` (FR-006) |
| Created | `docs/tests/US-054-test-strategy.md`, `docs/tests/US-054-ac-test-matrix.md`, this report |

## 2. Compile-only skeleton

None. Tests name keys through test data (`ConnectionInstructionTestData.TextKeys`), so no production
declaration is needed. Production changes — resx entries, `ConnectionInstructionTextKeys`, the view's
nested list, the `SaveWorkspaceConnectionRequest` and `ConnectionInstructionTextKeys` comments — are
IMPLEMENTATION's.

## 3. Commands

Delegated to a `cheap-worker` subagent (Sonnet); test design, data and code written inline.
Docker Desktop was not running at first; started, then:

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build --filter FullyQualifiedName~<Class>`, one class per run.

## 4. Results

| Class | Total | Passed | Failed |
|---|---|---|---|
| TechnicalAccountSuperAdminTextsTests (new) | 34 | 0 | 34 |
| ConnectionInstructionTechnicalAccountTests | 30 | 6 | 24 |
| ConnectionInstructionTranslationTests | 50 | 34 | 16 |
| ConnectionInstructionHandoverTests | 14 | 13 | 1 |
| ConnectionInstructionPageTests | 23 | 22 | 1 |
| AccessCheckTranslationTests | 64 | 64 | 0 |
| WorkspaceConnectionPageTests | 15 | 15 | 0 |
| LastSynchronizationBlockTests | 34 | 34 | 0 |
| GoogleClassroomReaderTests | 14 | 14 | 0 |
| AccessCheckFailureTests | 14 | 14 | 0 |

These are every class that references the changed test data (ripgrep over `tests/`) plus the
classes of the pages whose text changes. The full suite was not rerun: no other file changed.

## 5. Red-phase classification — all 76 failures expected

- **Missing key (majority):** `Translation key 'ConnectionInstruction.TechnicalAccount.<SuperAdminRole |
  MeasuresLead | Measure.*>' is missing for '<uk|en>'` — the seven new keys do not exist yet. Hits
  the binding-wording, order, nesting, completeness, on-the-page, role-guard theories and the two
  existing statement-list tests (Page, Handover).
- **Old wording still in place:** `EveryRewrittenEntry_HasTheBindingWording` for `NoPersonBehindIt`,
  the hint and `TechnicalAccountCannotRead` (uk/en) — `Strings differ` at the reworded position.
- **Removed keys still present:** `TheRemovedStatements_ExistInNeitherLanguageFile` (uk/en).
- **Retired text still rendered:** `TheInstruction_CarriesNoRetiredStatement` — 3 of 8 retired values
  found per language (the three instruction statements of that language; the hint is not on the
  instruction), which shows the check bites.
- **New text not shown:** `TheConnectionPage_ShowsTheV97Hint`,
  `TheAccessCheck_TellsTheSchoolToGrantTheSuperAdminRole`,
  `TheLastSynchronizationBlock_TellsTheSchoolToGrantTheSuperAdminRole` (uk/en) — the step text /
  page still carries the pre-v97 advice.

No failure is caused by compilation, fixtures, host setup or assertion errors.

## 6. Passing tests

- Regression classes stay green (AccessCheckTranslation, WorkspaceConnectionPage, LastSynchronizationBlock,
  GoogleClassroomReader, AccessCheckFailure). They read texts through their keys, so they stay green after
  the rewording too.
- `ThePage_DoesNotInventATechnicalAccountAddress` (AC-004) is green by design: unchanged behaviour.
- `AccessCheckTranslationTests.NoMessage_NamesAWorkspaceAdminRole` is green: the new advice names only
  super-admin, now outside the guard list.

## 7. Untested Acceptance Criteria

None. Every AC-001…AC-008 maps to at least one test (matrix).

## 8. Open Decisions

None.

## 9. Overall

PASS — tests compile, regression green, every new behaviour test red for the expected reason.
