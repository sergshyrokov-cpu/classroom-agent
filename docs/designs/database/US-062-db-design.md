---
artifact_type: database_design
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T19:19:41Z
updated_at: 2026-10-27T19:19:41Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/api/US-062-openapi.yaml
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
supersedes: null
---

# US-062 Database Design — data-health reads, empty-list date, confirmation of deleted courses

Companion: `docs/designs/database/US-062-entity-model.md`.

Delegated: none. Read directly: `SyncState` and `SyncStateConfiguration`, `Course` and
`CourseConfiguration`, `CourseWork` / `Submission` configurations, `AuditEvent` and
`AuditEventConfiguration`, `RunSynchronizationUseCase.CloseCourseListAsync`, `IUnitOfWork`, the
existing read-source ports, the latest migration `20261027110753_AddCourseListMarks`.

## 1. Summary of changes

| Table | Change | Spec |
|---|---|---|
| `sync_state` | new column `course_list_empty_since timestamptz NULL`, backfill, CHECK | FR-008, OD-002 a |
| `audit_event` | new column `confirmed_course_ids bigint[] NULL`, new action code, two CHECKs | FR-006.2 step 5, SC-11, API m-1 |
| `course_work` | new partial index for "not in Classroom" items | §9 |
| `submission` | new partial covering index over graded rows | §9 |
| `course` | no column change; a new domain operation and a row-locking read | FR-006.2 step 4, §8 |

One migration: `AddDataHealth` (PC-2). Backward compatible (NFR-033): only nullable columns and
indexes are added; `last_course_list_empty` stays.

## 2. `sync_state.course_list_empty_since`

- `timestamptz`, nullable. The `StartedAt` of the first run of the current streak of empty course lists
  read to their end while courses were stored (FR-008.1, I-11).
- Written only by `SyncState.RecordCourseListRead` (entity model §1.1): `true` sets the flag and, if
  the date is null, the date := `StartedAt`; `false` clears both. A run that stops before the end of
  the list does not call it (US-059 FR-005.4), so the date survives a failed run.
- An empty list while **no** course is stored goes through the non-empty branch of
  `CloseCourseListAsync` today and clears both — no problem is raised (BR-048 "while courses are
  stored"); unchanged.
- **Migration backfill (OD-002 a):** `UPDATE sync_state SET course_list_empty_since = started_at WHERE
  last_course_list_empty;` — then the CHECK.
- **CHECK** `ck_sync_state_course_list_empty_since`:
  `last_course_list_empty = (course_list_empty_since IS NOT NULL)` — FR-008.1 "flag true exactly when
  the date is set".

## 3. `audit_event` — the confirmation row

### 3.1 Several course ids on one row: an array column

The Specification asks for **one** audit event carrying the confirmed courses' ids (FR-006.2 step 5,
AC-010). The single `target_id` holds one id. Options weighed:

- **`bigint[]` column `confirmed_course_ids`** — chosen. One row as specified; Npgsql maps `long[]`
  natively (no new package); the row stays self-contained, so the retention purge (PC-11) deletes it
  with nothing else to clean; ids only, no personal data (SC-11).
- a child table `audit_event_course` — a second table for one action, a FK and a purge step for no
  gain; rejected.
- one row per course — contradicts "one audit event"; rejected.

No FK to `course`: audit ids outlive the course they name (PC-11), as `target_id` does.

### 3.2 Action and shape

- New `AuditAction.CoursesConfirmedDeleted`, code `courses_confirmed_deleted`, added to
  `ck_audit_event_action`.
- `target_type` / `target_id` NULL: the courses are in the array.
- **CHECK** `ck_audit_event_course_deletion_shape`:

  ```sql
  action <> 'courses_confirmed_deleted' OR (
    actor_type = 'app_user' AND actor_role = 'admin'
    AND target_type IS NULL AND target_id IS NULL
    AND ((outcome = 'succeeded' AND refusal_category IS NULL
          AND confirmed_course_ids IS NOT NULL AND cardinality(confirmed_course_ids) BETWEEN 1 AND 1000)
      OR (outcome = 'refused' AND refusal_category = 'read_only_mode'
          AND confirmed_course_ids IS NULL)))
  ```

  Succeeded rows carry 1…1000 ids (VR-001); the refused row (API design §2.5, m-1 accepted) carries
  none — unverified input.
- **CHECK** `ck_audit_event_confirmed_course_ids_absent`:
  `action = 'courses_confirmed_deleted' OR confirmed_course_ids IS NULL`.
- `ck_audit_event_immutable` already covers the new row.
- Ids are stored ascending and distinct (the factory sorts and de-duplicates), so a test can compare
  the column to the expected set.

## 4. The confirmation transaction and concurrency (spec §8)

Inside `IUnitOfWork.ExecuteInTransactionAsync` (PostgreSQL default READ COMMITTED):

1. `ICourseRepository.GetForAbsenceConfirmationAsync(ids)` reads the selected rows with
   `SELECT … FROM course WHERE id = ANY(@ids) FOR UPDATE` — tracked entities, fresh values;
2. the use case requires `count = ids.Count` and every row `NotVisibleToTechnicalAccount`, else it
   returns stale (`409`, nothing staged — VR-002, AC-017);
3. `Course.ConfirmDeletedInClassroom(now)` on each; the audit row is added; commit.

Why the lock is enough:

- synchronization commits a change to a marked course only by `RecordListed` (mark removed).
  Committed before step 1 → step 1 sees no mark → stale. Attempted after step 1 → it waits for the
  lock, then writes `absence_mark = NULL` over the confirmed mark — correct: the course is listed
  again, and BR-048 removes "not in Classroom" too;
- synchronization never writes `MarkAbsent` over a set mark (`RunSynchronizationUseCase.cs:633-636`),
  so it cannot replace the confirmation's mark with another;
- a second concurrent confirmation of the same course waits, then sees `NotInClassroom` → stale;
- the retention purge deleting a selected course waits, or the course is missing → stale.

No concurrency token on `course` is needed; a lock held for one short transaction is simpler and
does not affect the synchronization's own writes.

## 5. Data-health reads and indexes (spec §9)

All reads are `AsNoTracking`, through a new read port `IDataHealthSource` (entity model §2) plus the
existing `ISyncStateRepository.GetForReadAsync`, `ISyncSkippedCourseRepository.GetAllAsync`,
`ILegitimacyStateRepository.GetForReadAsync` and `GetLegitimacyModeQuery`. The banner and the page
use the **same** fact query, so their counts agree by construction.

### 5.1 The per-request fact query (banner and page)

`IDataHealthSource.GetCourseFactsAsync` returns one row per course that has at least one of: an
absence mark, a graded submission above its item's maximum, an item "not in Classroom", or a skip
record matching its Google id. Columns: id, Google id, name, state, absence mark and date, last
complete read, and two booleans. In SQL terms:

```sql
SELECT c.id, c.google_id, c.name, c.course_state, c.absence_mark, c.absent_since, c.last_complete_read_at,
  EXISTS (SELECT 1 FROM course_work w JOIN submission s ON s.course_work_id = w.id
          WHERE w.course_id = c.id AND w.max_points > 0
            AND (s.assigned_grade > w.max_points OR s.draft_grade > w.max_points)) AS above_max,
  EXISTS (SELECT 1 FROM course_work w
          WHERE w.course_id = c.id AND w.not_in_classroom_since IS NOT NULL) AS items_gone
FROM course c
WHERE c.absence_mark IS NOT NULL OR <above_max> OR <items_gone>
   OR c.google_id IN (SELECT course_google_id FROM sync_skipped_course)
```

The implementation writes it in LINQ (`Any(...)` subqueries translate to `EXISTS`); the shape, not
the text, is binding. `max_points > 0` and the strict `>` are `DataMarkRules.IsAboveMaximum` (spec
§1.1) — a test pins that the SQL and the domain rule agree on the boundaries (equal, zero max, null).
Counting (one per course × kind, archived excluded, X5 once) is done in `Application` over these
rows, not in SQL: it is a business rule (AD-3).

### 5.2 Indexes

- `ix_submission_graded` on `submission (course_work_id) INCLUDE (assigned_grade, draft_grade)
  WHERE assigned_grade IS NOT NULL OR draft_grade IS NOT NULL` — the above-maximum probe reads only
  graded rows, index-only.
- `ix_course_work_not_in_classroom` on `course_work (course_id) WHERE not_in_classroom_since IS NOT
  NULL` — the "items gone" probe touches only marked items.
- `course_work (course_id, …)` already exists (`ix_course_work_course_item_date`) for the join.
- `course`, `sync_skipped_course` are bounded by the school's courses; no index added.

IMPLEMENTATION reports the plan (`EXPLAIN ANALYZE`) of the fact query on a synthetic school of
~200 courses × ~30 items × ~30 students in the implementation report; no time budget is invented
here.

### 5.3 The page-only detail queries

- `GetAboveMaximumGradesAsync(courseIds)` — rows of course id, item title, student display name (the
  label the journal shows, built by the existing `ParticipantLabel`), points, maximum,
  draft flag; ordered item title, student. One row per grade above max: both the assigned and the
  draft grade of one submission can each be a row (`isDraft`).
- `GetItemsNotInClassroomAsync(courseIds)` — course id, title, `not_in_classroom_since`.

Called with the course ids whose booleans are true, only for the page.

## 6. Sensitive data

New columns carry no personal data: a date and course ids. Student names and grades are read for the
page as the journal reads them; they are never logged (SC-10) or written to audit.

## 7. Migration `AddDataHealth`

Order in `Up`: add `sync_state.course_list_empty_since`; backfill (§2); add its CHECK; add
`audit_event.confirmed_course_ids`; drop and re-create `ck_audit_event_action` with the new code; add
the two audit CHECKs (§3.2); create the two indexes (§5.2). `Down` reverses. The snapshot is
regenerated by `dotnet ef migrations add`; no hand-written SQL except the backfill `UPDATE` and the
CHECK texts, which EF emits from the configuration.

## 8. Not changed

`course`, `sync_skipped_course`, `legitimacy_state`, `workspace_connection`: no column. The retention
purge (PC-11) needs no change: the audit array goes with its row.
