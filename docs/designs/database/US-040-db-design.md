---
artifact_type: database_design
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-05T05:55:23Z
updated_at: 2026-10-05T05:55:23Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/designs/api/US-040-api-design.md
    version: 1
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
supersedes: null
---

# US-040 Database Design — Responses are not cached

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour, so no
schema design and no entity model are produced. There is deliberately no
`docs/designs/database/US-040-entity-model.md`, as for US-007 and US-010.

## Why the stage does not apply

| Where | What it states |
|---|---|
| Spec §10 Out of Scope | "persistence (DB_DESIGN is expected `NOT_APPLICABLE`)" |
| Spec FR-005 | no change except the `Cache-Control` response header |
| Spec §8 | the rule writes no log and no audit event |
| API design §8 | "no persistence change" |

The rule is a response header set in the request pipeline of each host. It
reads and writes no table of either database (installation or Control Plane),
adds no column, constraint, index or migration (PC-2), and adds no audit
action code (SC-11).
