---
artifact_type: database_design
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T12:35:00Z
updated_at: 2026-10-14T12:35:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-047-spec.md
    version: 1
  - path: docs/designs/api/US-047-api-design.md
    version: 1
  - path: docs/decisions/US-047-open-decisions.md
    version: 1
supersedes: null
---

# US-047 Database Design — Run the Control Plane and the installation as Windows services

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour: no
entity, column, constraint, index, enum value, migration or query is added or
changed, in either database (installation or Control Plane).

**There is deliberately no `docs/designs/database/US-047-entity-model.md`, and
no `US-047-openapi.yaml` was produced upstream** (API design v1,
NOT_APPLICABLE). Their absence is the recorded decision of those stages, not a
missing artifact — as for US-043, US-045 and US-046.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it:

- **§1** — "No endpoint, request, response, status code, entity, table or
  migration changes." Only the start-up wiring of the two hosts and the
  resolution of relative directories from configuration change.
- **§10** — "Any endpoint, entity, migration, translation entry or UI change"
  is out of scope.
- **§12** — "DB_DESIGN is expected NOT_APPLICABLE (no persistence change)." The
  Story Notes say the same.

The API design (v1) is NOT_APPLICABLE as well: no DTO exists that a column
would have to back.

## 2. Consequences for later stages

- No EF Core migration ships with this Story (PC-2 has nothing to cover); both
  model snapshots must be unchanged by IMPLEMENTATION.
- Connection strings and database access are unchanged. Running as a service
  under a dedicated local account (OD-002 a) does not change how either host
  connects to PostgreSQL; the connection string still comes from configuration
  (DC-3).
- The Data Protection key directory (spec FR-004) is a file-system location,
  not database content; resolving it against the content root is not a
  persistence change. Neither the key ring nor the service-account key is ever
  stored in a database (Hard Stops, SC-7).
- Tests need no new table or seed data; integration tests that start a host
  keep using Testcontainers PostgreSQL as today (TC-2).

Delegation: none — the Specification and API design sections above were read
directly.
