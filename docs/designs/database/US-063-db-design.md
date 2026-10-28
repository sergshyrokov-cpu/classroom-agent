---
artifact_type: database_design
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:04:53Z
updated_at: 2026-10-28T17:04:53Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/designs/api/US-063-api-design.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
supersedes: null
---

# US-063 Database Design — «Сформовано» and «Дані з Google на» in the journal, the report and the Excel export

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-063-entity-model.md`**.

Delegation: none. The designer checked the stored field and the sources that read it directly in
`src/ClassroomAgent.Domain/Entities/Course.cs` and `src/ClassroomAgent.Infrastructure/Persistence/Repositories`.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v1) states it:

- **§1:** "Nothing is written, nothing is read from Google, nothing new is stored."
- **§7:** "This Story adds no write"; the export's existing audit row stays the only write.
- **§9:** "Persistence: no schema change; DB_DESIGN expected NOT_APPLICABLE."
- **OD-002 a)** (resolved by the Owner): "data as of" is the existing `Course.LastCompleteReadAt`,
  not a new or combined field.

## 2. What the Story reads, and where it already lives

| Spec element | Stored data (exists) | Source |
|---|---|---|
| FR-001 "generated" | none — the instant B comes from `TimeProvider`, read once per build | `GetJournalQuery.cs:31`, `ReportBuilder.cs:80` |
| FR-002 "data from Google as of" | `Course.LastCompleteReadAt` (`DateTimeOffset?`, US-058) | `Course.cs:93` |
| FR-002 on the journal page | already projected by `JournalSource` | `JournalSource.cs:29` |
| FR-002 on the report page and export | already projected by `JournalFieldSource` (used by `ReportBuilder`) | `JournalFieldSource.cs:28` |

## 3. Query cost (spec §9)

`LastCompleteReadAt` is already selected with the course row on both paths; the Story adds no
query and no index.

## 4. Changes

- No migration, entity, column, constraint or index.
- No schema initialisation change (PC-2 unaffected).
- No new sensitive data: the two values are a clock instant and a sync time, neither personal.

## 5. Open findings

The OPEN US-063 findings are US-063/SPEC_REVIEW/m-1 and US-063/SPEC_REVIEW/m-2. They concern the
Specification and the digest, not persistence, so this stage closes neither.
