---
artifact_type: implementation_report
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T19:13:53Z
updated_at: 2026-10-31T19:13:53Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md
    version: null
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
  - path: docs/designs/api/US-061-api-design.md
    version: 1
  - path: docs/designs/database/US-061-db-design.md
    version: 1
  - path: docs/tests/US-061-test-strategy.md
    version: 1
  - path: docs/tests/US-061-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-061 Implementation Report

## 1. Summary

Implemented both parts of the Story from stored data only — no Google call, no schema change, no endpoint, no new audit
event, no new package:

- **Candidates (FR-001, FR-002):** `MeetCodeScorer.Score` leaves out courses with a stored mark (US-059) before the
  decision; the denominator and thresholds are unchanged. Because the automatic link (`LinkMeetCodesStep`), the
  "unlinked meetings" list (`GetMeetCodesQuery`) and the choice page's candidates (`GetMeetCodeCourseChoiceQuery`) all
  score through it, one filter covers all three. The marked set is read once per scoring batch with the memberships.
- **Choice page (FR-003, OD-002 a):** a marked course — among the other courses or as the current course — shows the
  US-060 course-list mark with its date after its name; choosing it uses the unchanged use case, guard and audit.
- **Meet line (FR-005, FR-006, OD-001 a):** a structured `MeetCourseNotice` built in Application (`DataMarkRules.MeetNotice`)
  travels in `MeetReports` and `MeetingDetail`; the pages word it through `DataMarksTexts.MeetNotice`, the exports
  through `IReportTexts` into C1 of the Course header row (`MeetExportLayout.CourseRow`). Skipped-only courses get none.

Validation: build 0/0, format clean, all 32 story tests green, full suite 6924/6926 with the 2 failures traced to
existing tests whose assumptions US-061 legitimately changes; both adapted and green (§5, §7).

## 2. Source Artifacts

Story `docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md`; Specification v1 (APPROVED); Open Decisions
v1 (OD-001 a, OD-002 a); API design v1 and DB design v1 (both NOT_APPLICABLE); test strategy v1; AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001, AC-002 | `DataMarkRules.MeetNotice`; `GetMeetReportsQuery` (record of the chosen course), `GetMeetReportMeetingQuery` + `IMeetReportSource.GetCourseMarkAsync`; views `MeetReports/Index.cshtml`, `Meeting.cshtml`; `MeetExportLayout.CourseRow` in both workbook mappers | `MarkedCourseMeetReportsPageTests`, `MarkedCourseMeetExportTests` | PASS |
| AC-003, AC-011 | `MeetNotice` returns null without a mark; `CourseRow` leaves C1 empty | same | PASS |
| AC-004, AC-005 | `MeetCodeScorer.Score` (`MarkedCourseIds`), `MeetCodeScoringSource.GetScoringInputsAsync` | `MarkedCourseMeetLinkingHostTests`, `MarkedCourseMeetCodesPageTests` | PASS |
| AC-006 | unchanged `SetMeetCodeCourseUseCase` (filter is not in the manual path) | `MarkedCourseMeetCodesPageTests` | PASS |
| AC-007 | unchanged: only unlinked codes are scored | `MarkedCourseMeetLinkingHostTests` | PASS |
| AC-008 | translations uk/en; queries and exports have no read-only guard (unchanged) | `MarkedCourseMeetReportsPageTests`, `MarkedCourseMeetExportTests` | PASS |
| AC-009 | `RosterAsOf` null → undated roster text | same | PASS |
| AC-010 | `IMeetCodesReadSource.GetCourseAbsencesAsync`, `MeetCodeCourseChoicePageModel.Absences`, `CourseChoice.cshtml` | `MarkedCourseMeetCodesPageTests` | PASS |

## 4. Change Set

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/MeetLinking/MeetCodeScoringInput.cs` | FR-001, VR-002: `MarkedCourseIds` |
| `src/ClassroomAgent.Application/MeetLinking/MeetCodeScorer.cs` | FR-001.1, I-6: marked course not a candidate |
| `src/ClassroomAgent.Infrastructure/Persistence/MeetCodeScoringSource.cs` | FR-001, §9 cost: marks read with the memberships, once per batch |
| `src/ClassroomAgent.Application/Models/Dtos/MeetCourseNotice.cs` (new) | FR-005.1, FR-005.6 |
| `src/ClassroomAgent.Application/Models/MeetCourseMarkRecord.cs` (new) | FR-005 (report 4's course mark) |
| `src/ClassroomAgent.Application/Models/MeetCourseAbsenceRecord.cs` (new) | FR-003.2 |
| `src/ClassroomAgent.Application/UseCases/DataMarkRules.cs` | FR-005.1, FR-005.5, VR-001, VR-004 |
| `src/ClassroomAgent.Application/Models/Dtos/MeetReports.cs`, `MeetingDetail.cs` | FR-005.6: notice in the DTOs |
| `src/ClassroomAgent.Application/UseCases/GetMeetReportsQuery.cs` | FR-005.1 (reports 1–3) |
| `src/ClassroomAgent.Application/UseCases/GetMeetReportMeetingQuery.cs` | FR-005.1 (report 4) |
| `src/ClassroomAgent.Application/Ports/IMeetReportSource.cs`, `src/ClassroomAgent.Infrastructure/Persistence/MeetReportSource.cs` | FR-005: `GetCourseMarkAsync` |
| `src/ClassroomAgent.Application/Ports/IMeetCodesReadSource.cs`, `src/ClassroomAgent.Infrastructure/Persistence/MeetCodesReadSource.cs` | FR-003.2: `GetCourseAbsencesAsync` |
| `src/ClassroomAgent.Application/Models/Dtos/MeetCodeCourseChoicePageModel.cs`, `UseCases/GetMeetCodeCourseChoiceQuery.cs` | FR-003.2, OD-002 a |
| `src/ClassroomAgent.Application/Models/Export/ReportText.cs` | FR-006: four export texts |
| `src/ClassroomAgent.Application/UseCases/MeetExportLayout.cs`, `MeetReportsWorkbookMapper.cs`, `MeetMeetingWorkbookMapper.cs` | FR-005.3: C1 of the Course header row |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-006: four keys (supporting translation entries) |
| `src/ClassroomAgent.Web/Security/DataMarksTextKeys.cs`, `DataMarksTexts.cs`, `LocalizedReportTexts.cs` | FR-006: keys, screen wording, export mapping |
| `src/ClassroomAgent.Web/Views/MeetReports/Index.cshtml`, `Meeting.cshtml` | FR-005.2 |
| `src/ClassroomAgent.Web/Views/MeetCodes/CourseChoice.cshtml` | FR-003.2 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/FakeMeetReportSource.cs` | supporting: implements the new port member (unmarked) |
| `tests/ClassroomAgent.Tests/Web/Pages/MarkedCourseMeetReportsPageTests.cs` | ac_test_matrix; D-1 |
| `tests/ClassroomAgent.Tests/Web/Localization/JournalExportTranslationTests.cs` | D-2 |
| `tests/ClassroomAgent.Tests/Web/Localization/MeetExportTranslationTests.cs` | D-2 |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/FailedRunRecordedTests.cs` | D-3 |
| other new test files and test artifacts | TEST_WRITING outputs (test generation report) |
| `docs/workflow/workflow-state.yaml`, `docs/workflow/history.jsonl` | workflow state (closed list) |

No secret, database file, `.xlsx` or IDE config in the change set.

## 5. Validation Evidence

All runs delegated to `cheap-worker` subagents (report-only, no edits); code, test adaptations and this report were
written inline.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors (last run after every change) |
| `dotnet test --no-build --filter FullyQualifiedName~MarkedCourseMeet` | 32/32 passed (11 + 9 + 7 + 5) |
| `dotnet test ClassroomAgent.sln --no-build` (full, 11 min 31 s) | 6926 total, 6924 passed, 2 failed, 0 skipped — the two below, each failing again alone |
| `--filter FullyQualifiedName~FailedRunRecordedTests` (after D-3) | 8/8 passed |
| `--filter FullyQualifiedName~MeetExportTranslationTests` (after D-2) | 105/105 passed |
| `--filter FullyQualifiedName~JournalExportTranslationTests` (after D-2) | 92/92 passed |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |

The full suite was not rerun after D-2 and D-3: those edits touch only the three named test classes, which were rerun
green. Environment: an earlier full run was lost to a Docker Desktop failure (WSL backend); Docker was restarted
(`wsl --shutdown`) and the run repeated. The compiler server was stopped once to free memory (free RAM 3.8 → 5.4 GB).

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1 (story test, own):** `ANotInClassroomCourse_…_AndNothingElseChanges` compared the whole page before and after the
  mark; the US-062 data-health banner at the top counts marked courses and rightly changes. The comparison now starts
  at the delay notice (reports page) and at the course name (meeting page). What it proves — the reports' content is
  unchanged apart from the line (FR-005.4) — is the same.
- **D-2 (existing tests, US-028 / US-044):** both translation tests require every export text to differ between the
  languages; the separator «; » is the same in both by spec FR-006. `JournalExportTranslationTests` now pins it to
  exactly "; " in both languages (a missing key would fail); `MeetExportTranslationTests` lists it with the two "Email"
  headers already exempted from the "differs" theory, its "resolves" theory still covering it.
- **D-3 (existing test, US-053):** `ARejectedLinkingWrite_…` lists only course 7 in Classroom, so US-059 marks the seeded
  courses A and B "not in Classroom" in the same run; under FR-001 they are no longer candidates, nothing is linked and
  the rejected link write never happens. The test now also lists A and B (without rosters) so they stay unmarked and A
  remains the candidate; its assertions are unchanged. This is the Story's intended behaviour reaching an older test's
  world, not a defect.
- Interpretation kept: the Meet "not in Classroom" text reuses `DataMarks.Header.NotInClassroomSince` (spec I-4); the
  choice page shows the mark in parentheses after the name.

## 8. Open Decisions

None touched or newly required.
