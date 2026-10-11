---
id: US-045
epic: EPIC-6
title: Log lines name the route, never the raw request path
slug: route-template-in-logs
priority: LOW
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from US-021 security review INFO-1 and INFO-2;
# OD-001 … OD-004 resolved by the Owner on 2026-10-11. Aligned with
# trebovaniya.md v89.
---

# User Story

As the **Owner** of the service

I want every log line written inside a request to name the route that served
it (`/workspace/meet-codes/{meetingCode}/link`) instead of the raw path the
caller sent (`/workspace/meet-codes/abc-defg-hij/link`)

So that no value typed into an address reaches a log file, on any page, now or
in a later Story, and the one-off redaction added by US-021 can be removed.

---

# Business Value

`trebovaniya.md` §8 and §5 allow only internal identifiers in logs (SC-10,
DC-10). The installation's logging (`InstallationLogging`) takes every property
of the ASP.NET Core request log scope, and that scope carries `RequestPath` —
the path exactly as the caller sent it. So every log line written inside a
request records the raw path values:

- a Meet meeting code (`/workspace/meet-codes/{meetingCode}/…`);
- a report template reference (`/reports/templates/{templateRef}/…`);
- whatever a person types or pastes into an address that nothing serves —
  which can be anything, including a name.

No personal data is at risk today: the system's own links carry internal ids
only (US-021 S-06). But the rule "the value never reaches a log line" holds
only where a Story added a point fix. US-021 did exactly that for one event:
`RefusedValueRedaction` strips `RequestPath` from event 5463 only, keyed on the
numeric event id (US-021 security review INFO-1, INFO-2). Every new route with
a path value would need another such fix.

Replacing the raw path with the route template closes this for the whole host
at once: the template says *which* page was involved, which is all the Owner
needs to diagnose, and holds no value.

---

# Scope

**In scope:**

- the installation, both ports: inside a request, no log line carries the raw
  request path; a request that matched an endpoint carries its route template
  instead, in `RequestPath` (OD-002);
- a request that matched no endpoint (`404`, static files, middleware that
  answers before routing): no raw path either (OD-003);
- the Control Plane by the same rule (OD-001);
- removal of `RefusedValueRedaction` and its registration, replaced by the
  host-wide rule; `CourseCardLoggingTests` stay green;
- the request identifier stays on every line (DC-10) — only the path changes;
- tests per host: a request with a distinctive path value writes a log line,
  and that value appears nowhere in the log file, while the route template
  does.

**Out of scope:**

- the content of existing log events (messages, levels, event ids) — only the
  scope property changes;
- query strings: the request scope does not log them today, and this Story does
  not start;
- the audit log (`AuditEvent`, SC-11) — it is not a log file and records no
  path;
- a log viewer, log shipping or any change to rotation and retention (DC-10).

---

# Acceptance Criteria

## AC-001 Installation: a matched request logs its route, not its path

**Given** the installation and a request to a page whose route has a path value
(for example a Meet code page with the code `zzq-path-probe`)

**When** the request writes any log line

**Then** that line carries the route template of the page and the request
identifier, and the value `zzq-path-probe` appears nowhere in the log file.

## AC-002 Installation: an unmatched request logs no path

**Given** the installation

**When** a request to an address that nothing serves, containing a distinctive
value, writes a log line (signed in or anonymous)

**Then** the value appears nowhere in the log file; the line carries the
request identifier and the agreed marker for "no route" (OD-003).

## AC-003 Control Plane: the same rule

**Given** the Control Plane (OD-001)

**When** a matched or an unmatched request writes a log line

**Then** AC-001 and AC-002 hold there as well.

## AC-004 The US-021 point fix is gone and its guarantee kept

**Given** the change

**When** the code is searched and the test suite runs

**Then** `RefusedValueRedaction` no longer exists, and
`CourseCardLoggingTests` (the refused course id never reaches the log file)
pass unchanged.

## AC-005 Nothing else changes

**Given** the existing test suite

**When** it runs after the change

**Then** it stays green; every log line written inside a request still carries
the request identifier, and every synchronization line its run identifier
(DC-10).

---

# Open Decisions

## OD-001 The Control Plane too?

**Question.** The Control Plane's paths carry only installation and admin GUIDs
today — internal identifiers, allowed in logs. Apply the rule there now, or
leave the Control Plane as it is?

**Options.**

1. **Both hosts (Рекомендую).** One rule for both, as DC-10 already says the
   Control Plane "logs by the same rules"; a later Control Plane route with a
   typed value is covered in advance; the cost is one configuration line and
   its tests.
2. Installation only. Smaller; the Control Plane would need its own Story when
   a route with a value appears.

**Resolution (the Owner, 2026-10-11):** option 1 — both hosts, one rule.

## OD-002 What the route property is called

**Question.** How should the route appear in the log line?

**Options.**

1. **Replace `RequestPath` with the route template under the same name
   (Рекомендую).** Searches the Owner already uses keep working; the meaning of
   the field changes from "the path" to "the page".
2. Remove `RequestPath` and add a new property (for example `Route`). Clearer
   name, but a search for `RequestPath` silently finds nothing.

**Resolution (the Owner, 2026-10-11):** option 1 — `RequestPath` keeps its name and carries the route template.

## OD-003 A request that matched no route

**Question.** A request that no endpoint serves has no route template. What
does its log line carry instead of the path?

**Options.**

1. **A fixed marker, the same for every such request (for example
   `(unmatched)`) (Рекомендую).** The Owner still sees that a line came from an
   unknown address, without the address.
2. No path property at all. Simplest, but a line from an unmatched request
   cannot be told from one written before routing for another reason.

**Resolution (the Owner, 2026-10-11):** option 1 — a fixed marker, the same for every unmatched request; its exact text is set by the Specification.

## OD-004 Write the rule into the requirements

**Question.** `trebovaniya.md` §8 says "only internal identifiers" but does not
say what a request's log line names. Should the rule "a log line written in a
request names the route, never the raw path" be added to §8 (a new version, in
its own commit, before this Story is activated), or is the Specification
enough?

**Options.**

1. **Add one sentence to §8 in a new version (Рекомендую).** The rule then
   binds every later Story and the security review checks it; DC-10 and SC-10
   are updated from it.
2. Specification only. Faster; the rule lives in this Story and in the code,
   and a later Story could break it without any document saying so.

**Resolution (the Owner, 2026-10-11):** option 1 — one sentence in `trebovaniya.md` §8, in a new version and its own commit, before this Story is activated; DC-10 and SC-10 follow from it.

---

# Notes

- Origin: US-021 security review, INFO-1 (host-wide `RequestPath`) and INFO-2
  (redaction keyed on a numeric event id); the Owner chose a technical Story on
  2026-10-11.
- A conformance fix like US-041 (SC-10, DC-10), not a new feature — it does not
  widen the frozen pilot scope.
- Expected stages: API_DESIGN — no endpoint changes, likely NOT_APPLICABLE or a
  note; DB_DESIGN — NOT_APPLICABLE.
- No new NuGet package is expected: Serilog, `Serilog.AspNetCore` and the
  framework's routing already provide what is needed.
