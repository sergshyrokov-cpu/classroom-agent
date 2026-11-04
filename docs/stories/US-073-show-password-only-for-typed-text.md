---
id: US-073
epic: EPIC-6
title: The eye shows only a password the person entered, never one the browser filled in
slug: show-password-only-for-typed-text
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-04 from the Owner's decision (option A) on finding
# US-069/SECURITY_REVIEW/INFO-1. Aligned with trebovaniya.md v110 (section 8, NFR-077, SC-2).
# Before the pilot.
---

# User Story

As a **school** whose Deans and Admins may work on shared computers

I want the show-password eye to work only for a password the person entered themselves

So that nobody at the sign-in page of a shared computer can read a password the browser saved.

---

# Business Value

Since US-069 the eye reveals a password the browser filled in from its saved ones. On a school computer
with a saved Dean password, one press would show it to whoever sits down next. Developer tools can do the
same, but the eye makes it easy for anyone. People who type a password keep the benefit they have today.

---

# Scope

**In scope**, on all 9 password fields in 6 views of both hosts, the ones US-069 lists:

1. **The control appears only after the person enters text** into an empty field by typing or pasting. An
   empty field has no control. Until then the control stays hidden and cannot be reached by keyboard.
2. **A value the browser filled in has no control**, whether it arrived when the page loaded or later.
3. **Editing a filled value does not bring it back**: neither adding, deleting nor overtyping characters.
   Only an empty field followed by new entry does.
4. **The browser fills in after the person typed:** the field goes back to hidden and the control disappears.
5. **Undo must not reveal.** A filled value that returns by undo (Ctrl+Z after clearing the field) or by any
   route other than typing or pasting counts as filled, and the field goes back to hidden.
6. **In doubt, no control.** When the script cannot tell entry from filling, the control stays hidden.
7. **Removing the text** (the field becomes empty) hides the field again and removes the control.

**Out of scope:** any change to password rules, sign-in, lockout, storage or logging; the security headers;
hiding a shown password when focus leaves the field (the Owner chose option A alone); any new visible text.
What US-069 did for a shown field stays: hidden on submit and on leaving the page, no spell check.

---

# Acceptance Criteria

## AC-001 The control appears after entry

**Given** an empty password field, with scripts on

**When** the person types or pastes text

**Then** the control appears and works as in US-069. Before that the field has no visible or
keyboard-reachable control.

## AC-002 The control goes when the field is emptied

**Given** a field with typed text, shown or hidden

**When** the person removes all the text

**Then** the field is hidden and has no control. Typing again brings the control back.

## AC-003 A filled value is never shown

**Given** a password the browser saved, filled in at page load or from its suggestion list, in Edge and in
Chrome

**When** the page is used

**Then** there is no control, and the value cannot be shown by any press. Proved by a person (AC-008).

## AC-004 Editing a filled value does not reveal it

**Given** a filled field

**When** the person adds, deletes or overtypes characters without first emptying the field

**Then** there is still no control. After emptying the field and entering text, the control appears.

## AC-005 Filling after typing hides

**Given** a field with typed text, shown as text

**When** the browser fills in a saved password

**Then** the field returns to hidden and the control disappears.

## AC-006 Undo does not reveal

**Given** a filled field the person emptied, and possibly typed into again

**When** undo brings the filled value back

**Then** the field is hidden and there is no control.

## AC-007 Both hosts, nothing else changed

**Given** the installation and the Control Plane

**Then** their scripts follow the same rules; without scripts there is no control and the field works as
before; the script still sends nothing, stores nothing and does not change the value; US-069's behaviour for
a shown field (hidden on submit and on leaving the page, no spell check, accessible names) is unchanged.
Tests read the script of both hosts, as `PasswordToggleSourceTests` does today; that class changes where it
fixes the old rule of showing the control at load.

## AC-008 A person checks it with a really saved password

**Given** a password really saved in the browser, in Edge and in Chrome, in both hosts, on a computer, and
typing on a phone

**When** the person walks AC-001 to AC-006

**Then** each behaves as stated, and the walk is recorded in the implementation report. No automated test can
reproduce a browser's filling. The phone step proves that an on-screen keyboard counts as entry, because such
keyboards report their input differently.

---

# Open Decisions

None open. The Owner decided on 2026-11-04: option A; pasting counts as entry.

---

# Notes

- **Known limitation** (`trebovaniya.md` section 8): a password-manager extension that imitates key presses
  cannot be told from a person.
- **Hints for the Specification, not requirements:** browsers may fire `input` on filling and may fill late;
  `:autofill` and the `inputType` of `beforeinput` and `input` are the candidates for telling entry from
  filling; composition input from phone keyboards must count as entry. Prove the approach in the real
  browsers early, since the tests cannot.
- **SECURITY_REVIEW** reads the script as it did for US-069: a control revealing a filled value is a finding
  (SC-2). Closes the concern of the deferred finding `US-069/SECURITY_REVIEW/INFO-1`.
- The look-check stand can serve the manual walk: see the project's note on the stand.
- No new endpoint, schema or text is expected: API and database design should be empty deltas.
