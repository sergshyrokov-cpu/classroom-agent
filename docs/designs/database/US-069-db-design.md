---
artifact_type: database_design
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T11:51:50Z
updated_at: 2026-11-02T11:51:50Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/designs/api/US-069-api-design.md
    version: 1
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
supersedes: null
---

# US-069 Database Design - Small interface text fixes and a show-password control

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no
schema design.

**There is deliberately no `docs/designs/database/US-069-entity-model.md` and no migration.** The
missing file is this stage's recorded decision, not a missing artifact, as in earlier Stories with no
schema change (for example US-068). Downstream stages that list `database_design` or `entity_model`
among their inputs read this document instead.

Delegation: none. The designer read the Specification directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the Story
does not change persistence behaviour. The approved Specification (v3), Out of Scope, states it: this
Story "**does not change persistence behaviour** (no entity, schema or migration)". It also excludes
any change of what is stored or logged and adds no audit event.

## 2. What changes, and why none of it is persistence

Views, translation files, `site.css`, one script and two icons per host (FR-001 ... FR-008). The role
line reads the existing role; `/database` shows the same counters under new headings; the Meet reports
prompt keeps `value=""`; the control keeps the typed value in the browser and sends the same request.
Password storage, lockout and the Dean's language or account fields are untouched (SC-2).

## 3. Open findings

US-069/SPEC_REVIEW/m-4 concerns the wording of the Russian digest, not persistence; this stage leaves
it OPEN.
