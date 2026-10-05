---
artifact_type: database_design
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:28:45Z
updated_at: 2026-10-05T07:28:45Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/designs/api/US-041-api-design.md
    version: 1
  - path: docs/designs/api/US-041-openapi.yaml
    version: 1
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
supersedes: null
---

# US-041 Database Design — An unknown file-like address answers 404

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour, so no
schema design and no entity model are produced. There is deliberately no
`docs/designs/database/US-041-entity-model.md`, as for US-007, US-010 and
US-040.

## Why the stage does not apply

| Where | What it states |
|---|---|
| Spec §9 | "no database change (DB_DESIGN is expected NOT_APPLICABLE)" |
| Spec FR-003 | the catch-all "reads and writes nothing" |
| Spec §7 | no audit event — answering `404` is not an SC-11 action |
| API design §8 | "DB_DESIGN is expected NOT_APPLICABLE (no entity, no migration)" |

The change is the route pattern of one anonymous catch-all per host. It reads
and writes no table of either database (installation or Control Plane), adds no
column, constraint, index or migration (PC-2), and adds no audit action code
(SC-11).
