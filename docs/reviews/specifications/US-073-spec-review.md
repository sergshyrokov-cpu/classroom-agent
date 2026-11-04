---
artifact_type: specification_review
story: US-073
version: 4
status: APPROVED
created_at: 2026-11-04T14:52:57Z
updated_at: 2026-11-04T15:15:18Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-073-show-password-only-for-typed-text.md
    version: null
  - path: trebovaniya.md
    version: 110
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/specifications/US-073-spec-digest.ru.md
    version: 4
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
  - path: docs/specifications/US-069-spec.md
    version: 3
supersedes: docs/reviews/specifications/US-073-spec-review.md@v3
---

# US-073 Specification Review

## Summary

**PASS**: no finding at any severity. Version 4 closes m-8 of the previous review as the Owner asked at the gate. The state
machine was walked again from the text, not taken from the earlier review, and holds. One consequence of the m-8 fix is worth the
Owner's attention and is stated under Story Coverage; it is not a defect. Nothing was delegated.

Versions 1 to 3 of this review were never committed, so their findings are kept in the Appendix, and the ledger
`docs/workflow/findings.jsonl` holds their events.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-073-show-password-only-for-typed-text.md` | unversioned |
| Specification | `docs/specifications/US-073-spec.md` | 4 |
| Digest | `docs/specifications/US-073-spec-digest.ru.md` | 4 |
| Open Decisions | `docs/decisions/US-073-open-decisions.md` | 1 |
| Requirements | `trebovaniya.md` §8 «Показ пароля» | v110 |

Also read: SC-2 «Showing a typed password». The earlier reviews' reading of US-069 spec v3 FR-006 and `_PasswordToggle.cshtml` was
not repeated: `src` and `tests` have no change since commit `ffe2b6e`, and the last commit touching them is older. The two
`password-toggle.js` files were compared again and are identical apart from line endings, as FR-010 says. No input is stale: the
Specification names the Story and v110, the digest names Specification v4.

## Previous Findings

| Id | Claimed fix | Checked |
|---|---|---|
| m-8 | Walk step 5 records a list not offered over typed hidden text, not a failure; the digest no longer says "in any case" | Holds. Step 5's second part has the outcome, and the phrase is gone. Digest section 2 now names both displays |

## Fidelity

Each §8 v110 sentence on this control is carried: typing or pasting (FR-003), none in an empty field (FR-002), a filled value never
shown and editing not bringing it back (FR-005.1, FR-006 step 5), clearing and entering anew (FR-006 steps 2 and 3), a fill after entry
(FR-006 step 1, FR-007.1), doubt gives none (FR-003), the known limitation (Security Requirements), a person's check in Edge and Chrome
(AC-008), no browser icon beside it (FR-004.1), hidden on submit, no spell check, no script no control (FR-009, AC-007). SC-2 agrees.

Version 4's statement that no rule changes could be compared only with the previous review's record, since no version was committed.
Paths walked against FR-002 to FR-007 in this version:

| Path | Result |
|---|---|
| Empty; type two characters | Mark set, `ENTERED` at step 3; second character reaches step 5 and stays |
| `ENTERED`; select all and paste | Stays `ENTERED` |
| `FILLED`; select all and type | Value not empty at `beforeinput`, stays `FILLED`; step 5 keeps it |
| `FILLED`; cut everything, then type | Deletion kind, step 2 `EMPTY`; then mark set, `ENTERED`. This is the permitted "emptied and entered anew" |
| `FILLED`; empty it, Ctrl+Z | `historyUndo` gives `FILLED` at `beforeinput`, step 1 keeps it |
| Filled, emptied, typed and shown; Ctrl+Z | Hidden at `beforeinput`, before the text changes (FR-005.3) |
| Typed, Ctrl+Z down to an empty field | `FILLED` over an empty field by step 1; the next typing finds it empty, marks it and reaches `ENTERED`. Safe, and the control returns on new typing as the digest says |
| Held-back fill, no event; press impossible, then a deletion | `EMPTY` with text, step 4 `FILLED` |
| Mark set, then no `input`, then a fill with no event; next typing | Not empty at `beforeinput`, `FILLED` |
| Return by `pageshow` with text in the field | `FILLED` (FR-004.2); a value restored after the script started raises no control until an event, the safe side |

No route reaches `ENTERED` except FR-006 step 3 with the mark set, and the mark is set only while the field is empty and not filled.
`blocked_by_specification` in FR-011 is a real key of the IMPLEMENTATION loop-back in `stage-map.yaml`.

## Story Coverage

AC-001 to AC-008 and Scope 1 to 7 are represented. Nothing goes beyond the Story.

**Consequence of the m-8 fix, not a defect.** Both parts of walk step 5 may now end as "recorded, not a failure". If neither Edge nor
Chrome offers a list over typed text, nobody observes AC-005 and A-5 in a browser, and only SECURITY_REVIEW's reading of the script
stands behind them. The risk stays small, because with no list that route to a fill does not exist. A late fill raising no event would
remain unobserved. Digest section 2 tells the Owner the part cannot be walked and is not an error, so the choice is visible. The
implementation report must say plainly which browsers offered the list.

## Consistency

Unchanged: US-069 FR-006.1 and the "never read" sentence are revised openly, the rest stands (FR-009). No permission, read-only, audit
or translation entry is touched.

## Open Items

`trebovaniya.md` section 7 holds no item on this control. None open, which matches the Story.

## Interpretations

| # | Choice | Digest section 2 |
|---|---|---|
| I-1 | The script may compare the value with the empty string (FR-008) | named |
| I-2 | Entry is the closed list; drop and dictation give no control (FR-003) | named |
| I-3 | Undo in one's own typed text also removes the control (FR-005.3) | named |
| I-4 | Text restored on "back" counts as filled (FR-004.2) | named |
| I-5 | Enter does not remove the control (FR-003, FR-005.2) | named |
| I-6 | Edge's own button stays hidden with no control present (FR-004.1) | named |
| I-7 | Broken browser assumptions: no control is accepted, a reveal stops the work (FR-011) | named |
| I-8 | Computer walk: only the sign-in page of each host | named |
| I-9 | Phone: typing only, on one host | named |
| I-10 | A list not offered over typed text, shown or hidden, is recorded, not a failure (walk step 5) | named, both displays |

## Digest Fidelity

Every statement traces to the Specification, in plain Russian, and no decision is softened. The paragraph on what the Owner must do
lists the additions truthfully.

## Findings

None.

## Verdict

`PASS`. Ready for `HUMAN_SPEC_APPROVAL`.

## Appendix: findings of version 3 (Specification version 3)

Closed as FIXED by SPECIFICATION and checked above.

**m-8. Walk step 5 has no outcome if the browser offers no list over typed hidden text.**
Location: AC-008 walk step 5, second part; digest section 2, the sentence ending "проверяем в любом случае". Checked against:
FR-011 ("The AC-008 walk proves them"), A-5. The walk excused a missing list only for a field shown as text. If a browser did not offer
it, the person could not do this part, the walk did not say what then, and the digest's "in any case" was untrue. Fix: the same outcome
as the shown field, recorded in the report and not a failure, and "в любом случае" dropped from the digest.

## Appendix: findings of version 2 (Specification version 2)

All three were closed as FIXED by SPECIFICATION and checked above.

**m-5. The walk never edits a value filled at load as the first action on the page.**
Location: AC-008 walk steps 1 and 2; FR-011 A-2. Checked against: FR-011 ("The AC-008 walk proves them"), Story AC-004.
Step 1 has the person press Tab and choose from the suggestion list before step 2 edits. Those are user gestures, so by then
Chrome has probably released a held-back value (from memory, not verified here). A-2 is about the moment before release: if
it breaks, FR-005.1 sees an empty field that is not filled, marks it, and step 3 gives `ENTERED` over the saved password. That
is the one path by which a break reveals a value, and the walk as ordered does not exercise it. The first check of
IMPLEMENTATION may catch it, but the Specification does not name this path there either. Fix: a separate step on a fresh load
with a saved password, the edit being the first action on the page: type one character, clicking into the field first only if
it has no focus: no control. Again on a fresh load, deleting one character instead.

**m-6. A fill after typing is walked and assumed only for a field shown as text.**
Location: FR-011 A-5, AC-008 walk step 5. Checked against: `trebovaniya.md` §8 ("Если браузер заполнил поле уже после ввода"),
Story Scope 4. §8 and Scope 4 do not limit this to a shown field; AC-005 does. The rules handle a typed, still hidden field
if an event comes. With no event the control stays until a press, and if the person first edits a character `:autofill` is
gone, so FR-007.2 no longer catches it and the press reveals the filled value. Also, a browser may not offer its suggestion
list on a field shown as text, which would make step 5 impossible as written, and the walk gives no outcome for that. Fix:
extend A-5 to a typed field in either display; add a walk step: type without showing, choose from the suggestion list: hidden,
no control; and say that a list not offered on a shown field is recorded in the report rather than failing.

**m-7. Emptying a shown field is not walked.**
Location: AC-008 walk step 3; Traceability row AC-002. Checked against: Story AC-002 ("shown or hidden"). Step 3 shows, hides
and only then empties. No test runs the script, so returning to hidden on this path (FR-002) is proved by nobody; the failure
would be an empty field left as `type="text"`, whose next typed text is visible without a press. Fix: in step 3, press to show,
remove the text, then type: the text is masked and the control is back.

## Appendix: findings of version 1 (Specification version 1)

All five were closed as FIXED by SPECIFICATION and checked above.

**M-1 (Major). FR-006 contradicts itself; read in order, the second typed character removes the control.** Step 3 ended: "An
entry kind without the mark is doubt: `FILLED`." The mark is set by FR-005.1 only while the field is empty, so the second
character went to `FILLED`, and step 5 ("leave `ENTERED` as it is") was never reached. AC-001 failed for every password longer
than one character. The failure was on the safe side, but no source test could see it.

**m-1. FR-007.3 has no source.** Moving focus from a control that became `hidden` to its field was cited nowhere.

**m-2. The source scan's change is not stated.** FR-008.1 also changes `TheScript_DoesNothingElse`; the Specification should
say the scan still reports any other use of the value, including `var v = input.value;`.

**m-3. Two browser assumptions are missing from FR-011.** (a) A fill into a field that is `ENTERED` and shown as text must
raise an `input` or `change` event, or the password stays visible as text. (b) Enter may raise `beforeinput` of kind
`insertLineBreak`, which under FR-005.3 would remove the control if client validation blocks the submit.

**m-4. The digest does not say the computer walk covers only the sign-in pages.**
