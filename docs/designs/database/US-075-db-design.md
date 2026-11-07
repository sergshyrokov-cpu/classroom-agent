---
artifact_type: database_design
story: US-075
version: 1
status: DRAFT
created_at: 2026-11-07T18:48:47Z
updated_at: 2026-11-07T18:48:47Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
supersedes: null
---

# US-075 Database Design — A short hint under each kind of data problem, and banner links to each section

**Verdict: NOT_APPLICABLE.** This Story changes no persistence behaviour, so this stage produces no schema
design.

**There is deliberately no `docs/designs/database/US-075-entity-model.md`.** The missing file is this stage's
recorded decision, as in earlier Stories with no persistence change (US-072, US-073, US-076). Downstream stages
that list `entity_model` among their inputs read this document instead.

Delegation: none. The designer read the Specification, the API design and `Views/DataHealth/Index.cshtml`
directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` optional when the approved Specification states the Story does not change
persistence behaviour. Specification v2 states it:

- **Overview:** "Nothing is computed anew: the counts, the kinds, who sees which problem, and the page's data
  stay as they are. API design and database design are expected to be empty deltas — no endpoint, schema, DTO
  field or audit event is added."
- **SEC-005:** "the page reads stored data only … No audit event is added."
- **Out of Scope:** "Any new data, right, endpoint, DTO field, schema change or audit event."

API design v1 is `NOT_APPLICABLE` and adds no DTO field.

## 2. What the Story reads, and why it needs nothing new

Every hint is chosen from facts the page already has (Specification VR-002): the problem's kind
(`TechnicalProblemKind` and the teaching kinds of US-062), the viewer's role (`IsAdmin` on the page model),
the section (current or archived, already separate in the view) and the read-only state (`ReadOnly` on the
page model, already rendered as the notice). The hint texts and the Dean's X4 title are translation entries
(`SharedResource.*.resx`), not data. The banner counts are unchanged.

- No table, column, constraint, index or relationship is added or changed.
- No migration (PC-2 does not apply: no entity change).
- No new sensitive data; no audit row (SC-11 unchanged).

## 3. Open findings

The Story has no OPEN finding. No new finding.
