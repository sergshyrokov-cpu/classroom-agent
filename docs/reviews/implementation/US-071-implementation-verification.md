---
artifact_type: implementation_verification
story: US-071
version: 2
status: APPROVED
created_at: 2026-11-03T18:19:44Z
updated_at: 2026-11-03T19:03:16Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-071-test-generation-report.md
    version: 1
  - path: docs/evidence/US-071-implementation-report.md
    version: 2
supersedes: docs/reviews/implementation/US-071-implementation-verification.md@v1
---

# US-071 Implementation Verification

## Summary

**Verdict: PASS.** No Critical or Major finding. All five findings of v1 are handled, and the code was read again in a fresh context. One new Minor finding (m-5).

**AC-011 is proved in its automated part only.** The person's walk (grey disabled button and the header field, on a computer and a phone) has not happened, so nobody has yet seen the header's new layout. It stays `US-071/IMPLEMENTATION/m-1`, due before `HUMAN_PR_APPROVAL`.

Delegated to `cheap-worker`: one build and one run of each class in "Story Test Run", no file touched. All reading and judgement were inline.

## Reviewed Artifacts

As in the front matter, at those versions, plus the working-tree diff (`git status --untracked-files=all`, `git diff`, untracked files read directly). No input is stale: the implementation report names the current versions.

Since v1 (18:19:44Z) only these changed in `src`, `tests`, `docs/evidence` and `docs/tests`: `Views/Search/Index.cshtml`, `StudentSearchSourceTests.cs`, `SearchResultsPageTests.cs` and the implementation report. This matches report §4 and §7.

## AC Verification

| AC | Tests read | Verdict |
|---|---|---|
| AC-001 | `HeaderSearchFieldTests`, `ErrorPageHeaderTests` | Proved. `PageHeaderRules` is one rule for both controls, and every path to the error page uses `ErrorPageModel` (`ErrorController.cs:58-62`). |
| AC-002 | `SearchResultsPageTests`, `StudentSearchSourceTests`, `GetSearchResultsQueryTests` | Proved. Order, headings and hidden groups, over HTTP on real PostgreSQL. |
| AC-003 | same | Proved. The dated test uses 22:30 UTC, so a wrong zone shows; the mark is per course; the courses come in the port's order through `ToLookup`, which keeps it. |
| AC-004 | same | Proved, now including Meet codes at exactly 20, on one list and as 9 + 8 + 3 (`WithExactly20MeetCodes_ThereIsNeitherLineNorLink`). It would fail on a limit line, as the helpers it uses find the line at 22 in `TheMeetLink_OpensTheFirstListWithAMatch_AndTheTotalIsTheSumOfTheCounters`. |
| AC-005 | `SearchResultsPageTests`, `GetSearchResultsQueryTests` | Proved. `ARefusedText_Answers400_WithTheMessage_AndIsNotEchoed` checks the status, message, empty field, bare return path and absence of the text. |
| AC-006 | `StudentSearchSourceTests`, `SearchResultsPageTests`, `ErasureExclusionInAdvanceSyncTests` | Proved. The excluded person's proof is `TheFirstRun_StoresNothingOfTheExcludedStudent_AndFillsTheExclusionWithTheirGoogleUserId`, which asserts no row is stored, so search has nothing to match. `AnExcludedPerson_IsNotFound_ByTheirEmail` proves little alone, and its summary now says so. |
| AC-007 | `SearchAccessTests`, `PermissionMatrixTests`, `StudentSearchSourceTests` | Proved. `[Authorize(Policy = ViewCourses)]` on the controller; the log lines carry counts or the parameter's name only. |
| AC-008 | `SearchResultsPageTests`, `SearchTranslationTests` | Proved. The warning is outside every state branch of the view. |
| AC-009 | `PageSearchHintTests`, `PageSearchHintTranslationTests`, `SearchTranslationTests`, `DataHealthPageTests` | Proved. The resx texts were compared with FR-011 and `x-translation-keys` by eye. |
| AC-010 | `SearchMeasurementTests` | Measured and rerun here: "Story Test Run". |
| AC-011 | `DisabledButtonRuleTests` | **Automated part proved; the manual walk has not happened.** `.btn:disabled` outranks a colour class's variables and sets the opacity to 1. |
| AC-012 | `ErrorPageHeaderTests` | Proved. The marker assertion comes first, and the test generation report records it failing in the red phase. |
| AC-013 | `MarkupTextSurvivesActionsTests`, `MeetCodeSearchSurvivesActionsTests`, `PickerNarrowScriptTests` | Proved. The report now names the second class; the matrix does not (m-5). |

## Specification Coverage

FR-001 … FR-014 and VR-001 … VR-004 are implemented where the report says. Checked in the code:

- `GetSearchResultsQuery` follows the evaluation order. All states go through `CourseListQueryRules.ReadSearch`, and a refused text is kept nowhere.
- `StudentSearchSource` qualifies a person by a student membership on the roster or not, reads courses of student memberships only, in the course list's order, and uses the journal's `FirstSeenAt == LastSeenAt`.
- The controller reads only `q` (VR-004).
- Refusal texts use the existing `Courses.Validation.*` keys; course status uses `Courses.Status.*` through `CoursesTextKeys.State`.
- No entity, audit event, Google port or write was added (FR-014).

Behaviour no artifact asks for: none. v1's M-1 is gone: `data-participant-id` no longer appears in the view.

## Design Conformance

- **Route, status codes, policy, log lines:** match `getSearchResults`, `x-log` and `x-return-path`. No JSON API, so no error body.
- **Markup:** matches `x-markup-results-page` and `search-field`, apart from the disclosed items below.
- **Translations:** the `.uk` and `.en` entries match `x-translation-keys`.
- **Database:** no schema change, as designed. `GetFirstMatchesAsync` is three commands over the screen's own queries, merged by ordinal code (db-design §4).
- **Architecture:** AD-3, AD-4, AD-6 and AD-8 hold. The controller holds no `DbContext` or rule, no Google type crosses, the page only reads and so has no guard, and the view uses DTOs.

## Deviations

| # | Departs from | Reason | Verdict |
|---|---|---|---|
| Report 1 | test as written at TEST_WRITING | `AnErasedPerson_IsNotFound_ByNameOrEmail` started a host where the erasure is refused. Setup only; every assertion stays. | Sound |
| Report 2 | openapi `course-row` | Code follows the entity model's `CourseState`, the same translation key (`US-071/DB_DESIGN/m-1`). | Sound; the openapi sentence stays stale |
| Report 3 | openapi view-data key name | Name only, same behaviour. | Sound |
| Report 4 | openapi markup | Classes for styling: `header-search`, `form-error`, `search-rows`, `search-row-detail`, `search-student-courses`. Each has a rule or an existing use. | Sound; complete now |
| Report 5 | openapi `teacher-row` | D-8 states the rule generally: an address that is the label shows once. | Sound |
| Report 6 | none in substance | `Prompt` writes no log line, as `x-log` lists only "shown" and "refused". | Sound |
| Report 7 | none in substance | The title's right padding stays, so titles may wrap earlier. | Sound; the Owner judges it in the walk |
| Report 9, 11 | `ac_test_matrix` | Tests added or named at IMPLEMENTATION, while the matrix belongs to TEST_WRITING. | Sound and disclosed; see m-5 |
| Report 10 | none | A test's summary corrected; the test unchanged. | Sound: it now says what it proves |

Report item 8 returns the code to the openapi, so it is not a deviation.

## Report Accuracy

- The production change set in §4 equals the diff, file by file.
- Among test files, only `StudentSearchSourceTests.cs` and `SearchResultsPageTests.cs` are newer than the test generation report, as §4 says.
- The AC-to-test locations exist, including the two named in attempt 2.
- Class totals match: `SearchResultsPageTests` 57, as the report says.
- The full-suite figures (7561 of 7561, after one teardown timeout in `InstallationCookieTests`) were not rerun here. `security-reviewer` runs the suite; the cause of that timeout was not investigated, as the report says.
- Clean change: no `TODO`, `TBD`, `FIXME`, skipped test or `NotImplementedException` in the changed `.cs`, `.cshtml` and `.css` files.

## Story Test Run

Docker answered. Build: `dotnet build ClassroomAgent.sln -nodeReuse:false -p:UseSharedCompilation=false`, exit 0, 0 warnings, 0 errors. Each class: `dotnet test --no-build --filter FullyQualifiedName~<Class>`, exit 0, none failed or skipped. A filter also matches other classes whose names end the same way.

| Class | Passed |
|---|---|
| `GetSearchResultsQueryTests` | 28 |
| `StudentSearchSourceTests` | 36 |
| `SearchResultsPageTests` | 57 |
| `HeaderSearchFieldTests` | 32 |
| `ErrorPageHeaderTests` | 7 |
| `MarkupTextSurvivesActionsTests` | 6 |
| `MeetCodeSearchSurvivesActionsTests` | 23 |
| `PageSearchHintTests` | 63 |
| `SearchAccessTests` | 30 |
| `PermissionMatrixTests` | 3 |
| `SearchTranslationTests` | 48 |
| `PageSearchHintTranslationTests` | 5 |
| `DisabledButtonRuleTests` | 9 |
| `PickerNarrowScriptTests` | 27 |
| `DataHealthPageTests` | 26 |
| `ErasureExclusionInAdvanceSyncTests` | 15 |
| `SearchMeasurementTests` | 1 |

AC-010 in this run, from the file the test writes outside the repository: `ан` median 26 ms, worst 33 ms; `прізвище123 анна` median 24 ms, worst 25 ms; `ъъъъъъ` median 15 ms, worst 23 ms. The report's numbers are higher (67, 55, 38 ms median). All are far under the 2-second bound, so the difference does not matter.

## Findings

### Findings of v1

| v1 | What | Now |
|---|---|---|
| M-1 (Major) | `data-participant-id` on student and teacher rows | Fixed: gone from the view; `participantId` stays in the DTOs, as the openapi says. |
| m-1 | unused `class="search-group"` | Fixed: gone. `search-group-limit` and `search-group-whole-list` are the contract's own classes. |
| m-2 | Meet codes at exactly 20 untested | Fixed: new theory, two cases, read and passing. |
| m-3 | the excluded person's test overstated | Fixed in the summary and report §3. The test still proves little alone; the AC rests on `ErasureExclusionInAdvanceSyncTests`, read here. |
| m-4 | report did not name the `MarkForm` proof | Fixed in report §3. |

### m-5 Minor: the matrix does not name three tests the ACs now rest on

- **Where:** `docs/tests/US-071-ac-test-matrix.md`, AC-004, AC-006 and AC-013 rows.
- **Checked against:** implementation report §3, §7 items 9 to 11.
- **What is wrong:** `WithExactly20MeetCodes_ThereIsNeitherLineNorLink`, `ErasureExclusionInAdvanceSyncTests.TheFirstRun_…` and `MeetCodeSearchSurvivesActionsTests` are absent, and the AC-006 row still gives `AnExcludedPerson_IsNotFound_ByTheirEmail` as the excluded person's proof. Only the report names them. Every AC still maps to a passing test (Definition of Done item 3). No stage after TEST_WRITING may edit the matrix, and looping back for this alone is not worth it, so the human decides.

### OPEN findings from earlier stages

None closed here; closing them as `ACCEPTED` or `DEFERRED` is the human's.

- `US-071/IMPLEMENTATION/m-1`: the AC-011 walk. Still not done; due before `HUMAN_PR_APPROVAL`.
- `US-071/DB_DESIGN/m-1`: the code is right and the openapi sentence is stale.
- `US-071/API_DESIGN/m-1`: the Control Plane switcher's same fallback, outside FR-010 and AC-012. Not fixed by this Story.

## Verdict

`PASS` → `SECURITY_REVIEW`. No artifact upstream of the code is at fault.
