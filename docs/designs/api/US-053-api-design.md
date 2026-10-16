---
artifact_type: api_design
story: US-053
version: 1
status: DRAFT
created_at: 2026-10-16T16:06:06Z
updated_at: 2026-10-16T16:06:06Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-053-spec.md
    version: 1
  - path: docs/decisions/US-053-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-053 API Design — A failed synchronization run is always recorded as failed

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-053-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052 and
earlier Stories without API changes. Downstream stages that list `openapi`
among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "It changes no requirement, no endpoint, no schema and no
  user-visible text (OD-001 a)."
- **§7** — "No endpoint, page, policy or role changes; the 'Last
  synchronization' block stays on the Admin-only connection page (US-017
  FR-008, SC-4)."
- **FR-006** — the "Last synchronization" block is unchanged; it only reads the
  row the run now stores correctly.

## 2. What the Story touches, and why it is not a contract change

The change is inside the background synchronization run
(`RunSynchronizationUseCase` failure path) and the `IUnitOfWork` port — neither
is reachable over HTTP. The connection page (`GET`, Razor, Admin-only) renders
the existing `LastSynchronizationView` DTO through the existing
`GetLastSynchronizationQuery`; no field, route, status code, error body or
policy is added or changed (FR-006, I-6). Nothing is under `/api/v1`.

## 3. Delegation

Nothing was delegated; the Specification and the US-052 precedent were read
directly.
