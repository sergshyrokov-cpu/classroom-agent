---
id: US-001
epic: EPIC-8
title: Owner first-run setup
slug: owner-first-run-setup
priority: HIGH
source:
  type: authored
  repository: null
  issue_number: null
  issue_url: null
  last_synced_at: null
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
- the setup page asks for a login and a password;
- no other Control Plane function is reachable until setup completes.

## AC-002 Owner account is created

**Given** the first-run setup page is open and no Owner account exists

**When** a valid login and password are submitted

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

- The Control Plane knows nothing about courses, participants or grades. It must
  not reference `ClassroomAgent.Domain` (`package-map.md`).
- `trebovaniya.md` §7 item 6 leaves open whether the Control Plane may read Data
  Plane data at all; the assumed answer is no. This Story must not create any
  path that would allow it.
- Audit of Owner actions is an open requirement (NFR-025, `trebovaniya.md` §7
  item 7). Do not invent an audit scheme here.
