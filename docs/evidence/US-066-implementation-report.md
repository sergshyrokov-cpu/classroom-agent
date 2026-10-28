---
artifact_type: implementation_report
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T19:04:17Z
updated_at: 2026-10-28T19:04:17Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/designs/api/US-066-openapi.yaml
    version: 1
  - path: docs/designs/database/US-066-db-design.md
    version: 1
  - path: docs/tests/US-066-test-strategy.md
    version: 1
  - path: docs/tests/US-066-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-066 Implementation Report

## 1. Summary

Namesake rows of one journal or report now carry the student's stored email, decided in `Application`
(`NamesakeRule`, spec FR-002, FR-003); the journal and report views and the workbook mapper (program
layout and, through the shared `StudentCell`, the blank) render it as `<name> (<email>)` through one
helper (`NamesakeLabel.Compose`, FR-004, api-design D-3). No endpoint, policy, translation key, schema,
log or audit change. Build clean, full suite 6798/6798, format clean.

`security_sensitive: false` — an email already shown to the same roles on the course card is now also
shown next to a namesake's name; no new role, destination, log or audit content (spec §7).

## 2. Source artifacts

As in `inputs`; Open Decisions OD-001 a), OD-002 a) resolved. No OPEN finding of US-066.

## 3. Acceptance Criteria

| AC | Implementation | Tests (all green) |
|---|---|---|
| AC-001 | `GetJournalQuery.ExecuteAsync` (rows → `NamesakeRule.Find`, `JournalRow.NamesakeEmail`); `Views/Journal/Index.cshtml` row heading | `NamesakeJournalPageTests.TwoStudentsWithTheSameName_…` (uk, en) |
| AC-002 | `ReportBuilder` (student rows → `NamesakeRule.Find`, `GradingRow.NamesakeEmail`); `Views/Report/Index.cshtml` | `NamesakeReportPageTests.TwoStudentsWithTheSameProfileName_…` (uk, en) |
| AC-003 | `ReportWorkbookMapper.StudentCell` | `NamesakeReportExportTests.TheNamesakesNameCells_…`, `OnlyTheNameCellsText_Changes_TheLayoutIsTheSame` |
| AC-004 | same `StudentCell`, called by `GroupLessonsBlankMapper`; rule judged on the whole report before copies | `NamesakeBlankExportTests.TheBlanksNameCells_…`, `NamesakesInDifferentCopies_BothShowTheirEmail` |
| AC-005 | `NamesakeRule.Find` — `StringComparer.Create(uiCulture, ignoreCase: true)` | `NamesDifferingOnlyInCase_AreNamesakes` (journal, report; uk, en) |
| AC-006 | `NamesakeLabel.Compose` returns the name unchanged when the email is null | the four `NoNamesakes_…` tests |
| AC-007 | rule runs on the labels after the name source | `TheNameVariantDecides_WhoIsANamesake`, `TheSamePartBeforeTheAt_AtTwoDomains_…` |
| AC-008 | `NamesakeRule.Email` — null for an empty or missing email | `ANamesakeWithoutAnEmail_…` (journal, report, export) |
| AC-009 | the "left" mark/note untouched, rendered after the composed name | `ANamesakeWhoLeft_…`, `ALeavingNamesakesCell_…` (journal, report, export, blank) |

FR-001 / OD-002 a (teachers): the field is on the student row only (`GradingRow`, not `PersonName`) —
`TwoTeachersWithTheSameName_AreShownWithoutAnEmail`. FR-002.5: `GetJournalQuery` passes the email only for
`JournalNameKind.FullName` rows — `RowsLabelledByTheirEmail_GetNoSecondEmail`. FR-005: ordering unchanged
(the existing case-sensitive sort runs first; the rule only reads the ordered list).

## 4. Change set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Application/UseCases/NamesakeRule.cs` | new | FR-002, FR-003, VR-001 |
| `src/ClassroomAgent.Application/Models/Dtos/NamesakeLabel.cs` | new | FR-004.1, FR-004.2; api-design D-3 |
| `src/ClassroomAgent.Application/Models/Dtos/JournalRow.cs` | `NamesakeEmail` (optional, last) | api-design D-1; openapi `JournalRow.namesakeEmail` |
| `src/ClassroomAgent.Application/Models/Dtos/GradingRow.cs` | `NamesakeEmail` (optional, last) | api-design D-1, D-2; openapi `GradingRow.namesakeEmail` |
| `src/ClassroomAgent.Application/UseCases/GetJournalQuery.cs` | rows materialised, rule applied | AC-001, FR-002.5 |
| `src/ClassroomAgent.Application/UseCases/ReportBuilder.cs` | student rows materialised (`students`), rule applied | AC-002, FR-002.1 |
| `src/ClassroomAgent.Application/UseCases/ReportWorkbookMapper.cs` | `StudentCell` composes the name | AC-003, AC-004, FR-004.4 |
| `src/ClassroomAgent.Web/Views/Journal/Index.cshtml` | row heading composes | AC-001, FR-004.3 |
| `src/ClassroomAgent.Web/Views/Report/Index.cshtml` | row heading composes | AC-002, FR-004.3 |
| tests and test artifacts | from TEST_WRITING, unchanged | `ac_test_matrix` |
| `docs/evidence/US-066-implementation-report.md` | this report | workflow artifact |

No secret, generated database file or IDE config in the change set. No test was modified.

## 5. Validation evidence

- `dotnet build ClassroomAgent.sln` — 0 errors, 0 warnings (after the D-1 fix; the final run after the
  `StudentCell` reshaping also 0/0).
- Story classes, one per run: NamesakeJournalPageTests 9/9, NamesakeReportPageTests 11/11,
  NamesakeReportExportTests 7/7, NamesakeBlankExportTests 4/4.
- `dotnet test ClassroomAgent.sln --no-build` — exit 0, **6798 total, 6798 passed, 0 failed, 0 skipped**
  (9 m 57 s).
- `dotnet format ClassroomAgent.sln --verify-no-changes` — first exit 2 (whitespace in
  `ReportWorkbookMapper.cs`, the `with` expression split over lines); `StudentCell` rewritten as a block
  body with a local (same behaviour); rerun exit 0. After that change the build is 0/0 and
  NamesakeReportExportTests, NamesakeBlankExportTests and ReportWorkbookMapperTests were rerun green; the
  US-060 export classes (ReportMarksExportTests, BlankFormMarksExportTests) were rerun as well — results in
  §5.1.

Delegated: the first build (reported the D-1 errors), the four story classes, the full suite and the format
check ran on `cheap-worker` subagents; the code itself, the fix of D-1, the format fix and the final reruns
were done inline.

### 5.1 Rerun after the format change

One command, one class per run, after the `StudentCell` change: NamesakeReportExportTests, NamesakeBlankExportTests,
ReportWorkbookMapperTests, BlankFormMarksExportTests — exit 0, 0 failed. ReportMarksExportTests — exit 2, 1 failed,
the run taking 14 m 43 s (the class normally takes well under a minute), which points to an environment stall
(Docker/WSL, cf. the project's known Npgsql/Testcontainers hangs); the failure text was not captured (only the
counts were filtered). Rerun alone: exit 0, **15/15 passed**. The same class was green in the full-suite run above,
and the change between the two runs only reshaped `StudentCell` into a block body with identical output (the
Namesake export classes, which pin the same cell, are green). Recorded as INFO-1.

## 6. Configuration changes

None.

## 7. Deviations and discovered problems

- **D-1** First build: `ReportBuilder` already had a local `ordered` (lessons); the new student list was
  renamed `students`. Compile-time only.
- **INFO-1** One run of the US-060 class ReportMarksExportTests failed once after a 14-minute stall (§5.1), its
  message not captured; green alone (15/15) and in the full suite. Environmental by every sign; noted in case it
  recurs.
- No deviation from the Specification or the designs. `NamesakeLabel` is the "one composition helper in
  `Application`" api-design D-3 recommends; it lives in `Models.Dtos` because the Razor views already import
  that namespace and `UseCases` types are internal.

## 8. Open Decisions

None touched or required.
