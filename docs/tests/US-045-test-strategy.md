---
artifact_type: test_strategy
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T19:28:33Z
updated_at: 2026-10-11T19:28:33Z
produced_by: test-writer
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
supersedes: null
---

# US-045 Test Strategy — Log lines name the route, never the raw request path

## 1. Scope

Both deployables' log files (DC-10): the `RequestPath` property of every line
written inside a request, and the removal of the US-021 point fix. No endpoint,
schema, permission or translation changes (API_DESIGN and DB_DESIGN
`NOT_APPLICABLE`), so there are no contract, persistence, role or translation
tests in this Story.

## 2. Test levels

| Level | What | Why |
|---|---|---|
| Integration (`WebApplicationFactory`, Testcontainers PostgreSQL) | A real request to the installation (public and private port) and to the Control Plane; the assertion reads the host's log file | The rule is about what reaches the file under the production Serilog configuration; only a running host shows it (TC-2) |
| Architecture (reflection + source scan) | `RefusedValueRedaction` is gone from the assembly and from `src/` | AC-004 is a statement about the code |
| Regression | `CourseCardLoggingTests` unchanged; full suite | AC-004 guarantee, AC-005 |

No unit test: the rule has no business logic in `Application`; testing an
enricher in isolation would assert an implementation choice the Specification
does not make.

## 3. The probe (spec I-6)

At today's levels no production code writes a line on every request. The test
host registers its own lines through `TestInfrastructure/RouteProbe`:

- `RouteProbeBeforeRouting` — first middleware of the pipeline (an
  `IStartupFilter`), before anything else runs: spec I-1 → `(unmatched)`;
- `RouteProbeAfterPipeline` — the same middleware after the whole pipeline
  returned, still inside the request scope;
- `RouteProbeInEndpoint` — a global MVC resource filter: a line while a page or
  the error page executes (FR-002, FR-008).

The probe category is `ClassroomAgent.Tests.RouteProbe` (not `Microsoft.*`), at
`Warning`, so it passes DC-10's level overrides without touching them. The log
configuration under test is the production one.

Every scenario also runs `RouteProbe.AssertTheRuleAsync`, which checks the whole
file (spec §6): the distinctive value appears nowhere (case-insensitive); every
probe line carries a non-empty `RequestId` (FR-005); every line with a
`RequestId` has a `RequestPath` that is `(unmatched)` or starts with a single
`/` (I-4).

## 4. Scenarios

### Positive
- P-1 Installation, Admin, `GET /workspace/meet-codes/zzq-path-probe/course-choice`:
  the in-endpoint line carries `/workspace/meet-codes/{meetingCode}/course-choice`
  (AC-001, FR-002).
- P-2 Control Plane, Owner, `GET /installations/<random guid>/admins/new`: the
  in-endpoint line carries `/installations/{id:guid}/admins/new` — constraint text
  kept as declared (I-5) (AC-003).
- P-3 Error-page re-execution: in-endpoint lines are only the page template or
  `/error/{statusCode}` (FR-008, I-3).

### Negative
- N-1 Unmatched address, public port, signed in and anonymous: before-routing
  line `(unmatched)`, error-page line `/error/{statusCode}`, value nowhere
  (AC-002).
- N-2 Same on the Control Plane, Owner and anonymous (AC-003).
- N-3 Private port unmatched address: both the before-routing line and the
  after-pipeline line (fallback endpoint selected) carry `(unmatched)` — the
  catch-all fallback is not a page (I-2, FR-003).

### Boundary
- B-1 A line before routing on a request that *does* match a page still carries
  `(unmatched)` (I-1) — asserted in P-1 and P-2.
- B-2 A static file (served before routing) in both hosts: the after-pipeline line
  carries `(unmatched)` and the file name appears nowhere (FR-003).

### Validation
None — the Story accepts no input (spec §6 is a rule on the logged value,
covered by `AssertTheRuleAsync`).

### Security
- S-1 No raw path value on any line (SC-10, DC-10, §8 v89): every scenario.
- S-2 Request id kept on every probe line (DC-10): every scenario.
- S-3 The point fix is gone (AC-004) and its guarantee is held by the host-wide
  rule: `CourseCardLoggingTests` unchanged.

### Persistence
None (DB_DESIGN `NOT_APPLICABLE`).

## 5. Fixtures

- `PostgreSqlFixture` (Testcontainers) for both hosts.
- `JournalHostExtensions.StartAsync(..., Actor.Admin, beforeStart)` for the
  installation; the probe is chained after any `ConfigureServices` already set.
- `ControlPlaneTestHost.StartAsync(..., configureServices: RouteProbe.Register)`
  and `CreateOwnerAsync` for the Control Plane.
- Distinctive values: `zzq-path-probe`, `zzq-unmatched-probe`,
  `zzq-cp-unmatched-probe`, a fresh GUID.

## 6. Excluded scenarios

- A matched page on the private port with a path value: no private route has a
  parameter today (`MapPrivateEndpoints`); the rule is host-wide, so the public
  port scenarios cover the same configuration.
- The after-pipeline line of a re-executed public request: which endpoint is in
  effect after re-execution is framework behaviour the spec does not fix; the
  value-absence and well-formedness checks still cover that line.
- Lines outside a request (sync runs, start-up): unchanged by this Story; the
  existing logging tests cover their run identifier (AC-005).
- Spec §8 "a failure while computing the route falls back to the marker": no
  way to make the framework fail that lookup from outside without testing a
  specific implementation; left to SECURITY_REVIEW by reading the code.
- Query strings: out of scope.

## 7. Known limitations

- The probe proves the rule for lines written at the probe's points; a line
  written by framework code at another point is covered only by the whole-file
  checks of the value and of `RequestPath` shape.

## 8. Open Decisions affecting testing

None. OD-001 … OD-004 resolved (both hosts; `RequestPath` keeps its name; marker
`(unmatched)`; rule in §8 v89).
