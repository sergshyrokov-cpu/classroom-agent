---
id: US-044
epic: EPIC-4
title: Export the built-in Meet reports to Excel
slug: meet-reports-export
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v88.
# Scope decided with the Owner: OD-001 … OD-003 in US-033 discovery (2026-10-10),
# OD-004 … OD-012 in US-044 discovery (2026-10-11).
---

# User Story

As a **Dean** or an **Admin**

I want to download the Meet reports I see on screen as an Excel file — the
course's reports for a period, or one meeting in detail

So that I can keep them, print them, and filter, sort or total them in Excel.

---

# Business Value

`trebovaniya.md` §2 matrix, "Просмотр и экспорт статистики Meet-занятий": both
roles may view **and export** Meet statistics. US-033 delivered the viewing;
this Story delivers the export. Selecting rows is left to Excel's own filter
(OD-004), so the screen stays as it is and no new requirement is needed.

---

# Scope

## In scope

- **Export of reports 1–3** — an "Export to Excel" button on the Meet reports
  page, shown only when reports are built. It exports exactly what the page
  shows: the same course and period (OD-005). One workbook with three sheets:
  Summary, Meetings, Student participation («Участь студентів») (OD-003 of
  US-033).
- **Export of report 4** — an "Export to Excel" button on the page of one
  meeting. It produces a workbook with one sheet: the meeting's header, one row
  per identified person with their mark and minutes, and the "other
  participants" line (count and minutes, never an address or name) (OD-006).
- **Contents equal the screen.** Rows, marks, counting rules, ordering and the
  minute cap are exactly those of US-033. The export reads the same data and
  never recomputes it differently.
- **Header block** above the tables (OD-007): course, period (report 4: the
  meeting's date and time), export date and time in the school's time zone, and
  the Meet data-delay line with the time up to which Meet meetings are loaded,
  or that they have not been loaded yet.
- **Filtering in Excel** (OD-004): each table has an autofilter on its header
  row, and the header row is frozen on screen.
- **Numbers as numbers** (OD-008): counts, minutes and the average are numeric
  cells. The summary's total duration is a number of minutes, not "h:mm" text.
  Dates and times are real date/time cells.
- **Print settings** (OD-009): a "Portrait / Landscape" choice next to each
  button. Landscape is the default because the meeting list has nine columns.
  The choice is stored nowhere. Fit to one page wide, header row repeated on
  every printed page, as in US-028.
- **File name** (OD-010): `Meet <course> <from>–<to>.xlsx` for reports 1–3 and
  `Meet <course> <date> <time>.xlsx` for report 4. The course name is cleaned by
  US-028's file-name rule. A file name never contains a person's name or email.
- **Language of the file** (OD-012): sheet names, labels, marks and the header
  block follow the user's UI language, as in the journal export. Course names,
  person names, emails and meeting codes appear as Google holds them.
- **Formula-like text** — a cell whose text starts with `=`, `+`, `-` or `@` is
  written as text and cannot become a formula (US-028 rule; US-014 I-3).
- **Audit** (SC-11, OD-011): each export writes one audit row, for reports 1–3
  and for report 4 alike. Reports 1–3: course id, period and row counts. Report 4:
  course id, meeting id and row count. Never a name, email or meeting code.
- **Read-only mode** (BR-026): export keeps working. The audit row of an export is
  one of the service writes read-only mode permits, as for the journal export.
- **Transport** as in US-028: POST with antiforgery, under `/api/v1/exports/…`.
  The file is built in memory, never stored, and sent with `no-store`. A refused
  request gets an API-6 error body.
- **Roles**: Dean and Admin (§2 matrix). Every endpoint has an allowed-role and
  a forbidden-role test (TC-5). Both v1 roles are allowed, so the forbidden case
  is the unauthenticated request.
- **Validation** (§8, SC-10): the course id, meeting id, dates and orientation
  are validated as in US-033 and US-028. A rejected value is never logged or
  echoed.
- **Translations** (NFR-073) for the buttons, the orientation choice, sheet
  names, labels and the header block.

## Out of scope

- An on-screen filter or search over report rows, or exporting a filtered subset
  (OD-004). Course list filters and search are US-020.
- Word export — with US-029.
- A school's own layout or a template for the Meet export — Epic 11.
- One sheet per meeting inside the course workbook (OD-006).
- Any change to the on-screen reports of US-033.

---

# Acceptance Criteria

## AC-001 Export button

**Given** the Meet reports page

**Then** the "Export to Excel" button and the orientation choice appear only
when reports for a course are built, and they export that course and period.

## AC-002 Course workbook

**When** a Dean or Admin exports reports 1–3

**Then** the file has three sheets — Summary, Meetings, Student participation —
whose rows equal the screen for the same course and period.

## AC-003 Meeting workbook

**When** a Dean or Admin exports from the page of one meeting

**Then** the file has one sheet with the meeting's header, one row per
identified person with their mark and "N of M" minutes, and one line for other
participants with their count and minutes, without any address or name.

## AC-004 Header block

**Then** each sheet starts with the course, the period (or the meeting's date
and time), the export date and time in the school's time zone, and the Meet
data-delay line.

## AC-005 Filtering in Excel

**Then** each table has an autofilter on its header row, and the header row is
frozen.

## AC-006 Numbers as numbers

**Then** counts, minutes and the average are numeric cells, the total duration is
in minutes, and dates and times are date/time cells.

## AC-007 Orientation

**Given** no choice, the printout is landscape. **Given** "Portrait", it is
portrait. **Given** any other value, the request is refused with a validation
error.

## AC-008 File name

**Then** the file is named `Meet <course> <from>–<to>.xlsx` or
`Meet <course> <date> <time>.xlsx`, with the course name cleaned, and a Cyrillic
name downloads correctly.

## AC-009 Formula-like text

**Given** a course or person name starting with `=`, `+`, `-` or `@`

**Then** the cell holds that text unchanged and Excel does not evaluate it.

## AC-010 Audit

**Then** each export writes one audit row with the course id and the period, or
the course id and meeting id, and the row count. It never carries a name, email
or meeting code.

## AC-011 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** both exports work and their audit rows are written.

## AC-012 Invalid input

**Given** an unknown course, a meeting not of the course, an inverted or
unparsable period, or an invalid orientation

**Then** the request is refused with the API-6 body and the matching status, and
the rejected value is not logged.

## AC-013 Roles

**Then** a Dean and an Admin can export, and an unauthenticated request is
refused.

## AC-014 Bilingual

**Then** sheet names, labels, marks and the header block are in the user's UI
language, while Google data appear unchanged.

## AC-015 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner before activation.

Carried from US-033 discovery (2026-10-10):

- **OD-001** Excel only; Word later with US-029.
- **OD-002** A built-in, fixed layout, not a school template.
- **OD-003** One workbook per course and period with the sheets Summary,
  Meetings and Student participation; report 4 exported separately from the
  meeting's page.

US-044 discovery (2026-10-11), all (a):

- **OD-004** Selecting rows — in the Excel file: an autofilter on each table's
  header row and a frozen header. No on-screen filter or search, and no new
  requirement.
- **OD-005** What is exported — exactly what the page shows (the same course and
  period). The button appears only when reports are built.
- **OD-006** Report 4 — its own button and a one-sheet file. It is not added to
  the course workbook.
- **OD-007** Header block — course, period, export date and time in the school's
  time zone, and the Meet data-delay line, as in the journal export.
- **OD-008** Durations and minutes — numeric cells; the total duration is in
  minutes.
- **OD-009** Orientation — chosen at export, landscape by default, stored
  nowhere.
- **OD-010** File name — US-028's rule with a `Meet` prefix; no person's name or
  email.
- **OD-011** Audit — one row per export, report 4 included (course id, meeting
  id, row count).
- **OD-012** Language of the file — the user's UI language; Google data
  untranslated.

---

# Notes

- Reuse US-028's export infrastructure: `IReportRenderer` / ClosedXML in
  Infrastructure, the workbook model built in Application, the file-name rule,
  formula neutralisation, and the API-6 errors under `/api/v1`. No new package is
  expected.
- The audit row for report 4 adds a meeting id to the SC-11 export fields
  (course ids, period, template id, row count). A meeting id is an internal id,
  not personal data. The Specification confirms it fits SC-11.
- Known limitations of US-033 apply unchanged (shared accounts, a non-Classroom
  link reused across courses, §7 item 28).
