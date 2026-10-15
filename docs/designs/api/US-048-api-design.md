---
artifact_type: api_design
story: US-048
version: 1
status: DRAFT
created_at: 2026-10-15T19:10:00Z
updated_at: 2026-10-15T19:10:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-048-spec.md
    version: 1
  - path: docs/decisions/US-048-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-048 API Design — A Dean's failed-attempt count starts again when a lockout begins

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-048-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-007,
US-013, US-014, US-018, US-045, US-046 and US-047. Downstream stages that list
`openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "No endpoint, page, message, audit category, schema or migration
  changes."
- **§6** — "This Story adds no input, field or format."
- **§10** — "API_DESIGN: this Story does not change public API behaviour — no
  endpoint, route, status code, request or response body changes."

## 2. The one observable change, and why it is not a contract change

AC-005…AC-007 remove an unhandled `500` on the Dean sign-in page when two writes
to the same account collide. The page, its route, its form fields, its
antiforgery protection, its anonymous status (SC-4) and its responses are those
US-012 already specifies; the Story makes the page return the response US-012
and SC-2 already require (the common refusal, or the outcome of the current
account state) instead of the error page. No new status code, body or route is
introduced. A third consecutive conflict (FR-004, AC-008) still ends on the
existing error page (`trebovaniya.md` §8, SC-4).

## 3. Delegation

Nothing was delegated; the Specification and the US-047 precedent were read
directly.
