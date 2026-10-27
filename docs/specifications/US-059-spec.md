---
artifact_type: specification
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T09:45:51Z
updated_at: 2026-10-27T09:45:51Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-059-sync-marks-courses-gone-and-skips-negative-values.md
    version: null
  - path: trebovaniya.md
    version: 101
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
supersedes: null
---

# US-059 Specification — Synchronization marks courses no longer in Classroom or hidden from a new technical account, guards against an empty course list, and skips a course with negative values

Delegation: three read-only `quick-look` lookups collected (a) the `trebovaniya.md` v100/v101
passages — changelog, §3 `Course`, `WorkspaceConnection`, `SyncState`, §4 Epic 1 "Курс, которого нет
в Classroom", "Пустой список", "Смена технического аккаунта", §7; (b) BR-026, BR-043, BR-047,
BR-048, BR-049, BR-072, SC-10, SC-11, PC-2, PC-6 and the glossary entries; (c) code facts (§1.1).
The spec writer checked the code facts against `RunSynchronizationUseCase.cs` lines 455–650,
`SaveWorkspaceConnectionUseCase.cs` lines 100–135 and `WorkspaceConnection.cs` lines 50–95.

## 1. Overview

`trebovaniya.md` v100 and v101 change what synchronization does with the course list and with
negative values. This Story stores the facts; showing them is v99 Stories 2 and 3.

1. **A course Google no longer lists** (v100, BR-048). A stored course missing from a course list
   read to its end is kept and marked "not in Classroom" with the instant the run noticed it. The
   mark goes when the course is on the list again.
2. **A new technical account** (v101, BR-049). `WorkspaceConnection` records when the technical
   account last changed; every course records when synchronization last saw it on the list. A
   missing course last seen **before** the change is marked "not visible to the technical account"
   instead of "not in Classroom".
3. **An empty course list** (v100, BR-048). A list read to its end with no course in it, while
   courses are stored, marks nothing and sets a flag in `SyncState`.
4. **A negative grade, draft grade or maximum points** (v100, BR-043). The course is skipped like
   one with a value above the range, instead of failing the whole run on a database check
   constraint.

### 1.1 Code facts the Specification must not contradict

- `RunSynchronizationUseCase.ImportCoursesAsync` (≈465) takes one instant `observedAt` for every
  observation of the run, loads every skip record, and builds `listed` — the set of Google course
  ids the list yielded, each trimmed and cut to `Course.MaxGoogleIdLength` by `RecordKeyOf`
  (≈637), the way `Course.Import` stores `Course.GoogleId` (`Course.cs` ≈116). An entry with a
  blank id adds nothing to `listed`.
- Per listed course, in order: unknown state → skip + skip record; blank name → skip; unchanged
  archived (not in the weekly full run) → skip, nothing read or written; reads (roster, items,
  submissions) before any transaction, catching `CourseGone` (`404`) → skip and `ValueOutOfRange`
  → skip + skip record; then one transaction per course (`ImportCourseAsync`), which may return
  "aged out" for a new course older than N.
- After the `await foreach` ends — reached only when the list was read to its end — the skip
  records of courses not in `listed` are removed and saved (≈588–598, US-058 FR-007.3). A run that
  stops before that point never reaches it.
- A run that fails calls `unitOfWork.DiscardUncommittedChanges()` and `SyncState.FailRun`
  (≈141–152); what was already saved stays.
- `GoogleClassroomReader.Points` (≈392–411) converts `maxPoints`, `assignedGrade` and
  `draftGrade` (≈352, ≈384, ≈385) from `double` to `decimal` and throws
  `GoogleReadFailedException(GoogleReadFailureKind.ValueOutOfRange)` on overflow. A negative value
  passes and violates `ck_course_work_max_points_non_negative` or
  `ck_submission_grades_non_negative` at save, which ends the run.
- `Course` has no mark and no "last seen on the list" instant today. `CourseWork` carries
  `NotInClassroomSince` (US-058).
- `SyncState` has no empty-list flag today.
- `WorkspaceConnection` stores `ImpersonationUserEmail` normalised by `NormalizeEmail` (trimmed,
  lower-cased, domain part normalised). `SaveWorkspaceConnectionUseCase.StoreAsync` (≈103) takes
  `now` once, then in one transaction either `Create`s the connection (first save) or calls
  `ChangeTo`, and writes the audit row `WorkspaceConnectionSaved` (SC-11). The use case refuses
  in read-only mode before that. Nothing else writes `WorkspaceConnection`.
- Run-log events in `SynchronizationBackgroundService` are 5121–5136 (5129 unused); 5135
  `SyncCourseValueOutOfRange` writes a skipped course's id without its values.
- The retention purge computes a course's last activity from Google-side times only (US-058
  §1.1).

## 2. Business Goal

Deans take what the program shows as the truth (Owner, 2026-10-26). A course deleted in Classroom
silently passes for a live one today, and one negative value in one course stops every course from
updating. Storing the marks now lets Stories 2 and 3 show them. Telling a lost course apart by the
technical account's change keeps the Dean from asking teachers about courses nobody deleted.

## 3. Business Flow

For each run (scheduled or by the button — the same rules, §4 Epic 1):

1. The run reads Google's course list and handles each listed course as today (§1.1). A course
   in which Google returns a negative value is skipped for out-of-range values (FR-008).
2. If the run stops before the list has been read to its end, nothing below happens: no mark is
   set or removed, no sighting is recorded and the empty-list flag is unchanged (FR-001.4).
3. Once the list has been read to its end:
   - **No usable course in the list and at least one course stored** → the empty-list flag is set;
     nothing is marked or unmarked; no skip record is removed (FR-005).
   - **Otherwise** → the empty-list flag is cleared; every stored listed course records the
     run's instant as its last sighting and loses its mark if it has one (FR-003, FR-004); every
     stored course not listed and not yet marked gets a mark — "not visible to the technical
     account" if it was last seen before the technical account's last change, otherwise "not in
     Classroom" (FR-001, FR-006); skip records of unlisted courses are removed as today.
4. The run writes one log line with the counts of courses newly marked of each kind and
   unmarked (FR-007).

Separately, when the Admin saves the connection with a different technical account, the save
records its instant as the technical account's last change (FR-002).

## 4. Functional Requirements

### FR-001 A course Google no longer lists is marked "not in Classroom"

1. A **stored course** is a `Course` row in the installation's database at the moment the list
   has been read to its end, archived ones included (BR-047: archived courses keep their marks).
2. When the run has read the course list to its end and the list is not empty in the sense of
   FR-005.1, every stored course whose `GoogleId` is not in `listed` (§1.1), and which carries no
   mark, gets a mark with the instant `observedAt` of the run. The kind of the mark is "not in
   Classroom" unless FR-006 makes it "not visible to the technical account".
3. The marked course is otherwise untouched: its Google-sourced fields, including its state,
   keep their stored values; its roster, items, submissions and grades are neither changed nor
   deleted; its items get no mark of their own (BR-048). Nothing is deleted (PC-11).
4. Marks are set only at the point after the list's enumeration has finished (§1.1). A run that
   stops before it sets none and removes none (AC-005).
5. A course "not in Classroom" because a run met it as `404` is not marked in that run: it was
   listed. It is marked only if a later list lacks it (BR-048).
6. The mark is a synchronization record, not an edit of the mirror (§3 `Course`, v100): it is
   stored beside the Google-sourced fields and never overwrites one.

### FR-002 The technical account's last change is recorded

1. When `SaveWorkspaceConnectionUseCase` changes an existing connection (`ChangeTo`) to an
   impersonation email that differs from the stored one after both are normalised by
   `WorkspaceConnection.NormalizeEmail` and compared ordinally, the connection records the save's
   `now` as the instant of the technical account's last change, in the same transaction as the
   save.
2. Saving the same email (including in different letter case or with surrounding spaces) records
   nothing and keeps the previous instant. The first save of a connection (`Create`) records
   nothing (AC-011).
3. A connection saved before this Story's migration has no change instant until its next change
   of email.
4. Nothing else changes: the save's refusals, its read-only refusal and its single audit event
   `WorkspaceConnectionSaved` stay as they are; no new audit event (SC-11 already lists "saving or
   changing `WorkspaceConnection`").

### FR-003 A listed course records when it was last seen on the list

1. When the run has read the list to its end and the list is not empty (FR-005.1), every stored
   course whose `GoogleId` is in `listed` records `observedAt` as its last sighting on the list —
   whether the run read it, skipped it (unknown state, blank name, unchanged archived, `404`,
   out-of-range or negative values) or wrote it in this run (AC-016).
2. The sighting is a synchronization record, not an edit of the mirror (BR-049). It is not a
   Google-side time and does not enter the retention purge's last activity (§1.1, BR-072).
3. A course stored before this Story's migration has no sighting until the first run that reads
   the list to its end with the course on it (see FR-006.3).

### FR-004 A course that comes back loses its mark

1. When the run has read the list to its end, a stored course in `listed` that carries either
   mark loses it (both its kind and its instant), whether or not the run read the course (BR-048:
   "if the course comes back on the list, the mark is removed").
2. A course that comes back and is read in this run is updated from Google's data as every read
   course is (`UpdateFrom`, PC-10), so it carries the new name (AC-003).

### FR-005 An empty course list marks nothing

1. The list is **empty** when its enumeration finished and `listed` holds no id — no entry, or only
   entries without a usable id (I-4).
2. An empty list while at least one course is stored:
   - sets no mark, removes no mark and records no sighting;
   - removes no skip record — the removal of US-058 FR-007.3 is held back too;
   - sets the `SyncState` flag "the last course list came back empty".
3. A list read to its end that is not empty, or empty with no course stored, clears the flag (if
   set). So the flag holds exactly when the most recent list read to its end was empty while
   courses were stored (AC-007, AC-008).
4. A run that stops before the end of the list leaves the flag unchanged.
5. The flag is a fact for Story 3's technical problem "Google не вернул ни одного курса —
   проверьте роль технического аккаунта" (§4 Epic 1); this Story does not show it. The run's own
   outcome is unchanged: a run with an empty list completes as today (I-5).

### FR-006 A course lost after the technical account changed is "not visible to the technical account"

1. A course that FR-001.2 would mark gets the mark "not visible to the technical account"
   instead of "not in Classroom" when the connection has a change instant (FR-002) and the course's
   last sighting (FR-003) is strictly earlier than that instant (BR-049).
2. A course whose last sighting is at or after the change instant — the new account saw it at least
   once — is marked "not in Classroom" (AC-013). With no change instant recorded, every mark is
   "not in Classroom".
3. A course with no sighting at all (stored before this Story's migration, or stored by runs that
   never read the list to its end) counts as seen before any recorded change: it is marked "not
   visible to the technical account" when a change instant exists, "not in Classroom" otherwise
   (I-1).
4. The "not visible" mark keeps its first instant while the course stays absent and goes when the
   list yields the course again (FR-004, AC-014).
5. **A marked course keeps the mark it has.** A course already carrying either mark is not
   re-marked in a later run, even if the rule would now give the other kind (AC-015).
6. The empty-list guard (FR-005) applies first and marks nothing.

### FR-007 One run-log line counts course marks

1. A run that has read the list to its end, and in which at least one course was newly marked or
   unmarked, writes **one** log line with three numbers: courses newly marked "not in Classroom",
   courses newly marked "not visible to the technical account", and courses unmarked. A course
   already marked and still absent is not counted (I-3).
2. A run whose list came back empty while courses are stored writes one log line saying so, with
   the number of stored courses and nothing else.
3. Neither line carries a course name, a course or item Google id, a person, an email or a grade
   (SC-10).
4. Both are new run-log events next to 5121–5136; their ids and names are chosen at
   IMPLEMENTATION in the existing sequence.

### FR-008 A negative value skips the course

1. A course in which Google returns a `maxPoints` of a course work, or an `assignedGrade` or
   `draftGrade` of a submission, that is less than zero is skipped exactly as a course with a value
   above the range (BR-043, v98, US-057, US-058):
   - its reads fail with `GoogleReadFailureKind.ValueOutOfRange`, before its transaction, so
     nothing of it is written and its already loaded data stays as it was;
   - it gets or refreshes a skip record with reason `CourseSkipReason.ValueOutOfRange` (US-058
     FR-005, FR-006);
   - the run's log has its course id without the values (event 5135, unchanged);
   - the run goes on with the other courses; the next run reads the course again.
2. The check is made where the out-of-range check is made — in the conversion of these three
   values in `GoogleClassroomReader` — so no negative value reaches `Application` and the use
   case's skip path stays unchanged (I-6).
3. Zero is a valid value. A value that is not a number cannot arrive (Google returns JSON
   numbers); no other field changes.

## 5. Acceptance Criteria

From the Story, unchanged in meaning; the "Then" is restated precisely where the Story left a
term open.

| AC | Given / When / Then (summary) | FR |
|---|---|---|
| AC-001 | Two stored courses, each with an item and a graded submission; the list (read to its end) yields only the first. After the run the second course is stored with its state, item, submission and grade unchanged and is marked "not in Classroom" with the run's `observedAt`; its item has no mark; the first course has no mark. | FR-001 |
| AC-002 | A course marked by an earlier run, still absent. After the run it is still marked with the earlier run's instant. | FR-001.2, FR-006.5 |
| AC-003 | A marked course the list yields again with a changed name. After the run it has no mark and carries the new name. | FR-004 |
| AC-004 | Stored courses the list yields but the run does not read: an unchanged archived course, a course skipped for out-of-range values, a course in an unknown state. After the run none of them is marked. | FR-001.2, FR-003.1 |
| AC-005 | A stored unmarked course absent from the list, a marked course, and a run that fails before the list is read to its end. After the run the first has no mark and the second keeps its mark and instant. | FR-001.4 |
| AC-006 | Stored courses, one marked, a skip record, and a list read to its end with no course. After the run no course gained or lost a mark, the skip record stays, and `SyncState` holds the empty-list flag. | FR-005.2 |
| AC-007 | `SyncState` holds the empty-list flag. A run reads a non-empty list. After it the flag is cleared and absent stored courses are marked by FR-001. | FR-005.3 |
| AC-008 | No course stored; a run reads an empty list. The run completes and the flag is not set. | FR-005.3 |
| AC-009 | Three courses with, respectively, a negative assigned grade, a negative draft grade and a negative maximum points, and a fourth with ordinary values. The run completes; the fourth course is loaded; each of the three has a skip record with reason ValueOutOfRange and its already loaded data unchanged; the run's log has their course ids and no values. | FR-008 |
| AC-010 | The data of AC-001 and AC-003 in one run. The run log has one line giving 1 marked "not in Classroom", 0 marked "not visible", 1 unmarked; no log line of the run contains the name or Google id of either course, a person or a grade. | FR-007 |
| AC-011 | A saved connection with email A. Saving A again in other letter case records no change; saving B later records that save's instant as the last change. A first save of a connection records no change. | FR-002 |
| AC-012 | Two stored courses that a run listed; the technical account changes after that run; each course has an item and a graded submission; the list now yields only the first. After the run the second carries "not visible to the technical account" with the run's instant (not "not in Classroom"), its state, item, submission and grade unchanged; the first has no mark and its last sighting is this run's instant. | FR-003, FR-006.1 |
| AC-013 | The account changes; a later run lists a course; the run after that lacks it. After that run the course carries "not in Classroom". | FR-006.2 |
| AC-014 | A course marked "not visible" by an earlier run. After a run whose list still lacks it, it keeps the earlier instant; after a later run whose list yields it, it has no mark and its last sighting is that run's instant. | FR-006.4, FR-004, FR-003 |
| AC-015 | A course marked "not in Classroom"; then the account changes. After a run whose list still lacks it, it keeps "not in Classroom" with its first instant. | FR-006.5 |
| AC-016 | An unchanged archived course the list yields and the run does not read. After the run its last sighting is the run's instant. | FR-003.1 |
| AC-017 *(derived)* | A course stored with no sighting (as after the migration), a change instant recorded, and a list that lacks the course. After the run it carries "not visible to the technical account". With no change instant recorded, the same course carries "not in Classroom". | FR-006.3 |
| AC-018 *(derived)* | A course listed, then `404` during its reads. After the run it has no mark and its last sighting is the run's instant. | FR-001.5, FR-003.1 |

## 6. Validation Rules

- **VR-001 Course identity.** A stored course is "listed" when its `GoogleId` equals, ordinally,
  an id in `listed` (trimmed and cut as `Course.Import` stores it, §1.1). No case folding: Google
  ids are opaque.
- **VR-002 Email comparison** (FR-002): both emails through `WorkspaceConnection.NormalizeEmail`,
  then ordinal equality — the comparison the entity already stores by.
- **VR-003 Mark.** A course carries no mark, or exactly one mark: a kind from the closed set
  {NotInClassroom, NotVisibleToTechnicalAccount} with an instant. Never a kind without an instant
  or an instant without a kind. Story 3 will move a "not visible" course to "not in Classroom"
  dated by the Admin's confirmation (BR-049); the shape must allow that.
- **VR-004 Instants.** The mark's instant, the sighting and the empty-list flag's change come from
  the run's `observedAt`; the account-change instant from the save's `now`. All UTC from
  `TimeProvider` (PC-6); never a Google-side time.
- **VR-005 Negative value** (FR-008): a `maxPoints`, `assignedGrade` or `draftGrade` strictly
  less than zero as Google returned it. A missing value stays allowed (null), as today.
- **VR-006 Empty-list flag:** set or not set; no other states.

## 7. Security Requirements

- No endpoint, page, role, policy or permission changes.
- **Read-only mode.** Synchronization does not run in it and the connection save is already
  refused (BR-026), so this Story adds no service write to BR-026's list.
- Google access stays read-only; no new scope, no new request — every fact comes from reads the run
  already makes (SC-7, Hard Stops).
- **Personal data.** The mark, the sighting and the account-change instant are instants; the flag
  is a yes/no. None holds student data. The account-change instant says nothing about who the
  technical account is. The log lines hold counts only (SC-10).
- **Audit.** No new audit event: synchronization's own writes are not in SC-11, and the connection
  save already writes `WorkspaceConnectionSaved`. The Admin's confirmation of BR-049, which is
  audited, is Story 3.
- **Retention.** A marked course is purged by the ordinary rule (BR-072); neither the mark nor the
  sighting is used for last activity, so neither prolongs a course's life. Nothing new to purge:
  the marks and the sighting are on the course row and go with it; the flag is in `SyncState`.

## 8. Error Handling

- The end-of-list writes (marks, unmarks, sightings, the flag) are saved after the enumeration has
  finished, together with the US-058 FR-007.3 removal. A failure there is a run failure like any
  other database failure: the run stops and `SyncState` records it as today, and what is not saved
  is discarded (§1.1). No new error kind.
- A course skipped for a negative value is a normal outcome, not an exception reaching the run
  (AD-9): the existing `ValueOutOfRange` catch handles it.
- The account-change instant is written in the save's existing transaction; a failure rolls it back
  with the save.
- An absent course, an empty list and a mark are normal results; no exception is introduced for
  them (AD-9).

## 9. Non-Functional Requirements

- **Persistence.** The course mark (kind and instant), the course's last sighting, the account-change
  instant and the empty-list flag are new stored data. Their shape is decided at DB_DESIGN and ships
  with its EF Core migration (PC-2). An existing installation gets no backfilled values (FR-002.3,
  FR-003.3).
- **Idempotency (PC-10).** The same run twice over the same Google answer gives the same marks and
  flag; the sighting is the later run's instant.
- **Cost.** No additional Google call. The end-of-list step needs the stored courses' ids, marks and
  sightings — one query per run — and writes the sighting of every stored listed course in each run
  (a few hundred rows per school). DB_DESIGN may choose a set-based update.
- **Localization.** No user-visible string in this Story (NFR-073); Story 2 and 3 translate the
  marks and the flag's problem.
- **Tests.** Synthetic data only, Google ports substituted (TC-4); PostgreSQL via Testcontainers
  (TC-2). FR-002 is tested through the use case; FR-008 through the reader's conversion and through
  the run with a substituted port.

## 10. Out of Scope

- Any display: the mark in the course list, course card, journal, report and export (Story 2);
  notices, banner, «Стан даних», the Dean's hint to contact the Admin, the "data not updated since"
  header (Story 3).
- The Admin's confirmation that courses not visible to the technical account were deleted, and its
  audit event (BR-049, SC-11) — an action on «Стан даних», Story 3.
- A course that vanishes mid-run (`404`): skipped as today, marked only if a later list lacks it.
- Any threshold for a partial loss of courses (v100: none).
- Telling a deleted course from one that left the technical account's view — Google's answer cannot.
- API design: no endpoint (API_DESIGN expected NOT_APPLICABLE).

## 11. Open Decisions and Interpretations

### Open Decisions

None. The Story's own section is empty (the Owner decided the behaviour on 2026-10-26/27, v100,
v101), and `trebovaniya.md` §7 holds no open item this Story depends on (items 14, 26–29 concern
other topics). See `docs/decisions/US-059-open-decisions.md`.

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 A course with no sighting counts as seen before the change.** Story Notes propose the safer
  side: after the migration no course has a sighting, so a course lost before its first sighting,
  with an account change recorded, becomes a technical problem for the Admin ("not visible") rather
  than a teaching one sending the Dean to a teacher. With no change recorded the ordinary rule
  applies (FR-006.3, AC-017).
- **I-2 Everything happens at the end of the list.** Marks, unmarks and sightings are all written
  once the list has been read to its end, not inside each course's transaction. A course read and
  unmarked in a run that later stops keeps its mark until a run reads the whole list — the same
  rule that keeps a run that stops from marking (FR-001.4, FR-004.1).
- **I-3 What the log counts.** "Marked" means newly marked in this run; courses still absent and
  already marked are not counted again. No line when all three numbers are zero.
- **I-4 A list with only id-less entries is empty.** Such entries add nothing to `listed`; treating
  that list as non-empty would mark every stored course. Google never returns a course without an
  id, so this is a guard, not a case.
- **I-5 An empty list does not fail the run.** The run completes as today and the flag is the
  technical problem. Failing the run would stop the Meet steps that do not depend on the course
  list, and BR-048 calls it a recorded technical problem, not a failure.
- **I-6 The negative check is in the reader.** It is the same conversion that already rejects
  values above the range, so Google data is validated before it reaches business logic (Security
  Policy) and the skip path, skip record and log line are reused unchanged.
- **I-7 Unmark does not need a read.** A marked course on the list loses its mark even when the run
  skips it (unchanged archived, skipped for values, unknown state): BR-048 ties the mark to the
  list, not to a successful read. Its data may still be stale — that is the skip's own notice.

## 12. Traceability

| AC | FR | VR / NFR | Source |
|---|---|---|---|
| AC-001 | FR-001 | VR-001, VR-003, VR-004 | §4 Epic 1 v100, BR-048 |
| AC-002 | FR-001.2, FR-006.5 | VR-004 | BR-048 |
| AC-003 | FR-004 | — | BR-048, PC-10 |
| AC-004 | FR-001.2, FR-003.1 | VR-001 | BR-048, Story scope |
| AC-005 | FR-001.4 | — | BR-048 "read to its end", BR-043 |
| AC-006 | FR-005.2 | VR-006 | §4 Epic 1 "Пустой список", BR-048, US-058 FR-007.3 |
| AC-007 | FR-005.3 | VR-006 | Story scope |
| AC-008 | FR-005.3 | VR-006 | BR-048 "while courses are stored" |
| AC-009 | FR-008 | VR-005 | BR-043 v100, US-057 m-4/L-1 |
| AC-010 | FR-007 | SC-10 | Story scope, SC-10 |
| AC-011 | FR-002 | VR-002, VR-004 | §3 `WorkspaceConnection` v101, BR-049 |
| AC-012 | FR-003, FR-006.1 | VR-003, VR-004 | §4 Epic 1 "Смена технического аккаунта", BR-049 |
| AC-013 | FR-006.2 | — | BR-049 |
| AC-014 | FR-006.4, FR-004, FR-003 | VR-004 | BR-049 |
| AC-015 | FR-006.5 | — | Story scope |
| AC-016 | FR-003.1 | VR-004 | Story scope, BR-049 |
| AC-017 | FR-006.3 | — | I-1, Story Notes |
| AC-018 | FR-001.5, FR-003.1 | — | BR-048 (`404`) |
