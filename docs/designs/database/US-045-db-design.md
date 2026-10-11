---
artifact_type: database_design
story: US-045
version: 1
status: DRAFT
created_at: 2026-10-11T17:51:34Z
updated_at: 2026-10-11T17:51:34Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-045-spec.md
    version: 1
  - path: docs/designs/api/US-045-api-design.md
    version: 1
  - path: docs/decisions/US-045-open-decisions.md
    version: 1
supersedes: null
---

# US-045 Database Design — Log lines name the route, never the raw request path

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour, so no
schema design and no entity model are produced. There is deliberately no
`docs/designs/database/US-045-entity-model.md`, as for US-007, US-010, US-040
and US-041. There is no `US-045-openapi.yaml` either (API_DESIGN
NOT_APPLICABLE); the API design note stands in for it.

## Why the stage does not apply

| Where | What it states |
|---|---|
| Spec §1 | "No endpoint, page, schema, permission or user-visible text changes." |
| Spec §7 | the audit log (`AuditEvent`, SC-11) is untouched — it records no path |
| Spec §10 | "Any endpoint, page, schema or translation change" is out of scope; DB_DESIGN expected NOT_APPLICABLE |
| Story Notes | "DB_DESIGN — NOT_APPLICABLE" |

The change lives entirely in the Serilog configuration of both hosts: the value
of one log-scope property in the log file (DC-10). Log files are not a database
(PC-1); no table, column, index, migration or query changes in the installation
or the Control Plane database. Definition of Done item 5 (entity change ships
with its migration) is satisfied vacuously.

## Delegation

None; the Specification's scope statements were sufficient.
