---
artifact_type: ac_test_matrix
story: US-076
version: 2
status: DRAFT
created_at: 2026-11-07T07:33:06Z
updated_at: 2026-11-07T11:26:06Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/designs/database/US-076-db-design.md
    version: 2
  - path: docs/tests/US-076-test-strategy.md
    version: 2
supersedes: docs/tests/US-076-ac-test-matrix.md@v1
---

# US-076 Acceptance Criteria to Test Matrix

Classes (all under `tests/ClassroomAgent.Tests/`):

- **Help** — `Web/Pages/PageHelpTests` (new)
- **Hint** — `Web/Pages/PageSearchHintTests` (existing; helper `TestInfrastructure/PageSearchHintTestData.AfterTitle` adjusted)
- **Title** — `Architecture/OnePageTitleRuleTests` (existing, unchanged)
- **Org** — `Web/Pages/MeetCodeOrganizerNamePageTests` (existing; one test tightened)
- **Outside** — whole-page text readers `TestInfrastructure/PageResponse.Text` and `MeetCodesHostExtensions.VisibleText`,
  now reading the page outside the help (spec I-8, strategy §3a); exercised by every existing page test of the five pages

Status: **red** = fails until implementation; **green** = passes before implementation (regression guard, reason given).

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | five pages, Admin/Dean × uk/en: one `<details>`, not open, summary first, «?», accessible name | Integration | Help | `EachOfTheFivePages_ShowsItsHelp_Collapsed_BesideTheTitle` (4 cases) | help as §3 of the strategy | red |
| AC-001 | read-only mode, three causes, same help | Integration | Help | `InReadOnlyMode_TheHelpIsTheSame` (3 cases) | same help | red |
| AC-001 | empty states (no course, no rows, no codes, fresh template list) | Integration | Help | `WithNoRowsAtAll_TheHelpIsShown` (4 cases) | help shown | red |
| AC-001 | journal with validation message 400/404 | Integration | Help | `TheJournalWithAValidationMessage_StillShowsTheHelp` (2 cases) | help shown | red |
| AC-002 | native disclosure, text inside it, directly beside/under title, before the search hint | Integration | Help | `EachOfTheFivePages_…` (via `AssertHelp`) | `<details>` after `</h1>`, nothing between; hint first after it | red |
| AC-002 | page-search hint unchanged and first after the title block | Integration | Hint | all existing placement tests (`EveryTeachingDataPage_ShowsItsHintOnce_DirectlyUnderTheTitle` etc.) | stay green after implementation | green (existing behaviour; must stay green) |
| AC-002 | one `<h1>`, only in the layout | Source | Title | `NoViewButTheLayout_HoldsAnH1`, `TheLayout_HoldsOneH1` | stay green after implementation | green (binds implementation) |
| AC-003 | collapsed on every load, twice and after a language change; no new cookie; no new script | Integration | Help | `EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged` | never `open`; cookies ⊆ session/antiforgery/TempData; scripts ⊆ existing two | red |
| AC-004 | each page's exact lines, in order, in the user's language | Integration | Help | `EachOfTheFivePages_…`, `InReadOnlyMode_…`, `EveryLoad_…` | paragraphs = approved lines | red |
| AC-004 | 18 keys with exact values in uk and en; no other `PageHelp.` key | Integration | Help | `TheTranslationFiles_HoldExactlyTheApprovedTexts` | equal | red |
| AC-004 | no help text in any `.cshtml`/`.cs`/`.css`/`.js` | Source | Help | `NoSourceFile_HoldsAHelpText` | resx hold all fragments; no source file holds one | red (resx part) |
| AC-005 | no help on every other installation page, both roles, errors, sign-in; control: journal has it | Integration | Help | `NoOtherInstallationPage_HasTheHelp` | none, control has one | red (control) |
| AC-005 | forced password change | Integration | Help | `TheForcedPasswordChange_HasNoHelp` | none | green (nothing renders help yet; non-vacuous through the shared reader proven by `TheReader_…` and the control above) |
| AC-005 | Control Plane pages | Integration | Help | `NoControlPlanePage_HasTheHelp` | none | green (same reason) |
| AC-006 | no link/form/script/style/handler in the help; no content inside it beyond its lines | Integration | Help | `EachOfTheFivePages_…` (via `AssertHelp`) | empty leftover, no forbidden markup | red |
| AC-006 | security headers exactly SC-15; no write, no audit | Integration | Help | `EveryLoad_IsCollapsed_…` | `Problems` empty; fingerprint and audit count unchanged | red (whole test; the header part alone holds today) |
| AC-006 | five pages' content, forms, links, policies, read-only behaviour, exports unchanged | Integration | existing page tests of the five pages (Outside) | (existing suites: `JournalPageTests`, `JournalMarksPageTests`, `MeetCodesPageTests`, `MeetReportsPageTests`, `MeetReportMeetingPageTests`, `MembershipStartMeetTests`, `ReportTemplatePagesTests`, …) | stay green; their text assertions read the page outside the help (I-8), assertions unchanged | green (regression) |
| AC-006 | the outside-help reader removes the help and only the help | Unit | Help | `TheReader_TellsAProperHelpFromABrokenOne` | disclosure gone, rest equal, no-op without help | green (tests the test reader) |
| AC-007 | a person checks the look on computer and phone, both languages | Manual | — | walk recorded in the implementation report §3.1 | control easy to tap, readable, no sideways scroll, restrained | manual |
| — | the markup reader is not vacuous | Unit | Help | `TheReader_TellsAProperHelpFromABrokenOne` | proper/open/overfull/linked/misplaced told apart | green (tests the test reader) |
| — (supporting, FR-010) | organizer cell markup: two `</span>, <span` separators, no white space before a comma | Integration | Org | `SeveralOrganizers_AreOrdered_AndEachShowsOnce` (all lists) | equal / no match | green (US-074 behaviour already right; tightening only) |

Every Acceptance Criterion AC-001 … AC-006 maps to at least one automated test; AC-007 is the manual walk the Story itself
prescribes.
