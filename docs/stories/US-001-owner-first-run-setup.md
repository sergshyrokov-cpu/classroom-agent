---
id: US-001
epic: EPIC-8
title: Owner first-run setup
slug: owner-first-run-setup
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v64.
---

# User Story

As the **Owner** of the service

I want to create my Control Plane account the first time I start the Control
Plane

So that nobody else can claim it, and I can begin registering schools.

---

# Business Value

The Control Plane is the root of the whole authorization model: it decides which
installations may run, with which domain, and who may administer them
(`trebovaniya.md` §9). Until the Owner account exists, nothing else in the
system can be configured — no `Installation`, no `AllowedAdmin`, therefore no
Admin login and no Workspace connection.

It is also the one account with no external authority behind it. An Admin is
vouched for by the Owner; the Owner is vouched for by nobody, which is why the
account is claimed once, at first run, with a code only someone on the server
can see, and is never re-openable afterwards.

---

# Scope

**In scope:** the Control Plane (`ClassroomAgent.ControlPlane`) only — its own
host, its own database, ASP.NET Core Identity; the one-time setup code; Owner
sign-in and sign-out; the `AuditEvent` table of the Control Plane for the events this Story
produces; Ukrainian and English translation files for every page and message
this Story adds.

**Out of scope:** anything in a school installation; `Installation` and
`AllowedAdmin` management (US-002, US-003); password reset and account recovery;
a second Owner account; the Owner switching their own UI language (US-039).

---

# Acceptance Criteria

## AC-001 First run offers setup

**Given** the Control Plane database contains no Owner account

**When** the Owner opens any Control Plane page

**Then**:

- they are redirected to the first-run setup page;
- the setup page asks for the one-time setup code, a login and a password;
- no other Control Plane function is reachable until setup completes.

## AC-002 Owner account is created

**Given** the first-run setup page is open and no Owner account exists

**When** the valid setup code, a valid login and a valid password are submitted

**Then**:

- exactly one Owner account is created via ASP.NET Core Identity;
- the password is stored only as an Identity hash — never in plain text, never
  recoverable;
- the account's UI language is Ukrainian (NFR-073);
- the Owner is signed in and lands on the Control Plane home page;
- the account lives in the Control Plane database only, and no row is written
  to any installation's `AppUser` table (BR-005).

## AC-003 Setup is single-use

**Given** an Owner account already exists

**When** anyone requests the first-run setup page

**Then**:

- setup is refused;
- no second Owner account can be created;
- an unauthenticated visitor is sent to the sign-in page instead, and the
  response does not reveal the existing Owner's login.

## AC-004 Concurrent setup attempts create one account

**Given** no Owner account exists

**When** two setup submissions arrive at the same time

**Then**:

- exactly one Owner account is created;
- the losing request is refused with a conflict, not a server error;
- no partially-created account remains.

## AC-005 Sign-in after setup

**Given** an Owner account exists

**When** the Owner submits the correct login and password on the sign-in page

**Then**:

- they are authenticated and the session is carried by an `httpOnly` cookie
  (NFR-072);
- the login is compared case-insensitively (SC-2, `trebovaniya.md` §3, v64);
- on an incorrect login or password, authentication fails with a message that
  does not reveal which of the two was wrong, and does not reveal whether the
  login exists;
- after 5 consecutive failed attempts sign-in is locked for 15 minutes, even with
  the correct password; after 15 minutes the correct password signs in again, and
  a successful sign-in resets the counter; there is no permanent lockout (SC-2);
- a refusal during a lockout shows the same message as any other refusal
  (SC-2, `trebovaniya.md` §9, v62).

## AC-006 Weak input is rejected

**Given** the first-run setup page is open

**When** a login failing the format — shorter than 4 or longer than 64
characters, or with a character other than Latin letters, digits, `.`, `-` and
`_` — or a password failing the policy — shorter than 15 or longer than 128
characters, or equal to or containing the login in any letter case — is submitted
(SC-2, `trebovaniya.md` §3, §9, v62, v64)

**Then**:

- the account is not created;
- a password of 15 or more characters with no digit, upper-case letter or symbol,
  spaces included, is accepted, and one of 128 characters is accepted; length is
  counted in characters, not bytes;
- the response names which field failed and why, in terms safe to display
  (`api-conventions.md` API-6);
- the submitted password never appears in the response, in a log, or in an
  error body (SC-10).

## AC-007 Setup requires the one-time code

**Given** the Control Plane has started with no Owner account

**When** it starts, and when a setup is submitted

**Then**:

- a random one-time setup code is printed to the server console and never
  written to the log file (SC-2, SC-10);
- a submission with a missing or wrong code creates no account, and the
  response does not reveal the correct code;
- once the account exists, the code no longer works (AC-003);
- a restart while no Owner account exists generates a new code, and the
  previous one stops working.

## AC-008 Sign-in is audited

**Given** the Control Plane `AuditEvent` table (SC-11)

**When** anyone signs in, or fails to sign in, as the Owner

**Then**:

- a successful sign-in writes a row with the Owner account's internal id and
  role as actor, outcome, UTC time and request id;
- a refused sign-in writes a row with outcome "refused" and the refusal
  category — unknown login, wrong password, or locked out after failed attempts
  (SC-11, v62); the
  actor is the Owner account's id when the login exists, otherwise "anonymous";
- no row carries the login or password typed, or any other personal data
  (`trebovaniya.md` §5, v45);
- no use case, endpoint or page can update or delete a Control Plane audit row;
  these rows are kept indefinitely (v45).

## AC-009 Pages are translated, Ukrainian by default

**Given** the setup page, the sign-in page and every message this Story shows

**When** they are displayed

**Then**:

- they are in Ukrainian;
- no user-visible string is hard-coded: each comes from
  `ClassroomAgent.ControlPlane.Localization` and exists in both Ukrainian and
  English (NFR-073);
- date and number formats follow the Ukrainian locale.

## AC-010 Only the setup, sign-in and error pages are anonymous

**Given** the Control Plane is running

**When** any endpoint is requested without a signed-in Owner

**Then**:

- only the first-run setup, Owner sign-in and error page endpoints (and nothing
  else this Story adds) allow anonymous access — they are on the SC-4 closed list;
- every other endpoint is closed by the deny-by-default fallback policy, and a
  test enumerating endpoints proves it (TC-5).

## AC-011 State-changing forms are protected from CSRF

**Given** the Control Plane is running

**When** the first-run setup form or the Owner sign-in form is submitted without
a valid antiforgery token

**Then**:

- the submission is refused with `400`, no account is created and nobody is
  signed in; the Owner sees the translated "page expired — reload it and try
  again" error page (SC-4, `trebovaniya.md` §8, v64);
- every other state-changing request this Story adds — sign-out included — is
  refused the same way, and none of them is on the SC-4 antiforgery exemption
  list;
- sign-out is a POST: a GET to it does not sign the Owner out, and after sign-out
  the session no longer works (API-4, v64);
- no state-changing action this Story adds is reachable by GET (API-4);
- the Control Plane session cookie is `httpOnly`, `Secure` and
  `SameSite=Strict`, the antiforgery cookie is `httpOnly`, `Secure` and
  `SameSite=Strict`, and every other cookie is `Secure` (SC-2, `trebovaniya.md`
  §8, §9, v61, v64).

---

# Open Decisions

**OD-001 — Password policy for the Owner account.** `trebovaniya.md` states that
the Owner authenticates with a login and password via ASP.NET Core Identity
(§9), but does not fix minimum length, complexity or lockout behaviour. This
must be decided at `HUMAN_SPEC_APPROVAL`, not guessed: it is the only account
protecting the entire service, and ASP.NET Core Identity's defaults are a
starting point, not an approved decision.

Affects: AC-005, AC-006, AC-008.

*Resolved (`trebovaniya.md` v62, `security-conventions.md` SC-2):* the password
is at least 15 characters, at least 64 are accepted, spaces are allowed, there
are no composition rules, and it may not equal or contain the login; 5
consecutive failed attempts lock sign-in for 15 minutes, a successful sign-in
resets the counter, and there is no permanent lockout; every refused sign-in
shows the same message; the audit refusal category "locked out" is added. No
external breached-password check (SC-13). *Refined in v64:* the password is at
most 128 characters, counted in characters; "contains the login" is
case-insensitive; the Owner login is 4–64 characters — Latin letters, digits,
`.`, `-` and `_` — compared case-insensitively.

*Resolved:* **where the Owner switches UI language** — in the separate
cross-cutting Story US-039, which covers the Owner, Admins and Deans. This Story
ships translations and a Ukrainian default only (AC-009).

---

# Notes

- The whole Control Plane, the setup page included, is reachable only from the
  Owner's private network (DC-6, SC-9, `trebovaniya.md` v35). The setup code of
  AC-007 protects the account even if that network is misconfigured.
- The Control Plane is one project with internal boundaries (`architecture.md`
  AD-3): the setup and sign-in logic and its transactions live in
  `ControlPlane.Services`; `Controllers` hold no business rules and never touch
  `DbContext`.
- The Control Plane knows nothing about courses, participants or grades. It must
  not reference `ClassroomAgent.Domain` (`package-map.md`).
- The Control Plane has no path to teaching data and receives no school
  statistics (SC-12, decided in `trebovaniya.md` v20). Nothing this Story adds —
  pages, endpoints, contract types — may create one.
- The Control Plane sends data nowhere except the service channel to
  installations: no external error tracker, analytics or telemetry service
  (SC-13).
- The Control Plane keeps its ASP.NET Core Data Protection keys in its own
  directory on a persistent volume, outside the database and backups, so a
  restart neither signs the Owner out nor voids an open form (SC-7, v64).
- This Story is the first to need the Control Plane `AuditEvent` table (AC-008).
  Creating the Owner account at first run and submitting a wrong setup code are
  not in the audited list of `trebovaniya.md` §5 — the Specification should say
  explicitly whether they become audited events.
