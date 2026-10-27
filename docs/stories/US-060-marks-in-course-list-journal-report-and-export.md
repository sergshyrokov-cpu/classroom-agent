---
id: US-060
epic: EPIC-3
title: Marks in the course list, course card, journal, report and export
slug: marks-in-course-list-journal-report-and-export
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-27 from trebovaniya.md v102 (`33174fe`,
# derived rules in `236832a`). Second of the v99 Stories ("Story 2": marks on the
# screens and in the export); it carries the display parts of v100 (course "not in
# Classroom"), v101 ("left", "no teacher in Classroom") and v102 ("not visible to
# the technical account" everywhere). The data exists since US-058 and US-059.
# Before the pilot (Owner 2026-10-26).
---

# User Story

As a **Dean**

I want the course list, the course card, the journal, the report and its export
to show what synchronization has noticed — points above the maximum, items and
courses no longer in Classroom, courses not updated, students who left, courses
without a teacher

So that I catch a teacher's entry error or a gap in the data where I already
look, instead of trusting figures that only look normal.

---

# Business Value

US-058 and US-059 store the facts; today nothing shows them. A grade of 15 out of
12 prints as 100 % in the school's scale, a deleted task keeps its column without
a word, a deleted course looks live, a student who left looks present, and a
course whose teacher's account was deleted shows an empty teacher column. The Dean
controls the teaching process (v99) — each of these is something they must see.

---

# Scope

**In scope — course list and course card (Epic 2):**

- a course marked "not in Classroom" (BR-048) shows «нет в Classroom с <даты>» next
  to its status in the course list and the course card; a course marked "not
  visible to the technical account" (BR-049, v102) shows «не виден техническому
  аккаунту с <даты>» there; the status shown is the last one stored; no new filter;
- a course with no teacher on its roster now shows «преподавателя нет в Classroom»
  in place of its teachers in the course list (BR-069); the course card already
  lists the teachers who left.

**In scope — journal and report on screen (Epic 3):**

- a grade or draft grade above the item's maximum points is highlighted and
  labelled «больше максимума» (BR-067); the school's scale value stays 100 % (v98);
- an item "not in Classroom" (BR-045) keeps its column and grades, with the mark in
  its column header (and next to its topic on the «Теми занять» sheet of the
  report);
- the header of a course "not in Classroom" says «Курса нет в Classroom с <даты>»
  (BR-048); of a course "not visible to the technical account" or skipped by
  synchronization (BR-046, BR-049) — «данные не обновлялись с <даты последнего
  успешного чтения курса>»;
- a student off the course's roster now shows «выбыл <дата «последний раз
  замечен»>» next to the name; one known only through submissions — «выбыл» with no
  date (BR-068);

**In scope — export of the report (Epic 3):**

- the above-maximum cell keeps the scale value, is coloured, and carries a note
  with the raw points and the maximum (BR-067);
- the "not in Classroom" item and the course header marks appear as on screen;
- the student name stays as is and the name cell carries a note with the «выбыл»
  mark (BR-068);
- every mark and note text comes from the translation files, Ukrainian and English
  (NFR-073); Google's data and template text are not translated.

**In scope — common:** marks are read from stored data only (no Google call), so
they work in read-only mode as all viewing does (BR-026); tests with synthetic data
(TC-4).

**Out of scope:**

- the banner and the «Стан даних» page, the Admin's confirmation (US-062);
- Meet reports and Meet code linking (US-061);
- any change to synchronization or to the stored marks (US-058, US-059);
- a filter by mark in the course list (v100, v102: none);
- a course never loaded (it has no journal; it is shown on «Стан даних», US-062).

---

# Acceptance Criteria

## AC-001 Course list and card show a course's mark

**Given** three courses: one marked "not in Classroom" since date D1, one marked
"not visible to the technical account" since date D2, one with no mark

**When** the Dean opens the course list and each course's card

**Then** the first shows «нет в Classroom с D1» and the second «не виден
техническому аккаунту с D2» next to the last stored status, in the list and on the
card; the third shows only its status.

## AC-002 A course without a teacher on the roster says so

**Given** a course whose only teacher is no longer on the roster, and a course with
a teacher on the roster

**When** the Dean opens the course list

**Then** the first course shows «преподавателя нет в Classroom» in place of its
teachers, the second its teacher's name.

## AC-003 Above-maximum points are highlighted on screen

**Given** an item with maximum 12, one student's grade 15 and another's draft grade
13, and a third student's grade 12

**When** the Dean opens the full journal and a report of the course

**Then** the first two cells are highlighted and labelled «больше максимума» (the
draft only where drafts are shown), the report's scale value for them is that of
100 %, and the third cell is ordinary.

## AC-004 Above-maximum points in the export

**Given** the data of AC-003

**When** the Dean exports the report

**Then** the cell holds the scale value, is coloured, and carries a note with the
raw points and the maximum; the third cell has neither colour nor note.

## AC-005 An item "not in Classroom" keeps its column with a mark

**Given** a graded item marked "not in Classroom" with grades, and an ordinary item

**When** the Dean opens the journal, the report, and exports the report

**Then** the marked item's column is present with its grades and «нет в Classroom»
in its header everywhere, and next to its topic on «Теми занять»; the ordinary item
has no mark.

## AC-006 A course "not in Classroom" is marked in the header

**Given** a course marked "not in Classroom" since D1, with grades

**When** the Dean opens its journal and report and exports the report

**Then** each header says «Курса нет в Classroom с D1» and the grades are shown as
before.

## AC-007 A course "not visible" or skipped says since when it is not updated

**Given** a course marked "not visible to the technical account" last read
successfully at R1, and a loaded course skipped by the latest runs, last read
successfully at R2

**When** the Dean opens their journals and reports and exports the reports

**Then** each header says «данные не обновлялись с R1» (resp. R2), and the grades
are shown as before.

## AC-008 A student who left is marked

**Given** a student off the roster, last seen on L; a student known only through
submissions and never seen on the roster; a student on the roster

**When** the Dean opens the journal and report, and exports the report

**Then** on screen the first shows «выбыл L», the second «выбыл», the third
nothing; in the export the names are unchanged and the first two name cells carry
a note with the same mark.

## AC-009 Marks follow the user's language

**Given** the data of AC-001 … AC-008

**When** a user with English chosen opens the same pages and exports

**Then** every mark, label and note is in English, and course, item and person
names are as Google returned them.

## AC-010 Marks are visible in read-only mode

**Given** the installation is in read-only mode and the data of AC-001

**When** the Dean opens the course list and exports a report of a marked course

**Then** both work and show the marks.

---

# Open Decisions

All resolved by the Owner on 2026-10-27 before activation (option a) each).

## OD-001 Marks in the «Бланк для групових занять» layout

The export has two layouts: the program's own workbook and the shipped form
(US-043). The requirements say "in the export" without naming the layout.

- **a) (recommended)** — the same marks wherever the form has the cell: colour and
  note on an above-maximum grade cell, note on a «выбыл» name cell; the header and
  item marks only if the form has a place for them, otherwise not. The form's
  printed look stays unchanged — a note is not printed.
- b) — program layout only; the form stays without marks.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-002 "Not updated since" when the last successful read is not known

The date of the last successful read (`Course.LastCompleteReadAt`) exists only
since US-058; a course not read completely since then — an unchanged archived
course, a course skipped ever since — has none.

- **a) (recommended)** — show «данные не обновлялись» without a date, as v101 does
  for «выбыл» when the date is unknown.
- b) — show the date the skip streak began (`SyncSkippedCourse.SkippedSince`) or
  the mark's date — it is a later date than the real one, so it understates how
  stale the data is.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

---

# Notes

- **Data already stored:** `Course.AbsenceMark` / `AbsentSince` / `LastCompleteReadAt`
  (US-058, US-059), `CourseWork.NotInClassroomSince` (US-058), `SyncSkippedCourse`
  (US-058), `CourseMembership.OnRoster` / `LastSeenAt` (BR-051). No schema change is
  expected — DB_DESIGN likely NOT_APPLICABLE; API_DESIGN applies only if the
  export's error contract changes (it should not).
- **Code today:** course list `GetCourseListQuery` / `CourseListSource` (teachers
  filtered by `OnRoster`, ~23, ~53), card `GetCourseCardQuery`, journal
  `GetJournalQuery` (`JournalRow` has no leaver flag), report `GetReportQuery` /
  `ReportBuilder`, export `ExportJournalCommand` → `ReportWorkbookMapper` /
  `GroupLessonsBlankMapper` → `ClosedXmlReportRenderer` / `ClosedXmlBlankFormRenderer`.
  There is no Word export in the code; nothing here adds one.
- **"Skipped" for the header** means a course with a `SyncSkippedCourse` record
  (either reason). An unchanged archived course is not skipped and gets no header
  line.
- **Restrained tone** (Owner 2026-10-23, BR-047): colour highlight and a label, no
  emphatic banners.
- Read-only mode: nothing written, no new service write (BR-026).
- US-059 follow-up m-2 (more negative-value sync tests) is synchronization, not
  display; it stays in `project_us059_followups`.
- No new NuGet package, no new requirements version.
