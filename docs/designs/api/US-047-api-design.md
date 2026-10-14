---
artifact_type: api_design
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T12:27:00Z
updated_at: 2026-10-14T12:27:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-047-spec.md
    version: 1
  - path: docs/decisions/US-047-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 92
supersedes: null
---

# US-047 API Design — Run the Control Plane and the installation as Windows services

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-047-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-007,
US-013, US-014, US-018, US-045 and US-046. Downstream stages that list `openapi`
among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "No endpoint, request, response, status code, entity, table or
  migration changes." The Story changes only the start-up wiring of the two
  host projects and how they resolve a relative directory from configuration.
- **§6** — "No new user input, request body, query or route parameter.
  Configuration input only."
- **§7** — network exposure unchanged: "the same endpoints, addresses and ports
  as from the console"; authorization, audit and read-only mode unaffected.
- **§10** — "Any endpoint, entity, migration, translation entry or UI change" is
  out of scope.
- **§12** — "API_DESIGN is expected NOT_APPLICABLE (no endpoint, request or
  status code change)." The Story Notes say the same.

## 2. What the Story delivers, and why none of it is a contract

| Specification | Delivered change | Why it is not an API surface |
|---|---|---|
| FR-001, FR-002 | service detection and Windows service lifetime in both hosts | process lifetime, not HTTP |
| FR-003, FR-004, VR-001 | content root and relative-directory resolution | configuration handling at start-up |
| FR-005 | stop = ordinary host shutdown | no new code path, no endpoint |
| FR-006 | setup code stays on standard output | the setup page and its form are unchanged (US-001) |
| FR-007 | logs only to the DC-10 file | logging destination |
| FR-008 | one package reference in the two hosts | build |
| FR-009 | manual verification | `implementation_report` |

The endpoints AC-001 and AC-002 name ("serves its public endpoint and its
private port", "serves its HTTPS endpoint") are the existing ones, served
unchanged as a service; their contracts stay in the Stories that defined them.

## 3. Compatibility

No existing contract changes: no route, method, request or response schema,
status code, error body, authorization policy or anonymous-access entry (SC-4).

## 4. Open questions

None. OD-001 a) and OD-002 a) are resolved and do not touch the API.

Delegation: none — the Specification sections above were read directly.
