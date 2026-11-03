---
artifact_type: database_design
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T16:55:20Z
updated_at: 2026-11-03T16:55:20Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/designs/api/US-071-api-design.md
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
supersedes: null
---

# US-071 Database Design — read shapes of the header search

**Verdict: PASS.** No table, column, constraint, index or migration changes. The reads are new, and the students' read and its courses are where the Specification can go wrong, so they are designed here (spec section 13). Port and record signatures: `docs/designs/database/US-071-entity-model.md`.

Delegation: none. Read directly: `SearchMatch`, `ParticipantLabel`, `TeacherListSource`, `CourseListSource`, `MeetCodesReadSource`, `GetTeacherListQuery`, the `CourseMembership` and `ClassroomParticipant` configurations, `JournalSource` (the sightings' rule), the US-070 database design. Translation of the expressions below by the provider is not proven by this stage: the tests prove it against real PostgreSQL (TC-2).

## 1. Schema

Nothing changes, so no migration (PC-2). Story AC-010 and spec section 9 ask for no special index; a contains-match cannot use a B-tree anyway (US-070 db-design §1). `ix_course_membership_participant_id` already serves the membership test per participant and the courses of the shown students. The measurement of section 9 tells whether this holds.

## 2. What is reused and what is new

| Group | Total | Rows | Why |
|---|---|---|---|
| Teachers | `ITeacherListSource.CountTeachersAsync(null, q)` | `GetTeacherPageAsync(null, q, 0, 20)` | the list at status "all": the total equals the list's by construction (FR-007) |
| Courses | `ICourseListSource.CountCoursesAsync(new(null, null, q))` | `GetCoursePageAsync(same, 0, 20)` | the same, and the order is the course list's |
| Meet codes | `IMeetCodesReadSource.GetCountsAsync(q)`, summed | new `GetFirstMatchesAsync(q, 20)` | the same match definition; §4 |
| Students | new `IStudentSearchSource.CountStudentsAsync(q)` | new `GetStudentsAsync(q, 20)`, `GetStudentCoursesAsync(ids)` | no list of students exists; §3 |

Nothing else builds a search condition: everything goes through `SearchMatch` (spec section 7). `ParticipantErasureReadSource`'s default folding is not touched and not copied.

`CourseListSource.Matching` and `TeacherListSource.Listed` are unchanged. A status of null means every state in both.

## 3. Students

**Who** (FR-003). One filter, shared by the count and the rows:

```text
classroom_participant p
  WHERE EXISTS (course_membership m WHERE m.participant_id = p.id AND m.role = 'student')
    for each word: word in p.surname OR word in p.given_name OR word in p.email   -- SearchMatch
```

- `on_roster` is not tested: a departed student and an off-roster one known through submissions both count (spec FR-003, section 3 v56).
- A person with no name and no email has only null fields, so no word matches them (FR-003, I-4).
- A teacher membership does not qualify, so a person who is only a teacher is not here (AC-002).
- `full_name` is not searched (spec section 10).

**Rows.** `.Select(ParticipantLabel.Projection).OrderByLabel().Take(limit)`, then to `StudentSearchRecord`. The label's tie-break by id keeps namesakes apart and the order stable.

**Courses of the shown students.** One command, whatever the number of students (spec section 9), and none when no row is shown:

```text
course_membership m JOIN course c
  WHERE m.participant_id = ANY(ids) AND m.role = 'student'
  ORDER BY c.name COLLATE course_name_uk, c.section NULLS FIRST (as the course list), c.section COLLATE course_name_uk, c.id
  → ParticipantId, CourseId, Name, Section, OnRoster, SeenOnce = (first_seen_at = last_seen_at), LastSeenAt
```

- `SeenOnce` is the expression `JournalSource` uses (`m.FirstSeenAt == m.LastSeenAt`, US-060 OD-003 a), and `DataMarkRules.Left` in Application makes the mark in the school's time zone. The rule is not copied.
- The unique index on (course, participant) means one row per course. A teacher membership is excluded by the role (FR-003).
- Every status of course is listed: no filter on `c.state`. `absence_mark` is not read (section 10 of the Specification).
- The ids are the shown ones, at most 20, passed as a list; never the text.

## 4. Meet codes

`GetFirstMatchesAsync(search, limit)` on `MeetCodesReadSource`, beside the queries it reuses (`UnassignedCodes`, `LinkedLinks`, `MarkedLinks`), so the match has one definition:

- for each of the three, one command: the codes under `search`, `ORDER BY code COLLATE "C"`, `LIMIT limit`;
- merge the three in memory by `StringComparer.Ordinal`, take `limit`.

The first `limit` of each list are enough: no code past a list's first `limit` can be in the merged first `limit`. Ordinal in .NET and `"C"` in PostgreSQL agree on these codes, which are ASCII as Google gives them; the order is only for display, and a mismatch could not lose a match from the total. A code is in one list only: Unassigned has no link row, and a link row has a course or not.

Three separate commands rather than one `UNION ALL`: the provider's handling of a constant list column in a set operation would be a new risk for no gain. Commands stay fixed either way.

## 5. Commands per request

At most: students 3, teachers 2, courses 2, Meet codes 6 — **13 for a text with matches, fixed** whatever the number of words (spec section 9). A group with total 0 skips its rows: fewer, never more. `Prompt` and `Refused` issue none (VR-002, VR-003); a test asserts this by the fakes receiving no call.

Teachers' `GetTeacherPageAsync` also computes a course count that the page does not show. It is one correlated subquery on at most 20 rows, and reusing it keeps the total and the order the list's. Left as is.

## 6. Sensitive data, transactions

Reads only, untracked, with no transaction and no write (AD-7 not engaged). The text is a bound parameter (`SearchMatch`) and is in no log, exception message or audit row (SC-10, SC-14). The new source logs nothing, as the others do not. Erased or excluded people: search reads current rows only, and such a person's stored fields hold nothing to match; a Meet organizer so treated is stored as null, which the reused queries already skip (spec section 7, US-038 I-9).

## 7. Traceability

| Requirement | Section |
|---|---|
| FR-003, AC-003 | §3 |
| FR-004 | §2 |
| FR-005 | §2 |
| FR-006 | §2, §4 |
| FR-007, AC-004 | §2, §3, §4 |
| AC-006, spec section 7 | §3, §6 |
| AC-010, spec section 9 | §1, §5 |
