---
artifact_type: database_design
story: US-049
version: 1
status: DRAFT
created_at: 2026-10-16T13:00:00Z
updated_at: 2026-10-16T13:00:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-049-spec.md
    version: 1
  - path: docs/designs/api/US-049-api-design.md
    version: 1
  - path: docs/decisions/US-049-open-decisions.md
    version: 1
supersedes: null
---

# US-049 Database Design — Session-signing keys are encrypted at rest

**Verdict: NOT_APPLICABLE.** This Story changes no schema, so no entity model is
produced.

**There is deliberately no `docs/designs/database/US-049-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact. Downstream
stages that list `database_design` or `entity_model` among their inputs read
this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it in §13: "DB_DESIGN NOT_APPLICABLE (no schema
change)". The Data Protection keys the Story encrypts are files in each host's
key directory, outside both databases by rule (`trebovaniya.md` §8 v64, SC-7,
S-03): "a key ring kept … in a database" is forbidden, so the Story cannot touch
the schema.

## 2. What stays as it is

- No entity, table, column, index, constraint or migration in either database
  (installation or Control Plane); no change to the model snapshots.
- No audit event is added (SC-11 unaffected) — the Specification defines none.
- The key directory's location, validation and exclusion from backups (VR-004,
  DC-13) are configuration and deployment matters, not persistence.

## 3. Delegation

Nothing was delegated; the Specification and the US-050 precedent were read
directly.
