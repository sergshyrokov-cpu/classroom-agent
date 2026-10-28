---
artifact_type: test_generation_report
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:15:34Z
updated_at: 2026-10-28T17:15:34Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/tests/US-063-test-strategy.md
    version: 1
  - path: docs/tests/US-063-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-063 Test Generation Report

## 1. Files

Created (no file modified, no production file touched):

- `tests/ClassroomAgent.Tests/TestInfrastructure/GeneratedLinesTexts.cs` — FR-006 texts (uk, en), format, stems
- `tests/ClassroomAgent.Tests/Web/Pages/GeneratedLinesPageTests.cs` — 13 cases
- `tests/ClassroomAgent.Tests/Web/Pages/GeneratedLinesExportTests.cs` — 10 cases
- `tests/ClassroomAgent.Tests/Web/UseCases/GeneratedLinesReadOnlyTests.cs` — 3 cases

**Compile-only skeleton: none.** The tests reach the behaviour through HTTP and the produced files only; they reference
existing test infrastructure and the DOM ids / cells the Specification names (FR-003, FR-004, FR-005).

## 2. Delegation

Test design, artifacts and test code written inline (the assertions pin exact spec texts and cells; a brief would have
been as long as the code). Docker check and every test run delegated to a `cheap-worker` subagent; one
`dotnet build` run inline after Docker Desktop had to be started.

## 3. Commands and results

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `docker info` | first: daemon not running → Docker Desktop started; then server 29.8.0 |
| `dotnet test --project tests/ClassroomAgent.Tests --filter "FullyQualifiedName~GeneratedLinesPageTests"` | 13 total, 1 passed, 12 failed (~18 s) |
| `… ~GeneratedLinesExportTests` | 10 total, 2 passed, 8 failed (~23 s) |
| `… ~GeneratedLinesReadOnlyTests` | 3 total, 0 passed, 3 failed (~10 s) |

No environment errors (no Npgsql timeouts, containers started normally).

## 4. Red-phase classification

**Expected failures (23)** — all assertion failures caused by missing production behaviour:

- Page and read-only tests: `#journal-generated` / `#report-generated` / `#…-data-as-of` absent — Actual `""`, Expected
  e.g. «Сформовано: 17.09.2026 11:00», "Generated: 9/17/2026 11:00", «Дані з Google на: 08.09.2026 01:00».
- Export tests: the course-cell note lacks the two lines — Actual `[]`; for AC-004 Actual is
  `["Курсу немає в Classroom з 15.09.2026"]` (the US-060 note is already there, the two lines are missing — the
  fixture is right).
- `TheGeneratedLine_IsTheInstantTheCellsWereBuiltFor` fails at the first "generated" assertion, after its cell
  assertion «термін ще не минув» passed — the due-item fixture is valid; the second half (after the clock advance) is
  reached once the line exists.

**Green-by-design (3)**, documented in the matrix:

- `WithNoCourseSelected_TheJournalShowsNeitherLine` — a negative guard (FR-003.1): no line exists today, and none may
  appear after implementation without a course.
- `NoPrintedCell_HoldsEitherLine` (uk, en) — a negative guard (AC-003): nothing is printed today; it turns red if
  implementation writes a line into a printed cell instead of a note. Its positive counterpart (the note) is red.

**Unexpected failures: none.**

## 5. Regression

No production or existing test file changed, so the existing suite is unaffected by this stage; the full suite was not
re-run here (IMPLEMENTATION runs it). Existing layout suites (`JournalExportTests`, `GroupLessonsBlankExportTests`,
`ReportMarksExportTests`, `BlankFormMarksExportTests`, `JournalMarksPageTests`, `ReportMarksPageTests`) are the
evidence for "otherwise unchanged" in AC-003 and must stay green after implementation.

## 6. Coverage

Every AC-001 … AC-009 maps to at least one red test (`US-063-ac-test-matrix.md`). Untested ACs: none.

## 7. Open Decisions

None new. OD-001…OD-003 resolved a). SPEC_REVIEW findings m-1, m-2 do not affect the tests and are not closed here.

## 8. Overall result

PASS — red phase verified.
