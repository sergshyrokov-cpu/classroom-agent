---
artifact_type: api_design
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T17:49:43Z
updated_at: 2026-10-11T17:49:43Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-045-spec.md
    version: 1
  - path: docs/decisions/US-045-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 89
supersedes: null
---

# US-045 API Design — Log lines name the route, never the raw request path

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-045-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — the same decision
US-007, US-013 and US-014 recorded. Downstream stages that list `openapi` among
their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "No endpoint, page, schema, permission or user-visible text changes."
- **§7** — no change to authentication, authorization, the anonymous list
  (SC-4), audit or outbound flows.
- **§10** — "Any endpoint, page, schema or translation change" is out of scope;
  API_DESIGN is expected `NOT_APPLICABLE`.

The Story Notes say the same ("API_DESIGN — no endpoint changes").

## 2. What the Story delivers, and why none of it is a contract

| Specification | Delivered change | Why it is not an API surface |
|---|---|---|
| FR-001 … FR-004, FR-008 | the value of the `RequestPath` property on log lines (route template or `(unmatched)`) | log file content (DC-10); never part of a response |
| FR-005 | request identifier kept | unchanged log property |
| FR-006 | `RefusedValueRedaction` removed | a Serilog enricher; no route, no status code |
| FR-007 | everything else unchanged | — |

Every route, method, status code, error body (API-6), header and policy of both
hosts stays as it is — including the catch-all fallback's `404` / `405` answers
(SC-4, US-028) and the `/error/{statusCode}` page, which the Specification only
names as sources of log lines.

## 3. Acceptance Criterion → operation map

| AC | Operation | Note |
|---|---|---|
| AC-001 … AC-005 | none | verified on the log file; tests send requests to existing routes (e.g. a Meet code page, an unknown address, a Control Plane installation page) without changing them |

## 4. Auth and error model

Unchanged. No operation is added, removed or re-authorized.

## 5. Open questions

None. OD-001 … OD-004 resolved; the Specification's interpretations I-1 … I-6
concern log content only.

## 6. Delegation

None; the Specification's own scope statements were sufficient.
