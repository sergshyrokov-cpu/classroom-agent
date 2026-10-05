---
artifact_type: implementation_report
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:43:45Z
updated_at: 2026-10-05T07:43:45Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-041-unknown-file-path-404.md
    version: null
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/designs/api/US-041-api-design.md
    version: 1
  - path: docs/designs/database/US-041-db-design.md
    version: 1
  - path: docs/tests/US-041-test-strategy.md
    version: 1
  - path: docs/tests/US-041-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-041 Implementation Report

## 1. Summary

The anonymous 404 catch-all of both hosts is now mapped with the explicit route
pattern `{*path}` instead of `MapFallback`'s default `{*path:nonfile}`, so an
unmatched path whose last segment contains a dot answers `404` with the error
page to anyone instead of being challenged (`302` to sign-in). Everything else
is unchanged: same delegate, `AllowAnonymous`, and on the Control Plane the
same setup-gate exemption; fallback (lowest) precedence is kept by
`MapFallback`. Security-sensitive because it touches the SC-4 anonymous
catch-all; it adds no anonymous endpoint.

## 2. Source Artifacts

See front matter `inputs` (spec v2, open decisions v2, API design v1, DB design
v1 NOT_APPLICABLE, test strategy v1, AC matrix v1).

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `src/ClassroomAgent.Web/Program.cs` — `app.MapFallback("{*path}", …)` | Web `UnknownFileLikePathTests` (all), `NoStoreResponseTests.AMissingFileUnderAStaticPath_CarriesNoStore` | PASS |
| AC-002 | `src/ClassroomAgent.ControlPlane/Program.cs` — `app.MapFallback("{*path}", …)` with `SetupGateExemptAttribute` | ControlPlane `UnknownFileLikePathTests` (all) | PASS |
| AC-003 | unchanged — `UseStaticFiles` before `UseRouting` | `AnExistingStaticFile_IsStillServedAnonymously*` | PASS |
| AC-004 | unchanged | regression guards + full suite 3476/3476 | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Program.cs` | catch-all pattern `{*path}` + comment | FR-001, FR-003, FR-005; API D-1; AC-001, AC-004 |
| `src/ClassroomAgent.ControlPlane/Program.cs` | catch-all pattern `{*path}` + comment | FR-002, FR-003; OD-001; API D-1, D-3; AC-002 |
| `tests/ClassroomAgent.Tests/Web/Security/UnknownFileLikePathTests.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/UnknownFileLikePathTests.cs` | new (TEST_WRITING) | ac_test_matrix |
| `tests/ClassroomAgent.Tests/Web/Security/NoStoreResponseTests.cs` | assertion strengthened (TEST_WRITING) | ac_test_matrix AC-001 |
| `docs/tests/US-041-*.md`, `docs/evidence/US-041-*.md` | stage artifacts | artifact-paths.yaml |

No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 errors, 0 warnings |
| Story tests | `dotnet test --no-build --filter "…UnknownFileLikePathTests\|…NoStoreResponseTests\|…ErrorPageTests"` | 0 | 95 / 95 passed |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | 0 | 3476 total, 3476 passed, 0 failed, 0 skipped |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <5 changed .cs files>` | 0 | no changes |

Before implementation the same full suite had 20 failures — exactly the RED
tests of the matrix (test-generation report §3); all 20 now pass.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

None. The private port behaves as FR-005 states: `/health/x.txt` reaches the
catch-all and answers `404` with no body (no re-execution on that port).

## 8. Open Decisions

None touched or newly required (OD-001, OD-002 resolved).
