---
artifact_type: database_design
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T13:18:08Z
updated_at: 2026-10-27T13:18:08Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/designs/api/US-060-api-design.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
supersedes: null
---

# US-060 Database Design — Marks in the course list, course card, journal, report and export

**Verdict: NOT_APPLICABLE.** The Story changes no persistence behaviour. No entity model is
produced: **there is deliberately no `docs/designs/database/US-060-entity-model.md`**.

Delegation: none. The designer checked the stored fields named below directly in
`src/ClassroomAgent.Domain/Entities` and the `SyncSkippedCourse` EF Core configuration.

## 1. Why the stage does not apply

`stage-map.yaml` marks `DB_DESIGN` as optional when the approved Specification states that the
Story does not change persistence behaviour. The approved Specification (v1) states it:

- **§1:** "Nothing is written, nothing is read from Google, nothing new is stored."
- **§9:** "Persistence: no schema change (OD-003 a); DB_DESIGN expected NOT_APPLICABLE."
- **OD-003 a)** (resolved by the Owner at HUMAN_SPEC_APPROVAL): a leaver is undated when
  `FirstSeenAt = LastSeenAt`. Option b), a new stored field, was not chosen.

## 2. What the Story reads, and where it already lives

| Spec element | Stored data (exists) | Source |
|---|---|---|
| FR-001, FR-003 course marks | `Course.AbsenceMark`, `Course.AbsentSince` | `Course.cs:96,99` (US-059) |
| FR-003 "not updated since" | `Course.LastCompleteReadAt` | `Course.cs:93` (US-058) |
| FR-003 skipped course | `SyncSkippedCourse.CourseGoogleId`, unique index `uq_sync_skipped_course_course_google_id` | `SyncSkippedCourse.cs:23`; configuration line 40 (US-058) |
| FR-002, FR-006 roster | `CourseMembership.OnRoster`, `FirstSeenAt`, `LastSeenAt`, `Role` | `CourseMembership.cs` (US-014, BR-051) |
| FR-004 above maximum | grade points and item maximum, already read by the journal and report | `JournalCell`, `ReportGrade` (§1.1 of the spec) |
| FR-005 item mark | `CourseWork.NotInClassroomSince` | `CourseWork.cs:72` (US-058) |

## 3. Query cost (spec §9)

- The header lookup of the course's skip record uses the existing unique index on
  `course_google_id`. That is one indexed probe per page or export.
- The course list reads `AbsenceMark` and `AbsentSince` from the `course` rows it already selects.
  The "no teacher" check reuses the teacher-membership query the list already runs
  (`CourseListSource.GetCurrentTeachersAsync`). There is no per-row query.
- The leaver mark reads membership columns that the journal and report already join.

No new index is needed.

## 4. Changes

- No migration, entity, column, constraint or index.
- No schema initialisation change (PC-2 unaffected).
- No new sensitive data. Raw points and the last-seen date already exist in the database and are
  now also written into the export, which is already personal data (spec §7).

## 5. Open findings

The OPEN US-060 findings are US-060/SPEC_REVIEW/m-1 and US-060/SPEC_REVIEW/m-3. They concern the
Specification and the digest, not persistence, so this stage closes neither.
