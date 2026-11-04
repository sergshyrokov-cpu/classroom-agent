---
artifact_type: database_design
story: US-073
version: 1
status: DRAFT
created_at: 2026-11-04T15:20:19Z
updated_at: 2026-11-04T15:20:19Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/designs/api/US-073-api-design.md
    version: 1
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
supersedes: null
---

# US-073 Database Design — The eye shows only a password the person entered

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema design.

**There is deliberately no `docs/designs/database/US-073-entity-model.md`.** The missing file is this stage's recorded decision, not a missing artifact, as in earlier Stories with no persistence change (for example US-072). Downstream stages that list `entity_model` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the Story does not change persistence behaviour. The approved Specification (v4, Overview) states it: "Only the host's script and its source tests change. No endpoint, schema, policy, form field, view text or translation entry changes."

## 2. What changes, and why none of it is persistence

- **The state of each field** (`EMPTY`, `ENTERED`, `FILLED`, FR-002) is kept in the browser's memory only. It is never stored or sent (FR-008.2).
- **Password storage** is out of scope and does not change (Specification, Out of Scope).
- **Entities, tables, constraints, migrations:** none added or changed, so Definition of Done item 5 has nothing to ship.

## 3. Open findings

US-073 has no OPEN findings.
