---
artifact_type: specification
story: US-012
version: 1
status: APPROVED
created_at: 2026-09-26T17:14:21Z
updated_at: 2026-09-26T17:19:45Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-012-open-decisions.md
    version: 1
supersedes: null
---

# US-012 Specification — Create and manage Dean accounts

## 1. Overview

Until this Story an installation has one kind of human user. US-008 gave the
Admin a Google sign-in and creates their `AppUser` just in time; US-009, US-010
and US-011 gave that Admin three settings screens. The role the product was
designed around — Декан, the учебная часть — cannot sign in at all.

US-012 adds both halves of that role's access: the Admin's screen that creates a
Dean account and afterwards disables, re-enables and resets the password of one
(BR-014), and the Dean's own sign-in with the check sequence SC-2 fixes step by
step, the forced change of the temporary password, and the voluntary change of
their own password.

Nothing here reads teaching data, and nothing here calls Google. A Dean account
is a local login and password inside the installation; the technical account of
BR-015, which Google data is actually read as, is a different thing entirely and
is not touched.

### Interpretations

Where `trebovaniya.md` leaves a mechanism unstated, this Specification records
the reading it applies. Each one is a candidate objection at
`HUMAN_SPEC_APPROVAL`.

- **I-1 "Identity" names the password primitive, not the framework.**
  `trebovaniya.md` §2 and SC-2 both say the Dean signs in "через ASP.NET Core
  Identity". The installation host has no Identity today: `AppUser` is a domain
  entity with its own `PasswordHash`, `SecurityStamp`, `AccessFailedCount`,
  `LockoutEnd` and `IsDisabled`, and SC-2 itself forbids building the check
  sequence on `SignInManager`. OD-002 resolves the contradiction in favour of
  the hasher only. This Specification therefore requires Identity's
  `IPasswordHasher<AppUser>` and nothing else from Identity (FR-018).
- **I-2 The temporary password is a state of the account, not a second column of
  secrets.** BR-014 says the password an Admin sets "is temporary" and must be
  changed at the next sign-in. That is one boolean state beside the single hash;
  there is never a second stored password (FR-006).
- **I-3 "The next login" means the next successful authentication.** BR-014 v64
  says the change is обязательна при первом входе. Step 5 of the SC-2 sequence
  places it after the disabled check and after the password check, so a Dean who
  never gets that far is never shown the form (FR-012).
- **I-4 Disabling ends the account's existing sessions.** BR-014 makes disabling
  the substitute for deletion, and §2 v64 describes a disabled account as one
  that cannot be used. The session cookie is already validated against the
  account's security stamp on every request (BR-026 v79), so disabling rotates
  the stamp (FR-007). Leaving an open session alive until its own expiry would
  make "disabled" mean "cannot sign in again", which is not what the rule says.
- **I-5 A reset rotates the stamp too.** The point of a reset is that the old
  password stops working (BR-014); a session opened with it must stop as well
  (FR-009).
- **I-6 The refusal message is one message.** SC-2 requires that a refusal never
  reveals whether the login exists or is locked. This Specification treats the
  common message as a single translated string used by steps 1, 2 and 3 alike,
  and requires that the three paths differ in nothing else observable (FR-012,
  S-05).
- **I-7 No rule says a voluntary new password must differ from the current one.**
  BR-014 forbids only the new password equal to the **temporary** one. This
  Specification does not add the other rule (VR-003); adding it would be
  inventing a requirement.
- **I-8 An Admin never appears on the Dean screen.** The permission matrix gives
  the Admin the management of *Deans'* accounts (`trebovaniya.md` §2, v64), and
  an Admin has no password to reset (SC-2). Every management action therefore
  refuses an account whose role is not Dean (VR-005).
- **I-9 The installation's default language is what a new Dean gets.** Choosing a
  language is US-039; `AppUser.UiLanguage` already exists and already defaults,
  so creation sets the installation default and this Story adds no language
  screen (FR-002, §10).
- **I-10 "Never deleted" is a property of the finished code, not only of the
  screen.** AC-008 is specified as the absence of any delete path in the
  application layer and the repository, not merely the absence of a button
  (FR-010).
- **I-11 The audit list grows only by what this Story performs.** `AuditAction`
  holds three members today and `AuditTargetType` one; the new members are those
  SC-11 names for Dean accounts and no others (FR-017). Whether their check
  constraints need an amending migration is DB_DESIGN's to decide, as it was in
  US-009 and US-011 (PC-2).

## 2. Business Goal

A school runs the program for its учебная часть. The Dean is the person who
looks at courses, journals and Meet statistics and exports them; the Admin is a
Workspace administrator who sets the installation up and then steps back
(`trebovaniya.md` §2: "различаются они правом настраивать, а не правом
смотреть"). Without this Story the Admin would have to do the Dean's daily work
from an account that also holds every configuration right — exactly the
concentration the two-role matrix exists to prevent.

The Story also ends a compromise that three security reviews have carried: the
forbidden-role test for the Admin-only settings pages has had to construct a
Dean principal by hand, because no Dean could sign in (US-010 F-1, carried to
US-011 F-1 with owner US-012). AC-014 replaces it with a real session.

## 3. Business Flow

**The Admin creates an account.** The Admin opens the Dean accounts screen, sees
the installation's Deans with their state, and submits a work email in the
school's domain together with a temporary password. The program refuses an
address outside the domain, a string that is not an email, and an address that
already has an account of any role. On success the account exists, is active,
holds a temporary password and has never signed in.

**The Dean signs in for the first time.** They open the sign-in page, enter the
email and the temporary password. The program walks the six steps of SC-2 in
order; at step 5 it shows the forced change form instead of signing them in. The
Dean chooses a password that obeys the policy and differs from the temporary
one. From that moment only the Dean knows it.

**Daily use.** The Dean signs in and works. They may change their own password
at any time, including while the installation is in read-only mode. They cannot
open any of the three settings screens.

**Something goes wrong.** Five failed attempts lock sign-in for fifteen minutes;
the lock lifts by itself. If the Dean forgets the password, the Admin resets it
to a new temporary one — which also clears the lock, but does not re-enable a
disabled account. If the Dean leaves the school, the Admin disables the account;
it stays for history and audit until the retention purge removes it N years
later (PC-11).

**Read-only mode.** Everything the Admin does here is blocked (BR-026): the
school's licence state is not a matter the program lets a school work around.
The Dean's sign-in bookkeeping and the Dean's own password change keep working,
because the closed list of service writes names them.

## 4. Functional Requirements

### FR-001 What this Story adds

Two areas, both server-rendered (AD-3, the stack in `AGENTS.md`):

1. **The Dean accounts screen**, reachable by an Admin only, offering the four
   actions BR-014 allows — create, disable, re-enable, reset password — over the
   installation's Dean accounts.
2. **The Dean's own pages**: the sign-in page (anonymous), the forced change of a
   temporary password, and the change of one's own password (authenticated).

Routes, HTTP methods, status codes and response shapes are fixed by API_DESIGN
against this Specification. No `/api/v1` surface is required by any Acceptance
Criterion of this Story.

### FR-002 What a Dean account is

A Dean account is an `AppUser` with:

- `Role = Dean` and `SignInMethod = Password` — the existing check constraint
  already ties an Admin to Google, so a password account is a Dean account;
- `Email` and `NormalizedEmail` holding the work email, stored lower-cased, as
  the entity already requires; **there is no separate login field**
  (`trebovaniya.md` §3, v55);
- `PasswordHash` non-null, holding only a hash (FR-018);
- `SecurityStamp`, `AccessFailedCount`, `LockoutEnd`, `IsDisabled`,
  `LastSuccessfulSignInAt` — all already on the entity;
- `UiLanguage` set to the installation's default at creation (I-9);
- **a mark that the stored password is temporary** (I-2). The entity has no such
  field today; the entity model and the migration are DB_DESIGN's (PC-2).

The account carries no link to a `ClassroomParticipant`, no notion of "own
courses" and no per-course scoping (`trebovaniya.md` §3; AGENTS.md Domain
Essentials).

### FR-003 Creating a Dean account

An Admin submits an email and a temporary password. The use case evaluates, in
this order, and stops at the first refusal:

1. **read-only mode** — the guard is consulted as the first statement, before any
   repository call (FR-015, AD-6);
2. **the email is syntactically an email** and its local part is not empty
   (VR-001);
3. **the email's domain equals the school's domain** (FR-004);
4. **the password obeys the policy**, evaluated against the submitted email as
   the login (FR-005);
5. **no account exists with that normalized email** — any role, any state
   (VR-001).

On success, in one transaction: the `AppUser` is created with the fields of
FR-002, the password is hashed (FR-018), the temporary mark is set (FR-006), and
one audit row records the creation (FR-017). Nothing else is staged in that
transaction (carried US-009 F-2).

### FR-004 The login is a work email in the school's domain

The school's domain is the one the last successful legitimacy check reported —
the `Domain` of the installation's legitimacy state, as US-009 and US-010 already
read it (v54). It is never typed into this screen and never taken from the
submitted address.

When no legitimacy check has ever succeeded the domain is unknown; that is one
of the three causes of read-only mode (BR-025), so step 1 of FR-003 has already
refused the request. **No path falls back to accepting any domain** (S-04).

The comparison is case-insensitive over the normalized form, so
`Dean@school.example` and `dean@school.example` are one login. The domain is
compared for equality; a subdomain of the school's domain is not the school's
domain.

### FR-005 The password policy

One policy for every password this Story sets — creation, reset, forced change,
voluntary change — and it is the policy SC-2 states for the Owner and the Dean
together (`trebovaniya.md` §2, §9, v62, v64, v65):

- **length 15 to 128 characters**, counted in characters and not bytes; spaces
  are allowed and are not trimmed away;
- **no composition rules**: no required digit, upper-case letter or symbol;
- the password **may not equal the login**, compared case-insensitively;
- the password **may not contain the login, nor the part of the email before
  `@`**, compared case-insensitively; the containment check runs only when that
  string is **at least 4 characters** long, and a shorter one is checked for
  equality only (v65);
- **no check against an external breached-password service** (SC-13 — the school's
  data leaves the installation only to Google and the Control Plane).

Adding a rule, lowering the minimum length or introducing a permanent lockout is
a defect (S-06).

### FR-006 The temporary password

The password an Admin types at creation (FR-003) or at a reset (FR-009) is
**temporary**: the account is marked, and the mark means that the next successful
authentication leads to the forced change form instead of a session (FR-012 step
5).

The forced change:

- requires a new password obeying FR-005 **and** not equal to the temporary one
  (VR-003, BR-014 v64);
- on success clears the mark, replaces the hash, rotates the security stamp,
  signs the Dean in, and writes one audit row for a password change by the Dean
  themselves (FR-017);
- is the only page a Dean in that state may reach: any other authenticated
  request of theirs is redirected back to it until the change is done.

Only the Dean knows the password afterwards. A flow that leaves the Admin with a
working password is a defect (SC-2, S-07).

### FR-007 Disabling a Dean account

An Admin disables an active Dean account. The use case consults the read-only
guard first (FR-015), then, in one transaction: sets the disabled state, rotates
the account's security stamp, and writes one audit row.

The password hash, the failed-attempt counter, the lockout end and the last
successful sign-in time are **left untouched** — the account must stay exactly as
it was for history, audit and the retention clock (BR-014, PC-11).

Rotating the stamp ends that Dean's open sessions at their next request (I-4).

### FR-008 Re-enabling a Dean account

An Admin re-enables a disabled Dean account: the disabled state is cleared, one
audit row is written, and **nothing else changes**. The account signs in with the
password it had; re-enabling does not set a temporary password and does not
clear a lockout (BR-014). The action is offered only for a disabled account
(VR-005).

### FR-009 Resetting a Dean's password

An Admin submits a new temporary password for a Dean account, active or
disabled, locked out or not. In one transaction: the hash is replaced, the
temporary mark is set (FR-006), `AccessFailedCount` is reset to zero and
`LockoutEnd` is cleared (BR-014 v64), the security stamp is rotated (I-5), and
one audit row is written.

**A disabled account stays disabled.** A reset never re-enables (BR-014, S-08).

### FR-010 No path deletes an account

There is no use case, repository method, controller action or UI control that
deletes an `AppUser`. The only deletion of an account is the retention purge
(PC-11), which this Story does not implement and does not change (I-10).

### FR-011 The Dean accounts list

The screen lists every `AppUser` with role Dean, active and disabled alike, each
row showing (OD-003, resolved option 1):

- the email;
- the state — active or disabled;
- whether the stored password is still temporary;
- the date of the last successful sign-in, or an indication that there has been
  none.

No search and no paging in this version. No Admin account appears on the screen
and no action is offered on one (I-8).

Viewing the list is **not** blocked in read-only mode (FR-015, BR-026).

### FR-012 The Dean sign-in check sequence

The sign-in page is anonymous (SC-4) and carries the antiforgery token
(VR-004). On submission the steps below run in this order, each only if the
previous one passed (`trebovaniya.md` §2 v66, SC-2):

| # | Condition | Response | Counter | Audit refusal category |
|---|---|---|---|---|
| 1 | no account with that normalized email | the common refusal message | unchanged | unknown login; actor anonymous, no identifier |
| 2 | a sign-in lockout is in force | the common refusal message; **the password is not verified** | unchanged | locked out |
| 3 | the password does not match the hash | the common refusal message | **+1** | wrong password |
| 4 | the account is disabled | the "account disabled, contact your Admin" message | **unchanged** — neither incremented nor reset | account disabled |
| 5 | the stored password is temporary | the forced change form (FR-006) | reset to zero | — (no refusal; the sign-in has succeeded) |
| 6 | otherwise | the Dean is signed in | reset to zero | — (a succeeded sign-in row) |

and:

- the "account disabled" message is reachable **only** at step 4 — with the
  correct password and no lockout in force (S-05);
- steps 1, 2 and 3 are indistinguishable to the caller: the same message, the
  same status code, the same redirect target, and no branch that skips or adds
  a password verification in a way an observer could time (S-05, VR-006);
- at steps 5 and 6 the last successful sign-in time is recorded — this is the
  value the retention clock counts from (PC-11);
- the sequence is written as an application use case over the fields of FR-002.
  It is **not** built on `SignInManager.PasswordSignInAsync` or
  `CheckPasswordSignInAsync`, and the disabled state is expressed neither through
  `CanSignInAsync` nor through the lockout fields (SC-2, I-1, S-09).

An Admin signing in through US-008's Google path is unaffected; that path keeps
its own refusal categories and already refuses a disabled account.

### FR-013 Lockout

Five consecutive failed attempts (step 3 of FR-012) lock sign-in for **fifteen
minutes**. While the lock is in force step 2 refuses every attempt, including one
with the correct password. The lock **expires by itself**; there is no permanent
lockout and no Admin action is needed to lift one, though a reset clears it
(FR-009). A successful sign-in resets the counter to zero.

Time is read from the injected `TimeProvider`, never from `DateTimeOffset.Now`
(TC-1 conventions; it is also what makes AC-011 testable without waiting).

### FR-014 A Dean changes their own password

An authenticated Dean whose password is not temporary submits the current
password and a new one. The change succeeds only when the current password
verifies and the new one obeys FR-005. On success: the hash is replaced, the
security stamp is rotated, and one audit row is written.

This works **in read-only mode** — it is on the closed list of service writes
BR-026 permits, together with the sign-in bookkeeping of FR-012 and FR-013.

An Admin has no such page and no password (SC-2, S-07).

### FR-015 Read-only mode

The four management actions of FR-003, FR-007, FR-008 and FR-009 each consult
`IReadOnlyModeGuard` as the **first statement of the use case**, before any
repository call, any hash computation and any transaction (AD-6, TC-5). In
read-only mode each is refused with the host's existing read-only response, no
`AppUser` row is created or modified, and the refusal writes its audit row —
one of the permitted service writes (BR-026).

Not blocked in read-only mode: viewing the list (FR-011), the whole of the Dean
sign-in path including the failed-attempt counter and the lockout (FR-012,
FR-013), the forced change of a temporary password (FR-006) and the voluntary
change (FR-014).

Read-only mode is never enforced by hiding a screen or a button (AD-6).

### FR-016 Authorization

- The Dean accounts screen and all four actions require an authenticated user
  with role **Admin**, through a policy declared like the three existing
  settings policies and registered beside them.
- The Dean's own password-change page requires an authenticated user with role
  **Dean**.
- The sign-in page and its submission are **anonymous** — they join the closed
  list of SC-4 — and the forced change form is reachable only by the account
  that has just authenticated at step 5.
- A signed-in Dean receives **403** from the workspace-connection page, the
  connection-instruction page and the check-access page, on every method those
  pages expose (AC-014). The same Dean keeps receiving 200 from the
  legitimacy-status view, which both roles may see.
- Deny by default stays in force: the fallback policy already requires an
  authenticated user, and every new endpoint declares its own policy (SC-4).

### FR-017 Audit

One row per action, with no personal data (SC-10, SC-11):

| Action | Actor | Target | Outcome |
|---|---|---|---|
| Dean account created | the Admin | the new account's internal id | succeeded |
| Dean account disabled | the Admin | the account's internal id | succeeded |
| Dean account re-enabled | the Admin | the account's internal id | succeeded |
| Dean account password reset | the Admin | the account's internal id | succeeded |
| Dean changed own password | the Dean | their own internal id | succeeded |
| Dean sign-in | the account | the account's internal id | succeeded |
| Dean sign-in refused | the account, or **anonymous** with no identifier when no account matched | the account's internal id, or none | refused, with the category of FR-012 |
| any of the four management actions refused in read-only mode | the Admin | the account's internal id where one exists | refused, category read-only |

- No row carries an email address, a name, a typed login, a password or a hash
  (SC-10).
- Audit rows are never updated (SC-11).
- The `AuditAction` and `AuditTargetType` closed lists grow by exactly the
  members these rows need — the account-management actions, the Dean sign-in,
  and an account target type; `AuditRefusalCategory` already holds
  `AccountDisabled` and `ReadOnlyMode`, and grows by the sign-in categories it
  lacks. Which members are new, and whether their check constraints need an
  amending migration, is DB_DESIGN's (PC-2, I-11).
- A refusal commits its audit row with nothing else staged (carried US-009 F-2).

### FR-018 How a password is stored and verified

`IPasswordHasher<AppUser>` from `Microsoft.Extensions.Identity.Core`, as the
Control Plane already hashes the Owner's password (OD-002, resolved option 1).

- The package is added to the projects the design names and to no others; this
  Specification records the Owner's approval, which `AGENTS.md` requires for a
  NuGet package.
- A plaintext password exists only inside the request that carries it: it is
  never stored, never logged, never written to an audit row, never returned in a
  response and never echoed into a re-rendered form (SC-10, VR-006).
- Verification uses the hasher's own comparison. When the hasher reports that a
  stored hash needs rehashing, the use case may rehash it inside the same
  successful-sign-in transaction; it is never rehashed on a failed attempt.
- No `UserManager`, no `SignInManager`, no `IdentityDbContext`, no Identity
  tables (I-1, S-09).

### FR-019 Sessions

This Story reuses the session rules already fixed and adds none:
non-persistent cookie, no "remember me", 60 minutes of inactivity for a Dean, 8
hours absolute, and the security stamp validated on every request (SC-2, BR-026
v79).

It rotates the security stamp in three places — disabling (FR-007), a reset
(FR-009) and any password change (FR-006, FR-014) — so that sessions resting on
the previous state end at their next request.

### FR-020 Translations

Every user-visible string of both areas comes from the Ukrainian and English
translation files, with no literal in a controller, a view or the application
layer (NFR-073): the list and its column labels, the creation form, the
confirmation and refusal messages of all four management actions, the sign-in
page, the **one** common refusal message, the "account disabled" message, the
forced change form, the voluntary change form, and every validation message of
§6.

Both files hold the same key set, and keys follow the existing
`Area.Element[.Detail]` convention. The language is the signed-in user's, falling
back to the installation's default; on the anonymous sign-in page it is the
installation's default.

## 5. Acceptance Criteria

The Story's sixteen criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | The screen is Admin-only, forbidden to a Dean | FR-011, FR-016 |
| AC-002 | An Admin creates a Dean account on a work email | FR-002, FR-003, FR-004, FR-018 |
| AC-003 | Creation refused: wrong domain, not an email, already taken | FR-003, FR-004, VR-001 |
| AC-004 | Every password obeys the SC-2 policy | FR-005, VR-002 |
| AC-005 | An Admin disables an account | FR-007 |
| AC-006 | An Admin re-enables an account | FR-008 |
| AC-007 | A reset clears the lockout and never re-enables | FR-009 |
| AC-008 | No path deletes an account | FR-010 |
| AC-009 | Read-only refuses all four management actions | FR-015 |
| AC-010 | The six-step sign-in sequence in order | FR-012 |
| AC-011 | Lockout is 5 attempts / 15 minutes, never permanent | FR-013 |
| AC-012 | A temporary password must be changed at the next sign-in | FR-006, FR-012 step 5 |
| AC-013 | A Dean changes their own password, in read-only too | FR-014, FR-015 |
| AC-014 | A Dean is forbidden from the three settings pages over HTTP | FR-016 |
| AC-015 | Every user-visible string is translated | FR-020 |
| AC-016 | The audit trail carries what SC-11 requires, and no personal data | FR-017 |

## 6. Validation Rules

### VR-001 The email of a new account

Required; trimmed of surrounding whitespace; at most the length the `AppUser`
email column already allows; must parse as an email address with a non-empty
local part and a domain; the domain must equal the school's domain (FR-004);
after normalization it must not match an existing account of any role or state.

A refusal re-renders the form with the reason. The refusal for an address
already in use says only that the address cannot be used — it does not reveal
the role, the state or the existence of a particular account (S-03).

### VR-002 A submitted password

Required; not trimmed; 15 to 128 characters; the containment and equality rules
of FR-005 evaluated against the account's email as the login. Validation runs
before business logic and before hashing (SC-10: "all external input is
validated before it reaches business logic").

A refusal names the rule that was broken. The rejected value is never written to
a log, an audit row or the re-rendered form (VR-006).

### VR-003 The new password at a forced change

VR-002, and in addition: it must not equal the temporary password it replaces
(BR-014 v64). The comparison is made by verifying the **new** password against
the hash still stored for the account: a match means it equals the temporary one
and is refused. No plaintext password is kept anywhere to make this comparison
possible (S-10).

No rule forbids a **voluntary** new password equal to the current one; none is
added (I-7).

### VR-004 Antiforgery

Every form in this Story carries the antiforgery token, the anonymous sign-in
page included (SC-4, login-CSRF). A missing or invalid token is refused under
the host's existing global rule.

### VR-005 The target of a management action

The account id submitted with a disable, re-enable or reset must name an
existing `AppUser` **whose role is Dean**; an Admin account, or an id that
matches nothing, is refused without disclosing which (I-8, S-03). Re-enabling
requires a disabled account and disabling an active one; the opposite is
refused, so a stale screen cannot flip a state by accident.

### VR-006 What is never echoed

No response, re-rendered form, log line, audit row or error page carries a
submitted password, a password hash, a typed login that matched no account, or
any statement distinguishing "no such login" from "wrong password" from "locked
out" (SC-10, S-05).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | Every endpoint of this Story declares an authorization policy; anonymous access is limited to the sign-in page, which joins the closed list | SC-4 |
| S-02 | The four management actions require role Admin; the voluntary password change requires role Dean | `trebovaniya.md` §2 matrix, SC-1 |
| S-03 | A refusal never discloses the existence, role or state of another account | SC-2, SC-10 |
| S-04 | The school's domain comes from the legitimacy state, never from user input, and there is no fallback that accepts any domain | BR-020, FR-004 |
| S-05 | Steps 1, 2 and 3 of the sign-in sequence are indistinguishable; "account disabled" appears only with the correct password and no lockout | SC-2 v65 |
| S-06 | The password policy is exactly FR-005: no weaker length, no composition rule, no permanent lockout, no external breach check | SC-2, SC-13 |
| S-07 | After the forced change only the Dean knows the password; an Admin has no password and no reset of their own | SC-2 |
| S-08 | A reset never re-enables a disabled account; re-enabling never clears a lockout | BR-014 v64 |
| S-09 | The sequence is not built on `SignInManager`/`CheckPasswordSignInAsync`, and the disabled state uses neither `CanSignInAsync` nor the lockout fields | SC-2 |
| S-10 | Passwords are stored only as a hash produced by `IPasswordHasher<AppUser>`; no plaintext is stored, logged, audited or returned | SC-2, SC-10, OD-002 |
| S-11 | Audit rows carry no personal data and are never updated; a refused sign-in names an internal id or "anonymous" | SC-10, SC-11 |
| S-12 | Read-only mode is enforced in `Application`, not by hiding UI, and only the writes BR-026 permits still run | AD-6, BR-026 |
| S-13 | Disabling, a reset and any password change rotate the security stamp, ending sessions resting on the old state | SC-2, BR-026 v79 |
| S-14 | Nothing in this Story calls Google or sends data to any service; the only destinations remain Google and the Control Plane, neither of which this Story contacts | SC-13 |

## 8. Error Handling

- **Validation refusals** (§6) re-render the form with translated messages; no
  exception is used for an expected outcome (AD-9).
- **Read-only refusals** use the host's existing read-only response, the same one
  US-009 and US-011 produce (FR-015).
- **A refused sign-in** answers with the common message and no indication of
  cause; the audit row carries the cause (FR-012).
- **A management action on an account that has changed underneath** — already
  disabled, already active, or deleted by a purge between the screen and the
  submission — is refused with a translated message and no change (VR-005).
- **A concurrency conflict** on the account row is resolved by the existing
  concurrency token: the action is refused and the Admin repeats it from a fresh
  screen.
- **A duplicate email that slips past the check** (two Admins creating the same
  account at once) surfaces as the existing unique-index violation and is turned
  into the same refusal as VR-001, never a 500 — the pattern US-008 already uses
  for its race.
- No error message, log line or page reveals a password, a hash or which step of
  FR-012 refused (VR-006).

## 9. Non-Functional Requirements

- **Localisation** — NFR-073, as FR-020 states it.
- **Logging** — structured, no personal data, no password, no hash, no typed
  login (SC-10, DC-10). A failed sign-in is logged as an event with a category,
  never with the address attempted.
- **Password hashing cost** is the hasher's own default; hashing runs only after
  validation, and never on a step that has already refused (FR-012 step 2).
- **Time** comes from the injected `TimeProvider` (FR-013).
- **Sessions** — SC-2, unchanged by this Story (FR-019).
- **Tests** follow TC-2 (real PostgreSQL via Testcontainers), TC-5 (an
  allowed-role and a forbidden-role test per protected endpoint, read-only proven
  in the Application layer) and TC-4 — no test here has any reason to reach
  Google, since nothing in this Story calls it.

## 10. Out of Scope

- Deleting a Dean account (BR-014 forbids it; PC-11 owns the only deletion).
- Changing an existing account's email (OD-004, resolved option 1: not done, and
  a new rule in `trebovaniya.md` §2 would be needed).
- Choosing a UI language — US-039; a new account gets the installation's default
  (I-9).
- The Dean's working screens: courses, journals, Meet statistics, exports,
  report templates, starting a synchronization (EPIC-1, EPIC-3, EPIC-4).
- Anything about Admin accounts: an Admin has no local password, no password
  column and no reset; an Admin `AppUser` is still created only by a first
  successful Google sign-in (SC-2, US-008).
- Self-service password recovery: the installation sends no mail, and the only
  reset is the Admin's (BR-014, SC-13).
- The retention purge itself (PC-11), and any change to it.
- Any Teacher or Student account — deferred to Epic 7 (BR-001).

## 11. Open Decisions

All four arrived from the Story resolved by the Owner on 2026-09-26 as option 1,
before activation. They are carried into `docs/decisions/US-012-open-decisions.md`
with their ids and resolutions unchanged. No new Open Decision was raised by this
Specification, and `trebovaniya.md` §7 holds no open item about Dean accounts.

| Id | Decision | Impact on this Specification |
|---|---|---|
| OD-001 | One Story: the Admin's four actions **and** the Dean's sign-in, forced change and voluntary change | The whole of §4; without it FR-006 and FR-012 would be split across two Stories and AC-012 would be unprovable in either |
| OD-002 | The hand-written `AppUser` plus `IPasswordHasher<AppUser>` from `Microsoft.Extensions.Identity.Core`; no `UserManager`, no `SignInManager` | I-1, FR-012, FR-018, S-09, S-10 — and the package approval `AGENTS.md` requires |
| OD-003 | The list shows email, state, whether the password is temporary, and the last successful sign-in; no search, no paging | FR-011 |
| OD-004 | A mistyped email is not corrected: the Admin disables the wrong account and creates the correct one | FR-010, §10 |

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-011, FR-016 | VR-005 | S-01, S-02 |
| AC-002 | FR-002, FR-003, FR-004, FR-018 | VR-001, VR-002 | S-04, S-10 |
| AC-003 | FR-003, FR-004 | VR-001 | S-03, S-04 |
| AC-004 | FR-005 | VR-002, VR-003 | S-06 |
| AC-005 | FR-007 | VR-005 | S-13 |
| AC-006 | FR-008 | VR-005 | S-08 |
| AC-007 | FR-009 | VR-002, VR-005 | S-08, S-13 |
| AC-008 | FR-010 | — | — |
| AC-009 | FR-015 | — | S-12 |
| AC-010 | FR-012 | VR-004, VR-006 | S-03, S-05, S-09 |
| AC-011 | FR-013 | — | S-05, S-06 |
| AC-012 | FR-006, FR-012 | VR-003 | S-07, S-13 |
| AC-013 | FR-014, FR-015 | VR-002 | S-07, S-12, S-13 |
| AC-014 | FR-016 | — | S-01, S-02 |
| AC-015 | FR-020 | — | — |
| AC-016 | FR-017 | VR-006 | S-11 |
| — (storage and session mechanics) | FR-018, FR-019 | — | S-09, S-10, S-13, S-14 |

Requirement sources: `trebovaniya.md` v79 §2 (роли, матрица прав, сценарий
входа), §3 (`AppUser`), §4 (Epic 6), §5 (аудит, срок хранения, язык интерфейса),
§8 (валидация, сессии), §9 (парольная политика, Control Plane); BR-001, BR-002,
BR-013, BR-014, BR-020, BR-022, BR-025, BR-026; NFR-062, NFR-073; AD-3, AD-6,
AD-8, AD-9; API-7; SC-1, SC-2, SC-4, SC-10, SC-11, SC-13; PC-2, PC-11; DC-10;
TC-1, TC-2, TC-4, TC-5.
