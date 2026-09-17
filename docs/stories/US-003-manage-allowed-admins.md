---
id: US-003
epic: EPIC-8
title: Manage AllowedAdmin entries
slug: manage-allowed-admins
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v70.
---

# User Story

As the **Owner** of the service

I want to keep, for each `Installation`, the list of email addresses of the
school's domain administrators allowed to be its Admins — adding and revoking
them from the school's page in the Control Plane

So that only the people I approved can sign in to a school's installation and
configure it, and I can take that right away from someone without touching the
school's server.

---

# Business Value

`AllowedAdmin` is the first of the Owner's three control points in operation:
it decides **who** in a school may configure the program (`trebovaniya.md` §9,
"Три точки контроля Владельца"). An Admin is the only way the first user appears
in a new installation (BR-011), so without an entry no Admin can sign in
(US-008), no Google Workspace connection can be saved (US-009) and no Dean
account can be created (US-012).

It is also how a school recovers when its Admins leave: the Owner adds a new
email and the new Admin can sign in at once, without anyone going to the
school's server (`trebovaniya.md` §9, "Несколько Админов на школу").

---

# Scope

**In scope:** the Control Plane (`ClassroomAgent.ControlPlane`) only — the
`AllowedAdmin` entity and its migration; the list of an installation's
`AllowedAdmin` entries on its detail page; adding an entry; revoking an entry
with a confirmation step; the warning while an installation has fewer than two
entries; the audit events for adding and revoking; Ukrainian and English
translation files for every page and message this Story adds.

**Out of scope:** the Control Plane's answer to an installation's Admin login
check, the service channel and anything in `ClassroomAgent.Contracts` (US-008,
US-005); Google OAuth sign-in and just-in-time `AppUser` creation in the
installation (US-008); suspending and resuming an `Installation` (US-004);
editing an entry (there is no editing — `trebovaniya.md` §3, v70); anything in a
school installation.

---

# Acceptance Criteria

## AC-001 The Owner sees an installation's Admins

**Given** the Owner is signed in and an `Installation` exists

**When** they open its detail page (US-002, AC-005)

**Then**:

- the page lists every `AllowedAdmin` entry of that installation — and only of
  that installation — with its email and the date it was added;
- the date is shown in the Owner's UI language format;
- entries are ordered by email;
- with no entries, the page says so;
- the page offers adding an entry and, for each entry, revoking it.

## AC-002 The Owner adds an Admin

**Given** the Owner is signed in, an `Installation` exists, and the email being
entered is not already an entry of that installation

**When** they submit a valid email on the add form

**Then**:

- exactly one `AllowedAdmin` entry is created for that installation;
- the email is stored in lower case;
- the entry records the Owner account's internal id as who added it and the
  UTC time it was added; neither changes afterwards (`trebovaniya.md` §3, v70);
- the Owner lands back on the installation's detail page, which lists the new
  entry;
- there is no limit on the number of entries per installation.

## AC-003 Invalid email is rejected

**Given** the add form of an `Installation`

**When** an email failing the rules of `trebovaniya.md` §3 (v70) is submitted —

- empty, or longer than 254 characters;
- not of the form `name@domain`: no `@`, or more than one;
- a name part empty or longer than 64 characters, with a character other than
  Latin letters, digits, `.`, `_`, `-` and `'`, starting or ending with a dot, or
  with two dots in a row;
- a domain part that is not exactly this installation's domain — a personal
  address, another school's domain or a subdomain of this one

**Then**:

- nothing is created;
- an email in upper or mixed case, including its domain part, is accepted and
  stored in lower case — case is not a validation failure;
- the response names the field and why it failed, in terms safe to display
  (`api-conventions.md` API-6); for a wrong domain it names the domain that is
  expected; the form keeps what the Owner typed;
- the rejected value is never written to a log (SC-10).

## AC-004 An email is listed once per installation

**Given** an `Installation` already has an entry with some email

**When** the Owner adds the same email again, in any letter case

**Then**:

- nothing is created;
- the form says the email is already an Admin of this installation;
- two additions of the same email to the same installation arriving at the same
  time create exactly one entry; the other is refused as a conflict, not a
  server error.

## AC-005 The Owner revokes an Admin after confirming

**Given** an `Installation` has an `AllowedAdmin` entry

**When** the Owner chooses to revoke it

**Then**:

- a confirmation step shows the email and the installation's name and asks the
  Owner to confirm or cancel; nothing changes until they confirm;
- cancelling returns to the detail page with the entry unchanged;
- confirming deletes the entry together with its email (`trebovaniya.md` §5);
  the Owner lands back on the detail page, which no longer lists it;
- the page states that revoking takes away only the right to sign in and
  configure the installation, and does not stop the school — synchronization
  and Deans keep working (BR-022, BR-023);
- revoking the last entry is allowed (`trebovaniya.md` §9, v70);
- the revoked email may be added again later; that creates a new entry with
  new "added by" and "added at" values.

## AC-006 Entries of a suspended installation can be managed

**Given** an `Installation` with status "suspended"

**When** the Owner adds or revokes one of its entries

**Then**:

- the action succeeds exactly as for an active installation
  (`trebovaniya.md` §3, v70);
- nothing about the `Installation` itself changes.

## AC-007 Fewer than two Admins is warned about

**Given** an `Installation` with zero or one `AllowedAdmin` entry

**When** the Owner opens its detail page

**Then**:

- the page shows a warning that onboarding requires at least two Admins so that
  one person leaving never stops the school's configuration (BR-013);
- with two or more entries there is no warning;
- the warning never blocks adding or revoking.

## AC-008 Unknown targets answer 404

**Given** the Control Plane is running

**When** the add form, the revoke confirmation or a revoke submission is
requested for an installation id that does not exist or is not a UUID, or for an
entry that does not exist or belongs to a different installation

**Then**:

- the response is `404` with the translated error page;
- nothing is created, deleted or audited;
- a revoke confirmed twice — the entry already deleted — answers `404` and
  writes no second audit row.

## AC-009 Adding and revoking are audited

**Given** the Control Plane `AuditEvent` table (SC-11)

**When** an `AllowedAdmin` entry is added or revoked

**Then**:

- one row is written per completed action, with the Owner account's internal id
  and role as actor, the action (added / revoked), the target type
  `AllowedAdmin` and the entry's internal id, outcome "succeeded", UTC time and
  request id (`trebovaniya.md` §5);
- the row carries no email and no installation name or domain — internal
  identifiers only (SC-11); a revoked entry's id stays in its rows after the
  entry is deleted;
- a submission refused by validation, uniqueness or `404` (AC-003, AC-004,
  AC-008), and a cancelled revocation, change nothing and write no row;
- the row is written in the same transaction as the change, so there is never a
  change without its row or a row without its change;
- these rows are kept indefinitely and cannot be updated or deleted (AC-008 of
  US-001).

## AC-010 Only the Owner reaches these pages

**Given** the Control Plane is running

**When** any endpoint this Story adds is requested without a signed-in Owner

**Then**:

- the request is refused — a page request is sent to the sign-in page, nothing
  is shown or changed;
- none of these endpoints is on the SC-4 anonymous list, and the endpoint
  enumeration test of US-001 covers them (TC-5);
- while no Owner account exists, the first-run setup gate of US-001 still
  applies to them.

## AC-011 State-changing forms are protected

**Given** the Control Plane is running

**When** the add form or the revoke confirmation is submitted

**Then**:

- each is a POST and requires a valid antiforgery token; without one it is
  refused with `400` and the translated "page expired" error page, and nothing
  changes (SC-4, API-4);
- no action this Story adds changes anything on GET — opening the revoke
  confirmation deletes nothing.

## AC-012 Pages are translated

**Given** every page, label and message this Story adds

**When** it is displayed

**Then**:

- it is in the Owner's UI language, Ukrainian by default;
- no user-visible string is hard-coded: each comes from
  `ClassroomAgent.ControlPlane.Localization` and exists in both Ukrainian and
  English (NFR-073);
- emails and the installation's name and domain are shown as stored and never
  translated.

---

# Open Decisions

None. The questions this Story raised — whether the email must be in the
installation's domain, its format and letter case, uniqueness, re-adding a
revoked email, a limit on entries, managing entries of a suspended installation,
confirming a revocation, and what happens with fewer than two Admins — were
decided by the Owner and recorded in `trebovaniya.md` v70 (§3, §4, §9).

---

# Notes

- The Control Plane is one project with internal boundaries (`architecture.md`
  AD-3): the domain check, uniqueness and transactions live in
  `ControlPlane.Services`; `Controllers` hold no business rules and never touch
  `DbContext`. Controllers and views see DTOs only (AD-8).
- Uniqueness of (installation, email) must hold under concurrency, so it is
  enforced by the database (a unique index), not only by a check before insert
  (AC-004).
- The domain check compares with the `Installation` domain already stored in
  lower case (US-002); an `Installation` domain never changes (BR-021), so an
  entry never ends up in a foreign domain.
- Unlike `Installation`, an `AllowedAdmin` row is deleted on revocation — the
  email must not outlive the entry (`trebovaniya.md` §5). An `Installation` is
  never deleted, so no cascade from it is needed.
- Nothing in this Story answers an installation: the Admin login check reads
  these entries in US-008, comparing the lower-cased login email.
- US-004 (suspend) is not delivered yet, so AC-006 is tested with an
  installation whose status is set to "suspended" directly in the test database.
- Log lines written by these actions carry internal identifiers only — never an
  email (SC-10, DC-10; `non-functional-requirements.md`).
- Admins are school staff; a person who is also the Owner may be one only as the
  school's employee, through an entry like anyone else (BR-013). Nothing in this
  Story treats the Owner's own email specially.
