---
artifact_type: open_decisions
story: US-014
version: 2
status: DRAFT
created_at: 2026-09-27T16:33:58Z
updated_at: 2026-09-27T17:43:19Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-014 Open Decisions — Sync courses and rosters

Nine decisions (OD-001 … OD-009) were written into the Story by the author and
**resolved by the Owner on 2026-09-27 as option 1, before activation**. They are
carried here with their ids and resolutions unchanged.

Two further gaps (OD-010, OD-011) were found while writing the Specification from
`trebovaniya.md` §3. Both change the schema, so both had to be settled before
`DB_DESIGN`; the Owner **resolved them on 2026-09-27 at the
`HUMAN_SPEC_APPROVAL` gate**, before the Specification was approved. The
Specification was corrected accordingly at version 1, while it was still `DRAFT`
and the gate had not passed.

**Version 2** adds **OD-012** only, raised at `TEST_WRITING` and resolved by the
Owner the same day. It changes nothing the Specification or either design
consumed.

Not recorded here as an Open Decision, because it was a contradiction between
approved artifacts rather than an undecided question: the `CourseMembership`
uniqueness, which blocked `DB_DESIGN` at attempt 1 and was resolved in PC-8's
favour, producing Specification v2. The workflow history and Specification §1 and
I-9 carry that record.

---

## Resolved before activation

### OD-001 The "course older than N years" rule

**Question.** `trebovaniya.md` §5 (v36, v55) requires that synchronization not
import a *new* course whose last activity in Google is already older than the
retention period N. "Last activity" is the latest of four dates: the course's own
update time, creation or update of any `CourseWork`, update of any `Submission`,
and the start of any linked Meet session. This Story sees only the first.

**Resolution (Owner, 2026-09-27) — option 1.** The rule is **not** applied here.
Every course Classroom returns is imported, and the retention period is not read
by this Story. Judging age on `course.updateTime` alone would refuse a course
whose card is old but whose assignments are current, and because the course would
never be imported, US-015 would never see those assignments either — a mistake
that does not self-correct. The rule belongs to the Story that can see a course's
full last activity, and it only starts to matter when the purge exists (US-037),
since nothing deletes data before then.

**Impact on the Specification.** FR-003 imports without an age filter; the
limitation is recorded in §9 and §10 — until the rule exists, a fresh
installation may import a course whose data in Google is already past N.
`InstallationSettingsReader` gains no key.

### OD-002 Which `courseStates` are requested

**Question.** §3 lists five states; §5 purges "в любом статусе"; Epic 2 gives the
Dean a status filter. The prototype sent no filter (`google_api.py:56`).

**Resolution (Owner, 2026-09-27) — option 1.** No `courseStates` filter is sent,
and the state is stored exactly as Classroom reports it. Filtering is a reading
concern, never an import one.

**Impact.** FR-003, VR-002. All five §3 states are storable; see OD-010 for a
value outside that list.

### OD-003 Whether the technical account sees the school's courses

**Question.** `courses.list` returns the courses the *calling user* may see. The
prototype saw the whole domain because it impersonated a **super-admin**
(`with_subject('admin@dac.ukr.education')`), which `AGENTS.md` names explicitly as
not a precedent. BR-015 gives the technical account read-only Classroom and
Admin Reports roles, and §7 item 10 records that the minimum sufficient role set
"точный минимум не проверялся". So a correct implementation may return zero
courses on a live domain because of a Workspace role.

**Resolution (Owner, 2026-09-27) — option 1.** `courses.list` is called without a
per-user filter, and verifying the technical account's roles stays a deployment
task. §7 itself states that its remaining items do not block development, and
US-011's "check access" already exercises `courses.list` against the live domain.

**Impact.** FR-003 and §8: an empty course list is a **successful** run with a
counter of zero, not a failure, and the Specification records that an empty
import on a live domain is diagnosed as a Workspace role problem first. Reading
courses per teacher was rejected because it needs a Directory API scope that is
not on the fixed six-scope list (§6, v25).

### OD-004 How the course owner is stored

**Question.** §3 lists `owner` among a course's fields while also representing
co-teachers as memberships. Classroom returns `ownerId` on the course and normally
also lists that person in `teachers().list`, but does not guarantee it.

**Resolution (Owner, 2026-09-27) — option 1.** The Google `ownerId` is a plain
column on `Course` with **no** foreign key; the owner's membership arrives from
the teacher roster like anyone else's. A Google inconsistency then cannot make a
course unimportable.

**Impact.** FR-005, VR-002. US-022 reads memberships, not this column.

### OD-005 What the run counter counts

**Question.** US-013 kept **one** counter because §3 writes "счётчик" in the
singular (US-013 spec I-8). This Story is the first with something to count.

**Resolution (Owner, 2026-09-27) — option 1.** The counter reports **courses
processed**. §5 makes the course the unit of storage, so that number means
something to an Admin and stays comparable when US-015 adds coursework beneath the
same courses. `SyncState` is not redesigned and its migration is not amended.

**Impact.** FR-013; see I-6 for what makes a course count as processed.

### OD-006 A roster entry that is not a personal domain account

**Question.** §3 says the subjects of the teaching process each have "свой личный
аккаунт в домене" and that "групповые адреса Google и прочие аккаунты субъектами
не считаются". Classroom can nevertheless return an external address, a shared
account, or a profile with no email.

**Resolution (Owner, 2026-09-27) — option 1.** The roster is imported as Classroom
gives it: an entry outside the school domain is stored, and an entry with no email
is stored by its Google `userId` with the email absent. Nothing is discarded and
no run fails over it. Whether a person is a subject of the teaching process is
decided where the reports are built (BR-064), so §3's sentence must not become an
import filter.

**Impact.** FR-006, VR-003: the email is optional on `ClassroomParticipant`. See
OD-011 for the consequence on its uniqueness.

### OD-007 How much of BR-042 (incremental) belongs here

**Resolution (Owner, 2026-09-27) — option 1.** A full pass every run, idempotent
on the Google-side identifiers (BR-041). BR-042 — not re-fetching already-known
participants — belongs entirely to US-018, whose catalog entry stays.

**Impact.** FR-003, FR-004, §10.

### OD-008 Whether this Story classifies Google failures

**Question.** Epic 1 and AD-5 require that a permission failure (`403
unauthorized_client`, `access_denied`, a missing scope) is not retried and is
recorded with a human-readable diagnosis, while `429`/`5xx` are retried with
backoff. US-017 owns that; US-013 records only `RunFailed:<exception type>`.

**Resolution (Owner, 2026-09-27) — option 1.** No classification, no retry and no
Admin-facing diagnosis here. A Classroom read failure propagates and US-013's
existing handler records the run as failed. US-017 owns error policy in one place.

**Impact.** FR-014, §8, §10. NFR-010 and NFR-011 are therefore **not** satisfied
by this Story, and the Specification says so rather than implying coverage.

### OD-009 The transaction boundary of an import

**Resolution (Owner, 2026-09-27) — option 1.** One transaction per course, through
`IUnitOfWork.ExecuteInTransactionAsync`, covering the course, its participants and
its memberships together. A run that fails part-way leaves only complete courses
behind, and the idempotent upsert lets the next run finish the job. Short
transactions also suit PostgreSQL (PC-1).

**Impact.** FR-012, AC-007.

---

## Raised at SPECIFICATION, resolved at the gate

### OD-010 A `courseState` Google returns that §3 does not list

**The gap.** §3 fixes five states: `ACTIVE`, `ARCHIVED`, `PROVISIONED`,
`DECLINED`, `SUSPENDED`. OD-002 stores the state as Classroom reports it, and
SC-10 / §8 require that data returned by a Google API is validated before it
reaches business logic. Nothing in `trebovaniya.md` says what happens if Google
returns a sixth value — a state added to the API after v79 was written. The
program cannot store it in a closed enum with a check constraint (VR-002,
PC-*) and cannot silently reinvent it.

This is not hypothetical bookkeeping: whichever way it is resolved changes the
column type and the check constraint that `DB_DESIGN` writes.

**Options.**

1. **Skip that one course and complete the run.** The course is not imported, one
   log line records that a course was skipped for an unrecognised state — by
   internal identifier and state string only, never the course name (SC-10) — and
   the run completes successfully with the other courses. Rationale: a new Google
   state is a change on Google's side that calls for a new version of
   `trebovaniya.md` §3, not a reason to stop a school from synchronizing. Cost: a
   course silently absent from the Dean's list until the requirement is updated.
2. **Fail the run.** `SyncState` records a failed run, and the Owner sees it. One
   unfamiliar course then stops the whole school's synchronization, including the
   courses that were fine.
3. **Store the state as free text** rather than a closed vocabulary, so any value
   Google sends is kept. This contradicts VR-002's closed list and removes the
   check constraint that keeps the column honest; Epic 2's status filter would
   then have to cope with values no requirement defines.

**Resolution (Owner, 2026-09-27, at `HUMAN_SPEC_APPROVAL`) — option 1, with the
log level fixed.** A course whose state is not one of the five §3 values is
**skipped**, and the run completes successfully with the remaining courses. The
skip is logged at **`Warning`**, not `Information`, so it does not sink into the
ordinary lines of a run; the line carries the unrecognised state string and the
course's Google id only — never the course name (SC-10).

Rationale recorded with the decision: the closed list of five is a requirement of
§3, and inventing a sixth value is forbidden outright (`AGENTS.md` Hard Stops — no
business rule that no artifact defines), so the only real choice was between
skipping the course and failing the run. Failing it would stop a school receiving
*any* data because of one unfamiliar course — the class of failure §5's
idempotence exists to avoid. Noted for completeness: the Classroom API's
`courseState` enumeration formally contains a sixth member,
`COURSE_STATE_UNSPECIFIED`, which Google documents as never returned on a course —
so this rule guards against a future change to the API, not against today's
behaviour.

Accepted cost: such a course is absent from the Dean's list until
`trebovaniya.md` §3 is extended. The `Warning` line is what makes that absence
discoverable, since `SyncState` has one counter only (§3, OD-005) and cannot carry
a count of skipped courses.

**Impact on the Specification.** FR-003, VR-002 and §8 are complete. `DB_DESIGN`
keeps the closed vocabulary of five values with a check constraint, and
`TEST_WRITING` writes the test for an unrecognised value: the course is absent, the
other courses are imported, and the run's status is completed.

### OD-011 Is a participant's email unique?

**The gap.** PC-3 says natural keys such as email get a unique index, and PC-12
resolves a Meet participant to a person **by email**, matched against the roster
on the meeting's date — so the email is load-bearing for Epic 4, not decoration.
But OD-006 stores a roster entry with **no** email, and two different Google
`userId` values can carry the same address over time when an account is deleted
and recreated. `trebovaniya.md` §3 describes the email as a field of
`ClassroomParticipant` and says nothing about its uniqueness.

**Options.**

1. **Unique index on the email, allowing several rows without one.** In
   PostgreSQL a unique index permits many `NULL`s, so entries with no email
   coexist. Meet matching stays unambiguous. Cost: if Google ever returns two
   `userId` values with one address, the second course's import fails on that
   constraint — a Google-side reality becoming a failed run.
2. **No unique index; the email is a plain indexed column.** Nothing can break an
   import. Cost: Meet matching may find two people for one address, and Epic 4
   needs a documented rule for that case, which no requirement currently gives.
3. **Unique index, and a duplicate address treated as the same person** — the
   existing row's `userId` is overwritten. Rejected on sight: it would merge two
   people's course memberships, submissions and grades, which is a data-integrity
   defect, not a trade-off. Listed only so it is not proposed later.

**Resolution (Owner, 2026-09-27, at `HUMAN_SPEC_APPROVAL`) — option 2.** The email
address is a **plain indexed column, not unique**. An import can therefore never
fail on an address collision.

Rationale recorded with the decision, which corrected the option's stated cost:
option 2 was first written down as leaving Epic 4 without a rule for two people
sharing one address, but PC-12 already supplies that rule — a Meet participant is
matched by email **against the roster of the course on the meeting's date**
(BR-051), not against the whole participant table. Two people who held one address
at different times are represented by one of them in the roster of any given date,
so the ambiguity is resolved by the date rather than by a unique column. That
leaves the costs asymmetric: option 1's failure mode is a unique-index violation
stopping the whole school's import over a collision Google considers legitimate,
and at the several-years horizon of NFR-003 a reused school address is a realistic
event, not a theoretical one. An under-specified report rule can be fixed by the
Story that builds the report; an import that refuses to run blocks everything
downstream.

Option 3 — treating a duplicate address as the same person — stays rejected: it
would merge two people's memberships, submissions and grades.

**Impact on the Specification.** FR-006 and VR-003 are complete. `DB_DESIGN` gives
`ClassroomParticipant.Email` a non-unique index and keeps the unique index on the
Google `userId`, which remains the sole identity of a person (PC-3). Epic 4 must
match through the roster on the meeting's date, as PC-12 already requires — a
future Story that matches against the participant table globally would be
reintroducing this defect.

---

## Raised at TEST_WRITING, resolved by the Owner

### OD-012 How tests compile before the production types exist

**The gap.** `AGENTS.md` requires that `TEST_WRITING` run before
`IMPLEMENTATION`, against the approved artifacts and never against finished code.
In C# a test that names `Course`, `IClassroomReader` or a new constructor
parameter of `RunSynchronizationUseCase` **does not compile** while those do not
exist — and a test that does not compile is neither red nor evidence of anything.

This gap recurs in every Story that introduces new types, and it has been
resolved the same way five times: US-005 OD-002, US-007 OD-003, US-011 OD-006,
US-012 OD-005, US-013 OD-008.

**Options.**

1. **A compile-only skeleton.** The types and members the tests name are declared
   with bodies that throw `NotImplementedException`; the tests compile and fail
   for that one expected reason, and `IMPLEMENTATION` replaces the bodies.
2. Write tests that name no new type — covering only behaviour that already
   exists. Nine Acceptance Criteria would then have no test at all and the stage
   would lose its purpose.
3. Reverse the order: implement first, then test. Contradicts `AGENTS.md`.

**Resolution (Owner, 2026-09-27) — option 1**, scoped exactly like the five
precedents. The skeleton covers:

- `Domain/Entities`: `Course`, `ClassroomParticipant`, `CourseMembership`;
- `Domain/Enums`: `CourseState` (five members), `ClassroomRole` (two);
- `Application/Ports`: `IClassroomReader`, `ICourseRepository`,
  `IClassroomParticipantRepository`, `ICourseMembershipRepository`;
- `Application/Models`: `CourseSnapshot`, `CourseDetails`, `CourseRoster`,
  `RosterEntry`;
- **one change to an existing production file**: the new constructor parameters
  of `RunSynchronizationUseCase`. Without it the use-case tests cannot be
  compiled. US-013's skeleton carried an equivalent item (a member with a default
  on `InstallationSettings` plus the `SyncIntervalKey` constant);
- `Infrastructure/Google/GoogleClassroomReader`, the adapter itself. Added after
  the first pass: paging is a property of the adapter and invisible at the port by
  design (VR-005), so the test that proves it must construct the adapter;
- `Infrastructure/Persistence.Repositories`: `CourseRepository`,
  `ClassroomParticipantRepository`, `CourseMembershipRepository`, **and their
  registration in `InstallationServices`** together with `IClassroomReader`.
  Added after the first pass for a reason worth recording: without the
  registrations the host cannot construct `RunSynchronizationUseCase` at all, so
  **eight US-013 tests failed on a host that would not start** — and a stage that
  leaves existing regression tests red has not finished. The registration is the
  wiring; `IMPLEMENTATION` replaces only the bodies behind it.

The skeleton therefore grew twice beyond its first description, both times because
a test could not otherwise exist or an existing test could not otherwise pass. The
principle did not change.

**Deliberately outside the skeleton**, exactly as US-013 decided: the three EF
Core entity configurations, the three `DbSet` properties and the
`AddCoursesAndRosters` migration. Mapping an entity without its migration would
leave the model and the database disagreeing, so `IMPLEMENTATION` adds the
mapping, the check constraints, the indexes and the migration as **one** piece.

**Consequence the test-generation report must state plainly:** the schema tests
(db-design §9) fail on missing tables, not on a wrong constraint. That is the
expected red phase for them, and `IMPLEMENTATION` is what turns them green.

**Also required of `IMPLEMENTATION`:** every `NotImplementedException` and every
`#pragma warning disable` the skeleton needs must be gone once the members are
implemented. A remaining one is a finding — the rule US-013 set for itself.

---

## Not an Open Decision of this Story

- **§7 item 10** — the minimum Workspace role set for the technical account. §7
  marks it "проверить при внедрении" and states that its remaining items do not
  block development; OD-003 records how this Story behaves regardless. It is
  resolved by a new version of `trebovaniya.md`, not here.
- **§7 item 14** — whether Classroom returns the submissions of a student removed
  from a roster. It concerns submissions, which are US-015. The part of BR-051
  that gives such a student an off-roster membership is therefore out of scope
  here (§10), and the v56 modelling decision it depended on is already made.
