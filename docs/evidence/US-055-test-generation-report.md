---
artifact_type: test_generation_report
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T12:45:00Z
updated_at: 2026-10-25T12:45:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/api/US-055-api-design.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
  - path: docs/designs/database/US-055-entity-model.md
    version: 1
  - path: docs/tests/US-055-test-strategy.md
    version: 1
  - path: docs/tests/US-055-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-055 Test Generation Report

## 1. Files

Created:

- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/GradeColumnsPersistenceTests.cs` (5 methods, 7 cases)
- `tests/ClassroomAgent.Tests/Web/BackgroundServices/LargeGradesSyncTests.cs` (5)
- `tests/ClassroomAgent.Tests/Web/Pages/LargeGradesPageTests.cs` (4)
- `docs/tests/US-055-test-strategy.md`, `docs/tests/US-055-ac-test-matrix.md`, this report

Modified: none. No test infrastructure helper changed.

## 2. Compile-only skeleton

None. The entity model names no new declaration (no C# signature change); the
tests use existing types only.

## 3. Commands

Run 2026-10-25 by a delegated `cheap-worker` (build and test runs only; no file
edits), one filter term per run:

```
dotnet build ClassroomAgent.sln
dotnet test ClassroomAgent.sln --filter FullyQualifiedName~GradeColumnsPersistenceTests
dotnet test ClassroomAgent.sln --filter FullyQualifiedName~LargeGradesSyncTests
dotnet test ClassroomAgent.sln --filter FullyQualifiedName~LargeGradesPageTests
```

Build: 0 warnings, 0 errors. Docker/Testcontainers healthy (no stream timeouts,
no Ryuk cancellation).

## 4. Results (red phase)

16 cases: **15 failed, 1 passed** — every failure is caused by the missing
production change (columns still `numeric(10,4)`, no `WidenGradeColumns`).

| Class | Case | Result | Failure | Classification |
|---|---|---|---|---|
| P | `TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne` | fail | no `*_WidenGradeColumns` migration | missing implementation |
| P | `TheMigration_KeepsEveryStoredValueAndNull` | fail | guard: no `*_WidenGradeColumns` migration | missing implementation (guard against a vacuous pass) |
| P | `TheThreeColumns_AreUnconstrainedNumeric_AndNullable` | fail | expected `numeric|-|-|YES`, got `numeric|10|4|YES` | missing implementation |
| P | `LargeAndFineValues…("12345678.5", …)` | fail | `DbUpdateException` → 22003 numeric field overflow | missing implementation |
| P | `LargeAndFineValues…("99999999999999999999.123456", …)` | fail | `DbUpdateException` → 22003 | missing implementation |
| P | `LargeAndFineValues…("10.123456", …)` | fail | SQL equality false (stored rounded to 4 places) | missing implementation |
| P | `NegativeValues_AreStillRefusedByTheExistingChecks` | **pass** | — | green by design: regression guard for the kept CHECKs (db-design §7) |
| S | `LargeMaximumPoints_AreStoredAsReturned_AndTheRunCompletes` | fail | run `failed`, expected `completed` | missing implementation (the P-9 overflow) |
| S | `LargeGrades_AreStoredAsReturned_AndTheRunCompletes` | fail | run `failed` | missing implementation |
| S | `SixDecimalPlaces_AreNotRounded` | fail | expected 10.123456, got 10.1235 | missing implementation (rounding) |
| S | `TheCoursesAfterTheUnusualOne_StillLoad_AndTheMeetStepRuns` | fail | run `failed` | missing implementation |
| S | `ACompletedRunWithLargeValues_LogsNoneOfThem` | fail | run `failed` (completion asserted first) | missing implementation |
| W | `TheJournal_ShowsTheLargeGradeAndMaximum_AsStored` | fail | seed refused: 22003 | missing implementation |
| W | `ARawPointsReport_ShowsTheLargeGradeAndMaximum_AsStored` | fail | seed refused: 22003 | missing implementation |
| W | `TheBuiltInScaleReport_DoesNotFail_OnTheLargeValues` | fail | seed refused: 22003 | missing implementation |
| W | `TheReportExport_CarriesTheLargeGradeAndMaximum_AsStored` | fail | seed refused: 22003 | missing implementation |

P = `GradeColumnsPersistenceTests`, S = `LargeGradesSyncTests`, W = `LargeGradesPageTests`.

**Note for IMPLEMENTATION.** The W tests have not yet reached their display
assertions; they can show a formatting failure only once the columns hold the
values. Spec FR-005 / I-3 allows a formatting change only if one of them then
fails.

No unexpected failures. The full suite was not run at this stage (no source
change, no shared helper changed).

## 5. Coverage

Every AC (AC-001 … AC-006, derived AC-006a) maps to at least one test
(`ac_test_matrix`). Untested Acceptance Criteria: none.
SPEC_REVIEW m-1 → `TheBuiltInScaleReport_DoesNotFail_OnTheLargeValues`;
m-2 → AC-006a uses the table layout (strategy §5).

## 6. Open Decisions

None.

## 7. Delegation

Test design, the three classes and the artifacts were written inline (small
Story, three classes). The build and the three test runs were delegated to
`cheap-worker`.

## 8. Overall result

PASS — tests compile, the red phase is verified, the one green case is
explained.
