---
artifact_type: database_design
story: US-054
version: 1
status: DRAFT
created_at: 2026-10-24T20:35:00Z
updated_at: 2026-10-24T20:35:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/designs/api/US-054-api-design.md
    version: 1
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
supersedes: null
---

# US-054 Database Design — The program's texts describe the technical account as a super-admin with blocked sign-in

**Verdict: NOT_APPLICABLE.** This Story changes no schema and no persistence
behaviour, so no entity model is produced.

**There is deliberately no `docs/designs/database/US-054-entity-model.md`.** Its
absence is this stage's recorded decision, not a missing artifact — as for
US-048, US-052 and US-053. Downstream stages that list `database_design` or
`entity_model` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification
states the Story does not change persistence behaviour. The approved
Specification (v1) states it:

- **§1** — "This Story changes **texts only**: translation entries in both
  language files, the list of technical-account statements the instruction
  renders, and the comments and tests that pin the old meaning. No endpoint,
  schema, … changes."
- **§10** — "No new requirements version, NuGet package, endpoint, schema or
  migration."

The API design (v1) is NOT_APPLICABLE for the same reason.

## 2. What stays as it is

- **Translations are resource files**, not database rows:
  `SharedResource.uk.resx` / `.en.resx` in `Application/Localization`. Adding,
  removing or rewording a key touches no table.
- **`WorkspaceConnection`** — its columns (domain, impersonation user) and the
  domain-match invariant are unchanged; only the hint text shown beside the field
  changes (FR-003). The impersonation user's role is not stored and is not
  checked by the program (spec §7, §10).
- **Access-check outcomes and `SyncState`** — the outcome / `SyncDiagnosis`
  values stored by name (`LastError`, `FailedStep`) are unchanged; FR-004 changes
  only the text the stored name maps to at render time. No stored value needs a
  data fix.
- **Audit** — no event added or changed (SC-11).
- No migration (PC-2 is satisfied vacuously: no entity change).

## 3. Delegation

Nothing was delegated; the approved Specification, API design and the US-053
precedent were read directly.
