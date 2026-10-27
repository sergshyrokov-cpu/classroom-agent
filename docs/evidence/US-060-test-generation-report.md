---
artifact_type: test_generation_report
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T14:04:03Z
updated_at: 2026-10-27T14:04:03Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: docs/designs/api/US-060-api-design.md
    version: 1
  - path: docs/designs/database/US-060-db-design.md
    version: 1
  - path: docs/tests/US-060-test-strategy.md
    version: 1
  - path: docs/tests/US-060-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-060 Test Generation Report

## 1. Files

**Created** (all under `tests/ClassroomAgent.Tests/`):

- `TestInfrastructure/DataMarksTexts.cs` — the FR-009 texts (uk, en) and the date rule.
- `TestInfrastructure/DataMarksWorld.cs` — the marks course: titles, surnames, instants, expected header line.
- `TestInfrastructure/CourseHeaderVariant.cs` — the stored states of FR-003.1.
- `TestInfrastructure/DataMarksHostExtensions.cs` — SQL seeding, the variants, and the HTML and xlsx cell readers.
- `Web/Pages/CourseListMarksPageTests.cs` — 6 cases.
- `Web/Pages/JournalMarksPageTests.cs` — 19 cases.
- `Web/Pages/ReportMarksPageTests.cs` — 16 cases.
- `Web/Pages/ReportMarksExportTests.cs` — 15 cases.
- `Web/Pages/BlankFormMarksExportTests.cs` — 14 cases.
- `Web/UseCases/DataMarksReadOnlyTests.cs` — 3 cases.

**Modified:** none. No existing test was changed.

**Compile-only skeleton:** none. The tests read only rendered HTML and exported files, so they reference
no new production declaration (strategy §2). Production code is untouched.

## 2. Execution

Delegated to a `cheap-worker` subagent (Sonnet). Test design, fixtures and test code were written
inline; the subagent ran the build and the test classes only.

- `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build --filter FullyQualifiedName~<Class>`, one class per call:

| Class | Total | Passed | Failed |
|---|---|---|---|
| JournalMarksPageTests | 19 | 2 | 17 |
| ReportMarksPageTests | 16 | 2 | 14 |
| ReportMarksExportTests | 15 | 2 | 13 |
| BlankFormMarksExportTests | 14 | 2 | 12 |
| CourseListMarksPageTests | 6 | 0 | 6 |
| DataMarksReadOnlyTests | 3 | 0 | 3 |
| **Total** | **73** | **8** | **65** |

The subagent's summary said "78 / 4 / 74"; the per-class rows above are its actual counts and sum to
73 / 8 / 65. The 8 passes are listed in §3.

No Testcontainers, Npgsql or Docker failure occurred, and there were no hangs.

## 3. Red-phase verdict

**Expected failures (65).** Every failure is an assertion on text, a class, a fill or a note that does
not exist yet:

- the label is not in the cell;
- the heading has no item mark;
- the row heading has no "left" mark;
- no header line;
- cell C2 is empty;
- notes are `null`;
- "cell must be coloured".

The failure messages also confirm that the fixtures are valid:

- the pages render the seeded course, the students and both items (e.g. `"Test Marks Deleted 9/6/2026
  max. 10"` in English, `"Gonevych Test"` in the report);
- the date format the tests expect matches the one the pages already use (`9/6/2026` en, `06.09.2026` uk);
- the grade cells hold the values the tests pin (`"15 / 12"`, `TSFULL`);
- Dean sign-in and the export work in all three read-only causes.

No failure comes from compilation, setup or a contradiction with the artifacts.

**Passing by design (8):** the `None` and `ArchivedUnchanged` variants of each of the four header-line
tests (journal, report, program export, blank form). They assert that **no** line appears where FR-003.1
gives none (AC-012, FR-003.3). Today no line appears at all, so these pass. They become meaningful
guards once the lines exist: they then fail if an archived, unchanged course or an unmarked course gets
a line. This "nothing happens" kind of test was examined: the red cases of the same tests prove the same
code path once implemented.

**Regression.** No production or existing test file changed, and the build is clean. The full suite was
not re-run at this stage; its last green state is US-059 (`e24b8c6`, 6363/6363). IMPLEMENTATION runs the
full suite.

## 4. Untested Acceptance Criteria

None. Every AC-001 … AC-012 maps to red tests (matrix). Partial coverage is in strategy §5:

- the on-screen colour is proven by a class, not a rendered colour;
- the blank-form notes are checked on a single copy.

## 5. Open Decisions

None open. Review findings US-060/SPEC_REVIEW/m-1 (above maximum requires maximum > 0) and m-3 (the
digest's card / journal leaver dates) concern the Specification and its digest. They stay OPEN. The
tests follow the approved spec: VR-001 is exercised with maximum 12; a zero maximum has no case, since
ungraded items have no maximum.

## 6. Result

PASS — all AC covered, tests compile, red phase verified, no skeleton needed.
