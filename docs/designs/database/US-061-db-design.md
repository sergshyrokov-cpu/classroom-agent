---
artifact_type: database_design
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:32:58Z
updated_at: 2026-10-31T14:32:58Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/designs/api/US-061-api-design.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
supersedes: null
---

# US-061 Database Design — Meet reports of a marked course, and a marked course is not a Meet link candidate

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-061-entity-model.md`**.

Delegation: none. The designer checked the stored fields in
`src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseConfiguration.cs` and the read
ports `IMeetCodeScoringSource`, `IMeetCodesReadSource` directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v1) states it:

- **§1:** "Nothing is read from Google, no schema changes, no endpoint is added."
- **§7:** "This Story adds no write and no guard call"; no new audit event.
- **§9:** "Persistence: no schema change; DB_DESIGN expected NOT_APPLICABLE."
- **FR-001.4, FR-004:** existing `meeting_code_link` rows are never changed by the system.

## 2. What the Story reads, and where it already lives

| Spec element | Stored data (exists) | Source |
|---|---|---|
| VR-001 "marked" | `Course.AbsenceMark` (`varchar(40)`, nullable, code `not_in_classroom` / `not_visible_to_technical_account`, US-059) | `CourseConfiguration.cs:57` |
| FR-003.2, FR-005.1 mark date | `Course.AbsentSince` (nullable, US-059) | `CourseConfiguration.cs:60` |
| FR-005.1 roster date | `Course.LastCompleteReadAt` (nullable, US-058) | `CourseConfiguration.cs:53` |
| FR-001, FR-002 candidate filter | the same `AbsenceMark` column | — |
| FR-003.3 manual link, FR-004 existing links | `meeting_code_link`, unchanged | US-032 design |

Projecting these existing columns into the Meet read ports (scoring source, codes read source,
reports source) is an implementation change to queries, not a schema change.

## 3. Query cost (spec §9)

The marked-course set is read once per scoring batch or per page (spec §9). The `course` table of
one installation holds hundreds of rows, and the existing queries already scan it without a filter
index; reading one more column needs no index. No N+1: the mark comes with the course row already
loaded on each path.

## 4. Changes

- No migration, entity, column, constraint or index.
- No schema initialisation change (PC-2 unaffected).
- No new sensitive data: the values shown are a course mark and two instants, none of them
  personal.

## 5. Open findings

US-061 has no OPEN finding; this stage closes none.
