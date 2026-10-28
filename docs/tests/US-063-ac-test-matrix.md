---
artifact_type: ac_test_matrix
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:10:24Z
updated_at: 2026-10-28T17:10:24Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
  - path: docs/tests/US-063-test-strategy.md
    version: 1
supersedes: null
---

# US-063 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Code | Class |
|---|---|
| **GP** | `Web.Pages.GeneratedLinesPageTests` |
| **GX** | `Web.Pages.GeneratedLinesExportTests` (program layout and blank form) |
| **GR** | `Web.UseCases.GeneratedLinesReadOnlyTests` |

All tests are integration tests over HTTP against PostgreSQL (TC-2), manual clock, substituted Google ports (TC-4).
Status: red = fails before implementation as expected; green-by-design = passes before implementation on purpose.

| AC | Scenario | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| AC-001 | journal lines above the table, full/short, uk/en | GP | `TheJournal_ShowsGeneratedAndDataAsOf_AboveTheTable` (uk full, uk short, en full) | `#journal-generated` = «Сформовано: G1», `#journal-data-as-of` = «Дані з Google на: R2», once, before `#journal-table` | red |
| AC-001 | Kyiv time, date boundary, no seconds — literals | GP | `TheJournalLines_AreInKyivTime_WithoutSeconds` | «Сформовано: 17.09.2026 11:00», «Дані з Google на: 08.09.2026 01:00» | red |
| AC-001 | report header lines, uk/en | GP | `TheReportHeader_ShowsGeneratedAndDataAsOf` (uk, en) | `#report-generated`, `#report-data-as-of` inside `#report-header`, once | red |
| AC-001 | lines next to US-060's header line | GP | `NextToTheCourseHeaderLine_BothLinesAreShown_AndTheHeaderLineStays` | both lines; `#journal-notice` / `#report-notice` unchanged | red |
| AC-002 | cell state and «Сформовано» agree across the due moment | GP | `TheGeneratedLine_IsTheInstantTheCellsWereBuiltFor` | before: «термін ще не минув» + B; after +10 min: «не здано» + new B | red |
| AC-003 | program: note on B2 of both sheets | GX | `TheProgramLayout_CarriesBothLines_InANoteOnTheCourseCellOfBothSheets` (uk, en) | B2 = course name; note = [Generated G2, Data as of R2]; A2 no note; C2 empty | red |
| AC-003 | blank: note on `{{course}}` | GX | `TheBlankForm_CarriesBothLines_InANoteOnTheCourseCell` (uk, en) | A2 = course name; note = the two lines; topics copy no "generated" note | red |
| AC-003 | no printed cell holds a line | GX | `NoPrintedCell_HoldsEitherLine` (uk, en) | no cell value contains a label or either formatted instant | green-by-design (nothing is printed today; stays green after) |
| AC-004 | existing note kept first | GX | `AnExistingNote_IsKeptFirst_ThenBothLines` | blank note = [US-060 line, Generated, Data as of]; program C2 = US-060 line, B2 note = the two lines | red |
| AC-005 | English everywhere; names untranslated | GP, GX | every `(…, "en")` case above | English labels, `M/d/yyyy HH:mm` in Kyiv; course name as stored | red |
| AC-006 | null last read → unknown, screen | GP | `WithNoLastReadRecorded_TheDataAsOfLineSaysUnknown` (uk, en) | «Дані з Google на: невідомо» / "Data from Google as of: unknown" on journal and report | red |
| AC-006 | null last read → unknown, notes | GX | `WithNoLastReadRecorded_TheNotesSayUnknown` (uk, en) | program B2 and blank A2 notes end with the "unknown" line | red |
| AC-007 | read-only mode, three causes | GR | `InReadOnlyMode_TheJournalAndTheExportCarryBothLines_AndWriteNoTeachingData` | lines on screen and in the note; teaching fingerprint unchanged; one export audit row; 0 Google calls | red |
| AC-008 | every grading copy carries the note | GX | `EveryGradingCopy_CarriesTheNote` | 3 grading copies (2 students per copy), each A2 note = the two lines | red |
| AC-009 | archived course: last complete read, not listing | GP | `ForAnUnchangedArchivedCourse_TheLineShowsTheLastCompleteRead_NotTheLastListing` | line shows R2; the listing instant absent | red |
| — (FR-003.3) | empty period still shows both lines | GP | `AnEmptyPeriod_StillShowsBothLines` | July period: all four elements with their lines | red |
| — (FR-003.1) | no course selected → no lines | GP | `WithNoCourseSelected_TheJournalShowsNeitherLine` | no elements, no labels | green-by-design |

Every AC-001 … AC-009 has at least one red test.
