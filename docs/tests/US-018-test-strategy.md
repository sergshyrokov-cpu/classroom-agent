---
artifact_type: test_strategy
story: US-018
version: 1
status: DRAFT
created_at: 2026-10-12T06:40:00Z
updated_at: 2026-10-12T06:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-018-incremental-sync.md
    version: null
  - path: docs/specifications/US-018-spec.md
    version: 1
  - path: docs/decisions/US-018-open-decisions.md
    version: 2
  - path: docs/designs/database/US-018-db-design.md
    version: 1
  - path: docs/designs/database/US-018-entity-model.md
    version: 1
supersedes: null
---

# US-018 Test Strategy — Incremental synchronization

## 1. Scope

The skip rule, the remembered value, the weekly full run, counting and logging
(spec FR-001 … FR-010, VR-001 … VR-003, I-1 … I-6) and the schema of db-design
§2–§6. API_DESIGN is NOT_APPLICABLE: no endpoint, page, policy, audit event or
translation changes, so there are no contract, role or translation tests
(spec §7, §10).

## 2. Levels

| Level | Where | What |
|---|---|---|
| Unit (Domain) | `Application/UseCases/IncrementalSyncRuleTests` | `IncrementalSyncRule` (full-run decision, storage precision, skip rule), `Course.RecordCompleteRead`, `SyncState.LastFullRunAt` / `CompleteRun(fullRun)` |
| Unit (use case, ports in memory, TC-1) | `Application/UseCases/IncrementalSyncRunTests` | The run over `SyncWorld`: skip vs full read, nothing written for a skipped course, counters, failures, weekly full run, read-only |
| Integration (real PostgreSQL, TC-2) | `Infrastructure/Persistence/IncrementalSyncPersistenceTests` | Columns, check constraint, no backfill, repository lookup (unknown / never read / round-trip at storage precision / untracked) |
| Integration (real host, TC-2) | `Web/BackgroundServices/IncrementalSyncHostTests` | Button run = scheduled run (AC-007); completion and failure log lines carry `SkippedUnchanged`; no course id or name logged |

Existing tests updated to the approved schema: `CourseSchemaTests` (18 columns),
`SyncStateSchemaTests` (14 columns), `AppUserMigrationTests` (18 migrations, last
`_AddIncrementalSync`).

## 3. Scenarios

**Positive.** Unchanged archived course skipped (no roster, coursework or
submission read; counted as skipped, not processed); changed or never-read
archive read in full and remembers the new value; first run full and records its
start; run at exactly 7 days full; button run skips like a scheduled run.

**Negative.** Every non-archived state read in full with an unchanged
`updateTime`; listed state decides, not stored state; restored course read;
failed reads (gone / transient / permission × roster / coursework / submissions)
do not advance the value; failed full run records no full-run time; read-only
mode starts nothing and looks nothing up; blank name and unrecognised state keep
their own reports; age rule still refuses an old new course.

**Boundary.** Seven days exactly vs one tick less; 1 µs change vs sub-µs
difference (VR-001); stored full-run time one tick in the future (VR-002, I-5);
offset differences; missing value on either or both sides.

**Validation.** No new Google field (VR-004) — none added.

**Security.** Logs carry the count only, no course id or name (SC-10, FR-009);
read-only enforced in Application (AD-6, AC-009); every Google port substituted
(TC-4, AC-010). No endpoint, so no allowed/forbidden-role pair is due (TC-5).

**Persistence.** Two nullable `timestamptz` columns without default; the check
`ck_sync_state_last_full_run_at` accepts null / equal / earlier and rejects later
or without a successful run; no index; no backfill; value written through EF
Core reads back truncated to microseconds.

## 4. Fixtures

`SyncWorld` (in-memory ports) — its `CourseRepository` gains
`GetCompleteReadUpdateTimeAsync` and `CompleteReadLookups`.
`FakeClassroomReader` (host) — gains `updateTime` on `WithCourse` and
per-course `RostersRead` / `CourseWorkRead` / `SubmissionsRead`. The real host and
`PostgreSqlFixture` as elsewhere. Every course and person is synthetic.

## 5. Excluded, with justification

- **Rollback of the course transaction carrying the remembered value** (spec §8).
  The in-memory unit of work does not roll back, and a failure inside the
  transaction cannot be induced through the substituted ports. The value is a
  column of the course row written by the same `SaveChanges` that US-014 OD-009
  already proves atomic; FR-002's observable effect (a failed course does not
  advance its value) is covered for every read failure.
- **Host-stop cancellation of a full run** writes no full-run time. Covered at
  the domain level (`BeginRun`/`FailRun` never touch it, only `CompleteRun(…,
  true)` does) and by the existing US-013 cancellation tests that prove a stop
  never reaches `CompleteRun`.
- **The decision is taken once per run** (FR-005). The clock does not move
  inside an in-memory run; the use case reads it once by construction (entity
  model §5 step 1). Not separately observable.
- **No new audit event / no new permitted service write** (FR-010). Existing
  architecture and audit tests stay green unchanged; nothing new to assert.

## 6. Known limitations

The Application tests observe the full-run decision only through its effect
(reads made, `LastFullRunAt`). Host tests rely on the coordinator remembering a
request made while the first run is finishing (US-013 OD-007).

## 7. Open Decisions

OD-001, OD-002 resolved (a) before activation. OD-003 (compile-only skeleton)
raised here and resolved (a) by the Owner on 2026-10-12. None open.
