---
artifact_type: database_design
story: US-068
version: 1
status: DRAFT
created_at: 2026-11-01T13:38:31Z
updated_at: 2026-11-01T13:38:31Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/designs/api/US-068-api-design.md
    version: 1
  - path: docs/decisions/US-068-open-decisions.md
    version: 1
supersedes: null
---

# US-068 Database Design — Both hosts share one restrained look built on Bootstrap

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-068-entity-model.md`**.

Delegation: none. The designer read the approved Specification and API design directly; no code
lookup was needed, since the Story touches no entity, configuration, migration or query.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v2) states it:

- **§1:** "No endpoint, schema, translation, policy or business rule changes; API and database
  design are expected to be NOT_APPLICABLE."
- **FR-009.2:** no route, controller, policy, form field, export file, audit event or business rule
  changes.
- **§6:** "No user input is added or changed."

The API design (v1) is NOT_APPLICABLE for the same reason.

## 2. What the Story stores

Nothing in either database. The new files (Bootstrap stylesheet, `LICENSE.txt`, `VERSION.txt`,
override SVGs) are static files in each host's `wwwroot`, part of the deployed application, not
data (FR-001, FR-007, SC-4). No table, column, index, constraint, migration or audit event is
added or changed in the installation or the Control Plane database (PC-2 has nothing to ship).

## 3. Open findings

US-068/SPEC_REVIEW/m-2 (danger colour only for suspension) is presentation, not persistence; this
stage leaves it OPEN.
