---
artifact_type: implementation_report
story: US-073
version: 2
status: DRAFT
created_at: 2026-11-04T19:08:28Z
updated_at: 2026-11-04T21:25:15Z
produced_by: dotnet-implementor
attempt: 1
inputs:
  - path: docs/stories/US-073-show-password-only-for-typed-text.md
    version: null
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
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
supersedes: docs/evidence/US-073-implementation-report.md@v1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-073 Implementation Report

## 1. Summary

The eye control now appears only for text the person entered into an empty field. A value the browser filled in has no control, in Edge and in Chrome, and no walked route revealed it. Only the script of each host changed.

- **Version 2:** the loop-back from verification changed tests only (section 4). No script changed since the walk, so the walk stands.
- **Validation:** build clean, format check clean. The final full run passed 7665 of 7665, after two runs that each failed one test, a different one each time, neither related to this Story (section 7, D-5). The source tests read the script and do not run it.
- **Browser proof:** the Owner walked the behaviour by hand with really saved passwords (section 3.1). The assumptions that could reveal a filled value held **wherever they were walked**, in Edge and Chrome, so no return to the Specification is needed. On the Control Plane several routes were not walked (D-1), and whether that is enough is the Owner's decision.
- **Limitations, disclosed in section 7:** the walk on the Control Plane was shorter than the Specification's text; one missing control in the walk did not repeat and is unexplained; the phone step was Chrome on an iPhone only.

## 2. Source Artifacts

Story `docs/stories/US-073-show-password-only-for-typed-text.md`; Specification `docs/specifications/US-073-spec.md` v4; Open Decisions `docs/decisions/US-073-open-decisions.md` v1 (none open); API and database designs v1 (both NOT_APPLICABLE); `docs/tests/US-073-test-strategy.md` v2; `docs/tests/US-073-ac-test-matrix.md` v2; verification `docs/reviews/implementation/US-073-implementation-verification.md` v1.

## 3. Implemented Acceptance Criteria

Location for all: `wwwroot/js/password-toggle.js` of both hosts. Test class, unless stated: `PasswordToggleStateSourceTests`.

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 | `onBeforeInput`, `onInput` step 3, `setState`, `ENTRY_KINDS` | `TheEntryKinds_AreExactlyTheClosedList`, `TheOnlyRouteToEntered_IsAMarkedEntry`, `TheControl_IsVisibleOnlyInEntered`; markup tests; walk | Pass |
| AC-002 | `onInput` step 2, `setState` | `Input_ChecksInTheSpecifiedOrder`, `TheControl_IsVisibleOnlyInEntered`; walk | Pass |
| AC-003 | `settle`, `isFilled`, `onInput` step 1, `onPress` | `Filling_IsToldByAutofill_AndDoubtAnswersFilled`, `LoadAndReturn_WorkOutTheStateAnew`, `ChangeAndPress_ReCheck_BeforeAnythingIsShown`; walk | Pass |
| AC-004 | `onBeforeInput`, `onInput` step 5 | `Input_ChecksInTheSpecifiedOrder`, `BeforeInput_FollowsTheRules`; walk | Pass, one unexplained miss (section 7, D-2) |
| AC-005 | `onInput` step 1, `onChange`, `setState` | `Input_ChecksInTheSpecifiedOrder`, `ChangeAndPress_ReCheck_BeforeAnythingIsShown`; walk | Pass |
| AC-006 | `onBeforeInput` "any other kind" | `TheEntryKinds_AreExactlyTheClosedList`, `BeforeInput_FollowsTheRules`; walk | Pass |
| AC-007 | Two identical files; `isEmpty` the only use of the value | `TheTwoScripts_AreTheSame_ApartFromLineEndings`, `TheValue_IsComparedInOneHelper`; `PasswordToggleSourceTests.TheScript_DoesNothingElse`; markup tests | Pass |
| AC-008 | The walk below | Manual | Walked, with the gaps in section 7 |

### 3.1 The AC-008 walk

Walked by the Owner on 2026-11-04, Edge and Chrome 156 on the development laptop, clean browser profiles, synthetic data, saved passwords really saved through each browser's own prompt.

**Setup found during the walk:** with an untrusted development certificate Edge did not fill the saved password at all, so the first attempt proved nothing. The certificate was trusted for the walk with the Owner's confirmation and the trust removed afterwards. Whether the certificate was the cause is inferred from the fill starting once it was trusted.

| Spec walk step | Edge, installation | Chrome, installation | Edge, Control Plane | Chrome, Control Plane |
|---|---|---|---|---|
| 1 Fill at load, no control | Pass | Pass | Pass | Pass |
| 1 Tab reaches nothing | Pass | Pass | Not walked | Not walked |
| 2 Add, delete, overtype: no control | Pass | Pass | Add and delete only | Add only |
| 3 Empty and type; show, hide; empty while shown; masked on retyping | Pass | Pass | Type, show, hide; see D-2 | Type, show, hide |
| 4 Paste into an empty field | Pass | Pass | Not walked | Not walked |
| 5 Fill over shown text | Pass, from the login field's list | Pass, from the login field's list | Pass, from the login field's list | Pass, from the login field's list |
| 5 Fill over typed hidden text | Pass, from the login field's list | Pass, from the login field's list | Not walked | Not walked |
| 6 Undo after emptying | Pass | Pass | Not walked alone | Not walked alone |
| 6 Type, undo until the value returns | Pass | Pass | Pass (one press) | Pass |
| 7 Enter with an empty name | Form goes to the server, see D-3 | Form goes to the server, see D-3 | Form goes to the server, page returns | Form goes to the server, page returns |
| 8 Edit as the first action: add, delete | Pass | Pass | Add and delete (experiment A, after reload and after sign-out) | Add only |

**Filling from a list.** The list is not offered in a password field that holds typed text, in either browser. Under walk step 5 that is recorded and not a failure. A fill over typed text was still reached, from the login field's list, and it returned the field to hidden with no control.

**Assumptions of FR-011:** A-1, A-2, A-3 and A-5 held wherever walked: no walked route revealed a filled value. A-4 held for the one keyboard walked.

**Phone:** Chrome on an iPhone, installation sign-in page over the local network, on-screen keyboard: the control appears on typing, shows and hides, and goes when the text is removed. Android was not walked. The stand's temporary firewall rule was added and removed by the Owner, and its absence was checked.

**Cleanup checked:** stand file deleted, browser profiles deleted, certificate trust removed (`dotnet dev-certs https --check --trust` reports not trusted), firewall rule absent.

## 4. Change Set

| Change | File | Trace |
|---|---|---|
| Modified | `src/ClassroomAgent.Web/wwwroot/js/password-toggle.js` | FR-002 to FR-009 |
| Modified | `src/ClassroomAgent.ControlPlane/wwwroot/js/password-toggle.js` | FR-010, identical copy |
| Modified | `tests/ClassroomAgent.Tests/Architecture/PasswordToggleSourceTests.cs` | `ac_test_matrix`, TEST_WRITING |
| Modified | `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordFieldMarkup.cs` | FR-008.3, TEST_WRITING |
| New | `tests/ClassroomAgent.Tests/Architecture/PasswordToggleStateSourceTests.cs` | `ac_test_matrix`, TEST_WRITING |
| New | `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordToggleScriptRules.cs` | `ac_test_matrix`, TEST_WRITING |

**TEST_WRITING attempt 2** changed `PasswordToggleScriptRules.cs` (the rules now fix the whole text of each deciding function) and `PasswordToggleStateSourceTests.cs` (22 new defect rows; the sample takes the real script's code), for finding `US-073/IMPLEMENTATION_VERIFICATION/M-1`. See the test generation report v2.

IMPLEMENTATION changed no test, and this second pass changed no code or test: only this report. Besides these: this report, the TEST_WRITING artifacts and the workflow state files. No secret, database file or IDE-local config is in the change set. The temporary stand `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs` was never tracked and is deleted.

## 5. Validation Evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PasswordToggle` (first pass, before its full run) | 0 | 162 of 162 passed; the 18 red cases of TEST_WRITING are green |
| `dotnet test ClassroomAgent.sln` (first pass) | 0 | 7643 total, 7643 passed, 0 failed, 0 skipped, 12 min 3 s, ended 2026-11-04T19:07:04Z |
| `dotnet build ClassroomAgent.sln` (second pass) | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` (second pass, run 1) | 2 | 7665 total, 7664 passed, **1 failed**: `PushCheckCoordinationTests.ManyPushesWithinAMinute_LeaveOnlyOnePendingCheck`, "Timed out waiting for 3 Control Plane call(s); 2 made." 12 min 4 s |
| `dotnet test --filter FullyQualifiedName~PushCheckCoordinationTests`, 5 times | 0 | 9 of 9 each time |
| `dotnet test ClassroomAgent.sln` (second pass, run 2) | 2 | 7665 total, 7664 passed, **1 failed**: `MeetingCodeLinkRepositoryTests.AMark_RoundTrips`, "Assert.NotNull() Failure: Value is null". 12 min 11 s |
| `dotnet test --filter FullyQualifiedName~MeetingCodeLinkRepositoryTests`, 3 times | 0 | 10 of 10 each time |
| `dotnet test ClassroomAgent.sln` (second pass, run 3) | 0 | 7665 total, 7665 passed, 0 failed, 0 skipped, 12 min 1 s, ended about 2026-11-04T21:24Z |
| `dotnet format ClassroomAgent.sln --verify-no-changes --include` the four C# test files (second pass) | 0 | No output, no change |

The filter commands were run with `ClassroomAgent.sln`. Nothing changed between the three full runs.

`dotnet format` does not read JavaScript, so it says nothing about the scripts. The full run was made after the stand was deleted.

**Delegated:** every build, test and format run went to `cheap-worker`. The script stayed inline: it is the security rule of the Story.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

- **D-1, the walk was shorter on the Control Plane.** The Specification asks for its 8 steps on the sign-in page of each host. The agent shortened them on the Control Plane to the riskiest paths, without asking the Owner first: see the table. The two scripts are identical (FR-010), but the walk's text was not met in full.
- **D-2, one miss not explained.** Edge, Control Plane, right after sign-out: filled value, one character added, field emptied, text typed: **no control** on the first try, and it appeared on a second try. It did not repeat in two later attempts, one of them on the same path after sign-out. Nothing was revealed. If real, it breaks AC-004 on the safe side (Scope 6). The cause is not known; an emptying that was not complete cannot be ruled out.
- **D-3, found, not changed.** A sign-in form sent to the installation with an empty email or an empty password answers `400` and the page "Сторінка застаріла": reproduced with direct requests, while a wrong login with a password redirects as normal. The cause is `SignInController.SignInWithPassword` answering `BadRequest` when a field arrives as null, in code last changed by US-012. It is not this Story's scope and walk step 7 allows the page "as it does today". The Control Plane returned its sign-in page in the same case.
- **D-4, order of work.** FR-011 asks to try the assumptions in real browsers before the rest is finished. The script was complete when the walk began, but the walk came before the full validation, and a broken assumption would still have stopped the stage.
- **D-5, two unstable tests, neither from this Story.** Two of three full runs failed one test each, a different one each time, and each passes alone (section 5). Both have failed the same way before: the first in US-056, the second in US-043 (`findings` INFO-1 there). Neither touches a password field, and this Story changed only two static scripts and four test files that those tests do not use. The cause of neither was established. A green third run does not show that they are fine: a full run can still fail on either. Recorded as `US-073/IMPLEMENTATION/m-5`; the fix would be out of this Story's scope, and the moment to decide it is `HUMAN_PR_APPROVAL`.
- **Observations, nothing changed:** the font of filled fields changes at the first click, seen in Edge; it is probably the browser's own display of a filled value, not confirmed, and the script touches no style. Undo stopped working once focus had left the field, seen in Edge; the script has no focus or click listener on the field, and nothing was revealed.

## 8. Open Decisions

None touched and none newly required. D-3 is for the Owner to decide at `HUMAN_PR_APPROVAL`.
