---
artifact_type: specification
story: US-044
version: 1
status: APPROVED
created_at: 2026-10-11T06:39:47Z
updated_at: 2026-10-11T07:05:13Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-044-meet-reports-export.md
    version: null
  - path: trebovaniya.md
    version: 88
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/specifications/US-028-spec.md
    version: 1
supersedes: null
---

# US-044 Specification — Export the built-in Meet reports to Excel

## 1. Overview

US-033 shows the four built-in Meet reports of `trebovaniya.md` §4 Epic 4 on
screen. The §2 matrix row "Просмотр и экспорт статистики Meet-занятий" gives
Admin and Dean both viewing **and export**. This Story adds the export:

- an **"Export to Excel"** action on the Meet reports page: one workbook with
  reports 1–3 for the course and period on screen (OD-003, OD-005);
- an **"Export to Excel"** action on the page of one meeting: one workbook with
  report 4 (OD-006);
- both files are built from the **same Application queries** as the pages
  (`GetMeetReportsQuery`, `GetMeetReportMeetingQuery`), mapped in `Application`
  into the workbook model of US-028 and written by `IReportRenderer`
  (ClosedXML, `Infrastructure/Export`);
- each export is a `POST` with the antiforgery token, writes one audit row
  (§5, SC-11), and works in read-only mode (BR-026).

The layout is built in and fixed (OD-002). Rows are selected in Excel with an
autofilter, not on screen (OD-004). No new page, no new package and no Google
call.

Delegated: lookups in `trebovaniya.md` (§2, §4 Epic 4, §5, §7), SC-11, BR-025 /
BR-026, and the existing code of US-028 (`JournalExportController`,
`IReportRenderer` and its workbook model, `AuditEvent.JournalExported`,
`PermittedServiceWrites`, `JournalExportFileName`, export translation keys) and
US-033 (queries, routes, `ViewMeetReports`) went to read-only `quick-look`
agents. Their reports were checked against the files named in this
Specification.

## 2. Business Goal

A Dean watches how the lessons of a course actually go in Meet (§4 Epic 4).
Today the reports exist only on screen. The file lets the school keep them,
print them, and filter, sort or total them in Excel — for example, to look at
one teacher's meetings or the students with no meeting.

## 3. Business Flow

### 3.1 Course workbook (reports 1–3)

1. The Dean (or Admin) opens "Meet reports", chooses a course and a period and
   sees reports 1–3 (US-033).
2. Under the form, next to the reports, they choose the orientation (landscape
   pre-selected) and press **"Export to Excel"**.
3. The browser sends a `POST` with the course id and the period of the page and
   the orientation, with the antiforgery token.
4. The installation validates the input, builds the same reports, maps them to
   the workbook model, renders the file in memory, writes the audit row and
   returns the file.

### 3.2 Meeting workbook (report 4)

The same on the page of one meeting: the `POST` carries the course id, the
meeting id and the orientation; the file holds report 4.

### 3.3 Read-only mode

Both exports work; their audit rows are written (BR-026).

## 4. Functional Requirements

### FR-001 The two export requests

| Export | Inputs | Default |
|---|---|---|
| Course workbook | course id, `from`, `to`, orientation | orientation absent → landscape |
| Meeting workbook | course id, meeting id, orientation | orientation absent → landscape |

- **Method: `POST`** with the antiforgery token (`trebovaniya.md` §8 v64:
  "выгрузка журнала и отчёта — POST"; API-3, API-4, API-7). Routes under
  `/api/v1/exports/…`, as the journal export's
  `POST /api/v1/exports/journal-xlsx`; exact names and request shape are
  API_DESIGN's call within API-2, API-3 and API-6.
- The inputs are internal ids, dates and the orientation — **no personal data**
  (US-039 INFO-1).
- The meeting export takes no period: report 4 does not depend on it (US-033
  I-1).

### FR-002 The entry points

- **Reports page.** The action and the orientation choice appear only when the
  page shows reports 1–3 for a course — including the empty states of US-033
  FR-014 — and never on the bare form or after a validation failure (OD-005,
  AC-001). They carry the validated course id and the **effective** period of
  the page (after the current-month defaults of US-033 FR-003), so the file
  equals the screen.
- **Meeting page.** The action and the orientation choice appear whenever the
  page shows a meeting; not on the "meeting not found" page.
- The orientation choice: "Альбомна" / "Landscape" (pre-selected) and
  "Книжкова" / "Portrait" (OD-009). It is sent with the export and **stored
  nowhere**.
- The actions are shown in read-only mode. Showing them is not enforcement: the
  policy and validation decide (AD-6, SC-4).
- Usable at phone width (NFR-070).
- The pages of US-033 change only by these two actions (Story Out of scope).

### FR-003 One source of content

- The course workbook runs `GetMeetReportsQuery` and the meeting workbook runs
  `GetMeetReportMeetingQuery` — the queries of the pages — with the same
  rules: meetings of the course and period, counting and marks (US-033 FR-004,
  FR-005), the minute cap and rounding (FR-006), ordering (FR-008 … FR-010),
  how a person is shown (FR-011 with US-033 OD-012), empty states (FR-014).
  The export never recomputes a value of its own.
- If the file and the screen differ for the same inputs, the screen is right and
  the export is the defect.
- The only differences from the screen are those of form decided by this Story:
  numbers and dates as typed cells (FR-007), the total duration in minutes
  (OD-008), the "N of M" of report 4 as two numeric columns (FR-006), and a
  person's name and email in two columns (OD-013, FR-007).

### FR-004 The header block

Each sheet starts with a header block (OD-007), one row each, each value
preceded by a translated label in the first column:

| Row | Course workbook | Meeting workbook |
|---|---|---|
| 1 | Course — the course name | Course — the course name |
| 2 | Period — `from` and `to` | Meeting — date, start and end (end with its date when it differs, US-033 I-7) |
| 3 | — | Duration — minutes (number) |
| 4 | — | Meeting code — as stored |
| 5 | — | Organizer — name, email and mark in three cells (OD-013): the mark is "not a teacher of this course" when it applies, otherwise empty |
| 6 | Exported — date and time of the export | Exported — date and time of the export |
| 7 | the data-delay line | the data-delay line |

- Rows marked "—" are absent; the block has no gaps.
- **Exported** is the instant of the export from the injected `TimeProvider`,
  in the school's time zone (`SchoolTimeZone`), as a date-time cell (I-3).
- **The data-delay line** is the translated notice of US-033 FR-012
  (`MeetReports.DelayNotice`) followed by the time up to which Meet meetings
  are loaded (`LastSynchronizationView.MeetLoadedUpTo`), or, when that is null,
  the "not loaded yet" text (`MeetReports.DelayNotice.NotLoaded`). One text
  cell, formatted for the UI language as on screen.
- One empty row separates the block from the table.
- In the course workbook the block is repeated on all three sheets, so each
  sheet prints on its own (as US-028 I-2).

### FR-005 Course workbook — three sheets

Sheets in this order, names translated (OD-003; FR-010):

| Sheet | uk | en |
|---|---|---|
| 1 | «Зведення» | "Summary" |
| 2 | «Зустрічі» | "Meetings" |
| 3 | «Участь студентів» | "Student participation" |

**FR-005.1 Summary.** After the header block, a two-column table "Indicator" /
"Value" with four rows — the values of US-033 FR-007:

| Indicator | Cell |
|---|---|
| Number of meetings | number |
| Total duration, min | number, whole minutes (OD-008) |
| Average duration, min | number, the value shown on screen |
| Average number of course students per meeting | number, one decimal place as on screen, number format `0.0` |

With no meeting every value is 0, as on screen.

**FR-005.2 Meetings.** After the header block, a table with the columns of
US-033 FR-008 — the organizer in three columns (OD-013), eleven in all — one
row per meeting in the screen's order:

| Column | Cell |
|---|---|
| Date | date |
| Start | time |
| End | time; a date-time when the end date differs from the start date (US-033 I-7) |
| Duration | number, minutes as on screen |
| Organizer | text, the name (FR-007 person rule) |
| Organizer email | text |
| Organizer mark | text, the translated "not a teacher of this course" when it applies, otherwise empty |
| Course teachers | number |
| Course students | number |
| Not on the course list | number |
| Other participants | number |

No meeting: the translated line "no Meet meetings of this course in the period"
replaces the table (US-033 FR-014).

**FR-005.3 Student participation.** After the header block, a table with the
columns of US-033 FR-009 — Student (text, the name), Email (text), Meetings
(number), Minutes (number) — one row per student of the period in the screen's order. No
student: the translated line "no students in this period" replaces the table.

### FR-006 Meeting workbook — one sheet

One sheet named «Зустріч» / "Meeting". After the header block of FR-004:

- a table with the columns Person (text, the name), Email (text), Mark (text,
  the translated mark of US-033 FR-010), Minutes (number, N) and Of meeting, min
  (number, M — the meeting's duration in minutes); one row per identified
  person in the screen's order — organizer first (when connected, US-033 I-3),
  then course teachers, course students, not on the course list;
- one empty row, then the line **"Other participants"**: the translated label,
  their number (number) and their total capped minutes (number) — never an
  address or name (US-033 FR-010, I-11; AC-003). The line is outside the
  table and its autofilter, so sorting or filtering the table never moves it
  (I-5).

When nobody identified connected, the table keeps its header row with no data
row, and the "Other participants" line follows (I-6).

### FR-007 Typed cells, filter, freeze and print

- **Numbers** — every count, minute value and average is a numeric cell
  (OD-008), never text.
- **Dates and times** — real Excel date, time or date-time cells with the UI
  language's formats (OD-008), in the school's time zone.
- **Persons** (OD-013) — wherever the screen shows a person (US-033 FR-011),
  the file has two text columns, name and email, so that each can be sorted
  and filtered on its own. The name cell holds the participant's full name as
  Google gave it; when there is none it is empty and the email cell carries the
  person. A person with neither holds the translated "unnamed" label
  (`MeetReports.Person.Unnamed`, US-033 OD-012) in the name cell.
- **Text** — names, emails, course names, meeting codes, marks and labels are
  text cells written as values, never through a formula API. A text starting
  with `=`, `+`, `-`, `@`, tab or carriage return gets the quote prefix, as in
  US-028 FR-004.7 (AC-009). Text longer than 32 767 characters is cut to the
  limit (US-028 FR-004.9).
- **Autofilter** (OD-004) — on the header row of each table of FR-005 and
  FR-006, covering the table's rows only. A table replaced by an empty-state
  line has no autofilter.
- **Frozen header** — the rows down to and including the table's header row
  are frozen on screen.
- **Print** (OD-009) — the orientation chosen at export (landscape by default),
  fit to one page wide, the table's header row repeated on every printed page;
  column widths follow the content within a fixed maximum, text wraps in header
  cells — as US-028 FR-004.8.
- The journal export's file does not change: whatever the workbook model gains
  for this Story (autofilter, time and date-time cells, a number format) is
  opt-in per sheet or cell, and US-028's tests stay green (I-7).

### FR-008 The file and the response

- `200` with the file; content type
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`;
  `Content-Disposition: attachment` (API-2).
- `Cache-Control: no-store` by the host-wide rule of US-040 (SC-14); the SC-14
  enumeration test covers the new endpoints.
- Built **in memory**; never written to disk, stored in the database or kept.

**FR-008.1 File name** (OD-010):

- course workbook: `Meet <course> <from>–<to>.xlsx`, dates `yyyy-MM-dd`;
  example `Meet Алгебра 7-А 2026-09-01–2026-09-30.xlsx`;
- meeting workbook: `Meet <course> <date> <HH-mm>.xlsx` — the meeting's start
  date `yyyy-MM-dd` and time in the school's time zone, with `-` in place of
  `:`, which a Windows file name cannot hold (I-8); example
  `Meet Алгебра 7-А 2026-09-14 08-30.xlsx`.
- The course part is cleaned by US-028 FR-006.1 (`JournalExportFileName`'s
  rule: forbidden characters → `_`, white space collapsed, trimmed, 100
  characters, translated "course" fallback).
- Never a person's name or email, never the meeting code.
- Non-ASCII names per RFC 6266 (`filename*` UTF-8 with an ASCII fallback), so a
  Cyrillic name downloads correctly (AC-008).

### FR-009 Validation and not-found

Inputs are validated **before anything is read for the report** (§8, SC-10),
by the rules of §6. On a failure: no file, no audit row; the user sees a
translated message naming the problem by the mechanism the journal export uses
(API-6 error body, fixed by API_DESIGN as in US-028).

| Input | Answer |
|---|---|
| malformed course id, meeting id, date or orientation; `from` after `to`; a required input absent; a repeated parameter | `400` |
| unknown course; unknown meeting; a meeting not of the course | `404` |
| missing or invalid antiforgery token | `400` (API-7) |

A rejected value is never logged, echoed in the error body or audited; the log
names the field and the rule (SC-10).

### FR-010 Language of the file

Program text — sheet names, header-block labels, column headings, marks, the
"Other participants" label, the delay line, the empty-state lines, the
file-name fallback — comes from the translation files in the **UI language of
the exporting user** (OD-012, NFR-073). Ukrainian uses «студент», as on screen
(US-033 OD-003). Course names, person names, emails and meeting codes are
written as stored, never translated.

### FR-011 Authorization

- Both exports are covered by **`ViewMeetReports`** (US-033: the §2 row
  "Просмотр и экспорт статистики Meet-занятий" — Admin ✔, Dean ✔). No new
  policy, no new matrix cell.
- Deny by default; an anonymous request gets `401` (`/api/v1`); the SC-4
  anonymous list is unchanged; the endpoint enumeration test classifies both as
  protected.
- A Dean held on the forced password change is refused as everywhere (US-012).
- No per-course scoping; Teacher and Student have no account (Hard Stop).

### FR-012 Read-only mode

- Both exports work for all three BR-025 causes (BR-026). They do not call
  `IReadOnlyModeGuard`.
- Their only write is the audit row of FR-013, on the BR-026 closed list. Each
  new command declares `PermittedServiceWrite.AuditEvent` in
  `PermittedServiceWrites`, as `ExportJournalCommand` does.
- No Google port is opened, in any mode (SC-8, SC-13).

### FR-013 Audit

Every **successful** export writes one `AuditEvent` (§5 "экспорт журнала или
отчёта", SC-11, OD-011):

| Field | Course workbook | Meeting workbook |
|---|---|---|
| Action | new value, indicative `MeetReportsExported` | new value, indicative `MeetMeetingExported` |
| Actor | session account id and role | same |
| Target | course, its internal id | course, its internal id |
| Outcome | `Succeeded` | `Succeeded` |
| Period | `from`, `to` | — |
| Meeting | — | the meeting's internal id |
| Row counts | student rows of report 3, and meeting rows of report 2 (I-9) | identified-person rows of the table (I-9) |
| Format | Excel | Excel |

- The meeting id is an internal id, not personal data and not file content;
  SC-11's export list (course ids, period, template id, row count) gains it by
  OD-011, within §5 (F-1).
- Never a name, email, meeting code, mark or any file content (SC-10, SC-11).
- Storage of the new fields (meeting id, a second count) is DB_DESIGN's call,
  with its migration (PC-2).
- **Order**: render the file, write and commit the audit row, then return the
  file. If the audit row cannot be written, no file is returned and the single
  exception handler answers (AD-9, API-10), as in US-028 FR-010.
- Viewing the pages, a validation failure and a not-found write no audit row.

### FR-014 Logging

- A successful export: one `Information` line with the actor's account id, the
  course id, the period or the meeting id, the row counts, the orientation and
  the file size in bytes.
- A validation failure: one `Warning` naming the field and the rule, never the
  value.
- No log line carries a name, email, meeting code or file content (SC-10,
  DC-10).

### FR-015 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073):
the four sheet names, the header-block labels not yet present (period,
meeting, duration, meeting code, organizer, exported), the "Email",
"Organizer email" and "Organizer mark" column headings, the Summary's
"Indicator" / "Value" headings and its four indicator labels in their file
form, "Of meeting, min", and the "Other participants" label if no key fits.
Existing keys are reused, not duplicated: US-028's export action, orientation
choice and its values, orientation and malformed-request messages, "course"
fallback (`Report.Export.*`, `Export.*`); US-033's column headings, marks,
delay notice and empty-state lines (`MeetReports.*`). The missing-key test
covers the new keys.

### FR-016 Wiring

- Two Application commands (indicative `ExportMeetReportsCommand`,
  `ExportMeetMeetingCommand`) with request models and validation in
  `Application/Models/Requests` and `Validation`, mapping the page results into
  the workbook model (AD-8: no entity in a controller signature or body).
- Whatever the workbook model and the renderer need for FR-007 (autofilter,
  time and date-time cells, number format) is added to the existing
  `Application/Ports` model and `Infrastructure/Export` renderer. No ClosedXML
  type crosses into `Application` (AD-4). No new package.
- The file-name rule is reused, extended for the `Meet` prefix and the meeting
  form; the US-028 file name is unchanged.
- DI registration, translations, `PermittedServiceWrites` entries, and the
  audit migration if DB_DESIGN needs one.

## 5. Acceptance Criteria

The Story's AC-001 … AC-015, unchanged in meaning:

| AC | Summary | Covered by |
|---|---|---|
| AC-001 | Button and orientation only when reports are built; export that course and period | FR-002 |
| AC-002 | Three sheets Summary, Meetings, Student participation, rows equal to the screen | FR-003, FR-005 |
| AC-003 | Meeting workbook: header, one row per identified person with mark and "N of M", "Other participants" line without address or name | FR-003, FR-006 |
| AC-004 | Header block: course, period or meeting time, export time in school time zone, data-delay line | FR-004 |
| AC-005 | Autofilter on each table's header row; header frozen | FR-007 |
| AC-006 | Counts, minutes, average numeric; total duration in minutes; dates and times as date/time cells | FR-005, FR-006, FR-007 |
| AC-007 | No choice → landscape; "Portrait" → portrait; other value → validation error | FR-001, FR-007, VR-003 |
| AC-008 | File names, course cleaned, Cyrillic downloads | FR-008.1 |
| AC-009 | Formula-like text stays text | FR-007 |
| AC-010 | One audit row per export: course id and period, or course id and meeting id, and row count; no name, email or code | FR-013 |
| AC-011 | Read-only mode: both exports work, audit rows written | FR-012, FR-013 |
| AC-012 | Unknown course, meeting not of the course, inverted or unparsable period, invalid orientation → API-6 body, matching status, value not logged | FR-009, FR-014 |
| AC-013 | Dean and Admin export; unauthenticated refused | FR-011 |
| AC-014 | Program text in UI language; Google data unchanged | FR-010, FR-015 |
| AC-015 | Tests never reach Google | §9 |

Derived from the Specification:

| AC | Criterion |
|---|---|
| AC-016 | The response carries `Cache-Control: no-store`, and the journal export's workbook is unchanged by this Story (FR-007, FR-008) |
| AC-017 | Period boundaries in `Europe/Kyiv`: the course workbook contains exactly the meetings of the page for the same period, including the US-033 AC-018 boundary cases; the export time and meeting times are local (FR-003, FR-004, TC-8) |

## 6. Validation Rules

### VR-001 Course id and period (course workbook)

- `courseId`: US-033 VR-001, but **required** — absent or empty → `400`
  ("choose a course from the list"); well-formed but unknown → `404`.
- `from`, `to`: US-033 VR-002 format rules (`yyyy-MM-dd`, a real date, years
  2000–2100), but **required** — the page always sends its effective period;
  absent → `400`. `from` after `to` → `400`. A one-day period is valid.

### VR-002 Course id and meeting id (meeting workbook)

- `courseId`: as VR-001.
- `meetingId`: exactly one value of decimal digits parsing to a positive 64-bit
  integer, required; malformed or absent → `400`; unknown, or a meeting not of
  the course (US-033 FR-004 course condition) → `404`.

### VR-003 Orientation

Absent → landscape. Otherwise exactly `portrait` or `landscape`; any other
value → `400` with the translated message naming the field (reused US-028
key).

### VR-004 Other input

A repeated parameter is malformed (`400`). Any other input is ignored and never
echoed. The actor and the UI language come from the session, never from the
request (US-028 VR-002).

### VR-005 Data from the database

Names, emails, course names and meeting codes come from rows validated at
ingestion (US-014, US-031). In the file they are values only, never formulas
(FR-007), cut to Excel's limit, never translated.

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | Both endpoints declare `ViewMeetReports`; deny by default; SC-4 anonymous list unchanged. | SC-4, §2, API-9 |
| S-02 | Allowed-role (Admin, Dean) and forbidden-role (anonymous; Dean on forced password change) tests for each endpoint. | SC-1, TC-5 |
| S-03 | `POST` with antiforgery; never reachable by `GET`. | §8 v64, API-4, API-7 |
| S-04 | No Google port, no secret read, in any mode; read-only mode works with the audit row written. | SC-5, SC-8, BR-026, TC-5 |
| S-05 | Input validated before any read; a rejected value never in a log, audit row or error body. | §8, SC-10 |
| S-06 | One audit row per export with ids, period, counts and format only; written before the file is returned. | §5, SC-11 |
| S-07 | File built in memory, never stored. | Story, SC-10 |
| S-08 | No text cell is a formula; dangerous leading characters get the quote prefix. | Story, SC-10 |
| S-09 | Request and file name carry no personal data and no meeting code. | Story OD-010, US-039 INFO-1 |
| S-10 | `Cache-Control: no-store`. | SC-14 |
| S-11 | No entity in a request or response; the renderer gets the workbook model only. | AD-8, AD-4 |
| S-12 | Other participants appear only as a count and minutes; a stored non-personal domain email is never written to the file. | §3 `MeetParticipation`, US-033 FR-010 |
| S-13 | No new outbound destination, no new package. | SC-13, AGENTS.md |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous | `401` |
| Dean on forced password change | refused as everywhere (US-012) |
| Missing or wrong antiforgery token | `400` |
| Malformed or missing input, `from` after `to`, bad orientation | `400`, API-6 body with a translated message; no file, no audit row |
| Unknown course or meeting, meeting not of the course | `404`, API-6 body; no file, no audit row |
| No meetings / no students / nobody identified | not an error: the file with the empty-state lines (FR-005, FR-006), audited with counts 0 |
| Meet never loaded | not an error: "not loaded yet" in the delay line |
| Read-only mode | not an error (FR-012) |
| Rendering or audit write fails | no file; the single exception handler answers with no detail (AD-9, API-10) |

## 9. Non-Functional Requirements

- **Same queries as the pages** — no extra reads beyond what the pages do,
  plus the audit write; not paginated (API-8). A course has at most a few
  hundred meetings a year, so the workbook is built in memory.
- **Time** — injected `TimeProvider` for the export time; `SchoolTimeZone` for
  every date and time (NFR-074); boundary tests in `Europe/Kyiv` (TC-8).
- **Tests** — synthetic data in PostgreSQL via Testcontainers (TC-2), Google
  ports substituted (TC-4); the workbook is opened from the response bytes in
  memory and its sheets, cells, cell types, autofilter, frozen panes and page
  setup are asserted; no file is written into the repository (Hard Stop).
- **NFR-070** — the actions usable at phone width.
- **NFR-062** — .NET 10, nullable enabled, warnings as errors.

## 10. Out of Scope

- An on-screen filter or search, or exporting a filtered subset (OD-004);
  course list filters — US-020.
- Word export — with US-029 (OD-001).
- A school's own layout or template for the Meet export — Epic 11 (OD-002).
- Report 4 of every meeting inside the course workbook (OD-006).
- Any change to the on-screen reports of US-033 beyond the two actions.
- A screen to view audit rows (EPIC-9).

## 11. Open Decisions

OD-001 … OD-012 come from the Story, all resolved by the Owner before
activation; OD-013 (person columns) was raised and resolved at
`HUMAN_SPEC_APPROVAL`; see `docs/decisions/US-044-open-decisions.md`. Writing the
Specification raised none: `trebovaniya.md` v88 §2 grants the export, §5 audits
it, §8 makes it a `POST`; §7 has no open item on Meet reports or export (item
28, `call_ended` field names, concerns US-031's ingestion; this Story's tests
use synthetic data).

### Interpretations

Choices the wording leaves open; any may be turned into an Open Decision at
`HUMAN_SPEC_APPROVAL`. I-4 and I-9 were confirmed by the Owner at the gate (2026-10-11); I-1 became
OD-013.

- **I-1** Superseded by OD-013: name and email in separate columns.
- **I-2** The Summary is a two-column "Indicator / Value" table with an
  autofilter, so "each table has an autofilter" holds without an exception.
- **I-3** "Exported" is a date-time cell in the school's time zone; it is the
  only value in the file not shown on screen.
- **I-4** *(confirmed)* "N of M min" of report 4 becomes two numeric columns,
  Minutes (N) and Of meeting, min (M), so that both are numbers (OD-008). M is
  the same in every row.
- **I-5** The "Other participants" line sits below the table, after an empty
  row, outside the autofilter range.
- **I-6** A meeting where nobody identified connected keeps the table's header
  row with no data row, rather than an empty-state line; US-033 defines no
  empty state for report 4, and the "Other participants" line still follows.
- **I-7** Additions to the workbook model are opt-in, so the journal export's
  file is unchanged; a regression test keeps US-028's assertions.
- **I-8** The meeting file name uses `HH-mm` for the start time, because `:` is
  forbidden in Windows file names.
- **I-9** *(confirmed)* Row counts in the audit row: the course workbook records
  two — student rows of report 3 and meeting rows of report 2 (the Story says
  "row counts"); the meeting workbook records the identified-person rows of its
  table, not the "Other participants" line.
- **I-10** The meeting export takes no period (report 4 does not depend on it,
  US-033 I-1); the meeting page's period serves only its link back.

### Findings

- **F-1** SC-11 lists the export row's fields as course ids, period, template id
  and row count. The meeting id of report 4 (OD-011) is an internal id, not
  personal data and not file content, and `trebovaniya.md` §5 fixes only that
  the export is audited and what a row may not carry. **Done at the gate**
  (Owner, 2026-10-11): SC-11 now lists the meeting id and "row counts" — a
  derived-document fix, not a requirements change.
- **F-2** OD-007 says "as in the journal export", but the journal export's
  header block (US-028 FR-004.2) has no export time and no delay line.
  **Clarified at the gate** (Owner, 2026-10-11): OD-007 means "a header block
  in the manner of the journal export, plus the export time and the Meet
  data-delay line". The export time is the instant the file is built (not a
  duration). Both rows are added for the Meet export only; the journal export
  is unchanged and gains no export time in this Story.

## 12. Traceability

| AC | FR | VR / S |
|---|---|---|
| AC-001 | FR-002 | — |
| AC-002 | FR-003, FR-005 | — |
| AC-003 | FR-003, FR-006 | S-12 |
| AC-004 | FR-004 | — |
| AC-005 | FR-007 | — |
| AC-006 | FR-005, FR-006, FR-007 | — |
| AC-007 | FR-001, FR-007 | VR-003 |
| AC-008 | FR-008.1 | S-09 |
| AC-009 | FR-007 | VR-005, S-08 |
| AC-010 | FR-013 | S-06 |
| AC-011 | FR-012, FR-013 | S-04 |
| AC-012 | FR-009, FR-014 | VR-001 … VR-004, S-05 |
| AC-013 | FR-011 | S-01, S-02, S-03 |
| AC-014 | FR-010, FR-015 | VR-005 |
| AC-015 | §9 | S-04 |
| AC-016 | FR-007, FR-008 | S-10 |
| AC-017 | FR-003, FR-004 | VR-001 |
