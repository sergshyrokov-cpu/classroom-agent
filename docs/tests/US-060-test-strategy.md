---
artifact_type: test_strategy
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T14:04:03Z
updated_at: 2026-10-27T14:04:03Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: docs/designs/api/US-060-api-design.md
    version: 1
  - path: docs/designs/database/US-060-db-design.md
    version: 1
supersedes: null
---

# US-060 Test Strategy — Marks in the course list, card, journal, report and export

## 1. Scope

Display only: the marks US-058 / US-059 store are shown on the course list, the course card, the journal
(full and short), the report page and both export layouts, in Ukrainian and English. No endpoint, schema,
role or write changes (API_DESIGN and DB_DESIGN are NOT_APPLICABLE), so there are no contract, migration
or authorization tests to add. The existing authorization, antiforgery and endpoint-enumeration tests
cover the unchanged pages and export (TC-5).

## 2. Test levels

Every test is an **integration test over HTTP** against real PostgreSQL via Testcontainers (TC-2), with
the Google ports substituted (TC-4). The tests check only what can be seen from outside: the rendered
HTML and the exported `.xlsx` read back with ClosedXML from the response bytes.

Why not unit tests on the DTOs or the workbook model: no artifact names the new DTO members, the
workbook-model members (spec FR-007.5 says only "a cell fill colour and a cell note") or the CSS class.
Pinning names the design does not give would be inventing declarations (test-writer skeleton rule).
Observable tests stay valid whatever names IMPLEMENTATION chooses, so **no compile-only skeleton is
needed**.

## 3. Fixtures

- `DataMarksWorld` / `DataMarksHostExtensions.SeedMarksCourseAsync` — the marks course, seeded by SQL:
  a teacher on the roster; the graded item (max 12) and the deleted item (max 10,
  `not_in_classroom_since` set); five students — Overmax (15/12), Draftov (draft 13/12, not returned),
  Equalson (12/12; 7/10), Gonevych (off the roster, last seen L; 8/10), Submitson (off the roster,
  first = last seen; a turned-in submission); a full-view scale template (`TSFULL` at 100 %).
- `CourseHeaderVariant` + `ApplyVariantAsync` — the stored states of FR-003.1: None, NotInClassroom (D1),
  NotVisible (D2, last read R1), Skipped (skip record, last read R2), SkippedNeverRead (skip record, no last
  read), NotInClassroomAndSkipped, ArchivedUnchanged.
- `SeedListCoursesAsync` — four active courses for the list: Absent (NotInClassroom D1), Hidden
  (NotVisible D2), Plain, Teacherless (its only teacher off the roster).
- `DataMarksTexts` — the FR-009 texts verbatim, uk and en, and the date rule of spec §4: the instant
  converted to Kyiv time, shown with the language's short date pattern. Every instant falls at night in
  UTC, so its Kyiv date is the next day (TC-8).
- Language: the Dean chooses it through the real switcher (`ChooseLanguageAsync`).

## 4. Scenarios

**Positive:** each mark appears where FR-001 … FR-008 put it — next to the status in the list and the
card; in place of the teachers; in the cell with a class (screen) or fill + note (file); in the column
heading and the topic; above the journal table, in `#report-header`, in cell C2 of the program layout,
as a note on the form's `{{course}}` cell; next to the name (screen) or as a note on the name cell (file).

**Negative:** an ordinary cell, item, course and student carries no mark. The grade 12/12 is not above
the maximum (VR-001, equal). The short journal shows no draft label (BR-057). Students on the roster are
not "left". The form's printed text never contains a mark (FR-008.5). The marks course makes no Google
call.

**Boundary:**
- equal to the maximum (12/12);
- Kyiv date ≠ UTC date for every dated mark;
- leaver with first = last seen → undated "left" (OD-003 a);
- no last complete read → undated "Data not updated" (OD-002 a, AC-011);
- header precedence when both "not in Classroom" and skipped (I-2, AC-012);
- archived, unchanged course → no line (FR-003.3).

**Validation:** none — the Story adds no input (spec §6).

**Security:**
- read-only mode in three causes: the list and the export work and show the marks;
- no write to the teaching tables (fingerprint check);
- no Google call (BR-026, SC-7).

**Persistence:** none new — the marks are read from existing columns.

## 5. Excluded scenarios

- The highlight colour itself on screen: CSS rendering cannot be read from HTML. The tests prove that
  the cell has a class the ordinary cell lacks, and that the label is present (FR-004.5: the highlight
  never relies on colour alone). The visual check goes to IMPLEMENTATION_VERIFICATION.
- The exact fill colour value: the tests pin "coloured unlike an ordinary cell" and "the same colour in
  both layouts" (FR-007.5). The spec names no colour.
- Multi-copy blank-form notes (FR-008.5, second sentence): the world fits in one copy. The per-cell
  mapping is the same for every copy; the existing US-043 copy tests keep guarding the copy split.

## 6. Known limitations

- The spec does not fix the exact HTML placement of the course and card marks (only "next to the
  status"). The tests require the mark after the status and, in the list, before the teacher.
- Program-layout header line: only the grading sheet is checked (FR-007.1 says "the Course header row").
- Four tests pass before implementation by design: they are the variants that expect no line (None,
  ArchivedUnchanged) — see the report.

## 7. Open Decisions affecting testing

None open. OD-001 a, OD-002 a and OD-003 a are applied as resolved.
