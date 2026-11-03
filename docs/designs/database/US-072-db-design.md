---
artifact_type: database_design
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:33:12Z
updated_at: 2026-11-03T20:33:12Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/designs/api/US-072-api-design.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
    version: 1
supersedes: null
---

# US-072 Database Design - Control Plane error page without the language switcher

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema design.

**There is deliberately no `docs/designs/database/US-072-entity-model.md` and no migration.** The missing file is this stage's recorded decision, not a missing artifact, as in earlier Stories with no schema change (for example US-069). Downstream stages that list `database_design` or `entity_model` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the Story does not change persistence behaviour. The approved Specification (v1, section 13) states it: "It does not change persistence behaviour: no entity, schema, migration or read changes."

## 2. What changes, and why none of it is persistence

The switcher's rule and its comment in the Control Plane's presentation code (FR-001, FR-002, FR-005), and tests. The error page reads and writes nothing, and the Owner's stored language is read as before (FR-003).

## 3. Open findings

US-072 has no OPEN findings.
