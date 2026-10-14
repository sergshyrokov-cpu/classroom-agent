---
artifact_type: implementation_report
story: US-043
version: 2
status: DRAFT
created_at: 2026-10-14T07:46:29Z
updated_at: 2026-10-14T08:10:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
  - path: docs/designs/api/US-043-api-design.md
    version: 1
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
  - path: docs/designs/database/US-043-db-design.md
    version: 1
  - path: docs/tests/US-043-test-strategy.md
    version: 1
  - path: docs/tests/US-043-ac-test-matrix.md
    version: 1
supersedes: docs/evidence/US-043-implementation-report.md@v1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-043 Implementation Report — Fill the group-lessons blank form

> **v2 (2026-10-14).** v1 returned BLOCKED on three story-level test defects (§7). The Owner resolved OD-013 (a): the
> three tests were fixed as proposed; no production code changed after v1. Full suite green — verdict **PASS**.

## 1. Summary

The export of the report page gains the layout choice "Program layout" / "Бланк для групових занять"; with the form, the
same report is laid out over copies of the shipped, marked form (fill method Basic), numbered in the page footer, and
returned as one `.xlsx` with the US-028 response, audit row and authorization. Everything in the Specification is
implemented. Build and format are clean; the full suite is green (5640 / 5640) after the three test fixes of OD-013.
Verdict **PASS**.

No schema change (DB design NOT_APPLICABLE), no new package, no new endpoint or policy.

## 2. Source Artifacts

As in the front matter: Story, spec v2 (APPROVED), open decisions v2 (APPROVED), API design + OpenAPI v1, DB design v1
(NOT_APPLICABLE), test strategy + AC test matrix v1, test generation report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `GroupLessonsBlankMapper.Map`, `ClosedXmlBlankFormRenderer.RenderAsync` | `ASmallReport_FillsOneCopyOfEachSheet_WithTheScreensValues` | pass¹ |
| AC-002 | `GroupLessonsBlankMapper.Map` (copy order, numbering) | `ALargeReport_IsSplitIntoTheStoryTablesEightSheets` | pass¹ |
| AC-003 | `GroupLessonsBlankMapper.Map` (Empty placements), renderer `Write` → `Clear(Contents)` | `APartlyFilledCopy_…`, `OnePerCopy_IsTheLowerBoundary` | pass¹ |
| AC-004 | `ExportJournalCommand.TryReadLayout`; program path unchanged | `TheProgramLayout_IsExactlyTheUs028File` (5) | pass |
| AC-005 | `ExportJournalCommand.TryReadLayout` / `TryReadPerCopy`; controller `FieldName`, `MessageKey`; `JournalExportLog.Refused` | `AnInvalidPerCopyValue_…` (13), `AnUnknownLayout_…` (4), `ANonStringLayout_…`, `SeveralFailures_…`, `ARejectedValue_IsNotEchoed`, logging refusals | pass |
| AC-006 | `GroupLessonsBlankMapper` (non-material columns only), `ReportWorkbookMapper.GradingCell` | `ATemplateShowingMaterials_…` | pass (seed fixed, T-2) |
| AC-007 | `GroupLessonsBlankMapper` (`Groups` = max(1, …)) | `AnEmptyReport_…`, `LessonsWithoutStudents_…`, `NoLessonInThePeriod_IsOneCopyOfEach_WithoutStudents` (T-3) | pass |
| AC-008 | `ExportJournalCommand` (no guard, audit row as US-028) | `InReadOnlyMode_TheFormExportWorks_…` (3) | pass¹ |
| AC-009 | unchanged policy `UseReportTemplates` | `GroupLessonsBlankAuthorizationTests` (5) | pass¹ |
| AC-010 | `WorkbookCell.OfText` quote prefix; renderer `IncludeQuotePrefix` | `FormulaLikeText_IsWrittenAsQuotePrefixedText` | pass¹ |
| AC-011 | structural (synthetic PostgreSQL data, workbook read by cells) | all of the above | pass |
| AC-012 | host-wide `no-store` (nothing added) | `ASmallReport_…` | pass¹ |
| AC-013 | `docs/product/report-templates/group-lessons-blank.xlsx`, `EmbeddedResource` in Infrastructure csproj | `GroupLessonsBlankFormTests` (10) | pass |
| AC-014 | `ExportJournalCommand` (sheet limit after the report, before rendering), `JournalExportOutcome.TooManySheets` | `MoreThanAHundredSheets_…`, `ExactlyAHundredSheets_…`, `AnUnknownCourse_Is404_BeforeTheSheetLimit`, `TooManySheets_IsAWarningNamingTheRule` | pass |
| AC-015 | renderer: footer section `AddText(number, AllPages)` | footer assertions in `AssertFooter` | pass¹ (see T-1) |

FR-004 / FR-012 page and translations: `GroupLessonsBlankPageTests` (7), `GroupLessonsBlankTranslationTests` (5) pass.
FR-011 logging: `GroupLessonsBlankLoggingTests` (5) pass.

¹ With the footer reader of `AssertFooter` corrected (T-1, OD-013); with the v1 helper these 16 tests failed at line
210 although the file was correct.

## 4. Change Set

| File | Trace |
|---|---|
| `docs/product/report-templates/group-lessons-blank.xlsx` (new) | FR-001, FR-002, I-4, I-14, AC-013 — made from `school-journal-full.xlsx` by editing only: 10 marker cells (shared strings), number format `dd.mm.yyyy` on `Оцінювання!C8` and `Теми занять!B4` (two new cell styles, copies of the originals), footer margin 0.5 cm on both sheets |
| `docs/product/report-templates/README.md` | FR-001 (one line naming the file) |
| `src/ClassroomAgent.Infrastructure/ClassroomAgent.Infrastructure.csproj` | FR-001, I-1 (embedded resource) |
| `src/ClassroomAgent.Infrastructure/Export/ClosedXmlBlankFormRenderer.cs` (new) | FR-002, FR-003, FR-005.3, FR-005.6, FR-006, FR-006.1 |
| `src/ClassroomAgent.Application/Ports/IBlankFormRenderer.cs` (new) | api-design §8 (the form renderer port), AD-4 |
| `src/ClassroomAgent.Application/Models/Export/{ExportLayout,FillMethod,BlankFormMarker,BlankFormSheet,BlankFormFooterSection,BlankFormPlacement,BlankFormCopy,BlankFormFill}.cs` (new) | FR-004 (layout, fill method OD-010), FR-005 fill model, FR-006.1 |
| `src/ClassroomAgent.Application/UseCases/GroupLessonsBlankMapper.cs` (new) | FR-003, FR-005, FR-006, VR-001 limits, VR-002 count |
| `src/ClassroomAgent.Application/UseCases/ReportWorkbookMapper.cs` | FR-005.2–FR-005.4 "as the header / screen shows them": course, teachers, student name and `GradingCell` exposed `internal` and reused — no behaviour change |
| `src/ClassroomAgent.Application/UseCases/ExportJournalCommand.cs` | VR-001, VR-002, FR-007, FR-010, FR-011 |
| `src/ClassroomAgent.Application/UseCases/GetReportQuery.cs` | openapi `ExportActionChange` |
| `src/ClassroomAgent.Application/Models/Requests/JournalExportRequest.cs` | api-design §2.3 (three optional members) |
| `src/ClassroomAgent.Application/Models/Dtos/{ExportAction,ExportField,ExportMessageKey}.cs`, `BlankFormInputs.cs` (new) | openapi `BlankFormInputs`, api-design §5 |
| `src/ClassroomAgent.Application/Models/{JournalExportOutcome,JournalExportSummary}.cs`, `BlankFormExportFacts.cs` (new) | VR-002, FR-011 |
| `src/ClassroomAgent.Application/Localization/SharedResource.{uk,en}.resx` | FR-012 (10 keys each) |
| `src/ClassroomAgent.Web/Controllers/JournalExportController.cs` | api-design §2.3–§2.5, openapi 400 causes |
| `src/ClassroomAgent.Web/Security/JournalExportLog.cs`, `JournalExportBlankFormLog.cs` (new) | FR-011 |
| `src/ClassroomAgent.Web/Security/JournalExportTextKeys.cs` | FR-012, api-design §5 |
| `src/ClassroomAgent.Web/Views/Report/Index.cshtml`, `wwwroot/js/report-export.js` | FR-004, api-design §2.7 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI registration (supporting) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/JournalExportWorld.cs` | supporting: the command gains the renderer port (test report note "keep … compiling") |
| `tests/ClassroomAgent.Tests/TestInfrastructure/GroupLessonsBlankHostExtensions.cs` | OD-013 T-1: `AssertFooter` reads `OddPages` |
| `tests/ClassroomAgent.Tests/Web/Pages/GroupLessonsBlankExportTests.cs` | OD-013 T-2 (seed 5 × 4), T-3 (test replaced by `NoLessonInThePeriod_IsOneCopyOfEach_WithoutStudents`, AC-007) |
| `docs/tests/US-043-ac-test-matrix.md` | OD-013 T-3: the AC-007 row names the replaced test |
| `docs/decisions/US-043-open-decisions.md` | OD-013 recorded with the Owner's resolution |

No secret, generated database file, generated export or IDE file is in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Form file | `dotnet test … --filter FullyQualifiedName~GroupLessonsBlankFormTests` | 10 / 10 pass |
| Story + export tests | `dotnet test … --filter "…GroupLessonsBlank|…ExportJournalCommandTests|…JournalExport"` | 251: 233 pass, 18 fail (16 at `AssertFooter`, T-2, T-3) |
| Full suite | `dotnet test ClassroomAgent.sln` (with `AssertFooter` reading `OddPages` **temporarily**, then restored byte-for-byte) | 5640: 5638 pass, 2 fail (T-2, T-3), 0 skipped; 6 min 32 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <Application, Infrastructure, Web>` | exit 0 |
| Full suite, final (after OD-013) | `dotnet test ClassroomAgent.sln` | 5640: 5640 pass, 0 fail, 0 skipped; 6 min 46 s |
| Format, final | `dotnet format … --verify-no-changes --include <Application, Infrastructure, Web, changed test files>` | exit 0 |
| ClosedXML footer behaviour | scratch console (ClosedXML 0.105.1): `AddText("7", AllPages)`, save, reload | XML `oddFooter`/`evenFooter`/`firstFooter` = `&R7`; after reload `GetText(AllPages)` = `""`, `GetText(OddPages)` = `"7"` |

No regression outside US-043: every other test of the suite passes.

Delegated (cheap-worker): every build, test and format run (6 runs). Inline: design, all production code, the form file
(scripted XML edit of the reference), the ClosedXML footer experiment, failure triage, this report.

## 6. Configuration Changes

None (no `appsettings` change). The form is an embedded resource, not configuration.

## 7. Deviations and Discovered Problems

**T-1 — `AssertFooter` cannot read a footer (test defect).** `GroupLessonsBlankHostExtensions.AssertFooter` reads every
section with `GetText(XLHFOccurrence.AllPages)`. In ClosedXML 0.105.1 that occurrence is never stored on load (a loaded
footer is held per odd / even / first page), so it returns `""` for any file — every "number expected" assertion fails
and every "section empty" assertion is vacuous. The produced file is correct (§5 experiment). Fix: read
`XLHFOccurrence.OddPages` (6 lines). Verified: with that change all 16 affected tests pass.

**T-2 — `ATemplateShowingMaterials_…` cannot reach the form (test data defect).** Line 388 requires a multi-line grade
cell on the *screen* (US-028 file) before the form is looked at. The seed marks a submission late when
`(s + l) % 9 == 0`; with 4 students and 4 lessons `s + l ≤ 8`, so no late mark exists and no cell has two lines. The
failure is independent of this implementation. Fix: a seed size reaching 9 (e.g. 5 × 4), or a different late rule.

**T-3 — `StudentsWithoutLessons_AreStillSplitIntoGroups` contradicts the Story (artifact conflict).** With no lesson
in the period the report is in the "nothing published" state and carries no student (US-027 FR-005.7). Story AC-007:
"one copy of each sheet holding the course name and teachers and nothing else". Spec FR-005 builds the form from that
very report ("nothing computed again"). The test instead expects the course roster split into three copies, which needs
a roster the report does not have — changing the US-027 report builder or computing students a second way, both outside
this Story. The implementation follows the Story (authority order: Story above Specification): one copy of each sheet.
Spec I-6 is still honoured where the report itself has students and no lesson (only materials in the period). Fix: the
test expects `["Оцінювання", "Теми занять"]` with empty student areas — or, if the Owner wants the roster written, an
Open Decision and a spec change first.

**Resolution (OD-013 a, Owner, 2026-10-14):** the three tests were fixed exactly as proposed above; T-3 follows Story
AC-007.

## 8. Open Decisions

OD-013 raised here and resolved (a) by the Owner. No open decision remains.
