---
artifact_type: ac_test_matrix
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:45:20Z
updated_at: 2026-10-31T14:45:20Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
  - path: docs/tests/US-061-test-strategy.md
    version: 1
supersedes: null
---

# US-061 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`), all integration (PostgreSQL via Testcontainers, Google ports substituted):

| Code | Class |
|---|---|
| **MP** | `Web.Pages.MarkedCourseMeetReportsPageTests` |
| **MX** | `Web.Pages.MarkedCourseMeetExportTests` |
| **MC** | `Web.Pages.MarkedCourseMeetCodesPageTests` |
| **ML** | `Web.BackgroundServices.MarkedCourseMeetLinkingHostTests` |

Status column: expected state before IMPLEMENTATION — **red** (fails for missing behaviour) or **green by design**
(guards behaviour that already holds and must keep holding). Actual results are in the test generation report.

| AC | Scenario | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| AC-001 | "Not in Classroom" D1, read R1: both pages show the line; otherwise unchanged (Dean, Admin) | MP | `ANotInClassroomCourse_ShowsTheMarkAndRosterDate_OnBothPages_AndNothingElseChanges` | line «Курсу немає в Classroom з D1; склад курсу — на R1» on both pages; page text minus the line = page before the mark | red |
| AC-001 | Meeting page: line after the course name | MP | `OnTheMeetingPage_TheLineComesAfterTheCourseName` | line index > course-name index | red |
| AC-001 | Exports: C1 of every sheet; every other cell unchanged | MX | `ANotInClassroomCourse_PutsTheLineInC1_OfEverySheet_AndChangesNothingElse` | C1 = line on 3 + 1 sheets; all other cells equal to the pre-mark export | red |
| AC-002 | "Not visible" D2, read R2: both pages | MP | `ANotVisibleCourse_ShowsItsMarkAndRosterDate_OnBothPages` | «Курс не видно технічному акаунту з D2; склад курсу — на R2» | red |
| AC-002 | "Not visible": both exports | MX | `ANotVisibleCourse_PutsItsLineInC1_OfEverySheet` | C1 = that line | red |
| AC-003 | Ordinary course: no text on either page | MP | `AnOrdinaryCourse_ShowsNoLine` | no roster prefix, no mark text (uk, en) | green by design |
| AC-003 | Ordinary course: C1 empty, no text in the sheets | MX | `AnOrdinaryCourse_LeavesC1Empty` | C1 empty on every sheet | green by design |
| AC-004 | Old course marked (each mark), re-created course same roster: code links to the new one | ML | `ARecreatedCourse_GetsTheCodeAutomatically_WhenTheOldOneIsMarked` (×2) | one automatic link to B, audit `meet_code_auto_linked` → B; A still marked | red |
| AC-004 / FR-001.1 | Marked course with the best share (85 %) is never chosen; the rest (20 %) is below 60 % | ML | `AMarkedCourse_IsNeverChosen_EvenWithTheBestShare` | no link, no audit row | red |
| AC-004 / FR-001.2 | Marked B (20 %) removed: A's share unchanged, A linked | ML | `RemovingAMarkedCandidate_LeavesTheOthersSharesAsBefore` | one automatic link to A | green by design |
| AC-005 | Unlinked list: marked 100 % candidate absent, two unmarked 66 % present (Dean, Admin) | MC | `UnlinkedMeetings_OfferTheUnmarkedCandidatesOnly` | First, Second, two «66 %»; no Marked name, no «100 %» | red |
| AC-005 / FR-002.2 | Only candidate marked: code listed with "no candidate" | MC | `ACodeWhoseOnlyCandidateIsMarked_IsListedWithNoCandidate` | code listed, NoCandidates text, marked name absent | red |
| AC-006 | Choosing a marked course by hand | MC | `ChoosingAMarkedCourse_LinksTheCode_AsAPersonsLink_WithTheUsualAudit` | redirect; person's link to Marked; audit `meet_code_course_picked` succeeded | green by design |
| AC-007 | Automatic link to A; A marked; Meet loaded again | ML | `AnAutomaticLink_StaysWithItsCourse_AfterTheCourseIsMarked` | link snapshot equal; one audit row; A still marked | green by design |
| AC-008 | Read-only (every cause), English: pages | MP | `InReadOnlyMode_AnEnglishUser_SeesTheEnglishLine` | "Course not in Classroom since D1; course membership as of R1" on both pages | red |
| AC-008 | Read-only (every cause), English: exports | MX | `InReadOnlyMode_AnEnglishUsersExports_CarryTheEnglishLine` | C1 = English line; 2 export audit rows; 0 Google calls | red |
| AC-008 / FR-006 | English "not visible", undated roster (pages) | MP | `AnEnglishUser_SeesTheEnglishNotVisibleMark_AndTheUndatedRoster` | "Course not visible to the technical account since D2; course membership as of the last read" | red |
| AC-008 / FR-006 | English "not visible" (exports) | MX | `AnEnglishUser_GetsTheEnglishNotVisibleLine` | C1 = English line | red |
| AC-009 | No last complete read: pages | MP | `AMarkedCourseNeverReadCompletely_ShowsTheUndatedRosterPart` | «…з D1; склад курсу — на дату останнього читання» | red |
| AC-009 | No last complete read: exports | MX | `AMarkedCourseNeverReadCompletely_PutsTheUndatedRosterPartInC1` | C1 = undated line | red |
| AC-010 | Choice page (uk, en): marks next to Marked and Hidden, no share on Marked; none on First, Second, Other; all five offered | MC | `TheChoicePage_ShowsTheMarkNextToEachMarkedCourse_AndOffersEveryCourse` (×2) | course-list mark texts with D1 / D2 next to the marked courses only | red |
| AC-010 | Code linked to a marked course: mark next to the current course | MC | `TheChoicePage_OfACodeLinkedToAMarkedCourse_ShowsTheMarkNextToTheCurrentCourse` | current-course block contains «немає в Classroom з D1» | red |
| AC-011 | Skipped-only course: pages | MP | `ASkippedButUnmarkedCourse_ShowsNoLine` | no line | green by design |
| AC-011 | Skipped-only course: exports | MX | `ASkippedButUnmarkedCourse_LeavesC1Empty` | C1 empty | green by design |

Every Acceptance Criterion AC-001 … AC-011 has at least one mapped test.
