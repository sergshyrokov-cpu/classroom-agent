---
artifact_type: specification
story: US-073
version: 4
status: APPROVED
created_at: 2026-11-04T14:43:28Z
updated_at: 2026-11-04T15:18:16Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-073-show-password-only-for-typed-text.md
    version: null
  - path: trebovaniya.md
    version: 110
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
  - path: docs/reviews/specifications/US-073-spec-review.md
    version: 3
supersedes: docs/specifications/US-073-spec.md@v3
---

# US-073 Specification — The eye shows only a password the person entered

Grounded in `trebovaniya.md` v110 §8 («Показ пароля»), NFR-077 and SC-2 («Showing a typed password»).
Section 7 of `trebovaniya.md` holds no open item on this control. Nothing was delegated: the code facts
were read directly at commit `ffe2b6e`. Version 3 closes findings m-5 to m-7 of the review of version 2, as the Owner asked at the gate: A-2, A-5 and the
AC-008 walk grow, and no rule changes. Version 4 closes m-8 of the review of version 3, as the Owner asked at the
gate: walk step 5 gains one outcome, and no rule changes.

This Story replaces US-069 FR-006.1 and the sentence "the value is never read" of FR-006.2. Everything
else of US-069 FR-005 to FR-008 stands.

## Overview

Since US-069 the eye control appears at page load on all 9 password fields in 6 views of both hosts, so
it also reveals a password the browser filled in. After this Story the control appears only after the
person enters text into an empty field by typing or pasting. Only the host's script and its source tests
change. No endpoint, schema, policy, form field, view text or translation entry changes.

## Business Goal

On a shared school computer with a saved Dean password, nobody at the sign-in page can read that password
with one press. People who type a password keep the control.

## Business Flow

1. A page opens with an empty password field: no control.
2. The person types or pastes: the control appears and works as in US-069.
3. The person removes all the text: the field is hidden and the control goes. Typing brings it back.
4. The browser fills in a saved password, at load or later: no control, whatever the person then edits,
   until the field is emptied and text is entered anew.
5. The browser fills in after typing, or an undo brings a value back: the field goes back to hidden and
   the control goes.

## Functional Requirements

### FR-001 Where it applies

All 9 fields in the 6 views of the table in US-069 FR-005. The markup stays as it is: the control starts
`hidden` in the host's `_PasswordToggle.cshtml` partial, and the views do not change.

### FR-002 Three states per field

The script keeps one state per field, in memory only:

| State | Meaning | Control | Input type |
|---|---|---|---|
| `EMPTY` | the field holds no text | `hidden` | `password` |
| `ENTERED` | the text was entered into an empty field by typing or pasting, and nothing has cast doubt on it since | visible | as the person set it |
| `FILLED` | any other non-empty text, and any case of doubt | `hidden` | `password` |

Leaving `ENTERED` by any route returns the field to hidden first (US-069 FR-006.2's second state) and then
sets `hidden` on the control. The control is never visible outside `ENTERED`.

### FR-003 Definitions

- **Entry kinds**, a closed list of `inputType` values: `insertText`, `insertCompositionText`,
  `insertFromComposition`, `insertFromPaste`.
- **Deletion kinds**: any `inputType` starting with `delete`.
- **The Enter kind**: `insertLineBreak`. Enter changes no text in a one-line field and submits the form (US-069
  FR-006.3), and some browsers may announce it at `beforeinput`. Without this the control would go whenever the
  browser holds the form back. It is not an entry kind and never leads to `ENTERED`.
- **Any other kind**, a missing or empty `inputType` included, is none of these: `historyUndo`, `historyRedo`,
  `insertReplacementText`, `insertFromDrop`, dictation and anything a browser adds later.
- **Filled by the browser**: `input.matches(":autofill")` is true. If that call throws, because the browser
  does not know the selector, the answer is true (Scope 6, in doubt).
- **Empty**: the field's value equals the empty string (FR-008).

### FR-004 At load and on return to the page

For each `.password-field`, on load, and for every field again on a `pageshow` event with `persisted` true:

- FR-004.1 Add class `password-field--ready` to the wrapper, whatever the state. The field keeps its right
  padding and Edge's own reveal button stays hidden (US-069 FR-008.2) whenever the script runs, so filling
  brings neither a jump in the text nor the browser's own control.
- FR-004.2 If the field is empty and not filled by the browser the state is `EMPTY`; otherwise it is `FILLED`.
  So text already in the field when the script starts, or restored by the browser on "back", is never
  `ENTERED`.

### FR-005 On `beforeinput`

The listener never cancels the event and never changes the value.

- FR-005.1 Entry kind: if the field is empty and not filled by the browser, the state becomes `EMPTY` and
  this field is marked as awaiting entry. Otherwise, if the state was `EMPTY`, it becomes `FILLED`:
  text the script could not see has appeared. `ENTERED` and `FILLED` stay as they are.
- FR-005.2 Deletion kind or the Enter kind: no change. An `input` event of the Enter kind is not expected, since
  no text changed; if one arrives, FR-006 step 1 treats it as any other kind.
- FR-005.3 Any other kind: the state becomes `FILLED` at once, so the field is hidden before the browser
  changes the text and an undo paints no frame of a revealed value.

### FR-006 On `input`

Checked in this order, after which the mark of FR-005.1 is cleared:

1. Filled by the browser, the Enter kind, or any other kind (FR-003): `FILLED`. This is checked first, so a fill that
   arrives with an empty-looking value is not `EMPTY`.
2. Empty: `EMPTY`.
3. Entry kind with state `EMPTY`: `ENTERED` if the field is marked as awaiting entry, otherwise `FILLED`
   (doubt). This step applies only in state `EMPTY`, so the second typed character goes on to step 5.
4. Deletion kind with state `EMPTY` and text in the field: `FILLED`.
5. Otherwise the state stays. So adding to, deleting from or overtyping a `FILLED` value leaves it `FILLED`,
   and the same edits leave `ENTERED` as it is.

### FR-007 On `change`, and on a press of the control

- FR-007.1 On `change`: if the field is filled by the browser the state becomes `FILLED`.
- FR-007.2 A press first re-checks: filled by the browser gives `FILLED`, empty gives `EMPTY`, and the press
  then does nothing else. Only a field still `ENTERED` toggles, as in US-069 FR-006.2. This is the last
  barrier against a fill that raised no event.
- FR-007.3 If the control held the focus when it became `hidden`, the focus moves to its field. This is a
  supporting rule with no requirement of its own: the control is a keyboard-reachable button (US-069 FR-006.3),
  and hiding it while focused would drop a keyboard user to the top of the page. It shows and sends nothing.

### FR-008 What the script may do with the value

- FR-008.1 It compares the value with the empty string in one helper, and does nothing else with it: no
  copy, no length, no other property. It never writes it. This revises US-069 FR-006.2 and is the reason
  the Owner sees it in the digest: emptiness cannot be told otherwise without new markup.
- FR-008.2 US-069 FR-006.5 stands: no network request, no storage, no console, no inline code. If the script
  fails to run the page behaves as US-069 FR-007.
- FR-008.3 The source scan of `PasswordToggleSourceTests` (`TheScript_DoesNothingElse`) changes with FR-008.1, beyond
  what the Story names in AC-007. It accepts the one comparison in the one helper and still reports every other use
  of the value: its existing cases `var v = input.value;`, `defaultValue` and `getAttribute('value')` keep reporting,
  and assigning to the value or reading its length is reported too.

### FR-009 What stays from US-069

`submit` and `pagehide` listeners hiding every shown field (FR-006.4), the native button (FR-006.3), the
accessible names (FR-005.4), `spellcheck` and `autocapitalize` (FR-005.3), the icons and styles (FR-008).

### FR-010 One script

The two files `wwwroot/js/password-toggle.js` of the hosts stay identical apart from line endings, and a test
fixes it. They are identical today.

### FR-011 Browser behaviour this design relies on

The rules above were written from documented behaviour, not observed here. The AC-008 walk proves them, and
the first check of IMPLEMENTATION tries them in real Edge and Chrome before the rest is finished, as the Story
asks:

- **A-1** A fill raises no `input` event of an entry kind. Chrome and Edge are expected to send none or one with
  no `inputType`.
- **A-2** While Chrome holds a value filled at load back from scripts until the first user gesture, the field
  either matches `:autofill` or its value is already visible at the first `beforeinput`. If neither holds, an edit made
  as the first action on the page would give `ENTERED` over the saved password. Walk step 8 tries this path.
- **A-3** `:autofill` is still true at `beforeinput` of the first edit of a filled value.
- **A-4** Phone keyboards report entry kinds.
- **A-5** A fill into a field that is `ENTERED`, shown as text or still hidden, raises an `input` or `change` event at
  once. FR-007.2 acts only on a press, and after an edit `:autofill` no longer holds, so with no event at all a filled
  value would stay readable on a shown field, and a press could reveal it on a hidden one.

A broken assumption that leads to **no control** (A-4 on a given keyboard) is accepted under Scope 6 and recorded
in the implementation report. A broken assumption that could **reveal a filled value** (A-1, A-2, A-3, A-5) stops the
implementation: the stage returns `blocked_by_specification`, because a different way to tell entry from filling
is a Specification change, not a coding choice.

## Acceptance Criteria

Ids are the Story's.

- **AC-001** The control appears after typing or pasting into an empty field, and works as in US-069. Before
  that it is neither visible nor reachable by keyboard.
- **AC-002** Removing all the text, shown or hidden, gives a hidden field with no control. Typing again brings
  the control back.
- **AC-003** A password the browser saved, filled at load or from the suggestion list, in Edge and Chrome, has no
  control and cannot be shown by any press.
- **AC-004** Adding, deleting or overtyping characters of a filled value without first emptying the field leaves
  no control. After emptying the field and entering text the control appears.
- **AC-005** A fill after typing, with the field shown as text, returns the field to hidden and removes the control.
- **AC-006** An undo that brings the filled value back, to a field emptied and possibly typed into again, gives a
  hidden field with no control.
- **AC-007** Both hosts follow the same rules. Without scripts there is no control and the field works as before.
  The script sends nothing, stores nothing and does not change the value. US-069's behaviour for a shown field is
  unchanged. Source tests of both hosts check this; `PasswordToggleSourceTests` changes where it fixes the old rule.
- **AC-008** A person walks AC-001 to AC-006 with a really saved password in Edge and Chrome, in both hosts, on a
  computer, and walks typing on a phone. The walk is recorded in the implementation report. See below.

### The AC-008 walk

On a computer, Edge and Chrome, the sign-in page of each host, with a password really saved by that browser:

1. Fill at load: no control. Press Tab through the page: nothing to reach. Then fill from the suggestion list:
   no control.
2. Add a character, delete one, select all and overtype: still no control.
3. Empty the field and type: the control appears, shows and hides. Empty it again: hidden, no control. Type, press
   to show, then remove all the text: hidden, no control. Type again: the text is masked and the control is back.
4. Paste into an empty field: the control appears.
5. Type, press to show, then choose the saved password from the suggestion list: hidden, no control. If the browser
   offers no list on a field shown as text, this part cannot be walked: the report records that, and it is not a failure.
   Then empty the field, type without showing, and choose from the list: hidden, no control. If the browser offers
   no list over typed hidden text either, the same holds: the report records it, and it is not a failure.
6. With a filled field, empty it, press Ctrl+Z: hidden, no control. Empty it, type, press Ctrl+Z until the filled
   value returns: hidden, no control.
7. Type a password with the name empty and press Enter. If the browser holds the form back, the control is still
   there; if the form goes to the server, the page comes back as it does today.
8. Reload so that the browser fills at load. As the first action on the page, type one character, clicking into the
   field first only if it has no focus: no control. Reload again and delete one character the same way: no control.

The computer walk uses the sign-in pages only, not the other four views: all 9 fields run the same script and rules
(FR-001, FR-010).

On a phone: type into one password field on one host: the control appears, shows and hides; remove the text: it
goes. One host is enough, since FR-010 keeps the scripts identical. Filling on a phone is not part of the Story's
walk. The look-check stand listens on localhost only, so the phone step needs a LAN binding and a firewall
decision by the Owner.

## Validation Rules

No input is added and no validation changes. The 9 fields keep their names and server-side rules. The control
contributes nothing to a request.

## Security Requirements

- SC-2 «Showing a typed password» governs this Story. A control that reveals a browser-filled value is a finding.
- **SECURITY_REVIEW reads the script**: that no route other than FR-006 step 3 reaches `ENTERED`, that the entry
  kinds are exactly the closed list, that every doubt falls to `FILLED`, and that the value is used only as FR-008
  allows. This closes the concern of `US-069/SECURITY_REVIEW/INFO-1`.
- SC-15: the CSP string of both hosts is unchanged; no inline code. SC-13: nothing is sent or loaded (FR-008.2).
- SC-4: anonymous pages keep loading the same static file. No policy, password rule, sign-in check, lockout, storage
  or logging changes; a difference in any of them caused by the control is a finding. No password value is logged
  or returned (SC-10).
- Known limitation (`trebovaniya.md` §8): a password-manager extension that imitates key presses cannot be told from
  a person. This is not a finding.
- With scripts off the browser's own behaviour stays, Edge's reveal button included (US-069 FR-007). The Story keeps
  this (AC-007).

## Error Handling

The script raises nothing the person sees. A throwing `matches` is handled by FR-003. Any other script failure before
the state is set leaves the control `hidden`, the safe side. There are no server errors to add.

## Non-Functional Requirements

- NFR-077: no new visible word; accessible names come from the translation files as they do today.
- NFR-076, SC-15: same-host static file, no new package. No file besides the script and tests is expected to change.
- No user-visible string is added, so no translation entry changes.

## Out of Scope

- any change to password rules, sign-in, lockout, storage or logging;
- the security headers;
- hiding a shown password when focus leaves the field (the Owner chose option A alone);
- any new visible text;
- filling on a phone in the manual walk;
- telling a password-manager extension that imitates key presses from a person (known limitation).

## Open Decisions

None open. The Owner decided on 2026-11-04: option A; pasting counts as entry. See
`docs/decisions/US-073-open-decisions.md`.

## Traceability

| AC | Requirement | Proof |
|---|---|---|
| AC-001 | FR-002, FR-003, FR-005.1, FR-005.2, FR-006 steps 3 and 5 | Source tests for the closed list and the initial `hidden`; walk steps 3, 4, 7; phone step |
| AC-002 | FR-002, FR-006 step 2 | Source tests; walk step 3; phone step |
| AC-003 | FR-004.2, FR-006 step 1, FR-007.2, FR-011 | Walk steps 1 and 8 (a person only: no test reproduces filling); SECURITY_REVIEW |
| AC-004 | FR-005.1, FR-006 step 5, FR-011 A-2 | Walk steps 2 and 8; SECURITY_REVIEW |
| AC-005 | FR-002, FR-006 step 1, FR-011 A-5 | Walk step 5; SECURITY_REVIEW |
| AC-006 | FR-003, FR-005.3, FR-006 step 1 | Source test that `historyUndo` is not an entry kind; walk step 6 |
| AC-007 | FR-001, FR-008 (FR-008.3 for the scan), FR-009, FR-010 | `PasswordToggleSourceTests` and markup tests of both hosts; existing sign-in tests unchanged |
| AC-008 | FR-011 | Walk recorded in the implementation report |
