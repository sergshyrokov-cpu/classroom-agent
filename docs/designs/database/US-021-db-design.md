---
artifact_type: database_design
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:12:00Z
updated_at: 2026-10-11T16:12:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/designs/api/US-021-api-design.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/decisions/US-021-open-decisions.md
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
supersedes: null
---

# US-021 Database Design — Course detail with roster

Companion: `docs/designs/database/US-021-entity-model.md`.

Delegated: nothing to subagents. `CourseMembershipConfiguration`,
`ClassroomParticipantConfiguration`, the migration `AddCourseListSupport`,
`CourseListSource` and `GetCourseListQuery` were read directly; the entity
model's C# skeleton was compiled against `ClassroomAgent.Application` (entity
model §5).

## 1. Summary

**No schema change and no migration.** No table, column, constraint, index,
collation or audit action is added or changed. The card reads three existing
tables — `course`, `course_membership`, `classroom_participant` — in **two**
untracked queries, and every predicate is already served by an existing index.
DoD item 5 (migration with every entity change) is satisfied vacuously: no
entity changes.

## 2. Queries

Both run through a new read port `ICourseCardSource` (entity model §2),
implemented in `Infrastructure` with EF Core, `AsNoTracking`, one command each,
no transaction, nothing written, nothing logged (SC-10).

### Q1 — the course

```sql
SELECT id, name, section, state, room, description_heading, description,
       creation_time, update_time
FROM course
WHERE id = @courseId;
```

- Served by the primary key `pk_course`.
- `alternate_link`, `teacher_folder_*`, `calendar_id`, `google_id`,
  `owner_google_id` are **not selected** (spec FR-002, S-10): they never leave
  the database for this page.
- No row → `CourseUnknown` (spec FR-011); Q2 is not run.

### Q2 — the memberships with their participants

```sql
SELECT p.id, p.surname, p.given_name, p.email,
       m.role, m.on_roster, m.last_seen_at,
       (p.google_user_id = c.owner_google_id) AS is_owner   -- NULL owner → false
FROM course_membership m
JOIN classroom_participant p ON p.id = m.participant_id
JOIN course c               ON c.id = m.course_id
WHERE m.course_id = @courseId
ORDER BY <label is null>, <label> COLLATE course_name_uk,
         <email is null> DESC, p.email COLLATE course_name_uk, p.id;
```

- **Every** membership of the course in one round trip; `Application` splits
  them into the three sections (spec FR-003) keeping the order. The number of
  round trips is 2 whatever the roster size (spec FR-015, AC-017).
- `<label>` is the US-020 label expression, verbatim (`CourseListSource`):
  `surname || ' ' || given_name` when both are non-empty, else the non-empty
  one, else `email` when non-empty, else `NULL`. It sorts; it is not returned —
  the name is rebuilt in `Application` by the shared rule (entity model §4).
- **Order** (spec FR-007): named rows first (`label IS NULL` last, spec I-3),
  then the label under the ICU collation `course_name_uk` (created by US-020's
  `AddCourseListSupport`; Ukrainian letters in Ukrainian order, Latin in Latin
  order, Cyrillic before Latin — US-020 db-design §2), then the email with a
  row **without** email first (spec FR-007), the email under the same
  collation, then `p.id`. The collation is deterministic, so the order is total
  and stable.
- **Owner** (spec FR-004): `is_owner` is computed in SQL so neither
  `google_user_id` nor `owner_google_id` leaves `Infrastructure`. PostgreSQL
  `=` on `text` under the database's default deterministic collation is a
  byte-wise, i.e. ordinal, comparison. `owner_google_id` `NULL` makes the
  comparison `NULL`, mapped to `false` (EF Core: `c.OwnerGoogleId != null &&
  p.GoogleUserId == c.OwnerGoogleId`). `google_user_id` is unique
  (`uq_classroom_participant_google_user_id`), so at most one row is `true`.
  `Application` uses `is_owner` only on rows that land in "Teachers"; an owner
  in "Students" or "Left the course" is not marked (spec FR-004, I-7).
- `full_name`, `google_user_id`, `first_seen_at` are not selected.

## 3. Indexes

| Access | Index | Verdict |
|---|---|---|
| Q1 `course.id = ?` | `pk_course` | serves |
| Q2 `course_membership.course_id = ?` | `uq_course_membership_course_participant (course_id, participant_id)` — leading column `course_id`; also `ix_course_membership_course_seen (course_id, first_seen_at, last_seen_at)` | serves; the planner may use either |
| Q2 join to participant | `pk_classroom_participant` | serves |
| Q2 join to course | `pk_course` (one row) | serves |

No new index (PC-7, NFR-003). Sizing: a course holds at most a few hundred
current members (Classroom caps a course's teachers and students) and, after
several years, a few hundred leavers until the retention purge removes them
(§5, PC-11) — an index range scan of a few hundred rows and an in-memory sort
of them under ICU. A covering index for the sort is not worthwhile: the sort key
is an expression over the participant's columns, not a membership column.

## 4. Sections from the stored data

| Section | `role` | `on_roster` |
|---|---|---|
| Teachers | `teacher` | `true` |
| Students | `student` | `true` |
| Left the course | either | `false` |

`ck_course_membership_role` admits only the two roles and `on_roster` is
`NOT NULL`, so every row lands in exactly one section. A student created from
submissions (US-015 `ResolveSubmitterAsync`) is `student`, `on_roster = false`,
`first_seen_at = last_seen_at` = that synchronization's instant, and a
participant with `surname`, `given_name` and `email` all `NULL` — label `NULL`,
sorted last, shown "no name" (spec FR-005, FR-006, AC-004).

## 5. Time values

`course.creation_time`, `course.update_time` and
`course_membership.last_seen_at` are `timestamptz` (UTC instants). They are read
as `DateTimeOffset` and converted to the school's time zone in `Application`
(`SchoolTimeZone`, NFR-074): course times to a local `DateTime` truncated to the
minute (as US-020), the leaver's last seen to a local `DateOnly` (spec FR-006,
I-5). The database does no time-zone conversion.

## 6. Read-only mode, security, sensitive data

- Both queries are reads; nothing is written in any mode (spec FR-014). No
  `SaveChanges`, no tracking.
- Personal data read: surname, given name, email of participants of **one**
  course — shown to Admin and Dean only (§2), never logged (spec FR-013).
- No secret, no credential, no Google link leaves the database for this page.
- No raw SQL: both queries are LINQ; `@courseId` is a bound parameter.

## 7. Schema initialisation and deployment

Nothing to apply. The card depends on the collation `course_name_uk` that
US-020's migration already creates; the deployment requirement recorded by
US-020 (PostgreSQL built with ICU) is unchanged.

## 8. Tests this design implies (for TEST_WRITING)

- Integration (Testcontainers PostgreSQL, TC-2): Q2 returns every membership of
  one course and none of another; the order of spec AC-015; email tie-break;
  nameless last; `is_owner` true only for the matching Google id, false when
  `owner_google_id` is `NULL`; a submitter-only student comes back off-roster
  with its `last_seen_at`.
- Bounded reads (spec AC-017): the number of commands is the same for a course
  with 3 members and one with 300 members and 50 leavers (2), and 1 for an
  unknown id.
