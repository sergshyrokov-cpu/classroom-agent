---
artifact_type: test_generation_report
story: US-075
version: 1
status: DRAFT
created_at: 2026-11-07T19:01:47Z
updated_at: 2026-11-07T19:01:47Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/designs/database/US-075-db-design.md
    version: 1
  - path: docs/tests/US-075-test-strategy.md
    version: 1
  - path: docs/tests/US-075-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-075 Test Generation Report

## 1. Files

**Created**

- `tests/ClassroomAgent.Tests/Web/Pages/DataHealthHintsTests.cs`: 11 methods, 38 cases. AC-003 … AC-008.
- `tests/ClassroomAgent.Tests/Web/Localization/DataHealthHintTranslationTests.cs`: 5 methods, 6 cases. FR-008,
  VR-003, FR-001.4, FR-009.1.
- `tests/ClassroomAgent.Tests/TestInfrastructure/DataHealthHintTexts.cs`: the FR-008 literals, uk and en.

**Modified**

- `tests/ClassroomAgent.Tests/TestInfrastructure/DataHealthHostExtensions.cs`
  - `Banner` now returns the whole banner element's text. It no longer asserts that the banner is one link to the
    page, because FR-001 makes it two links.
  - New helpers: `BannerHtml`, `BannerLinks`, `SectionHtml`, `HintParagraphs`, and the constants `HintClass` and
    `BannerPartClass`.
  - The regex `BannerLink` is replaced by `AnchorTag` and `Paragraph`.
- `tests/ClassroomAgent.Tests/Web/Pages/DataHealthBannerTests.cs`
  - New tests for AC-001, AC-002 and AC-007: `EachPartOfTheBanner_IsALinkToItsSection`,
    `AZeroPart_IsPlainText_AndTheOtherPartIsTheLink` and `InReadOnlyMode_TheBannerParts_AreLinks`.
  - `TheBanner_IsRestrained` now checks the whole `<p>`, not the markup up to the first `</a>`. The forbidden-element
    list is unchanged, so the test is stricter, not weaker.
- `tests/ClassroomAgent.Tests/Web/Pages/PageSearchHintTests.cs` (FR-007): «Стан даних» (`data-health`) joins the
  teaching-page list and the "no rows at all" theory.

## 2. Compile-only skeleton

None. The tests reference no new production declaration: translation keys, CSS classes and markup hooks are strings,
which the test strategy §3 fixes. No production file was touched.

## 3. Commands

The build and test runs were delegated to a `cheap-worker` subagent. Test design, markup hooks, helpers and the
red-phase verdict were done inline.

- `dotnet build ClassroomAgent.sln`: succeeded, 0 warnings, 0 errors.
- The first test attempt failed on the environment. Docker Desktop was not running, so every `PostgreSqlFixture`
  constructor threw. Docker Desktop was then started and the runs below were repeated with `--no-build`, one
  `--filter FullyQualifiedName~<Class>` per run:

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| DataHealthHintTranslationTests | 6 | 2 | 4 | 0 |
| DataHealthHintsTests | 38 | 15 | 23 | 0 |
| DataHealthBannerTests | 33 | 25 | 8 | 0 |
| DataHealthPageTests (regression) | 26 | 26 | 0 | 0 |
| PageSearchHintTests (incl. FR-007 additions) | 69 | 69 | 0 | 0 |

The full suite was not run at this stage. Only the production view, stylesheet and resx will change, and the classes
that read them are the five above. IMPLEMENTATION runs the full suite.

## 4. Expected failures (red: missing production behaviour)

- **Translations:** `EachNewText_IsAnEntry_WithTheAgreedValue` uk and en (2 cases) fail with "no
  DataHealth.Hint.AboveMaximum". The keys do not exist yet.
- **Stylesheet:** `TheStylesheet_KeepsEachBannerLinkOnOneLine` and `TheStylesheet_StylesTheHintClass` fail because
  `site.css` has no such rules yet.
- **Teaching hints:**
  - `EachTeachingHint_AppearsOncePerCourseThatHasIt`: 4 cases, expected 2, got 0.
  - `TheTeachingHints_SitUnderTheHeading_BeforeTheList`: the hint text is not found.
- **Admin's technical hints:**
  - `TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle`: 10 cases, the hint collection is empty.
  - `TheX5Hint_PrecedesTheForm_AndTheX3Hint_FollowsTheCourseName`: no matching hint.
- **Dean's X4 title:** `TheDeansX4Title_EndsAtItsDate` uk and en (2 cases). The Dean still reads «… — перевірте
  роль технічного акаунта» and «… — check the technical account's role». This is the OD-001 a) change.
- **Read-only mode:** `InReadOnlyMode_TheHintsThatPromiseNothingBlocked_AreShown` for T1, T2, X3 and X4 (4 cases)
  finds no hint.
- **No script / Google / audit:** `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent` fails
  at its guard "Expected at least five hints, found 0". Its no-script, Google and audit assertions run once the hints
  exist.
- **Banner links:**
  - `EachPartOfTheBanner_IsALinkToItsSection` ×3: expected 2 links, got 1.
  - `AZeroPart_IsPlainText_AndTheOtherPartIsTheLink` ×4: got the whole banner as one link to `/workspace/data-health`.
  - `InReadOnlyMode_TheBannerParts_AreLinks`: the href is not the section anchor.

Every failure message matches its scenario. There are no syntax, fixture or host-configuration failures.

## 5. New tests that pass before implementation (examined)

These tests assert that something is absent, or that existing behaviour is kept. Such tests are free when nothing
happens, so each one was checked against its positive twin.

| Test | Why it passes now | What keeps it from being vacuous |
|---|---|---|
| `TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin` (10) | no hint exists for anyone yet | same seeding and same `HintParagraphs` reader as `TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle`, which is red; also asserts the Dean's «Повідомте Адміністратора» is present |
| `TheArchivedSection_ShowsNoNewHint` (2) | no hint exists yet | asserts the archived marks and existing texts are present; the teaching twin is red |
| `InReadOnlyMode_TheX5Problem_HasNoHint` | no hint exists yet | twin of the red X5 Admin case; asserts the title, list and form are present |
| `TheAdminsX4Title_StaysWhole` (2) | regression: the Admin's title is unchanged | guards against the Dean's short title leaking to the Admin |
| `NoView_HoldsAHintTextAsALiteral`, `TheAdminsX4Title_AndTheBannerKeys_AreUnchanged` | regression guards | — |
| `PageSearchHintTests` with `data-health` | the page already renders the ordinary hint (US-062/US-070); FR-007 expected this | — |

`TheBanner_IsRestrained` (adapted) and every pre-existing banner and page test pass, so the regression is green.

## 6. Unexpected failures

None. The first attempt's failure was environmental, not a test defect: Docker Desktop was not running.

## 7. Untested Acceptance Criteria

None automated-uncovered. Part of AC-008 is manual by the Story's wording: a person checks the page and banner on a
computer and a phone (NFR-076). IMPLEMENTATION records that check in its report §3.1.

## 8. Open Decisions

None.

## 9. Result

PASS. All AC-001 … AC-008 are mapped. The tests compile and the regression tests pass. The new behaviour tests fail
only for missing behaviour; the absence tests are explained in section 5.
