---
artifact_type: ac_test_matrix
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T12:23:40Z
updated_at: 2026-11-02T12:23:40Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/tests/US-069-test-strategy.md
    version: 1
supersedes: null
---

# US-069 Acceptance Criteria to Test Matrix

Status: **RED** fails until IMPLEMENTATION; **GUARD** green by design before and after; **EXISTING** an existing test kept
unchanged; **MANUAL** the Owner's walk, recorded in the implementation report.

Classes: `SmallTextsPageTests`, `PasswordToggleMarkupTests`, `PasswordValueNotExposedTests`, `PageSearchHintTests` in `Web.Pages`;
`ControlPlanePasswordToggleMarkupTests`, `ControlPlanePasswordValueNotExposedTests` in `ControlPlane.Controllers`;
`SmallTextsTranslationTests` in `Web.Localization`; `PasswordToggleTranslationTests` in `ControlPlane.Localization`;
`PasswordToggleSourceTests`, `OnePageTitleRuleTests` in `Architecture`.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Role line, Admin and Dean, uk and en | HTTP | SmallTextsPageTests | TheLandingPage_ShowsTheRoleInOneText (4) | «Роль: Декан» and the other three, exactly | RED |
| AC-001 | Role names keep their values | Source | SmallTextsTranslationTests | TheRoleNames_KeepTheirValues | `Landing.Role.*` unchanged | GUARD |
| AC-002 | One `<h1>` with the page's title, on the four views | HTTP | SmallTextsPageTests | EachPage_HasExactlyOneTitle_TheOwnOne (4) | one `<h1>`, its text the title key | RED |
| AC-002 | No view of the installation but the layout holds `<h1>` | Source | OnePageTitleRuleTests | NoViewButTheLayout_HoldsAnH1 (Web) | no offender | RED |
| AC-002 | The same on the Control Plane; each layout holds one; every non-partial view sets the title; the scan is not vacuous | Source | OnePageTitleRuleTests | NoViewButTheLayout_HoldsAnH1 (CP), TheLayout_HoldsOneH1, EveryNonPartialView_SetsTheTitle, TheScan_FindsAnH1 | as named | GUARD |
| AC-002 | Meet codes: the hint follows the only title | HTTP | PageSearchHintTests | seven cases on `meet-codes`, ordinal adapted from 2 to 1 | hint first after `<h1>` #1 | RED |
| AC-003 | Three headings directly above their `dl`, uk and en; heading and `dl` wrapped together in the counters | HTTP | SmallTextsPageTests | TheDatabasePage_NamesItsThreeBlocks (2) | structure matches | RED |
| AC-003 | Every counter and line stays in its block | HTTP | SmallTextsPageTests | EveryCounter_StaysInItsBlock | three `dl` with their fixed names | GUARD |
| AC-003 | Labels and values | HTTP | DatabaseStatisticsPageTests | existing | unchanged | EXISTING |
| AC-004 | First option is the prompt with empty value, uk and en, same text as the journal's key | HTTP | SmallTextsPageTests | TheCoursePicker_StartsWithThePrompt (2) | literal and key text equal | RED |
| AC-004 | With a chosen course the prompt is first and unselected, the course selected | HTTP | SmallTextsPageTests | WithAChosenCourse_ThePromptIsNotSelected_AndTheCourseIs | as named | RED |
| AC-004 | An empty course id behaves as no course | HTTP | SmallTextsPageTests | AnEmptyCourseId_ShowsNoReport_LikeNoCourse | same status, no report | GUARD |
| AC-004 | No new key for the prompt | Source | SmallTextsTranslationTests | ThePrompt_ReusesTheExistingKey | one key holds the text | GUARD |
| AC-005 | Each of the 9 fields: wrapper, input first, hidden `type="button"`, `aria-pressed`, "show" name, both names in `data-*`, no name, value, form or id, no visible word | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | EveryPasswordField_CarriesTheControl (4); TheSetupFields_CarryTheControl; TheSignInField_CarriesTheControl | as named | RED |
| AC-005 | The label keeps wrapping field and control (installation) | HTTP | PasswordToggleMarkupTests | TheLabel_KeepsWrappingFieldAndControl (4) | one label per field | RED |
| AC-005 | The script handles any number of fields and toggles type, `aria-pressed`, `aria-label` | Source | PasswordToggleSourceTests | TheScript_TogglesTheFieldAndTheControlsState (2) | source holds each | RED |
| AC-005 | Pressing, keyboard, text kept | Manual | spec step 1 | Dean sign-in, a Dean-accounts row | shows, hides, keeps | MANUAL |
| AC-006 | Hidden again on submit and on leaving | Source | PasswordToggleSourceTests | TheScript_HidesTheFields_OnSubmitAndOnPageHide (2) | page-wide `submit`, `pagehide` on window | RED |
| AC-006 | The checks are not vacuous | Source | PasswordToggleSourceTests | TheSubmitCheck_NeedsAPageWideListener (4), ThePageHideCheck_NeedsAWindowListener (3) | as named | GUARD |
| AC-006 | `spellcheck="false"`, `autocapitalize="off"` from the first render, on all 9 | HTTP | as AC-005 | AssertField inside the AC-005 tests | present in the markup | RED |
| AC-006 | A refused submit re-renders hidden and empty | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | AfterARefusedSignIn_TheFieldStartsHidden, AfterARefusedCreation_TheFieldStartsHidden; AfterARefusedSignIn_TheFieldStartsHidden, AfterARefusedSetup_TheFieldsStartHidden | control reset, no value | RED |
| AC-006 | The listeners themselves | Review | SECURITY_REVIEW reads the script (spec) | n/a | as FR-006.4 | MANUAL |
| AC-006 | "Back" after submit and after the address bar: the outcome only | Manual | spec steps 2, 3 | Dean sign-in, a Dean-accounts row | field hidden | MANUAL |
| AC-007 | No ready class in the markup; every control hidden | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | WithoutTheScript_NoFieldIsReady_AndEveryControlIsHidden (4; 2) | as named | RED |
| AC-007 | No rule defeats `hidden`; Edge's button hidden only on a ready field | Source | PasswordToggleSourceTests | NoRule_DefeatsTheHiddenAttribute (2), EdgesRevealButton_IsHiddenOnlyOnAReadyField (2) | as named | RED |
| AC-007 | Scripts off: no control, the field works | Manual | spec step 4 | Dean sign-in, a Dean-accounts row | as named | MANUAL |
| AC-008 | The control adds no field to any form | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | TheControl_AddsNoFieldToAnyForm (4), TheFormScan_ListsANamedButton; TheControl_AddsNoFieldToTheForm (2) | names as today | GUARD |
| AC-008 | The script does nothing else; the scan is not vacuous | Source | PasswordToggleSourceTests | TheScript_DoesNothingElse (2) | no forbidden construct | RED |
| AC-008 | The scan's own cases | Source | PasswordToggleSourceTests | TheScriptScan_ReportsAForbiddenConstruct (13), TheScriptScan_AcceptsAnAllowedConstruct (4) | as named | GUARD |
| AC-008 | Referenced once with `defer`, from the six views only, one partial per host | HTTP, Source | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests; PasswordToggleSourceTests | ThePage_ReferencesTheScriptOnce_Deferred (4; 2); OnlyTheViewsWithAPasswordField_ReferenceTheScript (2), OnePartial_HoldsTheControlsMarkup (2) | as named | RED |
| AC-008 | A page with no field does not load it | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | APageWithNoPasswordField_DoesNotLoadTheScript | no reference | GUARD |
| AC-008 | The script and icons are served | HTTP | PasswordToggleMarkupTests; ControlPlanePasswordToggleMarkupTests | AStaticFile_IsServedWithItsContentType (3; 3) | 200, type | RED |
| AC-008 | Icon chosen by state, room on the right, rules in the shared part of `site.css` | Source | PasswordToggleSourceTests | TheIcon_IsSelectedByTheControlsState (2), AReadyField_MakesRoomForTheControl (2), TheControlsRules_AreInTheSharedPart_OfBothStylesheets | as named | RED |
| AC-008 | CSP string and header tests | HTTP | SecurityHeadersResponseTests (both hosts) | existing | unchanged | EXISTING |
| AC-008 | No inline code; icons are plain same-host files; no `content` words or animation | Source | InlineCodeRuleTests, EmbeddedImageOverrideTests, BootstrapLayoutRuleTests | existing, they now scan the new files | unchanged | EXISTING |
| AC-009 | A known password in no response and no log line, host stopped first so logs are flushed | HTTP | PasswordValueNotExposedTests; ControlPlanePasswordValueNotExposedTests | AWrongDeanSignIn, ARefusedDeanCreation, AWrongCurrentPassword; AWrongOwnerSignIn, ARefusedSetup (`_DoesNotExposeThePassword`) | marker absent, logs not empty | GUARD |
| AC-009 | Sign-in, lockout and password rules | HTTP | DeanSignInPageTests, DeanPasswordPageTests and the Control Plane's | existing | unchanged | EXISTING |
| AC-010 | Role word, Дані, Синхронізація, both names, in uk and en under one key | Source | SmallTextsTranslationTests | EachText_IsOneKey_InBothLanguages (5 of 6) | key found | RED |
| AC-010 | Курси / Courses under one key | Source | SmallTextsTranslationTests | EachText_IsOneKey_InBothLanguages (uk: Курси) | key found; such a key exists today, the use is AC-003's | GUARD |
| AC-010 | The names on the Control Plane | Source | PasswordToggleTranslationTests | TheShowName_IsOneKey_InBothLanguages, TheHideName_IsOneKey_InBothLanguages | key found | RED |
| AC-010 | English names on installation pages | HTTP | PasswordToggleMarkupTests | InEnglish_TheControlCarriesTheEnglishNames (2) | English `aria-label`, `data-*` | RED |
| AC-010 | Same keys in both files; no view holds a new text as a literal | Source | SmallTextsTranslationTests; PasswordToggleTranslationTests | TheTwoFiles_HaveTheSameKeys; NoInstallationView_HoldsTheTextsAsALiteral; NoControlPlaneView_HoldsTheNamesAsALiteral | as named | GUARD |
| AC-011 | Look in Edge, desktop and about 375 px, one control per field, no CSP violation | Manual | n/a | Owner, recorded in the implementation report, confirmed at HUMAN_PR_APPROVAL | as the AC says | MANUAL |
