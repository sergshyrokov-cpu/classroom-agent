---
artifact_type: implementation_report
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T19:47:26Z
updated_at: 2026-10-11T19:47:26Z
produced_by: dotnet-implementor
attempt: 1
inputs:
  - path: docs/stories/US-045-route-template-in-logs.md
    version: null
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
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-045 Implementation Report

## 1. Summary

Both hosts now replace the request scope's `RequestPath` on every log line with
the route template of the selected endpoint (`/` + `RoutePattern.RawText`), or
with `(unmatched)` when there is no endpoint, when the catch-all fallback is
selected, or when reading the route fails. The US-021 point fix
`RefusedValueRedaction` is deleted. Build, full suite (5212/5212) and format
check pass. Security-sensitive because it is the SC-10 / DC-10 log rule.

## 2. Source Artifacts

As listed in `inputs`: Story, Specification v1 (APPROVED), Open Decisions v1
(OD-001 … OD-004 resolved), API and DB designs v1 (`NOT_APPLICABLE`), test
strategy v1, AC→test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Web.Configuration.RequestRouteEnricher.Enrich`, registered in `InstallationLogging.Configure` | `Web.Logging.RouteTemplateLoggingTests.AMatchedPage_LogsItsRouteTemplate_AndNeverThePathValue` | PASS |
| AC-002 | same; `Web.Security.UnmatchedRequestMetadata` on the fallback in `Program.cs` | `Web.Logging.RouteTemplateLoggingTests.AnUnmatchedAddress_…(true/false)`, `…OnThePrivatePort_…`, `AStaticFile_…` | PASS |
| AC-003 | `ControlPlane.Security.RequestRouteEnricher`, `ControlPlane.Security.UnmatchedRequestMetadata`, Control Plane `Program.cs` | `ControlPlane.Security.RouteTemplateLoggingTests` (4) | PASS |
| AC-004 | `RefusedValueRedaction.cs` deleted, its registration removed, `CourseCardLog` comment updated | `Architecture.RouteTemplateLoggingRuleTests` (2); `Web.Logging.CourseCardLoggingTests` unchanged (4 cases) | PASS |
| AC-005 | only `RequestPath` is touched; the request id comes from the unchanged scope | `RouteProbe.AssertTheRuleAsync` in every scenario; full suite | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Configuration/RequestRouteEnricher.cs` | new | FR-001 … FR-004, §8 fail-closed; AC-001, AC-002 |
| `src/ClassroomAgent.Web/Security/UnmatchedRequestMetadata.cs` | new | FR-003, I-2 |
| `src/ClassroomAgent.Web/Configuration/InstallationLogging.cs` | enricher replaces `RefusedValueRedaction`; `Configure` takes an `IHttpContextAccessor` | FR-001, FR-006 |
| `src/ClassroomAgent.Web/Program.cs` | `AddHttpContextAccessor`; accessor passed to logging; fallback marked | supporting DI; FR-003 |
| `src/ClassroomAgent.Web/Configuration/RefusedValueRedaction.cs` | deleted | FR-006, AC-004 |
| `src/ClassroomAgent.Web/Security/CourseCardLog.cs` | doc comment no longer names the deleted type | FR-006 |
| `src/ClassroomAgent.ControlPlane/Security/RequestRouteEnricher.cs` | new | FR-001 (OD-001), AC-003 |
| `src/ClassroomAgent.ControlPlane/Security/UnmatchedRequestMetadata.cs` | new | FR-003, I-2 |
| `src/ClassroomAgent.ControlPlane/Program.cs` | `AddHttpContextAccessor`; enricher registered; fallback marked | supporting DI; FR-001, FR-003 |
| `tests/…` (4 files), `docs/tests/US-045-*`, `docs/evidence/US-045-test-generation-report.md` | from TEST_WRITING, unchanged here | `ac_test_matrix` |
| `docs/evidence/US-045-implementation-report.md` | this report | `implementation_report` |

No secret, database file or IDE config in the change set. No package added.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Baseline (red) | from TEST_WRITING: `dotnet test tests/ClassroomAgent.Tests --no-build` | 5212 total, 11 failed (all new, expected), 5201 passed |
| Build | `dotnet build ClassroomAgent.sln` | 0 errors, 0 warnings |
| Story tests | `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~RouteTemplateLogging\|FullyQualifiedName~CourseCardLoggingTests"` | 15 total, 15 passed |
| Full suite | `dotnet test tests/ClassroomAgent.Tests --no-build` | exit 0; 5212 total, 5212 passed, 0 failed, 0 skipped |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <12 changed files>` | exit 0 |

Delegation: none. The change is four small types and three wiring edits,
decided inline; the runs were short to start and were executed directly in the
background rather than spending a subagent's start-up context on them.

## 6. Configuration Changes

No `appsettings` change. Startup wiring only: `AddHttpContextAccessor()` in
both hosts (the enricher reads the current endpoint); the enricher added to each
host's Serilog configuration; the fallback endpoint of each host carries
`UnmatchedRequestMetadata`.

## 7. Deviations and Discovered Problems

- **Placement in the Control Plane.** It has no `Configuration` namespace
  (`package-map.md`); its logging is configured in `Program.cs`. The enricher
  and the metadata type sit in `ControlPlane.Security`, next to the fallback and
  the error page they classify, rather than in a new namespace (AD-11).
- **Duplicated type per host.** The two hosts share only `Contracts`, which may
  carry no behaviour; the enricher is duplicated, as `NoStoreMiddleware` and
  `GlobalAntiforgeryFilter` already are.
- **Ordering with the scope.** The enricher acts only on events that already
  carry `RequestPath`, i.e. it relies on Serilog.Extensions.Logging adding the
  scope properties before the root enrichers run. The removed point fix relied
  on the same order (it removed the property), and the tests confirm it: if
  the order were reversed, the raw path would remain and every RED test would
  still fail.
- **Fail-closed path (spec §8)** is a `catch` returning the marker; not
  testable from outside (test strategy §6) — for SECURITY_REVIEW to read.
- `CourseCardLog.RefusedEventId` stays: it is still the event's id in the
  `LoggerMessage` attribute.

## 8. Open Decisions

None touched or required.
