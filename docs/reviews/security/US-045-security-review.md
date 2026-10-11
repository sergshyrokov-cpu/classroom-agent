---
artifact_type: security_review
story: US-045
version: 1
status: APPROVED
created_at: 2026-10-11T20:20:00Z
updated_at: 2026-10-11T20:20:00Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-045-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 89
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-045 Security Review — Log lines name the route, never the raw request path

## 1. Executive Summary

**Verdict: PASS.** The change replaces the raw request path in every log line
written inside a request with the route template of the selected endpoint, or
the fixed marker `(unmatched)`, on both deployables (installation — both ports —
and Control Plane). It removes the US-021 point fix `RefusedValueRedaction`
without losing its guarantee. It narrows what reaches log files (SC-10, DC-10,
`trebovaniya.md` v89 §8) and widens nothing.

Build 0/0, full suite 5212/5212 (Testcontainers ran), no vulnerable packages,
no package or project-reference change. No Critical, Major or Minor findings;
four Informational notes (§18).

## 2. Reviewed Artifacts

As listed in `inputs`. API and DB designs are `NOT_APPLICABLE` records; no
OpenAPI file and no entity model exist by design (spec §10).

## 3. Security-Relevant Scope

- Exposed functionality: none added. No endpoint, page, policy, anonymous entry,
  response field or translation changes.
- Changed components: Serilog configuration of both hosts
  (`InstallationLogging.Configure`, Control Plane `Program.cs`), a new
  `RequestRouteEnricher` per host, a `UnmatchedRequestMetadata` marker on each
  host's catch-all fallback, `AddHttpContextAccessor()` on both hosts; deletion
  of `RefusedValueRedaction`; a comment in `CourseCardLog`.
- Protected asset: values a caller places in an address — Meet codes, course
  ids, template references, arbitrary text — which may identify a course or a
  meeting of minors' classes.
- Trust boundary: browser → host (untrusted path) → log file read by the Owner.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker available (Testcontainers suites ran).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 5212 total, 5212 passed, 0 failed, 0 skipped.
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable packages in any of the seven projects (api.nuget.org).
- `git diff HEAD -- src/**/*.csproj` — empty.
- Delegated to `cheap-worker` (facts only): the commands above, `git status`,
  `git diff --stat`. All code reading and judgement in this review is the
  reviewer's own.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed change. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password, cookie or HTTPS change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | Both fallbacks keep `.AllowAnonymous()` and their 404 behaviour; only metadata added (`Web/Program.cs:157`, `ControlPlane/Program.cs:122`). Middleware order unchanged. No new endpoint. Existing anonymous-enumeration tests green. |
| SC-5 Read-only mode | NOT_APPLICABLE | No use case touched. |
| SC-6 No DB UI | NOT_APPLICABLE | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | No key or reference handling. |
| SC-8 Google | NOT_APPLICABLE | No Google port touched. |
| SC-9 Channel | NOT_APPLICABLE | Private port binding unchanged; private-port unmatched path covered by a test. |
| SC-10 Hygiene | PASS | `RequestRouteEnricher` overwrites `RequestPath` whenever present; raw path not copied to any other property, message or scope (FR-004). Tests assert the probe value appears nowhere in the log file, for matched, unmatched (signed in / anonymous), private-port and static-file requests, on both hosts. |
| SC-11 Audit | NOT_APPLICABLE | `AuditEvent` records no path; untouched. |
| SC-12 Owner | PASS | Control Plane copy lives in `ControlPlane.Security`; no new reference to `Web` or `Domain`. |
| SC-13 Outbound | NOT_APPLICABLE | No outbound flow. |

## 6. Authentication and Authorization

No change. The fallback endpoints were already anonymous (SC-4 closed list);
adding `UnmatchedRequestMetadata` only influences the logged value. The
enricher reads `HttpContext.GetEndpoint()` and never affects request handling.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

- Logs: the property is either a code-declared template (`RoutePattern.RawText`,
  leading `/` normalised) or `(unmatched)`. `RawText` is the declared pattern,
  never the matched values. No conventional or dynamic routes exist
  (`MapControllerRoute` / `MapDynamic*` absent in `src/`), so no template is
  filled from request values.
- Catch-all `{*path}` fallback logs the marker, not its pattern (I-2) — verified
  by code and by `AnUnmatchedAddressOnThePrivatePort_LogsTheMarker_EvenOnceTheFallbackHasAnswered`.
- Static files and pre-routing lines → marker (tests on both hosts).
- Error-page re-execution → `/error/{statusCode}` (FR-008), tested.
- Responses, DTOs, exports, audit rows: unchanged.

## 9. Input Validation

No input accepted. Nothing to validate.

## 10. API Security

No endpoint, method, request or response change (API design `NOT_APPLICABLE`).

## 11. Persistence and Configuration

No schema or migration. Configuration change is code-only (enricher wiring);
`IHttpContextAccessor` registration is a framework singleton with no security
effect. The startup-refusal logger receives a bare `HttpContextAccessor` —
outside any request, so no line carries `RequestPath`.

## 12. Logging, Audit and Telemetry

- Fail-closed (spec §8, verified by code reading — not testable from outside, as
  TEST_WRITING recorded): `Route()` wraps the lookup in a `try/catch`; any
  exception except `OutOfMemoryException` yields `(unmatched)`. It never falls
  back to the raw path. A disposed `HttpContext` reached from flowed execution
  context would throw and fall into the marker — safe.
- Ordering: the scope properties are added by the Serilog provider's context
  enricher on the child logger before the root enrichers run, so the replacement
  always sees and overwrites `RequestPath`. End-to-end file assertions prove it
  for the current package versions (INFO-2).
- Request id preserved (FR-005): asserted on every probe line.
- US-021 guarantee: `CourseCardLoggingTests` pass unchanged; the refused course
  id now never reaches the file because the route template replaces the path.
- Hook telemetry unaffected.

## 13. Dependencies

No package and no project reference added (`.csproj` diff empty). Vulnerability
scan clean for all projects.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| AC-001 matched page logs template, value absent | `Web/Logging/RouteTemplateLoggingTests.AMatchedPage_…` |
| AC-002 unmatched, signed in / anonymous | `…AnUnmatchedAddress_…(true/false)` |
| Private port unmatched (I-2) | `…AnUnmatchedAddressOnThePrivatePort_…` |
| Static file (FR-003) | `…AStaticFile_…` |
| AC-003 Control Plane | `ControlPlane/Security/RouteTemplateLoggingTests` (3 tests) |
| AC-004 point fix gone | `Architecture/RouteTemplateLoggingRuleTests` + `CourseCardLoggingTests` |
| AC-005 nothing else changes | full suite green |

Assertions are on the log file itself, with the production log configuration
(I-6); they prove the property, not merely that a method was called.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Caller puts personal text in an unknown URL to plant it in logs | Marker only | Unmatched tests, value absent from file | PASS |
| Caller puts a value in a route parameter (Meet code) | Template only | Matched-page test | PASS |
| Value in a static-file-like path | Marker | Static-file test | PASS |
| Lookup throws mid-request | Marker, line kept | Code: `catch … return Unmatched` | PASS (code reading) |

## 16. Repository Hygiene

`google_credentials.json`, `dac-classroom-agent-*.json`, `classroom_cache.db`
and root `.xlsx` exports remain git-ignored (contents not opened). No new
secret-like, database or export file in the change set. The staged deletion of
`RefusedValueRedaction.cs` is in scope (FR-006).

## 17. Deviations

None. The Control Plane enricher lives in `ControlPlane.Security` rather than a
`Configuration` namespace; recorded in the implementation report, no security
effect.

## 18. Findings

- **INFO-1 (LOGGING)** — `RequestRouteEnricher.Route()` excludes
  `OutOfMemoryException` from the fail-closed catch. Should the lookup ever throw
  OOM, Serilog's safe enricher wrapper would swallow it and the line would keep
  the raw path. Practically unreachable; no correction required. Both hosts.
- **INFO-2 (LOGGING)** — Correctness relies on scope properties being present
  before root enrichers run (Serilog.Extensions.Logging behaviour). The
  file-level tests would fail on a package upgrade that changed this; keep them
  when upgrading Serilog.
- **INFO-3 (OTHER)** — The enricher and marker exist twice (Web, ControlPlane)
  because the Control Plane may not reference Web (package-map). A future change
  to one must be mirrored in the other; the per-host tests guard each copy.
- **INFO-4 (DATA_EXPOSURE)** — Out of this Story's scope and unchanged: API-6
  error bodies echo `Request.Path` back to the caller (response, not log), and
  `LogError(exception, …)` writes the exception text, which a future exception
  type could fill with a request value. Neither is a log-path issue under §8;
  noted for awareness only.

## 19. Positive Controls

- Host-wide, event-independent replacement of `RequestPath` on both hosts.
- Fail-closed fallback to a constant marker.
- Fallback endpoints explicitly marked so a catch-all pattern is never logged.
- Request id retained on every request line.
- File-level negative assertions for the probe values on both hosts.
- No package, schema, endpoint or authorization change.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-004 are resolved.

## 21. Review Limitations

- The §8 fail-closed branch is verified by code reading only.
- No penetration testing; this is a code, configuration and test review.
- Framework log lines below `Warning` for `Microsoft.*` are filtered by DC-10
  levels and were not exercised at lower levels.

## 22. Verdict Rationale

Implementation report and an independent rerun show a green build and suite.
Every touched SC item is PASS; all ACs map to passing file-level tests; no
Critical, Major or Minor finding; no open security decision. Verdict **PASS**;
next stage `HUMAN_PR_APPROVAL`.
