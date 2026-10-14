---
artifact_type: specification
story: US-043
version: 2
status: APPROVED
created_at: 2026-10-13T18:55:00Z
updated_at: 2026-10-14T05:59:11Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: trebovaniya.md
    version: 91
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/product/report-templates/school-journal-full.xlsx
    version: null
supersedes: null
---

# US-043 Specification — Fill the group-lessons blank form with a template report

> **Revision v2 (2026-10-14).** Replaces v1 in place (v1 is in git history,
> commit `0236ace`). v1 specified the upload of a school's own Excel file; it
> was rejected at `HUMAN_SPEC_APPROVAL` when the Owner re-authored the Story:
> nothing is uploaded, the program ships one printed form and fills it copy by
> copy (Story OD-006 … OD-010). Everything about uploads, stored layouts and the
> SC-15 "Uploaded files" rule is gone.
>
> Updated at the gate (2026-10-14, still v2): OD-011 resolved (a) — 100 sheets;
> OD-012 added and resolved by the Owner — the copy number in the page footer
> (FR-006.1, AC-015); the Story amended accordingly.

## 1. Overview

US-028 exports the report of a template (US-027, names by US-042) into an
`.xlsx` laid out by the program. This Story adds a second way to export the same
report: into the school's approved printed form **"Бланк для групових занять"**,
shipped with the program, filled like a stack of paper forms — a set number of
students and lessons per copy, the rest on the next copy (`trebovaniya.md` v91
§3 `ReportTemplate`: "Excel-вёрстка (файл) подключается к шаблону при
экспорте"; §6: "сохранить поддержку Excel (в т.ч. по существующим шаблонам
школы) … шаблоны — конфигурируемый ресурс, а не хардкод в коде (как было в
прототипе)").

This Story:

- ships the **marked form** `docs/product/report-templates/group-lessons-blank.xlsx`
  — the empty school form with field markers in its cells (OD-008 a, OD-009 a);
- adds to the report page the choice **"Program layout" / "Бланк для групових
  занять"** with **students per copy** and **lessons per copy** (OD-007 a);
- lays the report out over copies of the form by the one fill method
  **"Базовий" / "Basic"** (OD-006, OD-010);
- builds every value from the **same report** as the screen — nothing computed
  again;
- prints each copy's number in its page footer (OD-012).

It adds no Google call, no role or matrix cell, no stored setting, no new audit
action and no schema change. The program's own layout (US-028) is unchanged.

Delegated during writing (read only, `quick-look`): the US-027 / US-028 / US-040
facts and the export code (routes, request record, renderer port, audit
factory), and the structure of `school-journal-full.xlsx`. Facts relied on:

- `POST /api/v1/exports/journal-xlsx` (`JournalExportController`, policy
  `UseReportTemplates`) takes a **JSON** body `JournalExportRequest(Template,
  CourseId, From, To, Names, Orientation)`; the report page posts it from
  `wwwroot/js/report-export.js`. `ExportJournalCommand` → `IReportRenderer`
  (`ClosedXmlReportRenderer`); cells as `Application/Models/Export/WorkbookCell`.
- The export audit row is `AuditEvent.JournalExported` with `ExportFormat`
  (enum, one value `Xlsx`, stored as `xlsx`), period, template, row count.
- No `.xlsx` is embedded in any project yet.
- The school form (`school-journal-full.xlsx`), checked cell by cell:
  - sheet **"Оцінювання"**: `A1` title; `A2` empty above the caption `A3`
    "(назва навчальної дисципліни/предмета)"; `A5` "Кількість годин на ",
    `E5` "семестр"; `C7` "Дата проведення заняття"; `A8` "№ з/п", `B8` "ПІБ
    студента"; header row 8 (height 72.75, `C8:Z8` text rotated 90°); student
    rows 9–38 (`A9` = 1, `A10:A38` formulas `=A9+1` …); lesson columns C–Z
    (24 columns, width 2.78); thin borders exactly `A7:Z38`;
  - sheet **"Теми занять"**: `A1` "ПІБ Викладача", `D1` empty; header row 3
    (№ заняття, Дата проведеня, К-сть навч. годин, Зміст заняття, Самостійна
    робота студента (домашнє завдання), Підпис викладача); topic rows 4–26
    (`A4` = 1, `A5:A26` formulas); borders exactly `A3:F26`;
  - both portrait A4, no print area, titles or scaling; no merged cells, data
    validation, conditional formats, names, headers or footers. All number
    formats General.

## 2. Business Goal

The school keeps its academic journal on an approved printed form. US-028's
layout does not look like it, so the school copies the data across by hand.
The prototype filled this very form at hard-coded cells (`exports.py`: `A2`,
`D1`, row 8, row 9, column 3) and wrote downward past its lines. Here the
program fills it the way a teacher fills paper forms — page by page, never past
the lines — and the field positions live in the form file, not in code.

## 3. Business Flow

1. On the report page (US-027 / US-028) the user picks template, course, period
   and name source as today.
2. Next to "Export to Excel" the user chooses **"Program layout"** (preselected)
   or **"Бланк для групових занять"**. With the form chosen, two inputs appear:
   **students per copy** (1–30, default 30) and **lessons per copy** (1–23,
   default 23); the orientation choice of US-028 is hidden.
3. "Export to Excel" downloads one `.xlsx`: with the program layout — the US-028
   file, unchanged; with the form — all filled copies of the form as sheets of
   that one file.
4. Nothing of the choice is remembered; the next visit starts with "Program
   layout" and 30 / 23.

## 4. Functional Requirements

### FR-001 The shipped form

- The program ships **one** form, "Бланк для групових занять", as the file
  `docs/product/report-templates/group-lessons-blank.xlsx` (OD-009 a, name as
  indicated there). It is built into the program as an **embedded resource** of
  `ClassroomAgent.Infrastructure`, included from that path (a project-file
  entry; no new package) (I-1).
- The file is made from `school-journal-full.xlsx` by typing the markers of
  FR-002 into the cells listed there, setting the date number format of
  FR-005.3 on the two date marker cells and the footer margin of I-14 —
  **nothing else changes**: no line,
  printed text, width, height, print setting, sheet name or order. It holds no
  student data. `school-journal-full.xlsx` stays untouched as the reference
  (OD-009 a). `docs/product/report-templates/README.md` gains one line naming
  the new file and its purpose.
- The form is read-only to the program: it is opened from the resource for each
  export and never written back, never stored in the database, never offered
  for download as such.
- No cell address of the form is written in code (OD-008 a; §6). The code knows
  the **markers** and the form's **capacities** (FR-003); it finds every place
  from the markers.

### FR-002 The markers

A marker is the whole text of a cell, `{{name}}`, fixed ASCII, written by the
program's authors into the shipped file (never typed by a school in this
Story). Each marker occurs **exactly once** in the file:

| Marker | Sheet | Cell in the shipped file | Meaning |
|---|---|---|---|
| `{{course}}` | Оцінювання | `A2` | course (discipline) name |
| `{{student.no}}` | Оцінювання | `A9` (replaces the printed `1`) | first student row — number |
| `{{student.name}}` | Оцінювання | `B9` | first student row — name |
| `{{lesson.date}}` | Оцінювання | `C8` | first lesson column — date heading |
| `{{grade}}` | Оцінювання | `C9` | first grade cell |
| `{{teachers}}` | Теми занять | `D1` | teachers |
| `{{topic.no}}` | Теми занять | `A4` (replaces the printed `1`) | first topic row — lesson number |
| `{{topic.date}}` | Теми занять | `B4` | first topic row — date |
| `{{topic.hours}}` | Теми занять | `C4` | first topic row — hours |
| `{{topic.title}}` | Теми занять | `D4` | first topic row — topic |

The cell column above is a fact about the file, recorded here for whoever
prepares it; the code never contains it. Students go **down** from the student
markers, lessons **right** from the lesson marker, topics **down** from the
topic markers; the grade of student *i* and lesson *j* is at the student row of
*i* and the lesson column of *j*.

"Самостійна робота студента" and "Підпис викладача" have no marker: they stay
empty (Story). "Кількість годин на ___ семестр" has no marker: it stays empty
(Story, OD-006).

### FR-003 Capacities of the form

The form is described in code by its markers and **three capacities**, all from
the Story (no cell address):

| Capacity | Value | Meaning |
|---|---|---|
| Student rows | 30 | rows from the student-marker row downward |
| Lesson columns | 24 | columns from the lesson-marker column rightward |
| Topic rows | 23 | rows from the topic-marker row downward |

The form's integrity is a fact checked by a test (AC-013), not at run time by a
user: every marker of FR-002 exists exactly once on its sheet; the student,
lesson and topic areas given by the capacities lie inside the form's drawn
lines.

If at export the embedded form cannot be opened or a marker is missing or
duplicated, that is a program defect: the export fails through the single
exception handler (AD-9, API-10) — no file, no audit row (I-2).

### FR-004 The export choice and request

- The report page's export block (`Views/Report/Index.cshtml`, `#report-export`)
  gains a choice **"Layout"** with two options: **"Program layout"**
  (preselected) and **"Бланк для групових занять"** (OD-007 a). With the form
  chosen, two whole-number inputs appear: **students per copy** (default 30) and
  **lessons per copy** (default 23); the orientation choice is hidden (FR-007).
  With "Program layout" the two inputs are hidden and the block is exactly that
  of US-028.
- Any template — the built-in "Academic journal" and every created one — can be
  exported either way (Story).
- The export request (US-028 FR-001, the same endpoint and JSON body) gains
  three **optional** members: the layout (`program` or `group-lessons-blank`),
  students per copy and lessons per copy. API_DESIGN fixes their JSON names and
  types within API conventions. Absent layout → `program`: every existing
  request and test of US-028 stays valid (AC-004).
- Nothing of the choice is stored — not in the template, not on the account,
  not in the session (OD-007 a).
- **Fill method** (OD-010): `Application` models the fill methods as a closed
  list with one item, **Basic** ("Базовий" / "Basic" in the translation files).
  It is not shown on the page and not sent in the request while it is the only
  one; the export of the form always uses it (I-3).

### FR-005 What a copy holds (fill method Basic)

The input is the report built by the existing report query exactly as for the
screen and for US-028 (US-027 FR-005, US-042 FR-006, US-028 FR-003) — same
template, course, period and name source. `Application` lays it out over copies
(FR-006) into a typed, translated **fill model**; the renderer only places
values at the markers (AD-3: no business rule in the renderer; AD-4: no
ClosedXML type in `Application`).

**FR-005.1 Lessons.** The lessons are the report's items that are **not
materials**, in report order — exactly the rows of the report's "Lesson
topics" (US-027 FR-005.6). Materials are never written into the form, whatever
the template's materials setting (Story), so the dates on both sheets match.

**FR-005.2 "Оцінювання" copy** for student group *g* and lesson block *b*:

| Place | Value |
|---|---|
| `{{course}}` cell | the course name and section as the report header shows them |
| student row *k* (0 … 29) holding a student | number: the student's position in the report (continuing across groups — 11, 12 … on the second copy of 10); name: the displayed name (US-042 FR-003) |
| student row *k* holding no student | number and name cleared (no value, no formula); lines stay |
| lesson column *j* (0 … 22) holding a lesson | heading: the lesson date; grade cells: the report cell of each student of the group × that lesson |
| lesson column *j* holding no lesson | heading and grade cells empty |
| lesson column 23 (the 24th) | always empty — kept for one final grade the second data stream fills later (OD-006) |

**FR-005.3 Cells.**

- Grade cells: exactly the typing of US-028 FR-004.5 — a whole-number grade
  alone is a **number**; anything else is **text** composed as on screen (the
  template's marks, scale, view and late marks, AC-006); an empty report cell is
  an empty cell. The "not assigned" value is the screen's (US-028 AC-003).
- Lesson dates (`{{lesson.date}}`, `{{topic.date}}`): Excel **dates**. Their
  number format is the one set on the marker cell in the shipped file:
  `dd.mm.yyyy` (I-4). The rotated heading of row 8 is the form's own style.
- Student numbers, lesson numbers and hours: **numbers**.
- Every text the program writes follows US-028 FR-004.7 (a string value, never
  a formula; the quote-prefix style for a leading `=`, `+`, `-`, `@`, tab or
  carriage return; AC-010) and FR-004.9 (cut at 32 767 characters).
- A written cell keeps its own style from the form (font, borders, alignment,
  rotation); only its value — and on date cells the number format — is set. The
  course name and the teachers are written **without wrapping**: their cells'
  wrap is off, so the text runs over the empty cells to the right; no width
  changes (Story).

**FR-005.4 "Теми занять" copy** for lesson block *b*:

| Place | Value |
|---|---|
| `{{teachers}}` cell | the teachers of US-027 FR-005.5 / US-042, joined with ", " |
| topic row *r* (0 … 22) holding a lesson | lesson number (continuing across blocks — 24, 25 … on the second block of 23); date; hours (the template's hours per lesson); topic (the item's title, never translated) |
| topic row *r* holding no lesson | number cleared; the other cells empty; lines stay |
| "Самостійна робота студента", "Підпис викладача" | empty |

**FR-005.5 Rows and columns past the chosen size.** When students per copy *N*
< 30 or lessons per copy *M* < 23, the rows *N* … 29, the lesson columns *M* …
22 and the topic rows *M* … 22 are treated as "holding no student / lesson":
cleared, lines kept (AC-003).

**FR-005.6 Untouched.** Every other cell of the form — printed text, the empty
"Кількість годин на ___ семестр" line, cells outside the areas — and every
width, height, line, print setting is exactly as in the shipped file — the
only print-setting change is the footer of FR-006.1 (OD-012). No
formula is written; the form's numbering formulas are replaced by values in
filled rows and removed in cleared rows (Story "Numbering"). No marker text
remains in the file.

**FR-005.7 Empty states** (US-027 FR-005.7). The program writes no "nothing was
published" or "no students" text into the form — it has no place for it (I-5).
With no lesson the lesson areas are empty; with no student the student areas
are empty; the course name and teachers are still written. Copy counts follow
FR-006 with at least one group and one block, so a report with neither lessons
nor students gives exactly one "Оцінювання" and one "Теми занять" (AC-007, I-6).
An empty report is exported and audited like any other (US-028 FR-004.6).

### FR-006 Copies and their order

- Students (report order) are split into **groups** of *N*; lessons (FR-005.1)
  into **blocks** of *M*. Groups = max(1, ⌈students / N⌉); blocks =
  max(1, ⌈lessons / M⌉).
- Order of sheets: for each block in order — one "Оцінювання" copy per group in
  order, then one "Теми занять" copy for that block (Story table: 25 students by
  10, 30 lessons by 23 → Оцінювання 1 … 3, Теми занять 1, Оцінювання 4 … 6, Теми
  занять 2).
- Every student and every lesson is present **exactly once per block** (AC-002).
- **Sheet names** come from the form's sheet names ("Оцінювання", "Теми занять"
  — the school's printed form, never translated) followed by a space and the
  copy's number of that kind, counted through the file ("Оцінювання 1" …
  "Оцінювання 6"). A kind with exactly **one** copy in the file keeps the name
  without a number (I-7).
- Each copy is a copy of the form's sheet with everything of FR-005.6; all
  copies are sheets of **one** file. The form's sheets themselves do not remain
  as extra unfilled sheets.
- **Sheet limit** (OD-011 a): when groups × blocks + blocks exceeds **100**, the
  export is refused with a translated message naming the two per-copy inputs
  and suggesting a shorter period; no file, no audit row (VR-002).

**FR-006.1 Copy number in the page footer** (OD-012). Each copy prints its
number within its kind — the same number as its sheet name, `1` for a kind with
a single copy — as digits only, in the **page footer**:

| Sheet kind | Footer section | Other sections |
|---|---|---|
| "Оцінювання" | right | left and centre empty |
| "Теми занять" | left | centre and right empty |

- The number is fixed text written by the program for that copy, not Excel's
  page-number field (I-13).
- The page header and the other footer sections stay empty: they are kept free
  for a future continuous page numbering of the paper journal (OD-012).
- The footer is the only print setting the program sets. The footer margin
  comes from the shipped file (I-14).

### FR-007 Request handling, file and response

- Validation (VR-001, VR-002) runs before the report query; an invalid request
  gets the US-028 error responses (`400` with the API error body, the field
  named, the value never echoed into a log).
- With the form, the orientation member is still validated as US-028 VR-001 but
  **not applied**: the form's own print settings rule (portrait A4, its margins)
  plus the footer of FR-006.1 (I-8). None of US-028 FR-004.8 (fit to width, repeated header, frozen column)
  is applied to the form.
- File name, content type, `Content-Disposition`, status codes: those of US-028
  FR-005, FR-006, FR-006.1 — unchanged for both layouts (I-9).
- The file is built in memory from the embedded form and streamed; never
  written to disk, never stored (Story Notes, US-028 FR-006).
- `Cache-Control: no-store` by the host-wide rule (SC-14, US-040); nothing of its
  own is added (AC-012).
- With "Program layout" the file is **exactly** that of US-028 (AC-004): the new
  members are ignored, not validated (I-10).

### FR-008 Authorization

As the US-028 export (FR-008 there): the endpoint stays under
`UseReportTemplates` — Admin ✔, Dean ✔ (§2). No new policy, endpoint or matrix
cell. Deny by default (SC-4); anonymous `/api/v1` → `401`; a Dean on the forced
password change refused as everywhere (US-012); missing antiforgery token →
`400` (API-7). Teacher and Student have no account (Hard Stop).

### FR-009 Read-only mode

The export with the form works in read-only mode, for every BR-025 cause; it
does not call `IReadOnlyModeGuard`; its only write is the export audit row
(BR-026, US-028 FR-009). No use case of this Story opens a Google port, in any
mode (SC-8).

### FR-010 Audit

- One `JournalExported` row per successful export with the form, exactly as
  US-028 FR-010: actor and role from the session, target the course, period,
  template, number of student rows, format **`xlsx`** — the format value does
  **not** distinguish the form from the program layout (I-11). No personal data,
  no file content, no per-copy sizes.
- Refused requests (VR-001, VR-002) write no row (AC-005). If the row cannot be
  written, no file is returned (US-028 FR-010).

### FR-011 Logging

- The US-028 FR-012 `Information` line, plus: the layout (`program` /
  `group-lessons-blank`), and for the form the fill method, students and
  lessons per copy and the number of sheets.
- Validation failures: one `Warning` naming the field and the rule, never the
  value (SC-10).
- No log line carries a name, grade, title, teacher, course name or file content
  (SC-10, NFR-023, DC-10).

### FR-012 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073):
the "Layout" label, "Program layout", the form's name ("Бланк для групових
занять" / "Group lessons blank form"), "Students per copy", "Lessons per copy",
the fill-method name ("Базовий" / "Basic"), every validation message of VR-001
and VR-002. Program values written into the form (marks of cell states, the
"not assigned" value) follow US-028 FR-007 — the exporting user's UI language.
The form's printed text and sheet names, Google data and text a Dean wrote in a
template are never translated. The existing missing-key test covers the new
keys.

## 5. Acceptance Criteria

Story AC-001 … AC-011 keep their ids; AC-012 … AC-015 are derived.

| Id | Criterion | Covered by |
|---|---|---|
| AC-001 | ≤ 30 students, ≤ 23 lessons, defaults: sheets "Оцінювання" and "Теми занять"; course, teachers, students, dates, grades, topics in their places; values equal the screen; a whole-number grade is a number; the 24th lesson column, the hours-per-semester line, "Самостійна робота", "Підпис" empty; lines, printed text, widths, heights, print settings unchanged except the footer of FR-006.1 | FR-001, FR-002, FR-005, FR-006 |
| AC-002 | 25 students, 30 lessons, 10 / 23: the eight sheets of the Story table in order and with those names; numbering continues; every student and lesson once per block | FR-006, FR-005.2, FR-005.4 |
| AC-003 | Partly filled copy: unused rows and columns empty — no number, no value — lines remain | FR-005.2, FR-005.4, FR-005.5 |
| AC-004 | "Program layout" (or no layout member): exactly the US-028 file | FR-004, FR-007 |
| AC-005 | Students per copy outside 1–30, lessons per copy outside 1–23, not a whole number, or an unknown layout value: refused naming the field; no file; no audit row; value not logged | VR-001, FR-007, FR-010, FR-011 |
| AC-006 | Template showing materials: no material in the form; marks, scale, view, late marks applied as on screen | FR-005.1, FR-005.3 |
| AC-007 | No lesson or no student: export succeeds with one copy of each sheet where FR-006 gives one; course and teachers written; nothing invented; audited | FR-005.7, FR-006, FR-010 |
| AC-008 | Read-only mode (each BR-025 cause): export with the form works and writes its audit row | FR-009 |
| AC-009 | Anonymous refused; allowed-role (Admin, Dean) and forbidden-role tests for the form export | FR-008 |
| AC-010 | Values Excel would read as a formula are written as text with the quote prefix, as US-028 | FR-005.3 |
| AC-011 | Data synthetic in PostgreSQL, no Google; the workbook read by sheets and cells, never compared by bytes | §9 |
| AC-012 | The form export response carries `Cache-Control: no-store` | FR-007 |
| AC-013 | The shipped form: every FR-002 marker exactly once on its sheet; capacity areas inside the drawn lines; no other difference from `school-journal-full.xlsx` than the markers, the two date formats and the footer margin | FR-001, FR-002, FR-003 |
| AC-014 | More than 100 sheets: refused with the message; no file; no audit row | FR-006, VR-002 |
| AC-015 | Every copy's footer: "Оцінювання" — its number on the right, "Теми занять" — on the left; digits only; 1 for a single copy; header and other sections empty | FR-006.1 |

## 6. Validation Rules

### VR-001 The new request members

Validated **only** when the layout is `group-lessons-blank`, after the US-028
VR-001 members:

| Member | Rule | Invalid → |
|---|---|---|
| layout | absent → `program`; otherwise exactly `program` or `group-lessons-blank` (case-sensitive, as US-028 orientation) | `400`, field named |
| students per copy | absent → 30; a JSON whole number 1 … 30 | `400`, field named |
| lessons per copy | absent → 23; a JSON whole number 1 … 23 | `400`, field named |

"Whole number" excludes fractions (`10.5`), strings (`"10"`), booleans and
`null` given explicitly is treated as absent (I-12). The translated message
names the field; the rejected value is never logged or echoed (SC-10). The
limits 30 / 23 are the capacities of FR-003 (23 = lesson columns minus the one
kept for the final grade, and equal to the topic rows).

### VR-002 Sheet limit

OD-011 (a): the number of sheets of FR-006 at most **100**, else refused
(`400`, message names students per copy and lessons per copy); checked after
the report query (it needs the counts), before rendering.

### VR-003 The actor

Actor, role and UI language come from the session, never from the request
(US-028).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The export keeps `UseReportTemplates`; deny by default; SC-4 anonymous list unchanged | §2, SC-4 |
| S-02 | Allowed-role and forbidden-role tests for the form export | SC-1, TC-5 |
| S-03 | `POST` with antiforgery, as US-028 | §8, API-7 |
| S-04 | Read-only: export works, writes only its audit row; no Google port in any mode | AD-6, SC-5, SC-8, BR-026 |
| S-05 | The form is a resource shipped with the program; nothing is uploaded; no user-supplied file is read | Story, OD-008 a |
| S-06 | The shipped form holds no personal data (AC-013) | AGENTS.md Hard Stops, OD-009 a |
| S-07 | The filled file is built in memory and streamed; never written to disk or stored | Story Notes, US-028 FR-006 |
| S-08 | Audit row as US-028 — no personal data, no content; logs carry no report content or request values | SC-10, SC-11 |
| S-09 | Values only — no formula written; quote prefix on dangerous leading characters | US-028 FR-004.7, AC-010 |
| S-10 | No entity in a request, response or view model; ClosedXML only in `Infrastructure`; no new package | AD-4, AD-8, AGENTS.md |
| S-11 | `Cache-Control: no-store` by the host-wide rule | SC-14 |
| S-12 | No new outbound destination | SC-13 (Hard Stop) |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous / forced password change / no antiforgery | As US-028 FR-008 (`401` / refused / `400`) |
| US-028 VR-001 failures, unknown template or course | As US-028 (`400` / `404`) |
| VR-001 of this Story | `400`, API error body naming the field; no file; no audit row |
| VR-002 (more than 100 sheets) | `400`, message naming the per-copy inputs; no file; no audit row |
| Embedded form missing, unreadable or its markers broken | Program defect: single exception handler (AD-9, API-10); no file; no audit row |
| Rendering or audit write fails | As US-028: no file; single exception handler |

The report page shows a refusal as US-028's script does today (its message
area), with the translated message of the response.

## 9. Non-Functional Requirements

- **Memory and time** — the form (≈ 80 KB) is opened from the resource and
  filled in memory per export; the report query is unchanged (US-027 FR-004: a
  fixed number of round trips). At most 100 sheets per file (OD-011 a).
- **NFR-073 / NFR-074** — bilingual program text; dates in the school time
  zone; period tests in `Europe/Kyiv` (TC-8).
- **NFR-070** — the layout choice and the two inputs usable at phone width.
- **Tests (AC-011)** — data built synthetically in PostgreSQL via Testcontainers
  (TC-2); Google ports substituted (TC-4). The exported workbook is opened from
  the response bytes and its sheet names, cell values, value types, number
  formats, styles, widths, heights and print settings are asserted against the
  shipped form — never compared by bytes. AC-013 compares the shipped form with
  `school-journal-full.xlsx` cell by cell. No generated `.xlsx` is written into
  the repository (Hard Stop); the only new versioned spreadsheet is the form of
  FR-001 (OD-009 a).
- **NFR-062** — .NET 10, nullable enabled, warnings as errors.

## 10. Out of Scope

- Uploading a school's own Excel file; any other printed form; the "individual
  lessons" form (second data stream).
- Further fill methods; the final grade in the 24th column; the hours per
  semester line.
- Word (US-029); several courses or templates in one file (US-030).
- Any change to the report (US-027), names (US-042), the program's own layout
  (US-028), template settings, or the export audit row's shape.
- Program-written formulas; translating the form's printed text or sheet names.

## 11. Open Decisions

See `docs/decisions/US-043-open-decisions.md` (v2).

| Id | Subject | State | Impact |
|---|---|---|---|
| OD-001 … OD-005 | Upload of a school's own file | Superseded 2026-10-14 (Story re-authored) | none |
| OD-006 | How the form is filled | Resolved (Story, 2026-10-14) | FR-005, FR-006 |
| OD-007 | Where the form is chosen | Resolved: (a) at export, 30 / 23, stored nowhere | FR-004, VR-001 |
| OD-008 | Where field positions live | Resolved: (a) markers in the shipped file | FR-001 … FR-003 |
| OD-009 | Where the marked form is kept | Resolved: (a) `docs/product/report-templates/` | FR-001 |
| OD-010 | Name of the fill method | Resolved: "Базовий" / "Basic" | FR-004, FR-012 |
| OD-011 | Upper limit on the number of sheets | Resolved: (a) 100 sheets | FR-006, VR-002, AC-014, Error Handling |
| OD-012 | Copy number on the printed page | Resolved by the Owner at the gate: footer, right / left, digits | FR-006.1, AC-015 |

### Interpretations

Stated choices the wording leaves open; any may become an Open Decision at
`HUMAN_SPEC_APPROVAL`.

- **I-1** The form is an embedded resource included from its versioned path in
  `docs/product/report-templates/`, so there is one copy of the file in the
  repository and the program cannot run with a different one.
- **I-2** A broken shipped form is a program defect (a `500` through the single
  handler), not a user error; AC-013 keeps it from shipping.
- **I-3** The fill method is a list in code with one item, not a request member
  and not shown; a second method will add the member and the choice.
- **I-4** Date format `dd.mm.yyyy`, set in the shipped file (the form's own
  format, not the UI language's): it fits the rotated heading of row 8 and the
  width of "Дата проведеня" on "Теми занять"; the form's General format would
  show a date as a bare number.
- **I-5** No "nothing published" / "no students" text is written into the form.
- **I-6** AC-007 is read with FR-006: one group and one block at least; with
  students but no lessons, students are still split by *N* into groups, each
  with an empty lesson area (they exist, so they are written).
- **I-7** "With a single copy, without the number" applies per kind: 25
  students by 10 with 5 lessons gives "Оцінювання 1 … 3" and "Теми занять".
- **I-8** With the form the orientation is validated but not applied; the
  choice is hidden on the page.
- **I-9** The file name of US-028 FR-006.1 is used for both layouts.
- **I-10** With `program` the new members are ignored, not validated, so the
  US-028 file and responses stay exactly as they are.
- **I-11** The audit format stays `xlsx` for both layouts: the format is the
  file type, and keeping it needs no new enum value and no migration. If the
  Owner wants the audit to tell the two apart, that becomes an Open Decision
  (a new `ExportFormat` value with its migration).
- **I-13** The footer number is fixed text per copy, not `&P`: each copy is
  one printed page (the form's rows and columns fit one portrait A4), and the
  number must equal the copy's sheet number, not a page count of the file.
- **I-14** The form's footer margin is 0, which most printers cannot print; the
  shipped file sets the footer margin to 0.5 cm (inside the 1 cm bottom margin)
  when it is prepared — a print setting stored in the file, not in code.
- **I-12** An explicit JSON `null` for a per-copy member is treated as absent
  (default), like US-028's optional members.

### Findings

- **F-1** The form's lesson columns are 2.78 characters wide with a rotated
  heading row 72.75 pt high: lesson dates fit only rotated, which the form's
  row 8 style already provides (`C8:Z8` text rotation 90°).
- **F-2** The form's numbering cells `A10:A38` and `A5:A26` are formulas
  (`=A9+1` …); left in a partly filled copy they would number empty rows —
  hence the Story's "row numbers cleared" and FR-005.6.
- **F-3** "Теми занять" has no teacher column: the teachers appear only once, at
  `D1` ("ПІБ Викладача").

## 12. Traceability

| AC | FR | VR / S |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-005, FR-006 | S-09 |
| AC-002 | FR-006, FR-005.2, FR-005.4 | — |
| AC-003 | FR-005.2, FR-005.4, FR-005.5 | — |
| AC-004 | FR-004, FR-007 | — |
| AC-005 | FR-007, FR-010, FR-011 | VR-001, S-08 |
| AC-006 | FR-005.1, FR-005.3 | — |
| AC-007 | FR-005.7, FR-006, FR-010 | — |
| AC-008 | FR-009 | S-04 |
| AC-009 | FR-008 | S-01, S-02, S-03 |
| AC-010 | FR-005.3 | S-09 |
| AC-011 | §9 | — |
| AC-012 | FR-007 | S-11 |
| AC-013 | FR-001, FR-002, FR-003 | S-05, S-06 |
| AC-014 | FR-006 (OD-011) | VR-002 |
| AC-015 | FR-006.1 (OD-012) | — |
