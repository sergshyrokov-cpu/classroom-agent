---
artifact_type: database_design
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T10:20:00Z
updated_at: 2026-10-04T10:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-015-db-design.md
    version: 1
supersedes: null
---

# US-025 Database Design — Journal view for a period

**Verdict: PASS.** **No migration.** No table, column, constraint, index or
audit action is added or changed (spec FR-014). Every index the journal needs
already exists from US-014 and US-015 (§3). The Story adds **reads only**: four
fixed queries behind one new read port, `IJournalSource` (entity model §2).

## 1. Tables read

| Table | Read for | Columns read |
|---|---|---|
| `course` | the course drop-down (spec FR-002) and the course-existence check (VR-001) | `id`, `name`, `section` |
| `course_work` | the columns (spec FR-004) | `id`, `course_id`, `resource`, `title`, `item_date`, `due_at`, `max_points` |
| `course_membership` | the candidate rows (spec FR-005) | `course_id`, `participant_id`, `role`, `first_seen_at`, `last_seen_at`, `on_roster` |
| `classroom_participant` | the row label (spec FR-005) | `id`, `full_name`, `email` |
| `submission` | the cells (spec FR-006) | `id`, `course_work_id`, `participant_id`, `update_time`, `state`, `raw_state`, `assigned_grade`, `draft_grade`, `turned_in_at`, `late` |

Not read: `google_id` / `google_user_id` columns, `course_work.update_time`, `creation_time`,
the Course descriptive columns, `created_at` / `updated_at`. Nothing outside the
five tables above (no `audit_event`, `app_user`, `workspace_connection`,
`legitimacy_state`, `sync_state`).

Nothing is written: every query runs with `AsNoTracking()`, and the use case
never calls `IUnitOfWork` (spec FR-013, S-04).

## 2. The queries

A journal request issues **at most four round trips**, whatever the number of
rows and columns (spec FR-014). The order is the evaluation order of
api-design §3; a failure of query shape stops before Q1.

| # | When | Query (logical SQL) | Index used |
|---|---|---|---|
| Q1 | every request with a valid query shape | `SELECT id, name, section FROM course` | none — whole table (§3.2) |
| Q2 | `courseId` given and present in Q1 | `SELECT … FROM course_work WHERE course_id = @c AND item_date >= @start AND item_date < @end` | `ix_course_work_course_item_date` |
| Q3 | after Q2, only if Q2 returned at least one item | `SELECT m.participant_id, m.first_seen_at, m.last_seen_at, m.on_roster, p.full_name, p.email FROM course_membership m JOIN classroom_participant p ON p.id = m.participant_id WHERE m.course_id = @c AND m.role = 'student'` | `uq_course_membership_course_participant` (leading `course_id`); `pk_classroom_participant` |
| Q4 | after Q3, only if Q3 returned at least one membership | `SELECT s.id, s.course_work_id, s.participant_id, s.update_time, s.state, s.raw_state, s.assigned_grade, s.draft_grade, s.turned_in_at, s.late FROM submission s JOIN course_work w ON w.id = s.course_work_id WHERE w.course_id = @c AND w.item_date >= @start AND w.item_date < @end` | `ix_course_work_course_item_date` → `ix_submission_course_work_participant` (leading `course_work_id`) |

Decisions:

- **Course existence comes from Q1.** The form needs every course anyway
  (spec FR-002), so "unknown course" (VR-001, `404`) is "the id is not in Q1's
  result" — no separate lookup. When no course is stored, every `courseId` is
  unknown, as api-design §3 requires.
- **Q2 short-circuits.** No item in the period → the `NoColumns` empty state
  (spec FR-009); Q3 and Q4 are not issued. No student membership → `NoRows`;
  Q4 is not issued.
- **Q4 filters by the course and period, not by a list of item ids.** It repeats
  Q2's predicate through the join, so the parameter count is fixed and no
  `IN (…)` list grows with the number of columns.
- **The row rule stays in `Application`.** Q3 returns *every* student
  membership of the course; spec FR-005's two conditions (on the roster during
  part of the period, or a submission to a column) are applied in memory by the
  use case. The rule is a business rule (BR-051) and is unit-testable there; the
  candidate set is bounded by one course's membership rows (§4).
- **No `ORDER BY` in any query.** Every order the page shows (courses, columns,
  rows) uses the collation of the user's UI language and internal-id tie-breaks
  (spec FR-002, FR-004, FR-005); it is applied in memory in `Application` with a
  culture-aware comparer, not by a PostgreSQL collation, which would depend on
  the ICU collations installed on each school's server.
- **Period bounds are UTC instants.** `@start` and `@end` are the spec FR-003
  instants with offset zero. All five tables store `timestamp with time zone`
  (PC-6); Npgsql rejects a `DateTimeOffset` with a non-zero offset for such a
  parameter, so the use case converts before calling the port.
- **`role` is compared as the stored code `'student'`** through the existing
  value converter of `CourseMembershipConfiguration`; the predicate is written
  against `ClassroomRole.Student`, never as a string literal in C#.

### 2.1 Consistency between queries

Q2 … Q4 are separate statements, not one snapshot: a synchronization run may
commit between them. The use case tolerates this without a transaction:

- a submission whose `course_work_id` is not among Q2's items is ignored;
- a submission whose participant has no membership in Q3 is ignored (not a row);
- a membership from Q3 that meets neither FR-005 condition is not a row.

The result is a journal that is correct for a moment between Q2 and Q4. A
`REPEATABLE READ` read-only transaction would make it a single snapshot, but
`IUnitOfWork.ExecuteInTransactionAsync` is the write boundary of AD-7 and no
convention defines a read transaction; the tolerance above is sufficient for a
view that is rebuilt on every request.

### 2.2 More than one submission per student and item

`ix_submission_course_work_participant` is deliberately **not unique**
(US-015 db-design §4.3): the schema permits two submissions of one item by one
student, while spec FR-006 computes the cell from "the student's submission for
it (or none)". Q4 returns every stored submission; it neither deduplicates nor
picks one. Which one feeds the cell is **OD-008 (a)**, resolved by the Owner on
2026-10-04: the most recently updated in Google (`update_time`, absent counts as
oldest), then the larger internal `id` — applied in memory in `Application`.
That is why Q4 reads `id` and `update_time`.

## 3. Indexes — confirmation against spec FR-014 / PC-7

| Spec FR-014 requirement | Existing index | Origin | Serves |
|---|---|---|---|
| items of a course by date | `ix_course_work_course_item_date (course_id, item_date)` | US-015 | Q2 range scan; Q4 driving side |
| memberships of a course | `uq_course_membership_course_participant (course_id, participant_id)` | US-014 | Q3 equality on `course_id` |
| submissions of an item | `ix_submission_course_work_participant (course_work_id, participant_id)` | US-015 | Q4 inner side, one probe per item |
| participant of a membership | `pk_classroom_participant (id)` | US-014 | Q3 join |

### 3.1 Why no new index

- **`ix_course_membership_course_seen (course_id, first_seen_at, last_seen_at)`**
  exists too (US-014, PC-7 roster-on-a-date). Q3 does not push the date
  condition into SQL (§2), so either index serves it; the planner chooses.
- **A partial index on `role = 'student'`** was considered and rejected: a
  course has a handful of teacher memberships, so the residual filter discards
  almost nothing.
- **A covering index for Q4** (`INCLUDE` the cell columns) was considered and
  rejected: the heap fetch per submission is the same order as the rendering
  cost of the cell, and a second wide index would slow every synchronization
  upsert of `submission`, the table that grows fastest.
- **`course`** has no index besides `uq_course_google_id`, by US-014 db-design
  §3.4's own reasoning; Q1 reads the whole table (§3.2 below).

### 3.2 Q1 reads the whole `course` table

A school stores at most a few thousand courses over its retention period
(courses are purged after it, US-037). Three narrow columns of that many rows is
one sequential scan of a few hundred kilobytes; an index cannot make a
whole-table read cheaper. US-020 (course list with filters) will index against
its own queries.

## 4. Sizing

The case the spec names (§9): one course, several hundred students, one school
year.

| Query | Rows | Note |
|---|---|---|
| Q1 | ≤ a few thousand | every course of the school |
| Q2 | ≤ a few hundred | items of one course dated in the period |
| Q3 | ≤ a few hundred | student memberships of one course, leavers included until purged |
| Q4 | ≤ rows × columns, ~10⁵ in the worst case | at most one per student and item in practice (§2.2) |

Q4's worst case is the size of the rendered grid itself; the page renders the
period whole by design (spec FR-014, I-9), so the database is not the
bottleneck. Q4 materialises narrow records (no strings except `raw_state`,
which is set only for an unrecognised state).

## 5. Schema initialization

None. No `dotnet ef migrations add`; `ClassroomAgentDbContextModelSnapshot`
does not change. TEST_WRITING may assert that the model has no pending changes
(the existing practice, if present), which is the proof of "no new table, no new
column" (spec FR-014).

## 6. Sensitive data

The journal reads students' names, emails and grades — personal data of
possibly minor students (`AGENTS.md` Security Policy).

- Nothing is persisted, cached or copied: no table, no column, no audit row
  (spec S-11), no file.
- `IJournalSource` returns them only to the use case, which maps them to the
  page DTO; no log line carries them (spec FR-016, S-07). The Information line
  of FR-016 counts rows and columns from the DTO, never logs the records.
- The participant's internal id travels from the port to the use case (it is
  the join key and the tie-break of FR-005) but not into the page DTO
  (api-design §2.5).
- Google identifiers (`google_id`, `google_user_id`) are not read.

## 7. Tests the design implies

For TEST_WRITING; real PostgreSQL via Testcontainers (TC-2), data built
synthetically (TC-4, AC-013).

1. **Bounded round trips.** A journal of 1 row × 1 column and one of N rows × M
   columns issue the same number of commands (counted by a test
   `DbCommandInterceptor` or Npgsql logging), at most four.
2. **Short-circuits.** No item in the period → Q3/Q4 not issued; items but no
   student membership → Q4 not issued.
3. **Period edges in SQL.** An item with `item_date = @start` is returned; one
   with `item_date = @end` is not (half-open interval, spec FR-003); the same
   for Q4's join.
4. **Q4 duplicates.** Two submissions of one item by one student are both
   returned by Q4 with their `Id` and `UpdateTime`; the use case test proves
   OD-008 (a): newer `UpdateTime` wins, absent is oldest, equal → larger id.
5. **Q3 scope.** Teacher memberships and memberships of other courses are never
   returned; a leaver (`on_roster = false`) is returned (the rule is applied in
   memory).
6. **Q4 scope.** Submissions to items of another course, or of the same course
   outside the period, are never returned.
7. **No write.** After a journal request, `SaveChanges` was not called and no
   row in any table changed (spec FR-013, AC-009) — e.g. `xmin` / `updated_at`
   of the seeded rows unchanged and `audit_event` row count unchanged.
8. **No schema change.** The EF model has no pending migration.

## 8. Findings

- **F-1 (resolved — OD-008 a).** Spec FR-006 assumes at most one submission
  per student and item; the schema allows more (US-015 db-design §4.3). Raised
  as OD-008 in `docs/decisions/US-025-open-decisions.md` v2 and resolved by the
  Owner on 2026-10-04 with option (a) (§2.2).
- **F-2 (informational).** No convention defines a read-only transaction;
  §2.1 relies on tolerance instead. If a later report (US-028 export) needs a
  single snapshot, that is a persistence-conventions change, not a Story choice.
