---
artifact_type: implementation_verification
story: US-073
version: 2
status: APPROVED
created_at: 2026-11-04T20:32:17Z
updated_at: 2026-11-04T21:32:51Z
produced_by: implementation-verifier
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
  - path: docs/evidence/US-073-test-generation-report.md
    version: 2
  - path: docs/evidence/US-073-implementation-report.md
    version: 2
supersedes: docs/reviews/implementation/US-073-implementation-verification.md@v1
---

# US-073 Implementation Verification

## 1. Summary

**Verdict: PASS.** No Critical or Major finding; one new Minor finding (m-2).

- **The script is correct as read.** Every rule of FR-002 to FR-009 is implemented as written, and FR-006 step 3 is the only route to `ENTERED`. The two host files are byte-identical. The scripts were last written at 15:58Z, before the first report and before M-1, and did not change at the loop-back.
- **v1's M-1 is really closed.** All twelve defects A to L, applied this time to the real script and not to the test sample, are now reported, and so are three more made up for this review.
- **What the rules still do not fix** is code outside the deciding functions (m-2). Of the single defects tried there, none can show a saved password. The worst one keeps a typed password visible on leaving the page, in code this Story did not change.
- **The behaviour in a browser rests on the Owner's walk,** as the Story says it must. Its gaps on the Control Plane are the Owner's to accept (D-1). This review cannot re-run it.

## 2. Reviewed Artifacts

The versions in the front matter, and the working-tree diff against `c0e3dfb`. The API and database designs are NOT_APPLICABLE; `openapi` and `entity_model` do not exist for this Story.

## 3. AC Verification

"Code" is this review's reading of the script. "Rules" is whether the mapped source tests fix what the `ac_test_matrix` says they fix, checked by the probe in section 8. "Walk" is the record in implementation report section 3.1.

| AC | Code | Rules | Walk | Verdict |
|---|---|---|---|---|
| AC-001 | Conforms: `onBeforeInput`, `onInput` step 3, `setState` | Yes, A, B, D caught; the markup tests fix the initial `hidden` | Both hosts; paste not walked on the Control Plane | Met |
| AC-002 | Conforms: `onInput` step 2 | Yes, L caught | Installation in full; emptying while shown not walked on the Control Plane | Met |
| AC-003 | Conforms: `settle`, `isFilled`, `onPress` | Yes, H, N2, N3 caught | Both browsers, both hosts | Met |
| AC-004 | Conforms: `onBeforeInput`, `onInput` step 5 | Yes, C, D caught | Installation in full; shorter on the Control Plane; D-2 | Met, with D-2 open |
| AC-005 | Conforms: `onInput` step 1, `onChange` | Yes, E, F, I, L caught | Both browsers, both hosts | Met |
| AC-006 | Conforms: `onBeforeInput` "any other kind", `onInput` step 1 | Yes, A, B, E, J caught | Both browsers on the installation; on the Control Plane one of two undo paths | Met |
| AC-007 | Conforms: files identical, one helper, no forbidden construct | Yes for the helper and the scan. For hiding on leaving see below | Not applicable | Met |
| AC-008 | Not applicable | Manual by its own text | Recorded; the Control Plane part is shorter than the Specification's text (D-1) | Installation met; Control Plane for the Owner to decide (D-1) |

**AC-007, hiding on submit and on leaving.** `TheScript_HidesTheFields_OnSubmitAndOnPageHide` sees only that the listeners exist, so it would pass with an empty `hideAll` (N5, N6). AC-007 is still met, on other evidence: the Story asks that this behaviour stay unchanged, and against `HEAD` the listeners, `hideAll` and `fields.push(field)` are unchanged lines. So this is no test that passes for new behaviour, and the weakness dates from US-069, whose Specification says no test watches a listener run. It is recorded as m-2.

## 4. Specification Coverage

| Requirement | Finding |
|---|---|
| FR-001 | Partial unchanged, the control starts `hidden` in both hosts; views unchanged |
| FR-002 | Three states in memory; `setState` hides the field before the control and is the only writer of the state |
| FR-003 | The closed list is exact; `isFilled` answers true on a throw; a missing `inputType` is "any other kind" |
| FR-004 | `settle` at load and on `pageshow` persisted; the ready class is independent of the state |
| FR-005 | Conforms, including that nothing cancels the event |
| FR-006 | Steps 1 to 5 in order; the mark is cleared after them |
| FR-007 | `onChange`, `onPress` and the focus move conform |
| FR-008 | One comparison in `isEmpty`; nothing else reads or writes the value; no network, storage or console. FR-008.3: the scan accepts the helper and reports other uses (11 new cases read) |
| FR-009 | `submit` and `pagehide` listeners, button, names and attributes unchanged against `HEAD` |
| FR-010 | Files identical, checked with `diff`, and fixed by a test |
| FR-011 | Walked, see D-1 and D-4; the outcome of each assumption rests on the report |

Paths traced by hand through the code, each ending as the Specification says: typing a first and second character; emptying while shown; an undo to an empty field, then typing; a fill with no `inputType` over typed text; a deletion as the first action on hidden text; a mark left by an `input` that never came, followed by a fill. Behaviour no artifact asks for: none.

## 5. Design Conformance

No endpoint, schema, policy, view or translation entry changed. AD-3, AD-4, AD-6 and AD-8 are not touched: the production change is two static scripts.

## 6. Deviations

| Id | Departs from | Reason given | Verdict |
|---|---|---|---|
| D-1 | Specification, AC-008 walk ("the sign-in page of each host"); Story AC-008 ("in both hosts") | The agent shortened the Control Plane walk to the riskiest paths, the scripts being identical; the Owner was not asked first | **Technically sound, not this stage's to waive.** The identity of the scripts is confirmed. The letter of AC-008 is not met: IMPLEMENTATION/m-1 stays OPEN for the Owner |
| D-2 | AC-004, one unrepeated miss | Unknown | **Safe side, unexplained.** In the code, no control after entry needs `:autofill` still true, text left in the field, or a kind outside the closed list. Each gives no control, none a reveal. The record does not say which. IMPLEMENTATION/m-2 stays OPEN |
| D-3 | None: existing behaviour | Out of scope | **Confirmed.** `SignInController.SignInWithPassword` returns `BadRequest()` on a null field, last changed by US-012 (`b53fd92`), and this Story does not touch it. IMPLEMENTATION/m-3 stays OPEN for the Owner |
| D-4 | FR-011, order of work | The script was finished before the first browser check; a broken assumption would still have stopped the stage | **Sound.** The purpose, not building on an unproved rule, was kept |
| D-5 | Definition of Done item 2, as to what a green run shows | Two of three full runs failed one test each, outside this Story | **Disclosed honestly.** Both tests are named as earlier failures in the US-056 report and the US-043 security review. This Story's change cannot reach them: two static scripts and test files they do not use. Their cause is not established. IMPLEMENTATION/m-5 stays OPEN for the Owner; `security-reviewer` runs the full suite next |

IMPLEMENTATION/m-4 (the phone step on an iPhone only) is no deviation: the Specification asks for one phone and one host. It stays OPEN for the Owner.

None of these is closed here: a stage closes only `FIXED` or `WITHDRAWN`. Undisclosed deviations: none found.

## 7. Report Accuracy

- The change set equals the diff: 2 scripts, 2 changed and 2 new test files, plus artifacts and state files.
- File times agree with the report: the scripts at 15:58Z; `PasswordToggleSourceTests.cs` and `PasswordFieldMarkup.cs` at 15:26Z; the two other test files at 20:37Z and 20:39Z, inside TEST_WRITING attempt 2 (20:32Z to 20:41Z). So the loop-back changed no script and IMPLEMENTATION changed no test.
- The test generation report's counts agree with the code: 41 + 22 = 63 defect rows, 83 cases in the class, 184 in the filter.
- Report section 1 now carries the walk's limits (v1's m-1, confirmed fixed).
- Every test named in the AC table exists. No `TODO`, `TBD`, `FIXME`, skip or `NotImplementedException` in the changed files.
- Cleanup: `tests/ClassroomAgent.Tests/LookCheck` is absent, and `dotnet dev-certs https --check --trust` reports that no certificate is trusted. A search of the firewall rules for `LookCheck`, `look-check` and `classroom` finds nothing, though the rule's real name is not recorded, so this does not fully prove its removal. The deleted browser profiles were not re-checked.
- The full-run results and the walk rest on the report; this stage does not rerun them.

## 8. Story Test Run

`dotnet test ClassroomAgent.sln --filter FullyQualifiedName~PasswordToggle`, delegated to `cheap-worker`, ended 2026-11-04T21:30:32Z: exit 0, 184 total, 184 passed, 0 failed, 0 skipped, 28 s. The full suite is `security-reviewer`'s.

**Mutation probe**, run inline by this Skill with `dotnet fsi` from a scratch script that loads the built test assembly: each row applies one defect to an in-memory copy of the installation's real script and calls the nine rules of v1 plus `ValueHelperCount`, `ListensForSubmitOnThePage` and `ListensForPageHide`. No repository file was changed. The unmodified script reports no problem.

| # | Defect | Consequence if it shipped | Rules |
|---|---|---|---|
| A to L | The twelve defects of v1 | Several reveal a saved password | **All caught** (`kinds`, `beforeinput`, `input`, `press`, `visibility`) |
| N1 | `isEmpty` inverted | Wrong states | Caught |
| N2 | `isFilled` always false | **Reveals** | Caught |
| N3 | `settle` always `EMPTY` | Wrong state at load | Caught |
| N8 | A new listener calls `setState(field, ENTERED)` | **Reveals** | Caught |
| N9 | A new listener calls `show(field)` | **Reveals** | Caught |
| N11 | `control.hidden = false` at load | The control is visible; a press is still guarded | Caught |
| N12 | `field["state"] = "ENTERED"` | **Reveals** | Caught |
| N13 | A field starts in `ENTERED` | **Reveals** | Caught |
| N16 | `historyUndo` added to the list at run time | **Reveals** | Caught |
| N17, N18, N19 | `pagehide`, `submit` or `pageshow` listener removed or renamed | Hiding on leaving, or the re-check on return, is lost | Caught |
| N4 | A new function sets the type with `setAttribute("type", "text")` | Reveals only if something calls it, and that code would be new | **Not caught** |
| N5 | `hideAll` empty | A typed password stays visible on leaving; the browser may keep it as ordinary text | **Not caught** |
| N6 | `fields.push(field)` removed | The same as N5, and no re-check on return from the cache | **Not caught** |
| N7 | The click listener on the input, not the control | The control does nothing; a click in the field toggles typed text. `onPress` still guards a filled value | **Not caught** |
| N10 | `control.style.display = "block"` | The control can be seen when it should not; a press is still guarded | **Not caught** |
| N14 | `onInput` gets a made-up event of an entry kind | Traced by hand: the `beforeinput` rules keep filled values out of `ENTERED` | **Not caught** |
| N15 | `onBeforeInput` gets a made-up event of an entry kind | Traced by hand: the real kind at `input` sends the field to `FILLED` | **Not caught** |
| N20 | The ready class never added | The script's own padding and Edge's button rule are lost | **Not caught** |

## 9. Findings

### m-2 (Minor) The rules do not fix the script's text outside the deciding functions

- **Where:** `tests/ClassroomAgent.Tests/TestInfrastructure/PasswordToggleScriptRules.cs`, `HasListener` and the `...Problems` methods; `PasswordFieldMarkup.ListensForSubmitOnThePage`, `ListensForPageHide`.
- **Checked against:** `docs/tests/US-073-ac-test-matrix.md`, row "US-069's behaviour for a shown field"; test strategy, Known limitations, whose last sentence discloses this in general terms.
- **What is wrong:** the eight defects N4, N5, N6, N7, N10, N14, N15 and N20 pass every rule. `hideAll` and `fields.push(field)` are not fixed, the listeners' receiver and event argument are not fixed, and code added elsewhere is seen only through the patterns the rules look for.
- **Why Minor:** no single defect of these shows a saved password, except new code that N4 stands for, and no source rule can exclude every addition. The lines behind N5 and N6 are unchanged from US-069, so AC-007's "unchanged" is shown by the diff; the wiring is read by this review and walked. The Story's security core is fixed.
- **Possible fix:** fix the whole of the script's code as one text, comments aside, which closes all eight with no script change and so keeps the walk valid. That is one constant in `PasswordToggleScriptRules` and a little more change friction than today. No loop-back is needed for it.

Open findings of the Story, none closed here: IMPLEMENTATION/m-1, m-2, m-3, m-4, m-5, see section 6.

## 10. Delegation

The Story's test run went to `cheap-worker`. Reading the code and tests, the mutation probe and all judgement stayed in this Skill.

## 11. Verdict

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_VERIFICATION
  story: US-073
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/implementation/US-073-implementation-verification.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-2: The source rules do not fix the script's text outside the deciding functions: an empty hideAll, a removed fields.push and wrong listener wiring pass; no single one of these shows a saved password."
    - "D-4: FR-011 order of work: the script was finished before the first browser check; sound, a broken assumption would still have stopped the stage."
  closed_findings: []
```
