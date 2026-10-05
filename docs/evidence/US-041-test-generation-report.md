---
artifact_type: test_generation_report
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:38:15Z
updated_at: 2026-10-05T07:38:15Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/tests/US-041-test-strategy.md
    version: 1
  - path: docs/tests/US-041-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-041 Test Generation Report

## 1. Files

Created:
- `tests/ClassroomAgent.Tests/Web/Security/UnknownFileLikePathTests.cs` (8 methods, 23 cases)
- `tests/ClassroomAgent.Tests/ControlPlane/Security/UnknownFileLikePathTests.cs` (6 methods, 14 cases)

Modified:
- `tests/ClassroomAgent.Tests/Web/Security/NoStoreResponseTests.cs` —
  `AMissingFileUnderAStaticPath_CarriesNoStore`: assertion `NotEqual(OK)` →
  `Equal(NotFound)` and its comment, which described the F-2 gap this Story
  closes (AC-001). Strengthened, not weakened.

No production file touched.

## 2. Commands

```
dotnet build ClassroomAgent.sln
dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~UnknownFileLikePathTests"
dotnet test ClassroomAgent.sln --no-build
```

## 3. Results

| Run | Total | Failed | Passed | Skipped |
|---|---|---|---|---|
| Build | — | 0 errors | 0 warnings | — |
| Story classes | 37 | 19 | 18 | 0 |
| Full suite | 3476 | 20 | 3456 | 0 |

The 20 failures of the full suite are exactly the 19 RED cases of the two new
classes plus the strengthened `NoStoreResponseTests.AMissingFileUnderAStaticPath_CarriesNoStore`.
Every one fails with `Assert.Equal() Failure: Expected: NotFound, Actual: Found`
— the `302` sign-in (or setup-gate-free sign-in) redirect of the F-2 gap, i.e.
missing production behaviour. No other existing test fails.

## 4. Tests passing before implementation (18) — explained

- 12 signed-in cases (Admin, Dean × 4 paths; Owner × 4 paths): the fallback
  authorization policy challenges only anonymous callers, so a signed-in caller
  already reaches the `404` status-code page with no endpoint. They guard that
  the fix keeps the error page for signed-in callers.
- 2 static-file tests (AC-003), 3 redirect regressions (protected page on both
  hosts, setup gate), 1 private-port filter test (AC-004): regression guards,
  expected to pass before and after.

## 5. Corrections during the stage

None needed; the first run compiled and failed only on the expected assertion.

## 6. Untested Acceptance Criteria

None. AC-004 is additionally evidenced by the full suite being green after
implementation.

## 7. Findings (non-blocking)

- TEST F-1: 18 cases pass pre-implementation (§4) — by design.

## 8. Open Decisions

None open (OD-001, OD-002 resolved).

## 9. Overall result

PASS — red phase verified.
