---
artifact_type: api_design
story: US-052
version: 1
status: DRAFT
created_at: 2026-10-16T13:54:24Z
updated_at: 2026-10-16T13:54:24Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-052 API Design — Every Admin-only and Dean-only write has a forbidden-role test

**Verdict: NOT_APPLICABLE.** This Story is test-only and changes no public API
behaviour, so no OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-052-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-049 and
earlier Stories without API changes. Downstream stages that list `openapi`
among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "This Story is **test-only**. … No file under `src/` changes."
- **FR-014** — "No file under `src/` changes. The Story adds test files under
  `tests/ClassroomAgent.Tests/` only."
- **§10** — "Any change to production code, policies or the §2 matrix" is out
  of scope.
- **I-7** — "API_DESIGN and DB_DESIGN NOT_APPLICABLE (no endpoint or schema
  change)."

## 2. What the tests touch, and why it is not a contract change

The tests exercise four existing Razor form endpoints
(`POST /settings/deans`, `POST /settings/deans/{deanId}/state`,
`POST /settings/deans/{deanId}/password`, `POST /account/password`) and read the
host's endpoint metadata. They assert today's behaviour — `403` with the
translated error page for the forbidden role, `302` for the allowed one (spec
§3, FR-003, FR-006, FR-008). No path, method, form field, status code, error
body or authorization policy is added or changed. None of the four is under
`/api/v1`.

## 3. Delegation

Nothing was delegated; the Specification and the US-049 precedent were read
directly.
