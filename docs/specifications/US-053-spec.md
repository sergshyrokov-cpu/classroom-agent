---
artifact_type: specification
story: US-053
version: 1
status: APPROVED
created_at: 2026-10-16T15:32:00Z
updated_at: 2026-10-16T16:02:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-053-failed-sync-run-recorded.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-053-open-decisions.md
    version: 1
supersedes: null
---

# US-053 Specification — A failed synchronization run is always recorded as failed

## 1. Overview

A synchronization run that stops on a failure must end with `SyncState`
`failed`, its diagnosis code and the step that stopped it (US-017 FR-005,
FR-006; US-031 FR-010; US-032 FR-006). Today it can stay `running`: when a
failure leaves changes staged in the run's unit of work and not committed, the
run's failure save (`SyncState.FailRun`, then `IUnitOfWork.SaveChangesAsync`)
commits the whole context, tries to write those changes again, fails again, and
the exception leaves the run. The host then writes its "exception escaped the
run" line and the row keeps `running` until the next run.

This breaks a rule an earlier Story already stated: **each `SyncState` commit
writes that row only; nothing else may be staged when it commits** (US-013
FR-006, security note S-10). This Story brings the failure path back to that
rule. It changes no requirement, no endpoint, no schema and no user-visible text
(OD-001 a).

Code facts this Specification relies on (read 2026-10-16, not re-designed here):

- `RunSynchronizationUseCase.ExecuteAsync` runs three steps — Classroom, Meet,
  Linking — inside one `try`; its `catch (Exception)` calls `FailRun` and saves.
  A cancellation by the host is caught before it and rethrown (US-013 FR-010).
- Each course (Classroom step) and each Meet page (Meet step) is written in its
  own `IUnitOfWork.ExecuteInTransactionAsync`; a course's transaction makes
  several `SaveChangesAsync` calls. A failed transaction is rolled back, but the
  change tracker is not: entries that were staged and not written stay staged.
- `UnitOfWork.SaveChangesAsync` already detaches failed `MeetingCodeLink` entries
  and their audit rows (US-032) and failed `AppUser` entries and their audit
  rows (US-048). No other entity type is handled.
- `SyncState` is written by `BeginAsync` (committed before the `try`) and is not
  modified again until `FailRun` or `CompleteRun`.
- `SynchronizationBackgroundService.RunOnceAsync` creates one DI scope per run;
  the run's `DbContext` is disposed with it after the outcome returns.

## 2. Business Goal

`trebovaniya.md` §4, Epic 6 "Настройки программы / Админ-панель", item "Состояние
синхронизации" (v80): the Admin sees the last synchronization's status and, when
it failed, a human-readable diagnosis — what happened and what to do. A run that
failed but shows "виконується" hides exactly that. Where the cause is in the
school's own data it repeats on every run, so the status would read "in
progress" for good and only the Owner, reading the log, could tell why.

(The Story cites this item as §6; it lives in §4. The text quoted is the same.)

## 3. Business Flow

1. A run starts; `SyncState` becomes `running` and is committed (unchanged).
2. A step fails — the database rejects a write, or anything else throws — while
   changes of that step are staged and not committed.
3. **New:** before the failure is recorded, every change the run staged and did
   not commit is discarded from the run's unit of work (FR-002).
4. `SyncState.FailRun` records `failed`, the diagnosis, the step and the counter;
   the save writes the `SyncState` row and nothing else (FR-003).
5. The use case returns its failed outcome; the host writes its usual failure
   line (FR-005). The Admin's "Last synchronization" block shows the failure
   (FR-006). The next run starts normally on schedule (FR-007).

## 4. Functional Requirements

### FR-001 The run still stops (OD-001 a)

A failure that is not "course gone" stops the run at the step where it happened,
exactly as US-017 FR-005, US-031 FR-010 and US-032 FR-006 say. This Story
changes only what the run records, never what it loads.

### FR-002 Uncommitted changes are discarded before the failure is recorded

In the run's failure path (the `catch` that calls `FailRun`), **before**
`FailRun` is called, every change the run has staged in its unit of work and
not committed is discarded, whatever entity it belongs to and whatever caused
the failure:

- an entry staged for insertion leaves the unit of work;
- an entry staged for update or deletion is not written by any later save of
  the run.

The run's `SyncState` stays tracked and is the entity `FailRun` modifies; a
discard that loses it, so that the failure is not written, fails AC-001.

`Application` holds no `DbContext` (AD-3). The discard is reached through the
existing `IUnitOfWork` port (indicative: a new method on it, implemented in
`Infrastructure/Persistence/UnitOfWork.cs`); the design of the method is the
implementation's, within AD-3/AD-4/AD-7 (I-2).

### FR-003 The failure save writes `SyncState` only

The save that follows `FailRun` commits the `SyncState` row and no other row
(US-013 FR-006). In particular:

- nothing of a course whose transaction was rolled back is stored by it;
- nothing of a Meet page whose transaction was rolled back is stored by it;
- no meeting code link and no audit row of the linking step that was not
  committed is stored by it (US-032 FR-006 already requires this for a link;
  this Story makes it hold for any failure, I-3);
- courses, Meet pages and links committed in earlier transactions of the run
  stay exactly as committed.

### FR-004 What the failed run records

For a failure in each step the stored row is (unchanged rules, now reached):

| Step that failed | `Status` | `LastError` | `FailedStep` | `ProcessedCount` | `LastSuccessfulRunAt`, `MeetLoadedUpTo`, `LastFullRunAt` |
|---|---|---|---|---|---|
| Classroom (a course's transaction) | `failed` | diagnosis per US-017 FR-006 — `Unexpected` for a database rejection | `Classroom` | courses committed before the failing one | unchanged |
| Meet (a page's transaction) | `failed` | `Unexpected` for a database rejection | `Meet` | courses committed in the Classroom step | unchanged |
| Linking | `failed` | `Unexpected` | `Linking` | courses committed in the Classroom step | unchanged |

`FinishedAt` is the run's clock at the failure, as today. The stored diagnosis is
a code of the closed list, never an exception message or type (US-017 FR-006,
SC-10).

### FR-005 Host logging

When the failure save succeeds, the use case returns its failed outcome and the
host writes the one failure line US-017 FR-010 defines (`Error`, diagnosis code
and exception **type** name only, run id). The "exception escaped the run" line
(`LogRunException`) is no longer written for these failures. No new log event is
added and no event id changes.

### FR-006 The Admin sees the failure

The Admin's "Last synchronization" block on the connection page (US-017 FR-007,
US-031 FR-011) is unchanged; reading the row FR-004 stores, it shows the failed
status, the "unexpected error" diagnosis and the step — not "in progress".

### FR-007 The next run

The next run is scheduled after the normal interval (US-017 FR-005) and begins
on the stored row as any run does (`BeginRun`). When the cause is gone it
completes: `Status` `completed`, `LastError` and `FailedStep` cleared, the
watermark and `LastSuccessfulRunAt` moved by `CompleteRun` as today.

### FR-008 Existing special cases keep their behaviour

The translations and detaches already in `UnitOfWork.SaveChangesAsync`
(`UniqueEmailViolationException`, `UniqueReportTemplateNameViolationException`,
`MeetingCodeLinkConflictException`, `AppUserConflictException`, and the
US-032 detach of pending links on any `DbUpdateException`) keep their
observable behaviour. This Story does not remove or refactor them (I-4).

### FR-009 Host shutdown is not a failure

A run cancelled because the host stops is still rethrown, not recorded as
failed, and its row is left as it is for the next run to replace (US-013
FR-010). The discard of FR-002 is not performed on that path.

## 5. Acceptance Criteria

Ids follow the Story. AC-007 is derived (I-3).

### AC-001 A rejected course write is recorded as a failed run

**Given** a synchronization whose second course's transaction is rejected by the
database (a constraint the course's data violates)
**When** the run executes
**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, `FailedStep` is
`Classroom`, `ProcessedCount` is 1; the first course is stored, nothing of the
second course is stored, and `LastSuccessfulRunAt` and `MeetLoadedUpTo` are
unchanged; the use case returns a failed outcome (it does not throw).

### AC-002 A rejected Meet page write is recorded as a failed run

**Given** a synchronization whose Classroom step completes and whose second Meet
page's transaction is rejected by the database
**When** the run executes
**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, `FailedStep` is
`Meet`; the courses and the first Meet page stay stored, nothing of the second
page is stored, and `MeetLoadedUpTo` is unchanged.

### AC-003 A rejected write in the linking step is recorded as a failed run

**Given** a synchronization whose Classroom and Meet steps complete and whose
linking step's write is rejected by the database
**When** the run executes
**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, `FailedStep` is
`Linking`, and what the Classroom and Meet steps committed stays.

This may pass before the change (US-032 already detaches a failed link); it is
kept as a regression test and the test report says so (Story Notes).

### AC-004 The Admin sees the failure

**Given** the state left by AC-001
**When** the Admin's connection page reads the "Last synchronization" block
**Then** it shows the failed status and the "unexpected error" diagnosis — not
"in progress".

### AC-005 The next run is unaffected

**Given** the state left by AC-001 and data that no longer violates the
constraint
**When** the next run executes
**Then** it completes, and `SyncState` is `completed` with no `LastError` and no
`FailedStep`.

### AC-006 Everything else is unchanged

**Given** the existing synchronization, Meet, linking, erasure and account
tests
**When** the suite runs after the change
**Then** it stays green — including US-032's link conflict and US-048's account
conflict handling.

### AC-007 A failure that is not a database rejection writes nothing staged (derived)

**Given** a synchronization in which an exception that is not a database
rejection is thrown inside a course's transaction after some of that course's
changes were staged and before they were saved
**When** the run executes
**Then** `SyncState` is `failed` with `FailedStep` `Classroom`, and nothing of
that course is stored — the failure save did not commit the staged changes
outside the course's transaction.

## 6. Validation Rules

No new input. The rules that already apply are unchanged:

- VR-001 `LastError` is one of the eight codes of US-017 FR-006, stored by name;
  `FailedStep` is one of `Classroom`, `Meet`, `Linking`, stored by name.
  `SyncState.FailRun` refuses any other value (existing).
- VR-002 `ProcessedCount` ≥ 0 and `FinishedAt` ≥ `StartedAt` (existing,
  `SyncState.Finish`).
- VR-003 A run is finished once: `FailRun` after `FailRun` or `CompleteRun` is
  refused (existing). The discard of FR-002 never finishes a run itself.

## 7. Security Requirements

- No endpoint, page, policy or role changes; the "Last synchronization" block
  stays on the Admin-only connection page (US-017 FR-008, SC-4).
- Read-only mode: unchanged. The run's write path stays protected by the guard at
  its start (US-013 FR-006); the discard is not a write and
  `PermittedServiceWrites` does not grow (BR-026, AD-6).
- No exception message, Google text or rejected value reaches `SyncState` or a
  log line: the stored value is a diagnosis code, the log line carries the
  exception type name only (US-017 FR-006, FR-010; SC-10).
- No test seam in production code: the way a test makes the database reject a
  write lives entirely in the test project and the test database (I-5); no
  configuration flag, hidden constraint or code path in `src/` exists to force
  a failure.
- No Google call is added; no data goes anywhere new (SC-13).
- No audit event is added: a failed run is not an SC-11 action (a manual
  synchronization start is, and is unchanged).

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| A course's transaction fails (database rejection or any exception) | Rolled back; staged changes discarded (FR-002); run `failed`, step `Classroom` |
| A Meet page's transaction fails | Rolled back; staged changes discarded; run `failed`, step `Meet` |
| The linking step fails | Staged changes discarded; run `failed`, step `Linking` |
| A transaction's commit itself is rejected | Same as a rejection inside it: nothing of it stored, run `failed` at that step |
| The host stops during a run | Unchanged (FR-009): rethrown, row left as it is |
| The failure save fails for its own reason (database unreachable) | Out of scope: the exception leaves the run, the host writes its "escaped" line, the next run replaces the row (Story, Out of scope) |
| A failure before the run's `try` (`BeginAsync`) | Unchanged; not covered by this Story |

## 9. Non-Functional Requirements

- Tests follow TC-2 (real PostgreSQL via Testcontainers, no InMemory provider)
  and TC-4 (Google ports substituted, synthetic fixtures).
- No new NuGet package; no migration; no new translation key (NFR-073 unaffected
  — the existing `Unexpected`, `Failed` and step texts are reused).
- Code conventions of `AGENTS.md` (async with `CancellationToken`, nullable
  enabled, warnings as errors).

## 10. Out of Scope

- What the run does after a failure: it still stops (OD-001 a).
- The diagnosis list, its texts and the "Last synchronization" block's layout
  (US-017 FR-006, FR-007).
- A run stopped by host shutdown (US-013 FR-010).
- The cause of any particular rejected write — each is its own defect.
- A failure save that fails for its own reason (database gone).
- Refactoring the existing US-032 / US-048 detaches in `UnitOfWork` (FR-008).
- Failures before the run's `try` (`BeginAsync`) and the read-only / connection
  skips.

## 11. Open Decisions

None open. OD-001 is resolved (a) by the Owner on 2026-10-16 — see
`docs/decisions/US-053-open-decisions.md`; its impact is FR-001.

## 12. Interpretations

- **I-1 The rule is US-013 FR-006.** The Story describes the database-rejection
  case; the requirement it breaks is the general one that a `SyncState` commit
  writes that row only. FR-002 therefore discards on every failure path of the
  run's `catch`, not only on a `DbUpdateException`.
- **I-2 Mechanism.** Discarding staged changes needs the change tracker, which
  only `Infrastructure` sees (AD-3); the use case asks for it through
  `IUnitOfWork`. The Specification names the behaviour, not the method; it must
  keep `SyncState` tracked (FR-002) and must not open a second `DbContext` for
  the failure save without a reason recorded in the implementation report.
  Entries that an earlier save inside a rolled-back transaction marked as
  committed need not be repaired: no later save of the run writes them, and the
  run's scope ends after the failure save (Overview, code facts).
- **I-3 AC-007 is derived.** A non-database exception inside a transaction after
  changes were staged would make today's failure save commit them outside the
  transaction — partial data, worse than a stuck status. The same discard fixes
  it; AC-007 makes it tested. It traces to the Story's title ("always recorded
  as failed") and to US-013 FR-006, not to a Story AC.
- **I-4 Existing detaches stay.** FR-002 may make parts of the US-032 detach
  redundant; removing them is a refactor outside the Story's trace (Git Policy).
- **I-5 How the database is made to reject a write** is the test design's choice
  (Story Notes), within TC-2/TC-4 and the "no test seam in production" rule of §7
  — for example a constraint or trigger the test adds to its own Testcontainers
  database, or synthetic data that violates an existing constraint.
- **I-6 AC-004 level.** The block's rendering of a `failed`/`Unexpected` row is
  already tested by US-017/US-031. AC-004 is proven by reading the block through
  the connection page's `Application` query (the DTO the page renders) from the
  state a real failed run left (AC-001); a page-level test is acceptable but not
  required.

## 13. Traceability

| AC | Requirements | Story |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004 (Classroom), FR-005 | AC-001 |
| AC-002 | FR-002, FR-003, FR-004 (Meet) | AC-002 |
| AC-003 | FR-002, FR-003, FR-004 (Linking), FR-008 | AC-003 |
| AC-004 | FR-006 | AC-004 |
| AC-005 | FR-007 | AC-005 |
| AC-006 | FR-008, FR-009 | AC-006 |
| AC-007 | FR-002, FR-003, I-1, I-3 | derived (title; US-013 FR-006) |

| Validation rule | Covered by |
|---|---|
| VR-001 | AC-001, AC-002, AC-003 (stored codes) |
| VR-002 | AC-001 (`ProcessedCount` = 1) |
| VR-003 | AC-006 (existing `SyncState` tests) |

## 14. Delegation note

No lookups were delegated; the `trebovaniya.md` item, the earlier
specifications (US-013, US-017, US-031, US-032) and the code facts in §1 were
read directly by the spec-writer.
