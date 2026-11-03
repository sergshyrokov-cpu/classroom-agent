---
artifact_type: implementation_report
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-03T08:09:23Z
updated_at: 2026-11-03T10:43:05Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
  - path: docs/designs/database/US-070-entity-model.md
    version: 1
  - path: docs/tests/US-070-test-strategy.md
    version: 1
  - path: docs/tests/US-070-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-070 Implementation Report

## 1. Summary

One set of matching rules now serves the course list, the teacher list and the Meet codes. The teacher list and the Meet codes have search fields, the search survives paging, row actions and the language switch, the Linked and Not-a-course tables show organizers, a Meet page past the last shows the last, the long pickers narrow as the user types, and the three lists have the new hint.

Build, format and the full suite pass: 7336 of 7336, no timeout. AC-009 was walked by the Owner on 2026-11-03 on a computer and on an iPhone, with no defect found and the limits recorded in §3.1; automated tests prove only the markup and the script's text.

## 2. Source Artifacts

As in the front matter: Story, Specification v4, Open Decisions v2 (OD-001 resolved a), API design v1 and OpenAPI, DB design v1 and entity model v1, test strategy v1, AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `SearchMatch` (server), `picker-narrow.js` `fold`, `matches` (picker) | `SearchMatchingSourceTests` | passing |
| AC-002 | `CourseListSource.Matching` | `SearchMatchingSourceTests`, `CourseListSearchPageTests` | passing |
| AC-003 | `TeacherListSource.Listed`, `GetTeacherListQuery`, `TeacherListUrl.Build`, `Views/Teachers/Index.cshtml` | `TeacherListSearchPageTests` | passing |
| AC-004 | `MeetCodesReadSource` (`UnassignedCodes`, `LinkedLinks`, `MarkedLinks`, `LoadOrganizersAsync`), `GetMeetCodesQuery.BuildAsync`, `Views/MeetCodes/Index.cshtml` | `MeetCodesSearchPageTests`, `SearchMatchingSourceTests` | passing |
| AC-005 | `MeetCodesReadSource.CodesByOrganizer` over the stored `organizer_email` | `SearchMatchingSourceTests`, `MeetCodesSearchPageTests` | passing |
| AC-006 | `MeetCodeWrite.ReturnQ`, `Result`; `MeetCodesController`; `GetMeetCodeCourseChoiceQuery`; both Meet views | `MeetCodeSearchSurvivesActionsTests`, `TeacherListSearchPageTests`, `MeetCodesSearchPageTests` | passing |
| AC-007 | `CourseListQueryRules.ReadSearch`, `GetMeetCodesQuery.ExecuteAsync`, `GetTeacherListQuery` | the three page classes, `ListSearchAccessTests` | passing |
| AC-008 | No guard, port or audit added; policies unchanged | `ListSearchAccessTests`, `MeetCodeSearchSurvivesActionsTests` | passing |
| AC-009 | `picker-narrow.js`, hooks in five views | `PickerNarrowMarkupTests`, `PickerNarrowScriptTests` | automated part passing; manual check passed, with the limits in §3.1 |
| AC-010 | `PageSearchHintTextKeys.FieldLabelKey`, `_PageSearchHint.cshtml`, resx | `PageSearchHintTests`, `ListSearchTranslationTests`, `PageSearchHintTranslationTests` | passing |

### 3.1 Manual check of AC-009

Walked by the Owner on 2026-11-03, on a temporary stand with synthetic data only. The stand was a temporary test class, removed after the walk and in no commit.

| Field | Value |
|---|---|
| Computer | Microsoft Edge 156.0.4314.58, a clean profile started with `--ignore-certificate-errors` |
| Phone | iPhone, Chrome, over the local network; the iOS version was not recorded |

| Check | Computer | iPhone |
|---|---|---|
| Journal: narrowing, words in any order, clearing brings all rows back in alphabetical order | passed | passed |
| Apostrophe variants and case | passed (journal) | not walked |
| Empty and selected rows stay; the form submits the selected course | passed | passed |
| "Nothing found"; Enter in the field does not submit | passed | passed, with the keyboard's own key, on the journal and the course-choice page |
| Template report, Meet reports | passed | not walked |
| Teacher filter: namesakes with email, a shown email found, a hidden email not found, the filter applied | passed | namesakes only |
| Course-choice page: one field for both blocks, an emptied block hidden with its heading, submit disabled with no row and back with a row | passed | passed |
| Course-choice page: the marked row stays, the mark survives typing and clearing, the link is saved | passed | passed |
| Absence mark shown beside a narrowed row | passed | not walked |
| Scripts off: no field, the picker works (journal, course-choice page) | passed | not walked |
| Red console messages | none on the five pages | console not available |

**Limits.**

- On the iPhone the template report, the Meet reports, the apostrophes and scripts-off were not walked. The select pickers share one script path with the journal, and the journal passed.
- In the Owner's everyday Edge profile the stand showed pages with no styles and no password eye. A clean Edge profile on the same stand showed both, and the server answered `200 text/css` to requests with browser headers. The cause was not found; the stand's development certificate is untrusted. On the pilot server, with a real certificate, the first sign-in shows whether it is gone.
- The journal shows no table on this stand, as no course has coursework. The submit was confirmed by the selected course and the "generated at" line.

## 4. Change Set

**Application.** `Models/Dtos/SearchRefusalKey.cs` (new; VR-001, openapi `searchMessage`); `Models/Dtos/LinkedCodeItem.cs`, `NotACourseCodeItem.cs` (OD-001 a); `MeetCodesPageModel.cs`, `TeacherListPageModel.cs`, `MeetCodeCourseChoicePageModel.cs`, `TeacherListMessageKey.cs` (openapi deltas); `Models/LinkedCodeRow.cs`, `MarkedCodeRow.cs`, `Ports/IMeetCodesReadSource.cs`, `Ports/ITeacherListSource.cs` (entity model §1, §2); `Models/MeetCodeChangeResult.cs`, `MeetCodeChoiceResult.cs` (FR-005, D-2); `Models/Requests/MeetCodesRequest.cs`, `TeacherListRequest.cs` (`q`); `UseCases/CourseListQueryRules.cs`, `GetCourseListQuery.cs` (VR-001); `GetTeacherListQuery.cs`, `TeacherListUrl.cs` (FR-003); `GetMeetCodesQuery.cs` (FR-004, I-6, VR-003, VR-004); `GetMeetCodeCourseChoiceQuery.cs`, `MeetCodeWrite.cs`, `MarkMeetCodeNotACourseUseCase.cs`, `SetMeetCodeCourseUseCase.cs`, `ConfirmMeetCodeLinkUseCase.cs` (FR-005); `Localization/SharedResource.uk.resx`, `.en.resx` (D-10, D-11).

**Infrastructure.** `Persistence/SearchMatch.cs` (new; db-design §2); `CourseListSource.cs`, `TeacherListSource.cs` (§3); `MeetCodesReadSource.cs` (§4, §5).

**Web.** `Controllers/MeetCodesController.cs`, `TeachersController.cs` (`q`, `returnQ`, FR-005); `Security/PickerTextKeys.cs` (new), `MeetCodesTextKeys.cs`, `TeachersTextKeys.cs`, `PageSearchHintTextKeys.cs` (D-10, D-11); `Views/Teachers/Index.cshtml`, `MeetCodes/Index.cshtml`, `MeetCodes/CourseChoice.cshtml`, `Courses/Index.cshtml`, `Journal/Index.cshtml`, `Report/Index.cshtml`, `MeetReports/Index.cshtml`, `Shared/_PageSearchHint.cshtml` (FR-002 to FR-009); `wwwroot/js/picker-narrow.js` (new; FR-009).

**Tests.** The files of `docs/evidence/US-070-test-generation-report.md`, plus `Application/UseCases/TeacherListQueryTests.cs` and `Web/UseCases/TeacherListReadOnlyTests.cs`: an empty `q` in the request (D-1 below).

**Workflow.** `docs/tests/US-070-*`, `docs/evidence/US-070-*`, the workflow state files.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no findings |
| `dotnet test ClassroomAgent.sln` (free RAM 4.92 GB) | 0 | total 7336, passed 7336, failed 0, skipped 0; 11 min 17 s; no timeout or Ryuk line |

Baseline: the TEST_WRITING run in the test generation report. The first build failed on four nullable errors in `SearchMatch.cs`, fixed by the `NotNullIfNotNull` annotation on `Replacer.Visit`. All story classes then passed on their first run, and no test was changed to pass.

Delegated to `cheap-worker`: translations, text keys and views against a fixed brief; every build, test and format run. Kept here: the matching, the use cases, the controllers, the script and this report.

## 6. Configuration Changes

None. No package, no CSP change, no migration.

## 7. Deviations and Discovered Problems

- **D-1 (test edit).** `TeacherListRequest` gained a required `Q`, so two existing test calls pass `[]`. No assertion changed.
- **D-2 (names no design gives).** `SearchRefusalKey` is the type of the openapi's `searchMessage` enum, `MeetCodeCourseChoicePageModel.CurrentList` serves "Cancel" (I-7), and `MeetCodeChoiceResult.ReturnQ` carries the search to the unknown-code page (openapi `getMeetCodeCourseChoice` 404).
- **D-3 (spec reading).** With an invalid `status` or `page` and a valid `q`, the teacher list keeps the valid text in the field and the return path, as the course list does (VR-002: "other valid parameters keep their values").
- **D-4 (extra reads).** Under a search the Unassigned read takes one more command for the matching codes (db-design §4), and page reads still count their own total, so the page's counts and the row total are separate reads, as today.
- `MeetCodes/Index.cshtml` has a large diff: the list block was reindented to sit under `SearchMessage is null`.

## 8. Open Decisions

None touched or newly required.

## Findings

- `US-070/TEST_WRITING/INFO-1` and `INFO-2` stay OPEN for the human: this run had no timeout and none of those three classes failed, but neither cause was diagnosed, so a stage may not close them as `FIXED` or `WITHDRAWN`.
