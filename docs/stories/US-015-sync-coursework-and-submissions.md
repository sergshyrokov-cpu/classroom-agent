---
id: US-015
epic: EPIC-1
title: Sync coursework and submissions
slug: sync-coursework-and-submissions
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
# DRAFT — OD-001 … OD-009 are NOT yet resolved. The Owner resolves them before
# activation, as US-013 and US-014 did; /so:start should not run until then.
---

# User Story

As a **Dean or Admin**

I want each imported course's assignments, materials and student submissions —
with the grades Classroom holds — to be present in the installation's own
database, refreshed by the same background run that already imports courses and
rosters

So that the journal for a period can be built from local data without calling
Google for every cell, and so that a course's real last activity is finally
knowable to the system.

---

# Business Value

`trebovaniya.md` §4 Epic 1 names this in the same sentence as US-014:
"я периодически (или по запросу) синхронизирую список курсов, участников с их
ролями в курсах, **заданий/материалов и оценок** через Classroom API". US-014
delivered the first half; this Story is the second, and it is the last import
Epic 3 waits on.

Nothing in Epic 3 can start without it. The journal is
`student × (coursework/material with a date) × cell` (§4 Epic 3), and every one
of those three axes except the student comes from this Story: the columns are
`CourseWork` rows, the cells are `Submission` states and grades, and BR-056's
six cell states are computable only from `state`, `assignedGrade`, `draftGrade`,
the due date, `maxPoints` and Google's `late` flag. US-025 (journal view),
US-026 (graded / ungraded / material), US-028 and US-029 (exports) all read
exactly what this Story creates.

Two rules that are currently **impossible to satisfy** also become possible here,
and both were explicitly deferred to this Story:

- **BR-051's second half (v56):** a person with submissions whom synchronization
  never saw on a roster — they left before the first run — gets a student
  membership marked off the roster, first and last seen on that run's date, so
  the leaver expiry has a date to count from. US-014 put this out of scope in
  writing, because the rule needs submissions to exist.
- **The §5 age rule (v36, v55):** synchronization must not import a *new* course
  whose last activity in Google is already older than the retention period N.
  "Last activity" is the latest of four dates, and US-014 could see only the
  first of them (US-014 OD-001, resolved as "not here — US-015 at the earliest").
  This Story is the first that can see three of the four; the fourth (Meet) is
  US-031. Whether it acts on them is OD-001 below.

---

# Scope

**In scope:**

- extending the Classroom read surface with coursework, materials and
  submissions — through `IClassroomReader` in `Application/Ports`, implemented in
  `Infrastructure/Google` beside the existing adapter; no Google SDK type crosses
  into `Application` or `Domain` (AD-4);
- two entities with their tables, mapping and **one** EF Core migration in this
  Story (PC-2): `CourseWork` and `Submission`, with the fields `trebovaniya.md`
  §3 lists for each and nothing more (PC-13);
- both Classroom resources behind `CourseWork`: `courseWork` (assignments) and
  `courseWorkMaterials` (materials), keyed per PC-3 on (resource, Google id)
  rather than on the Google id alone;
- the date cascade §3 fixes for a course item —
  `scheduledTime` → `dueDate` → `updateTime` → `creationTime` — which the
  prototype already implements (`google_api.py:107-124`);
- the submission facts and only those (PC-13, BR-059): Classroom state,
  `assignedGrade`, `draftGrade`, the date of the last turn-in, Google's `late`
  flag; **never** the work's content, the grade-change history or rubric grades;
- graded versus ungraded work **derived, not stored** — it follows from whether
  `maxPoints` is set, so a teacher adding points later updates the same row
  (PC-3, BR-052, §3 v32);
- the next step inside `RunSynchronizationUseCase`, after the US-014 step, with
  the same idempotent upsert on Google-side identifiers (BR-041, PC-10) and
  paging through every list response (VR-005 of US-014);
- BR-051's off-roster membership for a person who has submissions but was never
  seen on a roster;
- read-only mode: the guard already runs first, so this Story must prove the
  **new** reads are never reached in read-only mode (SC-5, AD-6, TC-5);
- logging per DC-10 and SC-10: no title, no name, no email, **no grade** — a
  grade is personal data of a student.

**Out of scope:**

- every screen that shows this data — the journal is **US-025**, the graded /
  ungraded / material distinction in the UI is **US-026**, templates and exports
  are US-027 … US-030, the sync statistics view is US-024. This Story adds no
  page, no endpoint and no translation key;
- computing a journal cell (BR-056) — this Story stores the facts a cell is
  computed from, and computes nothing;
- the grade-change history and rubric grades — **Epic 12**, excluded by BR-059
  and §3; `submissionHistory` is read only to find the last turn-in date, and
  the history itself is never stored;
- classifying a Google failure, retry, backoff and the Admin-facing diagnosis —
  **US-017** (AD-5, OD-008 of US-014). A failure propagates and US-013's handler
  records `RunFailed:<type>`;
- incremental behaviour, including not re-reading submissions that cannot have
  changed — **US-018** (BR-042);
- the retention purge itself — **US-037** (PC-11). This Story stores the dates
  the purge needs; it deletes nothing;
- Meet sessions and meeting codes, the fourth component of "last activity" —
  Epic 4 (US-031, US-032);
- starting a run from the UI and its audit row — **US-019**;
- any change to the six scopes, the service-account key handling or impersonation
  — `classroom.coursework.students.readonly` and
  `classroom.courseworkmaterials.readonly` are already on the fixed list
  (§6, `GoogleDelegationScopes`);
- any write to Google Workspace — a Hard Stop, not a scope choice.

---

# Acceptance Criteria

## AC-001 A run imports each course's assignments and materials

**Given** an installation with a saved `WorkspaceConnection`, not in read-only
mode, whose courses US-014 already imports

**When** a synchronization run executes

**Then**:

- every `courseWork` and every `courseWorkMaterials` entry Classroom returns for
  an imported course is present in the installation database with the fields §3
  lists: title, which of the two Classroom resources it came from, its date by
  the cascade `scheduledTime` → `dueDate` → `updateTime` → `creationTime`, its
  due date if one is set, and its maximum points if they are set;
- a `courseWork` **without** maximum points is stored as such — it is ungraded
  work, which has submissions and states but no grades (BR-052, §3 v32);
- graded versus ungraded is **not** a stored field: it follows from the presence
  of maximum points, so a teacher adding points later updates the same row
  (PC-3);
- list responses are paged through to the end, for both resources;
- the row is keyed on (Classroom resource, Google id) with a unique index and a
  surrogate `long Id` primary key (PC-3).

## AC-002 A run imports the submissions of each piece of work

**Given** a course with graded and ungraded work and students who have and have
not turned work in

**When** the run imports that course

**Then**:

- every student submission Classroom returns for each piece of work is present,
  carrying the Classroom state, `assignedGrade` and `draftGrade` as raw points
  exactly as Google gives them, the date of the last turn-in, and Google's `late`
  flag as Google computed it (§3, PC-13, BR-056);
- grades are stored as **raw points**, never converted to a school scale — that
  conversion belongs to report templates (PC-13);
- a material has no submissions and none are sought for it (§3, BR-052);
- the submission is keyed on Google's submission id (PC-3) and linked to its
  `CourseWork` row and to the person;
- **nothing else is stored**: no file, no answer, no attachment, no grade-change
  history, no rubric grade (BR-059, PC-13).

## AC-003 A second run changes nothing it should not

**Given** a run has already imported a course's work and submissions

**When** the same data is imported again with nothing changed in Google

**Then**:

- no row is duplicated — every write is an upsert on the Google-side identifier
  (BR-041, PC-10);
- a grade changed in Google is updated in place; the previous value is **not**
  kept anywhere, because the history is Epic 12 (BR-059);
- a piece of work that gained or lost maximum points updates the same row rather
  than creating a second one (PC-3);
- the run completes and `SyncState` records it as US-013 specifies, with the
  counter still counting **courses** (US-014 OD-005).

## AC-004 A person with submissions but no roster sighting gets a membership

**Given** a student who left a course before the installation's first
synchronization, and whose submissions Classroom still returns

**When** the run imports that course

**Then**:

- that person has a `ClassroomParticipant` row and a `CourseMembership` with role
  `student`, **not** on the roster, with first-seen and last-seen both set to the
  date of this run (BR-051, §3 v56);
- their submissions are attached to that membership's course like anyone else's;
- the documented limitation is recorded rather than worked around: the leaver's N
  years are counted from the day of this synchronization, not from the day they
  actually left (§3 v56);
- a person who *is* on the roster is untouched by this rule — US-014's
  observation logic keeps its behaviour, and no first-seen date is rewritten.

## AC-005 The database holds what the purge will need

**Given** an imported course with work and submissions

**When** the retention purge is later built (US-037)

**Then** the data it needs is already present:

- a course's **last activity** can be computed from stored data as the latest of
  the course's own update time, the creation or update of any of its `CourseWork`
  rows, and the update of any of its `Submission` rows — the three of PC-11's four
  components that exist before Epic 4 (§5 v36);
- a leaver's own expiry still works: their membership carries `last_seen_at`, and
  their submissions in that course are reachable from it (PC-11, §5 v31);
- nothing is deleted, hidden or marked by this Story — a piece of work Google no
  longer returns stays in the database exactly as US-014 leaves a vanished course
  (AC-005 of US-014).

## AC-006 Read-only mode never reaches the new reads

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (BR-025)

**When** the schedule would start a run

**Then**:

- no coursework, material or submission call is made, including the token
  request, proven in the Application layer with the port substituted (SC-5, AD-6,
  TC-4, TC-5);
- no `CourseWork` or `Submission` row is written or updated — a synchronization
  write is not on the BR-026 closed list;
- `PermittedServiceWrites` does not grow: the new writes happen inside
  `RunSynchronizationUseCase`, which already calls `IReadOnlyModeGuard` first
  (US-013 spec FR-006, US-014 AC-006).

## AC-007 A failure part-way through leaves a consistent database

**Given** a run that fails while importing — Google stops answering after some
courses are fully imported

**When** the run ends

**Then**:

- `SyncState` records the run as failed with a diagnosis carrying no personal
  data, no grade and no raw Google error text (SC-10, US-013 AC-006);
- what was committed before the failure is consistent at the boundary OD-003
  fixes — no course left with a roster but a half-written set of submissions;
- the next run completes the import without duplicating anything (BR-041);
- the service does not stop and the host is not taken down (US-013 AC-006).

## AC-008 No grade and no personal data reaches a log

**Given** a run importing real coursework and submissions

**When** its log lines are read

**Then**:

- no line carries a grade, a student's name or email, a Google id of a person, a
  course name or a work title, or a raw Google error object — counters and states
  only (SC-10, DC-10);
- the run's identifier is on every line (US-013 AC-007);
- nothing is sent anywhere but Google and the Control Plane (SC-13); the imported
  data is written only to the installation's own database;
- a scheduled run still writes **no audit row**, and the audit vocabulary does not
  grow (US-013 spec FR-017, US-014 AC-008).

## AC-009 Tests never reach Google, and the schema is tested for real

**Given** the test suite

**When** it runs

**Then**:

- every Classroom call goes through the substituted `IClassroomReader` with
  synthetic fixtures — never a real title, grade, name, email or school domain
  (TC-4);
- the new tables, their unique indexes and their constraints are tested against
  real PostgreSQL through Testcontainers; the InMemory provider is not used
  (TC-2);
- paging is covered by fixtures returning more than one page for coursework,
  materials **and** submissions, so a single-page implementation fails;
- the date cascade is covered with a fixture for each of its four levels,
  including the case where only `creationTime` is present;
- a graded work, an ungraded work and a material are each covered, and the
  ungraded case asserts that no grade is stored;
- both the allowed case and the read-only refusal are covered in the Application
  layer (TC-5).

---

# Open Decisions

**None of these is resolved.** They are written for the Owner to decide before
activation, as US-013's and US-014's were.

## OD-001 Does this Story apply the "last activity older than N" rule?

`trebovaniya.md` §5 (v36, v55) and Epic 1 require that synchronization **not
import a course that is not yet in the database** whose last activity in Google
is already older than the retention period N, while a course already in the
database is always updated until the purge deletes it. §5 v55 is explicit that
for a not-yet-imported course the check uses **Google's data only** — the course,
its coursework and materials, its submissions — because such a course has no
linked meetings yet. That is exactly the set of dates this Story can see.

US-014 OD-001 deferred the rule with the words "US-015 at the earliest", so the
question lands here by decision, not by accident. Two things complicate it: the
rule must run **before** a course is imported, which inverts the natural order
(today US-014 imports the course, then this Story reads its work); and PC-11's
retention setting N is still unread by any code
(`InstallationSettingsReader.cs`), so whichever option is chosen decides whether
this Story introduces that setting.

Options:

1. **Apply the rule here (recommended).** For a course **not yet in the
   database**, read its coursework and submissions first, compute the last
   activity from the three available dates, and import nothing for that course if
   it is older than N. A course already in the database is always updated. This
   Story then reads the retention setting for the first time. Cost: one extra
   pass of reads for an unknown course that is then discarded, and the known
   limitation §5 v55 already records — a course silent in Classroom for more than
   N years but active in Meet will not be imported into a new installation.
2. Defer the rule to US-037, where the purge is built, on the grounds that
   nothing deletes data until then so nothing can undo anything. Cost: a new
   installation keeps importing expired data in the meantime, and the Story that
   finally adds the rule must also change the import order this Story fixes.
3. Defer to US-018 (incremental), because that Story already reworks what is and
   is not re-read. Cost: the retention rule becomes a side effect of an
   optimisation Story, and an installation deployed before US-018 has no rule.

## OD-002 How submissions are requested

Classroom exposes `courses.courseWork.studentSubmissions.list`, which accepts
either a concrete `courseWorkId` or the literal `-` meaning "all work in this
course". The prototype calls it **per `courseWorkId`**
(`google_api.py:210`) inside a loop over the course's work.

Options:

1. **One call per course with `courseWorkId = "-"` (recommended).** One paged
   read per course instead of one per assignment: a course with 80 assignments
   costs 1 call rather than 80, which matters for the rate limits US-017 will
   have to live with (NFR-002, BR-043). Each returned submission carries its own
   `courseWorkId`, so the rows are still attributed correctly. Cost: a single
   large paged response per course, and a failure fails the whole course's
   submissions rather than one assignment's.
2. One call per piece of work, as the prototype does. Smaller responses and a
   narrower blast radius per failure, at roughly the cost of one API call per
   assignment per run — for a school with 140 courses × 50 assignments that is
   7 000 calls per run, before US-018 makes anything incremental.

## OD-003 The transaction boundary now that submissions exist

US-014 OD-009 fixed **one transaction per course**, covering the course, its
participants and its memberships. This Story adds, per course, up to
(assignments × students) submissions — for 50 assignments and 30 students that is
1 500 rows in one transaction, and a large course can be an order of magnitude
worse. PC-1 prefers short transactions on PostgreSQL.

Options:

1. **Keep one transaction per course, now including its work and submissions
   (recommended).** AC-007's guarantee stays exactly what US-014 established — a
   run that fails leaves only *complete* courses behind — and the idempotent
   upsert lets the next run finish the job. Consistent with §5's "единица
   хранения — курс". Cost: a long transaction for a large course.
2. One transaction per course for the course, roster and work, then one
   transaction per piece of work for its submissions. Much shorter transactions;
   but a course can then be left with some work having submissions and some not,
   which is a state AC-007 currently forbids.
3. Chunk submissions into batches of a fixed size inside the course's
   transaction. Cheaper on memory, no change to the visible guarantee, but it
   introduces a number that no requirement defines and that AD-10 would have to
   place somewhere.

## OD-004 Coursework whose Classroom state is `DRAFT` or `DELETED`

Classroom returns a `state` on a `courseWork` — `PUBLISHED`, `DRAFT` or
`DELETED`. `trebovaniya.md` §3 does not list this field among the ones to store,
and no business rule mentions it. A draft is not visible to students and has no
real submissions; a deleted assignment is still returned by the API for a period.

Options:

1. **Import only `PUBLISHED` work and materials, and do not store the state
   (recommended).** The journal is a record of what students were actually given
   (§4 Epic 3); a draft column in a Dean's journal would be an artefact of the
   teacher's editing, not of the teaching process. Nothing is lost: when the
   teacher publishes the work, the next run imports it. Cost: a piece of work
   deleted in Google stays in the local journal, which is the same behaviour
   US-014 chose for a vanished course (AC-005) and which the purge eventually
   resolves.
2. Import every state and store it, leaving the filtering to Epic 3. Faithful to
   "store what Google gives", but it adds a field §3 does not list and obliges
   every later report to filter correctly or show drafts to a Dean.
3. Import `PUBLISHED` and `DELETED`, skipping only drafts, so that a journal
   built for a past period still shows work that existed then. The most accurate
   for history, and the most expensive: it needs the state stored *and* a rule
   for how a deleted assignment appears in a cell, which BR-056 does not define.

## OD-005 An unrecognised submission state

BR-056 names the states a journal cell is computed from: `TURNED_IN` and
`STUDENT_EDITED_AFTER_TURN_IN` count as turned in, `CREATED` and
`RECLAIMED_BY_STUDENT` count as not turned in, and `RETURNED` is its own case.
Two problems sit behind that list. First, it is not identical to the enumeration
the Classroom API documents for `StudentSubmission.state`, and **nothing in this
repository verifies it**: the prototype reads only `userId` and `assignedGrade`
from a submission (`google_api.py:224-225`) and never touched a state, so there
is no empirical evidence here at all. Second, US-014 OD-010 already decided the
shape of the answer for an unfamiliar *course* state — skip that one course, log
a Warning, keep the run successful — and a closed vocabulary with a check
constraint is the established pattern.

**US-014's answer does not transfer unchanged, and the reason matters.** A
skipped *course* is visibly absent — it is simply not in the Dean's list. A
skipped *submission* is not absent at all: BR-056 reads a missing submission for
a student as **"не сдано"**, so skipping would make the journal state something
false about one child's work. The failure modes are not comparable, which is why
this decision has a fourth option US-014 did not need.

Options:

1. Store the state as a closed vocabulary and skip a submission whose state is
   outside it, with one Warning line carrying the state string and the submission
   id but never a name or a grade. Directly follows OD-010 and keeps a check
   constraint possible — but it is the option the paragraph above argues against:
   the skipped row becomes a false "not turned in" in the journal.
2. Store the state as the string Google sent, with no vocabulary. Nothing is ever
   lost, but every report must then handle an unknown value, and PC-3's closed
   vocabularies with check constraints are abandoned for this table.
3. Fail the run. Rejected by the same reasoning as OD-010: one unfamiliar value
   would stop a whole school's synchronization.
4. **Store the row, with the state as a closed vocabulary plus an explicit
   "unrecognised" marker carrying the raw string Google sent (recommended).** The
   submission exists, so no cell silently becomes "not turned in"; the vocabulary
   stays closed and checkable for every known value; the raw string is kept so the
   value can be identified without a second run. One Warning line is written as in
   option 1, with the state string and the submission id and nothing else
   (SC-10). **Cost, which must be stated in the Specification rather than
   discovered later:** US-025 inherits an obligation BR-056 does not currently
   describe — the journal must render an unrecognised state as its own thing,
   never as "не сдано" and never as a grade. A Story that ignores that obligation
   reintroduces exactly the defect this option avoids.

**Whichever option is chosen, the Specification must state which exact values the
vocabulary holds and record that BR-056's list is unverified against a live
Classroom response** — this is the first Story that reads a submission state at
all, so the discrepancy is real and must not be resolved by guessing.

## OD-006 A submitter who is on no roster: where the name and email come from

AC-004 requires a `ClassroomParticipant` and an off-roster `CourseMembership` for
a person known only through their submissions (BR-051 v56). A submission gives
only the Google `userId` — no email and no name. US-014 established that a
participant is identified by `userId` and that the email may legitimately be
absent (OD-006, OD-011 of US-014).

Options:

1. **Create the participant from the `userId` alone, leaving name and email
   absent (recommended).** No extra API call, no new scope question, and the
   schema already allows both to be absent. Cost: the journal shows such a person
   without a name until a roster sighting fills it in — and if they never return
   to the roster, never. For a leaver whose row exists only so the purge has a
   date, that may be exactly right.
2. Read the person's profile through `userProfiles.get` and store the name and
   email it returns. A named row in the journal; but it is one extra API call per
   unknown person, and whether the technical account may read a profile of someone
   no longer on any roster is unverified — §7 item 10 already records that the
   minimum role set was never checked.
3. Do not create a participant at all and attach the submission to nothing.
   Rejected: it contradicts BR-051 v56 and leaves the submission unreachable by
   the purge.

## OD-007 Does a `Submission` store Google's update time?

PC-13 lists exactly what a `Submission` row stores — "the Classroom state,
`assignedGrade`, `draftGrade`, the date of the last turn-in, and Google's `late`
flag" — and Google's `updateTime` is **not** on that list. PC-11, however,
defines a course's last activity as including "update of any of its `Submission`
rows", and OD-001 above may need that date to decide whether to import a course
at all. The two conventions, both derived from §5, do not agree on their face.

Options:

1. **Store Google's `updateTime` on the `Submission` row (recommended).** It is
   the only value that means what §5 says — when the submission changed *in
   Google*. The local `updated_at` of PC-6 records when *this program* wrote the
   row, which would tie a school's retention to the installation's sync history
   rather than to the teaching process. The Specification records that this adds
   one column beyond PC-13's list and why, so the convention can be amended
   rather than silently contradicted.
2. Use the local `updated_at` (PC-6) as the proxy for last activity. No new
   column, but a purge that is wrong in a way nobody can see: reimporting a school
   would refresh every row and postpone every expiry.
3. Store nothing and let US-037 work out the date. Cost: the data is gone by
   then — a retention rule cannot be reconstructed from rows that never recorded
   when Google changed.

**This is an apparent contradiction between two derived documents, not between
requirements. Whatever is decided, `trebovaniya.md` §5 wins and the losing
convention (PC-11 or PC-13) must be corrected** — the derived document is the one
that gets fixed (AGENTS.md).

## OD-008 One entity for coursework and materials, or two

§3 models a single concept — "**CourseWork** — задание или учебный материал
курса" with a type field — and BR-052 speaks of three *kinds*: graded work,
ungraded work and material. PC-3, on the other hand, notes that they are two
different Classroom resources and fixes the key as (resource, Google id), because
ids from the two resources may collide. `package-map.md` lists one entity
`CourseWork` and one enum `CourseWorkKind` that is computed and never stored.

Options:

1. **One `CourseWork` entity with a stored "which Classroom resource" field and a
   derived kind (recommended).** It is what §3 and `package-map.md` describe, it
   makes the journal's column list one query, and PC-3's composite key drops
   straight onto it. The two resources' differing fields are absent rather than
   wrong: a material has no due date and no maximum points, which is already
   expressible.
2. Two entities and two tables. Cleaner per-resource fields, but every journal
   query becomes a union, and `package-map.md` would have to change.

## OD-009 Reading the last turn-in date

§3 defines the submission date as "дата последней сдачи (последний переход в
`TURNED_IN` по истории сдачи)", and PC-13 confirms the history is read during
synchronization but **never stored** (Epic 12, BR-059). BR-058 restates the rule:
work turned in, reclaimed and turned in again shows the second date. The
prototype offers no evidence here — it never read a submission's history.

Options:

1. **Take the history from the submission resource itself and keep only the
   latest transition to `TURNED_IN` (recommended).** Classroom returns the
   history inline with the submission, so it costs no extra call; the program
   extracts one timestamp and discards the rest in memory, which is exactly what
   PC-13 describes. Cost: a larger response per submission, and a submission whose
   history Google truncates or omits has no date — which must be stored as absent,
   never invented.
2. Use the submission's `updateTime` as the date. One field, no history handling —
   and wrong whenever anything other than a turn-in changed the row, for example
   the teacher entering a grade.
3. Defer the date to Epic 12 with the rest of the history. Rejected: BR-058 and
   the journal need it in the first version, and it is a stored field of §3.

---

# Notes

- **The insertion point exists.** US-014 added the first pipeline step to
  `RunSynchronizationUseCase` behind the read-only guard; this Story's step runs
  in the same use case, inside the same per-course transaction if OD-003 keeps
  US-014's boundary. The guard-first order is not this Story's to change.
- **No new scope.** `classroom.coursework.students.readonly` and
  `classroom.courseworkmaterials.readonly` are already two of the six scopes fixed
  in §6 and tied to the requirement by a test. The materials scope was added in
  v24 precisely because "without it the prototype silently saw none" — a Story
  that needed a seventh scope would require a new version of `trebovaniya.md`.
- **The port is `IClassroomReader`**, already declared and already carrying
  `IGoogleDataPort`, which is what makes US-007 FR-007 bind: a use case holding it
  also holds `IReadOnlyModeGuard` and calls it first. Extending it keeps that
  protection automatically; a *new* port would have to carry the same marker, and
  `ReadOnlyEnforcementTests` would fail if it did not.
- **Prototype evidence, with its gaps.** It pages `courseWork().list` and
  `courseWorkMaterials().list` (`google_api.py:163`, `:176`) and reads submissions
  per assignment (`:210`); its date cascade (`:107-124`) is exactly §3's. But it
  stores **no** coursework, material or submission table at all (`db.py` has three
  tables: courses, metadata, users), and from a submission it reads only `userId`
  and `assignedGrade` — never a state, a draft grade, a `late` flag or a history.
  So it is evidence for *how to call* Classroom and for nothing about what to
  store. Its retry codes (429, 500, 502, 503, 504, exponential backoff) belong to
  US-017.
- **A grade is personal data.** §3's participants are students, potentially
  minors; SC-10 keeps names and emails out of logs, and a grade belongs in the
  same category. No log line, no error message and no `SyncState` diagnosis may
  carry one.
- **No user-visible string.** Nothing here reaches a screen — the journal is
  US-025, the statistics view is US-024 — so no translation key is added. If the
  Specification finds a message that does reach a user, NFR-073 applies in full
  and both language files grow together.
- **Audit stays untouched**, as in US-013 and US-014: a scheduled run writes no
  audit row, so `AuditAction`, `AuditTargetType` and their check constraints do
  not grow.
- **The counter still counts courses** (US-014 OD-005). That decision was taken
  with this Story in view — "it stays comparable when US-015 adds coursework
  beneath the same courses" — so `SyncState` is not redesigned and its migration
  is not amended.
- **PC-11 shapes the schema even though the purge is US-037.** The unit of
  deletion is the course with its work and submissions; a leaver's submissions go
  with their membership. DB_DESIGN should check the new columns against PC-11 and
  §5, not only against §3, and the foreign keys should be `RESTRICT` for the same
  reason US-014 gave: a cascade from a course would silently take grades with it,
  and the purge deletes in an explicit order inside one transaction.
- **Known limitation to carry into the Specification** (§5 v55): submissions of
  people still on a roster have no expiry of their own — they live until the
  course expires or the person expires as a leaver.
- Nothing in this Story touches the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.
