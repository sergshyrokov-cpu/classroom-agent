---
artifact_type: ac_test_matrix
story: US-073
version: 2
status: DRAFT
created_at: 2026-11-04T15:28:33Z
updated_at: 2026-11-04T20:40:43Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-073-show-password-only-for-typed-text.md
    version: null
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/tests/US-073-test-strategy.md
    version: 2
supersedes: docs/tests/US-073-ac-test-matrix.md@v1
---

# US-073 Acceptance Criteria to Test Matrix

Test class, unless stated otherwise: `ClassroomAgent.Tests.Architecture.PasswordToggleStateSourceTests`, source level, one case per host. Status is the state before the first IMPLEMENTATION; version 2 keeps these rows, and all of them are green on today's script. **Source tests prove the form of the script, not its behaviour in a browser: for AC-001 to AC-006 the behaviour is proved by the AC-008 walk.** Since version 2 the rows' rules also fix the whole text of each deciding function, so what a row names is really fixed (`US-073/IMPLEMENTATION_VERIFICATION/M-1`).

| Acceptance Criterion | Scenario | Test Level | Test Method | Expected Result | Status |
|---|---|---|---|---|---|
| AC-001 | Entry kinds are exactly the closed list, paste and composition included | Source | `TheEntryKinds_AreExactlyTheClosedList` | No problem reported | Red |
| AC-001 | A marked entry into an empty field is the one route to `ENTERED` | Source | `TheOnlyRouteToEntered_IsAMarkedEntry` | No problem reported | Red |
| AC-001 | The control is visible only in `ENTERED` | Source | `TheControl_IsVisibleOnlyInEntered` | No problem reported | Red |
| AC-001, AC-007 | The control is rendered `hidden`, so no keyboard reaches it before the script acts, and none without scripts | Integration, `PasswordToggleMarkupTests`, `ControlPlanePasswordToggleMarkupTests` (existing) | their field tests, through `PasswordFieldMarkup.AssertField` | Hidden in the markup | Green, unchanged |
| AC-002 | `input` gives `EMPTY` on an empty field, after the filled check | Source | `Input_ChecksInTheSpecifiedOrder` | No problem reported | Red |
| AC-002 | Leaving `ENTERED` hides the field before the control | Source | `TheControl_IsVisibleOnlyInEntered` | No problem reported | Red |
| AC-003 | Filling is told by `:autofill`; a throwing call answers filled | Source | `Filling_IsToldByAutofill_AndDoubtAnswersFilled` | No problem reported | Red |
| AC-003 | Text there at load or restored on "back" is never `ENTERED` | Source | `LoadAndReturn_WorkOutTheStateAnew` | No problem reported | Red |
| AC-003 | A press re-checks before anything is shown | Source | `ChangeAndPress_ReCheck_BeforeAnythingIsShown` | No problem reported | Red |
| AC-004 | No entry in `FILLED` reaches `ENTERED`: step 3 only in state `EMPTY` with the mark | Source | `Input_ChecksInTheSpecifiedOrder`, `TheOnlyRouteToEntered_IsAMarkedEntry` | No problem reported | Red |
| AC-004 | `beforeinput` marks entry only into an empty unfilled field | Source | `BeforeInput_FollowsTheRules` | No problem reported | Red |
| AC-005 | `input` and `change` go to `FILLED` on a fill; the field is hidden first | Source | `Input_ChecksInTheSpecifiedOrder`, `ChangeAndPress_ReCheck_BeforeAnythingIsShown`, `TheControl_IsVisibleOnlyInEntered` | No problem reported | Red |
| AC-006 | `historyUndo` and every other kind is not entry | Source | `TheEntryKinds_AreExactlyTheClosedList` | No problem reported | Red |
| AC-006 | Any other kind goes to `FILLED` at `beforeinput`, before the text changes | Source | `BeforeInput_FollowsTheRules` | No problem reported | Red |
| AC-007 | The two scripts are identical apart from line endings | Source | `TheTwoScripts_AreTheSame_ApartFromLineEndings` | Equal | Green by design: identical today |
| AC-007 | The value is compared in one helper | Source | `TheValue_IsComparedInOneHelper` | Exactly one | Red |
| AC-007 | The script sends and stores nothing, cancels no event, and uses the value only in that helper | Source, `PasswordToggleSourceTests` | `TheScript_DoesNothingElse`; `TheScriptScan_ReportsAForbiddenConstruct` (9 new cases); `TheScriptScan_AcceptsAnAllowedConstruct` (2 new cases) | Empty for the script; every defect reported | Green: must stay so |
| AC-007 | US-069's behaviour for a shown field: hidden on submit and on leaving, names, styles | Source, `PasswordToggleSourceTests` (existing) | `TheScript_HidesTheFields_OnSubmitAndOnPageHide`, `TheScript_TogglesTheFieldAndTheControlsState`, the stylesheet tests | As today | Green, must stay so |
| AC-001 to AC-007 | The rules can all pass, and each reports its defect | Source | `AConformingScript_PassesEveryRule`, `ADefect_IsReportedByItsRule` (63 cases, 22 of them keeping every word with the logic wrong) | Passes; each defect reported | Green by design: they test the rules, not the script |
| AC-008 | A person walks AC-001 to AC-006 with a really saved password in Edge and Chrome, both hosts, and types on a phone | Manual | The walk in the Specification, recorded in the implementation report | Each step as stated | Not run: IMPLEMENTATION |

Every Acceptance Criterion has a mapped scenario. AC-008 has no automated test by its own text: no automated test can reproduce a browser's filling.
