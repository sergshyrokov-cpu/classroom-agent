---
id: US-017
epic: EPIC-1
title: Retry, backoff and permission-error handling
slug: retry-backoff-permission-errors
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v80.
# OD-001 … OD-009 were all resolved by the Owner on 2026-10-03, before
# activation.
---

# User Story

As an **Admin**

I want a synchronization run to ride out Google's short hiccups by itself, to
stop at once when the school's delegation is not configured, and to tell me on
the connection page in plain words what went wrong and who has to act

So that a passing `429` does not cost a whole run, a configuration mistake is
not hammered against the shared quota every interval, and I learn about it
from the program rather than from a Dean noticing stale data.

---

# Business Value

`trebovaniya.md` §4 Epic 1 requires retry with exponential backoff for `429` /
`5xx` and a separate treatment of permission failures, which "не ретраятся, а
сохраняются в `SyncState` с человекочитаемой диагностикой и выводятся Админу".
v80 fixes the numbers and the screen.

Today (US-013 … US-015) any exception from a Google port fails the whole run,
`SyncState.LastError` holds `"RunFailed:" + <exception type name>`, the Google
client's own retries are deliberately switched off pending this Story, and
`SyncState` is shown on no screen. A school whose delegation is incomplete
therefore fails silently every interval. US-013 and US-014 list exactly this
work as out of scope "— US-017".

The quota of the Classroom API is shared by every school (§6), so bounded
retries and an immediate stop on a configuration error protect the other
schools too.

---

# Scope

## In scope

- Classifying every failure of a Google read made during a synchronization run
  as **transient**, **configuration** (permission, scope, technical account,
  key, API not enabled), **course gone** (`404` on one course) or
  **unexpected** — reusing the closed list of outcomes US-011 built for
  "Check access" (`AccessCheckStepOutcome`) wherever it fits.
- Retrying a transient failure inside the run per v80: at most 4 attempts of
  one call, pauses of about 2 s, 8 s and 30 s with random jitter; Google's own
  requested delay (`Retry-After`) honoured but capped at 2 minutes.
- Stopping the run as failed when the attempts run out, on a configuration
  failure (no retry at all), or on an unexpected failure; courses already
  committed stay. The next run keeps the normal interval (US-013 OD-003
  unchanged).
- Skipping a course that answers `404` mid-run, with a `Warning`, and going on.
- Recording in `SyncState` a **diagnosis code** instead of the exception type
  name, never Google's raw text.
- A "Last synchronization" block on the Admin's connection page
  (`WorkspaceConnection`): time of the last run, its status, the time of the
  last successful run and, after a failure, the diagnosis — what happened and
  who acts (school's super-admin or the Owner), worded as "Check access" words
  the same problem. Ukrainian and English (NFR-073). Admin only (§2 v80).
- Logging per §8: `Warning` for each retried transient failure and for a
  skipped course, `Error` for a configuration failure; internal identifiers
  only.
- Carried findings: bound the length of every Google-supplied value written to
  a log line (US-014 F-1, US-015 F-1); skip a course with a blank name with a
  `Warning` instead of failing the run (US-014 I-5).

## Out of scope

- The manual "Synchronize" button — US-019.
- The database statistics screen (Epic 5) and anything a Dean sees — US-024.
- Incremental synchronization — US-018.
- Meet event reads (Admin Reports API) — their Story applies this policy when
  it adds them.
- Retries inside "Check access" and the startup self-check: they stay
  single-shot (US-011 spec FR-005).
- A different interval after a failure; configurable retry parameters.

---

# Acceptance Criteria

## AC-001 A transient failure is retried and the run succeeds

**Given** a run whose Google read answers `429` (or `5xx`, a timeout, a dropped
connection) once or twice and then answers normally

**When** the run executes

**Then** the call is repeated after growing pauses, the run completes, every
course is imported as without the failure, `SyncState` is `completed` with no
error, and each retried failure wrote one `Warning` line.

## AC-002 Retries are bounded

**Given** a read that fails transiently on every attempt

**When** the run executes

**Then** the call is attempted exactly 4 times, the pauses follow about
2 s / 8 s / 30 s with jitter (a requested delay is honoured up to 2 minutes and
never longer), the run ends `failed` with the "Google unavailable" diagnosis,
courses committed before the failure remain, and the next run is scheduled
after the normal interval.

## AC-003 A configuration failure stops the run at once

**Given** a read refused because a scope is not authorised in domain-wide
delegation, the technical account is unknown or cannot read, the key is
unavailable or rejected, or the API is not enabled

**When** the run executes

**Then** the call is not repeated, the run stops immediately as `failed`,
`SyncState` holds the matching diagnosis, one `Error` line is written, and the
next run is scheduled after the normal interval — so a fixed delegation heals
without the Admin acting.

## AC-004 A course that disappears mid-run is skipped

**Given** a course imported by the course listing that answers `404` when its
roster, coursework or submissions are read

**When** the run executes

**Then** that course is skipped with a `Warning`, the remaining courses are
imported, and the run completes.

## AC-005 An unexpected failure is recorded without raw detail

**Given** a failure that fits none of the classes above

**When** the run executes

**Then** the run stops as `failed`, `SyncState` holds the "unexpected error"
diagnosis, and neither `SyncState` nor any log line contains Google's message
or the exception text.

## AC-006 The Admin sees the last synchronization on the connection page

**Given** an Admin on the connection page

**Then** they see the time and status of the last run and the time of the last
successful run; after a failed run, a plain-language diagnosis saying what
happened and who acts, in the user's UI language, with the same wording "Check
access" uses for the same problem; before any run, a "not run yet" state.

## AC-007 A Dean cannot see the diagnosis

**Given** a Dean

**When** they request the connection page

**Then** they are refused as for every Admin-only page today.

## AC-008 Read-only mode is unchanged

**Given** an installation in read-only mode

**Then** no run starts and no Google read is retried, and the connection page
still shows the last recorded state (viewing is allowed in read-only).

## AC-009 Google-supplied values in logs are bounded

**Given** a skipped course or an unrecognised submission state whose Google
identifier or state string is arbitrarily long

**Then** the `Warning` line carries it truncated to a fixed bound (US-014 F-1,
US-015 F-1).

## AC-010 A course with a blank name is skipped, not fatal

**Given** a course whose name Google returns blank

**When** the run executes

**Then** that course is skipped with a `Warning` and the run completes
(US-014 I-5).

## AC-011 Tests never reach Google

Every test substitutes the Google ports or the HTTP handler beneath the
adapter; pauses are driven by the injected `TimeProvider`, so no test sleeps
for real (TC-4).

---

# Open Decisions

All nine were resolved by the Owner on 2026-10-03, before activation (each as
the recommended option). Resolutions are kept next to the question.

## OD-001 How many retries for a transient failure

Options: (a) up to 4 attempts, pauses ~2 s / 8 s / 30 s + jitter, `Retry-After`
honoured; (b) more attempts, pauses up to minutes; (c) configurable by the
Owner.

**Resolution:** (a). v80 adds the 2-minute cap on a requested delay.

## OD-002 What happens when the retries run out

Options: (a) the run stops as failed, committed courses stay, the next run
continues; (b) skip the course and go on.

**Resolution:** (a) — continuing to call Google under `429` burns the quota
every school shares.

## OD-003 A permission failure

Options: (a) stop the run at once, no retries; (b) skip the course and go on.

**Resolution:** (a) — it affects the whole school, not one course.

## OD-004 The schedule after a permission failure

Options: (a) the normal interval, so the run heals by itself; (b) wait for a
successful "Check access".

**Resolution:** (a).

## OD-005 Where the Admin sees the diagnosis

Options: (a) a "Last synchronization" block on the connection page; (b) defer
display to US-024.

**Resolution:** (a). Written into `trebovaniya.md` v80 (Epic 6, §2 matrix).

## OD-006 The wording of the diagnosis

Options: (a) reuse the classification and texts of "Check access" (US-011);
(b) separate texts for synchronization.

**Resolution:** (a).

## OD-007 Network failures

Options: (a) transient, retried like `429`/`5xx`; (b) not retried.

**Resolution:** (a).

## OD-008 A course that answers `404` mid-run, and other errors

Options: (a) skip that course with a `Warning`; any other unclassified failure
stops the run with "unexpected error" instead of the exception type name;
(b) any such failure stops the run.

**Resolution:** (a).

## OD-009 Carried findings

Options: (a) fold US-014 F-1, US-015 F-1 (bounded log values) and US-014 I-5
(blank course name skipped) into this Story; (b) leave them.

**Resolution:** (a).

---

# Notes

- The diagnosis is stored as a code and translated when displayed (NFR-073);
  how the code is stored in `SyncState` is the Specification's and DB design's
  call.
- `GoogleClassroomReader` and `GoogleAccessProbe` switch the client library's
  retries off (`ExponentialBackOffPolicy.None`) with comments citing this
  Story. The probe stays single-shot; where the reader's retry lives (client
  library policy or our own loop) is a design decision, but the pauses must be
  testable under a manual `TimeProvider`.
- The background service must still stop promptly on shutdown while a retry
  pause is pending.
- Open findings US-011 F-2 (no test resolves a real Google adapter from the
  composition root) is worth closing here if the Story touches that wiring.
