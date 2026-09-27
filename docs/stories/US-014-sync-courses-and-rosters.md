---
id: US-014
epic: EPIC-1
title: Sync courses and rosters
slug: sync-courses-and-rosters
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
# OD-001 … OD-009 were all resolved by the Owner on 2026-09-27, before
# activation.
---

# User Story

As a **Dean or Admin**

I want the school's Classroom courses and the people on their rosters — each with
the role they hold in that course — to be present in the installation's own
database, refreshed by the background service on every run

So that every later screen, journal and Meet report has a local roster to work
from, and so that the system can answer who taught and who studied on a course
without calling Google for each question.

---

# Business Value

`trebovaniya.md` §4 Epic 1 asks for it in one sentence: "Как система, я
периодически (или по запросу) синхронизирую список курсов, участников с их
ролями в курсах, заданий/материалов и оценок через Classroom API". US-013 built
the run; this Story is the run's **first actual step** — until it exists, every
run completes with a counter of zero and the installation's database holds no
teaching data at all.

Nothing downstream can start without it. EPIC-2 (US-020 course list, US-021
course detail with roster, US-022 teachers and their courses) reads exactly the
three entities this Story creates; US-015 attaches `CourseWork` and `Submission`
to the courses it imports; Epic 4's Meet reports resolve a person against "the
roster on the meeting's date" (BR-051), which only exists once memberships carry
first-seen and last-seen observations; and PC-11's retention purge deletes by the
unit this Story establishes — the course.

§3 also makes a modelling point this Story is responsible for getting right: a
Classroom role belongs to the **membership**, not the person, because "Один
человек может вести один курс и учиться на другом" (BR-050). Getting that wrong
here would be expensive to correct later.

---

# Scope

**In scope:**

- `IClassroomReader` — the Google Classroom port `architecture.md` AD-4, TC-4 and
  `package-map.md` already name, declared in `Application/Ports` and implemented
  in `Infrastructure/Google` beside `GoogleAccessProbe`; no Google SDK type
  crosses into `Application` or `Domain` (AD-4);
- three entities with their table, mapping and **one** EF Core migration in this
  Story (PC-2): `Course`, `ClassroomParticipant`, `CourseMembership`, with the
  fields `trebovaniya.md` §3 lists for each;
- the first pipeline step inside `RunSynchronizationUseCase` — the place US-013
  marked for it — reading courses, then each course's teachers and students, and
  upserting them on their Google-side identifiers (BR-041, PC-3, PC-10);
- the membership observation rule of BR-051: first seen, last seen and whether
  the person is on the roster now; **a membership is never deleted** when someone
  leaves a roster — the flag and the last-seen date change;
- the role on the membership, never on the person (BR-050, §3);
- paging through every Classroom list response, so a large school is imported
  whole — the prototype lost data until it added paging (`google_api_OLD.py`
  fetched one page of `students().list`);
- the run counter US-013 established, reporting what OD-005 fixes;
- logging per DC-10 inside the existing run: no name, no email, no raw Google
  error (SC-10, AC-007 of US-013 still binding);
- read-only mode: the guard already runs first in `RunSynchronizationUseCase`, so
  this Story must prove that the **new** Google port is never reached in
  read-only mode (SC-5, AD-6, TC-5).

**Out of scope:**

- `CourseWork`, course materials and `Submission` with grades — **US-015**,
  including the part of BR-051 that gives a student with submissions but no
  roster sighting an off-roster membership (that rule needs submissions to
  exist);
- classifying a Google failure, separating `403 unauthorized_client` from a
  transient `429`/`5xx`, retry and exponential backoff, and the human-readable
  diagnosis shown to the Admin — **US-017** (AD-5, Epic 1, Epic 6). This Story
  lets a failure surface and US-013 records `SyncState` as failed;
- incremental behaviour — not re-fetching already-known participants (BR-042) —
  **US-018**;
- the not-yet-imported-course age rule of PC-11 / §5 v36, unless OD-001 resolves
  otherwise;
- the retention purge itself — **US-037** (PC-11);
- every screen that displays this data — **EPIC-2** (US-020 … US-022) and Epic 5
  (US-024); this Story adds no page, no endpoint and no translation key;
- Meet sessions, meeting codes and their linking — Epic 4 (US-031, US-032);
- starting a run from the UI and its audit row — **US-019**;
- any change to the six scopes, the service-account key handling or impersonation
  — all established by US-010 / US-011 and reused unchanged;
- any write to Google Workspace — a Hard Stop, not a scope choice.

---

# Acceptance Criteria

## AC-001 A run imports the school's courses

**Given** an installation with a saved `WorkspaceConnection`, not in read-only
mode

**When** a synchronization run executes

**Then**:

- every course Classroom returns for the school is present in the installation
  database with the fields §3 lists: id, name, section, description, room, owner,
  creation and update times, state, link, teacher folder and `calendarId`;
- the course's state is stored as Classroom reports it — one of `ACTIVE`,
  `ARCHIVED`, `PROVISIONED`, `DECLINED`, `SUSPENDED` (§3), with no value invented
  for "no longer returned by Google";
- list responses are paged through to the end; a school with more courses than one
  page is imported whole;
- the course is keyed on the Google course id, with a unique index, and the
  primary key is the surrogate `long Id` (PC-3).

## AC-002 A second run changes nothing it should not

**Given** a run has already imported the school's courses and rosters

**When** the same data is imported again with nothing changed in Google

**Then**:

- no row is duplicated — every write is an upsert on the Google-side identifier
  (BR-041, PC-10);
- a course whose card changed in Google is updated in place and keeps its
  surrogate identity, so anything referring to it still does;
- the run completes and `SyncState` records it as US-013 already specifies.

## AC-003 Each course's roster is imported with the role on the membership

**Given** a course with teachers and students, including a co-teacher who is not
the course owner

**When** the run imports that course

**Then**:

- every teacher and every student on the roster has a `ClassroomParticipant` row
  keyed on the Google `userId`, carrying the email and the name Classroom gives
  (§3);
- the same person appearing on two courses has **one** `ClassroomParticipant` row
  and **two** `CourseMembership` rows;
- the Classroom role (`teacher` / `student`) is recorded on the membership, never
  on the person, and a person who teaches one course and studies on another is
  stored exactly that way (BR-050, §3);
- co-teachers are imported, not only the course owner — the prototype's
  owner-only model is not the requirement (§3 "курс может иметь и других
  со-преподавателей").

## AC-004 A membership records what synchronization observed

**Given** a person on a course roster

**When** runs happen over time

**Then**:

- the membership records when synchronization **first** saw the person on that
  roster, when it **last** saw them, and whether they are on it **now** (BR-051,
  §3);
- when the person disappears from the roster, the membership is **not deleted**:
  the on-roster flag becomes false and the last-seen date stops advancing
  (BR-051, §5 v31);
- if that person returns to the roster, the same membership is reused — the
  on-roster flag becomes true again and the last-seen date advances; the
  first-seen date is never rewritten;
- everything before the first run counts as having started on the day of that run
  — the documented accuracy limit, not a defect (§3).

## AC-005 A course Google no longer returns is left alone

**Given** a course that was imported and that Classroom no longer returns

**When** a run executes

**Then** the course, its memberships and its participants stay in the database
untouched — only the retention purge deletes a course, and it deletes by age in
any state (§5 v36, PC-11, US-037). No row is marked, hidden or deleted by
synchronization, and no status outside the five Classroom values is invented.

## AC-006 Read-only mode never reaches the new port

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (BR-025)

**When** the schedule would start a run

**Then**:

- no call is made through `IClassroomReader`, including the token request, proven
  in the Application layer with the port substituted (SC-5, AD-6, TC-4, TC-5);
- no course, participant or membership row is written or updated — a
  synchronization write is not on the BR-026 closed list;
- `PermittedServiceWrites` does not grow, because the new writes happen inside
  `RunSynchronizationUseCase`, which already calls `IReadOnlyModeGuard` first
  (US-013 spec FR-006).

## AC-007 A failure part-way through leaves a consistent database

**Given** a run that fails while importing — Google stops answering after some
courses are already saved

**When** the run ends

**Then**:

- `SyncState` records the run as failed with a diagnosis carrying no personal
  data and no raw Google error text (SC-10, US-013 AC-006);
- what was committed before the failure is consistent at the boundary OD-009
  fixes — no half-written course with a partial roster;
- the next run imports the rest without duplicating anything (BR-041);
- the service does not stop and the host is not taken down (US-013 AC-006).

## AC-008 No personal data reaches a log, and none leaves the installation

**Given** a run importing real rosters

**When** its log lines are read

**Then**:

- no line carries a name, an email address, a course name, a Google id of a
  person, or a raw Google error object — counters and states only (SC-10, DC-10);
- the run's identifier is on every line, as US-013 AC-007 requires;
- nothing is sent anywhere but Google and the Control Plane (SC-13); the imported
  data is written only to the installation's own database;
- a scheduled run still writes **no audit row** (US-013 spec FR-017); the audit
  vocabulary and its check constraints do not grow.

## AC-009 Tests never reach Google, and the schema is tested for real

**Given** the test suite

**When** it runs

**Then**:

- every Classroom call goes through the substituted `IClassroomReader` with
  synthetic fixtures — never a real roster, name, email or school domain (TC-4);
- the three tables, their unique indexes and their constraints are tested against
  real PostgreSQL through Testcontainers; the InMemory provider is not used
  (TC-2);
- paging is covered by a fixture returning more than one page, so a
  single-page implementation fails;
- the BR-051 sequence is covered end to end: seen, gone, seen again — asserting
  the first-seen date never moves;
- both the allowed case and the read-only refusal are covered in the Application
  layer (TC-5).

---

# Open Decisions

## OD-001 Does this Story apply the "course older than N years" rule?

`trebovaniya.md` §5 (v36, v55) requires that synchronization **not import a new
course** whose last activity in Google is already older than the retention period
N, so that the purge and the next sync never undo each other. "Last activity" is
defined as the latest of: the course's own update time, creation or update of any
`CourseWork`, update of any `Submission`, and the start of any linked Meet
session.

The difficulty is that **this Story sees only the first of those four**. Coursework
and submissions arrive with US-015, Meet sessions with US-031. Judging age on
`course.updateTime` alone would be wrong in the dangerous direction: a course
whose card has not changed in years but whose assignments are current would be
refused import, and because it is never imported, US-015 would never see its
assignments either. The mistake is not self-correcting.

Note also that the retention period N is a **required** installation setting by
DC-3 and PC-11, but no code reads it yet
(`InstallationSettingsReader.cs`: "The time zone, retention period and default
language arrive with the Stories that use them"). Whichever option is chosen
decides whether this Story introduces that setting.

Options:

1. **Do not apply the rule here (recommended).** Import every course Classroom
   returns. The rule is implemented by the Story that can see a course's full last
   activity — US-015 at the earliest, and in practice it only starts to matter
   when the purge exists (US-037), since nothing deletes data before then. This
   Story then does not read the retention setting at all. Known limitation to
   record: between this Story and that one, a fresh installation may import a
   course whose data is already past N.
2. Apply the rule on `course.updateTime` alone, and accept that a course with an
   old card and current coursework is not imported. Cheaper now, wrong in a way
   later Stories cannot repair.
3. Apply the rule properly here by also reading coursework and submission dates
   for candidate courses — which pulls a large part of US-015 into this Story and
   costs an extra API call per unknown course.

**Resolution:** option 1, decided by the Owner on 2026-09-27. This Story imports
every course Classroom returns and does **not** read the retention period — the
setting stays unread until the Story that owns the rule. The Specification must
record the limitation explicitly: until the age rule exists, a fresh installation
may import a course whose data in Google is already past N. Nothing is lost by
waiting, because no code deletes data until the purge (US-037) exists.

## OD-002 Which `courseStates` are requested, and are all five stored?

§3 lists five states (`ACTIVE`, `ARCHIVED`, `PROVISIONED`, `DECLINED`,
`SUSPENDED`) and §5 says the purge deletes a course "в любом статусе". Epic 2
gives the Dean a status filter, which implies more than one state is stored. The
prototype passed no `courseStates` filter at all (`google_api.py:56`).

Options:

1. **Request without a state filter and store the state as given (recommended).**
   Consistent with §5's "any state", with Epic 2's filter, and with the prototype's
   empirical behaviour. Filtering becomes a reading concern, not an import one.
2. Request only `ACTIVE` and `ARCHIVED`. Smaller database, but `PROVISIONED` and
   `DECLINED` courses become invisible to the Dean's status filter, and a course
   changing state later looks like a new course.

**Resolution:** option 1, decided by the Owner on 2026-09-27. No state filter is
sent, and the state is stored exactly as Classroom reports it. Filtering is a
reading concern (Epic 2), never an import one, and all five §3 states are storable.

## OD-003 Will the technical account actually see the school's courses?

`courses.list` returns the courses the **calling user** may see. The prototype got
the whole domain because it impersonated a **super-admin**
(`with_subject('admin@dac.ukr.education')`), which AGENTS.md explicitly says is
not a precedent. BR-015 gives the technical account "read-only roles for Classroom
and Admin Reports only", and `trebovaniya.md` §7 item 10 records that the minimum
sufficient role set "точный минимум не проверялся". §7 also states that the
remaining items do not block the start of development.

So there is a real possibility that a correct implementation returns zero courses
on a live domain because of a Workspace role, not a defect in this code.

Options:

1. **Implement `courses.list` without a per-user filter and treat verification as a
   deployment task (recommended).** It is the only documented way to ask for a
   domain's courses, US-011's "Check access" button already exercises
   `courses.list` against the live domain, and §7 item 10 is a
   verify-at-onboarding item rather than a blocker. The Story records the risk so
   that an empty import on a live domain is diagnosed as a role problem first.
2. Block this Story until the Owner verifies the minimum role set on the live
   domain — which resolves §7 item 10 but stops EPIC-1 in the meantime.
3. Read courses per teacher by first enumerating domain users, which needs a
   Directory API scope that is **not** on the fixed six-scope list (§6 v25) and
   would therefore require changing `trebovaniya.md`.

**Resolution:** option 1, decided by the Owner on 2026-09-27. `courses.list` is
called without a per-user filter and verification of the technical account's
Workspace roles stays a deployment task (§7 item 10, which §7 itself says does not
block development). The Specification must record the diagnostic consequence: an
empty import on a live domain is a role problem to check first, not a defect in
this code, and it is not an error the run reports as a failure.

## OD-004 How the course owner is stored

§3 lists `owner` among the course's fields — "преподаватель, создавший курс —
основной владелец по данным Classroom API" — while also saying co-teachers are
represented as memberships. Classroom returns `ownerId` on the course and
normally also lists that person in `teachers().list`, but the API does not
guarantee it.

Options:

1. **Store the Google `ownerId` as a plain column on `Course`, and let the owner's
   membership come from the roster like anyone else's (recommended).** The column
   answers "who created this course" straight from Classroom, no foreign key can
   be violated when Classroom is inconsistent, and §3's two statements stay
   independent. US-022 ("teachers and their courses") then reads memberships, not
   the owner column.
2. Make the owner a foreign key to `ClassroomParticipant`. Cleaner in the model,
   but an owner missing from the teacher roster would make a course unimportable —
   a Google inconsistency becoming a failed run.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The Google `ownerId`
is a plain column on `Course` with no foreign key; the owner's membership comes
from the teacher roster like anyone else's. US-022 reads memberships, not this
column.

## OD-005 What the run counter counts

US-013 deliberately kept **one** counter, because §3 writes "счётчик" in the
singular (US-013 spec I-8), and it is currently always zero. This Story is the
first that has something to count, and Epic 5 (US-024) will show it.

Options:

1. **Courses processed (recommended).** §5 makes the course the unit of storage
   ("Единица хранения — курс"), so "142 courses" is the number that means
   something to an Admin, and it stays comparable when US-015 adds coursework
   beneath the same courses.
2. Courses plus participants plus memberships, summed. A larger number that
   changes meaning with every later Story.
3. Several counters. This contradicts §3's singular and would reopen US-013's
   `SyncState` design and its migration.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The counter reports
**courses processed**. `SyncState` is not redesigned and its migration is not
amended; participants and memberships are not counted in it.

## OD-006 A roster entry that is not a personal domain account

§3 says the subjects of the teaching process are teachers and students "каждый со
своим личным аккаунтом в домене", and that "групповые адреса Google и прочие
аккаунты субъектами не считаются". Classroom can nevertheless return a roster
entry that is an external address, a shared account, or a profile whose email is
absent. BR-055 assumes grades exist only for personal domain accounts, and BR-064
already has a category for "other participants" in Meet reports.

Options:

1. **Import the roster as Classroom gives it, including entries outside the school
   domain and those with no email (recommended).** The roster is an observation,
   and "is this a subject of the teaching process" is a question the reports
   answer (BR-064), not a filter on import. An entry with no email is stored by
   its Google `userId` with the email absent. Nothing is discarded, so no report
   silently misses a person.
2. Skip roster entries whose email is outside the school domain. Smaller data, but
   a co-teacher from another domain vanishes from the course with no trace, and the
   Dean cannot tell whether the course has one teacher or two.
3. Fail the run on such an entry. A Google-side reality becoming an outage.

**Resolution:** option 1, decided by the Owner on 2026-09-27. The roster is imported
as Classroom gives it: an entry outside the school domain is stored, and an entry
with no email is stored by its Google `userId` with the email absent. Nothing is
discarded and no run fails over it. Whether a person is a subject of the teaching
process is decided where the reports are built (BR-064), so the Specification must
not turn §3's "групповые адреса … субъектами не считаются" into an import filter.

## OD-007 How much of BR-042 (incremental) belongs here

BR-042 and Epic 1 ask that already-known participants not be re-fetched, and the
catalog has a separate Story for incremental synchronization (US-018).

Options:

1. **A full pass here; incremental behaviour entirely in US-018 (recommended).**
   This Story reads every course and every roster on every run and upserts
   idempotently (BR-041). US-018 then adds the "don't re-fetch what we know"
   optimisation with the evidence of a working full import to compare against.
   Matches the catalog's split.
2. Implement BR-042 here. Merges the two Stories, and an optimisation written
   before a correct baseline exists is hard to prove correct.

**Resolution:** option 1, decided by the Owner on 2026-09-27. A full pass every
run, idempotent on the Google-side identifiers (BR-041). BR-042 belongs entirely to
US-018, whose catalog entry stays.

## OD-008 Does this Story classify Google failures at all?

Epic 1 and AD-5 require that a permission failure (`403 unauthorized_client`,
`access_denied`, missing scope) is **not** retried and is recorded in `SyncState`
with a human-readable diagnosis shown to the Admin, while `429`/`5xx` are retried
with backoff. US-017 owns exactly that, and US-013 records only
`RunFailed:<exception type>` (SC-10).

Options:

1. **No classification here (recommended).** A Classroom read failure propagates,
   US-013's existing handler records the run as failed, and US-017 adds
   classification, the no-retry rule for permission errors and the Admin-facing
   diagnosis. Keeps this Story about data and keeps one owner for error policy.
2. Classify permission failures here because this is the first Story that can
   actually provoke one. Splits ownership of error policy across two Stories.

**Resolution:** option 1, decided by the Owner on 2026-09-27. No classification, no
retry and no Admin-facing diagnosis here: a Classroom read failure propagates and
US-013's existing handler records the run as failed with `RunFailed:<type>` (SC-10).
US-017 owns error policy in one place.

## OD-009 The transaction boundary of an import

AD-7 puts transactions in the Application layer. A run may process hundreds of
courses, each with a roster.

Options:

1. **One transaction per course — the course, its participants and its memberships
   committed together (recommended).** A run that fails on course 300 leaves 299
   complete courses and no half-written one; the idempotent upsert makes the next
   run finish the job (BR-041, AC-007). Also keeps transactions short, which PC-1
   prefers on PostgreSQL.
2. One transaction for the whole run. All-or-nothing is simpler to reason about,
   but one unreachable course discards a completed import of the entire school, and
   a large school holds a transaction open for a long time.
3. No explicit transaction — one `SaveChangesAsync` per course. Nearly option 1 in
   practice, but a course and its roster could be committed separately, which
   AC-007 forbids.

**Resolution:** option 1, decided by the Owner on 2026-09-27. One transaction per
course, through `IUnitOfWork.ExecuteInTransactionAsync`, covering the course, its
participants and its memberships together. A run that fails part-way leaves only
complete courses behind, and the next run finishes the job.

---

# Notes

- **The insertion point already exists and is marked.**
  `RunSynchronizationUseCase.ExecuteAsync` carries the comment "The pipeline of
  this Story has no step (OD-001). US-014 adds the first one, and the counter it
  reports replaces the zero below" — that is where this Story's step goes, behind
  the read-only guard that runs first.
- **The port name is already fixed by the conventions**, not chosen here:
  `architecture.md` AD-4, `package-map.md` and TC-4 all name `IClassroomReader`,
  implemented in `Infrastructure/Google`. A different name would contradict three
  approved documents.
- **`GoogleAccessProbe` is the precedent for the adapter**: one shared
  `HttpMessageHandler` so tests drive it offline and nothing but Google is
  reachable (SC-13), the client library's own retry switched off, the key resolved
  from `ISecretStore` per request, and no Google error text logged or returned.
  Note the naming drift to record but not to fix here: the conventions call the
  credential port `IWorkspaceCredentialProvider`, while the code has `ISecretStore`
  plus `GoogleServiceAccountSettings`.
- **The prototype is empirical evidence with three known gaps.** It never calls
  `courses().teachers().list` at all — its only "teacher" is the course owner, so
  co-teachers are absent from both its API calls and its schema. It stores no
  students in its database. It marks nothing as gone. Its paging was added after
  data was lost (`google_api_OLD.py` fetched a single page of students). None of
  those is a model to copy; the working details worth reusing are the paging loop
  and the retry codes, and the latter belongs to US-017.
- **The prototype's `with_subject('admin@dac.ukr.education')` is a super-admin.**
  AGENTS.md names this explicitly as not a precedent; the impersonated account is
  the technical account from `WorkspaceConnection` (BR-015), and OD-003 is where
  the consequence is decided.
- **No new scope.** The six scopes are fixed in `GoogleDelegationScopes` and tied
  to §6 by a test; courses and rosters are covered by
  `classroom.courses.readonly`, `classroom.rosters.readonly` and
  `classroom.profile.emails`, all already authorised. A Story that needed a seventh
  scope would require a new version of `trebovaniya.md`.
- **No user-visible string.** Nothing here reaches a screen — the course list is
  US-020, the roster is US-021, `SyncState` is US-024 — so no translation key is
  added. If the Specification finds a message that does reach a user, NFR-073
  applies in full and both language files grow together.
- **Audit stays untouched**, as in US-013: a scheduled run writes no audit row, so
  `AuditAction`, `AuditTargetType` and their check constraints do not grow.
- **`MeetParticipation` must not gain a foreign key to `ClassroomParticipant`**
  when this Story creates that entity — PC-12 calls such a migration a finding;
  the person is resolved by email against the roster on the meeting's date
  (BR-051).
- **PC-11 shapes the schema even though the purge is US-037.** The unit of deletion
  is the course with everything under it, and a leaver's membership expires on its
  own `last_seen_at`; the columns this Story creates are what that purge will use,
  so DB_DESIGN should check them against PC-11 rather than only against §3.
- Nothing in this Story touches the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.
