---
artifact_type: open_decisions
story: US-012
version: 2
status: DRAFT
created_at: 2026-09-26T17:14:21Z
updated_at: 2026-09-26T17:42:53Z
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
| OD-005 How the tests compile before the implementation exists | TEST_WRITING | RESOLVED 2026-09-26 (option 1) |

Version 2 adds OD-005 only. It concerns how TEST_WRITING makes its tests
compile and changes nothing the Specification, the API design or the database
design says, so those artifacts, which consumed version 1, are not stale in
substance.

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

## OD-005 How the tests compile before the implementation exists

TC-1 puts TEST_WRITING before IMPLEMENTATION, and the test-writer Skill accepts a
failing test only when it **compiles**. The US-012 tests name production types
that do not exist yet: six use cases, the password-policy type, the password
hasher port, the new `IAppUserRepository` members, the new `AppUser` members
(`PasswordIsTemporary`, `CreateDean`, `Disable`, `ReEnable`, `ResetPassword`,
`SetOwnPassword`, `RecordFailedSignIn`, `IsLockedOut`), the new audit enum
members and factories, the three policy names and the result models.

Options: (1) a compile-only skeleton — those files exist with members throwing
`NotImplementedException`, nothing registered in DI, as US-005 (OD-002), US-007
(OD-003) and US-011 (OD-006) each did; (2) write the tests without compiling
them, deferring part of the suite until the code exists; (3) implement the
production code during TEST_WRITING.

**Resolution:** option 1, decided by the Owner on 2026-09-26. Option 2 breaks
TC-1 and the Skill's own red-phase rule, and would make TEST_WRITING stop being
evidence; option 3 merges TEST_WRITING into IMPLEMENTATION and would make the
tests be written against finished code, which `AGENTS.md` forbids.

**Scope of the skeleton**, so IMPLEMENTATION knows exactly what it inherits:

- nothing is registered in dependency injection, so no existing behaviour
  changes and no existing test changes state because of it;
- `AppUser.PasswordIsTemporary` is temporarily **excluded from the EF Core
  model** (one `Ignore` line in `AppUserConfiguration`, marked as skeleton).
  Without it EF Core would map the property by convention and every existing
  test that touches `app_user` would fail against a column the migration has not
  created yet — a red phase that says nothing about US-012. IMPLEMENTATION
  replaces that line with the real mapping, the check constraint of db-design
  §3.2 and the `AddDeanAccounts` migration;
- every skeleton member throws `NotImplementedException`; no partial behaviour
  is written, so a test that passes before IMPLEMENTATION passes for a real
  reason.

**Impact on the Specification:** none. It is a stage mechanism, not a
requirement: no functional requirement, validation rule or security requirement
is added, removed or reinterpreted.
