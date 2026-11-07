---
artifact_type: implementation_report
story: US-075
version: 2
status: DRAFT
created_at: 2026-11-07T19:36:28Z
updated_at: 2026-11-07T22:55:20Z
produced_by: dotnet-implementor
attempt: 2
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/designs/database/US-075-db-design.md
    version: 1
  - path: docs/tests/US-075-test-strategy.md
    version: 1
  - path: docs/tests/US-075-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-075-test-generation-report.md
    version: 1
supersedes: docs/evidence/US-075-implementation-report.md@v1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-075 Implementation Report

## 0. Attempt 2 (loop-back from HUMAN_PR_APPROVAL)

The Owner rejected attempt 1 after the AC-008 manual check (Edge, DevTools device mode Samsung Galaxy 360 px, Admin
and Dean, look-check stand; history.jsonl 2026-11-07T22:03:20Z). Two findings, both fixed in this attempt:

- **R-1, the comma started the second line.** When the banner wrapped, «, 3 навчальні» began the second line.
  - **Fix:** `_Layout.cshtml` wraps the first part and the separator's punctuation (the separator of
    `DataHealth.Banner` with its trailing whitespace trimmed, i.e. the comma) in
    `<span class="data-health-banner-lead">`; `site.css` gives that span `white-space: nowrap`. The comma stays
    outside the link (FR-001.2) and the line can break only at the separator's space. The translation keys and
    texts are unchanged (FR-001.1).
- **R-2, a banner link overflowed the fill at 360 px.** The banner kept the 11rem right reserve for the language
  switcher (US-068 rule, `site.css` line 75), though on a phone the switcher sits on the row above the header search
  field (`.header-search` phone rule, `margin-top: 2.75rem`).
  - **Fix:** a phone-width rule (`max-width: 575.98px`, the same breakpoint as the header search field) sets
    `max-width: none; margin-right: 0` on `.page-header:has(.language-switcher) .data-health-banner`. Wider screens
    keep the reserve unchanged.

Everything else is as in attempt 1. The manual check (§3.1) has to be repeated by the Owner.

## 1. Summary

Changes in this Story, all presentation only:

- **Banner:** each non-zero part is now its own link to its section of «Стан даних». A zero part is plain text, and
  the separator of `DataHealth.Banner` stays outside the links.
- **Hints:** a fixed hint is added under each kind that had none. Teaching hints go to both roles, technical hints to
  the Admin only. There is no X5 hint in read-only mode, and no new hint in the archived section.
- **Dean's X4 title:** it ends at its date (OD-001 a).
- **Translations and CSS:** eight translation entries in each language, and two stylesheet rules.

There is no change to data, rights, endpoints, schema or audit.

The suite is green with 7837 of 7837 tests. The build has 0 warnings and `dotnet format` reports no changes.

`security_sensitive: false`. Hint selection does follow SEC-002 (no technical details for a Dean). But nothing in
the Story touches authentication, credentials, roles, account state, validation or stored data, and the Dean's view
model is unchanged.

## 2. Source Artifacts

As in the front matter `inputs`.

- API and DB design are NOT_APPLICABLE, so there is no `openapi` file and no entity model.
- No Open Decision is open; OD-001 was resolved a).

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Views/Shared/_Layout.cshtml`: banner `<p>` built from `DataHealth.Banner` around `BannerPart(...)`, and `@functions BannerPart` making an `<a class="data-health-banner-part" href="/workspace/data-health#…">`; since attempt 2 the first part and the separator's comma sit in `<span class="data-health-banner-lead">` (R-1). `wwwroot/css/site.css`: `.data-health-banner-part`, `.data-health-banner-lead { white-space: nowrap; }`, and the phone-width banner rule (R-2) | `DataHealthBannerTests.EachPartOfTheBanner_IsALinkToItsSection` (×3), `TheBanner_IsRestrained`, `TheBannerText_UsesTheLanguagesPluralForms`; `DataHealthHintTranslationTests.TheStylesheet_KeepsEachBannerLinkOnOneLine` | PASS |
| AC-002 | `_Layout.cshtml` `BannerPart`: a count of 0 returns plain text | `DataHealthBannerTests.AZeroPart_IsPlainText_AndTheOtherPartIsTheLink` (×4) | PASS |
| AC-003 | `Views/DataHealth/Index.cshtml` `CourseProblemsBlock(course, archived)`: `<p class="data-health-hint">` under the T2 and T1 headings when not archived. `DataHealthTextKeys.HintAboveMaximum`, `HintItemsNotInClassroom` | `DataHealthHintsTests.EachTeachingHint_AppearsOncePerCourseThatHasIt` (×4), `TheTeachingHints_SitUnderTheHeading_BeforeTheList` | PASS |
| AC-004 | `Index.cshtml`: an Admin-only switch after the problem details for X2a, X2b (hint plus the existing connection link), X3 and X4. The X5 hint sits inside the X5 case, before the form. `DataHealthTextKeys.Hint(kind)` | `DataHealthHintsTests.TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle` (×10), `TheX5Hint_PrecedesTheForm_AndTheX3Hint_FollowsTheCourseName` | PASS |
| AC-005 | `Index.cshtml`: hints only under `Model.IsAdmin`; `TechnicalTitle` uses `DataHealthTextKeys.CourseListEmptyDean` for a Dean | `TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin` (×10), `TheDeansX4Title_EndsAtItsDate` (×2), `TheAdminsX4Title_StaysWhole` (×2) | PASS |
| AC-006 | `Index.cshtml`: the archived section calls `CourseProblemsBlock(course, archived: true)`, so no hint is shown | `DataHealthHintsTests.TheArchivedSection_ShowsNoNewHint` (×2) | PASS |
| AC-007 | `Index.cshtml`: the X5 hint only when `Model.ReadOnly is null`; the other hints do not depend on the mode; no script added | `InReadOnlyMode_TheHintsThatPromiseNothingBlocked_AreShown` (×4), `InReadOnlyMode_TheX5Problem_HasNoHint`, `DataHealthBannerTests.InReadOnlyMode_TheBannerParts_AreLinks`, `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent` | PASS |
| AC-008 | resx entries (8 per language); `.data-health-hint` rule; no change to queries, rules or policies | `DataHealthHintTranslationTests` (6), `ThePageWithEveryHint_…`, `PageSearchHintTests` with `data-health`, the full suite | PASS (automated; manual part PASS 2026-11-07, §3.1) |

### 3.1 Manual check (AC-008, NFR-076)

**Attempt 1: done by the Owner 2026-11-07, rejected** (R-1, R-2 above; no horizontal scroll, hints and the Dean's
view were fine). **Attempt 2: done by the Owner 2026-11-07T22:55:20Z, PASS** (Edge DevTools device mode, look-check stand, Admin and
Dean): at 360 px the banner fits on one line inside its fill (R-2 fixed); at 280 px it wraps to two lines with the
comma at the end of the first (R-1 fixed); no horizontal scroll; the Dean sees the same banner and no technical hint. Open «Стан даних» and the banner on a computer and on a phone,
in both roles where possible, and check:

- no horizontal scroll;
- a banner link moves to the next line whole and never breaks inside itself;
- the hints read as quiet secondary text.

The look-check stand can be used for this. The result is recorded here when it is done.

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml` | banner split into parts; `BannerPart` function; attempt 2: `data-health-banner-lead` span | FR-001.1 … FR-001.5; AC-001, AC-002; R-1 |
| `src/ClassroomAgent.Web/Views/DataHealth/Index.cshtml` | hints; Dean's X4 title; `archived` parameter | FR-002 … FR-006, FR-009; AC-003 … AC-007 |
| `src/ClassroomAgent.Web/Security/DataHealthTextKeys.cs` | `HintAboveMaximum`, `HintItemsNotInClassroom`, `CourseListEmptyDean`, `Hint(kind)` | FR-008; API design §3 ("code refers to them through `DataHealthTextKeys`") |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `SharedResource.en.resx` | 8 entries each, texts verbatim from FR-008 | FR-008, VR-003, NFR-073 (supporting: translation entries) |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | `.data-health-banner-part`, `.data-health-hint`; attempt 2: `.data-health-banner-lead`, phone-width banner rule | FR-001.4, FR-009.1, SEC-004; R-1, R-2; AC-008 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/DataHealthHostExtensions.cs` | from TEST_WRITING, plus one fix at this stage, §7 D-1 | `ac_test_matrix` |
| `tests/ClassroomAgent.Tests/TestInfrastructure/DataHealthHintTexts.cs`, `Web/Pages/DataHealthHintsTests.cs`, `Web/Localization/DataHealthHintTranslationTests.cs`, `Web/Pages/DataHealthBannerTests.cs`, `Web/Pages/PageSearchHintTests.cs` | TEST_WRITING output, unchanged here | `ac_test_matrix` |
| `docs/tests/US-075-*.md`, `docs/evidence/US-075-*.md`, `docs/workflow/workflow-state.yaml`, `docs/workflow/history.jsonl` | workflow artifacts and state | Git Policy closed list |

No secret, generated database file or IDE-local config is in the change set. No migration: there is no entity change.

## 5. Validation Evidence

### 5.1 Attempt 2 (all delegated to `cheap-worker`; edits made inline with an anchored script)

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Classes touching the banner and the stylesheet | `dotnet test --filter FullyQualifiedName~<Class>` for `DataHealthBannerTests`, `BootstrapLayoutRuleTests`, `InlineCodeRuleTests`, `InstallationStylesheetLinkTests` | 33/33, 18/18, 29/29, 12/12 |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <the two changed files>` | exit 0; `dotnet format` checks C# only, so the `.cshtml`/`.css` edits are not really covered by it (no C# changed in attempt 2) |
| Full suite | `dotnet test ClassroomAgent.sln` | exit 0, **7838 / 7838 passed, 0 failed, 0 skipped**, 12 min 32 s, no Docker/WSL errors |

The total is one more than attempt 1's 7837 with no test file changed in attempt 2; the cause was not looked into
(test counts of data-driven theories can shift). Nothing failed or was skipped.

### 5.2 Attempt 1

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Story classes | `dotnet test --no-build --filter FullyQualifiedName~<Class>` for `DataHealthHintTranslationTests`, `DataHealthHintsTests`, `DataHealthBannerTests`, `DataHealthPageTests`, `PageSearchHintTests` | 6/6, 38/38, 33/33, 26/26, 69/69 |
| Probe before full suite | the same filter, `DataHealthHintTranslationTests` | 6/6 |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | exit 0, **7837 / 7837 passed, 0 failed, 0 skipped**, 12 min 22 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |

**The first full-suite attempt was stopped.** It hit the known Docker/WSL hang: Npgsql "operation has timed out" and
"reading from stream" errors, a silent log, and 2.2 GB free RAM. That is an environment failure, not a code verdict.
Docker Desktop and WSL were restarted (`wsl --shutdown`), a probe class passed, and the full suite was run again.

**Delegation:**

- `cheap-worker` ran the build and the five Story-class runs. A second `cheap-worker` started the first full run
  (the one stopped above).
- The repeated probe, full suite and format check ran as a background job in the main context. The subagent could
  not wait on its own background job, and its run had hung.
- The resx, CSS and view edits were made inline with a small anchored script. They were few, and each was decided
  with the view markup.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1, test helper fix (`DataHealthHostExtensions.Banner`).**
  - **Problem:** the TEST_WRITING helper read the banner's text by replacing each tag with a space. With the
    approved markup `<a>A</a>, <a>B</a>` (FR-001.2: the separator outside the links), that reads «A , B». A browser
    shows «A, B», and the existing tests pin «A, B». No approved markup could satisfy the helper.
  - **Fix:** the helper now removes tags without inserting spaces, so it reads what a browser shows. No assertion
    changed or weakened; every banner test asserts the same texts as before.
  - **Disclosure:** the fix is listed here for `IMPLEMENTATION_VERIFICATION`. It changes only how the text is
    read, and no artifact contradicts it, so it was not raised as a `BLOCKED` against TEST_WRITING.
- **D-2, banner format split.** The layout splits `DataHealth.Banner` ("{0}, {1}") at `{0}` and `{1}` so the
  separator stays translatable and outside the links. This relies on `{0}` coming before `{1}`, which is true in
  both files and pinned by `TheAdminsX4Title_AndTheBannerKeys_AreUnchanged`.
- **D-3, line endings.** The two resx files and `site.css` keep their CRLF endings; the diff shows only the added
  lines.

- **D-4, no automated test for the R-1/R-2 line breaking (attempt 2).** Where a browser breaks a line cannot be
  observed by the markup tests (TC: no browser automation in the suite). The markup change keeps every banner test
  green (text «A, B», links without the comma, the no-wrap class); the line breaking itself is checked by the
  Owner's repeated AC-008 manual check, §3.1.

## 8. Open Decisions

None touched or newly required.
