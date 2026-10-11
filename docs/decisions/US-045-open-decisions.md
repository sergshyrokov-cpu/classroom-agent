---
artifact_type: open_decisions
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T17:46:30Z
updated_at: 2026-10-11T17:46:30Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-045-route-template-in-logs.md
    version: null
  - path: trebovaniya.md
    version: 89
supersedes: null
---

# US-045 Open Decisions — Log lines name the route, never the raw request path

**Four, all resolved** by the Owner on 2026-10-11, before activation. Writing
the Specification raised none; `trebovaniya.md` §7 has no open item on logging.
Choices the wording leaves open are interpretations I-1 … I-6 in
`docs/specifications/US-045-spec.md` §11.

## OD-001 The Control Plane too?

**Resolved**: option 1 — both hosts, one rule.
Impact: FR-001, AC-003.

## OD-002 What the route property is called

**Resolved**: option 1 — `RequestPath` keeps its name and carries the route
template.
Impact: FR-002, I-4.

## OD-003 A request that matched no route

**Resolved**: option 1 — a fixed marker, the same for every unmatched request;
its exact text is set by the Specification.
Impact: FR-003 sets the marker to `(unmatched)`; AC-002; I-1, I-2.

## OD-004 Write the rule into the requirements

**Resolved**: option 1 — one sentence in `trebovaniya.md` §8, in a new version
and its own commit, before activation; DC-10 and SC-10 follow.
Impact: fulfilled by `trebovaniya.md` v89 (`2422ee4`); DC-10 restates the rule.
SC-10 points to DC-10 for logging and was not reworded (non-blocking finding).
