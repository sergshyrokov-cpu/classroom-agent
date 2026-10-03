---
artifact_type: open_decisions
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T08:46:05Z
updated_at: 2026-10-03T08:49:18Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-039-choose-ui-language.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-039 Open Decisions

Story-level Open Decisions for US-039 (Choose UI language). Every item is
resolved only by a human; the resolution is written next to the item and nothing
is deleted. OD-001…OD-006 were raised in the Story and resolved by the Owner on
2026-10-03, before activation; they are carried here with their ids and
resolutions unchanged. OD-007 was raised by the Specification and resolved by
the Owner on 2026-10-03 at HUMAN_SPEC_APPROVAL.

| Id | Raised by | Status |
|---|---|---|
| OD-001 Where the language is chosen | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-002 A switcher before sign-in | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-003 A later change of the school default | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-004 One Story or two | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-005 An audit row for the choice | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-006 Dates and numbers on existing screens | the Story | RESOLVED 2026-10-03 (option 1) |
| OD-007 The switcher on the forced password change page | the Specification | RESOLVED 2026-10-03 (option 1) |

## OD-001 Where the language is chosen

Options:

1. **A switcher in the header of every page**, one click, the user stays on the
   same page.
2. A separate "My settings" page.
3. Both.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-002 A switcher before sign-in

`trebovaniya.md` §9 fixes that an anonymous visitor sees the default language.

Options:

1. **No switcher before sign-in**: the requirements stay as they are.
2. Offer one. That needs a new version of `trebovaniya.md` and a place to keep an
   anonymous visitor's choice, such as a browser cookie.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-003 A later change of the school default

An `AppUser` gets a copy of the school default when it is created (US-008,
US-012). If the Owner later changes the default, existing users keep the old
language, even those who never chose one.

Options:

1. **Keep the copy made at creation**: no schema change. A school's default is
   rarely changed.
2. Store "not chosen" and follow the school default until the user chooses,
   which changes the schema.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-004 One Story or two

Options:

1. **One Story for both hosts**: the mechanism and the tests are the same.
2. Two Stories, installation and Control Plane.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-005 An audit row for the choice

SC-11's list of audited actions is closed, and choosing a language is not on it.
It is not an action on school data.

Options:

1. **No audit row**: SC-11 and `AuditAction` do not grow.
2. Audit it, which extends SC-11.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-006 Dates and numbers on existing screens

NFR-073 says formats follow the language. Today only the Control Plane's
installation list formats a date by culture.

Options:

1. **Check every existing screen that shows a date or a number and correct what
   does not follow the language**. Future screens (the journal, reports, exports)
   follow it in their own Stories.
2. Touch nothing but the switcher.

**Resolution:** option 1, decided by the Owner on 2026-10-03.

## OD-007 The switcher on the forced password change page

A Dean whose password is temporary (after account creation or an Admin's reset)
signs in to a session that "may reach the forced change form and nothing else"
(US-012 spec FR-006; `TemporaryPasswordMiddleware` redirects every other path
there, except sign-out and sign-in). The Story says the switcher is on "any page"
of a signed-in user; `trebovaniya.md` does not address this state.

Options:

1. **Offer the switcher on the forced change page too.** The middleware also lets
   the language-choice action through; the return path can only be the forced
   change page, so nothing else becomes reachable. The re-issued session keeps the
   temporary-password claim (spec FR-005). A Dean reads the form — the password
   rules in particular — in their own language before their first real session.
2. Do not offer it there. The forced change page stays as US-012 left it; the
   Dean chooses a language after the change, from any normal page.

Recommended: option 1 — the action touches only the Dean's own language and
opens no other page.

Impact: spec FR-002, FR-005; the temporary-password middleware of US-012; tests
of AC-001 and AC-004 for a temporary-password session.

**Resolution:** option 1, decided by the Owner on 2026-10-03 at
`HUMAN_SPEC_APPROVAL`.
