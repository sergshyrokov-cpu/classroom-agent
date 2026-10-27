---
artifact_type: implementation_report
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T14:39:31Z
updated_at: 2026-10-27T14:39:31Z
produced_by: dotnet-implementor
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
  - path: docs/tests/US-060-test-strategy.md
    version: 1
  - path: docs/tests/US-060-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-060 Implementation Report

## 1. Summary

The marks US-058 / US-059 store are now shown on the course list, the course card, the journal (full and
short), the report page and both export layouts, in Ukrainian and English:

- the course mark;
- "no teacher in Classroom";
- the course header line;
- above-maximum grades;
- the item "not in Classroom";
- the student "left" mark.

The marks are computed in Application from stored data and travel in the DTOs (AD-8); the views and
renderers only show them. There are:

- no new endpoint, schema, migration, role, write, audit event, Google call or package;
- no new round trip — the course facts and the skip record are read in the course query the pages
  already run (spec §9).

Build 0/0, full suite 6444/6444, `dotnet format --verify-no-changes` clean.

## 2. Source artifacts

| Artifact | Version |
|---|---|
| Story | `docs/stories/US-060-marks-in-course-list-journal-report-and-export.md` |
| Specification | `docs/specifications/US-060-spec.md` v1 APPROVED |
| Open Decisions | `docs/decisions/US-060-open-decisions.md` v1 APPROVED (OD-001…003 a) |
| API design | `docs/designs/api/US-060-api-design.md` v1 NOT_APPLICABLE |
| DB design | `docs/designs/database/US-060-db-design.md` v1 NOT_APPLICABLE |
| Test strategy | `docs/tests/US-060-test-strategy.md` v1 |
| AC matrix | `docs/tests/US-060-ac-test-matrix.md` v1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `DataMarkRules.Absence`; `CourseListRow.Absence`, `CourseCardDetails.Absence`; `Courses/Index.cshtml`, `Card.cshtml` | CourseListMarksPageTests | pass |
| AC-002 | `CourseListRow.NoTeacherInClassroom` (`GetCourseListQuery`); `Courses/Index.cshtml` | CourseListMarksPageTests | pass |
| AC-003 | `DataMarkRules.IsAboveMaximum`; `JournalCellRule` flags; `ReportCell.AboveMaximum` (`ReportBuilder`); class `mark-above-maximum` + label in both views | JournalMarksPageTests, ReportMarksPageTests | pass |
| AC-004 | `ReportWorkbookMapper.GradingCell` (Highlighted + note); `ExportMarks.Apply` in both renderers | ReportMarksExportTests, BlankFormMarksExportTests | pass |
| AC-005 | `NotInClassroom` on item/lesson records, `JournalColumn`, `GradingColumn`, `LessonTopicRow`; views; mapper heading/topic; blank topic note | Journal/Report page, export tests | pass |
| AC-006, AC-007, AC-011, AC-012 | `JournalCourseRecord` facts + `DataMarkRules.Notice`; `JournalPageModel.Notice`, `ReportHeader.Notice`; views; program C2 cell; blank `{{course}}` note | Journal/Report page, export tests | pass |
| AC-008 | `DataMarkRules.Left` (`SeenOnce` = first = last seen); `JournalRow.Left`, `GradingRow.Left`; views; `ReportWorkbookMapper.StudentCell` note | Journal/Report page, export tests | pass |
| AC-009 | `DataMarksTextKeys`, `DataMarksTexts`, 8 new `ReportText` values, 12 keys in both resx | every `en` case | pass |
| AC-010 | no guard added; reads only | DataMarksReadOnlyTests | pass |

## 4. Change set

**Application:**
- DTOs (new) — `CourseAbsence`, `CourseDataNotice`, `CourseDataNoticeKind`, `LeftMark`,
  `ReportAboveMaximum`. Trace: FR-001, FR-003, FR-004, FR-006.
- `UseCases/DataMarkRules.cs` (new) — the four rules (FR-001, FR-003.1, FR-004.1, FR-006.1; VR-001,
  VR-002, VR-004, VR-005).
- Records `JournalCourseRecord`, `JournalItemRecord`, `JournalLessonRecord`, `JournalMemberRecord`,
  `JournalCourseMemberRecord`, `CourseListCourseRecord`, `CourseCardCourseRecord` — stored facts the rules
  need. Trailing members with defaults, so existing fakes and tests compile unchanged.
- DTOs `CourseListRow`, `CourseCardDetails`, `JournalPageModel`, `JournalColumn`, `JournalRow`,
  `JournalCell`, `ReportHeader`, `GradingColumn`, `GradingRow`, `LessonTopicRow`, `ReportCell` — the marks
  carried to the views (AD-8).
- `GetCourseListQuery`, `GetCourseCardQuery`, `GetJournalQuery`, `JournalCellRule`, `ReportBuilder` — fill
  the DTOs.
- `ReportWorkbookMapper`, `GroupLessonsBlankMapper` — FR-007.1 … FR-007.4, FR-008.1 … FR-008.4.
- `Models/Export/WorkbookCell.cs` — `Highlighted`, `Note` (FR-007.5).
- `Models/Export/ReportText.cs` — the export texts (FR-009).
- `Localization/SharedResource.uk.resx`, `.en.resx` — 12 keys each (FR-009 table verbatim). Supporting
  translation entries.

**Infrastructure:**
- `CourseListSource`, `CourseCardSource` — project the mark (FR-001).
- `JournalSource`, `JournalFieldSource` — course facts with the skip record matched by Google id in the
  same query (FR-003, VR-003); item `NotInClassroomSince != null` (FR-005); `FirstSeenAt == LastSeenAt`
  (OD-003 a).
- `Export/ExportMarks.cs` (new) — the one fill colour and the note (comment without an author).
  `ClosedXmlReportRenderer`, `ClosedXmlBlankFormRenderer` call it (FR-007.5, FR-008).

**Web:**
- `Security/DataMarksTextKeys.cs`, `Security/DataMarksTexts.cs` (new) — keys and screen wording (FR-009).
- `Security/LocalizedReportTexts.cs` — the new `ReportText` values (FR-009).
- `Views/Courses/Index.cshtml`, `Views/Courses/Card.cshtml` — FR-001, FR-002.
- `Views/Journal/Index.cshtml` — FR-003.2, FR-004, FR-005, FR-006.
- `Views/Report/Index.cshtml` — FR-003.2, FR-004, FR-005, FR-006.
- `wwwroot/css/site.css` — `.mark-above-maximum`, a light fill (FR-004.2, BR-047; no inline style, CSP).

**Tests:**
- `TestInfrastructure/DataMarksHostExtensions.cs` — fixture correction D-1 (§7).
- The other TEST_WRITING files are unchanged.

**Workflow** (closed list): `docs/workflow/workflow-state.yaml`, `history.jsonl`, the US-060 test
artifacts and reports.

No secret, generated database or IDE file is in the change set.

## 5. Validation evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors (inline, and again by subagent) |
| Story tests | `dotnet test --no-build --filter FullyQualifiedName~<Class>` × 6 | 73/73 after D-1 (first run 69/73: the 4 D-1 failures) |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | exit 0 — total 6444, passed 6444, failed 0, skipped 0, 8 m 39 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --no-restore` | first exit 2 (2 whitespace diagnostics in `GroupLessonsBlankMapper.cs`); fixed; then exit 0 |
| After the format fix | `BlankFormMarksExportTests`, `GroupLessonsBlankExportTests`, `GroupLessonsBlankFormTests` | 14/14, 47/47, 10/10 |

**Delegation:**
- `cheap-worker` (Sonnet) ran the story-class runs, the full suite and the format check.
- Inline: the production code, the two incremental builds that decided compile fixes, the D-1 diagnosis
  and fix, and the re-runs after the format fix.
- Free RAM was 3.6 GB before the full suite. Chrome was closed at the Owner's request and the build
  server was shut down; 6.5 GB were free for the run.

## 6. Configuration changes

None.

## 7. Deviations and discovered problems

- **D-1 — test fixture label (TEST_WRITING defect, fixed here).**
  - What was wrong: `DataMarksHostExtensions.PresentTeacherName` / `FormerTeacherName` held the
    participants' full names. The course list labels a person surname-first (US-020 FR-007), so the page
    shows "Present Test Teacher", not "Test Teacher Present".
  - Effect: 4 cases of `CourseListMarksPageTests` failed. In addition, the "former teacher is not shown"
    assertion passed for the wrong reason.
  - Fix: the constants now hold the labels the US-020 rule produces, and the seeded full names follow
    them.
  - Unchanged: no assertion was weakened or removed; the expectation is the approved US-020 rule.
- **D-2 — record extension style.** New members of records that existing tests construct are trailing
  and defaulted, so no existing test had to change. DTOs built only by their query take the new members
  as required.
- **I-1 — note author.** ClosedXML puts the machine's user name as a comment's author. The author is set
  to empty, so no account name enters a file (the US-043 INFO-2 precedent for docProps).
- **Not visually checked:** the on-screen highlight colour (strategy §5). The CSS rule sets
  `#fff2cc` with dark text, and the same colour is used in both export layouts.
- **Line endings:** one edited file had mixed endings. It was normalised to CRLF and the format check
  is clean.

## 8. Open Decisions

None touched or needed. Review findings:
- US-060/SPEC_REVIEW/m-1: the code follows VR-001 as approved — maximum > 0, "equal" is not above.
- US-060/SPEC_REVIEW/m-3: concerns the digest; no code bearing on it.

Both stay OPEN.
