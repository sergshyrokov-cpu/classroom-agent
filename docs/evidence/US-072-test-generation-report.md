---
artifact_type: test_generation_report
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:40:15Z
updated_at: 2026-11-03T20:40:15Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/designs/api/US-072-api-design.md
    version: 1
  - path: docs/designs/database/US-072-db-design.md
    version: 1
  - path: docs/tests/US-072-test-strategy.md
    version: 1
  - path: docs/tests/US-072-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-072 Test Generation Report

**Result: red phase verified.** 10 new test cases: 6 red for the missing behaviour, 4 green by design (explained below). No unexpected failure in the tests run. No Acceptance Criterion is untested.

## Files

| Change | File | Cases |
|---|---|---|
| Created | `tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerErrorPageHeaderTests.cs` | 10 |
| Modified | none | |

**Compile-only skeleton:** none. The tests work by HTTP requests and reference no new declaration, and no production file was changed.

## Delegation

The test design, the test class (small, and each case decided by the design) and the red-phase verdict were done inline. Every `dotnet build` and `dotnet test` run went to a `cheap-worker` subagent.

## Commands and results

| Command | Exit | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors | | | |
| `dotnet test --filter FullyQualifiedName~OwnerErrorPageHeaderTests` | 2 | 10 | 4 | 6 | 0 |
| `dotnet test --filter FullyQualifiedName~OwnerUiLanguageTests` | 0 | 21 | 21 | 0 | 0 |
| `dotnet test --filter FullyQualifiedName~ClassroomAgent.Tests.ControlPlane` | 2 | 994 | 988 | 6 | 0 |
| `dotnet test --filter FullyQualifiedName~ClassroomAgent.Tests.Architecture` | 0 | 213 | 213 | 0 | 0 |

The six failures in the `ControlPlane` run are the same six cases below. The new class was run twice with the same failing set. The worker's first run was repeated only because its output was truncated. No environment failure occurred.

The full suite was not run: only one new test file was added and no production file changed, so nothing else can have been affected. The two namespaces above are the ones holding the Control Plane's tests and the source-scanning rules. IMPLEMENTATION runs the full suite.

## Expected failures (red for the missing behaviour)

All six fail on `Assert.DoesNotContain` at `OwnerErrorPageHeaderTests.cs:29` with `zzrawmarker` found in the switcher's hidden return-path field, for example `value="/installations?zz=zzrawmarker"` and `value="/account/language?zz=zzrawmarker"`. This shows by a request the defect that was established by reading the code, as AC-001 requires.

- `TheAntiforgeryRefusal_LeavesTheMarkerNowhere` (`/installations`, `/account/language`)
- `ARefusedLanguageCode_LeavesTheMarkerNowhere` (`de`, `rejected-language-marker-7f3c`)
- `TheRefusal_StaysInTheOwnersLanguage` (antiforgery, refused code)

## Green by design

| Test | Why it passes before the fix |
|---|---|
| `ANotFoundAnswer_LeavesTheMarkerNowhere` (2 cases) | Spec I-2: a re-executed `404` answers under the anonymous `ErrorController` at the address `/error/404`. A regression guard, not evidence of the fix |
| `AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback` | FR-004 is existing behaviour that must survive the fix |
| `AFormRefusedByValidation_KeepsItsSwitcher` | Existing behaviour; it fails only if the fix is built on the status code rather than the page (FR-002) |

## Untested Acceptance Criteria

None. FR-005 (a comment) has no test by spec section 13.

## Open Decisions

None.

## Overall result

PASS. Ready for IMPLEMENTATION.
