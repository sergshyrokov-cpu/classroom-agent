---
artifact_type: implementation_report
story: US-076
version: 2
status: DRAFT
created_at: 2026-11-07T10:48:33Z
updated_at: 2026-11-07T15:14:04Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
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
  - path: docs/evidence/US-076-test-generation-report.md
    version: 2
supersedes: docs/evidence/US-076-implementation-report.md@v1
attempt: 2
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-076 Implementation Report — a «?» beside the title opens short help on five pages

## 1. Summary

**Version 2 (attempt 2) — status: implemented, validated.** Specification v3 (I-8) and API/DB design v2 changed only
which existing tests may read the page outside the help; no production requirement changed, so the production code of
attempt 1 stands unchanged — re-checked against Specification v3 §4.1 texts (the 18 resx values are still asserted
verbatim by `PageHelpTests.TheTranslationFiles_HoldExactlyTheApprovedTexts`) and API design v2 §3. B-1 was closed by
TEST_WRITING v2 (the two whole-page test readers read outside the help; test generation report v2 §0). The full suite is
green: **7779/7779**, build 0/0, `dotnet format --verify-no-changes` clean (section 5). AC-007, the Owner's look walk on
computer and phone, was done on 2026-11-07 with result OK (section 3.1). No API, database, configuration or security change.

Attempt 1 text follows, kept for history.

**Attempt 1 status: BLOCKED.** The help is implemented as Specification v2 and API design v1 fix it, and every Story-level test
is green (`PageHelpTests` 20/20, `PageSearchHintTests` 63/63, `OnePageTitleRuleTests` 11/11,
`MeetCodeOrganizerNamePageTests` 48/48). But 12 cases of an **existing** journal test class,
`JournalMarksPageTests`, now fail, and AC-006 forbids editing existing page tests except for the page-search hint's
title-block position. The approved artifacts contradict each other (section 7, B-1); the implementation does not
choose between them.

The full suite did not complete: two attempts broke on the known Docker/WSL fault under low memory (Npgsql read
timeouts; section 5). So it is not yet known whether other existing tests are hit the same way.

No API, database, configuration or security change. OpenAPI and DB design were `NOT_APPLICABLE`.

## 2. Source Artifacts

As in the front matter. Story OD-001 resolved (b); no other Open Decision.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Views/Shared/_Layout.cshtml` (title block when `PageHelpTextKeys.PageOf` returns an id); `Views/Shared/_PageHelp.cshtml`; the five views set `ViewData[PageHelpTextKeys.ViewDataKey]` | `PageHelpTests.EachOfTheFivePages_…`, `InReadOnlyMode_…`, `WithNoRowsAtAll_…`, `TheJournalWithAValidationMessage_…` | green |
| AC-002 | `<details class="page-help">` right after `</h1>` inside `<div class="page-title">`; hint stays first after the block | `PageHelpTests` (via `AssertHelp`), `PageSearchHintTests`, `OnePageTitleRuleTests` | green |
| AC-003 | no `open` attribute, no script, no cookie | `PageHelpTests.EveryLoad_IsCollapsed_…` | green |
| AC-004 | 18 keys in `SharedResource.uk.resx` / `.en.resx`; key names in `Web/Security/PageHelpTextKeys.cs` | `PageHelpTests.TheTranslationFiles_…`, `NoSourceFile_HoldsAHelpText` | green |
| AC-005 | only a view-set id among the five renders the help (VR-001) | `PageHelpTests.NoOtherInstallationPage_…`, `TheForcedPasswordChange_…`, `NoControlPlanePage_…` | green |
| AC-006 | no change to the five pages' content, forms, links, policies | `PageHelpTests` header/write checks green; existing page tests of the five pages green with their text read outside the help (I-8; attempt 1: `JournalMarksPageTests` 12 red, B-1) | green |
| AC-007 | `wwwroot/css/site.css` (section «US-076») | manual walk by the Owner, 2026-11-07 (3.1) | done |
| FR-010 | test tightened by TEST_WRITING (`MeetCodeOrganizerNamePageTests`) | green | done |

### 3.1 Manual look check (AC-007)

**Done by the Owner on 2026-11-07 (walk ended 2026-11-07T15:14:04Z), result: OK.** Temporary synthetic look-check stand
(installation on `https://localhost:5443`, Testcontainers PostgreSQL, Dean sign-in; not in git, deleted after the walk),
Edge on the Owner's laptop. The five pages (journal, Meet codes, Meet reports, one meeting, report templates), the help
opened and closed on each, in four passes: computer Ukrainian, phone Ukrainian, phone English, computer English; phone =
Edge device mode (the Owner chose emulation over a real phone, so no LAN binding or firewall change); iPad Pro 13 was also
looked at. Control easy to hit, text readable, no sideways scroll, restrained look.

Two questions arose and were settled as not defects: (1) on the journal, the word «там» in line 3 looked to stick out
on the right — a ragged right edge of left-aligned text; the Owner confirmed it stays within the column (not right of the
«Показати» button); (2) switching the language clears a course picked but not yet submitted — the switcher returns to the
effective query (unchanged behaviour since the switcher's Story, not US-076). Stand limitation: the journal's course had
no coursework (the journal and Meet seed helpers collide on course and participant Google ids), so the help was judged
over an empty journal.

The earlier preliminary look by the implementer follows, kept for history. A preliminary look by the implementer, on a static copy of the markup with the real
`bootstrap.min.css` and `site.css` in headless Edge at 1100 px and 500 px, collapsed and open: the «?» sits on the
title line in a quiet ring, the opened lines fall under the title above the search hint, nothing overlaps the language
switcher, no sideways scroll. This is not the AC-007 walk.

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Security/PageHelpTextKeys.cs` | new | FR-004, FR-006, VR-001; api-design §3 constants class |
| `src/ClassroomAgent.Web/Views/Shared/_PageHelp.cshtml` | new | FR-002, FR-003, FR-005, VR-002 |
| `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml` | title block on the five pages | FR-004 |
| `src/ClassroomAgent.Web/Views/Journal/Index.cshtml` | sets page id | FR-001, FR-004 |
| `src/ClassroomAgent.Web/Views/MeetCodes/Index.cshtml` | sets page id | FR-001, FR-004 |
| `src/ClassroomAgent.Web/Views/MeetReports/Index.cshtml` | sets page id | FR-001, FR-004 |
| `src/ClassroomAgent.Web/Views/MeetReports/Meeting.cshtml` | sets page id | FR-001, FR-004 |
| `src/ClassroomAgent.Web/Views/ReportTemplates/Index.cshtml` | sets page id | FR-001, FR-004 |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` | 18 keys | FR-006, api-design §3 (translation entries) |
| `src/ClassroomAgent.Application/Localization/SharedResource.en.resx` | 18 keys | FR-006, api-design §3 (translation entries) |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | page-help styling | FR-007 |

Test files changed at TEST_WRITING (not here): `PageHelpTests.cs`, `PageHelpTestData.cs`, `PageSearchHintTestData.cs`,
`MeetCodeOrganizerNamePageTests.cs`; in TEST_WRITING v2 also `TestInfrastructure/PageResponse.cs` and
`TestInfrastructure/MeetCodesHostExtensions.cs` (readers outside the help, I-8). Attempt 2 changed no file of its own. No secret, database file or IDE config in the change set.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test … --filter FullyQualifiedName~PageHelpTests` | 0 | 20/20 |
| `dotnet test … --filter FullyQualifiedName~PageSearchHintTests` | 0 | 63/63 |
| `dotnet test … --filter FullyQualifiedName~OnePageTitleRuleTests` | 0 | 11/11 |
| `dotnet test … --filter FullyQualifiedName~MeetCodeOrganizerNamePageTests` | 0 | 48/48 |
| `dotnet test ClassroomAgent.sln` (attempt 1) | stopped | ~26 min (forecast 15): Npgsql "Timeout during reading attempt", free RAM 2.9 GB |
| `dotnet test ClassroomAgent.sln` (attempt 2, after Docker/WSL restart) | stopped | ~13 min: the same timeouts (free RAM 3 GB); among the fast failures, the 3 `JournalMarksPageTests` English cases |
| `dotnet test … --filter FullyQualifiedName~JournalMarksPageTests` | 2 | 12 failed (section 7, B-1) |
| **Attempt 2** (2026-11-07 11:41–11:54Z, free RAM 7.1 GB): `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` | 0 | 7779 total, 7779 passed, 0 failed, 0 skipped; 11 min 56 s; no environment failure |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes reported |
| `dotnet format --verify-no-changes` | — | not run (the stage stopped at B-1) |

Delegated: build and the four targeted classes (cheap-worker); full-suite attempt 1 (cheap-worker, stopped).
Attempt 2: build, full suite and format check delegated to a cheap-worker (no edits); the re-check against Specification v3
and API design v2 and this report inline.
Inline: the code (layout mechanism, CSS), the translation entries (a script copying the approved lines verbatim from
`PageHelpTestData` so no line is retyped), the headless look check, the `JournalMarksPageTests` diagnosis.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **B-1 (blocking). Existing journal test vs the approved help text.** `JournalMarksPageTests.AssertHeaderLine`
  (`tests/…/Web/Pages/JournalMarksPageTests.cs:157-183`) checks that the **whole page text** holds the course header
  line once and no stem of any other header line (`DataMarksWorld.HeaderStems`). Journal help line 2, approved at
  HUMAN_SPEC_APPROVAL (spec §4.1, api-design §3.1), names both header lines — «Курсу немає в Classroom з …» and «Дані
  не оновлювалися з …» / "Course not in Classroom since …" and "Data not updated since …" — so the stems are now on
  every journal page. Failing: `TheCourseHeaderLine_StandsAboveTheTable` 9 cases, `TheCourseHeaderLine_IsInEnglish_
  ForAnEnglishUser` 3 cases; message e.g. `Assert.DoesNotContain() Failure: Sub-string found … "Course not in Classroom
  since"`. The page itself is right: the header line still stands once above the table; only the test's scope (the
  whole page instead of the area outside the help) no longer fits.
  AC-006 says the existing tests of the five pages "stay green without edits other than the title-block position the
  page-search hint tests assert", and the test strategy/ac_test_matrix did not foresee this. The obvious fix — read
  the page text without the help's `<details>` in `AssertHeaderLine` — is an edit AC-006 does not allow, and changing
  the approved help text is not this stage's call. Other existing tests may be hit the same way (help lines also name
  «вибув(-ла)», «немає в Classroom», «Не в списку курсу», «Інші учасники», «Непризначені»); the full suite never
  completed, so the list is not known.
- Implementation choice: the layout opens `<div class="page-title">` only on the five pages, so the other pages render
  the title exactly as before (FR-004) and the layout keeps one `<h1>` (`OnePageTitleRuleTests`).

- Attempt 2: B-1 resolved upstream — the Owner chose option 1 (2026-11-07), Specification v3 added I-8, TEST_WRITING v2
  moved the whole-page test readers outside the help; finding closed FIXED by test-writer.

## 8. Open Decisions

None touched. Finding US-076/SPECIFICATION/m-2 (data-health page hint) stays OPEN — out of this Story's scope, moved to
v113 by the Owner.
