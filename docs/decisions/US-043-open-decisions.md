---
artifact_type: open_decisions
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-13T18:55:00Z
updated_at: 2026-10-13T18:55:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: trebovaniya.md
    version: 91
supersedes: null
---

# US-043 Open Decisions — Fill a school's own Excel layout file with a template report

Five carried from the Story, all resolved by the Owner on 2026-10-13 before
activation. One raised by the Specification (OD-006), **open — blocks
API_DESIGN**; to be resolved at `HUMAN_SPEC_APPROVAL`.

No item of `trebovaniya.md` §7 (v91: 10, 14, 26, 27, 28) concerns this Story.
Item 26 (documentation for the school) is not a dependency: the help text that
lists the markers is program text on the upload page (spec FR-012), not school
documentation.

## OD-001 Where the layout lives

Carried from the Story. §3 says the layout "подключается к шаблону при
экспорте" — stored with the template, or supplied at each export.

Options: (a) stored with the template in the installation database — uploaded
once, used by every export of that template, covered by the database backups
(DC-13), deleted with the template; (b) supplied with each export, never stored.

**Resolution:** (a) stored with the template in the installation database, by
the Owner on 2026-10-13. Spec FR-001, FR-002, FR-009.

## OD-002 How the program knows where each field goes

Carried from the Story.

Options: (a) markers typed into cells of the school's file; the Specification
fixes the marker list and the help text; (b) a form naming sheet and start cell
per field; (c) fixed positions, as the prototype — ruled out by §8.

**Resolution:** (a) markers typed into cells of the school's file, by the Owner
on 2026-10-13. Spec FR-004, FR-005.

## OD-003 Which templates can carry a layout

Carried from the Story. The built-in "Academic journal" cannot be changed (§3).

Options: (a) only templates created in the school, copies of the built-in one
included; (b) the built-in template too, as a separate per-school setting.

**Resolution:** (a) only templates created in the school, copies of the
built-in one included, by the Owner on 2026-10-13. Spec FR-001.

## OD-004 Which files are accepted

Carried from the Story.

Options: (a) `.xlsx` only, at most 1 MB; `.xls`, `.xlsm` and anything else
refused; the file must open as a workbook and mark at least one field; (b) also
`.xlsm` and `.xls`.

**Resolution:** (a) `.xlsx` only, at most 1 MB, by the Owner on 2026-10-13.
Spec FR-003, VR-001.

## OD-005 Is a new version of the requirements needed first

Carried from the Story. The Story adds the first file a user uploads into an
installation and stores; `security-conventions.md` has no rule for uploaded
files yet.

Options: (a) no — §3 and §8 already require the layout; the Story's resolutions
are the authority; the Specification derives the upload rule and
`security-conventions.md` gets it in this Story; (b) yes — a new version of
`trebovaniya.md` first.

**Resolution:** (a) no new version of the requirements; the Story's
resolutions are the authority, by the Owner on 2026-10-13. Spec FR-014
(the new SC section), §7.

## OD-006 What happens when the course has more students or lessons than the layout has rows or columns

Raised by the Specification. **Open — blocks API_DESIGN** (it decides how the
filling works and what the help text tells the school).

The Story requires (AC-003) that every student and every lesson reach the file
and that what the school placed below and to the right of the filled area (for
example signature lines) is kept. A marker tells the program where the table
**starts**, not where the school's prepared rows **end**.

The school's real journal form (`docs/product/report-templates/school-journal-full.xlsx`)
shows what a prepared table looks like: on "Оцінювання" the header is in rows
7–8 and the grid of lines covers rows 7–38, columns A–Z — **30 student rows**
(9–38) and **24 lesson columns** (C–Z), with a row-number formula in A10–A38;
on "Теми занять" the grid covers rows 3–26, columns A–F — **23 topic rows**.
Below and right of the grids nothing is drawn. No merged cells.

Options:

- **(a) (Рекомендую)** **Fill the school's drawn table first; add rows and
  columns only when it runs out.** The end of the prepared table is found from
  the file itself: the prepared rows are the marked row and the rows directly
  below it whose cell in the marker's column has a border line; the prepared
  columns likewise to the right of the lesson column, in the lesson-marker row.
  Students fill the prepared rows; when there are more students than prepared
  rows, the program inserts new rows after the last prepared one, formatted like
  it (lines, fonts, height); the same for lesson columns and topic rows.
  Everything the school placed below or to the right moves down or right
  intact — never overwritten. Unused prepared rows and columns stay as the
  school drew them (empty lines, as on the paper form). No extra marker to
  learn: with the school's form, 25 students fill rows 9–33 and rows 34–38 stay
  empty; 35 students fill rows 9–38 and 5 rows are added.
- **(b) The program always adds rows and columns.** The school prepares **one**
  row and **one** column with the markers; the program inserts a new row or
  column for every further student or lesson, formatted like the prepared one.
  Predictable, but the school's form must be cut down to one prepared row and
  column first, and the printout always has exactly as many lines as students.
- **(c) As (a), but the school marks the end of the prepared table itself** with
  one more marker (for example `{{students.end}}`, `{{lessons.end}}`) instead of
  the program reading the lines. Works for a form without lines, but one more
  thing for the Dean to learn and more rules to check on upload.
- (d) The program writes downward and rightward over whatever is there, never
  adding rows, as the prototype did. Overwrites signature lines when the course
  is larger than the form — contradicts AC-003.

- **(e) Under consideration by the Owner (2026-10-13): continue on a new sheet,
  a copy of the form.** The Dean states how many students (and lessons, topics)
  one sheet of the form holds; when the course has more, the program adds a
  copy of the form's sheet — with its header, lines and signature lines — and
  continues there, as a paper journal continues on the next page. The Owner is
  formulating the additional rules; the Specification will be revised (FR-006,
  FR-001 settings, VR, help text) once they are given.

  **Owner's direction so far (discussion 2026-10-13/14, not yet a resolution —
  the Story is to be re-authored):**
  - No upload in this Story. The program ships one form, "Бланк для групових
    занять" (`school-journal-full.xlsx`); another printed form a school orders
    is a separate Story. Proposed: the shipped file carries the markers, so
    positions live in the file, not in code (§6 "не хардкод").
  - Filling is like filling printed paper forms by hand: nothing is inserted,
    moved or resized; overflow goes to the next copy of the form.
  - One copy of the form = "Оцінювання" + "Теми занять": up to 30 students
    (rows 9–38) and up to 23 lessons; lesson dates of "Оцінювання" (C8…Y8) equal
    the dates of "Теми занять" (B4…B26); column Z is left for one final grade
    (empty in the pilot; the second data stream fills it later).
  - The Dean chooses students per copy (1–30) and lessons per copy (1–23) **at
    each export, next to the "Export" button**, defaults 30 and 23.
  - "Оцінювання" A2 = course (discipline) name above the A3 caption; "Теми
    занять" D1 = teachers, joined with ", "; both without wrapping (text runs
    over the empty cells to the right; column widths unchanged).
  - Materials are never written into the form, whatever the template setting
    (so the two sheets' dates match).
  - "Кількість годин на ___ семестр" (A5–E5) stays empty.
  - "Fill method" is a choice from a list (one item now; the second stream adds
    more for the same form); not shown in the UI while there is one. Its label
    is still to be named.
  - One file, all copies as sheets. Student numbering continues across copies
    (11, 12 … on the second). Order: students inner, lesson blocks outer, each
    lesson block followed by its "Теми занять" — e.g. 25 students by 10, 30
    lessons by 23: Оцінювання 1 (st 1–10, l 1–23), 2 (11–20), 3 (21–25), Теми
    занять 1 (l 1–23), Оцінювання 4 (st 1–10, l 24–30), 5, 6, Теми занять 2
    (l 24–30).
  - Sheet names "Оцінювання 1"…, "Теми занять 1"…; no number when there is
    only one copy.
  - Row numbers in unfilled student rows are cleared (the form's `=A9+1`
    formulas would otherwise number empty rows); lines stay.
  - Pending: the fill method's label; where the user chooses the blank instead
    of the program's own layout (US-028).

Impact: FR-006.2 (filling), VR-003 rule 7 (upload checks for (c)), FR-012 (help
text), AC-003.

**Resolution:** _open_
