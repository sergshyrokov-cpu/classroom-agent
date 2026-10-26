---
artifact_type: specification
story: US-058
version: 1
status: APPROVED
created_at: 2026-10-26T19:36:21Z
updated_at: 2026-10-26T19:49:19Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
  - path: trebovaniya.md
    version: 99
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
supersedes: null
---

# US-058 Specification — Synchronization marks items no longer in Classroom and records the courses it skips

Delegation: code facts (§1.1) were collected by a read-only `quick-look` lookup and then
checked by the spec writer against `RunSynchronizationUseCase.cs` lines 455–600 and 900–940.
The `trebovaniya.md` passages (§3 `CourseWork`, `SyncState`; §4 Epic 1 "Когда повторы не
помогли", "Инкрементальная синхронизация", "Задание или материал, которого нет в Classroom",
"Состояние данных"; §5 retention; v99 changelog) and BR-043, BR-045…BR-047, PC-10, PC-11,
SC-10 were read directly.

## 1. Overview

`trebovaniya.md` v99 makes two facts that synchronization meets today durable, so that the
next two v99 Stories can show them:

1. **An item Google no longer returns.** In a full read of a course, a stored `CourseWork` row
   (either resource: `courseWork` or `courseWorkMaterials`) that Google did not return is kept
   with its submissions and grades and marked "not in Classroom" with the instant the run
   noticed it (§3 `CourseWork`, §4 Epic 1, BR-045). It comes back → the mark goes.
2. **A course the run skips.** A course skipped for out-of-range values (BR-043, v98) or for a
   course state the program does not know stays recorded as part of the synchronization state —
   Google course id, the name from Google's course list, the reason and since when — until a
   run reads it successfully (§3 `SyncState`, BR-046).

Nothing is displayed by this Story. Story 2 of v99 shows the marks in the journal, report and
export; Story 3 shows the banner and the «Стан даних» page.

### 1.1 Code facts the Specification must not contradict

- `RunSynchronizationUseCase.ImportCoursesAsync` (≈455) reads the course list once per run and,
  per course, in this order: unknown state → `progress.SkippedCourses` (≈467); blank name →
  `CoursesWithBlankName` (≈475); unchanged archived course (not in the weekly full run) →
  `CoursesSkippedUnchanged` (≈486); then reads roster, items and submissions **before** any
  transaction, catching `CourseGone` (`404`) → `CoursesGone` and `ValueOutOfRange` →
  `CoursesValueOutOfRange` (≈511–527); then one transaction per course
  (`ExecuteInTransactionAsync`, ≈530) running `ImportCourseAsync`.
- `ImportCourseAsync` returns `CourseResult.AgedOut` for a course not in the database that is
  older than the retention period N, before writing anything; otherwise it upserts the course,
  roster, items (`ImportCourseWorkAsync`, ≈701, which first loads every stored item of the
  course, both resources, keyed by `(Resource, GoogleId)`), submissions, then
  `course.RecordCompleteRead()` (≈664) and saves; the transaction commits at the end of the
  unit of work.
- One instant, `observedAt`, is taken at the start of `ImportCoursesAsync` and used for every
  observation of the run.
- A material is a `CourseWork` row with `Resource == CourseWorkMaterial`.
- The retention purge (`RunRetentionPurgeUseCase`, `RetentionPurgeStore`) computes a course's
  last activity from Google-side times only (`Course.UpdateTime`, `CourseWork.CreationTime`,
  `CourseWork.UpdateTime`, `Submission.UpdateTime`) — never from row timestamps.
- Run-log events are defined in `SynchronizationBackgroundService` (5121–5135; 5129 unused).
- `Course` has no stored instant of its last successful read: `CompleteReadUpdateTime` is
  Google's `updateTime` at that read, not the program's clock (relevant to OD-001).

## 2. Business Goal

Deans take what the program shows as the truth and are not set up to check it (Owner,
2026-10-26). An item deleted in Classroom, and a course synchronization skips, are invisible
today. Storing both facts now lets Stories 2 and 3 show them, and lets the marks start
accumulating from the day this Story is deployed.

## 3. Business Flow

For each run (scheduled or by the button — the same rules, §4 Epic 1):

1. The run reads Google's course list in full, as today.
2. For each listed course, the existing order of checks applies (§1.1).
   - Skipped for an **unknown state** or for **out-of-range values** → its skip record is
     created or refreshed (FR-005, FR-006).
   - **Fully read** (its transaction commits, §1.1) → its stored items that Google did not
     return are marked, items that came back are unmarked (FR-001…FR-003), and its skip
     record, if any, is removed in the same transaction (FR-007).
   - Every other outcome (blank name, unchanged archived course, `404`, aged out) changes
     neither its marks nor — except "aged out", FR-007.2 — its skip record.
3. After the course list has been gone through to its end, skip records of courses that were
   not in this run's list are removed (FR-007.3).
4. The run writes one log line with the numbers of items marked and unmarked (FR-004).

A run that stops (any failure that ends the run, BR-043) leaves marks and skip records of every
course it did not commit exactly as they were; changes of courses already committed stay.

## 4. Functional Requirements

### FR-001 An item Google did not return in a full read is marked

1. A **full read** of a course is a course whose transaction in `ImportCourseAsync` commits in
   this run (it passed every skip check, its reads succeeded, it was not aged out). Only a full
   read changes marks (BR-045).
2. In a full read, every stored `CourseWork` row of the course — either resource — whose
   `(Resource, GoogleId)` is not among the items Google returned in this read, and which is not
   already marked, gets the mark "not in Classroom" with the instant `observedAt` of the run.
3. The marked row is otherwise untouched: its Google-sourced fields keep their stored values,
   and its `Submission` rows (states, grades) are neither changed nor deleted. Nothing is
   deleted (BR-045, PC-11 "synchronization never deletes teaching data").
4. The mark is a synchronization record, not an edit of the mirror (§3 `CourseWork`, v83, v99):
   it is stored beside the Google-sourced fields and never overwrites one.
5. The mark is written in the course's own transaction, so a course whose read or write fails
   keeps its marks exactly as before (Story scope; AD-7).

### FR-002 The mark keeps its first instant

While the item stays absent from later full reads, its mark and its instant stay unchanged.

### FR-003 An item that comes back loses the mark

In a full read, a marked item that Google returns again loses the mark (its instant is cleared)
and is updated from Google's data as every returned item is (`UpdateFrom`, PC-10).

### FR-004 One run-log line counts marks

1. A run in which at least one item was newly marked or unmarked writes **one** log line with
   two numbers: items newly marked (FR-001) and items unmarked (FR-003) in the courses this run
   committed. An item already marked and still absent is not counted (I-4).
2. The line is written whether the run completed or stopped, counting only committed courses.
3. The line carries no item title, item or course Google id, person, email or grade (SC-10).
4. It is a new run-log event next to 5121–5135; its id and name are chosen at IMPLEMENTATION
   in the existing sequence.

### FR-005 A skipped course is recorded

1. A course the run skips for one of exactly two reasons gets a skip record:
   - **ValueOutOfRange** — the reads raised `GoogleReadFailedException` with
     `GoogleReadFailureKind.ValueOutOfRange` (BR-043, v98);
   - **UnknownCourseState** — the listed state is not one the program knows (`TryParseState`
     returns false).
2. The record holds: the course's Google id; its name as given by Google's course list in this
   run (VR-002); the reason; and the instant the current streak of skips began (`observedAt` of
   the run that first skipped it).
3. The record is keyed by the Google course id, so it exists for a course never loaded as well
   as for a stored one, and there is at most one record per course.
4. Writing the record does not write anything else of the course: a stored course keeps its
   data exactly as it was, as today (BR-043).
5. No other skip creates a record: `404` mid-run (I-1), blank name (I-2), unchanged archived
   course, and a new course older than N (§5 policy, Story out of scope).

### FR-006 A repeated skip keeps the first instant

A course already recorded and skipped again for either reason keeps its record's instant; its
name is replaced by the name from this run's course list, and its reason by this run's reason
(I-3).

### FR-007 The record goes when the problem is gone

1. **Read successfully.** When a full read of the course commits (FR-001.1), its skip record is
   removed in the same transaction.
2. **Aged out.** When the run handles a recorded course as a new course older than the
   retention period N (`CourseResult.AgedOut`), its record is removed: the course is not loaded
   by policy, not because of a problem (I-5).
3. **Left Google's list.** When the run has gone through the whole course list (the enumeration
   finished without the run stopping), every record whose Google id was not in that list is
   removed. A run that stops before the end of the list removes none this way.
4. Every other outcome leaves the record as it was: blank name, unchanged archived course,
   `404`, and a run that stops before reaching the course.

### FR-008 The instant of a course's last successful read is stored *(OD-001 a)*

The Owner chose option (a) of OD-001: this Story stores the date.

1. When a full read of a course commits (FR-001.1), the course stores the run's `observedAt` as
   the instant of its last successful read, in the same transaction.
2. A course stored before this Story's migration has no such instant until its next full read
   (no source exists to backfill it).
3. Nothing in this Story reads the value; Story 2 shows it as "данные не обновлялись с <даты>"
   (§4 Epic 1 "пропущенный курс", BR-046).

## 5. Acceptance Criteria

From the Story, unchanged in meaning; the "Then" is restated precisely where the Story left a
term open.

| AC | Given / When / Then (summary) | FR |
|---|---|---|
| AC-001 | Stored course, two graded items each with a graded submission; Google returns only the first. After the run the second item is stored, marked with the run's instant (`observedAt`), its submission and grade unchanged; the first item has no mark. | FR-001 |
| AC-002 | Stored course with a material Google no longer returns. After the run the material is stored and marked with the run's instant. | FR-001 |
| AC-003 | Item marked by an earlier run, still absent. After the run it is still marked with the earlier run's instant. | FR-002 |
| AC-004 | Marked item returned again with a changed title. After the run it has no mark and carries the new title. | FR-003 |
| AC-005 | An unchanged archived course whose stored item Google would no longer return, and a course skipped for an out-of-range value whose stored item Google no longer returns, each also holding an item already marked. After the run neither absent item gains a mark and the already-marked items keep their marks and instants. | FR-001.1 |
| AC-006 | Stored course whose read fails after its items were read, so the run stops (a non-retriable failure of the submissions read). After the run no item of that course gains or loses a mark. | FR-001.5 |
| AC-007 | A course never loaded that the run skips for an out-of-range value, and a course whose state the program does not know. After the run each has a skip record with its Google id, its name from the course list, its reason (ValueOutOfRange / UnknownCourseState) and the run's instant. | FR-005 |
| AC-008 | A course recorded by an earlier run, skipped again, with a changed name in the course list. After the run the record keeps the earlier instant and carries the new name. | FR-006 |
| AC-009 | A recorded course whose value the teacher corrected. After the run, which reads it in full, the record is gone. | FR-007.1 |
| AC-010 | A recorded course no longer in Google's course list. After the run, which goes through the whole list, the record is gone. | FR-007.3 |
| AC-011 | A course that vanishes mid-run (`404`) and a new course older than N. After the run neither has a skip record. | FR-005.5 |
| AC-012 | The data of AC-001 and AC-004 in one run. The run log has one line giving 1 marked and 1 unmarked, and no log line of the run contains an item's title or Google id, a person or a grade. | FR-004 |
| AC-013 *(derived, OD-001 a)* | A stored course read in full. After the run it carries the run's instant as its last successful read; a course skipped for an out-of-range value in the same run keeps its previous value. | FR-008 |
| AC-014 *(derived)* | A recorded never-loaded course older than N whose value was corrected. After the run it is not loaded and its record is gone. | FR-007.2 |
| AC-015 *(derived)* | A run that stops (permission failure) after committing one course with an absent item, while a recorded course not in the list exists. The committed course's item stays marked; the record of the unlisted course stays. | FR-001.5, FR-007.3 |

## 6. Validation Rules

- **VR-001 Google course id** of a skip record: as returned in the course list, required,
  length ≤ `Course.MaxGoogleIdLength`; the course list already yields it validated for the
  course itself — the same rule applies to the record.
- **VR-002 Course name** of a skip record: Google's list name trimmed and cut to
  `Course.MaxNameLength`, as a course's own name is cut, never refused. A missing or blank name
  is stored as no name (null). (An unknown-state course is checked before the blank-name check,
  so it can arrive without a name, §1.1.)
- **VR-003 Reason**: one of the closed set {ValueOutOfRange, UnknownCourseState}; no free text,
  no raw Google value, no Google error text.
- **VR-004 Instants**: the run's `observedAt` (UTC, from `TimeProvider`), for the mark, the
  record and FR-008. Never a Google-side time.
- **VR-005** The mark is either absent or an instant; no other states.

## 7. Security Requirements

- No endpoint, page, role, policy or permission changes. Read-only mode is unaffected:
  synchronization does not run in it (BR-026), so this Story adds no service write to BR-026's
  list.
- Google access stays read-only; no new scope, no new request — every fact comes from reads the
  run already makes (SC-7, Hard Stops).
- **Personal data.** The mark is an instant on an item row. A skip record holds a course id, a
  course name, a reason and an instant — no student data. The course name is Google's own, as
  for a stored course, and may contain a teacher's name (schools name courses freely, §3
  `Course`); the record holds nothing else about a person (SPEC_REVIEW m-2). The run-log line holds two
  numbers (SC-10). Existing log lines are unchanged.
- No audit event: synchronization's own writes are not in the SC-11 list.
- Retention: a marked item is an ordinary `CourseWork` row and is purged with its course (PC-11).
  The mark does not touch any field the purge uses for last activity (§1.1), so marking never
  prolongs a course's life. The purge does not touch skip records (I-6).

## 8. Error Handling

- Marks are written inside the course's transaction; a failure there rolls them back with the
  rest of the course, and the run stops as today (BR-043).
- A skip record is written after the skip decision, outside any course transaction. A failure
  writing it is a run failure like any other database failure: the run stops and `SyncState`
  records the failure as today (no new error kind).
- Removal of records for courses not in the list (FR-007.3) runs only after the list has been
  enumerated to its end; if it fails, the run fails as above.
- No exception is introduced for an expected outcome (AD-9): an absent item, a skip, a removal
  are normal results.

## 9. Non-Functional Requirements

- **Persistence.** The mark and the skip records are new stored data; the shape (columns, table,
  keys, indexes) is decided at DB_DESIGN and ships with its EF Core migration (PC-2). §3 places
  the skip records in the synchronization state (`SyncState`); DB_DESIGN decides whether that is
  a child table or another shape.
- **Idempotency (PC-10).** Running the same run twice over the same Google answer gives the same
  marks, instants and records.
- **Cost.** No additional Google call; finding absent items reuses the stored items
  `ImportCourseWorkAsync` already loads.
- **Localization.** No user-visible string in this Story (NFR-073); the reason is a code, Story 3
  translates it.
- **Tests.** Synthetic data only, Google ports substituted (TC-4); PostgreSQL via Testcontainers
  (TC-2).

## 10. Out of Scope

- Any display: journal, report, export (Story 2); banner, «Стан даних», who sees what (Story 3).
- A course that vanishes mid-run (`404`) — not recorded (I-1).
- A course with a blank name — not recorded (I-2).
- New courses older than the retention period N — policy, not a problem.
- A course deleted in Google as a whole — v99 says nothing about it.
- Telling a deleted item from one returned to draft — Google's answer cannot (v99).
- Points above an item's maximum (BR-067) — read from stored grades by Story 2/3, nothing stored
  here.
- API design: no endpoint (API_DESIGN expected NOT_APPLICABLE).

## 11. Open Decisions and Interpretations

### Open Decisions

- **OD-001 (RESOLVED (a) by the Owner, 2026-10-26)** — where the date "данные не обновлялись с
  <даты последнего успешного чтения курса>" (§4 Epic 1, BR-046) comes from. No stored value
  holds it today (§1.1). Options: (a) this data Story stores the instant of each course's last
  full read (FR-008, AC-013) — *recommended*: it is data, this Story is the data Story, and Story
  2 then only displays; (b) Story 2 adds it. Impact: (a) keeps FR-008/AC-013 and adds a column to
  DB_DESIGN; (b) deletes them from this Specification before DB_DESIGN. **Resolution: (a)** — FR-008
  and AC-013 stay; DB_DESIGN adds the column.

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 `404` is not recorded.** BR-046 cites BR-043, which also covers a course vanishing
  mid-run, but BR-047 gives that case no notice kind and the course leaves the list by the next
  run (Story Notes).
- **I-2 Blank-name skip is not recorded.** BR-046 names only BR-043 skips and an unknown state;
  the blank-name skip (US-017) is neither. Its record would also have no name to show.
- **I-3 Reason refreshed, instant kept.** If a recorded course is skipped again for the other
  reason, the record keeps its instant (the course has been unreadable since then) and takes the
  new reason.
- **I-4 What the log counts.** "Marked" means newly marked in this run; items still absent and
  already marked are not counted again. No line when both numbers are zero.
- **I-5 Aged out removes the record.** A recorded never-loaded course can be skipped for
  out-of-range values before its age is known (the age needs its items). Once the values are
  fixed and the run finds it older than N, it is not loaded by policy; its record is removed so
  no notice stays for it. Known limitation: until then an old course with a bad value is recorded.
- **I-6 The purge leaves skip records alone.** A record holds no personal data beyond the
  course name (§7) and no link to a stored row, and it lives only while the problem lasts; a purged course still in Google's list is either skipped again (the record stays
  true) or aged out (I-5), and one not in the list is removed by FR-007.3.
- **I-7 Unchanged archived course keeps its record.** It is not read, so not "read
  successfully"; such a course can hardly hold a record (archiving changes `updateTime`, forcing a
  read), but the rule is stated.

## 12. Traceability

| AC | FR | VR / NFR | Source |
|---|---|---|---|
| AC-001 | FR-001 | VR-004, VR-005 | §4 Epic 1 v99, BR-045 |
| AC-002 | FR-001 | VR-004 | §3 `CourseWork`, BR-045 |
| AC-003 | FR-002 | VR-004 | BR-045 |
| AC-004 | FR-003 | — | BR-045, PC-10 |
| AC-005 | FR-001.1 | — | BR-045, BR-042, BR-043 |
| AC-006 | FR-001.5 | — | Story scope, AD-7, BR-043 |
| AC-007 | FR-005 | VR-001…VR-004 | §3 `SyncState` v99, BR-046 |
| AC-008 | FR-006 | VR-002 | BR-046, Story scope |
| AC-009 | FR-007.1 | — | BR-046 |
| AC-010 | FR-007.3 | — | Story scope |
| AC-011 | FR-005.5 | — | I-1, §5, Story Notes |
| AC-012 | FR-004 | SC-10 | Story scope, SC-10 |
| AC-013 | FR-008 | VR-004 | §4 Epic 1 "пропущенный курс", BR-046, OD-001 |
| AC-014 | FR-007.2 | — | I-5, §5 |
| AC-015 | FR-001.5, FR-007.3 | — | Story Notes, BR-043 |
