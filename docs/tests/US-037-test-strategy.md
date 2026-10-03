---
artifact_type: test_strategy
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T19:26:15Z
updated_at: 2026-10-03T19:26:15Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-037-retention-purge.md
    version: null
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/designs/api/US-037-api-design.md
    version: 1
  - path: docs/designs/database/US-037-db-design.md
    version: 1
  - path: docs/designs/database/US-037-entity-model.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 3
supersedes: null
---

# US-037 Test Strategy — Retention purge

## 1. Scope

The daily retention purge (spec FR-001 … FR-018), covering:

- what it deletes: expired courses, leavers, orphaned participants, accounts,
  old audit rows;
- what it keeps: the strict boundary, and the `CreatedAt` fallback (OD-006);
- how it deletes: one transaction per unit, rollback on failure, retry on the
  next run;
- its audit event and its log lines;
- read-only mode;
- the schedule, coordination with synchronization, and shutdown;
- the schema of db-design §2 … §7.

The API design is NOT_APPLICABLE: there is no endpoint, so there are no contract
tests and no allowed-role or forbidden-role tests (TC-3 and TC-5 have nothing
to bind).

## 2. Test levels

| Level | Where | What it proves |
|---|---|---|
| Unit | `RetentionRuleTests`, `RetentionPurgeAuditEventTests`, `RetentionPurgeCoordinationTests`, `SynchronizationNeverDeletesTests` | the shared rule (FR-001/002); the audit factory (FR-010, VR-003); the in-process gate (FR-014); synchronization keeping leavers (FR-016) |
| Integration, real PostgreSQL (TC-2) | `RetentionPurgeTests`, `RetentionPurgeReadOnlyTests`, `AdminReturnsAfterPurgeTests` | the use case resolved from the host's own DI, over migrated tables, with the real `UnitOfWork`, the read-only commit backstop and EF interceptors |
| Schema (TC-2) | `RetentionPurgeSchemaTests`, plus updates to `InstallationAuditEventSchemaTests` and `CourseMembershipSchemaTests` | columns, the four constraints, both indexes, `Restrict` foreign keys |
| Host (background service) | `RetentionPurgeScheduleTests`, `RetentionPurgeLoggingTests` | first run at start, the 24-hour interval, waiting for synchronization, prompt stop, the log lines |
| Architecture | `AuditDeletionConfinementTests`, the registry assertion in `RetentionPurgeReadOnlyTests` | audit rows deleted only by the purge store; set-based deletes only there; the BR-026 declaration |

### Why the use case runs inside a real host

A purge whose deletes do not pass through `SaveChangesAsync` must be proved on
the real store and the real transaction. Substituted repositories cannot show
child-first ordering, a rollback, or `Restrict` foreign keys. The host is
started **without** its purge background service:

- each test seeds rows, then runs the use case exactly once through
  `RetentionPurgeHost.RunPurgeAsync`;
- the use case is resolved from DI, so the registration is proved too.

The production service is exercised separately by the schedule and logging
tests.

### Failure injection

A failing unit is produced by a PostgreSQL `BEFORE DELETE` trigger on one row
(`RetentionPurgeHost.FailDeletesAsync`). It is a real failure inside the real
transaction, so "the course is left whole" is observed in the database, not
assumed from a mock.

## 3. Scenarios

### Positive

- An expired course, active or archived, goes with its coursework, submissions
  and memberships.
- An expired leaver goes with their submissions in that course.
- An orphaned participant is deleted, including a leaver who had no other
  course.
- Expired accounts are deleted: an Admin, a disabled Dean, and a Dean who never
  signed in.
- Audit rows older than the cutoff are deleted.
- One audit event per run carries the five counts.
- The purge runs in all three read-only causes.
- A deleted Admin gets a new account at the next Google sign-in.
- The first run happens at start; the next one 24 hours later.

### Negative

Recent activity of any kind keeps a course:

- the course's own update;
- an item's creation;
- an item's update;
- a submission's change.

Also kept:

- a recent leaver;
- roster members, whatever their dates (v55 limitation);
- a leaver's submissions in another course;
- a participant still enrolled elsewhere;
- recently used accounts;
- audit rows on or after the cutoff, including one naming a course purged in
  the same run.

Before 24 hours there is no second run. No purge runs while a synchronization
run holds the gate.

### Boundary

The cutoff itself is kept and one tick earlier is expired, for courses,
leavers, accounts and audit rows (AC-014). The calendar-year cutoff is checked
on a leap day. Dates with an offset are compared as instants.

### Validation (VR-001 … VR-004)

- Counts are non-negative, and enforced twice: by the factory and by a check
  constraint.
- Null dates are ignored.
- With no Google date, the `CreatedAt` fallback applies (OD-006).
- Log values carry internal ids and type names only.

### Security

- No personal data in the audit trail: course and person Google ids, names,
  emails and grades of the purged data are all absent (SC-10, SC-11).
- No personal data and no exception message in the log.
- No Google call in read-only mode (TC-4, TC-5).
- `AuditEvent` stays immutable, and the audit repository stays add-only.
- No other code deletes or updates audit rows.
- The purge is declared on the BR-026 closed list.

### Persistence

The schema assertions of db-design §9:

- the migration leaves old rows valid (`OrdinaryRows_CarryNoCount`);
- each new constraint rejects its violating insert;
- both indexes exist, one of them partial;
- no new index on `app_user`;
- every crossed foreign key stays `r` (restrict).

## 4. Fixtures

- `RetentionPurgeTestData` holds the fixed cutoff arithmetic, the event names
  and the constraint names.
- `RetentionPurgeHost` provides:
  - two host shapes;
  - `RunPurgeAsync`;
  - `PurgeAuditRowsAsync` and `WaitForPurgeRunsAsync`;
  - seeding helpers for audit rows and accounts;
  - `FailDeletesAsync` / `StopFailingDeletesAsync`.
- Existing helpers are reused: `CourseRows`, `CourseWorkRows`,
  `ReadOnlyModeHost`, `SyncWorld`, `ScriptedHttpHandler`, `ManualTimeProvider`.
- Everything is synthetic, nothing reaches Google (TC-4), and every class
  creates its own database.

### Change to shared infrastructure (OD-007)

`InstallationTestHost.AuditRowsAsync` now excludes `retention_purge_run` rows.
Once IMPLEMENTATION registers the service, the purge writes one at every host
start, and about 200 existing assertions count "the audit rows of my action".
`AuditRowsAsJsonAsync`, used for "no personal data" checks, still reads every
row.

## 5. Excluded scenarios

- **Contract, authorization and antiforgery tests** — the Story has no endpoint
  (API design §4, §5).
- **Translation tests** — no user-visible string is added (spec §9).
- **Meet data** — out of scope (US-031, US-032).
- **Shutdown in the middle of a course transaction** — it cannot be staged
  deterministically without a seam inside the store. FR-015 is covered by:
  - per-unit transactions — the failure tests prove a unit is all or nothing;
  - the prompt-stop test.
- **Performance of a school-sized purge** — no NFR sets a bound (spec §9).

## 6. Known limitations

- **AC-011 at host level** uses the running host's own coordinator (it holds the
  gate as a synchronization run would). It waits until the gate is free, so it
  does not race the synchronization service. The exact gate semantics are
  proved on a private coordinator in `RetentionPurgeCoordinationTests`.
- **AC-016** has no new test; its evidence is the unchanged US-015 suite
  (`CourseAgeRuleTests`). If IMPLEMENTATION rewrites
  `RunSynchronizationUseCase.IsOlderThanRetention` on `RetentionRule`, that
  suite must stay green unchanged.
- **"Started"/"Completed" log content** asserts the event names and levels; the
  counts are asserted exactly through the audit row and the outcome, not by
  parsing the log template.

## 7. Open Decisions affecting testing

OD-007 is resolved (option 1, compile-only skeleton). None open.
