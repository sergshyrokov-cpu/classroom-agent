---
id: US-004
epic: EPIC-8
title: Suspend and resume an Installation
slug: suspend-resume-installation
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v71.
---

# User Story

As the **Owner** of the service

I want to suspend a school's `Installation` from its page in the Control Plane
and resume it later, each after confirming

So that I can stop a school's work — for example when the cooperation ends —
without going to the school's server, and return it to normal work the same way.

---

# Business Value

The `Installation` status is the third of the Owner's control points in
operation: it decides **whether the school works at all** (`trebovaniya.md` §9,
"Три точки контроля Владельца"). It is the only lever that stops a school —
revoking an Admin does not (BR-022, BR-023). A suspended school goes to
read-only mode (BR-025); resuming it is also how a school returning to its former
domain gets back to work, since a domain can never get a second `Installation`
(BR-021).

This Story gives the Owner the lever itself. Delivering the status to the
school — the legitimacy check (US-005) and the push (US-006) — builds on it.

---

# Scope

**In scope:** the Control Plane (`ClassroomAgent.ControlPlane`) only — the
suspend and resume actions on an installation's detail page; a confirmation step
for each; the audit events for suspending and resuming; Ukrainian and English
translation files for every page and message this Story adds.

**Out of scope:** the legitimacy check, `InstanceLicenseCheck`, `LegitimacyState`
and anything in `ClassroomAgent.Contracts` (US-005); the push to the installation
on a status change (US-006); read-only mode enforcement in an installation
(US-007); a reason for the change or the time of the last status change on the
`Installation` (`trebovaniya.md` §3, v71); deleting an `Installation` (there is
none in the first version); anything in a school installation.

---

# Acceptance Criteria

## AC-001 The detail page offers the action the status allows

**Given** the Owner is signed in and an `Installation` exists

**When** they open its detail page (US-002, AC-005)

**Then**:

- an active installation offers suspending it and does not offer resuming;
- a suspended installation offers resuming it and does not offer suspending;
- the page shows the current status only — no reason and no time of the last
  change (`trebovaniya.md` §3, v71).

## AC-002 Suspending asks for confirmation

**Given** an active `Installation`

**When** the Owner chooses to suspend it

**Then**:

- a confirmation step shows the installation's name and domain and states what
  suspending does: the school goes to read-only mode — viewing and export keep
  working, synchronization and configuration stop; no data is deleted; the
  Admins in the list stay (`trebovaniya.md` §9, v71);
- nothing changes until the Owner confirms;
- cancelling returns to the detail page with the status unchanged.

## AC-003 The Owner suspends an Installation

**Given** the suspend confirmation of an active `Installation`

**When** the Owner confirms

**Then**:

- the installation's status becomes "suspended";
- its identifier, name, domain, creation date and client ID do not change, and
  its `AllowedAdmin` entries stay as they were;
- the Owner lands back on the detail page, which shows the status "suspended"
  and offers resuming;
- the installation list (US-002, AC-001) shows the new status.

## AC-004 Resuming asks for confirmation

**Given** a suspended `Installation`

**When** the Owner chooses to resume it

**Then**:

- a confirmation step shows the installation's name and domain and states that
  the school returns to normal work (`trebovaniya.md` §9, v71);
- nothing changes until the Owner confirms;
- cancelling returns to the detail page with the status unchanged.

## AC-005 The Owner resumes an Installation

**Given** the resume confirmation of a suspended `Installation`

**When** the Owner confirms

**Then**:

- the installation's status becomes "active";
- its identifier, name, domain, creation date and client ID do not change, and
  its `AllowedAdmin` entries stay as they were;
- the Owner lands back on the detail page, which shows the status "active" and
  offers suspending;
- an installation may be suspended and resumed any number of times.

## AC-006 An action that changes nothing writes nothing

**Given** an `Installation` already in the status an action would set — for
example suspended in another browser tab while this tab still offers suspending

**When** the Owner confirms suspending a suspended installation, or resuming an
active one — including the same confirmation submitted twice

**Then**:

- nothing is changed and no audit row is written (`trebovaniya.md` §9, v71);
- the Owner lands back on the detail page with a message stating the
  installation's current status;
- it is not a server error;
- two opposite confirmations arriving at the same time leave the installation in
  one of the two statuses with exactly one audit row per status change actually
  made.

## AC-007 Unknown targets answer 404

**Given** the Control Plane is running

**When** a suspend or resume confirmation, or a suspend or resume submission, is
requested for an installation id that does not exist or is not a UUID

**Then**:

- the response is `404` with the translated error page;
- nothing is changed or audited.

## AC-008 Suspending and resuming are audited

**Given** the Control Plane `AuditEvent` table (SC-11)

**When** an `Installation` is suspended or resumed

**Then**:

- one row is written per status change, with the Owner account's internal id
  and role as actor, the action (suspended / resumed), the target type
  `Installation` and its internal id, outcome "succeeded", UTC time and request
  id (`trebovaniya.md` §5);
- the row carries no installation name or domain — internal identifiers only
  (SC-11);
- a cancelled confirmation, a `404` (AC-007) and an action that changes nothing
  (AC-006) write no row;
- the row is written in the same transaction as the status change, so there is
  never a change without its row or a row without its change;
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
- while no Owner account exists, the first-run setup gate of US-001 still
  applies to them.

## AC-010 State-changing forms are protected

**Given** the Control Plane is running

**When** a suspend or resume confirmation is submitted

**Then**:

- each is a POST and requires a valid antiforgery token; without one it is
  refused with `400` and the translated "page expired" error page, and nothing
  changes (SC-4, API-4);
- no action this Story adds changes anything on GET — opening a confirmation
  changes no status.

## AC-011 Pages are translated

**Given** every page, label and message this Story adds

**When** it is displayed

**Then**:

- it is in the Owner's UI language, Ukrainian by default;
- no user-visible string is hard-coded: each comes from
  `ClassroomAgent.ControlPlane.Localization` and exists in both Ukrainian and
  English (NFR-073);
- the installation's name and domain are shown as stored and never translated.

---

# Open Decisions

None. The questions this Story raised — confirming a suspension and a
resumption, a reason for the change, showing when the status last changed, an
action that would not change the status, what the confirmation says, and the
boundary with US-005 and US-006 — were decided by the Owner and recorded in
`trebovaniya.md` v71 (§3, §4, §9).

---

# Notes

- The Control Plane is one project with internal boundaries (`architecture.md`
  AD-3): the status rules and the transaction live in `ControlPlane.Services`;
  `Controllers` hold no business rules and never touch `DbContext`. Controllers
  and views see DTOs only (AD-8).
- The `Installation` status column already exists (US-002, stored as `active` /
  `suspended`); this Story adds no status value.
- AC-006 under concurrency needs the "changes nothing" decision taken against
  the stored status inside the transaction — for example a conditional update
  whose affected-row count decides whether an audit row is written — not only a
  check before the update.
- Until US-005 and US-006 are delivered, a status change has no effect on a
  running installation; nothing in this Story calls an installation.
- Managing `AllowedAdmin` entries of a suspended installation stays allowed
  (US-003, AC-006); this Story does not change it.
- Log lines written by these actions carry internal identifiers only — never a
  name or domain (SC-10, DC-10; `non-functional-requirements.md`).
