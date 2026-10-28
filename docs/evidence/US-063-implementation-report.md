---
artifact_type: implementation_report
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:49:17Z
updated_at: 2026-10-28T17:49:17Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
  - path: docs/designs/api/US-063-api-design.md
    version: 1
  - path: docs/designs/database/US-063-db-design.md
    version: 1
  - path: docs/tests/US-063-test-strategy.md
    version: 1
  - path: docs/tests/US-063-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-063 Implementation Report

## 1. Summary

The journal page and the report header show «Сформовано: …» and «Дані з Google на: …»; the program-layout export
(both sheets) and every `{{course}}` cell of the blank form carry both lines in a note on the course-name cell, after
US-060's note where there is one. "Generated" is the build's own B — `GetJournalQuery`'s for the journal,
`ReportBuilder`'s for the report and the export — carried in a new DTO `CourseDataStamp`, never re-read from the
clock (FR-001). "Data as of" is `Course.LastCompleteReadAt`, «невідомо»/"unknown" when null. Both are converted to the
school zone in Application and formatted as `ShortDatePattern + " HH:mm"`. No schema, endpoint, policy, audit or log
change. Build, full suite and format check pass.

## 2. Source Artifacts

As in front matter. API_DESIGN and DB_DESIGN are NOT_APPLICABLE (v1 records).

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `DataMarkRules.Stamp`; `GetJournalQuery` (stamp from B and the selected record); `ReportBuilder` (header `Stamp`); views `Journal/Index.cshtml` `#journal-stamp`, `Report/Index.cshtml` `#report-generated`, `#report-data-as-of`; `DataMarksTexts.Generated` / `DataAsOf` | GP `TheJournal_ShowsGeneratedAndDataAsOf_AboveTheTable`, `TheJournalLines_AreInKyivTime_WithoutSeconds`, `TheReportHeader_ShowsGeneratedAndDataAsOf`, `NextToTheCourseHeaderLine_…` | green |
| AC-002 | the same `b` passed to `Stamp` and to every `Cell(…)` in `GetJournalQuery` | GP `TheGeneratedLine_IsTheInstantTheCellsWereBuiltFor` | green |
| AC-003 | `ReportWorkbookMapper.CourseNote`, `HeaderBlock` (note on the Course value cell; used by both sheets); `GroupLessonsBlankMapper` (note on every `{{course}}` placement) | GX `TheProgramLayout_…BothSheets`, `TheBlankForm_…CourseCell`, `NoPrintedCell_HoldsEitherLine` | green |
| AC-004 | `CourseNote(existing, …)`: existing text first, `\n`-joined, no empty line | GX `AnExistingNote_IsKeptFirst_ThenBothLines` | green |
| AC-005 | `SharedResource.en.resx` keys; culture-based formatting | every `en` case | green |
| AC-006 | `CourseDataStamp.DataAsOf` null → `DataAsOfUnknown` (screen and export) | GP `WithNoLastReadRecorded_…Unknown`, GX `WithNoLastReadRecorded_TheNotesSayUnknown` | green |
| AC-007 | no guard, no write added; queries unchanged in that respect | GR `InReadOnlyMode_…` (3 causes) | green |
| AC-008 | one `course` cell value reused for every grading copy's placement | GX `EveryGradingCopy_CarriesTheNote` | green |
| AC-009 | `Stamp` reads `LastCompleteReadAt` only | GP `ForAnUnchangedArchivedCourse_…` | green |

## 4. Change Set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/Models/Dtos/CourseDataStamp.cs` (new) | FR-001, FR-002, AD-8 (spec §9: B and last read travel in DTOs) |
| `src/ClassroomAgent.Application/Models/Dtos/JournalPageModel.cs` | FR-003.1 (`Stamp`) |
| `src/ClassroomAgent.Application/Models/Dtos/ReportHeader.cs` | FR-003.2, FR-004, FR-005 (`Stamp`) |
| `src/ClassroomAgent.Application/UseCases/DataMarkRules.cs` | FR-001, FR-002, VR-001, VR-002 (`Stamp`) |
| `src/ClassroomAgent.Application/UseCases/GetJournalQuery.cs` | FR-001.2, FR-003.1 |
| `src/ClassroomAgent.Application/UseCases/ReportBuilder.cs` | FR-001.2 |
| `src/ClassroomAgent.Application/UseCases/ReportWorkbookMapper.cs` | FR-004, FR-006.1, FR-006.2, VR-003, VR-004 (`CourseNote`, `DateTimeText`, `HeaderBlock`) |
| `src/ClassroomAgent.Application/UseCases/GroupLessonsBlankMapper.cs` | FR-005.1 … FR-005.3 |
| `src/ClassroomAgent.Application/Models/Export/ReportText.cs` | FR-006.3 (three export texts) |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-006.3, NFR-073 (translation entries) |
| `src/ClassroomAgent.Web/Security/DataMarksTextKeys.cs` | FR-006.3 (keys) |
| `src/ClassroomAgent.Web/Security/LocalizedReportTexts.cs` | FR-006.3 (export text → key) |
| `src/ClassroomAgent.Web/Security/DataMarksTexts.cs` | FR-003, FR-006.1 (screen wording) |
| `src/ClassroomAgent.Web/Views/Journal/Index.cshtml` | FR-003.1, FR-003.4 |
| `src/ClassroomAgent.Web/Views/Report/Index.cshtml` | FR-003.2, FR-003.4 |
| `tests/ClassroomAgent.Tests/Web/Pages/BlankFormMarksExportTests.cs` | adapted, see §7 D-1 (FR-005.2, I-5) |
| test files of TEST_WRITING (4 new) | `ac_test_matrix` |
| `docs/evidence/US-063-implementation-report.md`, test artifacts, workflow state | workflow artifacts (Git Policy closed list) |

No secret, database file or IDE config in the change set.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors (after the last edit too) |
| story + neighbour classes, one `FullyQualifiedName~` each: GeneratedLinesPage/Export/ReadOnly, JournalMarksPage, ReportMarksPage, ReportMarksExport, DataMarksReadOnly, JournalExport, GroupLessonsBlankExport, JournalPage, ReportPage | all green (13/13, 10/10, 3/3, 19/19, 16/16, 15/15, 3/3, 24/24, 47/47, 16/16, 11/11) |
| same, `BlankFormMarksExportTests` | first run 6/14 (D-1); after the adaptation 14/14 |
| `dotnet test ClassroomAgent.sln` | 6767 total, 6766 passed, 1 failed, 0 skipped (10 m 39 s) — the failure is the known flaky `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` (`Assert.NotNull` at line 103; US-043 INFO-1); the class rerun alone: 10/10 |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | first exit 2 (WHITESPACE in `GroupLessonsBlankMapper.cs:86`, a `with` expression across two lines); rewritten with a local; rerun exit 0; `GeneratedLinesExportTests` rerun 10/10 |

The red-phase baseline of the story tests was recorded at TEST_WRITING in this session (23 red, 3 green-by-design),
with no production change in between.

Delegation: every build/test/format run except the last format check, build and one class rerun went to `cheap-worker`
subagents (three runs). The C# and views were written inline: they are small and each line is a spec rule (B, field,
format, note order).

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1** `BlankFormMarksExportTests.TheCourseCell_CarriesTheHeaderLineAsANote` (US-060) required the `{{course}}`
  note to equal the US-060 header line, or no note at all. US-063 FR-005.2 / I-5 adds two lines after it on purpose,
  so its 8 cases failed with the new lines present. Adapted, not weakened: the header line must be the note's first
  line and the note exactly three lines; a variant without a line has no US-060 stem in the note and starts with the
  "generated" line.
- **INFO-1** The note is joined with `\n`; ClosedXML reads it back as `\r\n`. The tests normalise; Excel shows the
  lines one per line either way.

## 8. Open Decisions

None touched or needed. SPEC_REVIEW findings m-1, m-2 remain OPEN (Owner accepts them at HUMAN_PR_APPROVAL).
