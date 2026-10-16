---
artifact_type: api_design
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T07:58:40Z
updated_at: 2026-10-16T07:58:40Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-050 API Design — Every response carries the browser security headers

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-050-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-007,
US-013, US-014, US-018, US-045, US-046, US-047, US-048 and US-051. Downstream
stages that list `openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "The Story changes no endpoint, route, status code, response body,
  entity, table, translation entry, audit event or log event."
- **§6** — "No external input is added or changed: the rule reads nothing from
  the request."
- **§7 SC-4** — "no anonymous access changes — the rule adds headers, never
  content or routes."
- **§12** — "API_DESIGN NOT_APPLICABLE (no endpoint, route, status code or body
  changes; headers only)."

## 2. The one observable change, and why it is not a contract change

FR-002 / FR-003 / FR-004: every `/api/v1` response gains four response headers
(`X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy`,
`Referrer-Policy`). They are set by one host-wide middleware for every response
of the host (SC-15), exactly as `Cache-Control: no-store` is (SC-14, US-040,
whose contracts did not list that header either). They are cross-cutting
transport policy, not part of any operation: no operation's path, method,
request schema, response schema, status code, error body or authorization
changes, and an API client (the installation's own export scripts) needs
nothing new. Their exact values are fixed by Specification FR-003 / FR-004 and
tested there (VR-001, VR-004), not here.

## 3. Delegation

Nothing was delegated; the Specification and the US-051 precedent were read
directly.
