---
artifact_type: specification
story: US-045
version: 1
status: APPROVED
created_at: 2026-10-11T17:46:30Z
updated_at: 2026-10-11T17:48:13Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-045-route-template-in-logs.md
    version: null
  - path: trebovaniya.md
    version: 89
  - path: docs/specifications/US-021-spec.md
    version: 1
supersedes: null
---

# US-045 Specification — Log lines name the route, never the raw request path

## 1. Overview

ASP.NET Core opens a log scope for every request, and both hosts copy every
scope property into each log line (`Enrich.FromLogContext()`). That scope
carries `RequestPath` — the path exactly as the caller sent it — so a Meet code,
a template reference or any text typed into an address reaches the log file on
every line written during that request.

`trebovaniya.md` v89 §8 now forbids it: *"Строка, записанная во время запроса,
называет маршрут страницы, а не присланный адрес … для адреса, которому не
соответствует ни одна страница, — одна общая метка без адреса"*, and the rule is
the same for the installation and the Control Plane. `deployment-conventions.md`
DC-10 restates it.

This Story makes the rule hold host-wide in both deployables and removes the
point fix US-021 added for one event (`RefusedValueRedaction`, event 5463).

It is a conformance change to logging. No endpoint, page, schema, permission or
user-visible text changes.

## 2. Business Goal

No value taken from a request address reaches a log file, on any page, now or
in a later Story — without each Story adding its own redaction (SC-10, DC-10).
The Owner still sees *which* page a line came from, which is what diagnosis
needs.

## 3. Business Flow

1. A request reaches a host (installation public port, installation private
   port, or Control Plane).
2. Code anywhere in the pipeline writes a log line — application code,
   framework code at the levels DC-10 lets through, the error page, a
   middleware.
3. The line is written to the host's log file with its request identifier and a
   `RequestPath` property whose value is:
   - the route template of the endpoint the request has been matched to, when
     there is one (FR-002);
   - the fixed marker `(unmatched)` otherwise (FR-003).
4. The raw path, and any value taken from it, appear nowhere in the line.

Lines written outside a request (synchronization runs, start-up, background
services) carry no request scope today and are unchanged.

## 4. Functional Requirements

**FR-001 — One rule, both hosts.** The installation (both ports — they share one
host and one log configuration) and the Control Plane apply the same rule to
every log event written while a request is in progress (OD-001; §8 v89; DC-10).

**FR-002 — A matched request names its route.** When the request has been
matched to an endpoint that has a route pattern, the line's `RequestPath`
property (OD-002 — same name, new meaning) carries that endpoint's route
template: the pattern as declared in code, with exactly one leading `/`, e.g.
`/workspace/meet-codes/{meetingCode}/link`,
`/installations/{id:guid}/admins`. Parameter names, and constraint text where a
route declares one (`{deanId:long}`), are code, not values, and may appear. No
route value — what the caller put in place of a parameter — appears.

**FR-003 — An unmatched request carries one fixed marker.** When the request has
no matched endpoint at the moment the line is written, `RequestPath` is the
literal string `(unmatched)` — the same for every such request, signed in or
anonymous, any method, any port (OD-003; text set here as the Owner required).
This covers:

- a request that only the host's catch-all fallback serves (`MapFallback("{*path}")`
  in both hosts — the `404` / `405` answer of SC-4 and US-028): the fallback is
  not a page, so its pattern is never logged, the marker is (I-2);
- a static file (served before routing);
- a line written by middleware before routing has selected an endpoint (I-1);
- an endpoint without a route pattern.

**FR-004 — The raw path never appears.** No property added by the request scope
or by this Story carries the raw path or any part of it. The host does not move
the path into another property, the message text, or a new scope.

**FR-005 — The request identifier stays.** Every line written inside a request
keeps its request identifier exactly as today (DC-10); only `RequestPath`
changes. Synchronization lines keep their run identifier.

**FR-006 — The US-021 point fix is removed.** `RefusedValueRedaction` and its
registration in the installation's logging configuration are deleted; the
reference to it in `CourseCardLog` is updated. The host-wide rule now gives
US-021 FR-013 / S-05 its guarantee: the refused course id never reaches the log
file, and `CourseCardLoggingTests` pass unchanged (AC-004).

**FR-007 — Nothing else changes.** Messages, levels, level overrides, event ids,
event properties other than `RequestPath`, sinks, file names, rotation,
retention and size cap stay as they are (Story Out of Scope; DC-10).

**FR-008 — Re-execution names the error page.** When a request is re-executed to
the error page (`/error/{statusCode}`, `UseStatusCodePagesWithReExecute`,
`UseExceptionHandler`), a line written during the re-execution names the error
page's route, `/error/{statusCode}`; a line written in the original part of the
request names that part's route or the marker. Neither contains the original
path (I-3).

## 5. Acceptance Criteria

Story ACs are carried with their ids.

**AC-001 Installation: a matched request logs its route, not its path.** Given
the installation and a request to a page whose route has a path value (a Meet
code page with the code `zzq-path-probe`), when the request writes any log line,
then that line carries the route template of the page in `RequestPath` and the
request identifier, and `zzq-path-probe` appears nowhere in the log file.
— FR-001, FR-002, FR-004, FR-005.

**AC-002 Installation: an unmatched request logs no path.** Given the
installation, when a request to an address that nothing serves, containing a
distinctive value, writes a log line (signed in and anonymous), then the value
appears nowhere in the log file; the line carries the request identifier and
`RequestPath` = `(unmatched)`. — FR-001, FR-003, FR-004, FR-005, FR-008.

**AC-003 Control Plane: the same rule.** Given the Control Plane, when a matched
or an unmatched request writes a log line, then AC-001 and AC-002 hold there as
well. — FR-001 … FR-005.

**AC-004 The US-021 point fix is gone and its guarantee kept.** Given the change,
when the code is searched and the suite runs, then `RefusedValueRedaction` no
longer exists in `src/`, and `CourseCardLoggingTests` pass unchanged. — FR-006.

**AC-005 Nothing else changes.** Given the existing suite, when it runs after the
change, then it stays green; every line written inside a request still carries
the request identifier, every synchronization line its run identifier. —
FR-005, FR-007.

## 6. Validation Rules

No user input is added or accepted; nothing is validated by this Story.

Rules on the logged value (checked by tests):

- `RequestPath` on a line written inside a request is either a string starting
  with `/` that equals a route template declared in the host, or exactly
  `(unmatched)`.
- It never equals or contains the request's raw path when that path contains a
  route value or matches no route.
- It is never empty and never absent on a line written inside a request (I-4).

## 7. Security Requirements

- **SC-10 / DC-10 / §8 v89**: the change itself is the rule — only internal
  identifiers and code-defined text in logs; no value from an address.
- No other log content is widened: no new property carries a route value
  (`RouteValues`, `RouteData`, query string, headers). The query string stays
  unlogged (Story Out of Scope).
- No change to authentication, authorization, anonymous list (SC-4), audit
  (SC-11 — `AuditEvent` records no path), or outbound flows (SC-13).
- No new package: Serilog, `Serilog.AspNetCore` and ASP.NET Core routing are
  already referenced by both hosts (AGENTS.md — a package needs an Open
  Decision; none is expected).

## 8. Error Handling

- A failure while computing the route value for a line must not lose the line
  or break the request: the line is written with `RequestPath` =
  `(unmatched)` (fail closed — never fall back to the raw path).
- Lines written by the exception handler for an unhandled exception keep their
  level and content (DC-10 `Error`); only `RequestPath` follows FR-002 / FR-003.

## 9. Non-Functional Requirements

- Per-line overhead is a constant-time lookup of the current endpoint; no
  allocation proportional to the path, no I/O.
- Applies uniformly to framework and application lines; no per-event or
  per-event-id list (the weakness of the point fix, US-021 INFO-2).
- Tests follow TC-* and the existing log-test helpers (`HostLogs`,
  `WaitForLogEventAsync`); no live Google call (TC-4).

## 10. Out of Scope

- Content of existing log events (messages, levels, event ids).
- Query strings.
- The audit log (`AuditEvent`, SC-11).
- Log viewer, shipping, rotation or retention changes.
- A new log property for the route (OD-002 chose the existing name).
- Any endpoint, page, schema or translation change. API_DESIGN and DB_DESIGN are
  expected `NOT_APPLICABLE` (Story Notes).

## 11. Interpretations (for HUMAN_SPEC_APPROVAL)

- **I-1 Before routing.** A line written inside a request before the endpoint is
  selected (e.g. by middleware ahead of `UseRouting`) carries `(unmatched)`,
  even if the request later matches a page. "The route that served it" is
  unknown at that moment; the rule never waits for it and never logs the path.
- **I-2 The fallback is "unmatched".** Both hosts serve every unknown address by
  a catch-all fallback endpoint (`{*path}`). Technically that is a matched
  endpoint; the Story and §8 mean "an address no page serves", so its lines
  carry the marker, not `/{*path}`.
- **I-3 Error-page re-execution.** A line written while the error page renders
  names `/error/{statusCode}` — the page actually serving that part of the
  request. AC-002's "the agreed marker" is read as applying to lines written
  while no page is selected; a test for AC-002 asserts the marker on its line
  and the absence of the value on every line.
- **I-4 Always present.** Inside a request `RequestPath` is always present
  (template or marker), never dropped; the Owner's existing searches keep
  finding it (OD-002 rationale).
- **I-5 Template form.** Leading `/` added when the declared pattern lacks one;
  constraint text kept as declared rather than stripped — it is code and the
  Story's own example has none. If the Owner prefers names only
  (`{deanId}`), that is a one-line change of this FR before API_DESIGN.
- **I-6 Verifying AC-002.** No production code writes a line for an unmatched
  request at today's levels. TEST_WRITING may make the request write one through
  a test-registered logger/middleware in the test host; the assertion is on the
  file, the configuration under test is the production one.

## 12. Open Decisions

OD-001 … OD-004 are carried from the Story, all resolved by the Owner on
2026-10-11 (`docs/decisions/US-045-open-decisions.md`). OD-004 is already
fulfilled: the rule is `trebovaniya.md` v89 §8, and DC-10 restates it.

None new. `trebovaniya.md` §7 has no open item on logging.

## 13. Traceability

| AC | Functional requirements | Validation / interpretation |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-004, FR-005 | §6 rules 1–3; I-5 |
| AC-002 | FR-001, FR-003, FR-004, FR-005, FR-008 | §6 rules 1–3; I-1, I-2, I-3, I-6 |
| AC-003 | FR-001 … FR-005, FR-008 | §6; I-1 … I-6 |
| AC-004 | FR-006 | — |
| AC-005 | FR-005, FR-007 | I-4 |

## 14. Delegation note

No lookup was delegated: the Story, §8 v89, DC-10, SC-10 and the logging
configuration of both hosts were read directly (small, targeted reads).
