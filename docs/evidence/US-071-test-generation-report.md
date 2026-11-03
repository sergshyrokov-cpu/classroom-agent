---
artifact_type: test_generation_report
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T17:31:13Z
updated_at: 2026-11-03T17:31:13Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/designs/api/US-071-api-design.md
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
  - path: docs/designs/database/US-071-db-design.md
    version: 1
  - path: docs/designs/database/US-071-entity-model.md
    version: 1
  - path: docs/tests/US-071-test-strategy.md
    version: 1
  - path: docs/tests/US-071-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-071 Test Generation Report

**Result: red phase verified.** 223 new test cases, 203 red for missing behaviour and 20 green by design (explained below). 60 existing cases are red because they pin the new hint texts or the new permission matrix row. Every other existing test passes. No Acceptance Criterion is untested; AC-010 is a recorded measurement and AC-011's look is the person's walk.

## Files

Created, under `tests/ClassroomAgent.Tests/`:

| File | Cases |
|---|---|
| `Application/UseCases/GetSearchResultsQueryTests.cs` | 28 |
| `Infrastructure/Persistence/StudentSearchSourceTests.cs` | 36 |
| `Web/Pages/SearchResultsPageTests.cs` | 55 |
| `Web/Pages/HeaderSearchFieldTests.cs` | 32 |
| `Web/Pages/ErrorPageHeaderTests.cs` | 7 |
| `Web/Pages/MarkupTextSurvivesActionsTests.cs` | 6 |
| `Web/Security/SearchAccessTests.cs` | 9 |
| `Web/Localization/SearchTranslationTests.cs` | 35 |
| `Architecture/DisabledButtonRuleTests.cs` | 9 |
| `Web/UseCases/SearchMeasurementTests.cs` | 1 |
| `TestInfrastructure/SearchTestData.cs`, `SearchHostExtensions.cs`, `FakeStudentSearchSource.cs`, `FakeMeetCodesSearchSource.cs` | helpers |

Modified:

- `TestInfrastructure/PageSearchHintTestData.cs`: the final v108 texts and templates (FR-011, api-design D-9).
- `Web/Pages/PageSearchHintTests.cs`: 3 new cases, `TheHint_NamesTheSameLabelAsTheHeaderField` (2) and `TheHint_HasNoFinalPeriod`.
- `Web/Localization/PageSearchHintTranslationTests.cs`: the Ordinary template is asserted too.
- `Architecture/PickerNarrowScriptTests.cs`: 2 new cases; the "hidden" and "disabled" tests read the script with comments removed and require an assignment; each has a proof that removing the real line fails the check; summaries cite the right FR-009 items (AC-013).
- `Web/Pages/CourseListPageTests.cs`: two reads of the input named `q` now read the list's own field by id. After the Story the first such input is the header field, so the old reads would read the wrong input, and one would pass for free.
- `Web/Security/PermissionMatrixTests.cs`: the row of `GET /workspace/search`.
- `TestInfrastructure/RecordingMeetCodesReadSource.cs`: the new port member.

## Compile-only skeleton

| Declaration | File |
|---|---|
| `IStudentSearchSource` | `Application/Ports/IStudentSearchSource.cs` |
| `GetFirstMatchesAsync` | `Application/Ports/IMeetCodesReadSource.cs`, throwing in `Infrastructure/Persistence/MeetCodesReadSource.cs` |
| `StudentSearchRecord`, `StudentSearchCourseRecord`, `MeetCodeMatch` | `Application/Models/` |
| `SearchResultsState`, `SearchResultsPageModel`, `SearchResultsGroup<TRow>`, `MeetCodeSearchGroup`, `StudentSearchRow`, `StudentSearchCourse`, `TeacherSearchRow`, `CourseSearchRow` | `Application/Models/Dtos/` |
| `SearchResultsRequest` | `Application/Models/Requests/` |
| `GetSearchResultsQuery`, with `GroupSize` and `MinimumLength` | `Application/UseCases/GetSearchResultsQuery.cs` |
| `SearchResultsUrl`, with `Path` | `Application/UseCases/SearchResultsUrl.cs` |
| `StudentSearchSource` | `Infrastructure/Persistence/StudentSearchSource.cs` |

Every new method throws `NotImplementedException`. Nothing is registered in DI; no route, view or translation entry. The entity model's signatures compiled as written, so its `CourseSearchRow.State` is proved to fit.

`PageHeaderRules`, `_HeaderSearch.cshtml`, the controller and `MeetCodesController.ListUrl` are not in the skeleton: no test names them, as they are proved over HTTP.

## Delegation

- Inline: the strategy, the matrix, the skeleton, `GetSearchResultsQueryTests`, `StudentSearchSourceTests`, `SearchResultsPageTests`, `ErrorPageHeaderTests`, `DisabledButtonRuleTests`, `SearchMeasurementTests`, the script test changes, and the red-phase verdict.
- `cheap-worker`: `HeaderSearchFieldTests`, `SearchAccessTests`, `SearchTranslationTests`, `MarkupTextSurvivesActionsTests`, the hint test data, and every build and test run.

## Commands and results

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln -nodeReuse:false -p:UseSharedCompilation=false` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` (free RAM 5.6 GB after stopping the build servers) | 2 | total 7559, passed 7297, failed 262, skipped 0; 11 min 32 s; no timeout line |
| After two test changes (below), the same build | 0 | 0 warnings, 0 errors |
| `--filter FullyQualifiedName~ErrorPageHeaderTests` | 2 | total 7, failed 5, passed 2 |
| `--filter FullyQualifiedName~DisabledButtonRuleTests` | 2 | total 9, failed 7, passed 2 |
| `--filter FullyQualifiedName~SearchTranslationTests` | 2 | total 48, failed 34, passed 14: the filter also matches the 13 cases of `ListSearchTranslationTests` |
| `--filter FullyQualifiedName~MarkupTextSurvivesActionsTests` | 0 | total 6, passed 6 |

The two test changes came after the full run and the full suite was not repeated: `AssertBareErrorPage` now checks the marker first, which changes which line fails, not how many; `BothHosts_HaveTheSameValues` now requires the rule, which moves one case from green to red. So the expected next full run is 263 failed, 7296 passed.

7559 is 7336 (US-070's total) plus the 223 new cases. No existing test was removed or disabled.

## Red phase

### Red for missing behaviour (203 new)

| Class | Red | Why |
|---|---|---|
| `GetSearchResultsQueryTests` | 27 | `NotImplementedException` from the skeleton |
| `StudentSearchSourceTests` | 36 | `NotImplementedException`, after each test's rows were inserted and its controls reached the source |
| `SearchResultsPageTests` | 55 | no route: `404` instead of `200` or `400`, no group on the page, or a missing translation key |
| `HeaderSearchFieldTests` | 28 | "has no header search field"; the control on the landing page |
| `SearchAccessTests` | 8 | no route: `404` where `200` or the sign-in redirect is expected |
| `SearchTranslationTests` | 34 | no entry; old hint texts |
| `ErrorPageHeaderTests` | 5 | the marker, see below |
| `DisabledButtonRuleTests` | 7 | "`.btn:disabled` in the shared part sets no `--bs-btn-disabled-bg`" |
| `SearchMeasurementTests` | 1 | `NotImplementedException`, after the seed ran, so its SQL is valid |
| `PageSearchHintTests` | 2 | missing translation key |

**The defect of AC-012 is now shown by a request**, as the Story asks. The marker was found in the switcher's hidden `returnPath`:

| Test | Around the marker |
|---|---|
| `AMalformedCode_OnTheCourseChoice_LeavesTheMarkerNowhere` (Admin, Dean), `TheErrorPage_StaysInTheUsersLanguage` | `returnPage=1&amp;returnQ=zzrawmarker" />` |
| `TheAntiforgeryRefusal_HasNoHeaderControls` | `bbb/not-a-course-mark?zz=zzrawmarker" />` |
| `TheReadOnlyRefusal_HasNoHeaderControls` | `value="/error/500?zz=zzrawmarker" />` |

### Red existing tests (60)

| Class | Red | Why |
|---|---|---|
| `PageSearchHintTests` | 55 | adapted expected texts; the page still shows the old ones |
| `DataHealthPageTests` | 2 | pins the Ordinary hint through the same test data (I-14) |
| `PageSearchHintTranslationTests` | 2 | new templates |
| `PermissionMatrixTests` | 1 | "a matrix row with no endpoint", `GET /workspace/search` |

### Green by design (20 new)

| Tests | Cases | Why green before implementation |
|---|---|---|
| `MarkupTextSurvivesActionsTests` | 6 | AC-013: US-070 built this; they guard it |
| `PickerNarrowScriptTests.WithoutTheLine_*` | 2 | AC-013: each removes the real line and shows the check then fails |
| `HeaderSearchFieldTests`: forced change, two anonymous pages, Control Plane | 4 | "no field" holds while no field exists. They bite once `_Layout` gains it: the first three are the header rule's conditions. The Control Plane has its own layout |
| `SearchAccessTests.ARestrictedDean_IsSentToTheForcedChange` | 1 | the middleware runs before routing's answer, for any address |
| `ErrorPageHeaderTests.ANotFoundCard_HasNoHeaderControls` | 1 | a `404` re-executes as the anonymous `/error/404`, so it already has no switcher; it guards the new rule |
| `ErrorPageHeaderTests.AnOrdinaryPage_KeepsItsSwitcher_AndItsFallback` | 1 | guards that FR-010 stays narrow |
| `DisabledButtonRuleTests.NoEnabledButton_GetsTheseVariables` | 2 | nothing sets those variables yet; bites a wrong selector |
| `PageSearchHintTests.TheHint_HasNoFinalPeriod` | 1 | reads the expected texts of the test data, which also feed the page tests |
| `SearchTranslationTests.NoView_HoldsAResultsPageTextAsALiteral` | 1 | no view exists yet; bites a hard-coded text |
| `GetSearchResultsQueryTests.TheConstants_AreTheSpecifications` | 1 | the constants are the skeleton's, from the entity model |

The strengthened `TheScript_HidesAnEmptyBlock` and `TheScript_DisablesTheSubmit` are existing cases and stay green: the script does hold the behaviour.

## Unexpected failures

None. Every failure above is the missing behaviour or a pinned text.

## For IMPLEMENTATION

- `SearchMeasurementTests` writes the median and the worst to `US-071-search-measure.txt` in the temporary folder and to the test output. Copy them to the implementation report (AC-010). Submissions are not seeded: the search reads none.
- `Web/Pages/LanguageSwitcherTests` and other page-frame tests may hold assumptions about the header; api-design §6 says those change with it.
- A manual walk is due: AC-011, on a computer and a phone, in a clean Edge profile (spec section 9).

## Untested Acceptance Criteria

None by test level; AC-011's look is manual, as the Story says, and AC-010's times are recorded, not asserted.

## Open Decisions

None.

## OPEN findings handled

Neither is closed here. `US-071/API_DESIGN/m-1` is outside the Story (strategy section 6). `US-071/DB_DESIGN/m-1`: the skeleton and tests use `CourseState`, as the entity model does, and assert the text of `Courses.Status.*`, but the openapi's sentence still names `Status(status)` and this stage may not edit it.
