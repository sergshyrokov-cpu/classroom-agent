---
artifact_type: implementation_report
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T18:17:20Z
updated_at: 2026-10-13T18:17:20Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-046-page-search-hint.md
    version: null
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/designs/database/US-046-db-design.md
    version: 1
  - path: docs/tests/US-046-test-strategy.md
    version: 1
  - path: docs/tests/US-046-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-046 Implementation Report — Page-search hint

## 1. Summary

The hint is one shared partial, `Views/Shared/_PageSearchHint.cshtml`. It renders the
api-design §3 hook `<p id="page-search-hint" class="page-search-hint" data-variant="…">`
with the translated text, HTML-encoded. Each of the nine FR-001 views calls it
first in its body. On Meet codes the call sits directly after the view's own
`<h1>` (OD-002 a). The texts are three keys in each language. A
`.page-search-hint` rule in `site.css` makes the line small. No controller, use
case, route, policy, entity or migration changed. All tests are green.

## 2. Source Artifacts

As in `inputs` above. API_DESIGN and DB_DESIGN are NOT_APPLICABLE: there is no
`openapi.yaml` and no entity model, by design.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (`PageSearchHintTests` = P, `PageSearchHintTranslationTests` = T) | Status |
|---|---|---|---|
| AC-001 | `_PageSearchHint` with `Ordinary` in Courses/Card, Journal/Index, Report/Index, MeetReports/Index, MeetReports/Meeting, Database/Index | P `EveryTeachingDataPage_…`, `InEnglish_…` | PASS |
| AC-002 | `CourseList` in Courses/Index; texts quote `Courses.Form.Search` verbatim | P `TheCourseListHint_QuotesTheSearchFieldLabel` | PASS |
| AC-003 | `PagedList` in Teachers/Index and MeetCodes/Index (after own h1) | P `EveryTeachingDataPage_…` | PASS |
| AC-004 | static partial: no state, no script, no cookie, no device check | P `InReadOnlyMode_…`, `WithNoRowsAtAll_…`, `ThePagedListHint_…`, `TheJournalWithAValidationMessage_…`, `APhoneBrowser_…`, `ShowingTheHint_WritesNothing_…` | PASS |
| AC-005 | partial called only by the nine views | P `NoOtherInstallationPage_HasTheHint`, `TheForcedPasswordChange_HasNoHint`, `NoControlPlanePage_HasTheHint` | PASS |
| AC-006 | `PageSearchHint.{Ordinary,CourseList,PagedList}` in `SharedResource.{uk,en}.resx`; `@T[…]`, no `Html.Raw` | T `EachHintText_…`, `NoView_HoldsTheHintTextAsALiteral`, `TheHint_IsNeverRenderedRaw` | PASS |
| AC-007 | nothing else changed | full suite | PASS |
| AC-008 | one partial call per view | P presence tests (`Assert.Single`, variant) | PASS |
| AC-009 | call is the first body output after the code block / Meet codes h1 | P presence tests (`StartsWithHint`) | PASS |
| I-3 | `.page-search-hint { font-size: 0.875rem; opacity: 0.75; margin: 0 0 1rem; }` | T `TheStylesheet_StylesTheHintClass` | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Security/PageSearchHintTextKeys.cs` | new | FR-003, FR-008; variants of api-design §3; follows the `*TextKeys` pattern |
| `src/ClassroomAgent.Web/Views/Shared/_PageSearchHint.cshtml` | new | api-design §3 hook; S-03 |
| `src/ClassroomAgent.Web/Views/Courses/Index.cshtml` | +1 call | FR-001 (course-list) |
| `src/ClassroomAgent.Web/Views/Courses/Card.cshtml` | +1 call | FR-001 |
| `src/ClassroomAgent.Web/Views/Teachers/Index.cshtml` | +1 call | FR-001 (paged-list) |
| `src/ClassroomAgent.Web/Views/Journal/Index.cshtml` | +1 call | FR-001 |
| `src/ClassroomAgent.Web/Views/Report/Index.cshtml` | +1 call | FR-001 |
| `src/ClassroomAgent.Web/Views/MeetReports/Index.cshtml` | +1 call | FR-001 |
| `src/ClassroomAgent.Web/Views/MeetReports/Meeting.cshtml` | +1 call | FR-001, I-2 |
| `src/ClassroomAgent.Web/Views/MeetCodes/Index.cshtml` | +1 call after own h1 | FR-001, OD-002 a |
| `src/ClassroomAgent.Web/Views/Database/Index.cshtml` | +1 call | FR-001 |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` | +3 entries | FR-003, FR-008 (translation entries) |
| `src/ClassroomAgent.Application/Localization/SharedResource.en.resx` | +3 entries | FR-003, FR-008 (translation entries) |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | +1 rule | FR-002, I-3 |
| `tests/ClassroomAgent.Tests/Web/Pages/PageSearchHintTests.cs` | allowed-cookie list widened (§7) | `ac_test_matrix` AC-004 |

The three test files from TEST_WRITING are part of the Story. Nothing outside
this list was changed. No secret, database file or IDE config is involved.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~PageSearchHint"` (first run) | 63/64; 1 failure, see §7 |
| `dotnet test ClassroomAgent.sln` (full suite) | 5561 total, 5560 passed, 1 failed (the same test), 0 skipped, 6m03s |
| `dotnet test tests/ClassroomAgent.Tests --filter "FullyQualifiedName~PageSearchHint"` (after the §7 fix; this run builds the test project) | 64/64 passed, 0 skipped |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, run twice: before and after the fix |

The §7 change touched only one array in one test, so no other test is
affected. The full suite was not re-run after it.

**Delegated:** the build, the filtered run, the full suite and the first format
check, to a `cheap-worker` subagent. **Inline:** the code, diagnosing the
failure, the test fix, the final filtered run and format check.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **The test's cookie list was too strict.** One check failed:
  `ShowingTheHint_WritesNothing_SetsNoNewCookie_AndIsNotRemembered`. The
  Database page already set `.AspNetCore.Mvc.CookieTempDataProvider` before
  this Story: `DatabaseController` reads TempData for the one-time
  synchronization message (US-019/US-024). The hint touches no TempData and no
  cookie. The test allowed only the session and antiforgery cookies. **With the
  Owner's OK** (asked at IMPLEMENTATION, 2026-10-13), the TempData cookie was
  added to the allowed list, with a comment saying where it comes from. Nothing
  else in the assertion changed.
- **Partial call style.** The project registers no tag helpers, so the
  `<partial>` tag does not work here. The views use
  `@await Html.PartialAsync`, the existing pattern (`_MeetPerson`,
  `_LanguageSwitcher`).

## 8. Open Decisions

None touched or raised.
