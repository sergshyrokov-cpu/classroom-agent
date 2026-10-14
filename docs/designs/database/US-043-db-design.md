---
artifact_type: database_design
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-14T06:04:18Z
updated_at: 2026-10-14T06:04:18Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/designs/api/US-043-api-design.md
    version: 1
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
supersedes: null
---

# US-043 Database Design — Fill the group-lessons blank form with a template report

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour: no
entity, column, constraint, index, enum value, migration or query is added or
changed.

**There is deliberately no `docs/designs/database/US-043-entity-model.md`.**
Its absence is this stage's recorded decision, not a missing artifact — as for
US-045 and US-046.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v2) states it:

- **§1** — "It adds no Google call, no role or matrix cell, no stored setting,
  no new audit action and no schema change."
- **FR-001** — the form is an embedded resource, "never stored in the
  database".
- **FR-004** — "Nothing of the choice is stored — not in the template, not on
  the account, not in the session" (OD-007 a).
- **FR-010, I-11** — the export audit row is US-028's `JournalExported` with
  format `xlsx` for both layouts; "keeping it needs no new enum value and no
  migration".
- **FR-005, §9** — the content comes from the existing report query, unchanged
  (US-027 FR-004).

The API design (v1, §8) expects DB_DESIGN to be NOT_APPLICABLE: the new request
members are request-only and the view DTO member `blankForm` is mapped from the
form's capacities in code, not from data.

## 2. Consequences for later stages

- No EF Core migration ships with this Story (PC-2 has nothing to cover); the
  model snapshot must be unchanged by IMPLEMENTATION. `ExportFormat` keeps its
  single value `Xlsx` (stored `xlsx`).
- The shipped form `docs/product/report-templates/group-lessons-blank.xlsx` is
  a versioned file built into `ClassroomAgent.Infrastructure` as an embedded
  resource (spec FR-001, OD-009 a) — not database content, not covered by the
  database backups (DC-13), and holds no personal data.
- Tests need only the data US-028's export tests already build (courses,
  memberships, participants, coursework, submissions, templates); the
  many-sheet cases (AC-002, AC-014) need more synthetic students and items, no
  new table.
