---
artifact_type: database_design
story: US-052
version: 1
status: DRAFT
created_at: 2026-10-16T13:56:17Z
updated_at: 2026-10-16T13:56:17Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-052-spec.md
    version: 1
  - path: docs/designs/api/US-052-api-design.md
    version: 1
  - path: docs/decisions/US-052-open-decisions.md
    version: 1
supersedes: null
---

# US-052 Database Design — Every Admin-only and Dean-only write has a forbidden-role test

**Verdict: NOT_APPLICABLE.** This Story is test-only and changes no schema, so no
entity model is produced.

**There is deliberately no `docs/designs/database/US-052-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact. Downstream
stages that list `database_design` or `entity_model` among their inputs read
this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it:

- **FR-014** — "No file under `src/` changes."
- **I-7** — "API_DESIGN and DB_DESIGN NOT_APPLICABLE (no endpoint or schema
  change)."

No entity, configuration, migration or model snapshot changes (PC-2 needs no
migration).

## 2. What the tests read, and why it is not a persistence change

The tests read existing tables to prove that refused requests had no effect and
allowed ones did (FR-004, FR-006, FR-008): the Dean's `AppUser` / Identity row
(enabled state, password hash, failed-attempt count, lockout end, security stamp)
and the `AuditEvent` row count. They run against PostgreSQL via Testcontainers
(TC-2) with synthetic fixtures; nothing they read or write is new.

## 3. Delegation

Nothing was delegated; the Specification and the US-049 precedent were read
directly.
