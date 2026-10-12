---
artifact_type: database_design
story: US-018
version: 1
status: DRAFT
created_at: 2026-10-12T06:11:12Z
updated_at: 2026-10-12T06:11:12Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/decisions/US-018-open-decisions.md
    version: 1
supersedes: null
---

# US-018 Database Design — Incremental synchronization

API_DESIGN was NOT_APPLICABLE (spec §1, §7, §10): no DTO maps to anything
below. Two nullable columns are added to existing tables; no table, foreign
key or index is added. One migration, `AddIncrementalSync`.

## 1. Decision: where the per-course value lives

Spec FR-002 asks for the `updateTime` the course list carried at the course's
last **complete read**, advanced only when the course's transaction commits.

**Chosen: a dedicated column `course.complete_read_update_time`.**

Considered and rejected: reusing `course.update_time`. In today's code it is
written inside the course's transaction after all three reads
(`ImportCourseAsync`), so it would behave correctly now. It is rejected
because:

- `update_time` is the Google mirror (v83: stored as Google returned it). The
  course list, the course card and the retention purge (PC-11 last activity)
  read it as Google's fact. Making it double as sync bookkeeping means any
  later change that refreshes a course row from the list without reading the
  course — for example a skipped course whose state or name is refreshed —
  would silently mark an unread course as completely read and stale the
  journal.
- A dedicated column keeps FR-002 one explicit write that a test can observe,
  and starts null for every existing row, so no course is skipped on the
  strength of a value written before this Story existed.

Cost: one nullable `timestamptz` per course.

## 2. Table `course` — one added column

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `complete_read_update_time` | `timestamptz` | yes | none | Google's course `updateTime` as listed in the run that last read and committed the course completely (spec FR-002), at storage precision (§5). Null: never completely read since this Story, or Google sent no `updateTime` at that read. |

- No index: the skip decision looks the course up by `google_id`, which
  `uq_course_google_id` already serves (PC-7).
- No check constraint: any instant, or null, is valid.
- Written only together with the course's other writes, inside the course's
  transaction (US-014 OD-009); never written for a skipped course, a course
  refused by the age rule, a course with a failed read, or a rolled-back
  transaction (spec FR-001, FR-002, §8).
- Not sensitive data (PC-9): a Google timestamp, no personal data.
- Deleted with the row by the retention purge (PC-11); nothing to add to the
  purge.

## 3. Table `sync_state` — one added column

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `last_full_run_at` | `timestamptz` | yes | none | The **start** instant of the last weekly full run that completed successfully (spec FR-006, I-3; `trebovaniya.md` §3 v90). Null: no full run has completed yet. |

New check constraint:

```sql
CONSTRAINT ck_sync_state_last_full_run_at CHECK (
  last_full_run_at IS NULL
  OR (last_successful_run_at IS NOT NULL AND last_full_run_at <= last_successful_run_at))
```

Why it always holds: the value is written only by a completed run (with its
own `started_at`), which in the same write sets `last_successful_run_at` to its
finish (≥ start). A later non-full run happens only when the stored value is
not later than its start (spec VR-002), so its finish is later still. A clock
moved back makes the next run full (I-5), which rewrites both. The existing
constraints, including `ck_sync_state_terminal_fields`, are unchanged.

## 4. Migration `AddIncrementalSync`

- `ALTER TABLE course ADD complete_read_update_time timestamptz NULL;`
- `ALTER TABLE sync_state ADD last_full_run_at timestamptz NULL;`
- add `ck_sync_state_last_full_run_at`.

No backfill. Existing installations: every course starts with no remembered
value and `sync_state` with no full-run time, so the first run after
deployment is a full run and fills both (spec §9, Story Note). `Down` drops the
constraint and both columns. Generated with `dotnet ef migrations add`
(PC-2); the model snapshot is updated with it.

## 5. Storage precision (spec VR-001, I-4)

PostgreSQL `timestamptz` keeps microseconds; `DateTimeOffset` keeps 100 ns
ticks, and Google may send finer fractions. The value is therefore normalised
**before** it is stored — truncated to whole microseconds, in UTC — and the
listed value is normalised the same way before the comparison. The comparison
then gives the same answer whether the stored value was just set in memory or
read back from the database. Truncation (not rounding) is what Npgsql applies
when writing, so the in-memory and the round-tripped value agree. The single
normalisation function is defined in the entity model
(`IncrementalSyncRule.ToStoragePrecision`) and used by both sides.

## 6. Queries

- **Remembered value lookup** (per listed course, before its reads, outside
  any transaction): `SELECT complete_read_update_time FROM course WHERE
  google_id = @id` — untracked, served by `uq_course_google_id`. Returns null
  both for an unknown course and for a course with no value; the two are
  treated alike (spec FR-001 (c)).
- **Full-run decision**: `sync_state` is already loaded by `BeginAsync`; no new
  query.

Spec §9 allows loading the values for a page of the list at once; it is not
required, because `IClassroomReader.ReadCoursesAsync` yields one course at a
time and the per-course lookup is one indexed single-row read, cheaper than
the three Google reads it replaces.

## 7. Conventions checked

PC-2 migration in the Story; PC-4 both properties configured explicitly
(`builder.Property(...)`, nullable, no length — not strings); PC-5 snake_case
names, constraint `ck_sync_state_last_full_run_at` follows the existing
`ck_sync_state_*` pattern; PC-6 `updated_at` stamping unchanged; PC-7 no new
lookup column; PC-10 the incremental rule now has its columns; PC-11 purge
unaffected.

## 8. Delegation

None: the current `Course`, `SyncState`, their configurations and the
migration list were read directly; the rule and normalisation signatures in the
entity model were compiled and exercised in a scratch project referencing
`ClassroomAgent.Domain` (build clean with `TreatWarningsAsErrors`).
