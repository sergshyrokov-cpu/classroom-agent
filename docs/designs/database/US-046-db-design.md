---
artifact_type: database_design
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T17:47:56Z
updated_at: 2026-10-13T17:47:56Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
supersedes: null
---

# US-046 Database Design — Page-search hint on teaching-data pages

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour: no
entity, column, constraint, index, migration or query is added or changed.

**There is deliberately no `docs/designs/database/US-046-entity-model.md`.**
Its absence is this stage's recorded decision, not a missing artifact — as for
US-045.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it:

- **§1** — "no endpoint, request parameter, status code, use case, port,
  entity, table or audit event is added or changed."
- **FR-007** — showing the hint "sets no cookie, writes no setting, account
  field or database row, writes no audit event".
- **§9** — "no extra query, no extra round trip per page".

The API design (v1, NOT_APPLICABLE) adds only a markup element; the Story Notes
expect DB_DESIGN to be NOT_APPLICABLE.

## 2. Consequences for later stages

- No EF Core migration ships with this Story (PC-2 has nothing to cover); the
  model snapshot must be unchanged by IMPLEMENTATION.
- The hint's language follows the user's existing stored language choice
  (NFR-073); that read already happens for every page and is not new.
- No test needs a new table or seed data beyond what the FR-001 pages already
  use.
