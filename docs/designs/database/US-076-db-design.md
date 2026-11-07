---
artifact_type: database_design
story: US-076
version: 2
status: DRAFT
created_at: 2026-11-07T07:21:36Z
updated_at: 2026-11-07T11:06:05Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: docs/designs/database/US-076-db-design.md@v1
---

# US-076 Database Design — A «?» beside the title opens short help on five pages

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema
design.

**There is deliberately no `docs/designs/database/US-076-entity-model.md`.** The missing file is this
stage's recorded decision, as in earlier Stories with no persistence change (US-072, US-073). Downstream
stages that list `entity_model` among their inputs read this document instead.

**Version 2** (re-run after the loop-back from `IMPLEMENTATION`, finding US-076/IMPLEMENTATION/B-1):
Specification v3 changed only AC-006, section 9 and Interpretation I-8 (which existing page tests may read
the page outside the help block), and API design v2 repeats v1. Neither touches persistence; the content
below is version 1 unchanged, re-checked against the current inputs.

Delegation: none. The designer read the Specification and the API design directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification states the Story does not
change persistence behaviour. Specification v3 (as v2) states it:

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
- `US-076/IMPLEMENTATION/B-1` — not this stage's; `TEST_WRITING` adjusts the affected existing tests (I-8).

No new finding.
