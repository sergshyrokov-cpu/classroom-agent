---
artifact_type: open_decisions
story: US-043
version: 2
status: APPROVED
created_at: 2026-10-13T18:55:00Z
updated_at: 2026-10-14T07:55:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: trebovaniya.md
    version: 91
supersedes: null
---

# US-043 Open Decisions — Fill the group-lessons blank form with a template report

> **Revision v2 (2026-10-14).** The Owner re-authored the Story during
> `HUMAN_SPEC_APPROVAL` of spec v1: nothing is uploaded; the program ships the
> printed form "Бланк для групових занять" and fills it copy by copy.
> OD-001 … OD-005 (written for the upload) are **superseded** and kept below
> for history. OD-006 is resolved by the Story; OD-007 … OD-010 are carried from
> the re-authored Story, all resolved by the Owner on 2026-10-14. OD-011 is
> raised by spec v2 and resolved by the Owner at `HUMAN_SPEC_APPROVAL`;
> OD-012 was raised and resolved by the Owner there.

No item of `trebovaniya.md` §7 (v91: 10, 14, 26, 27, 28) concerns this Story.

## OD-001 Where the layout lives (superseded)

Carried from the Story. §3 says the layout "подключается к шаблону при
экспорте" — stored with the template, or supplied at each export.

Options: (a) stored with the template in the installation database — uploaded
once, used by every export of that template, covered by the database backups
(DC-13), deleted with the template; (b) supplied with each export, never stored.

**Resolution:** (a) stored with the template in the installation database, by
the Owner on 2026-10-13. Spec FR-001, FR-002, FR-009.

**Superseded** on 2026-10-14 by the Owner's re-authoring of the Story (no upload); the spec references above are to spec v1.

## OD-002 How the program knows where each field goes (superseded)

Carried from the Story.

Options: (a) markers typed into cells of the school's file; the Specification
fixes the marker list and the help text; (b) a form naming sheet and start cell
per field; (c) fixed positions, as the prototype — ruled out by §8.

**Resolution:** (a) markers typed into cells of the school's file, by the Owner
on 2026-10-13. Spec FR-004, FR-005.

**Superseded** on 2026-10-14 by the Owner's re-authoring of the Story (no upload); the spec references above are to spec v1.

## OD-003 Which templates can carry a layout (superseded)

Carried from the Story. The built-in "Academic journal" cannot be changed (§3).

Options: (a) only templates created in the school, copies of the built-in one
included; (b) the built-in template too, as a separate per-school setting.

**Resolution:** (a) only templates created in the school, copies of the
built-in one included, by the Owner on 2026-10-13. Spec FR-001.

**Superseded** on 2026-10-14 by the Owner's re-authoring of the Story (no upload); the spec references above are to spec v1.

## OD-004 Which files are accepted (superseded)

Carried from the Story.

Options: (a) `.xlsx` only, at most 1 MB; `.xls`, `.xlsm` and anything else
refused; the file must open as a workbook and mark at least one field; (b) also
`.xlsm` and `.xls`.

**Resolution:** (a) `.xlsx` only, at most 1 MB, by the Owner on 2026-10-13.
Spec FR-003, VR-001.

**Superseded** on 2026-10-14 by the Owner's re-authoring of the Story (no upload); the spec references above are to spec v1.

## OD-005 Is a new version of the requirements needed first (superseded)

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

**Superseded** on 2026-10-14 by the Owner's re-authoring of the Story (no upload); the spec references above are to spec v1.

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

**Resolution:** like printed forms filled by hand — a set number of students
and lessons per copy, overflow to the next copy in the order of the Story's
Scope; all copies in one file; numbering continues; unused rows cleared;
materials never written; the hours-per-semester line (A5–E5) empty; the 24th
lesson column kept for a future final grade. By the Owner on 2026-10-14 (Story
OD-006). Options (a) … (e) above are history of spec v1. Spec v2 FR-005,
FR-006.

## OD-007 Where the form is chosen

Carried from the Story. Options: (a) at export, next to "Export", with students
and lessons per copy (defaults 30 / 23), stored nowhere; (b) as a template
setting.

**Resolution:** (a), by the Owner on 2026-10-14. Spec v2 FR-004, VR-001.

## OD-008 Where field positions live

Carried from the Story. Options: (a) field markers typed into the shipped form
file; the code knows the markers, not the cells (§6 "не хардкод"); (b) cell
addresses in code — needs a new version of `trebovaniya.md`.

**Resolution:** (a), by the Owner on 2026-10-14. Spec v2 FR-001 … FR-003.

## OD-009 Where the marked form file is kept

Carried from the Story. Options: (a) a new file in
`docs/product/report-templates/` (name indicative `group-lessons-blank.xlsx`) —
the school form with markers, no student data — built into the program from
there; `school-journal-full.xlsx` stays untouched as the reference; (b) a file
inside `src/` — needs an exception to the `AGENTS.md` rule.

**Resolution:** (a), by the Owner on 2026-10-14. Spec v2 FR-001 (embedded
resource included from that path).

## OD-010 Name of the fill method

Carried from the Story. Not shown in the UI while it is the only one; named now
so later methods sit beside it.

**Resolution:** "Базовий" / "Basic", by the Owner on 2026-10-14. Spec v2 FR-004,
FR-012.

## OD-011 Upper limit on the number of sheets in one file

Raised by spec v2 (it decided whether the export
has one more refusal, its message and status).

The number of sheets is groups × blocks + blocks (spec FR-006). With the
defaults it is small: 35 students and 100 lessons → 2 × 5 + 5 = **15** sheets.
But the per-copy inputs go down to 1: the same course by 1 student and 1 lesson
→ 35 × 100 + 100 = **3 600** sheets, built in memory on the installation's
server in one request — slow, large, and of no use on paper. Nothing in
`trebovaniya.md` or the Story bounds it.

Options:

- **(a) (Рекомендую)** A fixed limit of **100 sheets** per file. Over it the
  export is refused with a translated message: "too many sheets — increase
  students or lessons per copy, or choose a shorter period"; no file, no audit
  row. 100 sheets is far above any real course with sensible numbers and keeps
  one export cheap.
- (b) No limit — the export always builds every sheet, however many.
- (c) A different number chosen by the Owner.

Impact: spec FR-006, VR-002, AC-014, Error Handling.

**Resolution:** (a) a fixed limit of 100 sheets per file, by the Owner on
2026-10-14.

## OD-012 Copy number on the printed page

Raised by the Owner at `HUMAN_SPEC_APPROVAL` of spec v2: each copy carries its
number on paper, placed so that the room for a future continuous page numbering
of a semester's or year's paper journal stays free. Options asked: which number
(within its kind / position in the file), where (page footer / a cell under the
table), a single copy (print 1 / nothing), the look (digits / with a caption).

**Resolution:** by the Owner on 2026-10-14 — the copy's number within its kind
(3 on "Оцінювання 3"), digits only, in the page footer: bottom right on
"Оцінювання", bottom left on "Теми занять"; a kind with a single copy prints 1.
Story amended; spec v2 FR-006.1, AC-015.

## OD-013 Three story-level tests contradict the approved artifacts

Raised at `IMPLEMENTATION` (implementation report v1, §7): T-1 `AssertFooter`
reads `GetText(XLHFOccurrence.AllPages)`, which ClosedXML 0.105.1 never fills
on load, so it can neither see a copy number nor prove a section empty; T-2
the seed of `ATemplateShowingMaterials_…` never marks a submission late, so
the test fails before the form is read; T-3
`StudentsWithoutLessons_AreStillSplitIntoGroups` expects the course roster in
three copies when no lesson is in the period, while the report then carries no
student and Story AC-007 asks for one copy of each sheet with the course and
teachers only.

Options:

- **(a) (Рекомендую)** Fix the three tests: T-1 read `OddPages`; T-2 a seed
  that reaches a late mark; T-3 expect one copy of each sheet with empty
  student areas (Story AC-007).
- (b) Write the roster without lessons — a spec change and a change to the
  US-027 report builder.

**Resolution:** (a), by the Owner on 2026-10-14 — the procedural exception to
change story-level tests at `IMPLEMENTATION` is authorised for these three
fixes only.
