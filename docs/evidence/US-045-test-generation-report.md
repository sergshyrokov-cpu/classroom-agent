---
artifact_type: test_generation_report
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T19:28:33Z
updated_at: 2026-10-11T19:45:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-045-spec.md
    version: 1
  - path: docs/decisions/US-045-open-decisions.md
    version: 1
  - path: docs/designs/api/US-045-api-design.md
    version: 1
  - path: docs/designs/database/US-045-db-design.md
    version: 1
  - path: docs/tests/US-045-test-strategy.md
    version: 1
  - path: docs/tests/US-045-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-045 Test Generation Report

## Files created

- `tests/ClassroomAgent.Tests/TestInfrastructure/RouteProbe.cs` — probe lines
  (spec I-6) and the whole-file rule check.
- `tests/ClassroomAgent.Tests/Web/Logging/RouteTemplateLoggingTests.cs` — 5 tests
  (AC-001, AC-002, AC-005).
- `tests/ClassroomAgent.Tests/ControlPlane/Security/RouteTemplateLoggingTests.cs` —
  4 tests (AC-003, AC-005).
- `tests/ClassroomAgent.Tests/Architecture/RouteTemplateLoggingRuleTests.cs` —
  2 tests (AC-004).

## Files modified

None. `CourseCardLoggingTests` is untouched (AC-004 requires it unchanged).
No production file was modified.

## Commands

- `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors.
- `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~RouteTemplateLogging|FullyQualifiedName~CourseCardLoggingTests"`
  — total 15, failed 11, passed 4.
- `dotnet test tests/ClassroomAgent.Tests --no-build` — total 5212, failed 11,
  passed 5201, skipped 0 (6 min 3 s).

## Expected failing new tests (red phase verified)

All 11 new tests fail, each for missing production behaviour:

| Test | Failure | Cause |
|---|---|---|
| W `AMatchedPage_…` | in-endpoint `RequestPath` = `/workspace/meet-codes/zzq-path-probe/course-choice`, template not found | raw path in the request scope |
| W `AnUnmatchedAddress_…(true/false)` | before-routing `RequestPath` = `/zzq-unmatched-probe/zzq-unmatched-probe`, expected `(unmatched)` | as above |
| W `AnUnmatchedAddressOnThePrivatePort_…` | `/zzq-unmatched-probe`, expected `(unmatched)` | as above |
| W `AStaticFile_…` | `/css/site.css`, expected `(unmatched)` | as above |
| C `AMatchedPage_…` | two in-endpoint lines with `/installations/<guid>/admins/new`, template not found | as above (Control Plane) |
| C `AnUnmatchedAddress_…(true/false)` | `/zzq-cp-unmatched-probe/…`, expected `(unmatched)` | as above |
| C `AStaticFile_…` | `/css/site.css`, expected `(unmatched)` | as above |
| A `TheRefusedValueRedaction_TypeNoLongerExists` | type present | point fix not yet removed |
| A `NoProductionSource_NamesTheRefusedValueRedaction` | `InstallationLogging.cs`, `RefusedValueRedaction.cs`, `CourseCardLog.cs` | as above |

The failures show the probe works: probe lines reach the file with a request
scope, MVC endpoints and the error page fire the resource filter (two
in-endpoint lines on the Control Plane — form and `/error/{statusCode}`), so the
assertions will turn green only when `RequestPath` actually changes. None passes
vacuously.

## Passing existing tests

All 5201 other tests, including the 4 `CourseCardLoggingTests` cases (AC-004
guarantee).

## Unexpected failures

None.

## Untested Acceptance Criteria

None. Exclusions with reasons are in the test strategy §6 (spec §8 lookup-failure
fallback is left to SECURITY_REVIEW by code reading).

## Open Decisions

None new; OD-001 … OD-004 resolved.

## Delegation

None. The test code is three small classes plus one helper whose design is the
judgement of this stage; the build and test runs were executed directly.

## Overall result

PASS — every AC mapped, tests compile, red phase verified, regression green.
