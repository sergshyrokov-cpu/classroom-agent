---
id: US-002
epic: EPIC-8
title: Register an Installation
slug: register-installation
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v69.
---

# User Story

As the **Owner** of the service

I want to register each school as an `Installation` in the Control Plane — its
name, Google Workspace domain and service-account client ID — and to see and
correct those records

So that a school's installation can be deployed with its own identifier and can
only ever work with the domain I recorded for it.

---

# Business Value

The `Installation` is the source of truth for which schools exist and with which
domain each may work (`trebovaniya.md` §3, §9; BR-020). Without it nothing
downstream can happen: there is nothing to attach `AllowedAdmin` entries to
(US-003), nothing to suspend (US-004), and an installation has no identifier to
present in the legitimacy check (US-005) — so no Admin can sign in and no school
can connect to Google.

It is also the Owner's guard against the program spreading: an installation can
only work with a domain the Owner registered (`trebovaniya.md` §9, "Три точки
контроля Владельца"), and one domain belongs to exactly one record.

---

# Scope

**In scope:** the Control Plane (`ClassroomAgent.ControlPlane`) only — the
`Installation` entity and its migration; the list of installations; creating an
`Installation`; the detail page showing its identifier; correcting its name;
changing its service-account client ID; the audit events for these three
actions; Ukrainian and English translation files for every page and message this
Story adds.

**Out of scope:** `AllowedAdmin` entries (US-003); suspending and resuming
(US-004); the legitimacy check, `InstanceLicenseCheck` and anything in
`ClassroomAgent.Contracts` (US-005); the status push (US-006); anything in a
school installation, including reading the installation id from its
configuration (US-005); deleting an `Installation` (none in the first version,
`trebovaniya.md` §3, v69); changing a domain (never — BR-021).

---

# Acceptance Criteria

## AC-001 The Owner sees the list of installations

**Given** the Owner is signed in to the Control Plane

**When** they open the installations list

**Then**:

- every `Installation` is listed with its name, domain, status and creation date;
- the status is shown as a translated label ("active" / "suspended");
- the creation date is shown in the Owner's UI language format;
- with no installations, the page says so and offers to register one;
- the list is reachable from the Control Plane home page.

## AC-002 The Owner registers an installation

**Given** the Owner is signed in and no `Installation` has the domain or client ID
being entered

**When** they submit a valid name, domain and client ID on the registration form

**Then**:

- exactly one `Installation` is created with those values;
- the Control Plane generates its identifier — a random UUID — which never
  changes afterwards (`trebovaniya.md` §3, v69);
- the domain is stored in lower case;
- the status is "active"; the form offers no status choice (`trebovaniya.md` §3,
  v69);
- the creation date is recorded in UTC;
- the Owner lands on the new installation's detail page (AC-005).

## AC-003 Invalid input is rejected

**Given** the registration form, the name correction form or the client ID form

**When** a value failing the rules of `trebovaniya.md` §3 (v69) is submitted —

- a name that is empty, longer than 200 characters, or starts or ends with
  whitespace;
- a domain shorter than 3 or longer than 253 characters, with a character other
  than Latin letters, digits, `-` and `.`, with no dot, or containing a scheme
  (`https://`), a path or `@`; a Cyrillic (IDN) domain is refused as well;
- a client ID with a character other than a digit, or shorter than 10 or longer
  than 32 digits

**Then**:

- nothing is created or changed;
- a domain in upper or mixed case is accepted and stored in lower case — case is
  not a validation failure;
- the response names which field failed and why, in terms safe to display
  (`api-conventions.md` API-6), and the form keeps what the Owner typed;
- the rejected values are never written to a log (SC-10).

## AC-004 A domain and a client ID belong to one installation

**Given** an `Installation` already exists — active or suspended

**When** the Owner registers another one with the same domain in any letter case,
or the same client ID, or changes a client ID to one another installation has

**Then**:

- nothing is created or changed;
- the form names the field and says the value is already registered to another
  installation, and for a domain explains that a school returning to its former
  domain is resumed, not registered again (BR-021, v69);
- two registrations with the same domain or the same client ID arriving at the
  same time create exactly one `Installation`; the other is refused as a
  conflict, not a server error.

## AC-005 The detail page shows the installation id

**Given** an `Installation` exists

**When** the Owner opens its detail page

**Then**:

- it shows the identifier, name, domain, status, creation date and client ID;
- the identifier can be copied in one action and the page explains that it goes
  into the installation's configuration at deployment (`trebovaniya.md` §5, v69;
  `deployment-conventions.md` DC-2, DC-3);
- the domain has no edit control and there is no delete control
  (`trebovaniya.md` §3, v69);
- a detail page for an identifier that does not exist, or is not a UUID, answers
  `404` with the translated error page.

## AC-006 The Owner corrects the name

**Given** an `Installation` exists

**When** the Owner submits a valid new name

**Then**:

- only the name changes; identifier, domain, status, creation date and client ID
  stay as they were;
- names are not unique: a name already used by another installation is accepted
  (`trebovaniya.md` §3, v69);
- submitting the name unchanged writes nothing and no audit row.

## AC-007 The Owner changes the client ID

**Given** an `Installation` exists and its service account has been recreated
(`trebovaniya.md` §9, "Ротация ключей")

**When** the Owner submits a valid new client ID not registered to any other
installation

**Then**:

- only the client ID changes;
- the page states that the school's super-admin must authorize domain-wide
  delegation for the new client ID, and that the installation picks it up no
  later than its next legitimacy check (`trebovaniya.md` §9, v43, v54);
- submitting the client ID unchanged writes nothing and no audit row.

## AC-008 Registration and changes are audited

**Given** the Control Plane `AuditEvent` table (SC-11)

**When** an `Installation` is created, its name is corrected or its client ID is
changed

**Then**:

- one row is written per completed action, with the Owner account's internal id
  and role as actor, the action, the target type `Installation` and its internal
  id, outcome "succeeded", UTC time and request id (`trebovaniya.md` §5, v43, v69);
- the row carries no name, domain or client ID — neither old nor new values
  (SC-11: internal identifiers only);
- a submission refused by validation or uniqueness (AC-003, AC-004) changes
  nothing and writes no row: `trebovaniya.md` §5 lists these as actions, and a
  refused submission is not one — unlike sign-in, no refusal category is defined
  for them;
- the row is written in the same transaction as the change, so there is never a
  change without its row or a row without its change;
- these rows are kept indefinitely and cannot be updated or deleted (AC-008 of
  US-001).

## AC-009 Only the Owner reaches these pages

**Given** the Control Plane is running

**When** any endpoint this Story adds is requested without a signed-in Owner

**Then**:

- the request is refused — a page request is sent to the sign-in page, nothing
  is shown or changed;
- none of these endpoints is on the SC-4 anonymous list, and the endpoint
  enumeration test of US-001 covers them (TC-5);
- while no Owner account exists, the first-run setup gate of US-001 still applies
  to them.

## AC-010 State-changing forms are protected

**Given** the Control Plane is running

**When** the registration, name correction or client ID form is submitted

**Then**:

- each is a POST and requires a valid antiforgery token; without one it is
  refused with `400` and the translated "page expired" error page, and nothing
  changes (SC-4, API-4);
- no action this Story adds changes anything on GET.

## AC-011 Pages are translated

**Given** every page, label and message this Story adds

**When** it is displayed

**Then**:

- it is in the Owner's UI language, Ukrainian by default;
- no user-visible string is hard-coded: each comes from
  `ClassroomAgent.ControlPlane.Localization` and exists in both Ukrainian and
  English (NFR-073);
- the school name, domain and client ID the Owner entered are shown as entered
  and never translated.

---

# Open Decisions

None. The questions this Story raised — the installation identifier and how it
reaches the installation, the status at creation, uniqueness of the domain and
the client ID, value formats, correcting the name and auditing it, and deleting
an `Installation` — were decided by the Owner and recorded in `trebovaniya.md`
v69 (§3, §4, §5, §9).

---

# Notes

- The Control Plane is one project with internal boundaries (`architecture.md`
  AD-3): the registration logic, uniqueness checks and transactions live in
  `ControlPlane.Services`; `Controllers` hold no business rules and never touch
  `DbContext`. Controllers and views see DTOs only (AD-8).
- Uniqueness of the domain and the client ID must hold under concurrency, so it is
  enforced by the database (unique indexes), not only by a check before insert
  (AC-004).
- The installation id is shown to the Owner, but nothing in this Story sends it
  anywhere: the service channel, `ClassroomAgent.Contracts` and the installation's
  configuration reader belong to US-005.
- The Control Plane has no path to teaching data (SC-12): the `Installation`
  holds only what `trebovaniya.md` §3 lists, and the Control Plane must not
  reference `ClassroomAgent.Domain` (`package-map.md`).
- Log lines written by these actions carry internal identifiers only — no name,
  domain or client ID (SC-10, DC-10).
- The school's name and domain are not personal data, but the audit rule is the
  same for every Control Plane row: internal ids only.
- Every school must have at least two Admins (BR-013); the page that manages them
  is US-003, so a freshly registered installation has none yet — that is expected.
