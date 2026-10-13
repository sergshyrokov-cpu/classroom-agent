---
artifact_type: database_design
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-12T12:56:53Z
updated_at: 2026-10-12T12:56:53Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/designs/api/US-024-api-design.md
    version: 1
  - path: docs/designs/api/US-024-openapi.yaml
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 2
supersedes: null
---

# US-024 Database Design — Database statistics and last synchronization

## 1. Summary

- **No schema change, no migration.** No table, column, constraint or index is
  added or changed; no audit action is added (the press writes US-019's
  `synchronization_requested` row).
- **Two read round trips per page**, independent of the size of the school
  (spec FR-011, AC-017):
  - **Q1** — one SQL statement returning all eleven counters as scalar
    sub-queries, through a new read port `IDatabaseStatisticsSource`;
  - **Q2** — the `sync_state` row, through the existing
    `ISyncStateRepository.GetForReadAsync` (as `GetLastSynchronizationQuery`).
- Q1 being one statement gives the counters one snapshot of committed data
  (PostgreSQL statement-level snapshot under READ COMMITTED). The spec does not
  require it (I-5) but gets it for free; Q1 and Q2 are not in one snapshot.
- Everything is read without change tracking; `DbContext` stays in
  `Infrastructure` (AD-3).

Delegated: nothing. Read directly (read only): the model snapshot
`ClassroomAgentDbContextModelSnapshot.cs` (tables `course`, `course_membership`,
`course_work`, `submission`, `meet_session`, `meeting_code_link`, `sync_state`,
their index names and check constraints), `CourseMembershipConfiguration`
(role codes), `MeetingCodeLinkConfiguration`, `MeetCodesReadSource.GetCountsAsync`,
`ISyncStateRepository`, `IMeetCodesReadSource`, `GetLastSynchronizationQuery`.
The latest migration is `20261012073623_AddIncrementalSync`; this Story adds
none. The entity-model signatures were compiled in a scratch project (see the
entity model).

## 2. Definitions (stored codes)

| Concept | Table | Predicate |
|---|---|---|
| Course of a state | `course` | `course_state = 'active' \| 'archived' \| 'provisioned' \| 'declined' \| 'suspended'` (`ck_course_course_state`) |
| Current teacher | `course_membership` | `role = 'teacher' AND on_roster = true` (`ck_course_membership_role`; US-020 OD-003, US-022 OD-002) |
| Current student | `course_membership` | `role = 'student' AND on_roster = true` |
| Coursework item | `course_work` | every row |
| Submission | `submission` | every row |
| Meet meeting | `meet_session` | every row |
| Code awaiting a decision | `meet_session` × `meeting_code_link` | distinct `meet_session.meeting_code` with no `meeting_code_link` row of the same code (US-032 "Unassigned"; `MeetCodesReadSource.GetCountsAsync`) |

The course of a membership, coursework or meeting is **not** joined: every
counter except the five course states is over all stored rows (spec FR-003 "any
state", I-4). No row of `classroom_participant` is read — a participant with no
current membership has no qualifying row and so is not counted (OD-003).

## 3. Queries

### 3.1 Q1 — the counters (`IDatabaseStatisticsSource.GetCountsAsync`)

One statement, no parameters, through `Database.SqlQuery<T>` with a
`FormattableString` (as `MeetCodesReadSource` already does), into an internal
keyless record of `long` columns:

```sql
SELECT
  (SELECT count(*) FROM course WHERE course_state = 'active')      AS active_courses,
  (SELECT count(*) FROM course WHERE course_state = 'archived')    AS archived_courses,
  (SELECT count(*) FROM course WHERE course_state = 'provisioned') AS provisioned_courses,
  (SELECT count(*) FROM course WHERE course_state = 'declined')    AS declined_courses,
  (SELECT count(*) FROM course WHERE course_state = 'suspended')   AS suspended_courses,
  (SELECT count(DISTINCT participant_id) FROM course_membership
     WHERE role = 'teacher' AND on_roster = true)                  AS teachers,
  (SELECT count(DISTINCT participant_id) FROM course_membership
     WHERE role = 'student' AND on_roster = true)                  AS students,
  (SELECT count(*) FROM course_work)                               AS course_work_items,
  (SELECT count(*) FROM submission)                                AS submissions,
  (SELECT count(*) FROM meet_session)                              AS meet_meetings,
  (SELECT count(DISTINCT s.meeting_code) FROM meet_session s
     WHERE NOT EXISTS (SELECT 1 FROM meeting_code_link l
                       WHERE l.meeting_code = s.meeting_code))      AS meet_codes_awaiting_decision
```

- The column and table names are the snapshot's; the state and role codes are
  the stored codes of §2. The adapter keeps the codes as constants next to the
  existing converters' codes so a rename breaks one place.
- `count` returns `bigint`; the adapter narrows each value to `int` with a
  **checked** conversion — an overflow is an unexpected failure (generic error
  page), not a wrong number (api-design §2.8).
- Five state sub-queries rather than one `GROUP BY` keep a state with no course
  at `0` without post-processing, and keep the statement single-row.
- **Agreement with the Meet codes page (AC-018).** The last sub-query is the SQL
  form of the LINQ in `MeetCodesReadSource.GetCountsAsync` (`MeetSessions` with
  no `MeetingCodeLinks` row of the same code, distinct codes). TEST_WRITING
  asserts that the two numbers are equal on the same data, so a future change to
  one is caught.
- Writing Q1 as eleven LINQ `CountAsync` calls would also satisfy AC-017 (a
  fixed count, independent of data), at eleven round trips; the single
  statement is chosen for one round trip and one snapshot.

### 3.2 Q2 — the synchronization state

`ISyncStateRepository.GetForReadAsync` unchanged: at most one row
(`uq_sync_state_singleton`), no tracking. `null` → `NotYet` (spec I-2). The
mapping Running/Completed/Failed and the rejection of any other status follow
`GetLastSynchronizationQuery`; only `Status`, `LastSuccessfulRunAt`,
`LastFullRunAt` and `MeetLoadedUpTo` are read into the DTO. `LastError`,
`FailedStep`, `ProcessedCount`, `RunId`, `StartedAt`, `FinishedAt` are not
copied anywhere (spec FR-005, S-08).

### 3.3 Time zone

The three times are `timestamptz` (UTC). `GetDatabaseStatisticsQuery` converts
them with `TimeZoneInfo.ConvertTime(instant, schoolTimeZone.Zone).DateTime`, as
`GetLastSynchronizationQuery` does for `MeetLoadedUpTo` (spec FR-005, AC-014).
No conversion in SQL.

## 4. Indexes

No index is added. Each sub-query against the existing indexes:

| Sub-query | Plan expected | Why no new index |
|---|---|---|
| course by state (×5) | sequential scan of `course` | a few thousand rows per school over the retention period (NFR-003); five scans of a small table cost less than a new index's upkeep. An index on `course_state` with five values would not be chosen for the large states. |
| teachers | index-only scan of the partial `ix_course_membership_current_teacher (participant_id, course_id) WHERE role='teacher' AND on_roster` | already exists (US-022) and matches the predicate exactly |
| students | scan of `course_membership` filtered by role and roster | current student memberships are the large majority of the table; a partial index on them would be nearly the table itself and add write cost to every synchronization. |
| course_work, submission, meet_session counts | `count(*)`; PostgreSQL may use an index-only scan of the smallest index | a full count reads every row whatever the index |
| codes awaiting decision | `ix_meet_session_meeting_code_started_at` for the distinct codes; anti-join probes `uq_meeting_code_link_meeting_code` | both exist (US-031, US-032) |

The page is opened by a person, not on a schedule; whole-table counts over the
sizing case (thousands of courses, tens of thousands of memberships, submissions
and meetings) take milliseconds. PC-7's "index every lookup column" is about
lookups; these are aggregates over whole tables.

## 5. Sensitive data and security

- Q1 returns counts only: no name, email, Google id or meeting code leaves the
  database (spec S-08, S-09).
- Q2 loads the `SyncState` entity, including `LastError`; it never leaves
  `Application` — the DTO has no field for it (api-design §2.6).
- No write in either query, in any mode (spec FR-008, S-04). No raw value from
  the request reaches SQL: Q1 has no parameters.
- No log line carries a count of persons together with an identifier; the page
  logs the actor id only (spec FR-013).

## 6. Testing notes for TEST_WRITING

- Integration tests on real PostgreSQL (Testcontainers, TC-2):
  - each of the eleven counters against seeded rows, including the AC-003 four
    people, a person with two teacher and one student membership (AC-015), a
    teacher membership of an ARCHIVED course, and rows of a course awaiting
    purge (I-4);
  - empty database → all zeros (AC-006);
  - AC-018: Q1's codes-awaiting-decision equals
    `MeetCodesReadSource.GetCountsAsync().Unassigned` on the same data — a code
    with two meetings, a linked code, a "not a course" code;
  - AC-017: the number of SQL commands for the page (EF Core command interceptor
    or diagnostic listener) is the same with 1 and with 200 courses.
- Read-only mode and the "no write" property are proven in `Application` with
  substituted ports (TC-5), and at the database by asserting no row changed.
