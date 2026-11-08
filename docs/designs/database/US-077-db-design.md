---
artifact_type: database_design
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T13:07:17Z
updated_at: 2026-11-08T13:07:17Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/designs/api/US-077-api-design.md
    version: 1
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
supersedes: null
---

# US-077 Database Design — Fewer Google token requests, and logs that explain failures, before the pilot

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema change and
no migration.

**There is deliberately no `docs/designs/database/US-077-entity-model.md`.** The missing file is this stage's recorded
decision, as in earlier Stories with no schema change (US-073, US-075, US-076). Downstream stages that list
`entity_model` among their inputs read this document instead.

Delegation: none. The designer read the Specification and `WorkspaceConnectionConfiguration.cs` directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification states the Story does not change
persistence behaviour. Specification v2 (approved at `HUMAN_SPEC_APPROVAL` 2026-11-08) states it:

- **Non-Functional Requirements:** "No new package, setting, schema, migration, endpoint, translation key, page or
  audit event."
- **Out of Scope:** "Any change to a user-visible text, screen, right, endpoint, schema or audit event."
- **FR-001, S-01:** the token lives only in process memory, "never in the database".

## 2. The one change near persistence

FR-008 adds an ordering by the primary key to the two reads in `WorkspaceConnectionRepository`. This is a query change
and not a schema change. The primary key is `Id` (`pk_workspace_connection`), and the index
`uq_workspace_connection_singleton` still allows one row, so the same row is returned. Tracking stays as it is:
untracked for display, tracked for update.

## 3. Sensitive data

No token, key or credential is written to the database (S-01, S-02).

## 4. Open findings

`US-077/SPEC_REVIEW/m-1` and `m-2` concern token behaviour inside the reader, not persistence. They stay OPEN for test
writing and implementation, as the Owner decided at approval. No new finding.
