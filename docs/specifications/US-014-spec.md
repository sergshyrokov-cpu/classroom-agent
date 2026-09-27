---
artifact_type: specification
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T16:33:58Z
updated_at: 2026-09-27T16:50:05Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-014-open-decisions.md
    version: 1
supersedes: null
---

# US-014 Specification — Sync courses and rosters

## 1. Overview

US-013 built a synchronization run that starts on a schedule, runs one at a time,
refuses to run in read-only mode and records itself in `SyncState`. Its pipeline
is empty by decision (US-013 OD-001), so every run today completes with a counter
of zero and the installation's database still holds no teaching data at all.

This Story is that pipeline's first step. It brings in what `trebovaniya.md` §4
Epic 1 asks for first — "список курсов, участников с их ролями в курсах" — and
creates the three entities everything downstream reads: `Course`,
`ClassroomParticipant` and `CourseMembership`. It also settles a modelling point
§3 is explicit about and that would be expensive to correct later: a Classroom
role belongs to the **membership**, not to the person, because "Один человек может
вести один курс и учиться на другом" (BR-050).

It is deliberately narrow. Coursework, materials, submissions and grades are
US-015. Retry, backoff and the separation of a permission failure from a transient
one are US-017 (OD-008). Not re-fetching what is already known is US-018
(OD-007). The age rule that keeps the purge and the next sync from undoing each
other is not applied here, because this Story can see only one of the four dates
that define a course's last activity (OD-001).

Two gaps found while writing this document were raised as OD-010 and OD-011 and
resolved by the Owner at `HUMAN_SPEC_APPROVAL` before approval, because both
change the schema: an unrecognised course state skips that one course rather than
failing the run, and a participant's email address is not unique. Both are
reflected in the requirements below.

## 2. Business Goal

**For the Dean and the Admin:** the school's courses and the people on them exist
locally, so every later screen answers from the installation's own database
instead of calling Google per question. Until this Story, EPIC-2 has nothing to
display (US-020 course list, US-021 course detail with roster, US-022 teachers and
their courses all read exactly these three entities).

**For the program:** the roster history Epic 4 depends on. Classroom gives no join
or leave dates, so BR-051 makes the membership record what synchronization
*observed* — first seen, last seen, on the roster now. Meet reports resolve a
person against "the roster on the meeting's date", which is only possible once
those observations are being collected. Every run that does not happen is a gap in
that history which cannot be reconstructed later.

**For the Owner:** the run counter stops being zero, so `SyncState` starts saying
something about whether a school's data is actually arriving (§8).

## 3. Business Flow

### 3.1 The first run of a configured school

The Admin has saved the connection (US-009) and confirmed access (US-011). A run
starts on its schedule (US-013). The read-only guard passes, the saved connection
is read, and the run asks Classroom for the school's courses — every page of them.
For each course it reads the teacher roster and the student roster, creates the
course, the people and their memberships, and commits that course as one unit
(FR-012). When the last course is committed, `SyncState` records a completed run
whose counter is the number of courses processed (FR-013).

### 3.2 A later run, nothing changed in Google

Every course and every roster is read again (OD-007: a full pass). Every write is
an upsert on the Google-side identifier, so no row is duplicated and every course
keeps its surrogate identity (FR-008). Memberships have their "last seen" advanced
to this run's instant (FR-009). The run completes with the same counter.

### 3.3 Someone leaves a roster

A student is no longer on a course's roster. Their membership is **not** deleted:
its on-roster flag becomes false and its last-seen date stops advancing (FR-010,
BR-051). Their grades and submissions — when US-015 brings them — stay in the
journal, because a journal is an accounting document (§5). The membership is
deleted only by the retention purge, on its own last-seen expiry (PC-11, US-037).

### 3.4 Someone returns to a roster

The same membership is reused: the on-roster flag becomes true again and the
last-seen date advances. The first-seen date is never rewritten (FR-009), so "when
did we first see this person on this course" survives an absence.

### 3.5 Google stops returning a course

Nothing happens to it. The course, its memberships and its participants stay
exactly as they were, and no row is marked, hidden or deleted (FR-011). §5 is
explicit that a course is deleted by the purge, by age, "в любом статусе —
архивирован, активен или уже не возвращается Google".

### 3.6 Read-only mode

The guard already refuses the run before anything else happens (US-013 FR-005), so
no Classroom call is made at all and nothing is written (FR-015). The school
resumes importing by itself once it leaves read-only mode.

### 3.7 A run fails part-way

Google stops answering after some courses are already committed. Those courses are
complete — no course exists with a half-written roster (FR-012). `SyncState`
records a failed run with a diagnosis that is a category and a type name, never a
Google error text (US-013 FR-008, SC-10). The next run imports the rest, and
because every write is an upsert, nothing is duplicated (FR-008). This Story does
not classify the failure and does not retry it (FR-014, OD-008).

### 3.8 An empty course list

A run that receives no courses at all completes **successfully** with a counter of
zero. It is not an error state (FR-003). On a live domain it most likely means the
technical account lacks the Workspace roles §7 item 10 leaves unverified — a
configuration problem to check before suspecting this code (OD-003).

## 4. Functional Requirements

### FR-001 What this Story adds to a run

The run use case gains its first pipeline step, at the place US-013 left for it in
`RunSynchronizationUseCase`. The step runs **after** the read-only guard and the
connection check, inside the existing run lifecycle: this Story adds no second
background service, no second schedule and no second state record.

`PermittedServiceWrites` does not grow. The new writes happen inside a use case
that calls `IReadOnlyModeGuard` first, which makes them a protected write path
rather than a service write permitted in read-only mode (US-013 FR-006, BR-026).

### FR-002 The Classroom port

A port `IClassroomReader` is declared in `Application/Ports` and implemented in
`Infrastructure/Google`. The name is fixed by `architecture.md` AD-4,
`package-map.md` and TC-4 and is not chosen here.

- No Google SDK type appears in its signature, and none crosses into `Application`
  or `Domain` (AD-4). The port speaks in the Application's own models.
- It carries the `IGoogleDataPort` marker, so the rule of US-007 FR-007 binds it:
  a use case holding it also takes `IReadOnlyModeGuard` and calls it first.
- Its implementation reaches only Google (SC-13), resolves the service-account key
  from `ISecretStore` per request and impersonates the technical account from
  `WorkspaceConnection` (BR-015, BR-031, SC-7, SC-8). `GoogleAccessProbe` is the
  precedent for all of this, including a single injected `HttpMessageHandler` so
  tests drive the adapter offline (TC-4) and the client library's own retry
  switched off (retry policy is US-017, OD-008).
- It adds no scope. Courses and rosters are covered by
  `classroom.courses.readonly`, `classroom.rosters.readonly` and
  `classroom.profile.emails`, already in `GoogleDelegationScopes` (§6, v25).

### FR-003 Reading the school's courses

The step asks Classroom for the school's courses:

- with **no** `courseStates` filter and no per-user filter (OD-002, OD-003);
- paging through **every** page of the response until Classroom reports no further
  page. A single-page implementation is a defect — the prototype lost student data
  exactly this way before paging was added (`google_api_OLD.py`);
- with **no** age filter: every course returned is imported (OD-001);
- a response containing no courses is a successful outcome with nothing to import,
  never an error (§3.8).

A course whose state is outside the five values §3 lists is **skipped**, and the
run completes successfully with the remaining courses (OD-010). The skip is logged
at `Warning` — not `Information`, so it does not sink into the ordinary lines of a
run — carrying the unrecognised state string and the course's Google id, never the
course name (SC-10, FR-016). Inventing a sixth state value is forbidden
(`AGENTS.md` Hard Stops: no business rule that no artifact defines), and failing
the run would stop a school receiving any data because of one unfamiliar course.
Such a course is absent from the Dean's list until §3 is extended; the `Warning`
line is what makes that absence discoverable, because `SyncState` has one counter
only and cannot carry a count of skipped courses (FR-013).

### FR-004 Reading a course's roster

For each imported course the step reads **both** rosters:

- the course's teachers, and the course's students, as two separate Classroom
  resources, each paged to the end;
- the course owner is not a substitute for the teacher roster. Co-teachers are
  imported (§3: "курс может иметь и других со-преподавателей — см.
  `CourseMembership`"). The prototype's owner-only model is not the requirement —
  it never called `courses().teachers().list` at all;
- for each roster entry the step records the Google `userId`, the email address and
  the name Classroom gives (§3). An entry with no email address, or with an address
  outside the school's domain, is imported all the same (OD-006);
- a roster read that fails does **not** cause the memberships of that course to be
  marked off the roster — see FR-010.

### FR-005 `Course`

A `Course` entity holds what §3 lists for a course: the Google course id, name,
section, description heading, description, room, the owner's Google user id,
creation and update times, state, the alternate link, the teacher folder and
`calendarId`.

- The Google course id is the **upsert key**, stored in its own column with a
  unique index; the primary key is the surrogate `long Id` (PC-3).
- The owner is stored as the Google `ownerId` in a plain column, with **no**
  foreign key to `ClassroomParticipant` (OD-004).
- "Курс" is always a Classroom course, never a year of study, and the program
  assumes nothing about what a school means by one (§3).

### FR-006 `ClassroomParticipant`

A `ClassroomParticipant` entity holds a person who came from synchronization: the
Google `userId`, the email address and the name (§3).

- The Google `userId` is the upsert key, with a unique index; the primary key is
  the surrogate `long Id` (PC-3).
- The email address is **optional**, because OD-006 imports an entry whose profile
  carries none.
- The person carries **no** Classroom role. The role lives on the membership
  (BR-050, §3) — a `ClassroomParticipant` with a role column is a defect.
- The same person on several courses is **one** row.
- The email address is **not unique**: it is a plain indexed column (OD-011). The
  Google `userId` remains the sole identity of a person. An import therefore cannot
  fail on an address collision, which Google permits when an account is deleted and
  its address later reused. Epic 4 resolves an address to a person through the
  roster of the course **on the meeting's date**, as PC-12 already requires — never
  against the participant table globally.
- `MeetParticipation` must **not** gain a foreign key to this entity: PC-12 calls
  such a migration a finding, and the person is resolved by email against the
  roster on the meeting's date (BR-051).

### FR-007 `CourseMembership`

A `CourseMembership` entity is one person's participation in one course, carrying:

- the course and the participant;
- the Classroom role, `teacher` or `student` — a closed vocabulary of exactly two
  values (§3, BR-050). It is not an application permission: in the first version
  it is a fact in statistics, not a right (§3);
- the observation fields of BR-051: when synchronization **first** saw the person
  on that roster, when it **last** saw them, and whether they are on it **now**;
- its own uniqueness: one row per (course, person, role).

A person who teaches one course and studies on another has one
`ClassroomParticipant` row and two `CourseMembership` rows with different roles
(BR-050).

### FR-008 Upsert and identity

Every write is an upsert on the Google-side identifier (BR-041, PC-10, PC-3):

- a course, a participant or a membership already known is **updated in place** and
  keeps its surrogate identity, so anything referring to it still does;
- a repeated run with nothing changed in Google creates no row and duplicates
  nothing;
- Google identifiers are never the primary key — they are strings owned by someone
  else (PC-3).

### FR-009 What a membership observes

On every run in which a person is seen on a course's roster:

- if no membership exists for that (course, person, role), one is created with
  first-seen and last-seen set to this run's instant and the on-roster flag true;
- if one exists, its last-seen becomes this run's instant and its on-roster flag
  becomes true. **The first-seen date is never rewritten**, including after an
  absence (§3.4).

Both instants are the program's own observation of when it looked, not a date from
Google — Classroom supplies no join or leave dates at all (BR-051). Everything
before the first run counts as having started on the day of that run: the
documented accuracy limit, "точность — частота синхронизации" (§3), not a defect.

### FR-010 Leaving a roster

After a course's rosters have been read **successfully and completely**, every
membership of that course that this run did not see is marked as no longer on the
roster: the on-roster flag becomes false and the last-seen date is left where it
was.

- The membership is **never deleted** (BR-051, §5 v31). Only the retention purge
  deletes it, on its own last-seen expiry (PC-11, US-037).
- The participant is never deleted either, whatever memberships remain.
- If reading a course's roster failed, **no** membership of that course is marked:
  a failed read means the roster is unknown, not empty. Marking on a failure would
  record a false observation that later runs cannot distinguish from a real
  departure.
- A course whose roster is legitimately empty does mark its memberships off the
  roster — an empty roster is an answer, a failed read is not.

### FR-011 A course Google no longer returns

Nothing is done to it. The course, its memberships and its participants are left
untouched, and synchronization marks, hides and deletes nothing (§5 v36, PC-11).
No status outside the five Classroom values is invented for "no longer returned" —
§3 defines no such value.

### FR-012 The transaction boundary

One course is one transaction, through `IUnitOfWork.ExecuteInTransactionAsync`
(OD-009, AD-7): the course, the participants it introduced and its memberships —
including the off-roster marks of FR-010 — are committed together.

- A run that fails on one course leaves the previously committed courses complete
  and no course half-written.
- Transactions begin and end in `Application`; repositories stage changes and never
  call `SaveChangesAsync` (AD-7).

### FR-013 The run counter

`SyncState.ProcessedCount` reports the number of **courses processed** (OD-005):
courses whose transaction committed. `SyncState` is not redesigned, gains no
column, and its migration is not amended — participants and memberships are not
counted in it (§3's "счётчик" is singular; US-013 spec I-8).

### FR-014 A failure inside the step

This Story does not classify a Google failure, does not retry one and produces no
Admin-facing diagnosis (OD-008):

- a failure reading courses or a roster propagates out of the step;
- US-013's existing handler records the run as failed with `RunFailed:<exception
  type>` and leaves the last **successful** run instant untouched (US-013 FR-008);
- `OperationCanceledException` at host shutdown stays a normal stop, not a failed
  run (US-013 FR-010);
- the service does not stop and the host is not taken down (US-013 AC-006).

Consequently **NFR-010 and NFR-011 are not satisfied by this Story** — retry with
backoff for `429`/`5xx`, the no-retry rule for permission failures and the
diagnosable message for the Admin all arrive with US-017.

### FR-015 Read-only mode

No call is made through `IClassroomReader` in read-only mode, including the token
request, and no course, participant or membership row is written or updated
(BR-026, SC-5, AD-6).

The refusal is the one US-013 already implements — `IReadOnlyModeGuard` called as
the first statement of the run use case — and is enforced in `Application`, never
by not registering something or by hiding UI (AD-6, SC-8). This Story adds no
second enforcement point and no new entry on BR-026's closed list.

### FR-016 Logging

Inside the existing run, per DC-10 and SC-10:

- the run's start and finish lines stay as US-013 defines them, the finish line
  carrying the counter of FR-013;
- every line written by this step carries the run identifier (DC-10, §8);
- **no line carries** a person's name, an email address, a course name, a person's
  Google id, a grade, a key, a connection string or a raw Google error object. A
  line may reference a course or a participant by **internal** id (SC-10);
- counters and states only: how many courses, how many memberships marked off a
  roster.

### FR-017 Persistence

Three new tables and **one** EF Core migration in this Story (PC-2): `Course`,
`ClassroomParticipant`, `CourseMembership`, with their explicit mappings (PC-4),
their unique indexes on the Google identifiers (PC-3) and the uniqueness of
FR-007. No `EnsureCreated()`, no schema change outside the migration.

No existing table is altered: `SyncState`, `WorkspaceConnection`, `AppUser`,
`LegitimacyState` and `AuditEvent` keep their shape, and the Control Plane schema
is untouched. `DB_DESIGN` checks the new columns against PC-11 as well as §3,
because the purge deletes by the course as a unit and a leaver by their own
last-seen date.

### FR-018 Surface and authorization

This Story adds **no** endpoint, **no** Razor page, **no** authorization policy and
**no** route. SC-4's anonymous list is unchanged and the US-008 endpoint
enumeration test gains no row. Nothing here is reachable from a web request at all
(NFR-001); the screens are EPIC-2 and Epic 5, the manual trigger is US-019.

### FR-019 Audit

No audit row and no audit vocabulary change. A **scheduled** run writes no audit
event (US-013 FR-017); §5 audits the *manual* start of a synchronization, which is
US-019. `AuditAction`, `AuditTargetType` and their check constraints do not grow,
so no constraint-amending migration is needed.

### FR-020 Packages, wiring and localization

- **No NuGet package is added.** The Google Classroom client is already referenced
  by `ClassroomAgent.Infrastructure` and used by `GoogleAccessProbe`.
- The port is registered in the installation host's composition root beside the
  existing Google adapter; the step is resolved inside the run's scope as US-013's
  service already does.
- **No translation key is added**: nothing in this Story reaches a screen
  (FR-018). Data from Google is never translated (NFR-073).

## 5. Acceptance Criteria

| AC | Statement (from the Story) | Covered by |
|---|---|---|
| AC-001 | A run imports the school's courses, paged, with the §3 fields and the state as Classroom reports it | FR-003, FR-005, VR-002, VR-005 |
| AC-002 | A second run duplicates nothing and updates in place | FR-008, VR-006 |
| AC-003 | Each course's roster is imported with the role on the membership, co-teachers included | FR-004, FR-006, FR-007, VR-003, VR-004 |
| AC-004 | A membership records first seen, last seen and on-roster now; it is never deleted; a return reuses it | FR-009, FR-010, VR-004 |
| AC-005 | A course Google no longer returns is left alone | FR-011 |
| AC-006 | Read-only mode never reaches the new port and writes nothing | FR-001, FR-015 |
| AC-007 | A failure part-way leaves a consistent database and a failed `SyncState` | FR-012, FR-014 |
| AC-008 | No personal data in a log; nothing leaves the installation; no audit row | FR-016, FR-019, S-05, S-08 |
| AC-009 | Tests never reach Google; the schema is tested on real PostgreSQL; paging and the BR-051 sequence are covered | FR-002, FR-017, §9 |

## 6. Validation Rules

### VR-001 Data returned by Google is external input

Every value Classroom returns is validated before it reaches business logic, on
the same footing as a request body (§8, SC-10, `package-map.md`). A value that
fails validation never reaches the domain entity, and the rejected value is not
written to a log (SC-10).

### VR-002 Course fields

- The Google course id is required, non-empty after trimming, and bounded in
  length. A course without one is not importable — it is the upsert key (PC-3).
- The name is required; section, description heading, description, room, the
  alternate link, the teacher folder fields and `calendarId` are optional, because
  Classroom does not guarantee any of them.
- Every string is bounded in length; `DB_DESIGN` fixes each bound. A longer value
  is **truncated**, not refused: a verbose course description must not make a run
  fail at the commit (the US-013 precedent for `SyncState.LastError`).
- The state is one of `ACTIVE`, `ARCHIVED`, `PROVISIONED`, `DECLINED`, `SUSPENDED`
  (§3), stored as Classroom reports it (OD-002). The vocabulary is closed and
  enforced by a check constraint. A value outside it makes that one course
  unimportable: the course is skipped and the run still completes (OD-010, FR-003).
- Creation and update times are stored as Google gives them, in UTC (PC-6 governs
  the school's time zone for display, not for storage — see I-1).

### VR-003 Participant fields

- The Google `userId` is required, non-empty after trimming, and bounded. It is the
  upsert key.
- The email address is **optional** (OD-006). When present it is stored trimmed and
  lower-cased, the way `WorkspaceConnection` normalises an address, so that Epic 4's
  matching by email is not defeated by case. An address outside the school's domain
  is stored, not rejected.
- The name is optional and bounded; it is stored as one string, as Classroom
  supplies it (I-2).
- The email is **not unique** (OD-011): a non-unique index, because Google permits
  a deleted account's address to be reused by a new `userId`, and a unique index
  would turn that into a failed import for the whole school. Identity is the
  `userId`.

### VR-004 Membership fields

- The role is exactly one of `teacher` or `student` (§3, BR-050), enforced by a
  closed vocabulary and a check constraint.
- First-seen and last-seen are required; last-seen is never earlier than
  first-seen.
- The on-roster flag is required.
- One row per (course, person, role), enforced by a unique index (FR-007).

### VR-005 Paging

Every Classroom list read follows the continuation token to the end. The page size
is a constant in the adapter, not an installation setting: it is a property of how
this program talks to Google, not a value a school agreed (AD-10 forbids
hard-coding *configuration*, which this is not). NFR-002's page sizes govern the
program's **own** endpoints and do not apply to a Google call.

### VR-006 Time

Every instant the step records — a membership's first-seen and last-seen, the
run's own instants — comes from the injected `TimeProvider`, never from
`DateTime.Now` or `DateTimeOffset.UtcNow`, and is stored as UTC (PC-6). A single
run uses one instant for all its observations, so two memberships written by the
same run carry the same last-seen value.

### VR-007 External input surface

This Story introduces no HTTP input: no request body, no query or route parameter,
no uploaded file (FR-018). Its only external input is the data Google returns,
governed by VR-001.

## 7. Security Requirements

- **S-01** The read-only refusal is enforced in `Application` through
  `IReadOnlyModeGuard`, called first, and no call reaches Google when it refuses
  (SC-5, AD-6, FR-015).
- **S-02** The service-account key is resolved from `ISecretStore` per request and
  never logged, returned, stored in the installation database or accepted through
  the UI (SC-7, BR-033). This Story changes nothing about key handling.
- **S-03** Google is read as the technical account of `WorkspaceConnection`, never
  as a person and never through an Admin's OAuth session (BR-015, BR-031, SC-8).
- **S-04** Every scope used is read-only and already on the fixed list; the program
  writes nothing to Google Workspace (Hard Stop, NFR-021, BR-030, SC-8).
- **S-05** Imported data is written only to the installation's own database.
  Nothing is sent to any destination outside the closed list of SC-13 — today
  Google and the Control Plane — and the Control Plane receives none of it.
- **S-06** No personal data reaches a log: no name, email, grade, course name or
  person's Google id. A line may carry an internal id, a count or a state (SC-10,
  FR-016).
- **S-07** A failure diagnosis stored in `SyncState` stays the category-and-type
  string US-013 defined; no Google error text, no stack trace and no rejected value
  is stored or logged (SC-10, FR-014).
- **S-08** This Story stores personal data of students, potentially minors. It adds
  no way to read it: no endpoint, no page, no export (FR-018). Access to that data
  stays limited to the Admin and Dean roles through the screens later Stories add.
- **S-09** All data returned by Google is validated before it reaches business
  logic, and the rejected payload is never logged (SC-10, VR-001).
- **S-10** No audit row is written by a scheduled run, and no audit vocabulary
  grows (SC-11, FR-019).

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Read-only mode | No Google call, no write, a log line only; the run is skipped as US-013 already specifies (FR-015). |
| No `WorkspaceConnection` saved | The run is skipped before this step runs (US-013 FR-005). |
| Classroom returns no courses | **Success** with a counter of zero — not an error (FR-003, §3.8). On a live domain, suspect the technical account's Workspace roles first (OD-003, §7 item 10). |
| A Classroom read fails (any status) | The failure propagates; US-013 records the run as failed with `RunFailed:<type>`; the next run retries by simply running again. No classification and no in-run retry here (FR-014, OD-008). |
| A course's roster read fails | That course is not committed, and **no** membership of it is marked off the roster (FR-010). |
| A course field fails validation | The value is truncated where VR-002 allows; a missing required identifier makes that course unimportable. |
| A `courseState` outside the five §3 values | That one course is skipped and the run completes with the others; one `Warning` line carries the state string and the course's Google id, never its name (OD-010, FR-003). |
| Host shutdown during the step | `OperationCanceledException` is a normal stop, not a failed run (FR-014, US-013 FR-010). |
| An exception anywhere in the step | Never takes the host down (US-013 AC-006). |

## 9. Non-Functional Requirements

- **NFR-001** Nothing here is reachable from a web request; the step runs inside
  the existing `BackgroundService` (FR-001, FR-018).
- **NFR-003** The system holds several years of courses per school. The indexes
  this Story creates are those of FR-005 … FR-007; `DB_DESIGN` adds the indexes the
  purge and EPIC-2 queries need rather than leaving them to a table scan (PC-7).
- **NFR-010 / NFR-011 are explicitly not satisfied here** — retry, backoff and the
  permission-failure rule are US-017 (FR-014, OD-008). This Specification states
  it rather than implying coverage.
- **NFR-021 / BR-030** Read-only access to Google, always (S-04).
- **NFR-062** .NET 10, C#, nullable enabled, warnings as errors.
- **NFR-073** No user-visible string is added (FR-020).
- **Testing** (TC-2, TC-4, TC-5): `IClassroomReader` is substituted in every test
  with synthetic fixtures — never a real roster, name, email, key or school domain;
  the three tables and their constraints are tested against real PostgreSQL through
  Testcontainers, never the InMemory provider; paging is covered by a fixture
  returning more than one page; the BR-051 sequence — seen, gone, seen again — is
  covered with the first-seen date asserted unchanged; the read-only refusal is
  tested in the Application layer.
- **Known limitation (OD-001).** Until the age rule exists, a fresh installation
  may import a course whose data in Google is already older than the retention
  period. Nothing is lost by this while no code deletes data (US-037).

## 10. Out of Scope

- `CourseWork`, course materials, `Submission` and grades — **US-015**, including
  the part of BR-051 that gives a student with submissions but no roster sighting
  an off-roster membership, which needs submissions to exist.
- Retry, backoff, the no-retry rule for permission failures and the Admin-facing
  diagnosis — **US-017** (OD-008).
- Not re-fetching already-known participants (BR-042) — **US-018** (OD-007).
- The "course older than N years" rule and reading the retention period —
  deferred by OD-001.
- The retention purge itself — **US-037** (PC-11).
- Every screen showing this data — **EPIC-2** (US-020 … US-022) and Epic 5
  (US-024).
- Meet sessions, meeting codes and their linking — Epic 4 (US-031, US-032).
- Starting a run from the UI, its endpoint, policy and audit row — **US-019**.
- Any change to the six scopes, the key handling or impersonation — US-010 /
  US-011, reused unchanged.
- Any write to Google Workspace — a Hard Stop, not a scope choice.

## 11. Open Decisions

Full text, options and impact: `docs/decisions/US-014-open-decisions.md` (v1).

**Resolved by the Owner on 2026-09-27 as option 1, before activation** —
OD-001 (no age rule here), OD-002 (no `courseStates` filter), OD-003
(`courses.list` without a per-user filter; roles verified at deployment), OD-004
(`ownerId` a plain column), OD-005 (the counter counts courses), OD-006 (the
roster imported as given), OD-007 (a full pass; BR-042 is US-018), OD-008 (no
failure classification; US-017 owns it), OD-009 (one transaction per course).

**Raised while writing this Specification and resolved by the Owner on 2026-09-27
at the gate**, before approval and while this artifact was still `DRAFT`:

- **OD-010** — a `courseState` outside the five §3 values: that one course is
  skipped, the run completes, and one `Warning` line records it (FR-003, VR-002,
  §8). The closed vocabulary keeps its check constraint.
- **OD-011** — a participant's email address is **not** unique; a plain indexed
  column, with the Google `userId` as identity (FR-006, VR-003). The decision
  corrected the option's originally stated cost: PC-12 already resolves an address
  through the roster **on the meeting's date** (BR-051), so Epic 4 is not left
  without a rule, while a unique index would let a legitimately reused address stop
  the whole school's import.

No Open Decision of this Story is unresolved. `API_DESIGN` is unaffected either
way, since this Story changes no endpoint (FR-018).

### Interpretations

Where `trebovaniya.md` does not answer directly, this Specification fixes the
reading. Each is a candidate for correction at the gate.

- **I-1 Google's times are stored as given, in UTC.** PC-6 fixes the school's time
  zone for presentation; a course's creation and update times are facts from
  Google and are not rebased on import.
- **I-2 A participant's name is one string.** Classroom supplies a full name
  (`profile.name.fullName` in the prototype's evidence); §3 says "имя" and no
  requirement uses given and family names separately, so it is not split.
- **I-3 The observation instants are the program's, not Google's.** BR-051 makes
  first-seen and last-seen records of when synchronization looked, so they come
  from the injected clock (VR-006), not from any Google timestamp.
- **I-4 A course has no "gone from Google" flag.** §3 defines five states and
  nothing else; AC-005 therefore leaves such a course untouched rather than marking
  it (FR-011).
- **I-5 A course counts as processed when its transaction commits.** A course whose
  roster read failed is not counted, so the counter never overstates what was
  imported (FR-013).
- **I-6 A failed roster read leaves the roster unknown, not empty.** Marking every
  membership of that course off the roster would record a departure that never
  happened and that later runs cannot tell from a real one (FR-010).
- **I-7 An empty roster is an answer.** A course Classroom reports with no students
  does mark its memberships off the roster — the distinction from I-6 is between a
  read that succeeded and one that did not.
- **I-8 One instant per run.** All observations of one run carry the same last-seen
  value, so "seen in the same run" is expressible in a query.
- **I-9 Uniqueness of a membership includes the role.** §3 allows one person two
  relationships to two courses; within one course, a person listed on both rosters
  is a Classroom reality the model represents as two memberships rather than
  choosing one role for them.
- **I-10 The page size is not configuration.** VR-005: it describes how this
  program talks to Google, so AD-10's ban on hard-coded configuration does not
  reach it, and DC-3 gains no key.

## 12. Traceability

| Acceptance Criterion | Functional requirements | Validation rules | Security |
|---|---|---|---|
| AC-001 courses imported | FR-002, FR-003, FR-005 | VR-001, VR-002, VR-005 | S-03, S-04, S-09 |
| AC-002 idempotent | FR-008 | VR-006 | — |
| AC-003 roster with roles | FR-004, FR-006, FR-007 | VR-003, VR-004 | S-08, S-09 |
| AC-004 observation fields | FR-009, FR-010 | VR-004, VR-006 | — |
| AC-005 course gone from Google | FR-011 | — | — |
| AC-006 read-only | FR-001, FR-015 | — | S-01, S-02 |
| AC-007 consistent on failure | FR-012, FR-014 | — | S-07 |
| AC-008 no personal data out | FR-016, FR-018, FR-019 | VR-007 | S-05, S-06, S-07, S-10 |
| AC-009 tests | FR-002, FR-017 | VR-005 | — |

| Requirement | Source |
|---|---|
| FR-001 | §4 Epic 1; US-013 FR-005, FR-006; BR-026 |
| FR-002 | AD-4; TC-4; `package-map.md`; §6 v25; BR-015, BR-031; SC-7, SC-8, SC-13 |
| FR-003 | §4 Epic 1; §3; OD-001, OD-002, OD-003, OD-010 |
| FR-004 | §3 (`CourseMembership`, co-teachers); §6 (rosters, profiles); OD-006 |
| FR-005 | §3 (Course); PC-3; OD-004 |
| FR-006 | §3 (ClassroomParticipant); BR-050; PC-3, PC-12; OD-006, OD-011 |
| FR-007 | §3 (CourseMembership); BR-050, BR-051 |
| FR-008 | BR-041; PC-3, PC-10 |
| FR-009 | BR-051; §3 |
| FR-010 | BR-051; §5 v31; PC-11 |
| FR-011 | §5 v36; PC-11 |
| FR-012 | AD-7; PC-1; OD-009 |
| FR-013 | §3 ("счётчик"); BR-044; US-013 spec I-8; OD-005 |
| FR-014 | AD-5; §4 Epic 1; US-013 FR-008, FR-010; OD-008 |
| FR-015 | BR-025, BR-026; AD-6; SC-5, SC-8; US-013 FR-005 |
| FR-016 | DC-10; SC-10; §8 |
| FR-017 | PC-2, PC-3, PC-4, PC-7, PC-11 |
| FR-018 | NFR-001; SC-4 |
| FR-019 | §5 (audit of a *manual* start); SC-11; US-013 FR-017 |
| FR-020 | `AGENTS.md` (packages); NFR-073 |
