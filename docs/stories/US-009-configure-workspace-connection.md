---
id: US-009
epic: EPIC-6
title: Configure WorkspaceConnection
slug: configure-workspace-connection
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
---

# User Story

As an **Admin** of a school's installation

I want to record which Google Workspace domain this installation serves and
which technical account it reads the school's data on behalf of, and to have the
program refuse any domain the Owner did not approve

So that the installation knows where to look before anything is ever
synchronized, and no one — including me — can point the program at a domain that
is not my school's.

---

# Business Value

This Story fires the **second of the Owner's three points of control**
(`trebovaniya.md` §9, "Три точки контроля Владельца"): saving a
`WorkspaceConnection` is checked against the `Installation` domain the Owner
recorded in the Control Plane, so the Owner governs **which domain an
installation may look at** — the first point (who may configure a school)
arrived with US-008, the third (whether a school works at all) with US-005 and
US-006.

It is also the last missing piece of onboarding before the program can read
anything: DC-2's deployment sequence, step 7, is exactly this screen. Without it
the school's technical account (BR-015) is unknown, so the connection
instructions (US-010), the "check access" diagnostic (US-011) and all of EPIC-1
have nothing to impersonate.

It is the first Story in which an Admin **writes** anything into the
installation database, so it is also the first real exercise of the read-only
refusal US-007 built and US-008 mapped to HTTP: in read-only mode the settings
stay visible and the save is refused (BR-026).

---

# Scope

**In scope:**

- the `WorkspaceConnection` entity in `Domain` — the Google Workspace domain and
  the impersonation user (the school's technical account), one record per
  installation, with its EF Core migration;
- the admin-panel page that shows the current connection or states plainly that
  the installation is not configured yet, and the form that saves it;
- the BR-020 invariant, enforced in `Application`: the connection domain **and**
  the domain part of the impersonation user's email must equal the
  `Installation` domain that the legitimacy check recorded in `LegitimacyState`
  (US-005, v54) — otherwise the save is refused;
- the behaviour while no legitimacy check has ever succeeded and the domain is
  therefore unknown;
- input validation before business logic — the shape of a domain, the shape of
  an email, length limits, trimming and case normalisation — with the rejected
  value never written to a log (SC-10);
- changing an existing connection: the single record is updated, never
  duplicated, and the change is audited;
- the audit rows for saving and changing `WorkspaceConnection` (SC-11,
  `trebovaniya.md` §5), carrying no personal datum and no email;
- the read-only refusal of the save, enforced in `Application` and surfaced
  through the mapping US-008 established (BR-026, AD-6);
- authorization: the page and its endpoints are Admin-only — the permission
  matrix row "Настройка `WorkspaceConnection` (домен, impersonation)" is `✔` for
  Admin and `✘` for Dean (`trebovaniya.md` §2);
- Ukrainian and English translations for every user-visible string the Story
  adds, including the refusal messages (NFR-073);
- the admin-panel entry point that leads to this page from the landing page
  US-008 built.

**Out of scope:** the connection instructions for the school's super-admin
(US-010); the "check access" diagnostic and the start-up self-check (US-011);
any call to a Google API — this Story neither verifies the technical account
against Google nor requests a scope, and adds no Google port (EPIC-1, US-011);
the service-account key and the reference to it, which are the Owner's
deployment configuration and never appear in the UI or in the database (DC-3,
SC-7 — a Hard Stop); Dean accounts (US-012); letting a user choose their UI
language (US-039); synchronization and `SyncState` (EPIC-1); viewing the audit
trail (EPIC-9); the retention purge (EPIC-10); anything the Owner sees about the
connection in the Control Plane — the Control Plane is not told that a school
saved its settings.

---

# Acceptance Criteria

## AC-001 Only an Admin reaches the connection settings

**Given** the installation is running

**When** the connection settings page or its save endpoint is requested

**Then**:

- an Admin reaches it; a Dean is refused; an anonymous visitor is sent to
  sign in and never sees the page (`trebovaniya.md` §2, SC-4);
- both the allowed-role and the forbidden-role case are tested (TC-5);
- the save endpoint is a `POST` carrying the antiforgery token, and a `GET`
  saves nothing (`trebovaniya.md` §8);
- nothing anonymous is added to the SC-4 closed list, and the endpoint
  enumeration test US-008 wrote still passes unchanged.

## AC-002 An unconfigured installation says so plainly

**Given** an installation where no connection has ever been saved

**When** an Admin opens the connection settings

**Then**:

- the page states that the connection is not configured yet, in the user's
  language, with no invented default and no value guessed from configuration;
- the form is empty except for what AC-003 pre-fills;
- nothing is written to the database by merely opening the page.

## AC-003 Saving a valid connection stores the domain and the technical account

**Given** an Admin on the connection settings page of an installation whose
`Installation` domain is known from a successful legitimacy check

**When** they save a connection whose domain equals that domain and whose
impersonation user is an email in it

**Then**:

- the connection is stored with exactly two values — the domain and the
  impersonation user's email — and **nothing else**: no service-account key, no
  reference to one, no client id, no password (`trebovaniya.md` §3 v33, SC-7,
  PC-8);
- the saved values are shown when the page is reopened;
- the email is stored in lower case and both values are trimmed, so the same
  account entered with different spacing or capitalisation is the same account
  (BR-079's precedent for stored emails);
- the installation makes **no** call to Google while saving: the account is not
  verified against Workspace here, and the Admin is told that checking access is
  a separate action (US-011, BR-026's "no call to Google at all" is not even
  reached because none exists).

## AC-004 A domain the Owner did not approve is refused

**Given** a known `Installation` domain in `LegitimacyState`

**When** an Admin saves a connection whose domain differs from it — a
neighbouring school's domain, a subdomain, a domain differing only in case or by
a trailing dot

**Then**:

- the save is refused with a translated message naming the domain the
  installation is allowed to work with (BR-020, SC-9);
- nothing is written: no connection row, no partial update of an existing one;
- the refusal is a validation outcome, not an exception surfaced to the user
  (AD-9);
- the comparison is case-insensitive and ignores a trailing dot, so
  `School.Example.Org.` and `school.example.org` are the same domain and a
  mismatch cannot be manufactured by spelling.

## AC-005 A technical account outside the domain is refused

**Given** the same installation

**When** an Admin saves an impersonation user whose email domain differs from
the `Installation` domain — a personal Gmail address, an address in another
school's domain, or an address in a subdomain of the school's domain

**Then**:

- the save is refused with a translated message (BR-020);
- the check is on the domain part of the email after the single `@`, compared by
  the same rule as AC-004;
- the connection domain and the email domain are both checked — a request that
  passes one and fails the other is refused.

## AC-006 Malformed input is rejected before business logic

**Given** the save endpoint

**When** it receives a missing, empty, over-long or malformed value — a domain
that is not a domain, an email with no `@` or with several, whitespace inside
either value, or a body that is missing or not a form at all

**Then**:

- the request is rejected with a translated message per field, before any use
  case runs (`trebovaniya.md` §8, SC-10);
- the rejected value is never written to a log, and never echoed into an error
  page beyond the field it belongs to (SC-10);
- the limits are the ones the database enforces as well, so a value the page
  accepts can always be stored (PC-8).

## AC-007 Changing the connection updates the one record

**Given** an installation with a saved connection

**When** an Admin saves a different technical account

**Then**:

- the same single record is updated — a second `WorkspaceConnection` row is
  impossible, enforced by the database and not only by a check before insert
  (`trebovaniya.md` §3: one active domain at a time);
- the previous value is not kept in the table; its history lives in the audit
  trail (AC-008);
- saving the identical values again is accepted and changes nothing beyond the
  audit row;
- the domain cannot be moved to another domain by this screen — moving a school
  is a new `Installation` with a new database (BR-021), so AC-004 refuses it.

## AC-008 Saving and changing are audited without personal data

**Given** any attempt to save the connection

**When** it succeeds or is refused

**Then**:

- an `AuditEvent` row is written with the time in UTC, the actor (the Admin's
  `AppUser` id and role), the action, the target, the outcome and the request id
  that ties it to the log (SC-11, `trebovaniya.md` §5);
- the row distinguishes the first save from a change of an existing connection;
- **no email, domain or other value the Admin typed is written into the row** —
  the technical account's address is an account of a real school and does not
  belong in an audit row that the retention purge keeps for years (SC-11);
- the row is never updated afterwards (PC-6);
- a refused save is audited too, with its refusal category — domain mismatch,
  impersonation domain mismatch, domain not yet known, or read-only mode.

## AC-009 Read-only mode blocks the save and keeps the view

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (US-007)

**When** an Admin opens the connection settings and tries to save

**Then**:

- the settings are shown, with the read-only reason stated in the user's
  language (BR-026: viewing keeps working);
- the save is refused in `Application`, not by hiding or disabling the form
  (AD-6, TC-5), and reaches HTTP as the `409` / error-page mapping US-008
  established;
- nothing is written to `WorkspaceConnection`;
- the refusal is audited — an audit row is on the BR-026 closed list of
  permitted service writes, and this Story adds **no** new entry to that list;
- a test asserts that `PermittedServiceWrite` still has exactly the members
  US-008 left it with.

## AC-010 An installation that has never been legitimated cannot be configured

**Given** an installation where no legitimacy check has ever succeeded, so
`LegitimacyState` has no `Installation` domain

**When** an Admin tries to save a connection

**Then**:

- the save is refused with a translated message saying the installation has not
  yet confirmed its legitimacy and the allowed domain is therefore unknown
  (BR-020: the domain comes from the Control Plane, never from the school);
- the message does not blame the Admin's input, which may be perfectly correct;
- a stale domain is never used instead: the last recorded answer is used when one
  exists, and nothing is used when none does (`trebovaniya.md` §3, v54);
- this case is also read-only (US-007), and the tests prove the domain refusal
  independently of the read-only refusal so neither hides the other.

## AC-011 Every new string is translated

**Given** an installation whose UI language is Ukrainian or English

**When** an Admin uses the connection settings, in either language

**Then**:

- every label, hint, button, validation message and refusal message comes from
  the translation files — no user-visible string is hard-coded (NFR-073);
- both language files carry every key this Story adds, and a test fails on a key
  present in one file and missing from the other;
- the technical account's email is data, not a translated string, and is never
  transformed for display.

## AC-012 The schema change ships as a migration

**Given** the installation database created by the US-008 migration

**When** this Story is applied

**Then**:

- one EF Core migration creates the connection table, and no schema change
  happens outside a migration — no `EnsureCreated()` (PC-2, DC-4);
- the table's constraints enforce what the entity promises: a single row, the
  two values non-empty, their lengths bounded;
- the Control Plane database is untouched — it learns nothing about a school's
  connection;
- the migration runs on the integration-test PostgreSQL and the US-008 tables are
  unaffected (TC-2).

---

# Open Decisions

## OD-001 Whether the Admin types the domain at all

`trebovaniya.md` §3 and Epic 6 both say the Admin configures "домен и
impersonation-пользователь", and BR-020 says the domain must equal the
`Installation` domain from the Control Plane. Taken together, the only domain
the Admin may ever successfully type is the one the installation already knows
from `LegitimacyState`. The requirements do not say whether the field is
therefore an input or a displayed value.

Options:

1. **The domain is shown, not typed.** The page displays the `Installation`
   domain from `LegitimacyState` and the Admin enters only the technical
   account. The BR-020 check stays in `Application` exactly as AC-004 requires —
   it is the control, not a UI convenience — but no human can fail it by typing.
2. **The domain is typed and checked.** The Admin enters both values; a mismatch
   is refused with the message of AC-004. Literal to the requirement's wording;
   its cost is a refusal that exists only to catch a typo.
3. The domain is typed, pre-filled with the known value, and editable. A middle
   ground that keeps the refusal reachable while making it unlikely.

Whichever is chosen, the stored connection keeps its own domain column: the
entity is what §3 describes, and the check compares two stored values rather
than trusting a form.

**Resolution:** option 1, decided by the Owner on 2026-09-20. The connection
domain is **displayed** from `LegitimacyState`, not typed: the Admin enters the
technical account alone. The only domain a save could ever carry is the one the
Owner recorded, so a domain field would exist solely to be mistyped.

Three things this resolution does **not** relax:

- the BR-020 check stays in `Application` and is tested exactly as AC-004
  requires. It is the Owner's control, not a form convenience, and a save
  arriving with a domain that differs — a crafted request, a later API client —
  is refused, not corrected. AC-004's cases are reached by constructing the
  request, not by typing in the form;
- `WorkspaceConnection` keeps its own domain column and the value is written
  from `LegitimacyState` at the moment of the save, so the record states which
  domain it was saved for and OD-002 has something to compare;
- AC-005 is unaffected: the impersonation user's email domain is typed by the
  Admin and is the field a person can get wrong, so its refusal is the one
  people will actually see.

The page still shows the domain plainly — the Admin must be able to see which
school the installation is bound to before entering an account in it.

## OD-002 What happens to a saved connection if the Installation domain ever changes

BR-021 says an `Installation` domain never changes — a school moving domains
gets a new `Installation` and a new database. So a legitimacy check returning a
domain that differs from the saved connection should be impossible. The
requirements do not say what the installation does if it happens anyway: a
Control Plane defect, a restored backup of the wrong database, or an
installation pointed at the wrong Control Plane record.

Options:

1. **Refuse to read data and say so.** The connection is treated as invalid: the
   settings page states that the saved domain no longer matches the one the
   Owner records, and the Admin must save it again. Nothing is deleted
   automatically.
2. Ignore the difference. The saved connection stands until an Admin changes it;
   only the next save is checked. Simplest, and the only path by which the
   program reads a domain the Owner did not approve.
3. Clear the connection automatically. Rejected on sight: a deleted connection
   cannot be distinguished from one never configured, and it destroys evidence.

This has to be decided now rather than later because EPIC-1 reads Google on
whatever this record says.

**Resolution:** option 1, decided by the Owner on 2026-09-20. A saved connection
whose domain differs from the `Installation` domain in `LegitimacyState` is
treated as **invalid**: it is not used, and the settings page states that the
saved domain no longer matches the one the Owner records and that the connection
must be saved again. Nothing is deleted and nothing is corrected automatically.

- The mismatch is a **state the program reports**, not a silent repair: an
  automatic rewrite would hide a Control Plane defect or a restored wrong
  database, which are exactly the situations this exists for.
- "Not used" means every later reader of the connection sees it as absent:
  synchronization does not start on it (EPIC-1) and "check access" does not run
  against it (US-011). Those Stories consume the invalid state; this Story
  defines it and proves it at the Application level.
- A save while the state is invalid follows the ordinary rules: the domain
  written is the one `LegitimacyState` holds now (OD-001), so saving again is
  what clears the mismatch.
- The comparison uses the same domain rule as AC-004 and AC-005, so case and a
  trailing dot never manufacture a mismatch.
- The Specification carries this as an Acceptance Criterion of its own, with the
  invalid state visible in `Application` rather than in the view (AD-6).

---

# Notes

- **No Google call anywhere in this Story.** Saving the connection records an
  intent; proving that the technical account actually works is "check access"
  (US-011). The Specification should say this plainly so no reviewer reads a
  missing verification as an omission, and so no Google port appears here — the
  US-007 security review finding F-5 stays with US-011, the first Story to add a
  real one.
- **The key is not in this Story and not in this table.** The service-account key
  and the reference to it are the Owner's deployment configuration (DC-3, SC-7);
  `WorkspaceConnection` holds the domain and the impersonation user only. An
  entity property, a form field or a configuration binding that carries the key
  or its reference into this table is a Hard Stop, not a design choice.
- The impersonation user is the school's **technical account** (BR-015): not the
  Admin's account and not a super-admin. The page should say so in a sentence, in
  both languages, so an Admin does not enter their own address — the instruction
  text proper is US-010.
- The `Installation` domain comes from `LegitimacyState`, which US-005 already
  keeps and which is written even in read-only mode (BR-026). This Story reads
  it; it never writes it and never asks the Control Plane directly.
- `AuditAction` and `AuditTargetType` grow here by the members this Story
  performs — the first save and the change, and the connection as a target.
  US-008 deliberately left `AuditTargetType` empty and documented that the Story
  adding the first target fills it; this is that Story.
- The admin panel needs a place to live. US-008 built the landing page for a
  signed-in user; this Story adds the first navigation entry to a settings
  section. Keep it small: US-010, US-011 and US-012 all add entries next to it,
  so the Specification should define the section once rather than a page-shaped
  one-off.
- Read-only mode is proven in `Application` (TC-5), and the HTTP outcome is the
  mapping US-008 created, not a second one. If the save's refusal needs a
  different status or body from the US-008 mapping, that is a finding about the
  mapping, not a licence to add another.
- Validation is DataAnnotations in `Application/Models/Requests` with the rules
  in `Validation` (`trebovaniya.md` §8, `package-map.md`), and the domain
  comparison rule is written once and used by both AC-004 and AC-005.
- No test calls a live Google API (TC-4), and the integration tests run against
  real PostgreSQL through Testcontainers (TC-2). Nothing in this Story needs a
  Google fixture at all.
