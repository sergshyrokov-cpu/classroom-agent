---
artifact_type: specification
story: US-037
version: 1
status: APPROVED
created_at: 2026-10-03T16:51:10Z
updated_at: 2026-10-03T16:55:47Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-037-retention-purge.md
    version: null
  - path: trebovaniya.md
    version: 80
  - path: docs/decisions/US-037-open-decisions.md
    version: 1
supersedes: null
---

# US-037 Specification — Retention purge

## 1. Overview

`trebovaniya.md` §5 ("Хранение и удаление персональных данных", v19 … v56) makes
deletion after N years an obligation of the product. PC-11, SC-11, BR-026 and
BR-075 restate it. Nothing deletes anything today.

Today (verified in code):

- N is read at start-up from `Retention:Years` (`InstallationSettingsReader`);
  the installation refuses to start without it.
- `RunSynchronizationUseCase.IsOlderThanRetention` holds the last-activity rule
  for a course **not yet imported**, computed from Google snapshots; the boundary
  is strict (`activity < now.AddYears(-N)`), and a course with no date at all is
  imported.
- The stored entities carry the dates the purge needs: `Course.UpdateTime`,
  `CourseWork.CreationTime` / `UpdateTime`, `Submission.UpdateTime`,
  `CourseMembership.OnRoster` / `LastSeenAt`, `AppUser.LastSuccessfulSignInAt` /
  `CreatedAt`, `AuditEvent.OccurredAt`. All Google dates are nullable.
- All foreign keys are `Restrict` (PC-8). No table has a foreign key to
  `app_user`; audit actor and target ids are plain columns (PC-9).
- The installation keeps no separate ASP.NET Core Identity tables: the Dean's
  password hash, security stamp and lockout counters are columns of `app_user`,
  and the Admin has no external-login row (the account is matched by email).
- `PermittedServiceWrite.RetentionPurge` already exists on the closed list
  (BR-026); no use case refers to it yet.
- `SyncRunCoordinator` (Web) guarantees one synchronization run at a time and
  remembers an out-of-schedule request.
- `AuditEvent` has no field for counts; `AuditAction` has no purge action.
- `IAuditEventRepository`, `AuditEventConfiguration` and `AppUserConfiguration`
  attribute the purge to "EPIC-10"; the catalog places US-037 in EPIC-5.

Meet data (sessions, participations, meeting-code links) does not exist yet;
US-031 and US-032 extend this purge to it.

## 2. Business Goal

- Personal data of students — potentially minors — is kept no longer than the
  period N the school agreed in writing (§5, PC-11), with no human action.
- The obligation holds while the school is suspended or in read-only mode
  (BR-075).
- Old audit rows and unused accounts expire too, so the installation holds no
  identifier for longer than N (§5 v45, v53).
- Purge and synchronization never undo each other or race on one course.

## 3. Business Flow

### 3.1 A daily run

The installation starts. Shortly after, the purge runs: it fixes one cutoff
`now − N years`, deletes expired courses one by one, then expired leavers in the
remaining courses, then participants no membership references, then unused
accounts, then audit rows older than the cutoff, and writes one audit event with
the counts. 24 hours later it runs again.

### 3.2 Synchronization is running

The purge becomes due while a synchronization run is in progress. It waits; when
the run ends, the purge starts. A synchronization run that becomes due (or is
requested) while the purge is running starts after the purge ends.

### 3.3 One course fails

Deleting course X throws. Course X's transaction rolls back — the course is whole.
One `Error` line names X by internal id. The purge goes on with the other
courses and steps; the audit event counts only what was removed. The next run
tries X again.

### 3.4 Read-only

The installation is suspended. Viewing and export still work; synchronization
does not. The purge runs as in 3.1 and calls nothing in Google.

### 3.5 An Admin comes back

An Admin who has not signed in for more than N years is deleted. Their email is
still in `AllowedAdmin`. They sign in with Google; a new account is created as
on a first sign-in (OD-005). Old audit rows keep the old internal id.

## 4. Functional Requirements

### FR-001 The cutoff

Each run reads the time once from the injected `TimeProvider` at its start and
computes `cutoff = start.AddYears(-N)`, N from `Retention:Years`. A date is
**expired** when it is strictly earlier than `cutoff` — the same boundary as
synchronization (US-015 I-3), so a course on the boundary is neither refused by
synchronization nor deleted by the purge. Every step of the run uses this one
cutoff.

### FR-002 Last activity of a stored course

A stored course's **last activity** is the latest of:

- `Course.UpdateTime`;
- `CreationTime` and `UpdateTime` of any of its `CourseWork` rows (assignments
  and materials alike);
- `UpdateTime` of any `Submission` of any of its `CourseWork` rows.

Null values are ignored. If all of them are null, the course's own
`Course.CreatedAt` — when this installation first imported it — is its last
activity (OD-006, recommended option; to be confirmed at the gate).

The definition is **one** in `Application`: synchronization's refusal of an
unimported course (Google snapshots) and the purge (stored rows) apply the same
rule, not two copies of it (Story note). The design chooses how — a shared domain
or application function over the set of dates is sufficient. Synchronization's
own behaviour for a course with no dates (import it) does not change.

Meet is not part of the rule in this Story (§5 v36 lists it; US-031 adds it).

### FR-003 Expired course

A course whose last activity is expired is deleted, **in any state**
(`CourseState` is not consulted), together with — child first —
its `Submission` rows (grades are columns of them), its `CourseWork` rows, its
`CourseMembership` rows, and then the `Course` row.

### FR-004 One transaction per course

Each expired course is deleted in its own transaction (AD-7, PC-11). Rows are
removed explicitly, child first, by the purge; no foreign key is changed to
`Cascade` and the purge does not rely on a cascade (PC-8). A failure inside a
course's transaction rolls that course back completely; it is logged (FR-012)
and the run continues with the next course (OD-003).

### FR-005 Leavers in kept courses

In each course that remains after FR-003, a `CourseMembership` with
`OnRoster = false` and `LastSeenAt` expired is deleted together with that
participant's `Submission` rows in **that course's** `CourseWork` (§5 v31). The
role does not matter. Members on the roster are never touched, whatever their
dates (§5 v55 known limitation). Leavers of one course are deleted in one
transaction per course, with the same failure rule as FR-004.

### FR-006 Orphaned participants

After FR-003 and FR-005, every `ClassroomParticipant` that no `CourseMembership`
references is deleted (§5 v36). A participant with a membership in any other
course is kept. A participant still referenced by a `Submission` without a
membership cannot exist after FR-003/FR-005; if one did, the delete would fail on
`Restrict` and be handled per FR-009 — the purge does not delete submissions to
make room.

### FR-007 Unused accounts

An `AppUser` (Admin or Dean) whose `LastSuccessfulSignInAt` — or `CreatedAt` when
it never signed in — is expired is deleted, **whether or not it is disabled** and
whether or not it is the last Admin (§5 v45, OD-005). Deleting the row removes
everything Identity keeps for it (password hash, security stamp, lockout
counters), since those are columns of `app_user`. Audit rows and any other row
naming the account keep its internal id; no foreign key exists to block or
cascade (PC-9, §5 v53).

A later Google sign-in by an Admin whose email is still allowed by the Control
Plane creates a new account exactly as on a first sign-in, through the existing
`CompleteGoogleSignInUseCase` path; this Story changes nothing there (OD-005).

### FR-008 Old audit rows

Every installation `AuditEvent` whose `OccurredAt` is expired is deleted,
independently of the courses or accounts it names (§5, SC-11, PC-11). This is
the **only** deletion of audit rows anywhere: no other use case, endpoint or
screen deletes or updates an audit row, and `AuditEvent` keeps exposing no
mutator. The run's own audit event (FR-010) is written after this step and is
never caught by it.

### FR-009 Order and failure of the non-course steps

The run performs, in this order: FR-003/FR-004 (expired courses), FR-005
(leavers), FR-006 (participants), FR-007 (accounts), FR-008 (audit rows),
FR-010 (the run's audit event). FR-006, FR-007 and FR-008 each run in their own
transaction. A failure in one of them rolls that step back, is logged (FR-012),
and the run goes on with the next step. The next run repeats every step, so
anything left behind is retried (OD-003).

### FR-010 The run's audit event

Every run that reaches its end — including a run that removed nothing and a run
in which some course or step failed — writes **exactly one** `AuditEvent`
(§5 v47, SC-11):

- actor `system` (`AuditActorType.System`), no actor id, no role;
- a new action for the purge run (name chosen by the design);
- no target;
- outcome succeeded;
- no request id (no request exists);
- five counts of rows actually committed as deleted in this run:
  **courses**, **leavers' memberships** (FR-005 only — memberships deleted with
  an expired course are not counted here), **participants**, **accounts**,
  **audit rows**.

The counts are integers; the row carries no name, email, grade, Google
identifier or free text. How the counts are stored (columns of `audit_event`
used only by this action, or another integer-only form) is the DB design's call;
free text or JSON with arbitrary keys is not acceptable (SC-10, PC-9). A run
interrupted by shutdown before its end writes no event (I-6).

### FR-011 Read-only mode

The purge runs in read-only mode exactly as in normal mode (BR-026, BR-075): it
is the `PermittedServiceWrite.RetentionPurge` entry of the closed list, and the
run's audit event is part of it. The purge reads the legitimacy state for
nothing and calls no Google port. Nothing else is added to the closed list.

### FR-012 Logging

Per §8, with internal identifiers only (SC-10):

| Event | Level | Content |
|---|---|---|
| purge run started | `Information` | the cutoff |
| purge run completed | `Information` | the five counts, number of failed courses and steps |
| a course failed (FR-004, FR-005) | `Error` | course internal id, which step, exception **type** name |
| a non-course step failed (FR-009) | `Error` | which step, exception type name |
| the run waits for a synchronization run | `Information` | — |

No log line carries a course name, a participant's name or email, a grade, a
Google id or an exception message. The design chooses event ids.

### FR-013 Schedule

A background service in the Web host (`RetentionPurgeBackgroundService`,
package-map `BackgroundServices`) runs the purge once shortly after the host
starts and then every **24 hours** measured from the previous run's start
(OD-001). Time is taken from the injected `TimeProvider`, so tests advance it
without sleeping. The interval is not configurable (Out of Scope).

### FR-014 Never overlapping synchronization

The purge and a synchronization run never run at the same time (OD-002):

- a purge that becomes due while a synchronization run is in progress waits and
  starts when that run ends;
- a synchronization run that becomes due — scheduled, or requested out of
  schedule (US-019 entry point) — while the purge runs starts when the purge
  ends; a remembered request is not lost.

The exclusion is one in-process gate shared by both background services; the
design chooses whether `SyncRunCoordinator` is extended or a shared gate wraps
it. The decision and the start remain one atomic step.

### FR-015 Shutdown

Cancellation is honoured **between** units of work (courses, steps), never
inside a transaction: a course being deleted when shutdown arrives either
commits or rolls back whole. The service then stops promptly. A wait for a
synchronization run ends at once on shutdown.

### FR-016 Synchronization still never deletes

Synchronization's behaviour is unchanged: a person who disappears from the
roster keeps their membership (`OnRoster = false`) and submissions; only the
purge deletes (PC-11). The shared last-activity rule (FR-002) does not change
which unimported courses synchronization refuses.

### FR-017 Indexes

The purge's queries get the indexes they need, added by an EF Core migration in
this Story (PC-2, PC-7; deferred here by the US-014 and US-015 DB designs). The
DB design derives them from the real queries — candidates: leavers
(`course_membership` by `on_roster`, `last_seen_at`), accounts
(`app_user` by `last_successful_sign_in_at` / `created_at`), audit rows
(`audit_event` by `occurred_at`). Existing indexes on `course_work.course_id`,
`submission.course_work_id` and `course_membership.participant_id` are reused.

### FR-018 Comment correction

The comments in `IAuditEventRepository`, `AuditEventConfiguration` and
`AppUserConfiguration` that attribute the purge to "EPIC-10" are corrected to
US-037 / EPIC-5 (supporting change, Story note).

## 5. Acceptance Criteria

| Id | Criterion | Source |
|---|---|---|
| AC-001 | A course whose last activity (course update, any coursework creation/update, any submission update) is expired is deleted with its memberships, coursework and submissions, in any state | Story AC-001 |
| AC-002 | A course with an old own update but one coursework or submission change within N is kept whole | Story AC-002 |
| AC-003 | In a kept course, an off-roster member seen before the cutoff is deleted with their submissions in that course; an off-roster member seen within N, roster members and the course stay | Story AC-003 |
| AC-004 | A participant left without memberships by this run is deleted; one with a membership elsewhere is kept | Story AC-004 |
| AC-005 | Admin and Dean accounts with expired last sign-in (one disabled) and a never-signed-in Dean created before the cutoff are deleted; accounts used within N stay; audit rows naming the deleted accounts keep their ids | Story AC-005 |
| AC-006 | A deleted Admin still allowed by the Control Plane signs in with Google and gets a new account as on a first sign-in | Story AC-006 |
| AC-007 | Exactly the audit rows older than the cutoff are deleted, including rows naming a course deleted in the same run; no other use case, endpoint or screen deletes or changes an audit row | Story AC-007 |
| AC-008 | Each run — removing something or nothing — writes exactly one audit event: actor `system`, the five counts, no name, email, grade or Google id | Story AC-008 |
| AC-009 | In read-only mode (grace expired, suspended, never checked) the purge runs and deletes as in normal mode, with no call to Google | Story AC-009 |
| AC-010 | A failure on one course leaves it whole, logs one `Error` with its internal id, deletes the other expired courses, the audit event counts only what was removed, and the next run retries it | Story AC-010 |
| AC-011 | A purge due during a synchronization run starts after it ends; a synchronization run does not start while a purge runs, and a request made meanwhile is not lost | Story AC-011 |
| AC-012 | First run shortly after start-up, then every 24 hours, driven by the injected `TimeProvider`; shutdown stops the service promptly without a half-deleted course | Story AC-012 |
| AC-013 | A synchronization run in which a person left the roster keeps their membership and submissions | Story AC-013 |
| AC-014 | Boundary: a course whose last activity equals the cutoff is kept; one tick earlier it is deleted (likewise for a leaver, an account and an audit row) | derived, FR-001 |
| AC-015 | A course whose stored Google dates are all null is judged by its `CreatedAt` | derived, FR-002, OD-006 |
| AC-016 | Synchronization and the purge judge last activity by one shared rule: the existing US-015 refusal tests still pass unchanged | derived, FR-002, FR-016 |
| AC-017 | A failing non-course step (participants, accounts or audit rows) is rolled back and logged; the later steps and the audit event still run | derived, FR-009 |

## 6. Validation Rules

### VR-001 N

`Retention:Years` is validated at start-up by the existing reader (required,
positive integer). The purge does not re-validate it and has no default.

### VR-002 Dates

Null dates are ignored by FR-002; the comparison is strictly-earlier against
the one cutoff of FR-001. All dates are UTC `DateTimeOffset`.

### VR-003 Counts

Each count is a non-negative integer equal to the number of rows committed as
deleted in this run for that category; rows of a rolled-back unit are not
counted.

### VR-004 Log values

Only internal ids, counts, the cutoff, step names and exception type names
enter a log line (SC-10).

## 7. Security Requirements

- **Personal data minimisation is the point of the Story**: deletion is
  physical, from the installation database only (§5 v36); backups roll over in
  30 days (DC-13, out of scope).
- **Nothing is written to Google** — and nothing is read: the purge calls no
  Google port (Hard Stop; FR-011).
- **Audit integrity (SC-11, PC-9):** audit rows are deleted only by FR-008 and
  only when older than the cutoff; no row is ever updated; `AuditEvent` gains no
  mutator and no free-text field; the purge's own event carries counts only.
- **No personal data** in the audit event or in log lines (SC-10, FR-010,
  FR-012).
- **Read-only mode:** permitted solely through `PermittedServiceWrite.RetentionPurge`
  (BR-026, BR-075); enforced in `Application` (AD-6); the closed list is not
  extended.
- **Authorization:** no endpoint, page or user action is added; nothing is
  exposed anonymously (SC-4). The purge has no user trigger (OD-004).
- **Accounts:** deleting an `AppUser` removes its password hash with it; no
  credential survives in another table. A session cookie of a deleted account no
  longer resolves to an account and is refused by the existing per-request
  check.
- **Architecture:** the purge use case lives in `Application`, persistence in
  `Infrastructure` behind a port (AD-4); no `DbContext` in `Web` (AD-3).
- No new outbound destination (SC-13); no new package.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| a course's deletion fails | that course rolled back, `Error`, run continues, retried next run |
| a course's leaver deletion fails | that course's leaver step rolled back, `Error`, run continues |
| participant / account / audit-row step fails | that step rolled back, `Error`, next step runs |
| the run's audit event cannot be written | `Error`; the deletions already committed stay (I-5) |
| synchronization in progress | purge waits (FR-014) |
| shutdown | stop between units; no half-deleted course; no audit event for an unfinished run |
| nothing expired | run completes; audit event with all counts 0 |

Exceptions signal failures only (AD-9); "nothing to delete" is a normal outcome.

## 9. Non-Functional Requirements

- One daily run; per-course transactions keep locks short, so viewing and export
  stay responsive during a run.
- Tests: manual `TimeProvider`, synthetic data, no Google (TC-4); deletion and
  transactions proven against real PostgreSQL via Testcontainers (TC-2).
- No user-visible string is added, so no translation entries (NFR-073).
- `TreatWarningsAsErrors`; no new package.

## 10. Out of Scope

Meet sessions, participations and meeting-code links, and Meet in the
last-activity rule (US-031, US-032); a "last purge" screen (OD-004); erasing one
person on request (BR-076, Epic 10); anything in Google; backups (DC-13); the
Control Plane's audit rows (kept forever, SC-11 v45); a configurable time of day
or interval; the school's time-zone setting.

## 11. Interpretations (for the gate)

- **I-1** The 24-hour interval counts from the previous run's start, not its
  end; a run delayed by a synchronization run does not shift later runs beyond
  that delay.
- **I-2** "Shortly after start-up" means no deliberate delay: the first run is
  due at once and starts as soon as no synchronization run holds the gate.
- **I-3** Leavers are judged only in courses kept by the same run; a member of an
  expired course is counted under the course, not as a leaver.
- **I-4** Non-course steps (participants, accounts, audit rows) are each one
  transaction, not one per row; their volume is small.
- **I-5** If writing the run's audit event fails, the deletions are not rolled
  back: retention outranks the record of it, and the log still holds the counts.
- **I-6** A run cut short by shutdown writes no audit event; the next run starts
  over from the schedule after restart and records its own counts.
- **I-7** The audit event's outcome is "succeeded" even when some course failed;
  the failure is in the log (the outcome vocabulary is succeeded/refused only).

## 12. Open Decisions

OD-001 … OD-005 were resolved by the Owner on 2026-10-03 before activation and
are carried unchanged into `docs/decisions/US-037-open-decisions.md`.

Writing this Specification raised **OD-006** (a stored course with no Google date
at all), recorded there with options and impact; FR-002 and AC-015 follow its
recommended option and change if the gate chooses otherwise. It is to be
resolved at `HUMAN_SPEC_APPROVAL`.

## 13. Traceability

| AC | FR / VR |
|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004 |
| AC-002 | FR-002, FR-003 |
| AC-003 | FR-005 |
| AC-004 | FR-006 |
| AC-005 | FR-007 |
| AC-006 | FR-007 |
| AC-007 | FR-008 |
| AC-008 | FR-010, VR-003 |
| AC-009 | FR-011 |
| AC-010 | FR-004, FR-010, FR-012 |
| AC-011 | FR-014 |
| AC-012 | FR-013, FR-015 |
| AC-013 | FR-016 |
| AC-014 | FR-001, VR-002 |
| AC-015 | FR-002 |
| AC-016 | FR-002, FR-016 |
| AC-017 | FR-009, FR-012 |
