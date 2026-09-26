---
id: US-012
epic: EPIC-6
title: Create and manage Dean accounts
slug: manage-dean-accounts
priority: HIGH
source:
  type: authored
---

<!-- Lifecycle status is owned by docs/catalog/stories.yaml (not this file). -->
<!-- Aligned with trebovaniya.md v79. -->
<!-- Drafted from trebovaniya.md §2, §3, §5, BR-014, SC-2, SC-4 and SC-11.
     OD-001 … OD-004 were all resolved by the Owner on 2026-09-26, before
     activation, as US-011's five decisions were. -->

# User Story

**As an** Admin of a school installation,
**I want** to create a Dean account on a work email in the school's domain, and
afterwards disable it, re-enable it and reset its password,
**so that** the person who does the day-to-day work — viewing courses, journals
and Meet statistics, and exporting them — can sign in to the installation
without ever receiving a Google Workspace administrator role.

**As a** Dean,
**I want** to sign in with my work email and my own password, and to change that
password,
**so that** the temporary password the Admin typed at creation is known to me
alone from my first sign-in onwards.

# Business Value

This is the Story that puts the second role into a running installation. Until
it lands, an installation has exactly one kind of human user — the Admin — and
the role the whole product was built around (`trebovaniya.md` §2: "Декан
(учебная часть)") cannot sign in at all. Everything EPIC-1, EPIC-3 and EPIC-4
deliver for a Dean is unreachable without it, and US-039 (choosing a UI
language) lists US-012 among its dependencies.

It also closes a gap that three security reviews have now carried forward: the
forbidden-role test for the Admin-only settings pages has had to use a synthetic
principal, because no Dean could sign in (US-010 F-1, carried to US-011 F-1 with
owner US-012). With a real Dean session the check becomes an ordinary HTTP test.

The Admin stays a Google Workspace domain administrator who configures the
installation; the Dean gets a local account and no configuration rights at all.
That separation is the reason the permission matrix in `trebovaniya.md` §2 has
two Data Plane roles.

# Scope

In scope:

- an Admin-only screen listing the installation's Dean accounts and offering the
  four management actions of BR-014: **create**, **disable**, **re-enable**,
  **reset password**;
- the login rule: a Dean's login is their work email in the school's domain,
  there is no separate login field, and the school's domain is the one the last
  successful legitimacy check reported (`LegitimacyState`, as US-009 and US-010
  already read it);
- the password and lockout policy of SC-2 — 15 to 128 characters, no
  composition rules, may not equal or contain the login or its local part, 5
  failed attempts lock sign-in for 15 minutes, no permanent lockout;
- the temporary password: the one an Admin types at creation or at a reset must
  be changed by the Dean at the next sign-in, and the new password may not equal
  it;
- the Dean sign-in page and the full six-step check sequence of SC-2, including
  the separate "account disabled" message and its exact position in that
  sequence;
- a Dean changing their own password at any time, including in read-only mode;
- the audit rows SC-11 requires for all of the above;
- Ukrainian and English strings for every new screen and message (NFR-073).

Out of scope:

- **deleting** a Dean account — BR-014 forbids it and PC-11 owns the only
  deletion there is (the retention purge, N years after the last successful
  sign-in or after creation);
- **changing** an existing account's email; no artifact defines such an action
  (see OD-004);
- the Dean's own screens for courses, journals, Meet statistics and exports —
  those belong to EPIC-1, EPIC-3 and EPIC-4;
- choosing a UI language, which is US-039; a Dean created here gets the
  installation's default language;
- anything about Admin accounts: an Admin has no local password, no password
  column and no reset flow (SC-2), and an `AppUser` with role Admin is still
  created only by a first successful Google sign-in (US-008);
- password recovery by email or any other self-service reset — the only reset is
  the Admin's (BR-014), and the installation sends no mail (SC-13).

# Acceptance Criteria

## AC-001 The Dean accounts screen is reachable by an Admin and forbidden to a Dean

**Given** an installation that is not in read-only mode

**When** a signed-in Admin opens the Dean accounts screen

**Then**:
- the screen answers 200 and lists every `AppUser` with role Dean, active and
  disabled alike, each with its state;
- the same request from a signed-in Dean answers 403, and from an anonymous
  visitor is redirected to sign-in (SC-4 deny-by-default; the screen declares
  its own Admin-only authorization policy);
- the screen lists no Admin account and offers no action on one.

## AC-002 An Admin creates a Dean account on a work email in the school's domain

**Given** a signed-in Admin, an installation that is not in read-only mode, and
a school domain known from the last successful legitimacy check

**When** the Admin submits a work email in that domain together with a temporary
password that satisfies the password policy

**Then**:
- an `AppUser` is created with role Dean, sign-in method password, the email
  stored lower-cased and normalized, not disabled, and its password stored only
  as a hash (SC-2; the plaintext is never stored, logged or returned);
- the account is marked as holding a temporary password, so the next successful
  sign-in forces a change (AC-012);
- one audit row records the creation: action "Dean account created", target the
  new account's internal id, outcome succeeded, actor the Admin — and no email
  address, name or password anywhere in the row (SC-11, SC-10);
- the new account appears in the list of AC-001 as active and never signed in.

## AC-003 Creation is refused for an address that is not a work email in the school's domain, or is already taken

**Given** a signed-in Admin on the creation form

**When** the Admin submits one of: an address in another domain; a free-form
string that is not an email; an address whose local part is empty; the email of
an account that already exists, whether that account is a Dean, a disabled Dean
or an Admin

**Then**:
- the request is refused with a validation message naming the reason, the form
  is re-rendered and no `AppUser` row is created or modified (BR-014, SC-2);
- the refusal for an address already in use does not reveal the role or state of
  the existing account;
- the comparison that finds a duplicate is the normalized, case-insensitive one,
  so `Dean@school.example` and `dean@school.example` are the same login;
- when no legitimacy check has ever succeeded the school's domain is unknown —
  the installation is then in read-only mode, so AC-009 governs, and no
  creation form ever falls back to accepting any domain.

## AC-004 Every password an Admin or a Dean sets obeys the SC-2 policy

**Given** any form that sets a password — creation, reset, the forced change at
first sign-in, and a Dean changing their own password

**When** a password is submitted

**Then**:
- it is accepted only when it is 15 to 128 characters long, counted in
  characters and not bytes, with spaces allowed and no composition rule
  required;
- it is refused when it equals the login, compared case-insensitively, or
  contains the login or the part of the email before `@`, where "contains" is
  checked only for a login or local part of at least 4 characters and a shorter
  one is checked for equality only (SC-2, v65);
- a refusal names the rule that was broken and the rejected password is never
  written to a log, an audit row or the re-rendered form (SC-10);
- no other rule is added — a required digit, an upper-case letter, a symbol, a
  minimum length below 15 or a check against an external breached-password
  service is a defect (SC-2, SC-13).

## AC-005 An Admin disables a Dean account

**Given** a signed-in Admin and an active Dean account

**When** the Admin disables it

**Then**:
- the account is marked disabled and is not deleted; its password hash, its
  failed-attempt counter and its last successful sign-in time are left as they
  are (BR-014, PC-11);
- the list of AC-001 shows it as disabled;
- one audit row records the action with the account's internal id and no
  personal data;
- an existing session of that Dean stops working at its next request — the
  disabled state is checked on every request, not only at sign-in.

## AC-006 An Admin re-enables a disabled Dean account

**Given** a signed-in Admin and a disabled Dean account

**When** the Admin re-enables it

**Then**:
- the account is active again and may sign in with the password it had before it
  was disabled — re-enabling alone does not set a temporary password and does
  not clear a lockout;
- one audit row records the action;
- the action is offered only for a disabled account.

## AC-007 An Admin resets a Dean's password

**Given** a signed-in Admin and a Dean account, active or disabled, with or
without a sign-in lockout in force

**When** the Admin submits a new temporary password for it

**Then**:
- the stored hash is replaced, the account is marked as holding a temporary
  password, and the Dean must change it at the next successful sign-in;
- the sign-in lockout is cleared and the failed-attempt counter is reset to zero
  (BR-014, v64);
- a **disabled** account stays disabled — a reset never re-enables it (BR-014);
- the account's security stamp is rotated, so any session opened with the old
  password stops working at its next request;
- one audit row records the reset; the new password appears in no row and no log.

## AC-008 No path deletes a Dean account

**Given** the finished Story

**When** the Dean accounts screen, its controller and the application layer are
inspected

**Then**:
- there is no action, endpoint, use case or repository method that deletes an
  `AppUser`, and no UI control offers it (BR-014);
- the only deletion of an `AppUser` remains the retention purge (PC-11), which
  this Story does not change.

## AC-009 In read-only mode every Dean-management action is refused

**Given** an installation in read-only mode for any of the three causes of
BR-025 — the grace period expired, the Owner suspended the `Installation`, or no
legitimacy check has ever succeeded

**When** an Admin attempts to create, disable, re-enable or reset the password
of a Dean account

**Then**:
- each attempt is refused with the host's read-only response, and the read-only
  guard is consulted in the application layer as the first statement of the use
  case, before any repository call — not by hiding the screen (AD-6, TC-5);
- no `AppUser` row is created or modified;
- the refusal writes its audit row, which is one of the service writes BR-026
  still permits;
- viewing the list of AC-001 keeps working, because viewing is never blocked in
  read-only mode (BR-026).

## AC-010 The Dean sign-in check sequence runs in the order SC-2 fixes

**Given** the Dean sign-in page, which is anonymous and carries an antiforgery
token (SC-4)

**When** a sign-in is attempted

**Then** each step runs only if the previous one passed, in exactly this order
(SC-2, `trebovaniya.md` §2 v66):
1. the login is not found — the common refusal message; audit refusal category
   "unknown login"; actor anonymous with no identifier, and the typed login is
   not recorded (SC-11);
2. a sign-in lockout is in force — the common message, and **the password is not
   checked**, so a disabled account cannot be used to test passwords past the
   lockout; category "locked out";
3. the password is wrong — the common message; the failed-attempt counter is
   incremented; category "wrong password";
4. the account is disabled — the separate "account disabled, contact your Admin"
   message; the counter is neither incremented nor reset; category "account
   disabled";
5. the password is temporary — the forced change form of AC-012;
6. success — the counter is reset, the last successful sign-in time is recorded,
   the Dean is signed in and an audit row records the sign-in.

and:
- the "account disabled" message is shown only at step 4 — with the correct
  password and no lockout in force; showing it at any other point is a defect;
- steps 1, 2 and 3 are indistinguishable to the caller: one message, and no
  difference in status code, redirect or timing that reveals whether the login
  exists;
- the sequence is not built on `SignInManager.PasswordSignInAsync` or
  `CheckPasswordSignInAsync`, and the disabled state is expressed neither
  through `CanSignInAsync` nor through the lockout fields (SC-2).

## AC-011 Lockout is five attempts for fifteen minutes, and never permanent

**Given** an active Dean account with a correct password

**When** five consecutive sign-in attempts fail

**Then**:
- the sixth attempt is refused with the common message even when the password is
  correct, and the refusal is audited as "locked out";
- the lockout ends by itself 15 minutes after the fifth failure — there is no
  permanent lockout and no Admin action is required to lift it, though an
  Admin's password reset also clears it (AC-007);
- a successful sign-in resets the counter to zero;
- the lockout clock is read from the injected `TimeProvider`, so the test does
  not wait 15 real minutes.

## AC-012 A temporary password must be changed at the next sign-in

**Given** a Dean account created or reset by an Admin, so it holds a temporary
password

**When** the Dean signs in with it and no lockout is in force and the account is
not disabled

**Then**:
- the Dean is not signed in to the application yet: the forced change form is
  shown instead, and no other page of the installation is reachable until the
  password is changed;
- the new password obeys AC-004 and, in addition, may not equal the temporary
  one (BR-014, v64);
- on success the temporary mark is cleared, the security stamp is rotated, the
  Dean is signed in, and one audit row records the password change by the Dean
  themselves (SC-11);
- from then on only the Dean knows the password — a flow that leaves the Admin
  with a working password is a defect (SC-2).

## AC-013 A Dean changes their own password, in read-only mode too

**Given** a signed-in Dean whose password is not temporary

**When** the Dean submits the current password and a new one

**Then**:
- the change succeeds only when the current password is correct and the new one
  obeys AC-004;
- the security stamp is rotated, so other sessions of that account stop working;
- one audit row records it;
- it works while the installation is in read-only mode, because a Dean changing
  their own password is on the closed list of service writes BR-026 permits —
  and so is the sign-in bookkeeping of AC-010 and AC-011;
- an Admin has no such screen and no password to change (SC-2).

## AC-014 A signed-in Dean is forbidden from the Admin-only settings pages over HTTP

**Given** a Dean signed in through the flow of this Story

**When** the Dean requests the workspace-connection page, the connection
instruction page and the check-access page, each with both GET and POST where
the page has one

**Then**:
- every one of them answers 403, asserted over HTTP against the real
  authorization policies rather than with a synthetic principal — this closes
  the finding carried from US-010 (F-1) through US-011 (F-1), whose owner is
  this Story (TC-5);
- the legitimacy-status view, which both roles may see, keeps answering 200 for
  the same Dean, so the test proves a role boundary and not a broken session.

## AC-015 Every user-visible string is translated

**Given** the screens and messages this Story adds — the Dean accounts list, the
creation form, the confirmation and refusal messages of all four management
actions, the Dean sign-in page, the common refusal message, the "account
disabled" message, the forced change form and the change-password form

**When** the installation runs with each of its two languages

**Then**:
- every string comes from the Ukrainian and English translation files, with no
  user-visible literal in a controller, a view or the application layer
  (NFR-073);
- both files hold the same set of keys, and the keys follow the existing
  `Area.Element[.Detail]` convention;
- the language used is the signed-in user's, falling back to the installation's
  default, and on the anonymous sign-in page it is the installation's default.

## AC-016 The audit trail carries what SC-11 requires and no personal data

**Given** the finished Story

**When** the audit rows written by all of its paths are inspected — creation,
disabling, re-enabling, password reset, the Dean's own change, a successful Dean
sign-in, and each refused sign-in with its category

**Then**:
- every one of the actions SC-11 lists for Dean accounts writes exactly one row,
  and a refused sign-in names the existing account's internal id as actor, or
  "anonymous" with no identifier when no account matches;
- no row carries an email address, a name, a password, a password hash or the
  typed login (SC-10, SC-11);
- audit rows are never updated, and the refusal categories used are exactly
  those SC-11 names — unknown login, wrong password, account disabled, locked
  out — plus the read-only category of AC-009;
- a write that refuses still commits its audit row alone, with nothing else
  staged in the same transaction (carried US-009 F-2).

# Open Decisions

## OD-001 Does US-012 cover the Dean sign-in flow, or only the Admin's management screen?

The catalog title is "Create and manage Dean accounts", which reads as the
Admin's side alone. But `docs/catalog/stories.yaml` holds no other Story for a
Dean signing in, the security review of US-011 recorded that "no Dean can sign
in until US-012", and the temporary-password rule of BR-014 is one rule that
spans both sides: an Admin types a password that the Dean must change at the
next sign-in. Split across two Stories, AC-012 cannot be proven in either.

Against keeping them together: this is a large Story — roughly the Admin screen
with four actions, plus a sign-in page with a six-step sequence, a lockout, two
password forms, a migration, new audit actions and two sets of translations.

Options:

1. **One Story, as drafted** — the Admin's screen and the Dean's sign-in,
   forced change and self-service change together. The temporary-password rule
   is testable end to end, and US-039 and every Dean-facing Epic unblock at once.
2. Split in two — US-012 keeps the Admin's four management actions, and a new
   Story (US-012a, or a new number the human assigns) takes the sign-in page,
   the forced change and the self-service change. AC-010 … AC-014 move there,
   and US-012 ends with accounts that exist but cannot be used.
3. Split differently — US-012 keeps management **and** plain sign-in, and a
   third Story takes the temporary-password and lockout mechanics. Not
   recommended: it would ship a sign-in that ignores SC-2's own check sequence.

**Resolution:** option 1, decided by Serhii SHYROKOV on 2026-09-26. One Story.
The temporary-password rule of BR-014 spans both sides — an Admin types a
password the Dean must then change — so a split would leave AC-012 unprovable in
either half, and US-012 would end with accounts nobody can use. The size is
accepted: the Story is large, and SPECIFICATION is expected to reflect that
rather than trim it.

## OD-002 How is the Dean's password verified — full ASP.NET Core Identity, or the hand-rolled account the installation already has?

`AGENTS.md` and SC-2 both say "ASP.NET Core Identity" for the Dean's local
login, but the installation host does not use Identity today: `AppUser` is a
domain entity written by hand, already carrying `PasswordHash`, `SecurityStamp`,
`AccessFailedCount`, `LockoutEnd` and `IsDisabled`, and authentication is
cookies plus Google OAuth. SC-2 also states plainly that the six-step sequence
**cannot** be built on `SignInManager`, because it checks `CanSignInAsync`
before the lockout and resets the counter on a correct password. The Control
Plane hashes the Owner's password with `IPasswordHasher<T>` from
`Microsoft.Extensions.Identity.Core`.

Adding a NuGet package to a project requires an approved Open Decision
(`AGENTS.md`, Technology Stack), which is why this is recorded here.

Options:

1. **Keep the hand-rolled `AppUser` and add only `IPasswordHasher<AppUser>`**
   from `Microsoft.Extensions.Identity.Core`, as the Control Plane already does
   for the Owner. The six-step sequence is written as an application use case
   over the fields `AppUser` already has. Nothing about the existing Admin
   sign-in changes.
2. Introduce full ASP.NET Core Identity in the installation host —
   `IdentityDbContext`, `UserManager`, `SignInManager` and the Identity tables —
   and map `AppUser` onto it. This contradicts SC-2's warning about the sequence,
   duplicates the account table that US-008 already ships, and needs a migration
   that touches every existing account.
3. Hash without any Identity package, using ASP.NET Core Data Protection or
   `Rfc2898DeriveBytes` directly. Fewer dependencies, but it means writing a
   password hasher by hand — the thing most likely to be got wrong.

**Resolution:** option 1, decided by Serhii SHYROKOV on 2026-09-26. The
installation keeps its hand-written `AppUser` and adds only
`IPasswordHasher<AppUser>` from `Microsoft.Extensions.Identity.Core`, the way the
Control Plane already hashes the Owner's password. **This is the approval
`AGENTS.md` requires for that NuGet package**, and it is scoped to the projects
the design names — nothing else is added to the stack. The six-step sequence of
SC-2 is written as an application use case over the fields `AppUser` already
carries; `UserManager` and `SignInManager` are not introduced, since SC-2
forbids building the sequence on them.

## OD-003 What does the Dean accounts list show, and does it need search or paging?

No artifact describes the screen. The account holds an email, a state, a
creation time, a last successful sign-in time and a UI language; the email is
personal data, so what appears on screen and in what order is a decision, not a
derivation. `trebovaniya.md` sets no cap on the number of Dean accounts per
installation.

Options:

1. **Email, state (active / disabled), whether the password is temporary, and
   the last successful sign-in date; no search and no paging in v1** — a school
   has a handful of Deans, and the four actions are reachable on each row.
2. The same columns plus a search box and paging, sized like the lists later
   Epics will need.
3. Email and state only — the smallest screen that supports the four actions.

**Resolution:** option 1, decided by Serhii SHYROKOV on 2026-09-26. The list
shows the email, the state (active / disabled), whether the password is still
temporary and the date of the last successful sign-in; no search and no paging in
v1, because a school has a handful of Deans. The temporary-password column is
kept because it tells the Admin at a glance that a newly created or reset account
has not been taken over by its Dean yet. Search and paging stay available to a
later Story if the lists of EPIC-1 or EPIC-3 need them.

## OD-004 A mistyped email cannot be corrected — is that accepted for v1?

The login is the email, there is no separate login field, and no artifact
describes editing an existing account's address. So an Admin who mistypes an
address creates an account nobody can use, and BR-014 forbids deleting it: it
stays in the list until the retention purge removes it, N years later.

Options:

1. **Accept it** — the Admin disables the mistyped account and creates the
   correct one. Nothing is invented, and the account's history stays honest. The
   list of OD-003 shows a disabled account that never signed in.
2. Allow an Admin to change the email of a Dean account that has never signed in
   successfully. Narrow and safe-looking, but it is a new rule: it needs its own
   audit action and its own line in `trebovaniya.md` §2, so it cannot be decided
   here.
3. Allow an Admin to delete a Dean account that has never signed in. Refused as
   drafted — it contradicts BR-014 and PC-11 and would be a Hard-Stop-adjacent
   change to the retention model; listed only so the decision is recorded.

**Resolution:** option 1, decided by Serhii SHYROKOV on 2026-09-26. A mistyped
address is accepted as it stands: the Admin disables the wrong account and
creates the correct one, and the wrong row leaves only with the retention purge,
N years after its creation (PC-11). Nothing is invented, BR-014 keeps its "never
deleted" rule intact, and the account's history stays honest. Changing an
account's email remains undefined; if the school ever needs it, it is a new rule
in `trebovaniya.md` §2 with its own audit action, not a decision this Story may
take.

# Notes

- **The school's domain comes from `LegitimacyState`, never from a form.**
  US-009 and US-010 already read it that way (v54), and `SignInRoutes` exposes
  it to the sign-in path without asking the Control Plane. When it is unknown the
  installation is in read-only mode, so AC-009 already covers that case and the
  creation form never needs a fallback.
- **The disabled check at sign-in already exists for the Admin.** US-008's
  Google sign-in refuses a disabled `AppUser` with the refusal category
  "account disabled", and `AppUser.IsDisabled` carries a comment naming US-012
  as the Story that first sets it. This Story adds the Dean's path and the
  Admin's actions, not a second notion of "disabled".
- **What the code does not have yet:** a factory for a Dean account, any
  password verification in the installation host, any Dean-facing screen, and
  any audit action or target type for account management — `AuditAction` holds
  `AdminSignIn`, `WorkspaceConnectionSaved` and `AccessCheckRun`, and
  `AuditTargetType` holds only `WorkspaceConnection`. The new members and the
  check constraints that list them need one amending migration, as US-009 and
  US-011 each needed (PC-2).
- **`AppUser` needs one new column** to mark a temporary password. The entity
  already carries the hash, the security stamp, the failed-attempt counter, the
  lockout end and the disabled flag, so that is expected to be the only schema
  change to the table; DB_DESIGN decides.
- **SC-4 lists the "Dean sign-in page" as its own entry** in the closed list of
  anonymous pages, and requires the antiforgery token on it. Whether it is a new
  route or a second form on the existing `/sign-in` page is left to API_DESIGN.
- **Session rules are already fixed** (SC-2): a non-persistent cookie, no
  "remember me", 60 minutes of inactivity for a Dean and 8 hours absolute, and a
  security-stamp rotation at sign-out. This Story reuses them; it does not
  restate them as Acceptance Criteria.
- **Nothing here touches Google.** No screen in this Story calls a Google API,
  and the Dean's account has no relation to the technical account of BR-015 or
  to any Classroom roster — `AppUser` is not linked to `ClassroomParticipant`,
  and neither role has "own courses" (`trebovaniya.md` §3).
- **Story-level defect of the neighbouring artifacts, worth checking at the spec
  gate:** `docs/product/epic-map.md` cites BR-014 for US-012, which is correct;
  `trebovaniya.md` §7 has no open item about Dean accounts, so nothing in the
  requirements blocks this Story.
