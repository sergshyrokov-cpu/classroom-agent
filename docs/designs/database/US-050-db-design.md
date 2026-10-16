---
artifact_type: database_design
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T08:01:15Z
updated_at: 2026-10-16T08:01:15Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/designs/api/US-050-api-design.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
supersedes: null
---

# US-050 Database Design — Every response carries the browser security headers

**Verdict: NOT_APPLICABLE.** This Story changes no schema, so no entity model is
produced.

**There is deliberately no `docs/designs/database/US-050-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact. Downstream
stages that list `database_design` or `entity_model` among their inputs read
this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it in §1: "The Story changes no endpoint, route,
status code, response body, entity, table, translation entry, audit event or
log event", and §12: "DB_DESIGN NOT_APPLICABLE (no schema change)". §9 adds:
no audit events (SC-11 unaffected) and no log events.

## 2. What stays as it is

- No entity, table, column, index, constraint or migration in either database
  (installation or Control Plane); PC-2 is satisfied vacuously.
- The new middleware writes constant response-header values and reads and
  writes nothing persistent.

## 3. Delegation

Nothing was delegated; the Specification, the API design and the US-048
precedent were read directly.
