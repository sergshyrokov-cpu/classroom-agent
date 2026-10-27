---
artifact_type: specification
story: US-060
version: 1
status: APPROVED
created_at: 2026-10-27T12:57:37Z
updated_at: 2026-10-27T13:12:30Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: trebovaniya.md
    version: 102
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
supersedes: null
---

# US-060 Specification — Marks in the course list, course card, journal, report and export

Delegation: two read-only lookups collected (a) the `trebovaniya.md` v99–v102 passages — changelog,
§4 Epic 1 "пропущенный курс" (lines 1155–1162), Epic 2 (1166–1176), Epic 3 marks (1276–1298), §7;
(b) code facts (§1.1). The spec writer read BR-026, BR-045…BR-049, BR-051, BR-053, BR-056, BR-057,
BR-067…BR-069 in `docs/product/business-rules.md` and checked the facts on leavers against
`CourseMembership.cs` and `RunSynchronizationUseCase.cs` lines 1015–1040.

## 1. Overview

US-058 and US-059 store what synchronization notices; nothing shows it yet. This Story shows it
where the Dean already looks — the course list, the course card, the full and short journal, the
report on screen and the report's export (both layouts) — in Ukrainian and English. Nothing is
written, nothing is read from Google, nothing new is stored.

The marks:

| Mark | Stored as | Shown in |
|---|---|---|
| Course "not in Classroom since D" | `Course.AbsenceMark = NotInClassroom`, `AbsentSince` | list and card next to the status; journal, report, export header |
| Course "not visible to the technical account since D" | `AbsenceMark = NotVisibleToTechnicalAccount`, `AbsentSince` | list and card next to the status |
| "Data not updated since R" | the course above, or a `SyncSkippedCourse` with the course's Google id; R = `Course.LastCompleteReadAt` | journal, report, export header |
| "No teacher in Classroom" | no teacher membership with `OnRoster` | course list, in place of the teachers |
| "Above maximum" | grade or draft grade > the item's maximum points | journal, report (highlight + label); export (colour + note) |
| Item "not in Classroom" | `CourseWork.NotInClassroomSince` set | journal, report, export column header; «Теми занять» next to the topic |
| Student "left L" / "left" | membership `OnRoster = false`; L = `LastSeenAt` | journal and report next to the name; export as a note on the name cell |

### 1.1 Code facts the Specification must not contradict

- `Course.AbsenceMark` (`CourseAbsenceMark?`: `NotInClassroom`, `NotVisibleToTechnicalAccount`) and
  `AbsentSince` (`DateTimeOffset?`) are null together; `LastCompleteReadAt` is `DateTimeOffset?`
  (null for a course not read completely since US-058); the status field is `State`.
- `CourseWork.NotInClassroomSince` (`DateTimeOffset?`) serves both coursework and materials (one
  entity, `Resource` tells them apart).
- `SyncSkippedCourse` has no foreign key to `Course`; it is keyed by `CourseGoogleId`. Records of
  courses absent from a list read to its end are removed (US-058 FR-007.3).
- `CourseMembership` has `FirstSeenAt`, `LastSeenAt`, `OnRoster`, `Role`. A student found only
  through submissions is created by `FirstSeen(…, observedAt)` then `NotOnRoster()` — first and
  last seen equal, off the roster. **No stored field tells such a student from one seen on exactly
  one roster read and gone since** (see OD-003).
- Course list: `CourseListRow` carries no mark; teachers are memberships with `Role = Teacher` and
  `OnRoster` (`CourseListSource.cs:53`). Card: `CourseCardDetails` carries no mark; leavers
  (`!OnRoster`) are listed with their last-seen date (`Card.cshtml:127-152`).
- Journal: `JournalCell` has `Points`, `MaxPoints`, `DraftPoints`; `JournalColumn` has `MaxPoints`
  (graded work only); the full journal shows drafts, the short one does not (BR-057). The journal
  page has **no course heading** — the course appears only in the course selector.
- Report: `ReportHeader` (template, course, section, period, teachers) has no mark; `ReportGrade`
  has `Points` and `MaxPoints`; drafts appear (`ReportCell.DraftGrade`); the «Теми занять» part
  lists non-material lessons. With a scale (`ScaleMode = Ranges`, max > 0) points ≥ max give 100.
- Export: `ExportJournalCommand` builds the report through `ReportBuilder`; layouts `Program`
  (`ReportWorkbookMapper` → `ClosedXmlReportRenderer`) and `GroupLessonsBlank`
  (`GroupLessonsBlankMapper` → `ClosedXmlBlankFormRenderer`). The workbook model
  (`WorkbookCell`) has no fill colour and no note; no ClosedXML comment is used anywhere. The
  program header block is Template, Course, Period, Teachers (label cell + value cell each) and an
  empty row, the table at row index 5. The blank form has a `{{course}}` cell, a
  `{{student.name}}` cell per row, `{{grade}}` cells, and a `{{topic.title}}` cell on the topics
  sheet; its grading sheet has no item-title cell, only `{{lesson.date}}`.
- Texts: `SharedResource.uk.resx` / `.en.resx`, dotted keys, constants in `Web/Security/*TextKeys.cs`;
  exports through `IReportTexts`; the user's language is `CultureInfo.CurrentUICulture`.
- Read-only mode: queries and the export do not call `IReadOnlyModeGuard`; the export's only write
  is its audit event, declared as `PermittedServiceWrite.AuditEvent`.
- Time zone: `SchoolTimeZone`; each query converts with `TimeZoneInfo.ConvertTime`.

## 2. Business Goal

A grade of 15 out of 12 prints as 100 %, a deleted task keeps its column without a word, a deleted
course looks live, a student who left looks present, a course whose teacher's account was deleted
shows an empty teacher column. The Dean controls the teaching process (v99) and must see each of
these where they already look, worded with restraint (BR-047: colour and a label, no banners).

## 3. Business Flow

1. Synchronization (unchanged) stores marks, sightings and skip records.
2. The Dean or Admin opens a page or exports a report. The query reads stored data only and adds
   the marks to the DTOs it already returns.
3. The page renders each mark as a translated label next to what it qualifies; the export writes
   the same texts, and for the grade and name cells a colour and/or a note instead of changing the
   printed value.

Admin and Dean see the same (BR-002). Read-only mode changes nothing here (BR-026).

## 4. Functional Requirements

Dates in marks are calendar dates in the school's time zone (BR-053), formatted with the user's
culture short date pattern as dates elsewhere on the same page or sheet.

### FR-001 Course mark in the course list and course card

1. A course with `AbsenceMark = NotInClassroom` shows, next to its status, the mark
   "not in Classroom since <AbsentSince>"; with `NotVisibleToTechnicalAccount` — "not visible to
   the technical account since <AbsentSince>". The status shown is the stored `State`, unchanged.
2. Both in the course list row and on the course card.
3. No new filter, sort or column; the existing filters and paging are unchanged.
4. A course with no mark shows only its status.

### FR-002 "No teacher in Classroom" in the course list

1. A course list row whose course has no membership with `Role = Teacher` and `OnRoster = true`
   shows "no teacher in Classroom" in place of the teacher names — whether its teachers left or it
   never had one on the roster (BR-069).
2. The course card is unchanged: it already lists the teachers who left.
3. The teacher filter is unchanged (it lists teachers on a roster now).

### FR-003 Course header line in the journal and report

1. For the selected course, the journal page and the report show one header line, chosen in this
   order:
   1. `AbsenceMark = NotInClassroom` → "Course not in Classroom since <AbsentSince>";
   2. `AbsenceMark = NotVisibleToTechnicalAccount`, **or** a `SyncSkippedCourse` exists whose
      `CourseGoogleId` equals the course's Google id → "Data not updated since
      <LastCompleteReadAt>"; when `LastCompleteReadAt` is null → "Data not updated" with no date
      (OD-002 a);
   3. otherwise no line.
2. The journal page has no course heading today; the line is placed above the journal table (both
   the full and the short journal). The report shows it in its header (`#report-header`).
3. An unchanged archived course that synchronization does not re-read is not "skipped" (no
   `SyncSkippedCourse`) and gets no line (Story Notes).
4. Grades, rows and columns are shown exactly as before.

### FR-004 Above-maximum grades on screen

1. A grade or a draft grade strictly greater than its item's maximum points (both present,
   maximum > 0) is "above maximum".
2. In the journal (full and short) and in the report, a cell showing an above-maximum grade is
   highlighted and carries the label "above maximum". A draft above maximum is highlighted and
   labelled only where drafts are shown (the full journal, the report — BR-057); the short journal
   ignores drafts.
3. The value shown is unchanged: the journal shows raw points; the report shows its grade as today
   (with a scale, that of 100 %, v98). Nothing is clamped or rewritten.
4. A grade equal to the maximum is ordinary.
5. The highlight is not conveyed by colour alone: the label is always present.

### FR-005 Item "not in Classroom" keeps its column with a mark

1. An item (coursework or material) with `NotInClassroomSince` set keeps its column and all its
   cells in the journal, report and export.
2. Its column header carries the mark "not in Classroom" (no date — BR-045, BR-067 name none) in
   the journal, the report and the program-layout export.
3. On the «Теми занять» part of the report and on the «Теми занять» sheet of the program-layout
   export, the mark follows the item's topic.
4. An item without the mark has no mark.

### FR-006 Student "left"

1. In the journal and the report on screen, a student row whose membership in the course has
   `OnRoster = false` shows next to the name "left <LastSeenAt>"; a student known only through
   submissions shows "left" with no date (BR-068). An off-roster membership whose `FirstSeenAt`
   equals its `LastSeenAt` counts as "known only through submissions" (OD-003 a).
2. A student on the roster shows no mark. The mark is current state: back on the roster, no mark.
3. Rows of other kinds (`JournalNameKind` other than a person with a membership) are unchanged.
4. The course card's "Left the course" section is unchanged (out of scope, §10).

### FR-007 Program-layout export

1. **Course header line** (FR-003): written as text in the third cell of the Course header row
   (after the label and the course text). The table does not move (row index 5).
2. **Item mark** (FR-005): the item's column header cell holds its title, a line break, then
   "not in Classroom"; on the «Теми занять» sheet the topic cell likewise.
3. **Above maximum** (FR-004): the cell keeps exactly the value it holds today (the scale value,
   or the text when the cell combines grade, draft and dates), gets the above-maximum fill colour,
   and gets a note listing each above-maximum value it shows — "Points: <points> of <max>" for the
   grade, "Draft: <points> of <max>" for a draft. Cells without an above-maximum value have
   neither colour nor note.
4. **Left** (FR-006): the student name cell keeps the name unchanged and gets a note "left
   <date>" or "left".
5. The workbook model gains a cell fill colour and a cell note; the renderer writes them as a
   ClosedXML fill and comment. The fill colour is one light colour, the same in both layouts.

### FR-008 Blank-form export («Бланк для групових занять», OD-001 a)

1. **Above maximum**: the `{{grade}}` cell keeps its value, gets the same fill colour and note as
   FR-007.3.
2. **Left**: the `{{student.name}}` cell keeps the name and gets the note of FR-007.4.
3. **Course header line** (FR-003): a note on the `{{course}}` cell with the line's text; the cell's
   value is unchanged.
4. **Item mark**: a note "not in Classroom" on the item's `{{topic.title}}` cell on the topics
   sheet; the grading sheet has no item-title cell and gets no item mark.
5. The form's printed text is unchanged — a note is not printed (OD-001 a). Every copy of a
   multi-copy fill carries the notes of its own cells.

### FR-009 Texts

1. Every mark, label and note comes from `SharedResource.uk.resx` and `.en.resx` (NFR-073):

   | Meaning | uk (proposed) | en |
   |---|---|---|
   | course list / card, not in Classroom | «немає в Classroom з {0}» | "not in Classroom since {0}" |
   | course list / card, not visible | «не видно технічному акаунту з {0}» | "not visible to the technical account since {0}" |
   | course list, no teacher | «викладача немає в Classroom» | "no teacher in Classroom" |
   | header, course not in Classroom | «Курсу немає в Classroom з {0}» | "Course not in Classroom since {0}" |
   | header, not updated | «Дані не оновлювалися з {0}» | "Data not updated since {0}" |
   | header, not updated, no date | «Дані не оновлювалися» | "Data not updated" |
   | item | «немає в Classroom» | "not in Classroom" |
   | above maximum | «більше максимуму» | "above maximum" |
   | note, grade | «Бали: {0} з {1}» | "Points: {0} of {1}" |
   | note, draft | «Чернетка: {0} з {1}» | "Draft: {0} of {1}" |
   | left, with date | «вибув(-ла) {0}» | "left {0}" |
   | left, no date | «вибув(-ла)» | "left" |

   The requirements word the marks in Russian; the Ukrainian texts above render the same meaning
   and are the ones the tests pin. «вибув(-ла)» is gender-neutral because the system stores no
   gender (Owner at HUMAN_SPEC_APPROVAL, review m-2).
2. Course, item and person names, and a Dean's template text, are never translated.
3. The language is the current user's (on screen) and `CultureInfo.CurrentUICulture` (export), as
   today.

## 5. Acceptance Criteria

From the Story, unchanged in meaning; derived criteria are marked.

| AC | Given / When / Then (summary) | FR |
|---|---|---|
| AC-001 | Courses marked NotInClassroom since D1, NotVisible since D2, unmarked. List and card show «… з D1», «не видно технічному акаунту з D2» next to the status; the third only its status. | FR-001 |
| AC-002 | A course whose only teacher is off the roster, and one with a teacher on it. The list shows "no teacher in Classroom" for the first, the teacher's name for the second. | FR-002 |
| AC-003 | Max 12; grade 15, draft 13, grade 12. Full journal and report: the first two highlighted and labelled "above maximum" (the draft only where drafts are shown), report scale value 100 %; the third ordinary. Short journal: the grade 15 only. | FR-004 |
| AC-004 | Data of AC-003, export. The grade-15 cell holds the scale value, has the fill colour and the note "Points: 15 of 12"; the grade-12 cell has neither. In both layouts. | FR-007.3, FR-008.1 |
| AC-005 | A graded item marked not in Classroom, with grades, and an ordinary item. Journal, report and program export: the marked column present with grades and "not in Classroom" in its header; on «Теми занять» next to its topic; blank form: a note on its topic title. The ordinary item unmarked. | FR-005, FR-007.2, FR-008.4 |
| AC-006 | A course NotInClassroom since D1 with grades. Journal, report and export headers: "Course not in Classroom since D1"; grades as before. | FR-003, FR-007.1, FR-008.3 |
| AC-007 | A course NotVisible, last read R1; a loaded course with a skip record, last read R2. Headers "Data not updated since R1" / "… R2"; grades as before. | FR-003, FR-007.1, FR-008.3 |
| AC-008 | A student off the roster last seen L; a student known only through submissions; a student on the roster. Screen: "left L", "left", nothing. Export: names unchanged; first two name cells carry the notes. | FR-006, FR-007.4, FR-008.2 |
| AC-009 | Data of AC-001…AC-008, a user with English. Every mark, label and note in English; names as Google returned them. | FR-009 |
| AC-010 | Read-only mode, data of AC-001. Course list and an export of a marked course work and show the marks. | §7 |
| AC-011 *(derived, OD-002 a)* | A skipped course with no `LastCompleteReadAt`. Header "Data not updated" with no date. | FR-003.1 |
| AC-012 *(derived)* | An unchanged archived course with no skip record. No header line. A course both NotInClassroom and with a skip record shows only "Course not in Classroom since …". | FR-003.1, FR-003.3 |

## 6. Validation Rules

No new input: no endpoint, query parameter, form or upload changes.

- **VR-001 Above maximum:** `points > maxPoints` with both non-null and `maxPoints > 0`; equality
  is not above. Ungraded work and materials have no maximum and are never above.
- **VR-002 Course header precedence:** FR-003.1 order; exactly one line or none.
- **VR-003 Skip record match:** ordinal equality of `SyncSkippedCourse.CourseGoogleId` and the
  course's Google id (opaque ids, no case folding — as US-059 VR-001).
- **VR-004 Dates:** UTC instants converted to the school's time zone, then shown as dates.
- **VR-005 Leaver:** a membership with `OnRoster = false`; undated when `FirstSeenAt = LastSeenAt`, else dated (OD-003 a).

## 7. Security Requirements

- No endpoint, page, role, policy or permission changes. Admin and Dean see the same (BR-002).
- **Read-only mode:** viewing and export keep working and show the marks (BR-026). This Story adds
  no write and no `IReadOnlyModeGuard` call; the export's audit write stays the only one.
- **Google:** no call — every mark is built from stored data (SC-7; TC-4 for tests).
- **Personal data:** the notes add a student's last-seen date and raw points next to data the
  export already holds; no email, no Google id, no new field leaves the system. The export is
  already personal data and is handled as today. No log line gains a name, grade or date (SC-10).
- **Audit:** no new event; the export's existing event is unchanged (SC-11).
- **Outbound:** no new destination (SC-13).

## 8. Error Handling

- No new error. A course with a mark, a skip record or a missing `LastCompleteReadAt` is a normal
  result (AD-9).
- A mark's text missing from a translation file is a defect caught by tests, not a runtime
  fallback.
- The export's error contract is unchanged.

## 9. Non-Functional Requirements

- **Persistence:** no schema change (OD-003 a); DB_DESIGN expected NOT_APPLICABLE.
- **API:** no endpoint change; API_DESIGN expected NOT_APPLICABLE.
- **Cost:** the course list reads the mark with the rows it already reads; the header needs one
  lookup of the course's skip record; no N+1 per row.
- **Tone:** restrained — a light fill and a label, no icon, blinking, pop-up or banner (BR-047,
  Owner 2026-10-23).
- **Architecture:** marks are computed in `Application` and travel in DTOs (AD-8); views render
  only (AD-3).
- **Tests:** synthetic data, PostgreSQL via Testcontainers, no Google call (TC-2, TC-4).

## 10. Out of Scope

- The banner, «Стан даних», notices, the Admin's "deleted in Classroom" confirmation (US-062).
- Meet reports and Meet code linking (US-061).
- Any change to synchronization or to the stored marks (US-058, US-059).
- A filter or sort by mark in the course list (v100, v102).
- A course never loaded — it has no journal (US-062).
- The course card's "Left the course" section, which already lists leavers with their last-seen
  date; it shows a submission-only student with the date of their first sighting, as today.
- A Word export (none exists).

## 11. Open Decisions and Interpretations

### Open Decisions

- **OD-001** (from the Story) — marks in the blank form: **resolved a)** 2026-10-27. FR-008.
- **OD-002** (from the Story) — no last successful read: **resolved a)** 2026-10-27. FR-003.1.
- **OD-003 — telling a submission-only student from a leaver** (new): **resolved a)** 2026-10-27
  at HUMAN_SPEC_APPROVAL. FR-006.1, VR-005. The stored data cannot tell a student found only through submissions from one seen on
  exactly one roster read and gone since: both are off the roster with first and last seen equal
  (§1.1). Options in `docs/decisions/US-060-open-decisions.md`.

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 Journal header placement.** The journal page has no course heading; the line goes above the
  table (FR-003.2).
- **I-2 Header precedence.** A course both "not in Classroom" and skipped shows only the
  not-in-Classroom line; "not visible" and "skipped" share one line (FR-003.1).
- **I-3 Program-export placement.** The header line goes to the third cell of the Course row and
  the item mark into the header cell after a line break, so the table does not move (FR-007).
- **I-4 Blank form uses notes for the header and item marks.** OD-001 a) keeps the printed look; a
  note on the `{{course}}` and `{{topic.title}}` cells carries the marks without printing them
  (FR-008.3, FR-008.4).
- **I-5 The item mark has no date** — BR-045 and BR-067 name none; the date stays in the data for
  «Стан даних» (US-062).
- **I-6 Ukrainian wording** — the table in FR-009 translates the Russian wording of the
  requirements.

## 12. Traceability

| AC | FR | VR | Source |
|---|---|---|---|
| AC-001 | FR-001 | VR-004 | Epic 2 v100/v102, BR-048, BR-049 |
| AC-002 | FR-002 | — | Epic 2 v101, BR-069 |
| AC-003 | FR-004 | VR-001 | Epic 3 v99, BR-067, BR-057 |
| AC-004 | FR-007.3, FR-008.1 | VR-001 | BR-067, OD-001 a |
| AC-005 | FR-005, FR-007.2, FR-008.4 | — | BR-045, BR-067, I-4, I-5 |
| AC-006 | FR-003, FR-007.1, FR-008.3 | VR-002, VR-004 | BR-048, I-1, I-3 |
| AC-007 | FR-003, FR-007.1, FR-008.3 | VR-002, VR-003 | BR-046, BR-049 |
| AC-008 | FR-006, FR-007.4, FR-008.2 | VR-005 | BR-068, BR-051, OD-003 |
| AC-009 | FR-009 | — | NFR-073 |
| AC-010 | §7 | — | BR-026 |
| AC-011 | FR-003.1 | VR-004 | OD-002 a |
| AC-012 | FR-003.1, FR-003.3 | VR-002 | Story Notes, I-2 |
