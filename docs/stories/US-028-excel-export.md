---
id: US-028
epic: EPIC-3
title: Export a journal to Excel using a school template
slug: excel-export
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v86.
# Scope decided with the Owner on 2026-10-05 in discovery (questions 1–6);
# OD-001 and OD-002 resolved by the Owner on 2026-10-05, before activation.
---

# User Story

As a **Dean** or an **Admin**

I want to download the report a template produces for a course and a period as
an Excel file

So that the school can print it, keep it and pass it on as its paper academic
journal, without retyping what the program already shows on screen.

---

# Business Value

`trebovaniya.md` §4 Epic 3 requires "Экспорт журнала в Excel (по шаблону) и
Word" and states that viewing on screen and exporting are different actions:
"экспорт строит файл из того же отчёта". §2 gives "Просмотр и экспорт журнала
успеваемости" to both Admin and Dean. §8 keeps Excel export as a requirement and
names ClosedXML as a suitable .NET library.

US-027 built the report and kept it independent of HTML so that this Story could
turn the same report into a file. A school that has the report only on screen
still has to copy it by hand into its paper journal; the Excel file closes that
gap.

---

# Scope

## In scope

- **Export the report of US-027 to `.xlsx`**: the user chooses a template, a
  course and a period — exactly the inputs of the on-screen report — and
  downloads one Excel file. One course, one period and one template per file.
- **Any template**: the built-in "Academic journal", its copies and templates
  created in the school, in the full and the short view. The file carries what
  the on-screen report shows for the same inputs — same items, same students,
  same names, same cell marks, same grades — built from the same report, not
  computed again.
- **The layout is produced by the program** (OD-002): one worksheet per part of
  the report (for the "Academic journal": "Grading" / Оцінювання and "Lesson
  topics" / Теми занять), with the headers and columns of the on-screen report.
  No layout file is uploaded or stored.
- **Language of program text in the file** (NFR-073): sheet names, column
  headers and the built-in template's marks are program text, taken from
  translation files, in the UI language of the user who exports. Data from
  Google and text a Dean wrote into a template (template name, marks) are never
  translated.
- **File name**: built from the course name and the dates of the period; never
  contains a student's name or email. The Specification fixes the exact form
  and how characters a file name cannot hold are handled.
- **Entry point**: an "Export to Excel" action next to the on-screen report.
- **Names** (§4 Epic 3 v86, US-042): the export uses the name source chosen on
  the report page it was started from, so the file lists people exactly as the
  screen did.
- **Read-only mode** (BR-025, BR-026): export keeps working; no Google API is
  called; the only write is the audit row, which BR-026 permits.
- **Audit** (§5, SC-11): every export writes one audit row with the course id,
  the period, the template id and the number of rows — never the file's
  contents, never personal data.
- **No caching** (§8 v85, US-040): the file response is marked
  `Cache-Control: no-store` like every other response.
- **Authorization**: Admin and Dean only (§2); deny by default (SC-4); an
  allowed-role and a forbidden-role test for every endpoint (TC-5).
- **Validation** (§8, SC-10) of the template, course and period exactly as the
  on-screen report validates them; the rejected value is never logged.
- **The ClosedXML package** (OD-001) is added to the project that builds the
  file, and only there.

## Out of scope

- **Uploading a school's own Excel layout file** and filling it with report
  data — `trebovaniya.md` §3 "Excel-вёрстка (файл) подключается к шаблону при
  экспорте" and §8 "в т.ч. по существующим шаблонам школы". Deferred to a later
  Story (OD-002).
- Choosing or checking names — US-042.
- Word export — US-029; assembling several templates or courses into one printed
  journal — US-030.
- Export of the US-025 journal page without a template, and export of Meet
  statistics.
- Any change to how the report itself is built (US-027), to templates, or to
  synchronization.
- Print settings beyond what the Specification decides is needed for a readable
  printout (for example page orientation); no per-school print configuration.

---

# Acceptance Criteria

## AC-001 Export of the "Academic journal"

**Given** a signed-in Dean or Admin, a course and a period with graded work,
ungraded work and materials

**When** they export the report of the built-in "Academic journal"

**Then** they receive an `.xlsx` file with two worksheets, "Grading" and "Lesson
topics", whose cells hold exactly what the on-screen report shows for the same
template, course and period.

## AC-002 Any template, any view

**Given** a template created in the school — full view, or short view with
changed marks, hidden materials, another scale or other hours

**When** it is exported

**Then** the file reflects each of those settings exactly as the on-screen
report does.

## AC-003 Grades and marks keep their meaning

**Given** a report with converted grades, "no conversion" raw points, empty
cells and the "not assigned" dash

**Then** each cell of the file holds the same value as the on-screen report;
a grade is a number Excel can calculate with, not text.

## AC-004 Language of program text

**Given** a user whose UI language is Ukrainian or English

**Then** sheet names, column headers and the built-in template's marks in the
file are in that language; course names, student names, coursework titles and
text a Dean wrote appear as written.

## AC-005 File name

**Then** the file name is built from the course name and the period, and
contains no student's name or email; a course name with characters a file name
cannot hold still produces a valid download.

## AC-006 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** export works, no Google API is called, and the audit row is written.

## AC-007 Audit

**Given** a successful export

**Then** one audit row records the action, the actor, the course id, the
period, the template id and the number of rows — no file contents and no
personal data; viewing the report on screen writes none.

## AC-008 Only Admin and Dean

Anonymous requests are refused; every endpoint has an allowed-role and a
forbidden-role test (TC-5).

## AC-009 Invalid input

**Given** an unknown template or course, or a bad period

**Then** no file is produced, the user sees a message naming the problem, no
audit row is written, and the rejected value is not written to the log (SC-10).

## AC-010 Not cached

**Then** the file response carries `Cache-Control: no-store` (US-040).

## AC-011 Tests never reach Google

Every test builds data synthetically in PostgreSQL (TC-2, TC-4); file contents
are checked by reading the produced workbook, not by comparing bytes.

---

# Open Decisions

## OD-001 The Excel library

`AGENTS.md` requires an approved Open Decision to add a NuGet package; no
project references an Excel library today. `trebovaniya.md` §8 names ClosedXML
and EPPlus.

Options: (a) **(recommended)** ClosedXML — MIT licence, no cost; (b) EPPlus —
since version 5 a paid licence is required for commercial use.

**Resolution:** (a) ClosedXML, by the Owner on 2026-10-05 (discovery 2). The
Specification names the version and the single project that references it.

## OD-002 Who supplies the Excel layout

`trebovaniya.md` §3 says an Excel layout file is attached to a template at
export, and §8 asks to support the school's existing templates. That requires
uploading and validating a file, storing it and a way to say which cell takes
which field — a separate body of work with its own security rules.

Options: (a) **(recommended)** in US-028 the program produces the layout itself
from the report; uploading a school's layout file is a later Story; (b) US-028
includes the upload.

**Resolution:** (a), by the Owner on 2026-10-05 (discovery 1). The upload is
US-043 in `docs/catalog/stories.yaml`.

---

# Notes

- The export is a `GET` with template id, course id and dates in the query
  string, like the on-screen report — no personal data in the URL (US-039
  INFO-1).
- The file holds students' personal data and grades; it is produced in memory
  and streamed to the user, never written to disk or kept by the installation.
- The template's name and marks written by a Dean become file content; the
  Specification decides how a value that Excel would treat as a formula (a
  leading `=`, `+`, `-`, `@`) is written, so that opening the file never
  executes anything. The same applies to coursework titles and other data from
  Google.
- The report of US-027 is the single source of the file's content; if the two
  ever disagree, the on-screen report is right and the export is the defect.
