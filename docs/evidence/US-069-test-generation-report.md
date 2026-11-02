---
artifact_type: test_generation_report
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T12:23:40Z
updated_at: 2026-11-02T12:23:40Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/tests/US-069-test-strategy.md
    version: 1
  - path: docs/tests/US-069-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-069 Test Generation Report

**Result: red phase verified.** 131 new test cases: 74 red for missing behaviour, 57 green by design. 7 existing `PageSearchHintTests`
cases are red through the adapted ordinal. The other 6983 existing cases pass.

## Files

Created, under `tests/ClassroomAgent.Tests/`:

- `TestInfrastructure/PasswordFieldMarkup.cs` (reads the control from a page and from the script)
- `Web/Pages/PasswordToggleMarkupTests.cs` (29 cases)
- `ControlPlane/Controllers/ControlPlanePasswordToggleMarkupTests.cs` (14)
- `Architecture/PasswordToggleSourceTests.cs` (43)
- `Architecture/OnePageTitleRuleTests.cs` (11)
- `Web/Pages/SmallTextsPageTests.cs` (15)
- `Web/Localization/SmallTextsTranslationTests.cs` (10)
- `ControlPlane/Localization/PasswordToggleTranslationTests.cs` (4)
- `Web/Pages/PasswordValueNotExposedTests.cs` (3)
- `ControlPlane/Controllers/ControlPlanePasswordValueNotExposedTests.cs` (2)

Modified (spec Traceability, AC-002):

- `Web/Pages/PageSearchHintTests.cs`: `TitleOrdinalOf` is 1 for every page.
- `TestInfrastructure/PageSearchHintTestData.cs`: the summary of `AfterTitle`.

**Compile-only skeleton: none.** The tests reference no new C# declaration. No production file was touched.

## Delegation

Kept here: test design, `PasswordFieldMarkup`, the markup and source-scan classes, the red-phase verdict and the corrections below.
Delegated to `cheap-worker` against a fixed brief: `SmallTextsPageTests`, both translation classes, both `…NotExposed` classes, the two
`PageSearchHint` edits, and every build and test run.

## Commands and results

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (first run, free RAM 5.9 GB, Docker 29.8.0) | total 7121, failed 81, passed 7040, skipped 0; 12 min 06 s; no environment failure |
| `--filter FullyQualifiedName~PasswordValueNotExposedTests` (after the corrections) | 5/5 passed |
| `--filter FullyQualifiedName~.PasswordToggleMarkupTests.AfterARefused` | 2/2 red, as expected |
| `--filter FullyQualifiedName~.ControlPlanePasswordToggleMarkupTests.AfterARefused` | 2/2 red, as expected |
| `--filter FullyQualifiedName~.SmallTextsPageTests.WithAChosenCourse` | 1/1 red, as expected |

**The full suite was not rerun after the corrections,** which touched four test files only. Combining the first run with the reruns gives the
counts below: one guard moved to green, one vacuous test moved to red, so 81 red and 7040 green still.

## Corrections after the first run

1. `AfterARefusedSignIn_TheFieldStartsHidden` (installation) and `AWrongDeanSignIn_DoesNotExposeThePassword` were **invalid**. A refused Dean
   sign-in answers `302` back to `/sign-in`, not a re-rendered form (existing `DeanSignInPageTests`), so the first would have stayed red after
   implementation. Both now follow the redirect.
2. `WithAChosenCourse_ThePromptIsNotSelected_AndTheCourseIs` was **vacuous**: green because it never read the prompt's text. It now does, and it is red.
3. The four `AfterARefused…` tests now assert the re-render first (status and the count of password inputs), so their red comes from the missing
   control and not from a missing form. Confirmed by the failing line in each.
4. Both `…NotExposed` classes read logs after stopping the host, so the absence of the marker is not a read racing the file write.

## Red and green, by class

| Class | Cases | Red | Green |
|---|---|---|---|
| SmallTextsPageTests | 15 | 13 | 2 |
| SmallTextsTranslationTests | 10 | 5 | 5 |
| PasswordToggleTranslationTests | 4 | 2 | 2 |
| PasswordValueNotExposedTests | 3 | 0 | 3 |
| ControlPlanePasswordValueNotExposedTests | 2 | 0 | 2 |
| PasswordToggleMarkupTests | 29 | 23 | 6 |
| ControlPlanePasswordToggleMarkupTests | 14 | 11 | 3 |
| PasswordToggleSourceTests | 43 | 19 | 24 |
| OnePageTitleRuleTests | 11 | 1 | 10 |
| **New, total** | **131** | **74** | **57** |
| PageSearchHintTests (adapted, `meet-codes`) | | 7 | |

**Why each red is expected:** the `<h1>` count is 2 on the four views; the role line, the headings and the prompt are not rendered; translation keys are
missing; `password-toggle.js` and the icons do not exist (404, "Missing file"); no wrapper or control is rendered; `site.css` holds no control rule.
**Unexpected failures: none.**

**Why each green is expected:**

- 30 scan self-tests prove the scanners report and accept what they should (24 in `PasswordToggleSourceTests`, 5 in `OnePageTitleRuleTests`,
  `TheFormScan_ListsANamedButton`).
- AC-008 form fields (6 cases): they fail the moment the control gains a name.
- AC-009 (5 cases): the behaviour exists today and must not change.
- `APageWithNoPasswordField_DoesNotLoadTheScript` (2): a negative that holds until someone wires the script into a layout.
- `EveryCounter_StaysInItsBlock`, `AnEmptyCourseId_ShowsNoReport_LikeNoCourse`: behaviour to keep. The empty id answered the same status as no id.
- The other 5 of `OnePageTitleRuleTests`: the Control Plane has no stray `<h1>`, each layout holds one, every non-partial view sets the title.
- Translation guards: the same keys in both files, no literals, `Landing.Role.*` kept, the prompt key reused. `Курси / Courses` already exists under another
  key, so that one pair cannot be red; the page test of AC-003 proves the heading is rendered.

## Untested by automation

AC-011 and the browser part of AC-005, AC-006 and AC-007: manual, by the Owner, as the Specification states. No browser tool was added.

## Open Decisions

None raised. OD-001...OD-005 are resolved. `US-069/SPEC_REVIEW/m-4` (the digest's wording) stays OPEN: it is not this stage's to fix.
