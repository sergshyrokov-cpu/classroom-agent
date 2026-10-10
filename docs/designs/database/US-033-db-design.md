---
artifact_type: database_design
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T19:50:00Z
updated_at: 2026-10-10T19:50:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/designs/api/US-033-api-design.md
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/decisions/US-033-open-decisions.md
    version: 1
supersedes: null
---

# US-033 Database Design — Built-in Meet reports for a course and period

Nothing was delegated. This stage read the entity configurations of
`meet_session`, `meet_participation`, `meeting_code_link`, `course_membership`
and `classroom_participant`, the latest migration (`AddMeetingCodeLinks`) and
the existing read ports (`IJournalSource`, `IMeetCodeScoringSource`) directly.

## 1. Summary

**No schema change and no migration.** The Story only reads (spec FR-015,
FR-017). Every query below is served by an index that already exists. Checking
that was the main job of this stage.

## 2. Tables read

| Table | Read for | Columns used |
|---|---|---|
| `meeting_code_link` | codes of the course (FR-004) | `meeting_code`, `course_id` |
| `meet_session` | meetings of the course and period; one meeting (FR-004, FR-010) | `id`, `meeting_code`, `organizer_email`, `started_at`, `ended_at` |
| `meet_participation` | connections of those meetings (FR-005, FR-006) | `meet_session_id`, `email`, `duration_seconds` |
| `course` | course name for report 4; existence (VR-001) | `id`, `name` |
| `course_membership` + `classroom_participant` | roster of the course on a date; students of the period (FR-005, FR-009) | `course_id`, `role`, `first_seen_at`, `last_seen_at`, `on_roster`; participant `id`, `email`, `full_name` |
| `course_membership` + `classroom_participant` | "personal account" check and names of the emails found (FR-005 rule 4, FR-011) | participant `email`, `full_name`; existence of any membership |
| `sync_state` | Meet watermark (FR-012), through the existing `GetLastSynchronizationQuery` | `meet_loaded_up_to` |

The course drop-down reuses the journal's Q1 (`IJournalSource.GetCoursesAsync`)
unchanged.

## 3. Queries and the indexes that serve them

Bounds are UTC instants; `[start, endExclusive)` is the spec FR-003 interval.

| # | Query | Index |
|---|---|---|
| R1 | meetings: `meet_session s JOIN meeting_code_link l ON l.meeting_code = s.meeting_code WHERE l.course_id = @course AND s.started_at >= @start AND s.started_at < @end` | `ix_meeting_code_link_course_id`, then `ix_meet_session_meeting_code_started_at` (code, start) |
| R2 | connections of R1's meetings: `meet_participation WHERE meet_session_id = ANY(@ids)` | `uq_meet_participation_meet_session_id_endpoint_id` (leading `meet_session_id`) |
| R3 | one meeting of the course: `meet_session s JOIN meeting_code_link l … WHERE s.id = @id AND l.course_id = @course`, with its connections | PK `meet_session`, then R2's index |
| R4 | memberships of the course with participant email and full name, any role | `uq_course_membership_course_participant` / `ix_course_membership_course_seen` (leading `course_id`) |
| R5 | for a set of lower-cased emails: participants with that `lower(email)`, their full name, and whether any `course_membership` exists for them | `ix_classroom_participant_email_lower` (US-032), then `ix_course_membership_participant_id` |
| R6 | course by id (name) | PK `course` |

A link row with `course_id` null (the "not a course" mark) never matches R1 or
R3, because they filter on a course id. This is how BR-083 holds without any
extra condition.

**Round-trips** are fixed per request (API design §3): the reports page runs
R6, R1, R2, R4, R5 and the watermark read; report 4 runs R3 (with R2), R6, R4
and R5. Nothing scales with the installation's size. R2 and R5 take bounded
sets (one course's meetings in the period; the distinct emails of those
connections). Like US-032's scoring, R5 batches its `ANY(@emails)` parameter
when the set is large. The batch size is an implementation constant, and the
Npgsql parameter limit is far above it.

## 4. Matching rules at the database boundary

- **Emails are compared case-insensitively** (PC-12). Participation and
  organizer emails are lower-cased in Application before R5, and R5 compares
  them to `lower(classroom_participant.email)`. R4 returns emails as stored,
  and Application lower-cases them before matching.
- **`classroom_participant.email` is not unique** (US-014 OD-011). For one
  lower-cased email, R5 can return several participants:
  - the email is a **personal account** when **any** of them has a membership;
  - the **displayed name** is the full name of the participant with the
    lowest id that has a non-null full name.

  The choice is deterministic and needs no schema change. It is recorded here
  so tests can rely on it.
- **A participant without an email** cannot match a connection. As a student
  of the period it still appears in report 3, with zeros (see the entity model
  §4 and the finding in §7).

## 5. Transactions, tracking, writes

All reads are `AsNoTracking`. None opens an explicit transaction, and none
writes. The reports page reads several tables without a snapshot. A
synchronization run committing between two reads can make one page combine
two consecutive states. This is accepted, as in the journal (US-025
db-design): the next load is consistent, and nothing is persisted from the
page.

## 6. Sensitive data

The reads return emails and full names of students and teachers (PC-9). They
travel only into the page DTOs and are never logged (SC-10): log lines carry
the course id and counts. Non-personal domain emails returned by R2 are used
only to count distinct "other participants" and are dropped before the DTO
(spec FR-010, I-11).

## 7. Findings for later stages

- **Participant without an email in report 3.** `classroom_participant.email`
  is nullable, but `PersonRef.email` in `US-033-openapi.yaml` is required. For
  a student with no email, the entity model makes the email nullable on the
  read record. The page then needs a fallback like the journal's
  (`Journal.Row.Unnamed` when there is neither a name nor an email). This is a
  one-line contract correction (`PersonRef.email` nullable, with a fallback
  label key). TEST_WRITING records it; it does not block DB_DESIGN.
- **Index reuse.** If a query plan on real data shows R1 doing more than an
  index scan per linked code, that is a performance follow-up, not a defect
  of this Story. The volume per course is small (spec §9).
