---
artifact_type: database_design
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T09:10:48Z
updated_at: 2026-10-12T09:10:48Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/designs/api/US-022-api-design.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: 1
  - path: docs/decisions/US-022-open-decisions.md
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
supersedes: null
---

# US-022 Database Design — Teachers and their courses

Delegated: nothing to subagents. Read directly: US-020 db-design §2–§4,
`CourseMembershipConfiguration` (indexes), `CourseMembership` (navigations),
`CourseListSource`. The query shapes of §3 were type-checked in a scratch
project referencing `ClassroomAgent.Infrastructure` (build: 0 warnings, 0
errors); SQL translation and plans are proven by the integration tests (§6).

## 1. Summary

**No schema change. No migration.** No table, column, constraint, index,
collation or extension is added or altered. The teacher list reads
`classroom_participant`, `course_membership` and `course` through two new
read-only queries, served by indexes that already exist — chiefly US-020's
partial `ix_course_membership_current_teacher`.

| Object | Change |
|---|---|
| Tables `classroom_participant`, `course_membership`, `course` | none — read only |
| Index `ix_course_membership_current_teacher` (US-020) | none — reused |
| Collation `course_name_uk` (US-020) | none — reused |
| Migrations / model snapshot | none |

## 2. Definitions

- **Current teacher membership** — `course_membership.role = 'teacher' AND
  on_roster = true` (spec FR-002; the same predicate as US-020 db-design §3 and
  the index filter).
- **Status predicate** — `course.course_state = @state` (stored lower-case
  code) for a chosen state; no predicate for `all` (spec FR-005).
- **Matching membership** — a current teacher membership whose course satisfies
  the status predicate.
- **Listed participant** — a participant with at least one matching membership.
- **Course count** — the number of a participant's matching memberships. Since
  `uq_course_membership_course_participant` allows one membership per
  (course, participant), this equals the number of distinct courses (spec I-5);
  no `DISTINCT` is needed.

The course list's teacher filter (US-020 §3.1) uses the same predicate plus the
same status predicate, so for any participant the course list under
`teacherId = p, status = s` returns exactly `course count(p, s)` courses
(spec FR-007, AC-016).

## 3. Queries

`ITeacherListSource` (entity model §2) runs these with `AsNoTracking()`, no
transaction, no write. At most **two** round trips per request, independent of
data size (spec FR-014, AC-015):

| # | Query | When | Serves |
|---|---|---|---|
| T1 | number of listed participants | query valid | FR-006 total, FR-008 |
| T2 | one page of listed participants with their course counts | T1 > 0 | FR-003, FR-006 |

On `400` nothing is read. `Application` clamps the page between T1 and T2
(`page = min(requested, pageCount)`), as the course list does; when T1 = 0, T2
is not run and the empty message is shown.

### 3.1 T1 — count

```
SELECT count(*)
FROM classroom_participant p
WHERE EXISTS (
  SELECT 1 FROM course_membership m
  JOIN course c ON c.id = m.course_id
  WHERE m.participant_id = p.id AND m.role = 'teacher' AND m.on_roster
    [AND c.course_state = @state])
```

### 3.2 T2 — page

```
SELECT p.id, p.surname, p.given_name, p.email, cnt.course_count
FROM classroom_participant p
CROSS JOIN LATERAL (
  SELECT count(*) AS course_count
  FROM course_membership m JOIN course c ON c.id = m.course_id
  WHERE m.participant_id = p.id AND m.role = 'teacher' AND m.on_roster
    [AND c.course_state = @state]) cnt
WHERE cnt.course_count > 0
ORDER BY <label key> COLLATE course_name_uk NULLS LAST, p.id
OFFSET @offset LIMIT 50
```

The SQL is indicative; the implementation writes LINQ (a correlated `Count`
projected, then filtered `> 0`, as type-checked) and whatever SQL the provider
emits for it is acceptable provided it is **one** command, the filter, count,
order and paging run in the database, and the tests of §6 pass. A `GROUP BY`
over the joined memberships is an equally acceptable shape.

### 3.3 Order

The **label key** of US-020 db-design §3.4, unchanged:

```
COALESCE(
  NULLIF(concat_ws(' ', NULLIF(p.surname, ''), NULLIF(p.given_name, '')), ''),
  NULLIF(p.email, '')
) COLLATE course_name_uk  NULLS LAST,
p.id
```

— profile name, else email, Ukrainian-aware, participants with neither last,
ties by id: total and stable between pages (spec FR-006, AC-014). It must be
written so that it produces exactly the order of the course list's teacher
choice (`CourseListSource.GetTeacherChoiceAsync`); sharing that expression in
`Infrastructure` rather than copying it is preferred, the mechanism is left to
implementation.

## 4. Indexes

PC-7 / NFR-003: every lookup has an explicit index.

| Access | Index |
|---|---|
| a participant's current teacher memberships (T1 `EXISTS`, T2 count) | existing partial `ix_course_membership_current_teacher (participant_id, course_id) WHERE role = 'teacher' AND on_roster = true` — exactly this predicate, index-only for the `all` filter |
| membership → its course state | existing `pk_course` |
| participants | existing `pk_classroom_participant` (sequential scan of a few hundred to a few thousand rows is also acceptable) |
| order (label key) | none — computed expression; a top-N sort of the listed participants |

**No new index.** Rationale:

- The partial index carries only current teacher rows — one to three per course,
  so a few thousand rows in the sizing case (spec §9) — and has
  `participant_id` as its leading column, which both T1 and T2 correlate on.
- An index on the label key is impossible as a plain column index (it is an
  expression over three columns) and, as an expression index, would carry the
  same B-tree entry-size risk US-020 §4.2 rejected for course names (surname
  and given name up to 750 characters each, email up to 320); the sort is over at most a few
  hundred to a few thousand listed participants. Not adopted.
- `course_state` has five values; no index (US-020 §4.2).

## 5. Sensitive data and security

- Reads only: no write, no audit row (SC-11, S-12), no secret.
- Projection is minimal: id, surname, given name, email, count. `google_id`,
  `full_name`, photo and every other column are not selected (S-11).
- Names and emails leave the database only into the page DTOs; no query result
  is logged (SC-10, S-08). `@state` is a bound parameter.
- No per-Dean or roster-based row filter exists or is added (Hard Stop, S-03).

## 6. Testing notes for TEST_WRITING

- Integration tests on Testcontainers PostgreSQL with all migrations applied
  (TC-2); the collation and the partial index come from US-020's migration.
- AC-001/AC-002 data set: teacher of an ACTIVE course; teacher of only an
  ARCHIVED course; former teacher (`on_roster = false`); a student only.
- AC-016: one participant teaching two ACTIVE courses, off-roster teacher of a
  third ACTIVE course, student of a fourth ACTIVE course → count 2; the course
  list with `teacherId` and `status=active` returns those two courses.
- AC-014: order of the spec's names plus an email-only and a nameless
  participant; assert the teacher list's order equals the course list's teacher
  choice order for the same participants.
- AC-015: 120 listed teachers → page 2 = 51–100, page 3 = 101–120; the
  per-request command count is 2 for page 1 and page 3, and 1 when nobody is
  listed.
- No schema test changes: table, column and migration counts are unchanged.
