---
id: US-053
epic: EPIC-1
title: A failed synchronization run is always recorded as failed
slug: failed-sync-run-recorded
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from the US-038 security review, INFO-4, and the
# diagnosis of 2026-10-16; to be reviewed and edited by the Owner before
# activation. Aligned with trebovaniya.md v96.
---

# User Story

As an **Admin**

I want a synchronization run that failed to show as failed, with its diagnosis

So that I learn that data stopped arriving and what to do about it, instead of
seeing "in progress" while nothing is being loaded.

---

# Business Value

`trebovaniya.md` §6 (v80) — "Состояние синхронизации": on the connection page the
Admin sees the time of the last synchronization, its status and, "если она не
удалась, человекочитаемую диагностику: что случилось и что сделать". US-017
FR-005 and FR-006 turn this into code: a run that stops sets `SyncState` to
`failed` with a diagnosis code and the number of courses committed before the
stop.

The US-038 implementation noticed (security review INFO-4) that a run can stay
`running` instead. Diagnosis of 2026-10-16 (code reading, not yet proven by a
test): when the database rejects a write inside one course's transaction, the
transaction is rolled back, but the entities staged for that course stay in the
run's change tracker. The run then records the failure
(`SyncState.FailRun`, then a save), and that save tries to write the rejected
entities again, fails again, and the exception leaves the run. `SyncState` keeps
`running`; the host writes one log line with the exception type and nothing else.
The same shape exists for a Meet page's transaction. Two narrower cases were
already fixed this way (meeting code links in US-032, account rows in US-048);
the general case was not.

What the school sees: "виконується" on the connection page until the next run,
never the diagnosis. If the failure comes from the data itself (one unusual
course in the school's Classroom), it repeats on every run: the status is
"in progress" forever, the courses after the failing one and the Meet step are
never reached, and nobody but the Owner reading the log can tell why. Real school
data is less tidy than test fixtures, so the pilot is where this would appear.

No data is corrupted (the failed course's transaction is rolled back) and the next
run starts normally. The defect is that a failure is invisible. The code is brought
to the requirement; the requirement does not change.

---

# Scope

**In scope:**

- a run whose step fails because a database write was rejected — in a course's
  transaction (Classroom step), a Meet page's transaction (Meet step) or the
  linking step — ends with `SyncState` `failed`, the diagnosis `Unexpected`, the
  step that stopped it and the number of courses committed before the stop
  (US-017 FR-005, FR-006; US-031 FR-010);
- the entries of the rejected write are not written by the failure save; the
  courses and Meet pages committed before the failure stay as they are;
- the host writes its usual one line for a failed run (US-017 FR-010), not the
  "exception escaped the run" line;
- tests that make the database reject a write inside each of those three steps
  and assert the stored `SyncState` and the committed data.

**Out of scope:**

- what the run does after a failure: it still stops (US-017 FR-005) — see OD-001;
- the diagnosis list, its texts and the "Last synchronization" block
  (US-017 FR-006, FR-007) — unchanged;
- a run stopped by the host shutting down — still left as it is and replaced by
  the next run (US-013 FR-010), not recorded as failed;
- the cause of any particular rejected write (each is its own defect);
- a failure save that fails for a reason of its own (the database is gone) — the
  row cannot be written then, and the next run replaces it.

---

# Acceptance Criteria

## AC-001 A rejected course write is recorded as a failed run

**Given** a synchronization whose second course's transaction is rejected by the
database (a constraint the course's data violates)

**When** the run executes

**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, the failed step is
Classroom, the counter is 1 (the first course); the first course is stored, nothing
of the second course is stored, and `LastSuccessfulRunAt` and the Meet watermark
are unchanged.

## AC-002 A rejected Meet page write is recorded as a failed run

**Given** a synchronization whose Classroom step completes and whose second Meet
page's transaction is rejected by the database

**When** the run executes

**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, the failed step is
Meet; the courses and the first Meet page stay stored, nothing of the second page
is stored, and the Meet watermark is unchanged.

## AC-003 A rejected write in the linking step is recorded as a failed run

**Given** a synchronization whose Classroom and Meet steps complete and whose
linking step's write is rejected by the database

**When** the run executes

**Then** `SyncState` is `failed`, `LastError` is `Unexpected`, the failed step is
Linking, and what the Classroom and Meet steps committed stays.

## AC-004 The Admin sees the failure

**Given** the state left by AC-001

**When** the Admin opens the connection page

**Then** the "Last synchronization" block shows the failed status and the
"unexpected error" diagnosis — not "in progress".

## AC-005 The next run is unaffected

**Given** the state left by AC-001 and data that no longer violates the constraint

**When** the next run executes

**Then** it completes, and `SyncState` is `completed` with no `LastError`.

## AC-006 Everything else is unchanged

**Given** the existing synchronization, Meet, linking and erasure tests

**When** the suite runs after the change

**Then** it stays green — including US-032's link conflict and US-048's account
conflict handling.

---

# Open Decisions

## OD-001 Stop the run, or skip the failing course and go on?

**Question.** Today a failure that is not "course gone" stops the whole run
(US-017 FR-005): the courses after the failing one and the Meet step are not
reached. This Story keeps that and only makes the failure visible. The other
option is to skip a course whose write the database rejects and continue, like a
course that vanished — the rest of the school's data and Meet keep loading, but a
course would be silently missing apart from a counter.

- **(a) Keep stopping the run; this Story only records the failure
  (recommended).** The smallest change, no new requirements version; the Admin
  sees the failure at once, and the pilot shows whether such failures really
  happen before the behaviour is changed.
- (b) Skip the course and continue. Changes behaviour the school sees, so it
  would need a new `trebovaniya.md` version and changes to US-017 FR-005 first.

**Resolution (the Owner, 2026-10-16):** (a) — the run still stops at the
failure (US-017 FR-005 unchanged); this Story only makes the failure recorded and
visible. Whether to skip a failing course instead is reconsidered after the pilot,
if such failures actually occur.

---

# Notes

- Origin: US-038 security review INFO-4 (`docs/reviews/security/US-038-security-review.md`)
  and implementation report deviation D-2.
- Code today: `RunSynchronizationUseCase.ExecuteAsync` (the `catch` that calls
  `FailRun` and saves), `UnitOfWork.SaveChangesAsync` and its detaching of failed
  `MeetingCodeLink` and `AppUser` entries
  (`src/ClassroomAgent.Infrastructure/Persistence/UnitOfWork.cs`),
  `SynchronizationBackgroundService.RunOnceAsync`.
- AC-003 may already pass before the change: US-032 detaches a failed
  meeting code link before the failure save. It stays as a regression test; the
  test report says so rather than presenting it as red (vacuous-red check).
- The tests need a way to make the database reject one write on purpose; the
  test design chooses it (TC-2: real PostgreSQL, no InMemory provider; TC-4: no
  live Google).
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint changes); DB_DESIGN
  NOT_APPLICABLE (no schema change).
- No new NuGet package, no new requirements version (OD-001 a).
