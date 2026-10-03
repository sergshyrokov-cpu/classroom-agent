---
id: US-037
epic: EPIC-5
title: Retention purge of expired courses, leavers, accounts and audit rows
slug: retention-purge
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v80.
# OD-001 … OD-005 were all resolved by the Owner on 2026-10-03, before
# activation.
---

# User Story

As the **school** (the data controller) and the **Owner** (its processor)

I want the installation to delete, once a day and by itself, every record whose
retention period N has run out — expired courses with everything in them,
leavers, accounts nobody uses and old audit rows

So that the school keeps students' personal data no longer than its written
agreement allows, even when nobody remembers to clean up and even while the
school is suspended.

---

# Business Value

`trebovaniya.md` §5 ("Хранение и удаление персональных данных", v19 … v55)
makes retention an obligation of the product, not a feature: the school is the
data controller, the data includes minors, and N is agreed in writing with the
school. PC-11 and BR-075 restate it.

Today nothing deletes anything. US-015 already reads N (`Retention:Years`,
required at start-up) and refuses to import a course whose last activity is
older than N, so the purge and synchronization never undo each other — but
the deletion itself was left to this Story. Several design records point here
(US-014 and US-015 DB designs: "no purge-scan index — US-037 adds it against
its real query").

US-031 and US-032 extend the purge to Meet data; they depend on this Story.

---

# Scope

## In scope

- A background job in the Web host (`RetentionPurgeBackgroundService`,
  package-map) that runs the purge once shortly after start-up and then every
  24 hours (OD-001).
- **Expired course** (§5 v36, PC-11): a course whose last activity is more than
  N years ago, in any state, is deleted together with its memberships, its
  coursework (assignments and materials) and its submissions with their grades.
  Last activity is computed by the same rule US-015 uses to refuse an import
  (course update; coursework creation or update; submission update). Meet
  sessions do not exist yet and are not part of the rule in this Story.
- **Leaver** (§5 v31, PC-11): a membership off the roster whose "last seen" is
  more than N years ago is deleted together with that person's submissions in
  that course, even when the course itself is kept.
- **Orphaned participant**: a `ClassroomParticipant` no remaining membership
  references is deleted.
- **Accounts** (§5 v45, v53): an Admin or Dean `AppUser` whose last successful
  sign-in — or creation, if never signed in — is more than N years ago is
  deleted, whether or not it is disabled. Rows that name it by internal id
  (audit) keep the id.
- **Audit rows** (§5, SC-11): installation `AuditEvent` rows whose own
  timestamp is more than N years old are deleted, independently of the courses
  they mention. This is the only deletion of audit rows anywhere.
- One transaction per course, child-first, without relying on cascades
  (PC-11, AD-7). A failure on one course rolls that course back, is logged, and
  the purge goes on with the rest; the next run tries again (OD-003).
- The purge never overlaps a synchronization run: if one is in progress, the
  purge waits for it to finish (OD-002).
- Each run writes exactly one `AuditEvent` — actor `system`, counts of courses,
  leavers' memberships, participants, accounts and audit rows removed, no
  personal data — including a run that removed nothing (§5 v47, SC-11).
- The purge runs in read-only mode too: it is on BR-026's closed list
  (BR-075, `PermittedServiceWrite.RetentionPurge`).
- Logging per §8: start and end of a run with its counts, one `Error` per
  failed course with internal identifiers only.
- Indexes the purge's queries need, added with their EF Core migration
  (US-014 and US-015 DB designs deferred them here).

## Out of scope

- Meet sessions and participations, and Meet in the "last activity" rule —
  US-031 and US-032 extend the purge.
- Any screen showing the last purge (OD-004).
- Erasing one person on request — an Owner procedure in v1, Epic 10 (BR-076).
- Deleting anything in Google (impossible: every scope is read-only).
- Backups — purged data leaves them as they roll over (DC-13, 30 days).
- The Control Plane: its audit rows are kept forever (SC-11, v45).
- A configurable time of day; the school's time zone setting.

---

# Acceptance Criteria

## AC-001 An expired course is deleted with everything in it

**Given** a course whose last activity — the latest of the course's update,
any of its coursework's creation or update, and any of its submissions'
update — is more than N years ago

**When** the purge runs

**Then** the course, its memberships, its coursework and its submissions are
gone from the installation database, whatever the course's state.

## AC-002 One recent item keeps the whole course

**Given** a course whose own update is older than N but one assignment was
updated (or one submission changed) within N

**When** the purge runs

**Then** nothing of that course is deleted.

## AC-003 A leaver expires on their own

**Given** a kept course with a member off the roster whose "last seen" is more
than N years ago, and another off-roster member seen within N

**When** the purge runs

**Then** the first membership and that person's submissions in this course are
deleted; the second member, everyone on the roster and the course stay.

## AC-004 An orphaned participant is deleted

**Given** a participant whose last membership was removed by this run, and a
participant who still has a membership in another course

**When** the purge runs

**Then** the first participant is deleted and the second is kept.

## AC-005 Unused accounts are deleted

**Given** an Admin and a Dean whose last successful sign-in is more than N
years ago (one of them disabled), a Dean who never signed in and was created
more than N years ago, and accounts used within N

**When** the purge runs

**Then** the three expired accounts are deleted, the others stay, and audit
rows naming the deleted accounts keep their internal id.

## AC-006 A deleted Admin can come back

**Given** an Admin account deleted by the purge whose email is still allowed
in the Control Plane

**When** that person signs in with Google

**Then** a new Admin account is created as on a first sign-in (OD-005).

## AC-007 Old audit rows are deleted, newer ones are not

**Given** audit rows older than N and rows within N, some of them naming a
course the purge deletes in the same run

**When** the purge runs

**Then** exactly the rows older than N are deleted, and no other use case,
endpoint or screen deletes or changes an audit row (SC-11).

## AC-008 Each run leaves one audit event

**When** the purge runs — whether it removed something or nothing

**Then** exactly one audit event is written with actor `system` and the counts
of courses, leavers' memberships, participants, accounts and audit rows
removed, and no name, email, grade or Google identifier.

## AC-009 The purge works in read-only mode

**Given** an installation in read-only mode (grace period expired, suspended
by the Owner, or never checked)

**When** the purge is due

**Then** it runs and deletes exactly as in normal mode, and makes no call to
Google.

## AC-010 A failure on one course does not spoil the others

**Given** a run in which deleting one expired course fails

**Then** that course is left complete (nothing of it half-deleted), one
`Error` line names it by internal id, the other expired courses are deleted,
the run's audit event counts only what was removed, and the next run retries
that course.

## AC-011 Purge and synchronization do not overlap

**Given** a synchronization run in progress when the purge becomes due

**Then** the purge starts only after the synchronization run ends, and a
synchronization run does not start while a purge is running.

## AC-012 Schedule and shutdown

**Then** the first purge runs shortly after the installation starts and the
next ones every 24 hours, driven by the injected `TimeProvider` in tests, and
the service stops promptly on shutdown without leaving a course half-deleted.

## AC-013 Synchronization still never deletes

**Given** a person who disappeared from the roster in Google

**When** a synchronization run executes

**Then** their membership and submissions stay; only the purge deletes
(PC-11).

---

# Open Decisions

All five were resolved by the Owner on 2026-10-03, before activation (each as
the recommended option). Resolutions are kept next to the question.

## OD-001 When in the day the purge runs

Options: (a) once shortly after start-up, then every 24 hours; (b) at a fixed
night hour.

**Resolution:** (a) — simple, and the school's time zone setting does not
exist yet.

## OD-002 Purge and synchronization at the same time

Options: (a) never overlap: the purge waits for a running synchronization;
(b) independent.

**Resolution:** (a) — synchronization must not update a course while the
purge deletes it.

## OD-003 A failure on one course

Options: (a) roll that course back, log it, go on with the rest; the next run
retries; (b) stop the whole run.

**Resolution:** (a).

## OD-004 A "last purge" screen for the Admin

Options: (a) none — the audit event and the log suffice; (b) a block on the
connection page, as US-017.

**Resolution:** (a) — a screen would need a new cell in the §2 permission
matrix.

## OD-005 The last Admin account

Options: (a) delete it like any other; the next Google sign-in recreates it
while the email is in `AllowedAdmin`; (b) never delete the last Admin.

**Resolution:** (a) — §5 v45 deletes accounts "независимо от отключения", and
`CompleteGoogleSignInUseCase` already creates the account on a first sign-in.

---

# Notes

- Comments in `IAuditEventRepository`, `AuditEventConfiguration` and
  `AppUserConfiguration` attribute the purge to "EPIC-10"; the catalog places
  US-037 in EPIC-5. Correcting them is a supporting change of this Story.
- `RunSynchronizationUseCase.IsOlderThanRetention` holds today's last-activity
  rule for an unknown course; the purge should share one definition rather
  than a second copy.
- Deleting an `AppUser` must also remove its ASP.NET Core Identity rows (Dean
  password hash, Admin external login); how is the Specification's and DB
  design's call.
- PC-11 known limitations stand: submissions of people still on the roster
  have no expiry of their own (v55); a student already off the roster at the
  first synchronization counts N from that run (v56).
- Every test uses synthetic data and a manual `TimeProvider`; no test calls
  Google (TC-4); deletion is proven against real PostgreSQL (TC-2).
