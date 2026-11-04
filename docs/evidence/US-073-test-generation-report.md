---
artifact_type: test_generation_report
story: US-073
version: 2
status: DRAFT
created_at: 2026-11-04T15:28:33Z
updated_at: 2026-11-04T20:40:43Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/designs/api/US-073-api-design.md
    version: 1
  - path: docs/designs/database/US-073-db-design.md
    version: 1
  - path: docs/tests/US-073-test-strategy.md
    version: 2
  - path: docs/tests/US-073-ac-test-matrix.md
    version: 2
  - path: docs/reviews/implementation/US-073-implementation-verification.md
    version: 1
supersedes: docs/evidence/US-073-test-generation-report.md@v1
---

# US-073 Test Generation Report

## Attempt 2 (loop-back `test_gap`, finding `US-073/IMPLEMENTATION_VERIFICATION/M-1`)

**Result: the gap is closed in the tests, and no production file changed in this attempt.** The rules looked for words and their order, so 12 of 13 wrong scripts passed. They now also fix the whole text, white space aside, of every function that decides something, plus the declarations and the `pageshow` condition (test strategy, "Source form the tests read").

| Change | File | What |
|---|---|---|
| Changed | `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordToggleScriptRules.cs` | Fixed texts added to seven of the rules, through `BodyProblems` and `StatementProblems`. No existing check removed or weakened |
| Changed | `tests/ClassroomAgent.Tests/Architecture/PasswordToggleStateSourceTests.cs` | 22 new rows in `ADefect_IsReportedByItsRule`, defects A to L among them. `ConformingScript` takes the real script's code, so six existing rows changed their anchor text (`inputKind` to `kind`, one early-return anchor); each still applies the same defect |

**Red, then green.** Run by `cheap-worker` subagents on 2026-11-04, from the repository root:

| Step | Command | Result |
|---|---|---|
| New rows, old rules | `dotnet build ClassroomAgent.sln` | Succeeded, 0 warnings, 0 errors |
| | `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PasswordToggleStateSourceTests` | 83 total, 63 passed, 20 failed, 0 skipped, exit 2. All 20 are `ADefect_IsReportedByItsRule`, `Assert.NotEmpty` on an empty list: the old rules did not report the defect |
| New rows, new rules | `dotnet build ClassroomAgent.sln` | Succeeded, 0 warnings, 0 errors |
| | `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PasswordToggle` | 184 total, 184 passed, 0 failed, 0 skipped, 24 s, exit 0 |

The red list holds all of A to L. Two of the 22 new rows passed under the old rules as well, so they prove nothing new about them and stay as guards: `FILLED = "ENTERED"` (the rule counting the declared `ENTERED`) and step 4 with `!== EMPTY` (the rule's regex). The real scripts passed the new rules at the first run, with no change, so the manual walk is not made stale. `git status --short src/` lists only the two scripts, already changed by IMPLEMENTATION.

**The full suite was not run.** Only these two test files changed, and they are used by no other class. The full run is `security-reviewer`'s.

**What this does not do.** It is a change guard: any edit to a fixed text now fails a test, however harmless, and whoever changes it must change the constant and walk AC-008 again. The rules still do not prove that the fixed text is right; the walk and SECURITY_REVIEW do.

**For IMPLEMENTATION.** No script change is needed. `US-073/IMPLEMENTATION_VERIFICATION/m-1` belongs to its report: section 1 should carry the walk's limits. The report should also list this attempt's test changes.

**Delegation.** The build and test runs went to `cheap-worker`. The rules and defects stayed with this Skill's model.

The rest of this report is attempt 1, as it was.

**Result: red phase verified.** 18 cases red for the missing behaviour (9 methods, 2 hosts), no unexpected failure in the tests run. These tests read the script's source and do not run it: browser behaviour stays unproved until the AC-008 walk.

## Files

| Change | File | What |
|---|---|---|
| New | `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordToggleScriptRules.cs` | The rules read from the source |
| New | `tests/ClassroomAgent.Tests/Architecture/PasswordToggleStateSourceTests.cs` | 61 cases: 18 on the real scripts red, 1 on the two scripts' identity, 1 conforming source, 41 defects |
| Changed | `tests/ClassroomAgent.Tests/Architecture/PasswordToggleSourceTests.cs` | 11 new scan cases (9 reported, 2 accepted). The check that the control is revealed at load is removed: it fixed the old rule (AC-007) and `TheControl_IsVisibleOnlyInEntered` replaces it |
| Changed | `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordFieldMarkup.cs` | `ScriptProblems` accepts one helper comparing the value with the empty string (FR-008.3) and now also reports `preventDefault` and `['value']`; new `ValueHelperCount` |

**Compile-only skeleton:** none. The tests read a JavaScript file as text, so they reference no new declaration. No production file changed.

## Commands and execution

Run by a `cheap-worker` subagent from the repository root, 2026-11-04:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | Succeeded, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PasswordToggle` | 162 total, 144 passed, 18 failed, 0 skipped, 28 s |

**The full suite was not run.** No production code changed, and the only shared code changed is `ScriptProblems`, used by `PasswordToggleSourceTests` alone. `SmallTextsPageTests` also uses `PasswordFieldMarkup`, only its markup helpers, and was not run.

## Expected failures (red)

Each in both hosts, each failing with messages that name the missing rule:

| Test | Failure on today's script |
|---|---|
| `TheEntryKinds_AreExactlyTheClosedList` | No entry-kinds array, `insertLineBreak` not named, no deletion prefix, `inputType` not read |
| `Filling_IsToldByAutofill_AndDoubtAnswersFilled` | No function `isFilled` |
| `TheOnlyRouteToEntered_IsAMarkedEntry` | `ENTERED` not declared, no route, no state write, no mark |
| `TheControl_IsVisibleOnlyInEntered` | The control is revealed by `removeAttribute("hidden")`, which is the old rule; no function `setState` |
| `BeforeInput_FollowsTheRules` | No listener, no function |
| `Input_ChecksInTheSpecifiedOrder` | No listener, no function |
| `ChangeAndPress_ReCheck_BeforeAnythingIsShown` | No `change` listener, no `onPress`, `onChange` missing |
| `LoadAndReturn_WorkOutTheStateAnew` | No `settle`, no `pageshow` listener |
| `TheValue_IsComparedInOneHelper` | Expected 1, found 0 |

## Green before implementation, and why

| Test | Why it is not vacuous |
|---|---|
| `TheTwoScripts_AreTheSame_ApartFromLineEndings` | The files are identical today; a guard for FR-010 during IMPLEMENTATION |
| `AConformingScript_PassesEveryRule` | Tests the rules against a sample source, so a rule that can never pass would show here |
| `ADefect_IsReportedByItsRule` (41) | Applies one defect at a time to that sample and asserts the defect text was really present |
| New cases of `TheScriptScan_*` (11) | Test the scan on fragments, not the script |

The existing classes `PasswordToggleSourceTests`, `PasswordToggleMarkupTests` and `ControlPlanePasswordToggleMarkupTests` had no failure.

## Unexpected failures

None.

## Untested Acceptance Criteria

No Acceptance Criterion lacks a mapped scenario. **AC-008 is manual and not yet run.** For AC-003, AC-005 and AC-006 the source tests fix only the form of the script; the proof that a fill or an undo really behaves so is that walk, and FR-011 requires trying its assumptions in real Edge and Chrome first.

## For IMPLEMENTATION

- The names and shapes the tests read are in the test strategy, "Source form the tests read". The sample in the test class is test data and was never run in a browser.
- `TheScript_DoesNothingElse` and `TheScript_HidesTheFields_OnSubmitAndOnPageHide` are green now and must stay so.

## Delegation

The build and the test run went to `cheap-worker`. The tests themselves stayed with this Skill's model: each rule is a judgement on the Specification, not bulk code against a fixed pattern.

## Open Decisions

None.
