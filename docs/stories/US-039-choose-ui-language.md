---
id: US-039
epic: EPIC-6
title: Choose UI language (Admin, Dean and Owner)
slug: choose-ui-language
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
---

# User Story

As an **Admin or Dean** of a school's installation, and as the **Owner** in the
Control Plane

I want to switch the interface between Ukrainian and English myself, from any
page, and have my choice remembered on my account

So that I work in the language I read best, on any device, without asking the
Owner to change the school's setting for everyone.

---

# Business Value

`trebovaniya.md` §5 (v51) makes the interface bilingual. The school's default is
an installation setting the Owner sets at deployment, and **each Admin and Dean
may choose their own language**, stored on their account and followed on every
device. The Control Plane is likewise bilingual, Ukrainian by default, with the
Owner's choice stored on the Owner account (NFR-073).

Almost everything this needs already exists:

- both translation files exist on both hosts;
- `AppUser` and the Control Plane `Owner` each have a stored language;
- each host already renders a signed-in user's pages in that language
  (`AccountCultureProvider`, `OwnerAccountCultureProvider`).

**What is missing is the choice itself.** Nothing lets a user change the stored
value, so today every Admin and Dean is fixed to the school default they were
created with, and the Owner to Ukrainian.

---

# Scope

**In scope:**

- a **language switcher in the header of every page** for a signed-in user —
  «УКР / ENG», one click, the user stays on the same page (OD-001):
  - in the installation for the Admin and the Dean;
  - in the Control Plane for the Owner (OD-004);
- storing the choice on the user's account: `AppUser` in the installation,
  `Owner` in the Control Plane. The choice follows the user to any device and
  any later sign-in (§5);
- the choice taking effect **immediately**, from the next page the user sees,
  without signing out and in again;
- choosing a language **in read-only mode**: it is on the BR-026 closed list of
  permitted service writes (`trebovaniya.md` §2, v51), so it must work while
  everything else is blocked;
- choosing is a state change, so it is a **POST with antiforgery** and never a
  GET (API-4, SC-4; §9 names "выбор языка" explicitly);
- checking that the **existing screens' dates and numbers follow the chosen
  language**, and correcting any that do not (NFR-073, OD-006).

**Out of scope:**

- a switcher before sign-in. Anonymous pages stay in the default language: the
  school's in the installation, Ukrainian in the Control Plane (§9; OD-002);
- changing the school's default language. That is an installation setting the
  Owner sets at deployment, not a screen;
- a third language. A new language arrives as translation files, without code
  changes (§5), and v1 has none;
- following a later change of the school default for users who never chose
  (OD-003);
- an audit row for the choice (OD-005);
- the date and number formats of screens that do not exist yet (journal,
  reports, exports): each of those Stories makes its own screens follow the
  language;
- Teacher and Student accounts: none exist in v1 (Epic 7).

---

# Acceptance Criteria

## AC-001 A signed-in user can switch the language from any page

**Given** a signed-in Admin, Dean or Owner on any page of their host

**When** they choose the other language in the header switcher

**Then**:

- the page they were on is shown again, in the chosen language;
- every following page is in that language, with no sign-out and no sign-in;
- the switcher shows which language is current.

## AC-002 The choice is stored on the account and follows the user

**Given** a user who chose a language

**When** they sign out and sign in again, or sign in from another browser

**Then**:

- their pages are in the language they chose, not the school default;
- the choice was stored on their own account (`AppUser` or `Owner`) and on no
  other account;
- another user's language is unchanged.

## AC-003 Choosing a language works in read-only mode

**Given** the installation is in read-only mode, for any of its causes (BR-025)

**When** an Admin or Dean chooses a language

**Then**:

- the choice is stored and takes effect;
- it is the BR-026 permitted write and nothing else: no other write path is
  opened by it;
- this is proven in the Application layer, not as UI state (AD-6, TC-5).

## AC-004 Only a signed-in user can choose, and only for themselves

**Given** each host

**When** the language-choice action is called

**Then**:

- an anonymous request is refused, and SC-4's closed list of anonymous endpoints
  gains nothing;
- a GET changes nothing;
- a POST without a valid antiforgery token is refused (SC-4, API-7);
- the request carries no user identifier: the account changed is always the
  signed-in user's own;
- the allowed and the refused cases are tested on both hosts (TC-5).

## AC-005 Only the supported languages can be chosen

**Given** the language-choice action

**When** it receives anything other than Ukrainian or English (empty, unknown,
oversized or malformed)

**Then**:

- it is refused and nothing is stored;
- the rejected value is not written to any log (SC-10).

## AC-006 Anonymous pages keep the default language

**Given** a visitor who is not signed in

**When** they open a page that does not require sign-in (the sign-in page, the
error page)

**Then**:

- the page is in the default language: the school's in the installation,
  Ukrainian in the Control Plane (§9);
- no switcher is offered there (OD-002).

## AC-007 Dates and numbers follow the chosen language

**Given** an existing screen that shows a date, a time or a number

**When** it is viewed in each of the two languages

**Then**:

- the format follows the chosen language (NFR-073);
- every such screen found is listed in the implementation report, together with
  any correction made.

## AC-008 Every new string is translated

**Given** the switcher and any message the Story adds

**When** they are shown in either language

**Then**:

- every new user-visible string comes from both translation files on its host,
  with no hard-coded text (NFR-073);
- the language names are shown as each language names itself.

---

# Open Decisions

All six were resolved by the Owner on 2026-10-03, before activation.

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

---

# Notes

- Existing code this Story builds on:
  - `AppUser.UiLanguage` and the Control Plane `Owner.UiLanguage`, each stored as
    `uk` / `en`;
  - `UiLanguage` in `Domain/Enums`;
  - `AccountCultureProvider` and `OwnerAccountCultureProvider`, which read the
    language from the sign-in claim;
  - `InstallationSettings.DefaultUiLanguage`;
  - the two `SharedResource` translation files on each host.
- Because each host reads the language from a **claim in the sign-in cookie**,
  making a new choice take effect at once (AC-001) means the cookie must be
  refreshed, not just the database row updated. How to do that is
  `SPECIFICATION`'s to settle. The refresh must keep SC-2's cookie attributes and
  must not weaken the security-stamp check introduced for sign-out.
- BR-026's permitted list already names this write, so read-only mode needs **no**
  requirements change. On the installation side, `PermittedServiceWrites` gains
  the language choice, and that is the only growth.
- Dependencies (catalog): US-001, US-007, US-008, US-012. All are done.
