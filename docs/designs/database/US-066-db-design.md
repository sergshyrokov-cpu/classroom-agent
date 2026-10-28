---
artifact_type: database_design
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T18:35:38Z
updated_at: 2026-10-28T18:35:38Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
supersedes: null
---

# US-066 Database Design — Students with the same name are shown with their email

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-066-entity-model.md`**.

Delegation: none. The designer checked directly that both builders already read the email with the
member rows (`GetJournalQuery.cs:132`, `ReportBuilder.cs:159`).

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v1) states it:

- **§1.1:** "The member records both builders read already carry the email (`m.Email`). No new data
  is read."
- **§9:** "no new query, no change to the existing per-request cost".
- **§10:** out of scope — "Any new requirements version, NuGet package or database change."
- **FR-006 / §7:** the Story adds no write and no audit event.

The API design (D-1…D-3) adds only a view-model field computed in `Application` from data already
loaded.

## 2. What the Story reads, and where it already lives

| Spec element | Stored data (exists) | Read today by |
|---|---|---|
| Journal displayed name (FR-002.2) | participant full name, email | `GetJournalQuery` member rows |
| Report displayed name (FR-002.2) | participant surname, given name, email | `ReportBuilder` member rows |
| `namesakeEmail` (FR-003.1) | participant email | the same member rows |

No column, index, constraint, migration or query changes. The email is stored as today (personal
data under the existing SC rules); this Story neither logs nor audits it (spec §7).
