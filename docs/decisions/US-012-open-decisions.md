---
artifact_type: open_decisions
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T17:14:21Z
updated_at: 2026-09-26T17:14:21Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-012-manage-dean-accounts.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-012 Open Decisions

Story-level Open Decisions for US-012 (Create and manage Dean accounts). Every
item is resolved only by a human; the resolution is written next to the item and
nothing is deleted. All four were raised in the Story and resolved by the Owner
before activation; they are carried here with their `OD-` ids and resolutions
unchanged.

| Id | Raised by | Status |
|---|---|---|
| OD-001 Whether the Dean sign-in flow belongs to this Story | the Story | RESOLVED 2026-09-26 (option 1) |
| OD-002 How the Dean's password is stored and verified | the Story | RESOLVED 2026-09-26 (option 1) |
| OD-003 What the Dean accounts list shows | the Story | RESOLVED 2026-09-26 (option 1) |
| OD-004 Whether a mistyped email can be corrected | the Story | RESOLVED 2026-09-26 (option 1) |

No new Open Decision was raised by SPECIFICATION. `trebovaniya.md` §7
("Открытые вопросы") holds no item about Dean accounts: item 26 mentions the
Dean only as an audience for future documentation, which does not block this
Story.

## OD-001 Whether the Dean sign-in flow belongs to this Story

The catalog title names the Admin's side only, but no other Story in
`docs/catalog/stories.yaml` covers a Dean signing in, the US-011 security review
recorded that no Dean can sign in until US-012, and BR-014's temporary-password
rule spans both sides: an Admin types a password that the Dean must then change.

Options: (1) one Story — the Admin's four management actions together with the
Dean's sign-in, forced change and voluntary change; (2) split, leaving US-012
with accounts that cannot yet be used; (3) split so that the temporary-password
and lockout mechanics arrive last.

**Resolution:** option 1, decided by the Owner on 2026-09-26. Split across two
Stories, AC-012 could not be proven in either half. The size is accepted: the
Specification reflects it rather than trimming it.

**Impact on the Specification:** the whole of §4; FR-006 and FR-012 in
particular, and Acceptance Criteria AC-010 … AC-014.

## OD-002 How the Dean's password is stored and verified

`AGENTS.md` and SC-2 both say the Dean signs in through ASP.NET Core Identity,
but the installation host does not use Identity: `AppUser` is a hand-written
domain entity already carrying `PasswordHash`, `SecurityStamp`,
`AccessFailedCount`, `LockoutEnd` and `IsDisabled`, and SC-2 itself forbids
building the six-step check sequence on `SignInManager`, which checks
`CanSignInAsync` before the lockout and resets the counter on a correct
password. Adding a NuGet package also requires an approved Open Decision
(`AGENTS.md`, Technology Stack).

Options: (1) keep the hand-written `AppUser` and add only
`IPasswordHasher<AppUser>` from `Microsoft.Extensions.Identity.Core`, as the
Control Plane already hashes the Owner's password; (2) introduce full ASP.NET
Core Identity in the installation host; (3) hash without any Identity package.

**Resolution:** option 1, decided by the Owner on 2026-09-26. **This is the
approval `AGENTS.md` requires for `Microsoft.Extensions.Identity.Core`**, scoped
to the projects the design names. No `UserManager`, no `SignInManager`, no
`IdentityDbContext` and no Identity tables; the check sequence is an application
use case over the fields `AppUser` already holds. Option 3 was refused because it
means writing a password hasher by hand.

**Impact on the Specification:** I-1, FR-012, FR-018, S-09, S-10.

## OD-003 What the Dean accounts list shows

No artifact describes the screen. The account holds an email, a state, a creation
time, a last successful sign-in time and a UI language; the email is personal
data, so what appears and in what order is a decision, not a derivation.
`trebovaniya.md` sets no cap on the number of Dean accounts.

Options: (1) email, state, whether the password is still temporary, and the last
successful sign-in date, with no search and no paging in v1; (2) the same plus
search and paging; (3) email and state only.

**Resolution:** option 1, decided by the Owner on 2026-09-26. A school has a
handful of Deans. The temporary-password column is kept because it tells the
Admin at a glance that a newly created or reset account has not been taken over
by its Dean yet. Search and paging stay available to a later Story.

**Impact on the Specification:** FR-011.

## OD-004 Whether a mistyped email can be corrected

The login is the email, there is no separate login field, and no artifact
describes editing an existing account's address — while BR-014 forbids deleting
the account. An Admin who mistypes therefore creates an account nobody can use.

Options: (1) accept it — the Admin disables the wrong account and creates the
correct one, and the row leaves with the retention purge; (2) allow an Admin to
change the email of an account that has never signed in; (3) allow deleting an
account that has never signed in.

**Resolution:** option 1, decided by the Owner on 2026-09-26. Nothing is
invented, BR-014 keeps its "never deleted" rule and PC-11 keeps the only
deletion there is. Option 2 would be a new rule needing its own line in
`trebovaniya.md` §2 and its own audit action; option 3 contradicts BR-014
outright.

**Impact on the Specification:** FR-010, §10 (Out of Scope).
