---
id: US-001
epic: EPIC-8
title: Owner first-run setup
slug: owner-first-run-setup
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
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
account is claimed once, at first run, and never re-openable afterwards.

---

# Scope

**In scope:** the Control Plane (`ClassroomAgent.ControlPlane`) only — its own
host, its own database, ASP.NET Core Identity.

**Out of scope:** anything in a school installation; `Installation` and
`AllowedAdmin` management (US-002, US-003); password reset and account recovery;
a second Owner account.

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
- on an incorrect login or password, authentication fails with a message that
  does not reveal which of the two was wrong, and does not reveal whether the
  login exists.

## AC-006 Weak input is rejected

**Given** the first-run setup page is open

**When** a login or password failing the configured policy is submitted

**Then**:

- the account is not created;
- the response names which field failed and why, in terms safe to display
  (`api-conventions.md` AC-6);
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

---

# Open Decisions

**OD-001 — Password policy for the Owner account.** `trebovaniya.md` states that
the Owner authenticates with a login and password via ASP.NET Core Identity
(§9), but does not fix minimum length, complexity or lockout behaviour. This
must be decided at `HUMAN_SPEC_APPROVAL`, not guessed: it is the only account
protecting the entire service, and ASP.NET Core Identity's defaults are a
starting point, not an approved decision.

Affects: AC-005, AC-006.

---

# Notes

- The whole Control Plane, the setup page included, is reachable only from the
  Owner's private network (DC-6, SC-9, `trebovaniya.md` v35). The setup code of
  AC-007 protects the account even if that network is misconfigured.
- The Control Plane knows nothing about courses, participants or grades. It must
  not reference `ClassroomAgent.Domain` (`package-map.md`).
- The Control Plane has no path to teaching data and receives no school
  statistics (SC-12, decided in `trebovaniya.md` v20). Nothing this Story adds —
  pages, endpoints, contract types — may create one.
- Owner sign-in is an audited action (SC-11, NFR-025, decided in
  `trebovaniya.md` v17). This Story is therefore the first to need the
  `AuditEvent` table in the Control Plane database: sign-in and refused sign-in
  are recorded with actor, outcome and request id, and no personal data. Creating
  the Owner account at first run is not in the audited list — the Specification
  should say explicitly whether it becomes one.
