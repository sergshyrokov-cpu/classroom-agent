---
artifact_type: test_generation_report
story: US-076
version: 2
status: DRAFT
created_at: 2026-11-07T07:36:15Z
updated_at: 2026-11-07T11:36:33Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/designs/database/US-076-db-design.md
    version: 2
  - path: docs/tests/US-076-test-strategy.md
    version: 2
  - path: docs/tests/US-076-ac-test-matrix.md
    version: 2
supersedes: docs/evidence/US-076-test-generation-report.md@v1
---

# US-076 Test Generation Report

## 0. Version 2 (spec v3, I-8; finding US-076/IMPLEMENTATION/B-1)

Second pass of TEST_WRITING after the loop-back from IMPLEMENTATION. The production code of US-076 already exists
(implementation report v1), so this pass could find the affected existing tests **empirically**, and the red phase of
version 1 (section 5 below, run 07:32–07:36Z before any production code) stays the red-phase evidence of the new tests.

### 0.1 Finding the affected tests

With the US-076 implementation in place and the test tree of version 1, every `Web.*` namespace was run (delegated to a
`cheap-worker`, no edits), 2026-11-07 11:08–11:25Z:

| Filter | Total | Passed | Failed |
|---|---|---|---|
| `ClassroomAgent.Tests.Web.Pages` | 1376 | 1361 | 15 |
| `ClassroomAgent.Tests.Web.Security` | 534 | 534 | 0 |
| `ClassroomAgent.Tests.Web.Localization` | 546 | 546 | 0 |
| `ClassroomAgent.Tests.Web.UseCases` | 449 | 449 | 0 |
| `ClassroomAgent.Tests.Web.Logging` | 123 | 123 | 0 |

No environment failure. The 15: `JournalMarksPageTests.TheCourseHeaderLine_StandsAboveTheTable` (9 cases) and
`…_IsInEnglish_ForAnEnglishUser` (3) — B-1, via `PageResponse.Text`; `MembershipStartMeetTests.AMeetingBeforeTheFirstRun_
MarksTheFirstLoadsTeacherAndStudent` («не в списку курсу» found) and `…AMeetingBeforeALaterArrivalWasSeen_DoesNotCountThemAsACourseStudent`
(count 2 instead of 1), `MeetReportMeetingPageTests.TheOtherParticipants_AreOneLine_WithoutAnyAddress` (count 2 instead
of 1) — via `MeetCodesHostExtensions.VisibleText`. Every one is a whole-page text reader meeting a label the help names;
the page itself is right.

A failing count or absence check is only half of the risk: a **presence** check of a label over the whole page would
pass vacuously because of the help. So the fix is made in the two whole-page readers, not in the 15 tests (strategy
§3a). Other whole-page reader patterns were searched (raw `Body` assertions on the labels the help quotes; other
decoded-body helpers such as `SmallTextsPageTests`' — statistics page, not one of the five): none reads a help label of
the five pages from the whole page.

### 0.2 Files changed in this pass

- `tests/ClassroomAgent.Tests/TestInfrastructure/PageHelpTestData.cs` — new `WithoutHelp(html)`: removes every
  `<details>` element (the help is the only one, FR-002).
- `tests/ClassroomAgent.Tests/TestInfrastructure/PageResponse.cs` — `Text` reads `WithoutHelp(Body)` (spec I-8).
- `tests/ClassroomAgent.Tests/TestInfrastructure/MeetCodesHostExtensions.cs` — `VisibleText` reads `WithoutHelp(html)`.
- `tests/ClassroomAgent.Tests/Web/Pages/PageHelpTests.cs` — `TheReader_TellsAProperHelpFromABrokenOne` also proves
  `WithoutHelp` removes the disclosure, keeps the rest exactly, and is a no-op without help.

No assertion of any existing test was edited or weakened; the 15 tests are unchanged. The help tests read `Body`, so they
are not blinded by the change. No production code touched in this pass.

### 0.3 Commands and results (2026-11-07 11:25–11:36Z, delegated to a `cheap-worker`)

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` (first attempt) | 5 errors in `PageHelpTests.cs` — an unescaped line break in the new literal (my edit); fixed, rebuilt |
| `dotnet build ClassroomAgent.sln` | 0 errors, 0 warnings |
| `dotnet test … --filter FullyQualifiedName~ClassroomAgent.Tests.Web.Pages` | 1376/1376 passed (4 min 38 s) |
| `dotnet test … --filter FullyQualifiedName~ClassroomAgent.Tests.Web.Localization` | 546/546 passed |
| `dotnet test … --filter FullyQualifiedName~ClassroomAgent.Tests.Web.Security` | 534/534 passed |
| `dotnet test … --filter FullyQualifiedName~ClassroomAgent.Tests.Web.UseCases` | 449/449 passed |

`Web.Logging` (123/123 before the fix) reads no page text and was not re-run. The full solution suite is left to
IMPLEMENTATION (Definition of Done item 2); the earlier full-suite attempts broke on the Docker/WSL fault, not on tests.

### 0.4 Red/green classification of this pass

The 15 failures above were red **because of the test readers' scope**, not missing behaviour; after the reader fix they
are green with unchanged assertions — the intended outcome of I-8. The new `PageHelpTests` are green against the existing
implementation; their red phase is version 1's (section 5). Unexpected failures: none.

### 0.5 Findings

- `US-076/IMPLEMENTATION/B-1` — closed FIXED here (readers outside the help, strategy §3a).
- `US-076/SPECIFICATION/m-2` — left OPEN as the Owner decided (v113); no test added.

---

Sections 1–8 below are version 1 (2026-11-07 07:36Z), kept as the red-phase evidence.

## 1. Files

Created:

- `tests/ClassroomAgent.Tests/TestInfrastructure/PageHelpTestData.cs` — page ids, the 18 keys, the 34 help lines and 2
  control names verbatim (spec v2 §4.1; journal line 2 per api-design §3.1), the markup reader.
- `tests/ClassroomAgent.Tests/Web/Pages/PageHelpTests.cs` — 11 test methods, 20 test cases.

Modified:

- `tests/ClassroomAgent.Tests/TestInfrastructure/PageSearchHintTestData.cs` — `AfterTitle` skips a `<details>` directly
  after `</h1>` and the closing tags of a title wrapper (spec AC-006 allows exactly this edit); new regex `TitleBlockEnd`.
- `tests/ClassroomAgent.Tests/Web/Pages/MeetCodeOrganizerNamePageTests.cs` — FR-010: `SeveralOrganizers_AreOrdered_AndEachShowsOnce`
  also asserts two `</span>, <span` separators in the raw organizer cell and no white space before a comma; existing
  assertions kept.

Also changed in this stage (bookkeeping, by the orchestrator before routing): `docs/specifications/US-076-spec.md` front
matter `status: DRAFT → APPROVED` (approval recorded in `history.jsonl` 2026-11-07T07:01:27Z; the status flip had been
missed).

## 2. Compile-only skeleton

None. The tests read rendered markup, translation files and the source tree; they reference no new production
declaration (the key constants class and view-data key are implementation choices, api-design §3, so the tests use the
key strings api-design fixes).

## 3. Delegation

Test design, markup contract, test data and test code: written inline (the reader and placement rules are judgement).
Build and the four test runs: delegated to a `cheap-worker` subagent (no edits).

## 4. Commands and results (2026-11-07, 07:32–07:36Z)

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 errors, 0 warnings |
| `dotnet test --filter FullyQualifiedName~ClassroomAgent.Tests.Web.Pages.PageHelpTests` | 20 total, 3 passed, 17 failed, 0 skipped |
| `dotnet test --filter FullyQualifiedName~PageSearchHintTests` | 63/63 passed |
| `dotnet test --filter FullyQualifiedName~MeetCodeOrganizerNamePageTests` | 48/48 passed |
| `dotnet test --filter FullyQualifiedName~OnePageTitleRuleTests` | 11/11 passed |

No environment failure (Docker up, containers started, no Npgsql timeouts).

## 5. Red-phase classification

Expected failures (production behaviour missing) — 17:

- 14 page cases fail in `AssertHelp` with "0 <details> elements instead of exactly one help control": 
  `EachOfTheFivePages_ShowsItsHelp_Collapsed_BesideTheTitle` (4), `InReadOnlyMode_TheHelpIsTheSame` (3),
  `WithNoRowsAtAll_TheHelpIsShown` (4), `TheJournalWithAValidationMessage_StillShowsTheHelp` (2),
  `EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged` (1). The pages render, so host, sign-in,
  seeding and paths are valid; only the help is absent.
- `NoOtherInstallationPage_HasTheHelp` — every other page asserted without help (passes), then the control
  `Assert.Single(Disclosures(journal))` fails: no help on the journal yet. The other-page part therefore ran green on 21
  admin/Dean pages, 4 error pages and sign-in with 200/404 statuses as asserted.
- `TheTranslationFiles_HoldExactlyTheApprovedTexts` — "Translation key 'PageHelp.Journal.Line1' is missing for 'uk'".
- `NoSourceFile_HoldsAHelpText` — the non-vacuity part fails: 36 of 36 fragments (34 lines + 2 names) are not in the
  resx files yet.

Passing before implementation — 3, each explained:

- `TheForcedPasswordChange_HasNoHelp`, `NoControlPlanePage_HasTheHelp` — "no help here" holds trivially while no page
  has help; they become meaningful once the five pages have it. Their reader `HasAnyHelp` is proven by
  `TheReader_TellsAProperHelpFromABrokenOne` and the same reader's control in `NoOtherInstallationPage_HasTheHelp`.
- `TheReader_TellsAProperHelpFromABrokenOne` — tests the test reader itself on fixed markup.

Existing regression tests: `PageSearchHintTests` 63/63 with the adjusted `AfterTitle` (no `<details>` yet, so the new
branch is not exercised; `TheReader_…` exercises it on fixed markup); `MeetCodeOrganizerNamePageTests` 48/48 including
the tightened test (US-074 markup is already right — the change is a guard); `OnePageTitleRuleTests` 11/11 (binds
implementation to one `<h1>` in the layout).

Unexpected failures: none.

## 6. Untested Acceptance Criteria

AC-007 (the look on computer and phone) is the manual walk the Story prescribes, recorded in the implementation report.
Mouse/touch/keyboard pressing of the native disclosure is part of that walk (test strategy §6).

## 7. Open Decisions and findings

No Open Decision. Finding `US-076/SPECIFICATION/m-2` (data-health page search hint) left OPEN as the Owner decided
2026-11-07 (v113; closes DEFERRED at `HUMAN_PR_APPROVAL`); no test added for it.

## 8. Overall result

PASS — every AC-001…AC-006 is mapped to automated tests that compile and fail only for missing behaviour; AC-007 is
manual; regression suites green.
