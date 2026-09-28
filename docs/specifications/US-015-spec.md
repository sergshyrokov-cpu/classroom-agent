---
artifact_type: specification
story: US-015
version: 1
status: APPROVED
created_at: 2026-09-28T11:28:43Z
updated_at: 2026-09-28T12:33:56Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-015-sync-coursework-and-submissions.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-015-open-decisions.md
    version: 1
supersedes: null
---

# US-015 Specification — Sync coursework and submissions

## 1. Overview

US-013 built the synchronization run; US-014 gave it its first step, importing
courses, participants and memberships. This Story adds the second and last import
step of Epic 1's opening sentence — «заданий/материалов и оценок» (§4 Epic 1) —
and creates the two entities Epic 3 is waiting for: `CourseWork` and
`Submission`.

It also makes two rules possible that were until now impossible to satisfy, both
deferred here in writing:

- **BR-051 v56** — a person with submissions whom synchronization never saw on a
  roster gets a student membership marked off the roster, so a leaver's expiry
  has a date to count from;
- **the §5 age rule (v36, v55)** — a course **not yet in the database** whose
  last activity in Google is older than the retention period N is not imported.
  US-014 could see only one of the four dates that define last activity; this
  Story sees three of them, and OD-001 resolves that it acts on them.

Two consequences of that second rule shape the whole Story: the import order
**inverts** for an unknown course (read its work, then decide whether to keep
anything), and this is the **first Story in the project to read the retention
setting N**, which DC-3 and PC-11 make required at startup.

Nine Open Decisions arrived resolved with the Story (OD-001 … OD-009). **Two
more were raised while writing this document** — OD-010 (the Story's dependence
on `trebovaniya.md` §7 item 14, still open) and OD-011 (the exact vocabulary of
submission states) — and both were **resolved by the Owner at
`HUMAN_SPEC_APPROVAL` on 2026-09-28, before approval**, because each fixes what
is stored or what a check constraint allows. This document was corrected at
version 1 while still `DRAFT`; §12 carries both.

## 2. Business Goal

**For the Dean:** the journal of a period becomes buildable from local data. §4
Epic 3 defines it as `student × (задание/материал с датой) × клетка`, and every
axis but the student is created here: the columns are `CourseWork` rows, the
cells are computed from `Submission` state, grades, due date, maximum points and
Google's `late` flag.

**For the school:** the retention period it agreed with the Owner starts being
honoured on import — data already past N is not brought in at all (§5 v36).

**For the Admin:** nothing visible changes yet. The run keeps reporting itself
through `SyncState` exactly as US-013 defined, with the counter still counting
courses (US-014 OD-005).

## 3. Business Flow

### 3.1 A course already in the database

The run reads the course and its rosters (US-014), then its coursework and
materials, then its submissions; everything for that course is committed in one
transaction. The course's age is **not** checked — §5 v55 is explicit that an
imported course is always updated until the purge deletes it.

### 3.2 A course not yet in the database

Before anything is written, the run reads that course's coursework, materials
and submissions and computes its last activity as the latest of: the course's own
update time, the creation or update of any of its coursework, and the update of
any of its submissions. If that instant is more than N years before the run's
instant, **nothing is written for the course** — no course row, no participant,
no membership, no coursework, no submission. Otherwise the course is imported in
full.

### 3.3 A piece of work gains or loses maximum points

The same row is updated. Graded versus ungraded is not stored; it follows from
whether maximum points are set (BR-052, PC-3), so nothing is duplicated and no
history is kept.

### 3.4 A grade changes in Google

The submission row is updated in place with the new `assignedGrade`. The previous
value is not kept anywhere — the history of grade changes is Epic 12 (BR-059).

### 3.5 A student turns work in, reclaims it and turns it in again

The stored submission date is the **second** turn-in: the latest transition to
`TURNED_IN` in the history Google returns with the submission (BR-058). The
history itself is never stored.

### 3.6 A submission arrives for someone who was never on the roster

A participant is created from the Google `userId` alone and a `student`
membership is created off the roster, first and last seen at the run's instant
(BR-051 v56). Whether Classroom ever returns such a submission is **OD-010**.

### 3.7 Read-only mode

The guard in `RunSynchronizationUseCase` runs first, so no coursework, material
or submission is read and nothing is written (BR-025, BR-026).

### 3.8 A run fails part-way

`SyncState` records the failure as US-013 specifies. Courses committed before it
are complete — each course's work and submissions were committed with it — and
the next run finishes the job (BR-041).

## 4. Functional Requirements

### FR-001 What this Story adds to a run

`RunSynchronizationUseCase` gains the coursework and submission import, inside
the existing per-course work and **behind the read-only guard that already runs
first**. No new use case is introduced and `PermittedServiceWrites` does not
grow: the new writes are inside a guarded use case, which is what makes them a
protected write path (US-013 FR-006, US-014 FR-001).

### FR-002 The Classroom read surface

The existing `IClassroomReader` port (`Application/Ports`, implemented in
`Infrastructure/Google`) gains the reads this Story needs. It already carries
`IGoogleDataPort`, which is what binds US-007 FR-007 — a use case holding it also
holds `IReadOnlyModeGuard` and calls it first. No Google SDK type crosses into
`Application` or `Domain` (AD-4); the Application models are the port's own
vocabulary.

No seventh scope is added: `classroom.coursework.students.readonly` and
`classroom.courseworkmaterials.readonly` are already two of the six fixed in §6
and asserted by an existing test.

### FR-003 Reading a course's coursework and materials

Two Classroom resources are read for each course under consideration:
`courseWork` and `courseWorkMaterials`. Each response is paged to the end
(VR-005). Only items whose Classroom state is `PUBLISHED` are imported; the state
itself is not stored (OD-004).

### FR-004 Reading a course's submissions

Submissions are read **once per course** with `courseWorkId = "-"`, paged to the
end (OD-002). Each returned submission carries its own `courseWorkId` and is
attributed by it. Materials have no submissions and none are requested for them
(§3, BR-052).

### FR-005 `CourseWork`

One entity and one table for both resources (OD-008), storing:

- the Google id and **which Classroom resource** it came from — together the
  natural key (PC-3), because ids from the two resources may collide;
- the course it belongs to;
- the title;
- **the item date**, by the cascade `scheduledTime` → `dueDate` → `updateTime` →
  `creationTime` (§3, BR-052);
- the **due date**, only if Google set one;
- the **maximum points**, only if Google set them;
- Google's creation and update times, which PC-11 needs for last activity.

The BR-052 kind — graded work, ungraded work, material — is **derived and never
stored**: it follows from the resource and from whether maximum points are set
(PC-3, §3 v32).

### FR-006 `Submission`

One entity and one table, storing exactly (PC-13, §3, and OD-007):

- Google's submission id (the natural key, PC-3);
- the `CourseWork` it belongs to and the person who submitted it;
- the Classroom **state** (VR-004);
- `assignedGrade` and `draftGrade`, as **raw points exactly as Google gives
  them**;
- the **date of the last turn-in** (FR-009), absent when unknown;
- Google's **`late`** flag as Google computed it;
- Google's **`updateTime`** (OD-007), which PC-11 needs for last activity.

**Nothing else is stored** — no file, no answer, no attachment, no grade-change
history, no rubric grade (BR-059, PC-13, §3).

### FR-007 The person a submission belongs to

A submission's Google `userId` is matched to the `ClassroomParticipant` US-014
created. If no participant exists for that `userId`, one is created from the
`userId` alone, with name and email absent (OD-006), together with a `student`
`CourseMembership` of that course marked **not on the roster**, first and last
seen at the run's instant (BR-051 v56). An existing membership is never rewritten
by this rule — US-014's observation logic owns it, and no first-seen date moves.

### FR-008 The item date

Exactly one date is stored per `CourseWork`, chosen by the first value present in
the order `scheduledTime`, `dueDate`, `updateTime`, `creationTime` (§3). The
cascade is applied identically to both resources.

### FR-009 The last turn-in date

The submission's history, as Google returns it **with the submission**, is read
in memory; the **latest** transition to `TURNED_IN` becomes the stored date
(BR-058, §3). The history itself is never stored (PC-13, BR-059, Epic 12). A
submission whose history Google omits or truncates has **no date** — stored as
absent, never inferred from `updateTime`, which changes when a teacher enters a
grade (OD-009).

### FR-010 Upsert and identity

Every write is an upsert on the Google-side natural key (BR-041, PC-10):
`CourseWork` on (resource, Google id), `Submission` on the Google submission id.
A row updated in place keeps its surrogate identity, so anything referring to it
still does. A second run with nothing changed in Google produces no duplicate and
no new row.

### FR-011 The age rule for a course not yet in the database

For a course that has **no row in the installation database**, the run computes
its last activity as the latest of: the course's own update time; the creation or
update time of any of its coursework or materials; the update time of any of its
submissions (§5 v36, v55, PC-11). If that instant is more than N years before the
run's instant (I-1), **nothing is written for that course** — the course, its
participants, its memberships, its coursework and its submissions are all
skipped.

A course that **already has a row** is updated on every run regardless of age,
until the purge deletes it (§5 v55). A skipped course is **not** a failure: the
run continues and completes.

### FR-012 The retention period N

The installation configuration key `Retention:Years` (I-2) is **required**: an
installation without a valid value refuses to start, with no default and no
"keep forever" (DC-3, PC-11, §5). `deployment-conventions.md` DC-3 gains the key
name, as it did for `Sync:IntervalMinutes` in US-013.

### FR-013 The transaction boundary

One transaction per course, through `IUnitOfWork.ExecuteInTransactionAsync`,
covering the course, its participants, its memberships, its coursework and
materials and its submissions together (OD-003, AD-7). A run that fails part-way
leaves only complete courses behind.

### FR-014 The run counter

`SyncState` is unchanged: one counter, counting **courses processed** (US-014
OD-005, US-013 I-8). A course processed means its transaction committed. A course
skipped by FR-011 is not counted. No column is added and the US-013 migration is
not amended.

### FR-015 A failure inside the step

A Classroom failure propagates; US-013's existing handler records the run as
failed with `RunFailed:<exception type>` (SC-10). This Story performs **no**
classification, **no** retry and **no** Admin-facing diagnosis — US-017 owns
error policy (US-014 OD-008).

### FR-016 Read-only mode

In read-only mode no coursework, material or submission read happens — including
the token request — and no row of either new table is written or updated. The
enforcement point is the existing guard in `RunSynchronizationUseCase`, in
`Application`, never UI state (AD-6, SC-5, BR-025, BR-026).

### FR-017 Logging

The step logs per DC-10 within the existing run: counters, states and the run
identifier. **No log line carries** a grade, a person's name or email, a Google
id of a person, a course name or a work title, or a raw Google error object
(SC-10). The one Warning line this Story adds is the unrecognised-state line of
VR-004, carrying the raw state string and the submission id and nothing else.

### FR-018 Persistence

Two new tables and **one** EF Core migration in this Story (PC-2). No existing
table is altered: `SyncState` gains no column (FR-014), `audit_event` needs no
amendment (FR-020), and US-014's three tables keep their shape. The Control Plane
is untouched.

### FR-019 Surface and authorization

This Story adds **no** endpoint, **no** Razor page, **no** authorization policy
and **no** route. SC-4's anonymous list is unchanged and the US-008 endpoint
enumeration test gains no row. The journal is US-025; the statistics view is
US-024.

### FR-020 Audit

A scheduled run writes **no** audit row (US-013 FR-017). `AuditAction`,
`AuditTargetType` and their check constraints do not grow.

### FR-021 Packages, wiring and localization

No NuGet package is added. New ports are registered in the existing installation
DI composition. **No user-visible string is added**, so no translation key is
created; if any message did reach a user, NFR-073 would apply in full and both
language files would grow together.

### FR-022 The PC-13 correction

`persistence-conventions.md` PC-13 is corrected to include Google's `updateTime`
in what a `Submission` stores (OD-007). The requirement is unchanged — §5 v36
already says «изменение любой сдачи» — and only the derived convention was wrong
(AGENTS.md).

## 5. Acceptance Criteria

Every Acceptance Criterion of the Story is carried unchanged; see §11 for the
mapping to requirements.

| Id | Criterion |
|---|---|
| AC-001 | A run imports each course's assignments and materials |
| AC-002 | A run imports the submissions of each piece of work |
| AC-003 | A second run changes nothing it should not |
| AC-004 | A person with submissions but no roster sighting gets a membership |
| AC-005 | The database holds what the purge will need |
| AC-006 | Read-only mode never reaches the new reads |
| AC-007 | A failure part-way through leaves a consistent database |
| AC-008 | No grade and no personal data reaches a log |
| AC-009 | Tests never reach Google, and the schema is tested for real |
| AC-010 | A course whose last activity is already older than N is not imported |

## 6. Validation Rules

### VR-001 Data returned by Google is external input

Everything Classroom returns is validated before it reaches business logic
(SC-10, §8): identifiers must be present and non-empty, dates must parse, numbers
must parse. A rejected payload is never written to a log.

### VR-002 `CourseWork` fields

The Google id and the resource are **required**; an item missing either is not
importable. The title is required by §3; string bounds are set by `DB_DESIGN` and
a value longer than its bound is **truncated, not refused** — the US-014 VR-002
precedent, so a verbose title cannot fail a run. `dueDate` and `maxPoints` are
optional and stored only when Google sets them.

### VR-003 `Submission` fields

The Google submission id, the `courseWorkId` and the `userId` are **required**.
`assignedGrade` and `draftGrade` are optional and stored as given, with no
conversion to any school scale (PC-13). `late` is stored as Google computed it,
never recomputed from the due date (§4 Epic 3). The last turn-in date is optional
(FR-009).

### VR-004 The submission state

The state is a **closed vocabulary with a check constraint**, plus an explicit
"unrecognised" marker that keeps the raw string Google sent; the row is stored
either way and one Warning line records the unrecognised value and the submission
id (OD-005).

The recognised values are **six** (OD-011): the five the Classroom API
documents — `NEW`, `CREATED`, `TURNED_IN`, `RETURNED`, `RECLAIMED_BY_STUDENT` —
plus `STUDENT_EDITED_AFTER_TURN_IN`, which BR-056 names. The unrecognised marker
therefore catches only a value Google genuinely adds later, never the ordinary
state of untouched work.

Two things this rule deliberately does **not** do:

- it does not say what a journal cell shows for `NEW`. BR-056 does not describe
  it, and that decision belongs to **US-025** together with the OD-005
  obligation — an unrecognised state is rendered as its own thing, never as
  «не сдано» and never as a grade;
- it does not treat BR-056's list as verified. It is **unverified against a live
  Classroom response** in both directions: `NEW` is absent from BR-056, and
  `STUDENT_EDITED_AFTER_TURN_IN` may be absent from the API. This is the first
  Story in the project that reads a submission state at all, and a correction
  belongs in a new version of `trebovaniya.md`, never in a Story.

### VR-005 Paging

Every list response — coursework, materials and submissions — is paged to the
end. Paging is a property of the adapter and invisible at the port (US-014
VR-005), so the test that proves it constructs the adapter.

### VR-006 Time

All timestamps are stored in UTC (BR-053, PC-6). Google's creation, update and
turn-in times are stored as given. First-seen and last-seen of a membership
created by FR-007 come from the run's single instant (I-1), not from any Google
timestamp (US-014 I-3, I-8).

### VR-007 Grades

Grades are stored as **raw points** together with the coursework's maximum; no
conversion to a school scale is stored, ever — that belongs to report templates
(PC-13). A grade on a piece of work with no maximum points is stored as Google
gave it and is not treated as an error (I-8).

### VR-008 The retention period

`Retention:Years` must be a positive whole number of years. Absent, unparsable or
non-positive, the installation **refuses to start** (DC-3, PC-11). The value is
read once at startup and used unchanged for the run's comparison (I-1, I-3).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | No endpoint, page, policy or route is added; SC-4's anonymous list is unchanged | SC-4, FR-019 |
| S-02 | Read-only mode is enforced in `Application` before any Google call or write; not by hiding UI | SC-5, AD-6, BR-026 |
| S-03 | Every Classroom call impersonates the technical account of `WorkspaceConnection`; no super-admin, no person's account | BR-015, SC-3 |
| S-04 | The six delegation scopes are unchanged; the key is resolved from the secret store per request and never logged, returned or stored in the database | SC-7, §6 |
| S-05 | Grades, titles, names and email addresses are **personal data of students, potentially minors**; they are written only to the installation's own database | SC-1, §5 |
| S-06 | No grade, name, email, Google person id, course name, work title or raw Google error text appears in any log line or `SyncState` diagnosis | SC-10, DC-10 |
| S-07 | Nothing is sent to any destination outside Google and the Control Plane | SC-13 |
| S-08 | The imported data has **no read path** until Epic 3 — no controller, page or view model exposes a `CourseWork` or a `Submission` in this Story | SC-2, FR-019 |
| S-09 | All data returned by Google is validated before it reaches business logic; a rejected payload is never logged | SC-10, VR-001 |
| S-10 | No audit row is written by a scheduled run, and the audit vocabulary does not grow | SC-11, FR-020 |
| S-11 | No write of any kind reaches Google Workspace — every scope is read-only | Hard Stop, BR-030 |

## 8. Error Handling

- **A Classroom read fails** — the exception propagates, the course's transaction
  rolls back, US-013's handler records `RunFailed:<type>`, and the service stays
  up (FR-015). Courses already committed are complete.
- **An item is unimportable** — an item missing its Google id or its resource is
  refused (VR-002). Consistent with US-014, a value merely too long is truncated
  rather than refused.
- **An unrecognised submission state** — the row is stored with the unrecognised
  marker and one Warning line; the run continues and completes (OD-005, VR-004).
- **A submission for an unknown person** — a participant and an off-roster
  membership are created (FR-007); this is normal flow, not an error.
- **A submission whose history has no turn-in** — the date is stored as absent;
  not an error (FR-009).
- **A course skipped by the age rule** — normal flow, not an error, and not
  counted (FR-011, FR-014).
- **`Retention:Years` missing or invalid** — the installation refuses to start
  (VR-008). This is a startup failure, not a run failure.

## 9. Non-Functional Requirements

- **NFR-002 (volume).** A school's submissions are the largest data set the
  program handles: (courses × assignments × students). Reading them once per
  course rather than once per assignment (OD-002) is the choice that keeps the
  call count linear in courses.
- **NFR-003 (years of data).** Rows live for the retention period; the age rule
  (FR-011) keeps a fresh installation from importing data already past it.
- **NFR-010 / NFR-011 are NOT satisfied by this Story.** Retry with backoff for
  `429`/`5xx`, the no-retry rule for permission failures and the diagnosable
  message for the Admin all arrive with US-017 (FR-015).
- **DC-10 (logging).** The step logs inside the existing run, with the run
  identifier on every line.
- **PC-1 (transactions).** One transaction per course is longer than US-014's;
  the trade is recorded in OD-003 and accepted.

## 10. Out of Scope

- Computing a journal cell (BR-056) — this Story stores the facts a cell is
  computed from. The journal is **US-025**, the graded / ungraded / material
  distinction in the UI is **US-026**, templates and exports are US-027 … US-030.
- The grade-change history and rubric grades — **Epic 12** (BR-059).
- Failure classification, retry, backoff, the Admin-facing diagnosis — **US-017**.
- Incremental behaviour, including not re-reading unchanged submissions —
  **US-018** (BR-042).
- The retention **purge** itself — **US-037** (PC-11). This Story stores the dates
  it needs and deletes nothing.
- Meet sessions, meeting codes and the fourth component of last activity — Epic 4
  (US-031, US-032).
- Starting a run from the UI and its audit row — **US-019**.
- Any change to the six scopes, the service-account key handling or impersonation.
- Any write to Google Workspace — a Hard Stop.

## 11. Interpretations

Where a requirement is silent, this document states an interpretation rather than
leaving it implicit. None of these invents behaviour; each is the reading
`DB_DESIGN`, `TEST_WRITING` and `IMPLEMENTATION` must follow.

- **I-1 One instant per run.** The age comparison of FR-011 and the first/last
  seen of FR-007 use the run's single instant, as US-014 I-8 established, so
  "seen in the same run" stays expressible in a query.
- **I-2 The configuration key is `Retention:Years`.** DC-3 requires the setting
  but names no key; US-013 set the precedent by naming `Sync:IntervalMinutes` in
  its Specification and having DC-3 record it. Only the name and unit are chosen
  here; the requirement to have it, and to refuse to start without it, is DC-3's.
- **I-3 N is compared in whole years** against the run's instant. §5 speaks of
  "N лет"; no document defines a finer granularity, and inventing one would
  change when data expires.
- **I-4 An imported course is never age-checked**, however old it becomes.
  §5 v55 is explicit, and the reason is given there: a course kept alive by
  recent Meet sessions must not have its roster go stale.
- **I-5 A course skipped by the age rule leaves no trace in `SyncState`.** The
  counter counts committed courses (FR-014) and there is one counter (US-013
  I-8), so the skip is visible only in the log — the same shape US-014 OD-010
  accepted for a skipped course.
- **I-6 Materials are never queried for submissions**, because §3 states they
  have none. An adapter that asked anyway would be making a call no requirement
  justifies.
- **I-7 A submission is attributed to a person by Google `userId`**, never by
  email — OD-011 of US-014 made the email deliberately non-unique, and matching
  by it would reintroduce the defect that decision avoided.
- **I-8 A grade on ungraded work is stored as given.** BR-052 says such work has
  no grades; if Google nevertheless returns one, storing it is faithful and
  discarding it would hide a real value. The journal's rule for the cell is
  BR-056's, not this Story's.
- **I-9 `late` is never recomputed.** BR-056 takes Google's flag as is, because
  Google accounts for extensions and individual due dates (§4 Epic 3).
- **I-10 The last activity of FR-011 is computed from Google's data**, not from
  rows in the database — for a course not yet imported there are none. For an
  imported course the question never arises (I-4).

## 12. Open Decisions

### Resolved before activation — OD-001 … OD-009

All nine arrived resolved by the Owner on 2026-09-28 and are carried unchanged
into `docs/decisions/US-015-open-decisions.md`. Eight are option 1; **OD-005 is
option 4**, deliberately not US-014 OD-010's answer, because a skipped submission
would read as «не сдано» and state something false about a child's work.

### Raised while writing this document — resolved at the gate

Both were raised here and **resolved by the Owner on 2026-09-28 at
`HUMAN_SPEC_APPROVAL`**, before approval, because each fixes what is stored or
what a check constraint allows. This document was corrected at version 1 while
still `DRAFT` — the US-014 precedent.

**OD-010 — does Classroom return the submissions of a student removed from a
roster?** This is `trebovaniya.md` §7 item 14, still open; AGENTS.md required it
to be raised rather than resolved by a Skill.

*Resolution:* proceed as specified; §7 item 14 stays a verify-at-onboarding item,
as US-014 OD-003 left §7 item 10. **The item itself is not closed** — only the
Story's dependence on it. The decisive point is that the **implementation is
identical either way**: FR-007 creates the membership when such a submission
arrives and does nothing when none does, so no line of code depends on the
answer. What the answer changes is only what may be claimed about coverage, so
AC-004's test uses synthetic fixtures and proves the program's rule, never
Google's behaviour (TC-4) — and an installation that never produces such a
membership on a live domain is evidence about Classroom, not a defect.

**OD-011 — the exact vocabulary of submission states.** OD-005 obliged this
document to list the recognised values, and doing so proved impossible without a
decision: BR-056 names five, the Classroom API also documents `NEW`, and
`STUDENT_EDITED_AFTER_TURN_IN` may not exist in the API at all.

*Resolution:* the vocabulary is the **six** values now fixed in VR-004, and the
cell rule for `NEW` belongs to **US-025**. What to store is this Story's
question; what a cell shows is BR-056's, and deciding that a `NEW` cell reads
«не сдано» is a change to §4 Epic 3 that only the Owner makes — better made when
the whole journal is designed. `STUDENT_EDITED_AFTER_TURN_IN` stays permitted
even though it may not exist: an unused permitted value costs nothing, while
omitting it would flag a real value as unrecognised.

## 13. Traceability

| AC | Functional requirements | Validation / security |
|---|---|---|
| AC-001 | FR-003, FR-005, FR-008, FR-010 | VR-002, VR-005 |
| AC-002 | FR-004, FR-006, FR-007, FR-009 | VR-003, VR-004, VR-007 |
| AC-003 | FR-010, FR-014 | VR-002, VR-003 |
| AC-004 | FR-007 | VR-006, I-7; OD-010 |
| AC-005 | FR-005, FR-006, FR-013 | VR-006, S-05 |
| AC-006 | FR-001, FR-016 | S-02 |
| AC-007 | FR-013, FR-015 | S-06 |
| AC-008 | FR-017, FR-019, FR-020 | S-05, S-06, S-07, S-08, S-10 |
| AC-009 | FR-002, FR-003, FR-004 | VR-005, S-11 |
| AC-010 | FR-011, FR-012 | VR-008, I-1, I-3, I-4, I-10 |

Requirements with no Acceptance Criterion of their own, traced to the Story's
Scope and to the conventions they implement: FR-018 (PC-2), FR-021 (FR-020 of
US-014, NFR-073), FR-022 (OD-007, AGENTS.md).
