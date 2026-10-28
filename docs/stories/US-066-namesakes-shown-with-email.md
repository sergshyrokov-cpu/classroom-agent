---
id: US-066
epic: EPIC-3
title: Students with the same name are shown with their email in the journal, the report and the export
slug: namesakes-shown-with-email
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-28 from trebovaniya.md v104 (`578281e`,
# §4 Epic 3 «Ученики с одинаковым именем»; derived BR-086 in `154a031`). Item 4 of
# the v104 agenda.
---

# User Story

As a **Dean**

I want two students who show the same name to be told apart in the journal and
the report

So that I put a grade, a remark or a signature against the right person — on
screen and on the printed journal — without opening the course card to compare
emails.

---

# Business Value

Two students named «Петренко Іван» in one course are two identical rows today: the
journal screen shows the Google full name only (`JournalCellRule.Label`), and the
report and its Excel export show the profile name «Прізвище Ім'я»
(`ReportPersonNameRule`). Only the email switcher on the report page or the course
card tells them apart. v104 (BR-086) adds the email to exactly those rows, so the
paper journal keeps its look for everyone else.

---

# Scope

**In scope:**

- the **journal page** (US-025): rows of one journal whose displayed name
  coincides show the email next to the name;
- the **report page** and everything built from the same report (`ReportBuilder`):
  the Excel export of the report by template and the blanks built from it — the
  email goes into the same cell as the name;
- "coincides" is judged among the student rows of that one journal or report,
  after the name variant chosen for it (profile or email, the page switcher
  included), with the comparison the rows are already ordered by (the
  Ukrainian-aware comparer, `ReportBuilder`);
- rows whose name does not coincide with another's are unchanged, on screen and in
  the file;
- the existing marks next to a name (for example «выбыл <дата>», BR-068, and its
  cell note in the export) keep working beside the email;
- texts, if any new label is needed, in Ukrainian and English (NFR-073); the email
  itself is data and is never translated.

**Out of scope:**

- teachers («ПІБ Викладача») — v104 speaks of students only; see the notes;
- Meet reports and the course card — they already show the email;
- rows with no name at all («без імені») — they are not "the same name"; shown as
  today;
- changing the order of the rows (by displayed name, then as today).

---

# Acceptance Criteria

## AC-001 Namesakes in the journal page

**Given** a course with two students whose Google full name is «Іван Петренко» and
a third named «Олена Коваль»

**When** a Dean opens the journal

**Then** both «Іван Петренко» rows show their email next to the name, and the
«Олена Коваль» row shows the name only.

## AC-002 Namesakes in the report page

**Given** the same course, a report with names from the profile

**When** a Dean opens the report

**Then** both «Петренко Іван» rows show their email next to the name; the other row
does not.

## AC-003 Namesakes in the Excel export

**Given** the situation of AC-002

**When** the Dean exports the report

**Then** in the file the two namesakes' name cells hold the name and the email, the
other name cell holds the name only, and the layout of the sheet is otherwise
unchanged.

## AC-004 Blank built from the report

**Given** the situation of AC-002

**When** the Dean exports a blank built from that report

**Then** the namesakes' name cells hold the name and the email as in AC-003.

## AC-005 Names that differ only in letter case count as the same

**Given** two students «Петренко Іван» and «петренко іван»

**When** the report is built

**Then** both rows show their email.

## AC-006 No namesakes, no change

**Given** a course where every student's displayed name is different

**When** the journal, the report and the export are built

**Then** no row shows an email, and the file is the same as before this Story.

## AC-007 The variant decides

**Given** two students with the same profile name and different emails

**When** the Dean switches the report to names from the email

**Then** no row shows an additional email (the names no longer coincide); switching
back shows it again.

## AC-008 A namesake without an email

**Given** two students with the same name, one of whom has no email in Google

**When** the report is built

**Then** the one with an email shows it; the one without is shown with the name
only.

## AC-009 «Выбыл» still shown

**Given** two namesakes, one of whom left the course

**When** the report is shown and exported

**Then** the leaver's row shows both the email and «выбыл <дата>» on screen, and
in the file the email is in the cell and «выбыл» stays in the cell note.

---

# Open Decisions

Resolved by the Owner on 2026-10-28 before activation.

## OD-001 How the email is written next to the name

v104 says "next to the name, in the same cell".

- **a) (recommended)** — in parentheses after the name on the same line:
  «Петренко Іван (ivan.petrenko@school.example)». One line keeps the printed row
  height; it reads the same on screen and on paper.
- b) — on a second line of the same cell: easier to read, but the printed row grows
  taller for those students only.

**Resolved 2026-10-28 by the Owner: a)** («1a»).

## OD-002 Teachers with the same name

The report also lists the course's teachers («ПІБ Викладача»).

- **a) (recommended)** — students only, as v104 says; two teachers of one course
  with the same name are rare, and a teacher's line is not graded.
- b) — teachers too, by the same rule — needs a one-line amendment of
  `trebovaniya.md` first.

**Resolved 2026-10-28 by the Owner: a)** — after an explanation: teachers are one
header line «ПІБ Викладача» without grades, so a repeated name there misleads no
one.

---

# Notes

- **Code today:** journal page — `GetJournalQuery`, row label in
  `JournalCellRule.Label` (`JournalCellRule.cs` ~106), row DTO `JournalRow`
  (`DisplayName`, `NameKind`, no email). Report, export and blanks — one builder,
  `ReportBuilder.cs` (students ~158, teachers ~98) with `ReportPersonNameRule`;
  the export maps the report through `ReportWorkbookMapper`, blanks through
  `IBlankFormRenderer` (`ExportJournalCommand.cs` ~128).
- With names "from the email" the report shows the part of the address before `@`;
  two people from different domains could share it — the rule then adds the full
  email, which is what AC-007's general rule gives anyway.
- Expected stages: SPECIFICATION, API_DESIGN (the journal and report DTOs gain an
  optional email for those rows — check the OpenAPI of US-025/US-042),
  DB_DESIGN likely NOT_APPLICABLE (the email is already read for the rows),
  TEST_WRITING, IMPLEMENTATION, verification, security review (an email now in the
  journal page — personal data already shown on the course card; SC-14 no-store
  applies to the page as today).
- No new NuGet package, no new requirements version.
