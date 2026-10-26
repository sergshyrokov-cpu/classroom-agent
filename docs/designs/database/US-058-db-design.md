---
artifact_type: database_design
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T19:54:01Z
updated_at: 2026-10-26T19:54:01Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: docs/designs/api/US-058-api-design.md
    version: 1
  - path: trebovaniya.md
    version: 99
supersedes: null
---

# US-058 Database Design — Synchronization marks items no longer in Classroom and records the courses it skips

**Verdict: PASS.** Three persistence changes, one migration:

1. `course_work.not_in_classroom_since` — the "not in Classroom" mark (FR-001…FR-003).
2. `course.last_complete_read_at` — the instant of the course's last successful read (FR-008, OD-001 a).
3. New table `sync_skipped_course` — the skip records of the synchronization state (FR-005…FR-007).

Delegation: none. The designer read `persistence-conventions.md` (PC-3…PC-13), the entities
`Course`, `CourseWork`, `SyncState`, their configurations, `ISyncStateRepository`,
`ICourseWorkRepository`, `IUnitOfWork`, `RunSynchronizationUseCase.ImportCoursesAsync` /
`ImportCourseAsync` / `ImportCourseWorkAsync`, `GoogleClassroomReader.Snapshot`, and the
migration list (latest `20261025104345_WidenGradeColumns`) directly.

## 1. Rationale

- **The mark lives on the item row** (§3 `CourseWork` v99: "Пометка «нет в Classroom» и
  дата"). It is one nullable instant beside the Google-sourced columns and never overwrites one
  (FR-001.4, v83 mirror rule): null = present in Classroom, a value = absent since then
  (VR-005). No separate flag column: the instant alone carries both facts.
- **The last-read instant lives on the course** next to `complete_read_update_time`, written by
  the same domain call, so the two "complete read" facts can never disagree (§3 below).
- **Skip records are their own table, not columns of `sync_state`.** `sync_state` is a single
  row (`uq_sync_state_singleton`); a course list does not fit in it. §3 `SyncState` names the
  records as part of the synchronization state; the table's name (`sync_` prefix) says so, and
  the spec lets DB_DESIGN pick the shape (§9).
  - **No foreign key to `sync_state`:** a key to an always-one row expresses nothing and would
    only add a delete-order rule. Ownership is by name and by the repository.
  - **No foreign key to `course`:** a record exists for a course never loaded (FR-005.3) and
    must outlive the purge of a stored one (I-6); it is keyed by Google's course id alone.

## 2. `course_work` — new column

| Column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `not_in_classroom_since` | `timestamptz` | yes | none | UTC instant of the run that first noticed the item absent from a full read (`observedAt`, VR-004); null while the item is in Classroom |

- **No check constraint.** The instant comes from the program's clock; no other column bounds it
  (`created_at` may be later for a row created by an older migration path — not worth a rule).
- **No new index.** No query of this Story filters on the column: absent items are found from
  the course's items the upsert already loads (`GetByCourseAsync`, uses
  `uq_course_work_course_resource_google_id`). Story 2/3 (journal marks, notice counts) design
  the index their queries need, e.g. a partial index on `course_id WHERE not_in_classroom_since
  IS NOT NULL`.
- **Personal data:** none — an instant on an item row.
- **Retention (PC-11):** unchanged. The purge reads `creation_time` / `update_time`, never this
  column, so marking never prolongs a course (spec §7). Marking changes `updated_at` (PC-6),
  which the purge also does not read.
- Existing rows get null — every stored item counts as present until its course's next full
  read (no source to backfill).

## 3. `course` — new column

| Column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `last_complete_read_at` | `timestamptz` | yes | none | UTC `observedAt` of the run whose full read of the course committed (FR-008.1); null until the first full read after this migration (FR-008.2) |

- Written **only** together with `complete_read_update_time`, by the one domain call that
  records a complete read (entity model §2). A course whose run skipped it (out of range,
  unchanged archive, `404`, blank name) keeps both values.
- No check constraint (`complete_read_update_time` is a Google time and may be null; the two
  are not comparable). No index: nothing in this Story reads it; Story 2 reads it with the
  course row it already loads by id.
- Personal data: none.

## 4. `sync_skipped_course` — new table

One row per course the latest runs skipped for out-of-range values or an unknown state, until
the problem is gone (BR-046).

| Column | Type | Null | Rule |
|---|---|---|---|
| `id` | `bigint` identity | no | PC-3 surrogate key, `pk_sync_skipped_course` |
| `course_google_id` | `varchar(64)` | no | Google's course id from the course list, trimmed, cut to `Course.MaxGoogleIdLength` = 64 as `Course.Import` cuts it (VR-001); unique |
| `course_name` | `varchar(750)` | yes | Google's list name trimmed and cut to `Course.MaxNameLength` = 750; null when missing or blank (VR-002) |
| `reason` | `varchar(32)` | no | `value_out_of_range` \| `unknown_course_state` (VR-003) |
| `skipped_since` | `timestamptz` | no | `observedAt` of the run that began the current streak (FR-005.2, FR-006) |
| `created_at` | `timestamptz` | no | PC-6, interceptor |
| `updated_at` | `timestamptz` | no | PC-6, interceptor |

Constraints and indexes:

| Name | Definition | Why |
|---|---|---|
| `pk_sync_skipped_course` | `PRIMARY KEY (id)` | PC-3 |
| `uq_sync_skipped_course_course_google_id` | `UNIQUE (course_google_id)` | at most one record per course (FR-005.3); the lookup key of the upsert (PC-7, PC-10) |
| `ck_sync_skipped_course_reason` | `reason IN ('value_out_of_range', 'unknown_course_state')` | closed set, no free text (VR-003) |
| `ck_sync_skipped_course_google_id` | `char_length(course_google_id) >= 1` | a record is keyed by a non-blank id (VR-001) |
| `ck_sync_skipped_course_name` | `course_name IS NULL OR char_length(course_name) >= 1` | blank is stored as null, never as `''` (VR-002) |

- No foreign keys (§1). No cascade questions arise.
- **A listed course with a blank Google id** (the reader maps a missing id to `''`) gets no
  record — it cannot be keyed and could never be removed by FR-007; it stays log-only as today.
  Classroom always returns an id, so this is a guard, not a case.
- **Personal data:** none about students. `course_name` is Google's course name and may contain
  a teacher's name (spec §7, SPEC_REVIEW m-2); it is the same value the `course` table stores
  for a loaded course. Not written to any log (SC-10).
- **Retention / erasure:** the purge (PC-11) does not touch the table (I-6); a record goes when
  the course is read, aged out or unlisted (FR-007). Participant erasure (US-038) has nothing
  to erase here. The audit list (SC-11) is unaffected.
- **Size:** bounded by the number of courses in Google's list; in practice a handful of rows.
  Loading all rows once per run is the intended access (entity model §4).

### 4.1 Write timing (spec §8, FR-005…FR-007)

| Event | Where it is written |
|---|---|
| Skip for an unknown state / out-of-range values — record created or refreshed | right after the skip decision, saved on its own (`IUnitOfWork.SaveChangesAsync`) outside any course transaction, so it is never part of — nor rolled back with — a later course |
| Full read of the course commits — record removed | inside the course's transaction (`ExecuteInTransactionAsync`), with the marks and `last_complete_read_at` |
| Aged out (`CourseResult.AgedOut`) — record removed | inside that course's transaction (the only write it makes) |
| Course list enumerated to its end — records of unlisted courses removed | after the loop, saved on its own; never when the run stopped before the end |

A failed course transaction is discarded by `IUnitOfWork.DiscardUncommittedChanges` (US-053);
a removal staged in it must be discarded with it. Since runs stop on such a failure (BR-043),
nothing later in the run depends on the record's tracked state.

## 5. Migration

- One migration, **`AddSyncDataHealthRecords`**, under
  `src/ClassroomAgent.Infrastructure/Persistence/Migrations`, after
  `20261025104345_WidenGradeColumns`.
- `Up`: `ADD COLUMN not_in_classroom_since timestamptz NULL` on `course_work`;
  `ADD COLUMN last_complete_read_at timestamptz NULL` on `course`; `CREATE TABLE
  sync_skipped_course` with the constraints and index of §4. Adding a nullable column without a
  default is a catalog-only change in PostgreSQL — no table rewrite, no backfill.
- `Down`: drops the table and both columns (the marks, skip records and read instants are lost —
  acceptable, they are re-derived by the next runs).
- The model snapshot is regenerated; no `EnsureCreated()` (PC-2).

## 6. Requirement → schema traceability

| Spec | Schema element |
|---|---|
| FR-001, FR-002, FR-003, VR-004, VR-005, AC-001…AC-006, AC-012, AC-015 | `course_work.not_in_classroom_since` |
| FR-005, FR-006, VR-001…VR-003, AC-007, AC-008, AC-011 | `sync_skipped_course` (all columns, `uq_…`, `ck_…`) |
| FR-007, AC-009, AC-010, AC-014, AC-015 | `sync_skipped_course` row removal (§4.1) |
| FR-008, AC-013 | `course.last_complete_read_at` |
| §9 Persistence, PC-2 | migration `AddSyncDataHealthRecords` |

## 7. Open questions

None. Index for Story 2/3 queries on marked items is left to those Stories (§2).

```yaml
result:
  verdict: PASS
  stage: DB_DESIGN
  story: US-058
  artifact_status: DRAFT
  artifacts:
    - docs/designs/database/US-058-db-design.md
    - docs/designs/database/US-058-entity-model.md
  next_stage: TEST_WRITING
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Course.RecordCompleteRead() gains a DateTimeOffset parameter; two existing test files call it (IncrementalSyncRuleTests, IncrementalSyncPersistenceTests) and are adapted at TEST_WRITING"
    - "No index on course_work.not_in_classroom_since; Story 2/3 add the one their queries need"
```
