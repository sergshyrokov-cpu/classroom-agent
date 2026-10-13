---
artifact_type: test_generation_report
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T18:03:08Z
updated_at: 2026-10-13T18:03:08Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/designs/api/US-046-api-design.md
    version: 1
  - path: docs/designs/database/US-046-db-design.md
    version: 1
  - path: docs/tests/US-046-test-strategy.md
    version: 1
  - path: docs/tests/US-046-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-046 Test Generation Report

**Overall result: PASS — red phase verified.**

## Files

Created (no file modified, no production file touched):

- `tests/ClassroomAgent.Tests/TestInfrastructure/PageSearchHintTestData.cs` — hook, the six texts of spec FR-003, markup readers
- `tests/ClassroomAgent.Tests/Web/Pages/PageSearchHintTests.cs` — 12 test methods, integration
- `tests/ClassroomAgent.Tests/Web/Localization/PageSearchHintTranslationTests.cs` — 4 test methods, source

## Commands

- `dotnet build ClassroomAgent.sln` — succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~PageSearchHint"` — 64 total, 4 passed, 60 failed, 0 skipped.
- `dotnet test ClassroomAgent.sln` (full suite, Docker up) — 5561 total, 5501 passed, 60 failed, 0 skipped, 5m59s. **All 60 failures are US-046 tests; no existing test fails.**

Delegated: the first build and the filtered run to a `cheap-worker` subagent. Test design, all test code, the full-suite run and the red-phase classification were done here.

## Red-phase classification

| Tests | Count | Failure | Classification |
|---|---|---|---|
| `EveryTeachingDataPage_ShowsItsHintOnce_DirectlyUnderTheTitle` (9 pages × Admin/Dean), `InEnglish_…` (9), `InReadOnlyMode_…` (9), `APhoneBrowser_…` (9), `WithNoRowsAtAll_…` (6), `TheCourseListHint_…` (2), `TheJournalWithAValidationMessage_…` (2), `ThePagedListHint_…` (1), `ShowingTheHint_WritesNothing_…` (1), `NoOtherInstallationPage_HasTheHint` (1) | 58 | `Assert.Single() Failure: The collection was empty` — no hook element | expected: hint not implemented. The preceding `HttpStatusCode.OK` (or 400/404) assertion passed in each, so every page, seed and URL is valid. |
| `EachHintText_IsAnEntryOfTheTranslationFileOfItsLanguage` | 1 | the uk ordinary text is not a resx value | expected: entries not added yet |
| `TheStylesheet_StylesTheHintClass` | 1 | "site.css has no rule for .page-search-hint." | expected: style not added yet |

`NoOtherInstallationPage_HasTheHint` was reordered so that its absence checks run before the final presence control; rerun alone, it now fails only on the control (line 355), proving every absence page — landing, Dean accounts, connection, instruction, access check, template list/new/edit/deletion, Meet code course-choice, Dean landing and own password, error pages (unknown card 404, `/error/404`, `/error/500`, anonymous 404), sign-in — opened with the expected status and carried no hook.

## Tests passing before implementation (explained)

| Test | Why it passes now | Why it is not vacuous |
|---|---|---|
| `TheForcedPasswordChange_HasNoHint` | no page has a hint yet | absence guard for FR-005; the same hook detection fails on the 58 presence tests |
| `NoControlPlanePage_HasTheHint` | same | guards against a shared partial leaking into the Control Plane |
| `NoView_HoldsTheHintTextAsALiteral` | no view mentions the texts | becomes meaningful once views render the hint (FR-008) |
| `TheHint_IsNeverRenderedRaw` | no view contains the hook | becomes meaningful once a view renders it (S-03); no view uses `Html.Raw` today |

## Coverage

Every AC (AC-001 … AC-009) is mapped in `docs/tests/US-046-ac-test-matrix.md`.
Untested by automation (visual): "small and unobtrusive" beyond a `font-size`
rule, wrapping on a phone — left to human review.

## Open Decisions

None.
