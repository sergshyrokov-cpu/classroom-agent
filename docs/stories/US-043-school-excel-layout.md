---
id: US-043
epic: EPIC-3
title: Fill the group-lessons blank form with a template report
slug: school-excel-layout
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v91.
# Split out of US-028 by its OD-002 (resolved by the Owner on 2026-10-05).
# Re-authored by the Owner on 2026-10-14 during HUMAN_SPEC_APPROVAL of spec v1:
# no upload of a school's file; the program ships the printed form
# "Бланк для групових занять" and fills it copy by copy (OD-006 … OD-010,
# all resolved by the Owner on 2026-10-14).
# The slug and file name are kept so the workflow's story path stays valid.
---

# User Story

As a **Dean** or an **Admin**

I want to export a report into the school's printed academic-journal form,
"Бланк для групових занять", filled the way a teacher fills printed forms by
hand — a set number of students and lessons on each copy, the rest on the next
copy

So that the school prints its approved paper journal pages straight from the
program, without copying the data into the form by hand.

---

# Business Value

`trebovaniya.md` §3 (`ReportTemplate`, v84): "Excel-вёрстка (файл) подключается
к шаблону при экспорте". §6: "сохранить поддержку Excel (в т.ч. по существующим
шаблонам школы); шаблоны — конфигурируемый ресурс, а не хардкод в коде (как было
в прототипе)". §2: viewing and exporting the journal — Admin and Dean.

A school's academic journal is kept on an approved printed form — a page of
fixed lines and printed headings. The form in
`docs/product/report-templates/school-journal-full.xlsx` suits almost every
school: a sheet "Оцінювання" with 30 student rows and 24 lesson columns, and a
sheet "Теми занять" with 23 topic rows. US-028 exports the report in the
program's own layout, which does not look like this form. The prototype filled
this form at hard-coded cells and wrote downward past its lines; this Story
fills it like a stack of printed forms, and keeps the positions of the fields
in the form file, not in code.

In this Story the program knows **one** form and **one** way to fill it. A
form for individual lessons and further ways of filling (the second data
stream), or another printed form a school orders, are separate Stories.

---

# Terms

- **Journal** — the data: the mirror of one or, later, two data streams.
- **Template** — how values are shown: view, marks, scale, hours (US-027).
- **Blank form** ("бланк") — the approved printed page the data is written into,
  with its lines and printed text; shipped with the program.
- **Copy** — one filled instance of the form: one "Оцінювання" sheet, or one
  "Теми занять" sheet.
- **Fill method** — the rules that lay the data out over copies of a form. One
  method in this Story; more will be added for the same form later.

---

# Scope

## In scope

- **The shipped form** (OD-008, OD-009): the program ships "Бланк для групових
  занять" — the form of `school-journal-full.xlsx` with field markers typed
  into its cells. The program finds where each field goes from the markers; no
  cell address of the form is written in code (§6). Its lines, printed text,
  column widths, row heights and print settings are never changed.
- **Choice at export** (OD-007): next to "Export to Excel" on the report page
  the user chooses "Program layout" (US-028, unchanged) or "Бланк для групових
  занять". With the form chosen, two more inputs appear: **students per copy**
  (1–30, default 30) and **lessons per copy** (1–23, default 23). Nothing is
  stored — not in the template, not on the account. Any template, the built-in
  one included, can be exported either way.
- **What a copy holds** (OD-006):
  - "Оцінювання": the course (discipline) name in the row above the printed
    caption "(назва навчальної дисципліни/предмета)"; up to N students — row
    number and name; up to M lessons — the lesson date as the column heading;
    the grade cells. The 24th lesson column stays empty — it is kept for one
    final grade, which the second data stream fills later.
  - "Теми занять": the teachers next to "ПІБ Викладача", joined with ", "; one
    row per lesson of the block — lesson number, date, hours, topic. The columns
    "Самостійна робота студента" and "Підпис викладача" stay empty.
  - The course name and the teachers are written without wrapping: the text
    runs over the empty cells to its right; column widths stay as they are.
  - "Кількість годин на ___ семестр" stays empty.
  - The lesson dates of "Оцінювання" are the dates of "Теми занять".
- **Copies and their order** (OD-006): students are split into groups of N,
  lessons into blocks of M. For every lesson block, one "Оцінювання" copy per
  student group, then one "Теми занять" copy for that block. Example — 25
  students by 10, 30 lessons by 23:

  | Sheet | Content |
  |---|---|
  | Оцінювання 1 | students 1–10, lessons 1–23 |
  | Оцінювання 2 | students 11–20, lessons 1–23 |
  | Оцінювання 3 | students 21–25, lessons 1–23 |
  | Теми занять 1 | lessons 1–23 |
  | Оцінювання 4 | students 1–10, lessons 24–30 |
  | Оцінювання 5 | students 11–20, lessons 24–30 |
  | Оцінювання 6 | students 21–25, lessons 24–30 |
  | Теми занять 2 | lessons 24–30 |

  All copies are sheets of **one** file. Sheet names are "Оцінювання 1",
  "Оцінювання 2" … and "Теми занять 1" …; with a single copy, without the
  number.
- **Numbering**: student numbers and lesson numbers continue across copies
  (students 11–20 on the second copy). In rows of a copy that hold no student or
  no lesson, the form's row numbers are cleared; the lines stay.
- **Materials** are never written into the form, whatever the template's
  setting, so that the dates on both sheets match.
- **One source of content**: every value is what the on-screen report shows for
  the same template, course, period and name source (US-027, US-042) — built
  from the same report, not computed again. Grades are typed as in US-028 (a
  whole-number grade alone is a number Excel can calculate with).
- **Language**: the form's printed text stays as it is (it is the school's
  approved Ukrainian form). Program values written into it — marks of cell
  states, the "not assigned" dash — follow US-028 (NFR-073).
- **Fill method** (OD-010): "Базовий" / "Basic" — stored as a choice from a
  list with one item; not shown to the user while there is only one. Later
  methods fill page by page too and differ after the last lesson column of
  "Оцінювання".
- **Read-only mode** (BR-026): the export with the form works; its only write is
  the export audit row.
- **Audit**: the US-028 export audit row (§5, SC-11); the Specification decides
  whether its format value distinguishes the form from the program layout. No
  personal data, no file content.
- **Authorization**: as the US-028 export — Admin and Dean (§2); deny by
  default (SC-4); allowed-role and forbidden-role tests (TC-5).
- **No caching** (US-040): the file response carries `Cache-Control: no-store`.

## Out of scope

- Uploading a school's own Excel file; any other printed form (a separate Story
  per form a school orders); the "individual lessons" form (second data stream).
- Further fill methods; the final grade in the 24th column (second data stream).
- Word (US-029); several courses or templates in one file (US-030).
- Any change to the report (US-027), names (US-042), the program's own layout
  (US-028), or the template settings.
- Formulas the program writes: the program writes values only.

---

# Acceptance Criteria

## AC-001 One copy

**Given** a course with at most 30 students and at most 23 lessons in the
period, and the defaults 30 / 23

**When** the report is exported to "Бланк для групових занять"

**Then** the file has the sheets "Оцінювання" and "Теми занять"; the course
name, teachers, students, lesson dates, grades and topics are in their places;
every value equals the screen for the same inputs; a whole-number grade is a
number; the 24th lesson column, the hours-per-semester line, "Самостійна
робота" and "Підпис" are empty; the form's lines, printed text, widths, heights
and print settings are unchanged.

## AC-002 Several copies

**Given** 25 students, 30 lessons, 10 students and 23 lessons per copy

**Then** the file holds the eight sheets of the Scope example, in that order and
with those names; numbering continues across copies; every student and every
lesson is present exactly once per lesson block.

## AC-003 Partly filled copy

**Then** on a copy with fewer students or lessons than its places, the unused
rows and columns are empty — no row number, no value — and their lines remain.

## AC-004 Program layout unchanged

**When** "Program layout" is chosen

**Then** the file is exactly that of US-028.

## AC-005 Copy sizes validated

**Given** students per copy outside 1–30, lessons per copy outside 1–23, or not
a whole number

**Then** the export is refused with a translated message naming the field; no
file; no audit row; the value is not logged (SC-10).

## AC-006 Materials and template settings

**Given** a template that shows materials

**Then** no material appears in the form; marks, scale, view and late marks of
the template apply to the grade cells exactly as on screen.

## AC-007 Empty report

**Given** no lesson or no student in the period

**Then** the export succeeds with one copy of each sheet holding the course
name and teachers and nothing else that does not exist; it is audited.

## AC-008 Read-only mode

**Then** the export with the form works and writes its audit row.

## AC-009 Only Admin and Dean

Anonymous requests are refused; allowed-role and forbidden-role tests (TC-5).

## AC-010 Nothing executes

Report values Excel would read as a formula (a leading `=`, `+`, `-`, `@`) are
written as text exactly as US-028 writes them.

## AC-011 Tests never reach Google

Data built synthetically in PostgreSQL (TC-2, TC-4); the produced workbook is
checked by reading its sheets and cells, not by comparing bytes.

---

# Open Decisions

## OD-001 … OD-005 (superseded)

Resolved on 2026-10-13 for a Story that uploaded a school's own file (where the
layout lives, markers, which templates, accepted files, requirements version).
Superseded on 2026-10-14 when the Owner re-authored the Story: nothing is
uploaded. Kept in `docs/decisions/US-043-open-decisions.md` for history.

## OD-006 How the form is filled

**Resolution:** like printed forms filled by hand — a set number of students
and lessons per copy, overflow to the next copy in the order of the Scope; all
copies in one file; numbering continues; unused rows cleared; materials never
written; A5–E5 empty; the 24th column kept for a future final grade. By the
Owner on 2026-10-14.

## OD-007 Where the form is chosen

Options: (a) at export, next to "Export", with students and lessons per copy
(defaults 30 / 23), stored nowhere; (b) as a template setting.

**Resolution:** (a), by the Owner on 2026-10-14.

## OD-008 Where field positions live

Options: (a) field markers typed into the shipped form file; the code knows the
markers, not the cells (§6 "не хардкод"); (b) cell addresses in code — needs a
new version of `trebovaniya.md`.

**Resolution:** (a), by the Owner on 2026-10-14.

## OD-009 Where the marked form file is kept

The marked form is a versioned `.xlsx`. `AGENTS.md` allows versioned
spreadsheets only in `docs/product/report-templates/` (and `.gitignore`
re-includes only that folder).

Options: (a) **(recommended)** a new file in `docs/product/report-templates/`
(name indicative `group-lessons-blank.xlsx`) — the school form with markers,
no student data — built into the program from there; `school-journal-full.xlsx`
stays untouched as the reference; (b) a file inside `src/` — needs an exception
to the `AGENTS.md` rule.

**Resolution:** (a) a new file in `docs/product/report-templates/` (name
indicative `group-lessons-blank.xlsx`), by the Owner on 2026-10-14.

## OD-010 Name of the fill method

Not shown in the UI while it is the only one; named now so later methods sit
beside it. Every method fills page by page; they will differ in what follows
the last lesson column on "Оцінювання" (for example a final grade, an exam).

**Resolution:** "Базовий" / "Basic", by the Owner on 2026-10-14.

---

# Notes

- The prototype's `exports.py` filled this form at fixed cells (`A2`, `D1`, row
  8, row 9, column 3) and wrote past the form's lines; it is the reference for
  what the form is, not for how to fill it.
- The exported file holds students' personal data and grades; as in US-028 it is
  built in memory and streamed, never written to disk.
- The report of US-027 stays the single source of the file's content; if the
  file and the screen disagree, the screen is right.
- ClosedXML (US-028 OD-001) fills the form; no new package is expected. If the
  Specification finds one is needed, that is a new Open Decision.
- No new version of `trebovaniya.md` is expected: §3 ("Excel-вёрстка
  подключается к шаблону при экспорте") and §6 ("по существующим шаблонам
  школы", "не хардкод") are met by a shipped form whose field positions live in
  the file.
