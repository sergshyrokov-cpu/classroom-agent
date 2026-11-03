---
artifact_type: implementation_report
story: US-071
version: 2
status: DRAFT
created_at: 2026-11-03T18:07:52Z
updated_at: 2026-11-03T20:01:01Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
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
supersedes: docs/evidence/US-071-implementation-report.md@v1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-071 Implementation Report

## 1. Summary

The header search field and the results page (`GET /workspace/search`) are implemented: four groups, including students with their courses and the BR-068 mark. Also the final hint texts, grey disabled buttons on both hosts, and no language switcher on any error page.

Build, format and the full suite pass: 7561 of 7561 on the final run; the run before it had one timeout in a test's database teardown (§5). The speed measurement is far under the bound (§5).

Attempt 2 answers the verification (`docs/reviews/implementation/US-071-implementation-verification.md` v1): M-1 and m-1 to m-4 are handled, see §7 items 8 to 11.

**The manual part of AC-011 was walked by the Owner on 2026-11-03**, on a computer and at phone width in Edge's device mode, with no defect found; §3.1 records it and its limit. Automated tests prove the markup and the CSS text, not the look.

`security_sensitive: true`: a new protected endpoint reads students' personal data and takes a search text in the query string. No schema change, no migration, no new package, no configuration change.

## 2. Source Artifacts

As in the front matter. No artifact was edited at this stage.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `PageHeaderRules.ShowsSearchField`, `Views/Shared/_HeaderSearch.cshtml`, `_Layout.cshtml` | `HeaderSearchFieldTests`, `ErrorPageHeaderTests` | passing |
| AC-002 | `GetSearchResultsQuery`, `Views/Search/Index.cshtml`, `StudentSearchSource`, `MeetCodesReadSource.GetFirstMatchesAsync` | `SearchResultsPageTests`, `GetSearchResultsQueryTests`, `StudentSearchSourceTests` | passing |
| AC-003 | `StudentSearchSource.GetStudentCoursesAsync`, `DataMarkRules.Left` through `GetSearchResultsQuery.ReadStudentsAsync`, `DataMarksTexts.Left` in the view | the same three classes | passing |
| AC-004 | `GetSearchResultsQuery.GroupSize`, `LimitLine` and `WholeListLink` in the view, `MeetCodesController.ListUrl` | `SearchResultsPageTests` (with `WithExactly20MeetCodes_ThereIsNeitherLineNorLink`), `GetSearchResultsQueryTests`, `StudentSearchSourceTests` | passing |
| AC-005 | `GetSearchResultsQuery.ExecuteAsync` (states), `SearchController`, the view's messages | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | passing |
| AC-006 | `StudentSearchSource.Listed`, the reused Meet queries | `StudentSearchSourceTests`, `SearchResultsPageTests.AnErasedPerson_AndAMeetingWithThemAsOrganizer_AreNotFound`, `ErasureExclusionInAdvanceSyncTests.TheFirstRun_StoresNothingOfTheExcludedStudent_AndFillsTheExclusionWithTheirGoogleUserId` (the excluded person) | passing |
| AC-007 | `[Authorize(Policy = ViewCourses)]` on `SearchController`, `SearchResultsLog` | `SearchAccessTests`, `PermissionMatrixTests`, `StudentSearchSourceTests` | passing |
| AC-008 | `Search.Warning`, `#search-history-warning` in every state | `SearchResultsPageTests`, `SearchTranslationTests` | passing |
| AC-009 | `PageSearchHint.*` in both `.resx`, `_PageSearchHint.cshtml` | `PageSearchHintTests`, `PageSearchHintTranslationTests`, `SearchTranslationTests`, `DataHealthPageTests` | passing |
| AC-010 | no index added | `SearchMeasurementTests`; numbers in §5 | measured |
| AC-011 | `.btn:disabled` in the shared part of both `site.css` | `DisabledButtonRuleTests` | automated part passing; manual walk done (§3.1) |
| AC-012 | `PageHeaderRules.IsErrorPage`, `ShowsLanguageSwitcher`, `_LanguageSwitcher.cshtml` | `ErrorPageHeaderTests` | passing |
| AC-013 | no production change (FR-013) | `MarkupTextSurvivesActionsTests`, `MeetCodeSearchSurvivesActionsTests` (that `MarkForm` emits `returnQ`), `PickerNarrowScriptTests` | passing |

### 3.1 Manual check of AC-011

Walked by the Owner on 2026-11-03 on the temporary stand with synthetic data, signed in as a Dean, in a clean Edge profile (spec section 9).

| Check | Computer | Phone width |
|---|---|---|
| `/workspace/meet-codes/abc-defg-hij/course-choice`, narrowed to no row: «Нічого не знайдено», the submit button grey with readable text | seen | seen |
| The header: field, button and language switcher do not overlap; the field is full width under the switcher at phone width | seen | seen |
| The page title does not wrap oddly (§7 item 7) | seen | seen |
| Header search for `мат`: «Результати пошуку», the text stays in the field, the history warning at the bottom, no overlap and no horizontal scroll | seen | seen |

The Owner's screenshots of the course-choice page at widths 400 and 393 (iPhone 16 preset) confirm its rows, with an empty console.

**Limit:** phone width was Edge's device mode, by the Owner's choice, not a real phone, so the stand stayed on localhost with no firewall change. It checks the layout, not a phone browser's own rendering.

## 4. Change Set

Production, created or completed:

| File | Trace |
|---|---|
| `Application/UseCases/GetSearchResultsQuery.cs` | FR-002 … FR-008, VR-001 … VR-003 |
| `Application/UseCases/SearchResultsUrl.cs` | openapi `x-return-path` |
| `Application/Ports/IStudentSearchSource.cs`, `IMeetCodesReadSource.cs` (member) | entity model §1 |
| `Application/Models/StudentSearchRecord.cs`, `StudentSearchCourseRecord.cs`, `MeetCodeMatch.cs`, `Requests/SearchResultsRequest.cs` | entity model §2, §4 |
| `Application/Models/Dtos/SearchResultsPageModel.cs`, `SearchResultsState.cs`, `SearchResultsGroup.cs`, `MeetCodeSearchGroup.cs`, `StudentSearchRow.cs`, `StudentSearchCourse.cs`, `TeacherSearchRow.cs`, `CourseSearchRow.cs` | entity model §3 |
| `Infrastructure/Persistence/StudentSearchSource.cs` | db-design §3 |
| `Infrastructure/Persistence/MeetCodesReadSource.cs` | db-design §4 |
| `Web/Controllers/SearchController.cs` | openapi `getSearchResults` |
| `Web/Controllers/MeetCodesController.cs` (`ListUrl`) | api-design D-5, FR-006, FR-007 |
| `Web/Security/SignInRoutes.cs` (`Search`) | api-design D-1 |
| `Web/Security/PageHeaderRules.cs` | FR-001, FR-010, api-design D-3 |
| `Web/Security/SearchResultsLog.cs` | openapi `x-log`, api-design D-10 |
| `Web/Security/SearchTextKeys.cs` | openapi `x-translation-keys` |
| `Web/Security/PageSearchHintTextKeys.cs` (comment) | FR-011, api-design D-9 |
| `Web/Configuration/InstallationServices.cs` | supporting: DI registration |
| `Web/Views/Search/Index.cshtml` | openapi `x-markup-results-page` |
| `Web/Views/Shared/_HeaderSearch.cshtml`, `_Layout.cshtml` | FR-001, openapi `search-field` |
| `Web/Views/Shared/_LanguageSwitcher.cshtml` | FR-010 |
| `Web/Views/Shared/_PageSearchHint.cshtml` | FR-011, D-9 |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | supporting: translation entries; FR-011 |
| `Web/wwwroot/css/site.css` | FR-012; header and results styles, FR-001 phone width |
| `ControlPlane/wwwroot/css/site.css` | FR-012 |

Tests: the files of `docs/evidence/US-071-test-generation-report.md`, created at `TEST_WRITING`. Changed here: `Infrastructure/Persistence/StudentSearchSourceTests.cs` (§7, items 1 and 10) and `Web/Pages/SearchResultsPageTests.cs` (§7, item 9).

Workflow artifact: this report.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln -nodeReuse:false -p:UseSharedCompilation=false` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build`, first run | 2 | total 7559, passed 7558, failed 1, skipped 0; 11 min 35 s. The failure: §7, item 1 |
| `dotnet test --no-build --filter FullyQualifiedName~StudentSearchSourceTests`, after the fix | 0 | total 36, passed 36 |
| same build after the last test edit | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build`, final | 0 | **total 7559, passed 7559, failed 0, skipped 0**; 11 min 33 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no output |

Attempt 2, after the changes of §7 items 8 to 10 (the table above is attempt 1):

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln -nodeReuse:false -p:UseSharedCompilation=false` | 0 | 0 warnings, 0 errors |
| `dotnet test --no-build --filter FullyQualifiedName~SearchResultsPageTests` | 0 | total 57, passed 57 |
| `dotnet test ClassroomAgent.sln --no-build`, first run | 2 | total 7561, passed 7560, failed 1, skipped 0; 13 min 58 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no output |
| `dotnet test --no-build --filter FullyQualifiedName~InstallationCookieTests` | 0 | total 8, passed 8 |
| `dotnet test ClassroomAgent.sln --no-build`, second run, same build | 0 | **total 7561, passed 7561, failed 0, skipped 0**; 11 min 23 s |

The one failure: `InstallationCookieTests.ThePublicPortSendsHsts_AndThePrivatePortDoesNot`, `Npgsql.NpgsqlException: Exception while reading from stream` / `System.TimeoutException`, thrown from `PostgreSqlFixture.DropDatabaseAsync` in `InstallationTestHost.DisposeAsync`, the teardown after the test's assertions. It took 2 min 36 s. No code changed between that run and the green one, and this Story does not touch that class. Read as the machine's load, not a defect; the cause was not investigated further.

Baseline: the red state recorded in the test generation report (7559 cases, 262 failing, expected 263 after its last two changes) with the same skeleton; it was not run again before the first production edit.

**AC-010 measurement** (`SearchMeasurementTests`, real PostgreSQL, no new index, five runs after one warm-up, on the development machine). Data: 1000 students, 300 teachers, 600 courses, 10 memberships per student, 300 Meet codes, no submissions, as the search reads none.

| Text | Median | Worst |
|---|---|---|
| `ан` (common 2-character word) | 67 ms | 81 ms |
| `прізвище123 анна` (2-word name) | 55 ms | 68 ms |
| `ъъъъъъ` (no match) | 38 ms | 40 ms |

The bound of spec section 9 is a median under 2 seconds. These numbers are from one isolated run of the class in attempt 1; a recorded measurement, not an assertion. Attempt 2 changed no query.

Delegated: every full-suite run and the final format check, to `cheap-worker`; in attempt 2 every build and test run as well. Inline: all production code, the translation entries and CSS (scripted edits), the one class run, and the interpretation of the failure. The views and translation entries were not delegated because they were written together with the contract's markup in one pass.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

1. **A test's setup was changed.** `StudentSearchSourceTests.AnErasedPerson_IsNotFound_ByNameOrEmail` started a host with no succeeded legitimacy check, so the erasure in its body was refused with `ReadOnlyModeException`. In the red phase the test stopped earlier, on the skeleton, so this did not show. The host now starts as `ParticipantErasureTests` starts it. No assertion changed; the test does not contradict any artifact, so this was treated as a setup defect and not returned to `TEST_WRITING`.
2. **`CourseSearchRow` follows the entity model, not the openapi.** The openapi's `course-row` names `CoursesTextKeys.Status(status)`; the code uses `CoursesTextKeys.State(row.State)`, the same translation key. This is `US-071/DB_DESIGN/m-1`. The openapi's sentence stays stale, since this stage may not edit it, so the finding stays OPEN for the human.
3. **The view-data key's name.** The openapi calls it `HeaderSearchTextKey`; the code has `PageHeaderRules.SearchTextViewDataKey`, value `HeaderSearchText`. Same behaviour.
4. **Markup details the openapi does not list.** The form has the extra class `header-search`; a refused text's message has `form-error`, as on the lists; rows are lists with the classes `search-rows`, `search-row-detail`, `search-student-courses`.
5. **D-8 also applied to the teacher row.** The openapi's `teacher-row` says "the label, the email"; D-8 states the rule generally, so a teacher with no stored name shows the address once.
6. **Log lines.** `Prompt` writes none, as it shows no totals and `x-log` lists only "shown" and "refused".
7. **The installation's title keeps its right padding.** The shared rule reserving 11rem beside the title stays, as the shared part is word for word on both hosts, though the switcher now shares its row with the header field in the installation. Titles may wrap a little earlier. To be judged in the walk.

8. **M-1 and m-1 of the verification.** `data-participant-id` on the student and teacher rows and `class="search-group"` on the four sections of `Views/Search/Index.cshtml` are removed, so the markup now matches the openapi on both. `participantId` stays in the row DTOs, as the openapi says.
9. **m-2: a test added at this stage.** `SearchResultsPageTests.WithExactly20MeetCodes_ThereIsNeitherLineNorLink`, two cases: 20 on one list, and 9 + 8 + 3 over the three lists. It passed at once, as it guards behaviour already built; its control is `TheMeetLink_OpensTheFirstListWithAMatch_AndTheTotalIsTheSumOfTheCounters`, which sees the line and the link at 22 through the same helpers. No mutation run was made. The `ac_test_matrix` belongs to `TEST_WRITING` and does not list this test.
10. **m-3: a test's summary corrected, the test unchanged.** `AnExcludedPerson_IsNotFound_ByTheirEmail` still stores no participant with that email, so it proves little by itself; its summary now says so and names `ErasureExclusionInAdvanceSyncTests`, which proves no row is stored. §3 names that test under AC-006.
11. **m-4.** §3 now names `MeetCodeSearchSurvivesActionsTests` under AC-013. The `ac_test_matrix` does not, for the reason in item 9.

`US-071/API_DESIGN/m-1` (the Control Plane switcher's same fallback) was not touched: outside FR-010 and AC-012, so it stays OPEN for the human.

## 8. Open Decisions

None touched and none newly required.
