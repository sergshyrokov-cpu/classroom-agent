---
id: US-067
epic: EPIC-6
title: An Admin changes a Dean's login after the work email was renamed in Google
slug: admin-changes-dean-login
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-28 from trebovaniya.md v104 (`578281e`,
# §2 «Смена логина», §2 matrix, §5 audit; derived BR-014, SC-2, SC-11 in
# `154a031`). Item 5 of the v104 agenda.
---

# User Story

As an **Admin**

I want to change a Dean's login to their new work email when it was renamed in
Google

So that the Dean keeps one account — the same password, history and audit trail
— instead of my disabling it and creating a new one that splits their history in
two.

---

# Business Value

A Dean's login is their work email (BR-014, v55). When the school renames that
email in Google (a changed surname, for example), the program does not notice: the
old address keeps signing in and the new one is refused. Today the only way out is
a new account, which splits the audit history, leaves report templates authored by
the old account, and starts a new retention clock. v104 lets the Admin change the
login of the same account.

---

# Scope

**In scope:**

- on the Dean-accounts page (`/settings/deans`, Admin only), a per-account action
  "change login": the Admin enters the new address and saves;
- the new address is checked by the **same rules as at creation**
  (`CreateDeanAccountUseCase`): a well-formed email, in exactly the school's
  domain (read server-side, never from the form), not taken by any other account
  — Dean or Admin; it is stored lower-cased (BR-079);
- on success the account keeps its id, password, temporary-password flag, lockout
  state, disabled state, language, last sign-in and creation time; only the login
  changes, and the security stamp is rotated (§2 v104, SC-2);
- the Dean signs in with the new address and the old password; the old address no
  longer signs in (generic refusal, as for an unknown login);
- an open session of that Dean stops working on the next request (stamp rotated),
  like disabling (US-064);
- the old address becomes free: an Admin may later create another account with it;
- audit: an Admin changing a Dean's login — actor the Admin, object the account's
  id; neither the old nor the new address (§5 v104, SC-11);
- read-only mode: refused in `Application` (BR-026, AD-6), like the other account
  actions;
- texts in Ukrainian and English (NFR-073).

**Out of scope:**

- an Admin's own login — an Admin's email is their Google account and their
  `AllowedAdmin` entry; a renamed Admin is a new `AllowedAdmin` entry made by the
  Owner (BR-010);
- noticing the rename in Google automatically — v104: the program does not watch
  Google for it;
- changing the password together with the login — v104: the password is not
  changed (an Admin can still reset it separately, as today);
- telling the Dean by mail — the program sends no mail.

---

# Acceptance Criteria

## AC-001 The login changes, the account stays

**Given** a Dean account `olena.koval@school.example` with a password the Dean set,
report templates the Dean authored and audit events of the Dean's sign-ins

**When** the Admin changes its login to `olena.shevchenko@school.example`

**Then** the account has the new login and the same id; the Dean's templates still
show the Dean as author, under the new address; earlier audit events still refer to
the same account; the page confirms the change.

## AC-002 Sign-in with the new address and the same password

**Given** the situation after AC-001

**When** the Dean signs in with `olena.shevchenko@school.example` and the old
password

**Then** the sign-in succeeds; with `olena.koval@school.example` it is refused with
the generic refusal message.

## AC-003 An open session ends

**Given** the Dean signed in under the old login, with the session cookie captured

**When** the Admin changes the login and the cookie is replayed on a page

**Then** the Dean is sent to the sign-in page.

## AC-004 The same rules as at creation

**Given** an Admin on the Dean-accounts page

**When** the new address is empty, malformed, outside the school's domain, or in a
subdomain of it

**Then** nothing changes, the field shows the same validation message as on the
creation form, and the rejected value is not written to the log (SC-10).

## AC-005 An address taken by another account

**Given** another Dean account, or an Admin account, with the address
`ivan.petrenko@school.example`

**When** the Admin changes a Dean's login to that address (in any letter case)

**Then** nothing changes and the page says the address is already in use.

## AC-006 Everything else is untouched

**Given** a Dean account with a temporary password, a sign-in lockout in force and
the disabled flag set

**When** the Admin changes its login

**Then** the password is still temporary, the lockout is still in force, the account
is still disabled, and the retention clock (last sign-in or creation) is unchanged.

## AC-007 Audited without addresses

**Given** an Admin changing a Dean's login

**When** the change is saved

**Then** one audit event "Dean login changed" is written with the Admin as actor
and the account's id as object, and it contains neither address.

## AC-008 Refused in read-only mode

**Given** the installation in read-only mode

**When** the Admin changes a Dean's login

**Then** the request is refused in the Application layer and nothing changes.

## AC-009 Only the Admin

**Given** a Dean signed in

**When** the Dean posts a login change for any account (their own included)

**Then** the request is forbidden (TC-5); an anonymous request is sent to the
sign-in page.

## AC-010 The old address is free again

**Given** the situation after AC-001

**When** the Admin creates a new Dean account with `olena.koval@school.example`

**Then** it is created.

---

# Open Decisions

Resolved by the Owner on 2026-10-28 before activation.

## OD-001 Changing the login of a disabled Dean

- **a) (recommended)** — allowed, and the account stays disabled (AC-006): the
  Admin can bring the login up to date before re-enabling, and the change does not
  quietly let anyone back in.
- b) — refused while disabled: the Admin re-enables first, which briefly lets the
  Dean in under the old address.

**Resolved 2026-10-28 by the Owner: a)** («a, a»).

## OD-002 Entering the same address the account already has

- **a) (recommended)** — nothing is written (no audit event, no stamp rotation) and
  the page says the login is unchanged — like an Owner status change that changes
  nothing (`trebovaniya.md` §9 v71).
- b) — treated as a change: audited, and the Dean's open session ends.

**Resolved 2026-10-28 by the Owner: a)** («a, a»).

---

# Notes

- **Code today:** `AppUser` (`src/ClassroomAgent.Domain/Entities/AppUser.cs`) holds
  `Email` and `NormalizedEmail`, both lower-cased, with no method that changes them —
  one is added here, rotating the stamp like `Disable` (~132). Address checks:
  `CreateDeanAccountUseCase.cs` (~57 school domain from `LegitimacyState`, ~111
  format, ~130 exact domain), uniqueness by the unique index on the normalized
  email (the creation path already maps the violation). Page:
  `Views/DeanAccounts/Index.cshtml` — per-row forms post to
  `/settings/deans/{id}/state` and `/password`; the new action follows them.
- **Password rule against the login** (§2 v64: the password does not contain the
  local part of the login, 4+ characters) is checked when a password is set; v104
  keeps the password, so the existing one is not re-checked against the new login.
  The next password the Dean or an Admin sets is checked against the new login.
- **Report template author:** templates store `AuthorId`, so they show the current
  address of the account — confirm in TEST_WRITING (AC-001).
- Expected stages: SPECIFICATION, API_DESIGN (one new POST, Razor form),
  DB_DESIGN likely NOT_APPLICABLE (no column changes; check the audit event type's
  closed list and its migration if one is needed), TEST_WRITING, IMPLEMENTATION,
  verification, security review.
- No new NuGet package, no new requirements version.
