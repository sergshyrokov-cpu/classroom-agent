---
artifact_type: specification
story: US-025
version: 1
status: APPROVED
created_at: 2026-10-04T09:15:50Z
updated_at: 2026-10-04T09:39:58Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: trebovaniya.md
    version: 82
  - path: docs/decisions/US-025-open-decisions.md
    version: 1
supersedes: null
---

# US-025 Specification — Journal view for a period

## 1. Overview

This Story gives Admin and Dean the first screen that shows teaching data: the
journal of one course for a period (`trebovaniya.md` §4 Epic 3 "Формирование
журнала: студент × (задание/материал с датой) × клетка, за выбранный период";
§2 "Просмотр и экспорт журнала успеваемости" ✔ ✔). Everything it shows is
already stored by US-014 (courses, participants, memberships with first/last
seen dates) and US-015 (coursework, materials, submissions, grades).

Five properties shape everything below.

- **A pure read.** Building the journal writes nothing, calls no Google API and
  reads no secret. It therefore works unchanged in read-only mode (§2, BR-026)
  and needs no read-only guard; the test proves that nothing is written.
- **One computation for every cell.** Graded work, ungraded work and materials
  are all handled here (OD-002 a); the cell is a pure function of the stored
  submission, the item and one instant — "the moment the journal is built"
  (BR-056, v57).
- **Raw points only.** Grades are shown as stored — points out of the item's
  maximum. Conversion to the school's scale is the template's job (US-027, §4
  Epic 3).
- **The school's time zone decides dates.** Period day boundaries and every
  date shown are in the school's time zone (BR-053, NFR-074). That setting does
  not exist in the code yet (`InstallationSettingsReader` defers it "to the
  Stories that use them"); this Story is the first user and introduces it as
  DC-3 defines it (FR-010).
- **Viewing is not audited.** SC-11 audits exporting a journal, not viewing it;
  the export row belongs to US-028 / US-029.

## 2. Business Goal

A Dean today can see nothing of what was synchronized; to check how a course
went they open Classroom course by course. With the journal they choose a course
and a period and see, in one table, who was in the course, what was set, and the
state of every piece of work — including what Google could not tell the program
(an unrecognised state), which is shown rather than hidden (US-015 OD-005).

The journal is also the root of Epic 3: export (US-028, US-029), templates
(US-027) and the printed journal (US-030) all build on the rows, columns and
cells defined here.

## 3. Business Flow

### 3.1 Opening the journal

From the home page the Dean (or Admin) follows "Journal". The page shows a
form: a drop-down of all synchronized courses by name (OD-001 a), a from-date and
a to-date pre-filled with the first and last day of the current calendar month
in the school's time zone (OD-006 a), and the "full / short" switch (OD-003 a).
No journal is shown until a course is chosen.

### 3.2 Viewing

The user chooses a course, adjusts the dates if needed and submits. The form is
a `GET`: course id, dates and view travel in the query string, which carries no
personal data (Story Notes; US-039 INFO-1). The page shows the same form with the
chosen values and, below it, the journal: one row per student of the period, one
column per item dated in the period, one cell per student and item.

### 3.3 Switching full / short

The switch re-requests the page with the other view. The short view shows only
assigned grades and states; the full view also shows draft grades and the last
turn-in date (BR-057, BR-058).

### 3.4 Invalid input

An unknown course, a malformed date or a from-date after the to-date: the page
shows the form with the values as far as they can be redisplayed and a message
naming the problem. No journal is computed (AC-011).

### 3.5 Read-only mode

The installation is suspended, past its grace period or never legitimated. The
journal opens and behaves exactly as above; the home page already states the
read-only reason (US-008 FR-019).

## 4. Functional Requirements

### FR-001 The journal page and its query

One server-rendered page (`GET`), reachable by Admin and Dean, with these query
parameters (names indicative; API_DESIGN fixes them):

| Parameter | Meaning | Absent |
|---|---|---|
| `courseId` | the internal id of a `Course` | the page shows the form only, no journal |
| `from` | first day of the period, school time zone | FR-007 default |
| `to` | last day of the period, inclusive, school time zone | FR-007 default |
| `view` | `full` or `short` | `full` (I-6) |

The page always shows the form of §3.1 with the effective values. The query is
answered by a use case (query) in `Application/UseCases` — `GetJournalQuery`,
name indicative — returning a DTO (AD-8); the host only binds, validates shape
and renders.

No JSON endpoint is added (I-9). The page is a `GET` and changes nothing
(API-4).

### FR-002 The course drop-down

The drop-down lists **every** stored `Course`, whatever its Classroom state
(an archived course is shown like any other — Story Notes), ordered by name in
the collation of the user's UI language, ties broken by internal id. The label
is the course name as Google gave it, followed by its section when the course has
one (I-7). Course names are never translated (NFR-073).

When no course is stored yet, the drop-down is replaced by a translated message
saying no course has been synchronized yet.

### FR-003 The period

The period `[from, to]` is a pair of calendar dates in the school's time zone,
both inclusive. It is turned into one UTC interval:

- **start** = `from` at 00:00 in the school's time zone, converted to UTC;
- **end (exclusive)** = the day after `to` at 00:00 in the school's time zone,
  converted to UTC.

If local midnight does not exist on a date (a daylight-saving jump at
midnight), the first valid local instant of that day is used. Every comparison
below is between UTC instants (NFR-074, PC-6).

### FR-004 Columns

The columns are exactly the `CourseWork` rows of the chosen course — both
resources, `CourseWork` and `CourseWorkMaterial` — whose `ItemDate` lies in
`[start, end)`. `ItemDate` is already the BR-052 cascade `scheduledTime` →
`dueDate` → `updateTime` → `creationTime`, resolved at synchronization (US-015);
the journal does not recompute it.

- **Order** (OD-007 a): by `ItemDate` ascending, then by title in the collation
  of the UI language, then by internal id.
- **Header**: the title as Google gave it (never translated); the item's date
  (`ItemDate` in the school's time zone, formatted for the UI language); and by
  kind (BR-052, `CourseWork.Kind`):
  - graded work — its maximum points;
  - ungraded work — nothing more;
  - material — a translated "material" marker.
- Coursework deleted in Google after it was imported keeps its row (US-015
  OD-004) and is a column like any other (Story Notes).

### FR-005 Rows

The rows are the **students of the period** (BR-051): every `CourseMembership`
of the chosen course with role `student` for which at least one holds:

1. **on the roster during part of the period** — `FirstSeenAt < end` and
   (`OnRoster` is true **or** `LastSeenAt ≥ start`) (I-1);
2. **a submission in the period** — the participant has a `Submission` to one of
   the columns of FR-004.

A membership with role `teacher` is never a row, even if a submission exists for
that person. BR-051 gives every submitter a student membership (off the roster
when never seen on it, US-015 OD-006), so condition 2 always finds its
membership.

- **Display**: the participant's full name as Google gave it (never
  translated). A participant with no full name but an email is shown by the
  email; one with neither is shown with the translated label "student without a
  name" (OD-005 a, I-5).
- **Order** (OD-007 a, OD-005 a): rows with a displayed name or email by that
  value in the collation of the UI language, ties broken by internal participant
  id; then the unnamed rows, by internal participant id.

### FR-006 Cells

Each cell is computed by one pure function of: the item, the student's
submission for it (or none), the view, and the instant **B** — the moment the
journal is built, read once per request from the clock and used for every cell
(BR-056 v57: "due" is judged against B, not against the end of the period).

"Due has passed" means the item has a `DueAt` and `DueAt < B`. Due dates are
compared as exact UTC instants (NFR-074); the program never recomputes lateness
— extensions and individual due dates are Google's (§4 Epic 3).

**Material** — the cell is empty, for every row.

**Coursework with no submission for the student** — "not assigned" (assigned
to some students only), never "not turned in" (BR-056, AC-004). No other mark.

**A submission whose state is `Unrecognised`** — a distinct "unrecognised
state" carrying the raw value Google sent (`Submission.RawState`), shown as
data and never translated. It never shows a grade — assigned or draft — and is
never "not turned in" (US-015 OD-005, VR-004; AC-006). The late mark applies
(I-3).

**Graded work** (`MaxPoints` set), recognised state, in this order:

| Condition | Cell |
|---|---|
| `AssignedGrade` set | the grade: `AssignedGrade` out of `MaxPoints` — whatever the state (an assigned grade wins, v57) |
| state `TURNED_IN` or `STUDENT_EDITED_AFTER_TURN_IN` | "turned in, not graded" |
| state `RETURNED` | "returned without a grade" |
| state `NEW`, `CREATED` or `RECLAIMED_BY_STUDENT`, no `DueAt` | "not turned in, no due date" |
| same states, due has passed | "not turned in" |
| same states, due has not passed | "not due yet" |

**Ungraded work** (`MaxPoints` absent), recognised state — never a grade and
never "not graded", even if Google sent a grade (US-015 stores it, I-8 there):

| Condition | Cell |
|---|---|
| state `TURNED_IN` or `STUDENT_EDITED_AFTER_TURN_IN` | "turned in" |
| state `RETURNED` | "returned" |
| state `NEW`, `CREATED` or `RECLAIMED_BY_STUDENT`, no `DueAt` | "not turned in, no due date" |
| same states, due has passed | "not turned in" |
| same states, due has not passed | "not due yet" |

**Marks added to a cell that has a submission:**

- **late** — when `Submission.Late` is true, added to any of the states above,
  including a grade and the unrecognised state; taken from Google as is
  (BR-056, I-9 of US-015);
- **draft grade** — **full view only**, graded work only, when `DraftGrade` is
  set and the cell shows a state rather than an assigned grade: the draft points
  next to the state, marked "draft" (BR-057, AC-005; I-2);
- **last turn-in date** — **full view only**, when `TurnedInAt` is set: the date
  in the school's time zone (BR-058).

The short view shows the state, the assigned grade and the late mark, and
nothing else (BR-057, OD-003 a).

Points are shown as stored, with insignificant trailing zeros dropped, in the
number format of the UI language (NFR-073) — `8.5 / 10` in English, `8,5 / 10`
in Ukrainian; a grade of `0` is shown as `0 / 10`, never as an empty cell.

### FR-007 Defaults

- **Period** (OD-006 a): when `from` is absent it is the first day of the
  current calendar month; when `to` is absent it is the last day of the current
  calendar month — the month that contains B in the school's time zone. Each
  default applies on its own, so one absent date is filled independently of the
  other (and FR-008 then checks the pair).
- **View**: `full` (I-6).

### FR-008 Validation and its answer

The query is validated before anything is read for the journal (§8, SC-10);
the rules are §6. On a failure:

- the page is re-rendered — **not** the host's error page — with the form and a
  translated message naming the problem (AC-011);
- no journal is computed and no column, row or cell data is read;
- the status code is chosen by API_DESIGN within API-5 (`400` for a malformed
  or inverted input, `404` for a well-formed id of no stored course is the
  expected choice);
- the rejected value is never written to a log (SC-10); the log line names the
  parameter and the rule, never the value.

### FR-009 Empty journals

- The period has no column — a translated message "nothing is dated in this
  period" replaces the table.
- The period has columns but no row — the table is not drawn; a translated
  message "no students in this period" is shown.

Neither is an error.

### FR-010 The school's time zone setting

`InstallationSettingsReader` gains the required setting DC-3 lists — the
school's time zone as an **IANA id** (for example `Europe/Kyiv`; key name
indicative, `Installation:TimeZone`):

- absent, blank, or not resolvable by the runtime as an IANA time zone — the
  installation **does not start**, through the existing
  `InstallationSettingException` path naming the key, as for the retention
  period (NFR-074, DC-3; VR-006);
- the resolved zone is available to `Application` through a port or a settings
  value — never by reading configuration in `Application` — so the journal query
  and its tests receive it by constructor injection (AD-4);
- the setting is not a secret and may be logged at start-up.

Existing development and test configuration gains the key. No other screen is
changed by this Story to use the setting (I-10).

### FR-011 Navigation

The home page gains a "Journal" entry for both Admin and Dean, visible in
read-only mode as well (viewing is allowed, §2). Hiding it is never the
enforcement — FR-012 is.

### FR-012 Authorization

`InstallationPolicies` gains a policy for the matrix row "Просмотр и экспорт
журнала успеваемости" — ✔ Admin, ✔ Dean (§2) — `ViewJournal` (name indicative),
declared by the journal page. It is its own policy (one policy per matrix row,
US-019 I-5); the later export Stories reuse it.

- An anonymous request is sent to sign in (SC-4); the SC-4 anonymous closed list
  gains nothing; the endpoint enumeration test classifies the page as
  protected.
- A Dean held on the forced password change (US-012) is refused as on every other
  page.
- There is no per-course restriction for a Dean in v1 (§2; Story Notes). Teacher
  and Student have no account (Hard Stop) and the journal never scopes by roster.

### FR-013 Read-only mode

Viewing is permitted in read-only mode (§2, BR-026). The journal query does not
call `IReadOnlyModeGuard`, writes nothing — not even a permitted service write —
and opens no Google port. Proven in `Application` for all three BR-025 causes:
the journal is returned, no entity is added or modified, and no Google port is
invoked (TC-5, AC-009).

### FR-014 Persistence and performance

- **No new table, no new column, no new audit action.**
- The data is read through a read port in `Application/Ports` (name
  indicative, `IJournalSource`) implemented in `Infrastructure` with EF Core,
  without change tracking. `DbContext` appears in neither `Application` nor
  `Web` (AD-3). No entity crosses into a view model (AD-8).
- **A fixed number of queries per journal**, independent of the number of rows
  and columns — no per-row or per-cell query (Story Notes): the items of the
  period, the memberships and participants of the course, and the submissions
  to those items. The cell computation runs in memory.
- The queries are supported by explicit indexes (NFR-003, PC-7) on at least: the
  items of a course by date, the memberships of a course, and the submissions of
  an item. DB_DESIGN confirms which exist already (US-014, US-015) and adds any
  that are missing by an EF Core migration in this Story (PC-2).
- The journal of one course for a period is rendered whole, not paginated (I-9).

### FR-015 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): page title, navigation entry, form labels,
the full / short switch, the material marker, every cell state of FR-006 (graded
and ungraded wording separately), "not assigned", "unrecognised state", the late
and draft marks, the last turn-in label, "student without a name", the empty
states of FR-002 and FR-009, and every validation message of §6. Dates and
numbers follow the UI language. Google data — course names, sections, item
titles, student names and emails, the raw unrecognised state — is never
translated. The existing test that fails on a key missing from one file covers
the new keys.

### FR-016 Logging

- A built journal logs one line at `Information`: the actor's account id, the
  internal course id, the period dates, the view, and the row and column counts.
- A validation failure logs one line at `Warning` naming the parameter and the
  rule, never the value.
- No log line carries a name, an email, a grade, a title or anything else from
  Google (SC-10, NFR-023, DC-10).

## 5. Acceptance Criteria

The Story's thirteen criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | The journal of a course for a period — columns, rows, one state per cell | FR-001, FR-003, FR-004, FR-005, FR-006 |
| AC-002 | Who is in the journal — whole, part, left before, came after, off-roster submitter; no teachers | FR-005 |
| AC-003 | Cell states of graded work, including the late mark | FR-006 |
| AC-004 | Work not assigned to a student shows "not assigned" | FR-006 |
| AC-005 | Draft grades — full view shows them marked "draft", short view does not | FR-006, FR-001 (`view`) |
| AC-006 | An unrecognised state is never hidden | FR-006 |
| AC-007 | Ungraded work and materials | FR-004, FR-006 |
| AC-008 | A participant without a name | FR-005 |
| AC-009 | Read-only mode — opens, writes nothing, calls no Google API | FR-013 |
| AC-010 | Only Admin and Dean | FR-012 |
| AC-011 | Invalid input — message, nothing computed, value not logged | FR-008, §6 |
| AC-012 | The page is bilingual; Google data untranslated | FR-015 |
| AC-013 | Tests never reach Google; data built synthetically in PostgreSQL | FR-013, FR-014, TC-2, TC-4 |

Two criteria are derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-014 | Period boundaries in the school's time zone: with a non-UTC zone (`Europe/Kyiv`), an item dated 00:30 local time on the day after the period's last day — still the last day in UTC — is **not** a column, and one dated 00:30 local time on the period's first day — still the previous day in UTC — **is** (FR-003, TC-8) | FR-003, FR-004 |
| AC-015 | Without a valid school time zone the installation does not start, naming the setting (FR-010) | FR-010, VR-006 |

## 6. Validation Rules

### VR-001 `courseId`

- Absent or empty — not an error: the form only (FR-001).
- Otherwise exactly one value, decimal digits only, parsing to a positive
  64-bit integer. Anything else — sign, spaces, letters, overflow, zero, a
  repeated parameter — is **malformed**: message "choose a course from the list".
- A well-formed id of no stored `Course` is **unknown**: message "this course is
  not in the program — choose one from the list". Nothing else is read.

### VR-002 `from` and `to`

- Absent or empty — the FR-007 default.
- Otherwise exactly one value each, in the form `yyyy-MM-dd` (ISO 8601 calendar
  date, the form an HTML date input sends), a real calendar date, year 2000 to
  2100 inclusive (I-4). Anything else is **malformed**: message naming which
  date is not a valid date.

### VR-003 The pair

`from` ≤ `to` after defaults are applied. Otherwise: message "the start of the
period is after its end". A one-day period (`from` = `to`) is valid.

### VR-004 `view`

Absent or empty — `full`. Otherwise exactly `full` or `short`, case-insensitive;
anything else is malformed: message "unknown journal view".

### VR-005 Unknown parameters and data from the database

- Any other query parameter is ignored; it is never echoed into the page.
- Values read from the database were validated on import (US-014, US-015) and
  are rendered with HTML encoding; a title, name or raw state is never
  interpreted as markup (SC-10).

### VR-006 The time zone setting

Required; trimmed; must resolve to an IANA time zone known to the runtime.
Absent, blank or unresolvable stops the start with `InstallationSettingException`
naming the key (FR-010).

### VR-007 The actor

The actor of the log line is the signed-in account from the session, never a
request value (VR-003 of US-019).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page declares its own policy granted to Admin and Dean; deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for the endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access, no roster-based scoping, no per-course restriction for a Dean. | Hard Stop, §2 |
| S-04 | The journal writes nothing, calls no Google API and reads no secret, in any mode; proven in `Application` for all three BR-025 causes. | SC-5, SC-8, BR-026, AD-6 |
| S-05 | The query string carries the internal course id, dates and view only — no name, email or other personal data. | Story Notes, US-039 INFO-1 |
| S-06 | Invalid input is rejected before any journal data is read; the rejected value never reaches a log or an error page detail. | §8, SC-10 |
| S-07 | No log line carries a student name, email, grade, title or other Google data; internal ids only. | SC-10, NFR-023 |
| S-08 | Google data is HTML-encoded on output; nothing from the database is rendered as markup. | SC-10, VR-005 |
| S-09 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-10 | No new outbound destination. | SC-13 (Hard Stop) |
| S-11 | Viewing writes no audit row; the export audit row belongs to the export Stories. | SC-11 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Signed-in user without the policy (a Dean on the forced password change) | Refused as on every other page (US-012). |
| Malformed `courseId`, date or view; `from` after `to` | The journal page with the form and the VR message; no journal; status per API_DESIGN within API-5 (expected `400`). |
| Unknown course | The journal page with the form and the VR-001 message; status per API_DESIGN (expected `404`). |
| No course stored yet | Not an error: FR-002 message. |
| Period without columns or rows | Not an error: FR-009 message. |
| Read-only mode | Not an error: the journal is shown (FR-013). |
| A submission with an unrecognised state | Not an error: the FR-006 unrecognised cell. |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

## 9. Non-Functional Requirements

- **NFR-003 / PC-7** — explicit indexes back the journal queries (FR-014).
- **Bounded queries** — the number of database round trips does not grow with
  rows or columns (FR-014). A course of several hundred students over a school
  year is the sizing case the DB design reasons about.
- **NFR-070** — usable on a phone: the table scrolls horizontally inside the page
  rather than widening it; the form is usable at phone width.
- **NFR-073 / NFR-074** — bilingual; dates in the school's time zone; tests of
  period boundaries and displayed dates use `Europe/Kyiv` (TC-8).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- Any export — US-028 (Excel), US-029 (Word) — and its audit row.
- Report templates, conversion of points to the school's scale, the paper
  layout — US-027, US-030.
- A course list with filters or search — US-020; a course card — US-021.
- A journal of several courses at once.
- Grade history, rubric grades, submission content (BR-059, Epic 12).
- Any change to synchronization or to what is stored (US-014, US-015).
- Showing the time of the last synchronization to the Dean — US-024.
- Converting other screens' times to the school's time zone (I-10).
- A JSON API for the journal (I-9).

## 11. Open Decisions

Full text, options and resolutions in `docs/decisions/US-025-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 How the course is chosen | **RESOLVED** 2026-10-04 (a) | none — FR-002 |
| OD-002 The US-025 / US-026 boundary | **RESOLVED** 2026-10-04 (a) | none — FR-004, FR-006 |
| OD-003 Full and short view on screen | **RESOLVED** 2026-10-04 (a) | none — FR-001, FR-006 |
| OD-004 What `NEW` means in a cell | **RESOLVED** 2026-10-04 (a), `trebovaniya.md` v82 | none — FR-006 |
| OD-005 A student without a name | **RESOLVED** 2026-10-04 (a) | none — FR-005 |
| OD-006 The default period | **RESOLVED** 2026-10-04 (a) | none — FR-007 |
| OD-007 Row and column order | **RESOLVED** 2026-10-04 (a) | none — FR-004, FR-005 |

No new Open Decision was raised. No item of `trebovaniya.md` §7 blocks the
Story (see the Open Decisions artifact).

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 A student still on the roster counts until now.** BR-051 says "on the
  roster for at least part of the period by first-seen / last-seen dates".
  `LastSeenAt` is the last run that saw the person, so a period that begins
  after the last run (synchronization paused, read-only mode) would otherwise
  drop every current student. A membership with `OnRoster = true` is therefore
  treated as continuing to the present; one that left the roster ends at
  `LastSeenAt`. In normal operation, with runs every hour, the two readings
  differ only for the last hour.
- **I-2 A draft grade is shown next to a state, not next to an assigned grade.**
  BR-056 (v57): "a draft grade never replaces the state: the full journal shows
  it next to the state". Once an assigned grade exists the cell shows the grade,
  not a state, and Classroom typically keeps the draft equal to it; showing both
  would be noise. Draft grades of ungraded work are never shown (no grades,
  BR-052).
- **I-3 The unrecognised state carries the late mark but no grade.** BR-056 adds
  "late" to any state; AC-006 forbids any grade. The raw value is shown as data.
- **I-4 Years 2000–2100.** A bound on the date input so the period always
  converts to a representable UTC interval; it is not a limit on the period's
  length, and no business rule limits that length.
- **I-5 Email when the name is absent.** OD-005 (a) covers a participant with
  neither name nor email. A participant with an email but no name is shown by the
  email and sorted with the named rows — the email is already visible to Admin
  and Dean as roster data.
- **I-6 The default view is full.** OD-003 (a) adds the switch but does not say
  which side it starts on. Full shows everything stored (§4 Epic 3 "в полном
  объёме"); short is the paper-like reduction.
- **I-7 The section in the drop-down label.** OD-001 (a) says "by name". Courses
  repeat names across years and groups; the section, when present, tells them
  apart. It is Google data, not translated.
- **I-8 The order is by instant, then title.** OD-007 (a) "by date, then by
  title": `ItemDate` is an instant, so two items on the same day are in
  chronological order; the title decides only for equal instants.
- **I-9 No pagination and no JSON endpoint.** API-8 paginates REST collections
  that grow without bound and exempts exports, which "render the full selected
  period by design". The journal view is the same document as the export, bounded
  by one course and one period; splitting it into pages would break its rows.
  This Story adds no `/api/v1` endpoint, so API-8's body shape does not apply.
  The course drop-down is likewise a page element, not an API collection. This
  concerns the on-screen journal only: splitting a journal into printed pages
  is defined by the report templates (US-027, US-030). Whether
  API-8 should name the journal view explicitly is a non-blocking finding.
- **I-10 Only the journal uses the time zone now.** Existing screens show
  instants (the last successful legitimacy check, the last synchronization) as
  they do today. Converting them to the school's time zone is a separate change
  with its own trace; recorded as a non-blocking finding.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-003, FR-004, FR-005, FR-006, FR-007 | VR-001, VR-002, VR-003 | S-01, S-05, S-09 |
| AC-002 | FR-005 | — | S-03 |
| AC-003 | FR-006 | — | — |
| AC-004 | FR-006 | — | — |
| AC-005 | FR-001, FR-006 | VR-004 | — |
| AC-006 | FR-006 | VR-005 | S-08 |
| AC-007 | FR-004, FR-006 | — | — |
| AC-008 | FR-005, FR-015 | — | — |
| AC-009 | FR-011, FR-013 | — | S-04 |
| AC-010 | FR-012 | VR-007 | S-01, S-02, S-03 |
| AC-011 | FR-008, FR-016 | VR-001 … VR-005 | S-06, S-07 |
| AC-012 | FR-002, FR-004, FR-005, FR-015 | — | S-08 |
| AC-013 | FR-013, FR-014 | — | S-04, S-10 |
| AC-014 (derived) | FR-003, FR-004, FR-010 | VR-002 | — |
| AC-015 (derived) | FR-010 | VR-006 | — |
