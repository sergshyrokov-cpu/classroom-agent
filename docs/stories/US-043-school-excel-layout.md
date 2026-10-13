---
id: US-043
epic: EPIC-3
title: Fill a school's own Excel layout file with a template report
slug: school-excel-layout
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v91.
# Split out of US-028 by its OD-002 (resolved by the Owner on 2026-10-05).
# OD-001 … OD-005 resolved by the Owner on 2026-10-13, before activation.
---

# User Story

As a **Dean** or an **Admin**

I want to attach the school's own Excel journal file to a report template and
get the export filled into that file

So that the exported journal looks exactly like the paper journal the school
already prints, without copying the program's output into the school's file by
hand.

---

# Business Value

`trebovaniya.md` §3 (`ReportTemplate`, v84): "Excel-вёрстка (файл) подключается
к шаблону при экспорте". §8: "сохранить поддержку Excel (в т.ч. по существующим
шаблонам школы)"; templates are "конфигурируемый ресурс, а не хардкод в коде (как
было в прототипе)". §2 gives creating and editing report templates to both Admin
and Dean.

US-028 exports a report into a layout the program produces itself; a school with
its own journal form — headers, column widths, borders, signatures, merged title
cells — still has to move the data across by hand. The Python prototype filled
one fixed file (`docs/product/report-templates/school-journal-full.xlsx`) at
hard-coded cell positions; that is the defect §8 names, and this Story replaces
it with a layout file any school can supply.

---

# Scope

## In scope

- **Attach a layout file to a template** (OD-001, OD-003): on a template's
  page, a Dean or Admin uploads an Excel file as that template's layout,
  replaces it with another file, or removes it. One layout per template.
- **Where the data goes** (OD-002): the school marks in its own file which cell
  receives which field of the report; the program fills those places and leaves
  every other cell, format, width, border, merge and print setting of the file
  as the school made it.
- **Fields the layout can take** — the fields of the report (US-027), not tables
  of the Google mirror (§3): course name, period, the course's teachers, the
  student list (row number and name), the lesson dates as column headers, the
  grade cells, and the lesson-topics table (row number, date, hours, topic,
  teacher). The Specification fixes the exact list.
- **Upload checks** (OD-004): the file is accepted only if it is an Excel
  workbook of the allowed type and size, opens as a workbook, and marks at least
  one field; a file that fails any check is refused with a message naming the
  problem, and nothing is stored.
- **Export with the layout**: when the chosen template has a layout, the US-028
  export fills that layout; without one, it produces the program's own layout
  exactly as today. The cells carry exactly what the on-screen report shows for
  the same template, course, period and name source — built from the same
  report, not computed again.
- **Rows and columns grow with the data**: a course with more students or more
  lessons than the layout has rows or columns still exports completely; what
  the school placed below or to the right of the filled area (for example
  signature lines) is kept, not overwritten.
- **Language**: the school's own text in the layout stays as written. Values the
  program writes — marks of the built-in template, the "not assigned" dash —
  follow US-028 (NFR-073).
- **Read-only mode** (BR-025, BR-026): uploading, replacing and removing a
  layout are refused in `Application`; export with an already attached layout
  keeps working, with the export audit row as its only write.
- **Audit** (§5, SC-11): attaching, replacing and removing a layout is a change
  of the template and writes an audit row with the template id only — never the
  file or its name. The export audit row is unchanged from US-028.
- **Authorization**: Admin and Dean only (§2); deny by default (SC-4); an
  allowed-role and a forbidden-role test for every endpoint (TC-5).
- **No caching** (US-040): every response, including a downloaded layout if the
  Specification adds one, carries `Cache-Control: no-store`.

## Out of scope

- Word layouts — US-029; assembling several templates or courses into one file —
  US-030.
- AI help with mapping fields of an uploaded file (§4, a later edition).
- Any change to how the report is built (US-027), to names (US-042) or to the
  program's own layout (US-028).
- Layouts for the Meet reports (US-044).
- Formulas the program writes: the program writes values only; formulas the
  school put in its file stay as they are.

---

# Acceptance Criteria

## AC-001 Attach a layout

**Given** a signed-in Dean or Admin and a template created in the school

**When** they upload a valid layout file to it

**Then** the template shows that it has a layout, and an audit row records the
change with the template id only.

## AC-002 Export fills the school's file

**Given** a template with a layout, a course and a period with graded work,
ungraded work and materials

**When** the report is exported to Excel

**Then** the file is the school's layout with the marked places filled; every
filled cell holds what the on-screen report shows for the same inputs, a grade
is a number Excel can calculate with, and every unmarked cell, format, merge and
print setting of the layout is unchanged.

## AC-003 More data than the layout has room for

**Given** a layout with fewer prepared rows than the course has students and
fewer prepared columns than the period has lessons

**Then** every student and every lesson is in the file, and the cells the school
placed below and to the right of the filled area are kept.

## AC-004 Template without a layout

**Then** export produces the program's own layout exactly as in US-028.

## AC-005 Replace and remove

**When** a layout is replaced or removed

**Then** the next export uses the new layout or the program's own layout; each
change writes its audit row.

## AC-006 Refused files

**Given** a file of a disallowed type, too large, not an Excel workbook, or with
no field marked (and any further case OD-002 and OD-004 settle)

**Then** it is refused with a message naming the problem, nothing is stored, no
audit row is written, and neither the file nor its name is written to the log
(SC-10).

## AC-007 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** uploading, replacing and removing a layout are refused, and export with
an attached layout works and writes its audit row.

## AC-008 Only Admin and Dean

Anonymous requests are refused; every endpoint has an allowed-role and a
forbidden-role test (TC-5).

## AC-009 Nothing executes

**Given** report values that Excel would read as a formula (a leading `=`, `+`,
`-`, `@`)

**Then** they are written as text exactly as US-028 writes them.

## AC-010 Tests never reach Google

Every test builds data synthetically in PostgreSQL (TC-2, TC-4); layouts used in
tests are synthetic files made by the tests, and file contents are checked by
reading the produced workbook, not by comparing bytes.

---

# Open Decisions

## OD-001 Where the layout lives

§3 says the layout "подключается к шаблону при экспорте" — it can be read as
"stored with the template and used at export" or as "supplied at each export".

Options:
(a) **(recommended)** stored with the template in the installation database —
uploaded once, used by every export of that template, covered by the database
backups (DC-13), deleted with the template;
(b) supplied with each export and never stored — nothing to keep, but the user
picks the file every time, and two Deans may export the same template
differently.

**Resolution:** (a) stored with the template in the installation database, by the Owner on 2026-10-13.

## OD-002 How the program knows where each field goes

Options:
(a) **(recommended)** markers typed into cells of the school's file, for
example `{{course}}`, `{{students}}`, `{{grades}}`, `{{topics}}` — the Dean
prepares the file in Excel, and the program fills the places the markers stand;
the Specification fixes the marker list and the help text that lists them;
(b) a form on the template page where the user names the sheet and starting
cell for each field — no change to the file, but the form is larger work and
easy to get wrong;
(c) fixed positions, as the prototype did — fits only
`school-journal-full.xlsx`, which §8 rules out.

**Resolution:** (a) markers typed into cells of the school's file, by the Owner on 2026-10-13.

## OD-003 Which templates can carry a layout

The built-in "Academic journal" cannot be changed (§3); attaching a layout
changes a template.

Options:
(a) **(recommended)** only templates created in the school, copies of the
built-in one included — a school that wants its own file on the built-in
template makes a copy;
(b) the built-in template too, as a separate per-school setting — more work,
and it blurs "the built-in template cannot be changed".

**Resolution:** (a) only templates created in the school, copies of the built-in one included, by the Owner on 2026-10-13.

## OD-004 Which files are accepted

Options:
(a) **(recommended)** `.xlsx` only, at most 1 MB; `.xls`, `.xlsm` (macros) and
anything else are refused; the file must open as a workbook and mark at least
one field;
(b) also accept `.xlsm` and `.xls` — `.xlsm` carries macros the program would
hand back to every user who exports, and `.xls` needs another library.

**Resolution:** (a) `.xlsx` only, at most 1 MB, by the Owner on 2026-10-13.

## OD-005 Is a new version of the requirements needed first

The Story adds the first file a user uploads into an installation and stores.
`security-conventions.md` has no rule for uploaded files yet.

Options:
(a) **(recommended)** no: §3 and §8 already require the layout, and the
resolutions of OD-001 … OD-004 are the authority for the Specification
(AGENTS.md, order of authority 4), as US-028 did for its library and layout;
the Specification derives the upload rule, and `security-conventions.md` gets it
in this Story;
(b) yes: write the upload rules into a new version of `trebovaniya.md` (§3, §8)
in its own commit before activation.

**Resolution:** (a) no new version of the requirements; the Story's resolutions are the authority, by the Owner on 2026-10-13.

---

# Notes

- The layout is meant to be an empty form. A Dean could upload a file that
  already holds student names or grades; the program cannot tell. The upload
  page says, in text from translation files, that the file must be an empty
  form; the stored file is never shown to anyone but Admin and Dean, and is
  deleted with its template.
- The file's name is the user's text and may contain a person's name; it is
  never written to the log or the audit row (SC-10, SC-11).
- The exported file holds students' personal data and grades; as in US-028 it is
  built in memory and streamed, never written to disk.
- The prototype's `exports.py` is the reference for what a school's journal file
  looks like, not for how to fill it: its fixed positions (row 8, row 9,
  column 3, `A2`, `D1`) are the hard-code §8 forbids.
- The report of US-027 stays the single source of the file's content; if the
  file and the on-screen report ever disagree, the screen is right.
- ClosedXML (US-028 OD-001) opens and saves the school's file; no new package is
  expected. If the Specification finds one is needed, that is a new Open
  Decision.
