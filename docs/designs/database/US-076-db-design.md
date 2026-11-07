---
artifact_type: database_design
story: US-076
version: 1
status: DRAFT
created_at: 2026-11-07T07:21:36Z
updated_at: 2026-11-07T07:21:36Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-076-spec.md
    version: 2
  - path: docs/designs/api/US-076-api-design.md
    version: 1
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: null
---

# US-076 Database Design — A «?» beside the title opens short help on five pages

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema
design.

**There is deliberately no `docs/designs/database/US-076-entity-model.md`.** The missing file is this
stage's recorded decision, as in earlier Stories with no persistence change (US-072, US-073). Downstream
stages that list `entity_model` among their inputs read this document instead.

Delegation: none. The designer read the Specification and the API design directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification states the Story does not
change persistence behaviour. Specification v2 states it:

- **FR-003:** "No cookie, no browser storage, no query parameter and no server-side state records whether
  the help was opened."
- **FR-008:** the help adds "no audit event, no log line, and no write".
- **Section 7:** no data stored client-side (NFR-072).

## 2. What changes, and why none of it is persistence

- **Help texts** live in the translation files `SharedResource.uk.resx` / `.en.resx` (API design section 3),
  not in the database.
- **The page id** is a view-data value set in code (VR-001); it is never stored.
- **Entities, tables, constraints, migrations:** none added or changed, so Definition of Done item 5 has
  nothing to ship.

## 3. Open findings

- `US-076/SPECIFICATION/m-2` — not this stage's; deferred by the Owner to `trebovaniya.md` v113.

No new finding.
