---
artifact_type: specification
story: US-063
version: 1
status: APPROVED
created_at: 2026-10-28T16:56:01Z
updated_at: 2026-10-28T17:02:09Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
supersedes: null
---

# US-063 Specification — «Сформирован» and «Данные из Google на» in the journal, the report and the Excel export

Delegation: one read-only lookup (Explore) collected code facts on the journal, report and export
paths; the spec writer read `trebovaniya.md` §4 Epic 3 lines 1363–1377 (v103, v104), the v103/v104
changelog, §7, and BR-026, BR-056, BR-084, NFR-074 in `docs/product/`, and checked the facts in §1.1
against the files named there.

## 1. Overview

A journal or report on screen, and a file exported from it, do not say when they were built nor as
of when the course's data was read from Google. This Story adds two lines — "generated" and "data
from Google as of" — to the header of the journal and the report on screen, and to a **note on the
course-name cell** of the Excel export (program layout and «Бланк для групових занять»), so the
printed paper journal is unchanged. Nothing is written, nothing is read from Google, nothing new is
stored.

### 1.1 Code facts the Specification must not contradict

- **The instant B.** `GetJournalQuery` reads `TimeProvider.GetUtcNow()` once per request (B) and
  builds every journal cell's state, "due passed" included, against it
  (`GetJournalQuery.cs:31`). `ReportBuilder.BuildAsync` reads its own B once
  (`ReportBuilder.cs:80`); both the report page (`GetReportQuery`) and the export
  (`ExportJournalCommand`) build the report through it. `GetReportQuery` reads the clock a second
  time only for the default period (`GetReportQuery.cs:35`); `ExportJournalCommand` reads it again
  for the audit event's time (`ExportJournalCommand.cs:145`). Neither of those two reads is B.
- **Last read.** `Course.LastCompleteReadAt` (`DateTimeOffset?`, US-058) is already read into
  `JournalCourseRecord`; US-060's `CourseDataNotice` carries it only as a date (`DateOnly? Since`,
  `DataMarkRules.cs:34`).
- **DTOs.** `JournalPageModel` (journal page) and `ReportHeader` (report page and export) carry
  US-060's `Notice` and neither carries B nor a date-time of the last read.
- **Screen.** The journal page has no course heading; US-060's line is `<p id="journal-notice">`
  above the table (`Views/Journal/Index.cshtml:54`). The report header is
  `<section id="report-header">` with US-060's `<p id="report-notice">`
  (`Views/Report/Index.cshtml:65-75`).
- **Program layout.** `ReportWorkbookMapper.HeaderBlock` writes the rows Template, Course, Period,
  Teachers (label cell + value cell) and an empty row on **both** sheets (grading and «Теми
  занять», lines 101 and 140). The course-name cell is the value cell of the Course row; US-060's
  line is a printed **third** cell of that row. The course-name cell has no note today.
- **Blank form.** `GroupLessonsBlankMapper` places `BlankFormMarker.Course` (`{{course}}`) on every
  grading copy (line 98); the topics copies have no course cell. That cell already carries US-060's
  line as its note when the course has one (line 84).
- **Notes.** `WorkbookCell.Note` is one `string?`; the renderers write it as an Excel comment.
- **Date-time display.** Screens show a date-time as the culture's `ShortDatePattern + " HH:mm"`
  (`Views/Courses/Index.cshtml:93`, `DataHealth/Index.cshtml:239`); the user's language is
  `CultureInfo.CurrentUICulture`; texts come from `SharedResource.uk.resx` / `.en.resx`, exports
  through `IReportTexts`.
- **Read-only mode.** Queries and the export do not call `IReadOnlyModeGuard`; the export's only
  write is its audit event (`PermittedServiceWrite.AuditEvent`, BR-026).

## 2. Business Goal

An exported file is a snapshot: a grade changed later is not marked anywhere, because the previous
value is not kept (BR-059, mirror). The Dean must be able to tell an old page or file from a fresh
one, and whether a course's data was last read an hour or a week ago (BR-084), without service lines
printed into the paper academic journal.

## 3. Business Flow

1. The Dean (or Admin, BR-002) opens the journal or the report of a course. The query takes B once,
   builds the cells against it, and returns B and the course's `LastCompleteReadAt` with the page.
2. The page shows both lines in its header.
3. The Dean exports the report (program layout or blank). The build takes its own B — the moment of
   export — and the file carries both lines in a note on each course-name cell.

## 4. Functional Requirements

### FR-001 "Generated"

1. "Generated" is the instant B the cells are built for — the same instant every cell's "due passed"
   is judged against (BR-056, BR-084). It is taken once per build and carried in the result; it is
   never read from the clock again when the header, the page or the file is rendered.
2. On the journal page it is `GetJournalQuery`'s B; on the report page and in the export it is
   `ReportBuilder`'s B. For an export B is therefore the moment of export.
3. The audit event's time and the report's default period keep their own clock reads, unchanged;
   they are not shown.

### FR-002 "Data from Google as of"

1. "Data from Google as of" is the course's `Course.LastCompleteReadAt` — its last complete
   successful read — whatever the course's state, an unchanged archived course included (OD-002 a).
2. It is the full instant, not only its date.
3. When `LastCompleteReadAt` is null the value is the word "unknown" (OD-001 a); the line is still
   shown.

### FR-003 On screen

1. **Journal** (full and short view): when a course is selected and found, the page shows both lines
   above the journal table, in its own block (`id="journal-generated"`, `id="journal-data-as-of"`),
   next to US-060's line where that line is present. No course selected, or an unknown course → no
   lines.
2. **Report**: when the report is built, its header (`#report-header`) shows both lines
   (`id="report-generated"`, `id="report-data-as-of"`) after the teachers line and US-060's line.
3. The lines are shown whether or not the period has data (an empty journal or report still shows
   them).
4. Plain text, no emphasis, no icon (BR-047, restrained tone).
5. Everything else on the page is unchanged.

### FR-004 Program-layout export

1. The course-name cell (the value cell of the Course row of the header block) gets a note holding
   both lines; its value, the row's other cells — US-060's third cell included — and every other
   cell are unchanged. The table does not move.
2. Both sheets (grading and «Теми занять») carry the note on their course-name cell.

### FR-005 Blank-form export («Бланк для групових занять»)

1. Every `{{course}}` cell gets a note holding both lines; its value is unchanged.
2. When the cell already has a note (US-060's header line), the note holds that text first, then
   both lines (AC-004).
3. Every copy of a multi-copy fill carries the note (OD-003 a). The topics copies have no course
   cell and get no note.
4. The form's printed text and layout are unchanged.

### FR-006 Format and texts

1. Both instants are converted from UTC to the school's time zone (`SchoolTimeZone`, NFR-074) and
   shown as the user's culture `ShortDatePattern + " HH:mm"`, as other date-times on screens; on
   screen and in the note alike.
2. The note's text is: the existing note (if any), then «Сформовано: …», then «Дані з Google на: …»,
   one per line (`\n`).
3. Texts come from `SharedResource.uk.resx` and `.en.resx` (NFR-073):

   | Meaning | uk (proposed) | en |
   |---|---|---|
   | generated | «Сформовано: {0}» | "Generated: {0}" |
   | data as of | «Дані з Google на: {0}» | "Data from Google as of: {0}" |
   | unknown last read | «невідомо» | "unknown" |

   The requirements word the labels in Russian («Сформирован», «Данные из Google на»,
   «невідомо»); the Ukrainian texts render the same meaning and are the ones the tests pin.
4. Course names, template text and other Google or school text are never translated.

## 5. Acceptance Criteria

From the Story, unchanged in meaning; derived criteria are marked.

| AC | Given / When / Then (summary) | FR |
|---|---|---|
| AC-001 | Course last read at R1, zone Europe/Kyiv, clock G1. Journal and report headers show «Сформовано: G1» and «Дані з Google на: R1», both in Kyiv time. | FR-001, FR-002, FR-003, FR-006 |
| AC-002 | An item due between two loads. Before and after the due moment the cell's state and «Сформовано» agree: the line shows the instant the cells were built for. | FR-001 |
| AC-003 | Data of AC-001, export at G2. The course-name cell of each file (program layout, blank) has a note with «Сформовано: G2» and «Дані з Google на: R1»; no printed cell contains either line; the cells and header rows are otherwise as before. | FR-004, FR-005 |
| AC-004 | A course whose course-name cell already has a note (US-060 header line, blank form). The note holds that text and both lines. | FR-005.2, FR-006.2 |
| AC-005 | A user with English. Header lines and note in English; times in the school zone; course names as Google returned them. | FR-006 |
| AC-006 | `LastCompleteReadAt` null. «Дані з Google на: невідомо» (en "unknown") on screen and in the note (OD-001 a). | FR-002.3 |
| AC-007 | Read-only mode. Both lines on screen and in the note; this Story writes nothing (the export's existing audit row stays the only write, BR-026). | §7 |
| AC-008 *(derived, OD-003 a)* | A blank fill with several grading copies; a program export. Every `{{course}}` cell, and the course-name cell of both program sheets, carry the note. | FR-004.2, FR-005.3 |
| AC-009 *(derived, OD-002 a)* | An unchanged archived course whose last complete read is older than its last listing. «Дані з Google на» shows the last complete read. | FR-002.1 |

## 6. Validation Rules

No new input: no endpoint, query parameter, form or upload changes.

- **VR-001 Generated:** exactly B of FR-001; one clock read per build.
- **VR-002 Data as of:** `Course.LastCompleteReadAt`, never `LastListedAt` or another field; null →
  "unknown".
- **VR-003 Time zone:** UTC instant → `SchoolTimeZone`, then the format of FR-006.1; seconds are not
  shown.
- **VR-004 Note composition:** existing note text kept verbatim and first; an absent or empty
  existing note adds no empty line.

## 7. Security Requirements

- No endpoint, page, role, policy or permission changes. Admin and Dean see the same (BR-002).
- **Read-only mode:** viewing and export keep working and show both lines (BR-026). This Story adds
  no write and no `IReadOnlyModeGuard` call; the export's audit write stays the only one.
- **Google:** no call — both values come from the clock and stored data (SC-7; TC-4 for tests).
- **Personal data:** the two lines hold no personal data; nothing new about a student leaves the
  system. No log line gains either value (SC-10).
- **Audit:** no new event; the export's existing event is unchanged (SC-11).
- **Outbound:** no new destination (SC-13).

## 8. Error Handling

- No new error. A null `LastCompleteReadAt` is a normal result (FR-002.3, AD-9).
- A text missing from a translation file is a defect caught by tests, not a runtime fallback.
- The export's error contract is unchanged.

## 9. Non-Functional Requirements

- **Persistence:** no schema change; DB_DESIGN expected NOT_APPLICABLE.
- **API:** no endpoint change; the page view models gain fields as US-060's `Notice` did; API_DESIGN
  expected NOT_APPLICABLE.
- **Cost:** `LastCompleteReadAt` is already read with the course; no extra query.
- **Architecture:** B and the last read travel in DTOs built in `Application` (AD-8); views and
  mappers render only (AD-3).
- **Tests:** synthetic data and a synthetic clock (`FakeTimeProvider`-style), PostgreSQL via
  Testcontainers, no Google call (TC-2, TC-4).

## 10. Out of Scope

- The Meet exports — they already carry their generation time (US-044); BR-084 names the journal
  and the report only.
- A "grade changed" mark — the previous value is not kept (BR-059, mirror).
- The course list and the course card.
- A Word export (none exists).
- An open page refreshing itself or reminding that it is old (v104, known limitation).
- US-060's header line placement (third printed cell of the program layout) — unchanged.

## 11. Open Decisions and Interpretations

### Open Decisions

- **OD-001** (from the Story) — unknown last read: **resolved a)** 2026-10-27. FR-002.3, AC-006.
- **OD-002** (from the Story) — archived course: **resolved a)** 2026-10-27. FR-002.1, AC-009.
- **OD-003** (from the Story) — several copies: **resolved a)** 2026-10-27. FR-005.3, AC-008.

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 "Generated" in an export is the build's B**, not the audit time: both are "the moment of
  export", milliseconds apart; B is the one the cells agree with (FR-001.2, FR-001.3).
- **I-2 Format: date and hours:minutes**, no seconds, as other date-times on screens (FR-006.1).
- **I-3 Journal placement.** The journal page has no course heading; the lines go above the table,
  next to US-060's line (FR-003.1).
- **I-4 Program layout: both sheets.** "The course-name cell" is the value cell of the Course row;
  both sheets have one, and each gets the note — OD-003 a) reasoning (FR-004.2).
- **I-5 Existing note first** — US-060's header line, then the two lines (FR-005.2, FR-006.2).
- **I-6 Read-only "nothing written"** means this Story adds no write; the export's audit row
  (already permitted by BR-026) is still written (AC-007).
- **I-7 Ukrainian wording** — the table in FR-006.3 translates the Russian wording of the
  requirements.

## 12. Traceability

| AC | FR | VR | Source |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-006 | VR-001, VR-002, VR-003 | BR-084, NFR-073, NFR-074 |
| AC-002 | FR-001 | VR-001 | BR-084, BR-056, Story Notes |
| AC-003 | FR-004, FR-005 | VR-003 | BR-084 |
| AC-004 | FR-005.2, FR-006.2 | VR-004 | Story AC-004, I-5 |
| AC-005 | FR-006 | VR-003 | NFR-073 |
| AC-006 | FR-002.3 | VR-002 | OD-001 a |
| AC-007 | §7 | — | BR-026, I-6 |
| AC-008 | FR-004.2, FR-005.3 | — | OD-003 a, I-4 |
| AC-009 | FR-002.1 | VR-002 | OD-002 a |
