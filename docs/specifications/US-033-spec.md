---
artifact_type: specification
story: US-033
version: 1
status: APPROVED
created_at: 2026-10-10T19:39:19Z
updated_at: 2026-10-10T19:51:18Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-033-meet-reports.md
    version: null
  - path: trebovaniya.md
    version: 88
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/specifications/US-025-spec.md
    version: 1
supersedes: null
---

# US-033 Specification — Built-in Meet reports for a course and period

## 1. Overview

US-031 stores the Meet meetings organized by school accounts, and US-032 links
their codes to courses. Nothing shows them yet. This Story adds the built-in
set of four reports from `trebovaniya.md` §4 Epic 4, for one course and one
period, showing facts only (BR-062):

1. **Summary** — number of meetings, total and average duration, average number
   of course students per meeting;
2. **Meeting list** — one row per Google meeting (BR-063) with the counting
   columns of v58;
3. **Student participation** — per student of the period, meetings attended
   and total minutes;
4. **One meeting in detail** — everyone who connected, with a mark and
   "N of M min".

Reports 1–3 are on a new **"Meet reports" page**, and report 4 is on its own
page. Dean and Admin both see them (§2 matrix). The Story only reads: it writes
no row, adds no audit event and calls no Google API. Export is US-044 (OD-001).

Delegated: lookups of the existing entities (`MeetSession`,
`MeetParticipation`, `MeetingCodeLink`, `ClassroomParticipant`,
`CourseMembership`, `SyncState`), of `SchoolTimeZone`, of the journal's course
source and of the home page navigation went to a read-only `quick-look` agent;
its report was checked against the files.

## 2. Business Goal

A Dean controls how lessons of a course actually go in Meet (§4 Epic 4): whether
meetings take place, how long they last, and how many of the course's students
take part. This is not an attendance register: a lesson can take place in a
classroom or offline, and a missing meeting does not mean a missing student.
The reports therefore show facts and make no judgement.

## 3. Business Flow

### 3.1 Looking at a course

From the home page the Dean (or Admin) follows "Meet reports". The page shows a
form: the journal's drop-down of all synchronized courses (OD-007) and a
from/to period that defaults to the current month (OD-010). Choosing a course
shows, under the form, the delay notice and reports 1–3 for the course and
period. The choice travels in the query string (course id and dates — no
personal data), so the page can be bookmarked and reloaded.

### 3.2 Looking at one meeting

A row of report 2 links to report 4 for that meeting. The page names the
course, the meeting's date and time and its organizer, lists every connected
person with a mark and their minutes, and links back to the report page with
the same course and period.

### 3.3 Read-only mode

The reports work as usual; nothing in this Story writes (BR-026).

## 4. Functional Requirements

### FR-001 The "Meet reports" page and navigation

A new page of the installation UI, open to **Dean and Admin**. The home page
gains a "Meet reports" entry for both roles, next to "Journal" and "Meet
meetings" (OD-010). Exact routes are API_DESIGN's call. As in the journal
(US-025), the page is a `GET` whose query string carries only the course id,
`from` and `to`.

Without a course id the page shows only the form. With a valid course id and
period it shows the form with the chosen values, then the delay notice
(FR-012), then reports 1, 2 and 3 in that order.

### FR-002 Choosing a course

The drop-down is the journal's own (US-025 FR-002, OD-007): every stored
`Course` by name, archived ones included, from the same Application source
(`IJournalSource.GetCoursesAsync` or a shared query — a design choice). When
no course is stored, a translated message replaces the drop-down.

### FR-003 The period

As in the journal (US-025 FR-003, FR-007): `[from, to]` are calendar dates in
the school's time zone (`SchoolTimeZone`), both inclusive, turned into the
UTC interval `[start, end)` — `start` is `from` at local midnight, `end` is the
local midnight after `to`; on a daylight-saving gap the first valid local
instant is used. An absent `from` is the first day of the current month, an
absent `to` its last day, each on its own (OD-010). The current month is the
one that contains "now" from the injected `TimeProvider` in the school's time
zone.

### FR-004 The meetings of the course and period

The **meetings of the report** are every `MeetSession` where both hold:

- its `MeetingCode` has a `MeetingCodeLink` whose `CourseId` is the chosen
  course — automatic or confirmed, never shown differently (v55, OD-009). A code
  marked "not a course" (`CourseId` null) belongs to no course (BR-083). A code
  with no link row belongs to no course;
- its `StartedAt` lies in `[start, end)` (OD-004). The meeting counts wholly in
  the day of its start, even if it ends after midnight or after `end`.

A meeting's **duration** is `EndedAt − StartedAt`, as stored at ingestion
(PC-12).

### FR-005 Who a connected person is

For one meeting with start date D (the date of `StartedAt` in the school's time
zone), every connection is classified. Emails are matched case-insensitively
(PC-12). "Student / teacher of course C on date D" is US-032 FR-002, reused as
the shared Application function.

1. **Organizer** — the connection's email equals the meeting's
   `OrganizerEmail`. The organizer is named in their own column (report 2) or
   line (report 4) and **never counted** (v58). When the organizer has no
   connection in the stored data at all, they are still shown in report 2 and
   at the head of report 4 (I-3).
2. **Course teacher** — a teacher of the course on D (other than the
   organizer).
3. **Course student** — a student of the course on D.
4. **Not on the course list** — a **personal account** (BR-064: an email that
   has a `ClassroomParticipant` with any `CourseMembership` of any course of
   the installation, in any role, at any time) that is neither of the above.
5. **Other participant** — a connection with no email (external guest, no
   account), or a domain email that is not a personal account (class account,
   group address, staff without courses — v58 known limitations).

A person who matches several rules takes the first in this order (I-4).

The organizer also gets a mark of their own: **"not a teacher of this course"**
when they are not a teacher of the course on D (BR-064). That mark is shown
next to the organizer's name in reports 2 and 4.

### FR-006 A person's minutes in one meeting

For each identified person (organizer, teacher, student, not on the list — one
email) in one meeting, their **seconds** are the sum of `DurationSeconds` over
all their connections to that meeting, **capped at the meeting's duration**
(OD-005). Two devices at once or overlapping reconnects never give more than
the meeting lasted. Minutes shown are seconds ÷ 60, rounded to the nearest
whole minute, half up (I-5). Report 3 adds **capped seconds** first and rounds
only the total (I-5).

### FR-007 Report 1 — Summary

Over the meetings of the report (FR-004):

- **Number of meetings**.
- **Total duration** — the sum of the meetings' durations, shown in hours and
  minutes (I-6).
- **Average duration** — total ÷ number of meetings, shown in minutes.
- **Average number of course students per meeting** — the mean of report 2's
  "course students" column, shown with one decimal place in the UI language's
  number format (I-6).

With no meeting, every value is 0 (AC-012).

### FR-008 Report 2 — Meeting list

One row per meeting of the report (BR-063), ordered by `StartedAt` ascending,
then by internal id. Columns (§4 Epic 4, v58):

| Column | Value |
|---|---|
| Date | `StartedAt`'s date, school time zone |
| Start | `StartedAt`'s time, school time zone |
| End | `EndedAt`'s time, school time zone; with the date too when that date differs from the start date (I-7) |
| Duration | in minutes (FR-006 rounding) |
| Organizer | FR-011, with the "not a teacher of this course" mark when it applies (FR-005) |
| Course teachers | number of distinct teachers of FR-005 rule 2 |
| Course students | number of distinct students of rule 3 |
| Not on the course list | number of distinct personal accounts of rule 4 |
| Other participants | the number of distinct non-personal domain emails, plus the number of connections with no email (I-8) |

Each row links to report 4 for that meeting (FR-010). The list is rendered
whole, not paginated (I-9).

### FR-009 Report 3 — Student participation

Ukrainian label «Участь студентів», English "Student participation" (OD-003).

The rows are the **students of the period** (v31): every `CourseMembership` of
the chosen course with role `student` that was on the roster for at least part
of the period — `FirstSeenAt < end` and (`OnRoster` or `LastSeenAt ≥ start`),
as in the journal (US-025 FR-005 condition 1). Per row:

- the student (FR-011);
- **meetings** — the number of meetings of the report at which this person
  was a **course student** under FR-005. A meeting on a date when they were
  not on the roster does not count for them (AC-008);
- **minutes** — the total of their capped seconds (FR-006) over those
  meetings.

A student with no meeting has 0 and 0 (AC-008, AC-012). The rows are ordered by
the displayed name in the collation of the UI language, then by email, then by
internal id (I-10). The list is rendered whole.

### FR-010 Report 4 — One meeting in detail

A separate page for one meeting **of the chosen course** (FR-004, the course
condition only). The meeting is identified by its internal id, plus the course
id and the period it was opened from (they serve only the link back). The page
shows:

- the course name, the meeting's date, start, end and duration, the meeting
  code, the organizer (FR-011), and the "not a teacher of this course" mark when
  it applies;
- one line per identified person — organizer first, then course teachers,
  course students and not on the course list, each group ordered by displayed
  name (I-10). Each line has the person (FR-011), their mark (§4 Epic 4 v58:
  organizer, course teacher, course student, not on the course list) and
  "N of M min" (FR-006; M is the meeting's duration in minutes);
- **one line "Other participants"** with their number (FR-008's count) and
  their total capped minutes, without any address or name (I-11). Non-personal
  domain emails are stored, but they are not shown (§3 `MeetParticipation`,
  data minimization).

The period is not checked: a meeting of the course opens from any period. A
meeting that does not belong to the chosen course is "not found" (FR-013).

### FR-011 How a person is shown

A person with an email is shown by the **full name** of the
`ClassroomParticipant` with that email (case-insensitive) — the name as Google
gave it, never translated — with the email beside it (OD-006). When no
participant has that email, or the participant has no full name, the email
alone is shown (I-12).

### FR-012 Data delay notice

Above the reports, a translated notice says that Meet data arrive with a delay
of up to 24 hours (v47). It also shows the time up to which Meet meetings are
loaded: `SyncState`'s Meet watermark, already exposed as
`LastSynchronizationView.MeetLoadedUpTo` (a local date-time in the school's
time zone, US-031), formatted for the UI language. When the watermark is null,
the notice says that Meet meetings have not been loaded yet (OD-008). Dean and
Admin both see the notice. Only this time is shown: the failure diagnosis
stays on the Admin's connection page (§2 matrix).

### FR-013 Validation and its answer

The input is validated before any report data is read (§8, SC-10), by the
rules in §6. On a failure:

- the report page is re-rendered with the form and a translated message naming
  the problem — never the host's error page;
- report 4 with an unknown meeting, or one not of the chosen course, shows a
  translated "meeting not found" page;
- the status codes are API_DESIGN's choice within API-5. The expected choice is
  `400` for a malformed or inverted input and `404` for an unknown course or
  meeting;
- a rejected value is never written to a log; the log line names the
  parameter and the rule (SC-10).

### FR-014 Empty states

None of these is an error.

- No meeting in the period: report 1 shows zeros; report 2 is replaced by a
  translated line "no Meet meetings of this course in the period"; report 3
  still lists the students, with zeros.
- No student of the period: report 3 is replaced by a translated line "no
  students in this period".

### FR-015 Read-only mode and writes

The Story writes nothing: no entity, no audit row (viewing is not in SC-11), no
`SyncState`. In read-only mode (any BR-025 reason) both pages work as usual
(AD-6). Nothing here needs a read-only guard. A test proves the pages answer
in read-only mode (AC-015).

### FR-016 Localization and formatting

Every label, column header, mark, the delay notice, the empty-state lines and
the error messages come from the installation's translation files, in
Ukrainian and English (NFR-073). In the report labels, Ukrainian uses
«студент» (OD-003: «Участь студентів», «студенти курсу»). Dates, times and
numbers follow the UI language. Course names, person names, emails and
meeting codes are shown as stored and never translated.

### FR-017 Wiring

- Application query/queries for the two pages and their DTOs (AD-8); no
  domain entity reaches a controller or view.
- No new entity and no schema change are expected. If DB_DESIGN finds an index
  is needed for the queries (PC-7), it ships with its migration (PC-2).
- DI registration and translation entries.

## 5. Acceptance Criteria

The Story's AC-001 … AC-017, unchanged in meaning:

| AC | Summary |
|---|---|
| AC-001 | Course drop-down of every synchronized course incl. archived; current month default; reports 1–3 for a chosen course |
| AC-002 | Meetings of both linked codes (automatic unconfirmed and confirmed), unmarked; none of a "not a course" code |
| AC-003 | Meeting starting 23:30 on the last day is in the period wholly; one starting 00:10 the next day is not |
| AC-004 | Summary computed from the same meetings as report 2 |
| AC-005 | Counting rules: organizer by name and email, counted nowhere; 1 teacher, 2 students, 1 not on list, 2 other |
| AC-006 | Roster on the meeting's date |
| AC-007 | Organizer not a teacher of the course is marked |
| AC-008 | Report 3 lists attenders, a zero row, and a mid-period leaver counted only while on the roster |
| AC-009 | Two devices of 40 min on a 45-min meeting → 45, "45 of 45 min"; report 3 adds 45 |
| AC-010 | Report 4: everyone with mark and "N of M min"; other participants one line, count only |
| AC-011 | Delay notice with Meet-loaded-up-to time, or "not loaded yet" |
| AC-012 | No meetings → zeros, empty line, students with zeros |
| AC-013 | Invalid input → validation error / 404, rejected values not logged |
| AC-014 | Dean and Admin allowed; unauthenticated refused |
| AC-015 | Pages work in read-only mode |
| AC-016 | Bilingual; Google data shown as stored |
| AC-017 | Tests never reach Google |

One criterion is derived from the Specification:

| AC | Criterion |
|---|---|
| AC-018 | Period boundaries in a non-UTC school time zone (`Europe/Kyiv`): a meeting starting 00:30 local time on the day after `to` (still `to` in UTC) is **not** in the report; one starting 00:30 local on `from` (still the previous day in UTC) **is** (FR-003, FR-004, TC-8) |

## 6. Validation Rules

### VR-001 `courseId`

As in the journal (US-025 VR-001): absent or empty means the form only.
Otherwise it must be exactly one value of decimal digits that parses to a
positive 64-bit integer; anything else is **malformed** ("choose a course from
the list"). A well-formed id of no stored course is **unknown** ("this course is
not in the program").

### VR-002 `from`, `to` and the pair

As in the journal (US-025 VR-002, VR-003): absent or empty takes the FR-003
default. Otherwise each is exactly one `yyyy-MM-dd` value, a real calendar
date, year 2000–2100. After the defaults, `from` ≤ `to`, otherwise "the start
of the period is after its end". A one-day period is valid.

### VR-003 Meeting id (report 4)

Exactly one value of decimal digits that parses to a positive 64-bit integer.
Anything else is malformed and answered as not found. A well-formed id of no
stored meeting, or of a meeting not of the chosen course, is **not found**. The
course id of report 4 follows VR-001; there it is required, and absent counts
as malformed.

### VR-004 Unknown parameters and data from the database

Any other query parameter is ignored and never echoed back. Emails, names,
course names and meeting codes come from rows validated at ingestion (US-014,
US-031). They are HTML-encoded on output and never trusted as markup.

## 7. Security Requirements

- **Authorization** (SC-4, API-9, §2 matrix "Просмотр и экспорт статистики
  Meet-занятий"): one policy for both pages, allowing **Admin and Dean**. Every
  endpoint declares it. An anonymous request is refused (sign-in redirect for
  pages, `401` for any `/api/v1` endpoint).
- **Forbidden-role test** (TC-5): both v1 roles are allowed, so each endpoint's
  forbidden case is the unauthenticated request (AC-014).
- **No state change on `GET`** (API-7): the Story has no action; both pages are
  `GET` and change nothing.
- **Read-only mode** (AD-6): the pages are viewing and stay open (FR-015).
- **No Google call** (SC-8, SC-13): stored data only; no outbound flow.
- **Personal data** (PC-9, BR-070): names and emails of students and teachers
  are shown to Dean and Admin only. They never appear in a log (SC-10), an error
  message or a query string — the query string carries only ids and dates. The
  pages send `no-store`, like the other pages that show personal data (US-040).
- **Data minimization** (§3 `MeetParticipation`): other participants are shown
  only as a count; a stored non-personal domain email is not displayed (FR-010).
- **Audit** (SC-11): viewing is not an audited action; no row is written.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| malformed course id or dates, inverted period | page re-rendered with form and message, expected `400` (FR-013) |
| unknown course | page re-rendered with message, expected `404` |
| report 4: unknown meeting, or not of the course | "meeting not found" page, expected `404` |
| no meetings / no students | empty-state lines, `200` (FR-014) |
| Meet never loaded | notice "not loaded yet", reports from what is stored (FR-012) |
| read-only mode | pages work as usual (FR-015) |
| unauthenticated | sign-in redirect / `401` |

## 9. Non-Functional Requirements

- **Volume**: a course has up to a few hundred meetings a year, each with tens
  of connections. One page reads the meetings of one course and period, their
  participations, the course's memberships and the participants that match the
  emails found. It must not load the participations of the whole installation.
  The "personal account" check (FR-005 rule 4) looks up only the emails of the
  report's connections.
- **Time**: injected `TimeProvider` for the default period; `SchoolTimeZone`
  for every date (NFR-074, TC-8).
- **Bilingual UI** (NFR-073): FR-016.
- **Logging** (DC-10, SC-10): counts only (meetings, rows) and internal ids — no
  name, email or meeting code.

## 10. Out of Scope

- Export of the reports — US-044 (OD-001…OD-003).
- Lesson plan, lesson length, norms, merging reconnects, comparing teachers,
  report settings, views wider than one course — Epic 11 (BR-062).
- Course list filters and search — US-020.
- Any change to linking or to the "Meet meetings" page — US-032.
- An audit row for viewing.
- Showing the identity of other participants.

## 11. Interpretations (for the gate)

The wording of `trebovaniya.md` leaves these choices open. Each is decided here
and may be turned into an Open Decision at `HUMAN_SPEC_APPROVAL`. **Flagged:**
I-5, I-8, I-11.

- **I-1** Report 4 opens for a meeting of the course whatever the period;
  the period serves only the link back (FR-010).
- **I-2** Report 4 is a separate page, not an expandable row (OD-010).
- **I-3** An organizer who never connected (no stored connection with their
  email) is still named in report 2 and in report 4's header. They have no
  minutes line in report 4.
- **I-4** Precedence when a person matches several rules: organizer → course
  teacher → course student → not on the course list → other (FR-005). A person
  on the roster as both teacher and student on D counts once, as a teacher.
- **I-5** *(flagged)* Rounding: seconds are summed and capped per meeting;
  minutes are rounded half up to whole minutes; report 3 rounds only its total
  (FR-006). Example: two meetings of 30 s each give "1 min", not "2 min" or
  "0 min".
- **I-6** Total duration is shown as hours and minutes, average duration in
  minutes, and average students with one decimal (FR-007).
- **I-7** A meeting that ends on a later date than it started shows the end date
  in the "End" column (FR-008).
- **I-8** *(flagged)* "Other participants" counted in report 2: distinct
  non-personal domain emails + every connection with no email. A guest who
  reconnects twice counts twice, because such connections carry no identity
  to deduplicate (PC-12).
- **I-9** Reports 2 and 3 are rendered whole, not paginated, like the journal
  (US-025 I-9) — a course has at most a few hundred meetings a year.
- **I-10** People are ordered by displayed name in the UI language's collation,
  then by email (FR-009, FR-010).
- **I-11** *(flagged)* In report 4, other participants are one line with a
  count and total minutes. A non-personal domain email (a class account, for
  example) is stored but not shown (FR-010), because the requirement says "a
  separate line 'other participants'".
- **I-12** A person whose email matches no `ClassroomParticipant`, or one with
  no full name, is shown by email only (FR-011). This applies, for example, to
  an organizer from the staff without courses.

## 12. Open Decisions

OD-001 … OD-010 come from the Story. All are resolved; see
`docs/decisions/US-033-open-decisions.md`. None is open.

## 13. Traceability

| AC | Functional requirements | Validation |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003 | VR-001, VR-002 |
| AC-002 | FR-004 | — |
| AC-003 | FR-003, FR-004 | VR-002 |
| AC-004 | FR-007, FR-008 | — |
| AC-005 | FR-005, FR-008, FR-011 | — |
| AC-006 | FR-005 | — |
| AC-007 | FR-005, FR-008, FR-010 | — |
| AC-008 | FR-009, FR-006 | — |
| AC-009 | FR-006, FR-009, FR-010 | — |
| AC-010 | FR-010, FR-005, FR-006 | VR-003 |
| AC-011 | FR-012 | — |
| AC-012 | FR-014, FR-007, FR-009 | — |
| AC-013 | FR-013 | VR-001 … VR-004 |
| AC-014 | §7 Authorization | — |
| AC-015 | FR-015 | — |
| AC-016 | FR-016, FR-011 | VR-004 |
| AC-017 | §7 No Google call | — |
| AC-018 | FR-003, FR-004 | VR-002 |
