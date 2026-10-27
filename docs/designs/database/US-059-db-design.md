---
artifact_type: database_design
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T11:20:00Z
updated_at: 2026-10-27T11:20:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
  - path: docs/designs/api/US-059-api-design.md
    version: 1
  - path: trebovaniya.md
    version: 101
supersedes: null
---

# US-059 Database Design — Synchronization marks courses no longer in Classroom or hidden from a new technical account, guards against an empty course list, and skips a course with negative values

**Verdict: PASS.** Four persistence changes, one migration:

1. `course.absence_mark` + `course.absent_since` — the course mark, "not in Classroom" or "not
   visible to the technical account", with its instant (FR-001, FR-004, FR-006, VR-003).
2. `course.last_listed_at` — when synchronization last saw the course on the list (FR-003).
3. `workspace_connection.technical_account_changed_at` — the technical account's last change (FR-002).
4. `sync_state.last_course_list_empty` — the empty-list flag (FR-005, VR-006).

FR-008 (negative values) changes no schema: the reader refuses the value before it reaches the
database, and the existing check constraints `ck_course_work_max_points_non_negative` and
`ck_submission_grades_non_negative` stay as the last line of defence.

Delegation: none. The designer read `persistence-conventions.md`, the entities `Course`,
`SyncState`, `WorkspaceConnection`, their configurations, `ICourseRepository`,
`IWorkspaceConnectionRepository`, `WorkspaceConnectionView`, `GetWorkspaceConnectionQuery`,
`SaveWorkspaceConnectionUseCase.StoreAsync`, `RunSynchronizationUseCase` (constructor, run order
around lines 86–118 and 436–476), the US-058 design, and the migration list (latest
`20261026202733_AddSyncDataHealthRecords`) directly.

Open finding handled: **US-059/SPEC_REVIEW/m-1** — closed by the timing rule of §6 (R1, R2).

## 1. Rationale

- **The mark lives on the course row** (§3 `Course` v100/v101: the marks and the last sighting are
  synchronization records beside the mirror). Two columns, kind and instant, held together by one
  check constraint — VR-003: no kind without an instant, no instant without a kind. One kind column
  rather than two instants, because a course carries at most one mark (FR-006.5) and Story 3 moves
  a "not visible" course to "not in Classroom" by rewriting both columns (BR-049, VR-003).
- **The sighting is its own column**, not derived from `last_complete_read_at` (US-058): a listed
  course that the run skips (unchanged archived, out of range, unknown state, blank name, `404`) is
  seen but not read (FR-003.1, AC-016, AC-018).
- **The account-change instant lives on `workspace_connection`** (§3 `WorkspaceConnection` v101:
  "время последней смены технического аккаунта"), written by the save that changes the email, in
  its existing transaction (FR-002).
- **The empty-list flag is a column of the single `sync_state` row** — one yes/no fact of the
  synchronization state, not a list (contrast `sync_skipped_course`, US-058).

## 2. `course` — new columns

| Column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `absence_mark` | `varchar(40)` | yes | none | `not_in_classroom` \| `not_visible_to_technical_account`; null = no mark (VR-003) |
| `absent_since` | `timestamptz` | yes | none | the run's `observedAt` that set the current mark; kept while the course stays absent (FR-001.2, FR-006.4) |
| `last_listed_at` | `timestamptz` | yes | none | the `observedAt` of the latest run whose list, read to its end and not empty, yielded the course (FR-003.1); null until the first such run after this migration (FR-003.3) |

Constraints:

| Name | Definition | Why |
|---|---|---|
| `ck_course_absence_mark` | `(absence_mark IS NULL AND absent_since IS NULL) OR (absence_mark IN ('not_in_classroom', 'not_visible_to_technical_account') AND absent_since IS NOT NULL)` | closed set; kind and instant always together (VR-003) |

- **No index.** The end-of-list step loads every stored course once per run (entity model §4);
  nothing in this Story filters on the new columns. Stories 2/3 add the index their queries need
  (e.g. partial on `absence_mark IS NOT NULL`).
- **Personal data:** none — a code and two instants on a course row.
- **Retention (PC-11, BR-072):** unchanged. The purge's last activity is Google-side times and Meet
  starts; it reads none of the three columns, so neither a mark nor a sighting prolongs a course
  (spec §7). A marked course is purged by the ordinary rule; the columns go with the row.
- Existing rows get null in all three: no mark, no sighting (spec FR-003.3, I-1).

## 3. `workspace_connection` — new column

| Column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `technical_account_changed_at` | `timestamptz` | yes | none | the save's `now` when an existing connection was saved with an impersonation email that differs after `NormalizeEmail` (FR-002.1, VR-002); null after the first save and until the first change (FR-002.2, FR-002.3) |

- No check constraint: `created_at` / `updated_at` come from the interceptor and are not
  comparable in a useful way (the change instant equals the save that also moves `updated_at`).
- Personal data: none — an instant; it says nothing about who the account is (spec §7).
- Audit unchanged: the save's single `WorkspaceConnectionSaved` row (SC-11).

## 4. `sync_state` — new column

| Column | Type | Null | Default | Rule |
|---|---|---|---|---|
| `last_course_list_empty` | `boolean` | no | `false` | true exactly when the latest course list read to its end was empty while courses were stored (FR-005.2, FR-005.3); untouched by a run that stops before the end (FR-005.4) |

- `NOT NULL DEFAULT false` gives the existing single row `false` — no course list has been judged
  empty yet. In PostgreSQL 11+ a constant default is a catalog-only change.
- No constraint ties it to the status columns: a failed run leaves the flag of the last list read
  to its end (FR-005.4), so any combination with `status` is valid.

## 5. Write timing

| Event | Where it is written |
|---|---|
| Account change (FR-002) | inside `SaveWorkspaceConnectionUseCase`'s existing transaction, before the audit row; rolled back with the save |
| End of list — marks, unmarks, sightings, the flag (FR-001, FR-003…FR-006) | after the `await foreach` ends normally, in the same `SaveChangesAsync` as the US-058 FR-007.3 removal of unlisted skip records — one statement batch, atomic; never when the run stopped before the end (FR-001.4) |
| Empty list (FR-005.2) | the same point: only `sync_state.last_course_list_empty := true`; no course row and no skip record changes |

A failure of that save is a run failure like any other database failure: `DiscardUncommittedChanges`,
`FailRun` (spec §8).

## 6. Instants and the account change (closes US-059/SPEC_REVIEW/m-1)

FR-006 compares a stored `last_listed_at` with `technical_account_changed_at`. For "seen before
the change" to mean "seen by an account that was replaced", each sighting must not be later than
the moment its run fixed the technical account it read as. Two rules:

- **R1 — the run instant precedes the account.** The run takes its single instant `observedAt`
  (VR-004) **before** it reads the connection (`RunSynchronizationUseCase` today takes it at ≈472,
  after the connection read at ≈86; it moves before ≈86 and is passed into the course import). Every
  value this run writes as `observedAt` — `last_listed_at`, `absent_since`, and the US-058 instants
  `last_complete_read_at`, `not_in_classroom_since`, `skipped_since` — is then not later than the
  account choice. A change saved after the account was fixed has a later instant than every
  sighting of that run, so that run's sightings count as "before the change" — correct, the list was
  read as the old account.
- **R2 — the change instant is read at the end of the list**, together with the stored courses
  (entity model §4). A change saved during a run can then only turn this run's "not in Classroom"
  into "not visible" (the safe side, the I-1 direction), never the reverse: a sighting at or after
  the change instant was written by a run that fixed its account after the change.

With R1, a course gets "not in Classroom" from FR-006.2 only if a run that read as the new account
listed it — exactly BR-049. The comparison is made on stored values (`timestamptz`, microseconds);
equal values give "not in Classroom" (FR-006.1 is strict), which needs a sighting and a change in
the same microsecond and is accepted.

R1 changes no stored meaning: `observedAt` stays one instant per run; it is only taken a few
milliseconds earlier (before `BeginAsync`'s save).

## 7. Migration

- One migration, **`AddCourseListMarks`**, under
  `src/ClassroomAgent.Infrastructure/Persistence/Migrations`, after
  `20261026202733_AddSyncDataHealthRecords`.
- `Up`: `ADD COLUMN absence_mark varchar(40) NULL`, `absent_since timestamptz NULL`,
  `last_listed_at timestamptz NULL` and `ck_course_absence_mark` on `course`;
  `ADD COLUMN technical_account_changed_at timestamptz NULL` on `workspace_connection`;
  `ADD COLUMN last_course_list_empty boolean NOT NULL DEFAULT false` on `sync_state`. All
  catalog-only; no table rewrite, no backfill (spec §9).
- `Down`: drops the constraint and the five columns (marks, sightings, the change instant and the
  flag are lost; the next runs re-derive marks and sightings, the change instant is lost for good —
  acceptable for a rollback).
- The model snapshot is regenerated; no `EnsureCreated()` (PC-2).

## 8. Requirement → schema traceability

| Spec | Schema element |
|---|---|
| FR-001, FR-004, FR-006, VR-003, AC-001…AC-003, AC-005, AC-012…AC-015, AC-017 | `course.absence_mark`, `course.absent_since`, `ck_course_absence_mark` |
| FR-003, FR-006, AC-004, AC-012, AC-014, AC-016, AC-018 | `course.last_listed_at` |
| FR-002, VR-002, AC-011 | `workspace_connection.technical_account_changed_at` |
| FR-005, VR-006, AC-006…AC-008 | `sync_state.last_course_list_empty` |
| FR-006.1, VR-004, SPEC_REVIEW m-1 | §6 R1, R2 |
| FR-008, AC-009 | none (reader); existing `ck_course_work_max_points_non_negative`, `ck_submission_grades_non_negative` |
| §9 Persistence, PC-2 | migration `AddCourseListMarks` |

## 9. Open questions

None.

```yaml
result:
  verdict: PASS
  stage: DB_DESIGN
  story: US-059
  artifact_status: DRAFT
  artifacts:
    - docs/designs/database/US-059-db-design.md
    - docs/designs/database/US-059-entity-model.md
  next_stage: TEST_WRITING
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "N-1: WorkspaceConnection.ChangeTo gains a DateTimeOffset parameter (one call site, SaveWorkspaceConnectionUseCase); tests calling it are adapted at TEST_WRITING"
    - "N-2: R1 moves the run's observedAt before the connection read; existing tests that pin the order of clock reads (if any) are adapted at TEST_WRITING"
  closed_findings:
    - id: US-059/SPEC_REVIEW/m-1
      resolution: FIXED
      note: "db-design §6: observedAt taken before the run reads the connection (R1); the change instant read at the end of the list (R2) — a mid-run change can only give the safe 'not visible'"
      ref: docs/designs/database/US-059-db-design.md
```
