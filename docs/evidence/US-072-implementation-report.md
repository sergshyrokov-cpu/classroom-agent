---
artifact_type: implementation_report
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:56:13Z
updated_at: 2026-11-03T20:56:13Z
produced_by: dotnet-implementor
attempt: 1
inputs:
  - path: docs/stories/US-072-control-plane-error-page-without-switcher.md
    version: null
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
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
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-072 Implementation Report

## 1. Summary

The Control Plane's language switcher no longer renders on the error page, under any endpoint. A refused request's address is therefore no longer copied into the page. The change is one condition and a reworded comment in one view. Build, full test suite and format check pass. The change removes a reflection and adds no input, endpoint, policy or text, so `security_sensitive` is false; SECURITY_REVIEW still runs.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-072-control-plane-error-page-without-switcher.md` | current |
| Specification | `docs/specifications/US-072-spec.md` | 1 |
| Open Decisions | `docs/decisions/US-072-open-decisions.md` (none open) | 1 |
| API design | `docs/designs/api/US-072-api-design.md` (NOT_APPLICABLE) | 1 |
| Database design | `docs/designs/database/US-072-db-design.md` (NOT_APPLICABLE) | 1 |
| Test strategy | `docs/tests/US-072-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-072-ac-test-matrix.md` | 1 |

## 3. Implemented Acceptance Criteria

Implementation location for all: `src/ClassroomAgent.ControlPlane/Views/Shared/_LanguageSwitcher.cshtml`, the `isErrorPage` condition. Test class: `OwnerErrorPageHeaderTests`.

| AC | Test | Status |
|---|---|---|
| AC-001 (a) | `TheAntiforgeryRefusal_LeavesTheMarkerNowhere` (2 cases) | Pass; red before the fix |
| AC-001 (b) | `ARefusedLanguageCode_LeavesTheMarkerNowhere` (2 cases) | Pass; red before the fix |
| AC-002 | `ANotFoundAnswer_LeavesTheMarkerNowhere` (2 cases) | Pass; a guard, green before the fix too (spec I-2) |
| AC-003 | `TheRefusal_StaysInTheOwnersLanguage` (2 cases) | Pass; red before the fix |
| AC-004 | `AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback`, `AFormRefusedByValidation_KeepsItsSwitcher`, `OwnerUiLanguageTests` (21 cases, file unchanged) | Pass |

FR-005: the comment is reworded to state the FR-002 rule and cite this Story. No test, by spec section 13.

## 4. Change Set

| Change | File | Trace |
|---|---|---|
| Modified | `src/ClassroomAgent.ControlPlane/Views/Shared/_LanguageSwitcher.cshtml` | FR-001, FR-002, FR-005 |
| Created (at TEST_WRITING) | `tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerErrorPageHeaderTests.cs` | `ac_test_matrix` |

No test was changed at this stage. No secret, generated database file or IDE-local config is in the change set.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test --filter FullyQualifiedName~OwnerErrorPageHeaderTests` | 0 | 10 of 10 passed, 0 skipped |
| `dotnet test --filter FullyQualifiedName~OwnerUiLanguageTests` | 0 | 21 of 21 passed, 0 skipped |
| `dotnet test ClassroomAgent.sln` | 0 | 7571 total, 7571 passed, 0 failed, 0 skipped, 11m 52s |
| `dotnet format ClassroomAgent.sln --verify-no-changes --include tests/ClassroomAgent.Tests/ControlPlane/Security/OwnerErrorPageHeaderTests.cs` | 0 | No changes needed |

The format check covers the C# test file only: `dotnet format` does not process `.cshtml`. The view's edit follows the surrounding formatting and was checked by reading.

**Baseline.** Before the fix (TEST_WRITING report): the 6 refusal cases red on the marker, `OwnerUiLanguageTests` 21 of 21, `ControlPlane` 988 of 994, `Architecture` 213 of 213. The full suite before this Story was 7561 of 7561; the 10 new cases make 7571.

**Delegated.** The build, all test runs up to and including the start of the full suite went to a `cheap-worker` subagent. The edit (a rule) and this report were done inline. The format check and the reading of the full run's result were run inline, because the worker handed back while the suite was still running.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

None from the approved artifacts.

One technical choice inside FR-002: the rule is a condition in the view, not a `PageHeaderRules` class as in the installation. The installation's class exists because two controls share the rule; the Control Plane has only the switcher (spec FR-002), and the condition it extends was already in this view.

The header element on the error page is now empty, as it already is on the sign-in pages (spec section 9). This was not looked at in a browser; no Acceptance Criterion asks for it.

## 8. Open Decisions

None touched or newly required.
