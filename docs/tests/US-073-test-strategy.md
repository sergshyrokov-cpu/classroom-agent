---
artifact_type: test_strategy
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
  - path: docs/designs/api/US-073-api-design.md
    version: 1
  - path: docs/designs/database/US-073-db-design.md
    version: 1
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
supersedes: docs/tests/US-073-test-strategy.md@v1
---

# US-073 Test Strategy

## Scope

The eye control's rules live in one static script per host. The tests read that source in both hosts; a person proves the behaviour in real browsers (AC-008). Nothing else changes, so nothing else is tested anew: no endpoint, schema, policy, form field, view text or translation entry.

## What these tests can and cannot prove

**No automated test runs the script.** The project has no JavaScript engine or browser test tool, Node is not installed, and adding a package would need an Open Decision. The Specification's traceability table expects this (source tests, the walk, SECURITY_REVIEW). So:

- a green suite proves the source has the required **form**, down to the text of every function that decides something: the closed list, the single route to `ENTERED`, the order of the checks, and each condition with its negations and operators (version 2, finding `US-073/IMPLEMENTATION_VERIFICATION/M-1`);
- it does **not** prove that the script behaves correctly in a browser, or any of the assumptions A-1 to A-5 of FR-011;
- AC-003, AC-005 and AC-006 depend on a browser's filling, which only the AC-008 walk can show. AC-008 stays open until the walk is recorded in the implementation report.

## Test levels

| Level | Use |
|---|---|
| Source (architecture folder, no host, no database) | FR-002 to FR-010, both hosts: `PasswordToggleStateSourceTests` (new), `PasswordToggleSourceTests` (changed where it fixed the old rule, AC-007) |
| Integration (HTTP), existing | `PasswordToggleMarkupTests`, `ControlPlanePasswordToggleMarkupTests`: the control is rendered `hidden`, so it is unreachable until the script acts (AC-001), and without scripts stays so (AC-007). They do not change |
| Manual | The AC-008 walk, at IMPLEMENTATION |
| Unit, contract, persistence | None: no such change (API_DESIGN and DB_DESIGN are NOT_APPLICABLE) |

## Source form the tests read (for IMPLEMENTATION)

Reading rules out of source needs fixed names. These are a technical choice of this stage, no requirement of the Specification, and they constrain only how the script is written:

| Element | Form |
|---|---|
| States | `EMPTY`, `ENTERED`, `FILLED`, with `ENTERED` declared as `ENTERED = "ENTERED"` |
| Entry kinds | one array literal holding exactly the four values; the Enter kind named once outside it, compared through `ENTER_KIND` |
| Deletion kinds | `indexOf("delete") === 0`, `startsWith("delete")` or `/^delete/` |
| Helpers | `isEmpty(input)` holding only `return input.value === "";`, `isFilled(input)` holding only the `:autofill` try/catch, `isEntryKind(kind)`, `isDeletionKind(kind)` |
| State change | `setState(field, state)`, the only writer of `field.state`, holding `field.control.hidden = state !== ENTERED;` |
| Route to `ENTERED` | `field.awaitingEntry ? ENTERED : FILLED` in `onInput`, the mark set only in `onBeforeInput` |
| Handlers | `function onBeforeInput(field, …)`, `onInput(field, …)`, `onChange(field)`, `onPress(field)`, `settle(field)`, each called from a `function` listener whose first statement calls it |
| Whole text, white space aside (version 2) | The bodies of `isEntryKind`, `isDeletionKind`, `show`, `hide`, `setState`, `settle`, `onBeforeInput`, `onInput`, `onChange`, `onPress`; the declarations of the states, the entry kinds, the Enter kind and a field; the `pageshow` condition. They are constants in `PasswordToggleScriptRules` |

`PasswordToggleStateSourceTests.ConformingScript` shows one source that passes. It is test data, never run. Since version 2 it has the real script's code, because both must pass the same whole-text rules; the walk proved that code, and the sample adds no proof of its own. A deliberate change to a fixed text changes its constant in the same step, is disclosed in the implementation report, and needs AC-008 walked again: the walk proved this text.

## Scenarios

**Positive.** The closed list is present; `:autofill` decides filling; a marked entry reaches `ENTERED`; the control shows only there; the two scripts are identical (FR-010).

**Negative (the security core, SC-2).**

- A second route to `ENTERED`: at `beforeinput`, by a direct state write, from `settle`, or with no mark.
- `historyUndo`, `insertFromDrop`, `insertReplacementText` or `insertLineBreak` in the entry list, or kinds matched by the prefix `insert`.
- A throwing `matches` answering "not filled".
- The control revealed outside `setState`, or hidden before the field is.
- A press that shows without re-checking; `show` called from elsewhere.
- The words all present and the logic wrong (version 2): a negated or inverted condition, `||` for `&&`, a helper that ignores its list, a kind check that accepts a missing kind, `hide` that no longer hides, a state with another state's value, a field that starts marked.
- The value used beyond the one comparison in the one helper; an event cancelled.

**Boundary.** Step 3 of FR-006 limited to state `EMPTY`, so a second typed character reaches step 5; step 1 before step 2, so a fill with an empty-looking value is not `EMPTY`; the mark cleared after every `input`.

**Validation, persistence.** None: no input, schema or storage changes. The script stores nothing (existing scan).

**Security.** The scan of `TheScript_DoesNothingElse` keeps reporting network, storage, console, submit and load, and now also `preventDefault` and `input['value']`. Sign-in, lockout and role tests are untouched and no endpoint changes, so no allowed-role or forbidden-role test is due (TC-5). SECURITY_REVIEW reads the script (spec, Security Requirements).

## The rules are themselves tested

Source rules can pass vacuously, so each is proved both ways: `AConformingScript_PassesEveryRule` shows they can all pass together, and `ADefect_IsReportedByItsRule` applies 63 defects to that source and expects the responsible rule to report each. Version 2 added 22 defects that keep every word: defects A to L of the verification, and 10 of the same kind. Since the real script passes the same whole-text rules, a defect reported on the sample would be reported on the script.

## Fixtures

`TestInfrastructure/PasswordToggleScriptRules.cs` (new) holds the rules. `PasswordFieldMarkup.ScriptProblems` accepts the one helper (FR-008.3). Nothing needs Docker except the existing markup tests.

## Excluded, with justification

- **Running the script in a headless browser from a test:** it would make the suite depend on an installed browser and could still not reproduce saved-password filling, which is the point of the Story (Story, AC-008).
- **Filling on a phone:** out of the Story's walk.
- **A password-manager extension imitating key presses:** known limitation (`trebovaniya.md` section 8).

## Known limitations

See "What these tests can and cannot prove". The source rules fix the text of the deciding functions, so any change to them fails a test, however harmless, even renaming a variable or an equivalent rewrite. That cost is accepted: nothing runs the script, and the walk is not repeated unless someone knows the text changed. The rules are a change guard with the logic spelled out. They do not prove that the fixed text is right; the walk and SECURITY_REVIEW do. A defect in code outside the fixed texts is still caught only by the narrower rules, such as the listeners' wiring.

## Open Decisions affecting testing

None.
