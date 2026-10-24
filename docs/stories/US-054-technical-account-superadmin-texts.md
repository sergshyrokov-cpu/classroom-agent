---
id: US-054
epic: EPIC-6
title: The program's texts describe the technical account as a super-admin with blocked sign-in
slug: technical-account-superadmin-texts
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Drafted by the agent on 2026-10-24 from trebovaniya.md v97 (666ece7); decisions
# 1 yes, 2a, 3 yes, 4a, 5a, 6 ok taken by the Owner the same day. Aligned with
# trebovaniya.md v97.
---

# User Story

As an **Admin**

I want the connection instruction I hand to the school's super-admin, and the
hint on the connection page, to say what v97 requires of the technical account

So that the super-admin sets up an account that can actually see the school's
courses, with interactive sign-in under it closed by the mandatory measures —
instead of following today's text to an account that sees no courses.

---

# Business Value

`trebovaniya.md` v97 (§1, §7 item 10 closed, §9 "Технический аккаунт"): the
technical account — the impersonation user in `WorkspaceConnection` — holds the
**super-admin** role, because delegated Classroom administrator roles give the
Classroom API no view of the domain's courses (verified on a live domain
2026-10-24). The program still only reads: what it can read is bounded by the
read-only domain-wide delegation scopes (§6), not by the account's roles. The one
new risk is interactive sign-in under the account, closed by five mandatory
measures (§9):

1. a random password that is stored nowhere;
2. enforced 2-step verification with no enrolment grace period and no factor
   enrolled — no backup codes, no recovery email, no recovery phone — so that
   signing in is impossible;
3. a separate organizational unit carrying these settings;
4. an alert rule for any sign-in to the account, emailed to a domain administrator;
5. the super-admin role is granted immediately before the first synchronization,
   not earlier.

§4 Epic 6, "Инструкция по подключению": the instruction carries "требования к
техническому аккаунту (раздел 9)". BR-015 and the glossary were brought to v97 in
`1469569`; the program's texts were not. Today they say the opposite:

- `ConnectionInstruction.TechnicalAccount.NoPersonBehindIt` — "він не super-admin";
- `ConnectionInstruction.TechnicalAccount.ReadOnlyRoles` — "має читати Classroom і
  Admin Reports — і не мати нічого понад це";
- `ConnectionInstruction.TechnicalAccount.NoWriteAccess` — "акаунт super-admin … не
  підходять";
- `WorkspaceConnection.ImpersonationUser.Hint` — "не super-admin … має лише права
  на читання";
- `AccessCheck.Outcome.TechnicalAccountCannotRead` — "Super-admin школи має надати
  йому права лише на читання цих даних". This text is also the synchronization
  diagnosis for the same problem (§6 v80: one problem, one name), so after a failed
  check or run the program itself would send the school to the role that sees no
  courses.

A school following today's instruction gets an empty synchronization, or a refused
one, and an instruction that forbids the only working setup. The instruction is
needed before pilot stage 5 (connecting the pilot school). The requirement does not
change; the texts are brought to it.

---

# Scope

**In scope** (Ukrainian and English alike, NFR-073):

- the technical-account requirements in the super-admin instruction (US-010
  FR-005) state, one translation key per statement (US-010 I-8):
  - the school's super-admin creates the account at onboarding, together with the
    domain-wide delegation (unchanged);
  - no person stands behind it, and nobody signs in to the program with it;
  - it gets the super-admin role, with one sentence of why: lesser Classroom roles
    do not see the domain's courses, and the program still only reads, because its
    access is bounded by the delegation scopes, not by the account's role
    (decision 3);
  - each of the five mandatory measures of §9, as *what* to do, worded after §9,
    with no Google Admin console menu paths (decision 2a);
  - the role is granted last — after the other measures are in place, immediately
    before the Admin runs "Перевірити доступ" (decision 4a);
  - its address is entered by the Admin in the connection settings, in the school's
    domain (unchanged);
- the statements that contradict v97 (`NoPersonBehindIt` in its present wording,
  `ReadOnlyRoles`, `NoWriteAccess`) leave the instruction and both translation
  files; keys whose name no longer matches their meaning are replaced by new keys,
  not reworded under the old name;
- `WorkspaceConnection.ImpersonationUser.Hint` says: the school's super-admin
  creates the account; it is not the Admin's account; no person stands behind it
  and sign-in under it is blocked; it holds the super-admin role, but the program
  only reads, bounded by the delegation scopes. No list of measures — those are in
  the instruction (decision 6);
- `AccessCheck.Outcome.TechnicalAccountCannotRead` tells the Admin that the school's
  super-admin must give the technical account the super-admin role, as the
  connection instruction says (decision 1) — on the access-check page and as the
  synchronization diagnosis alike;
- the copied instruction (US-010 OD-002, "Скопіювати інструкцію") carries the same
  statements as the page;
- the tests that pin the old meaning (for example
  `ConnectionInstructionTechnicalAccountTests.ThePage_SaysASuperAdminAccountIsNotAcceptable`)
  are rewritten to the new statements; the translation-completeness tests cover the
  new keys.

**Out of scope:**

- what the program reads, its scopes, the access check's steps and the
  classification of Google failures — unchanged (§6);
- any check by the program of the account's role, its 2-step verification or its
  organizational unit — the program cannot see them through read-only scopes and
  §9 asks for none;
- Google Admin console menu paths or screenshots (decision 2a);
- the instruction's other parts — title, intro, client ID, scope list, the two steps,
  copy button — unless a step's wording must change to match;
- `trebovaniya.md` §7 item 29 (whether delegation works with sign-in blocked) — it is
  verified on the live domain by the first access check after this Story; see OD-001;
- the deployment kit README and the ops notes in `classroom-agent-ops` — not part of
  the repository's delivery;
- no new requirements version, no NuGet package, no endpoint or schema change.

---

# Acceptance Criteria

## AC-001 The instruction says the account is a super-admin, and why

**Given** a signed-in Admin of an installation with confirmed legitimacy

**When** the Admin opens the connection instruction, in Ukrainian and in English

**Then** the technical-account requirements say that the account gets the
super-admin role, that lesser Classroom roles do not see the domain's courses, and
that the program still only reads because its access is bounded by the delegation
scopes.

## AC-002 The instruction lists the five mandatory measures

**Given** the same Admin

**When** the Admin opens the connection instruction, in Ukrainian and in English

**Then** it states each measure of §9 "Технический аккаунт" as its own statement:
random password stored nowhere; enforced 2-step verification with no grace period,
no enrolled factor, no backup codes, no recovery email or phone; a separate
organizational unit with these settings; an alert on any sign-in to the account to
a domain administrator; the role granted last, immediately before the Admin runs
the access check.

## AC-003 Nothing in the instruction or the hint forbids a super-admin any more

**Given** both translation files

**When** the instruction and the connection page are rendered in either language

**Then** no statement says the technical account must not be a super-admin, must
hold only read-only roles, or must have nothing beyond reading Classroom and Admin
Reports; the keys `ConnectionInstruction.TechnicalAccount.ReadOnlyRoles` and
`ConnectionInstruction.TechnicalAccount.NoWriteAccess` no longer exist in either
file.

## AC-004 The statements that stay are unchanged

**Given** the same Admin

**When** the Admin opens the connection instruction

**Then** it still says that the school's super-admin creates the account at
onboarding with the delegation, that no person stands behind it and nobody signs in
to the program with it, and that the Admin enters its address in the connection
settings, in the school's domain; it still prints no technical-account address.

## AC-005 The copied instruction matches the page

**Given** the same Admin

**When** the Admin copies the instruction

**Then** the copied text carries the same technical-account statements, in the
same order, as the page.

## AC-006 The connection page hint matches v97

**Given** a signed-in Admin

**When** the Admin opens the connection settings page, in Ukrainian and in English

**Then** the hint under the technical-account field says the account is created by
the school's super-admin, is not the Admin's account, has no person behind it and
blocked sign-in, and holds the super-admin role while the program only reads.

## AC-007 The "cannot read" diagnosis points to the super-admin role

**Given** an access check, or a synchronization, whose Google call is refused
because the technical account has no right to the read
(`TechnicalAccountCannotRead`)

**When** the Admin sees the outcome — on the access-check page or in the "Last
synchronization" block

**Then** the text says the school's super-admin must give the technical account the
super-admin role, as the connection instruction says — not read-only rights.

## AC-008 Everything else is unchanged

**Given** the existing connection-instruction, access-check, connection-page,
synchronization and translation tests

**When** the suite runs after the change

**Then** it stays green, apart from the assertions this Story deliberately rewrites;
every new key exists in both translation files.

---

# Open Decisions

## OD-001 Write the texts now, while §7 item 29 is still open?

**Question.** `trebovaniya.md` §7 item 29: it is not yet verified that acting as the
technical account through domain-wide delegation works while 2-step verification
with no enrolled factor is enforced on it. If it does not, measure 2 must be replaced
by another way to block sign-in, and this Story's measure texts would change with it.

- **(a) Write the texts now, after v97; if item 29 fails, a new requirements version
  and a follow-up Story (recommended).** Item 29 is verified by the first access
  check on the live domain — done by following exactly this instruction — and the
  instruction is needed before pilot stage 5.
- (b) Wait until item 29 is verified on the pilot server.

**Resolution (the Owner, 2026-10-24):** (a) — the texts follow v97 now. If item 29
fails, the replacement measure comes through a new `trebovaniya.md` version and its
own Story.

## OD-002 When the super-admin grants the role, relative to the access check

**Question.** §9 says the role is granted "непосредственно перед первой
синхронизацией". The access check ("Перевірити доступ") runs before the first
synchronization and reads Classroom too; without the role its trial read may return
an empty course list and prove nothing.

- **(a) The instruction says: grant the role as the last step, after the other
  measures, immediately before the Admin runs the access check (recommended).** The
  access check then verifies the role and item 29 at once, and the first
  synchronization follows it.
- (b) Literally "before the first synchronization"; the access check runs before the
  role is granted.

**Resolution (the Owner, 2026-10-24):** (a).

## OD-003 Which texts are in scope

**Question.** The request named the instruction's technical-account statements and
the connection-page hint. Two more texts contradict v97: `ReadOnlyRoles` in the
instruction and `AccessCheck.Outcome.TechnicalAccountCannotRead`, which is also the
synchronization diagnosis and today advises read-only rights.

- **(a) Include both (recommended).** Otherwise the program's own diagnosis sends the
  school back to the setup that sees no courses.
- (b) Only the texts named in the request.

**Resolution (the Owner, 2026-10-24):** (a).

## OD-004 How detailed the measures are

- **(a) The five measures of §9 as *what* to do, with no console menu paths
  (recommended).** Google changes its console menus without notice, and §9 does not
  define paths; adding them would need a new requirements version.
- (b) The measures plus Google Admin console menu paths.

**Resolution (the Owner, 2026-10-24):** (a). The instruction also explains in one
sentence why the account needs the super-admin role (decision 3, yes), and the
connection-page hint carries the wording proposed in the discovery (decision 6, ok).

---

# Notes

- Origin: `trebovaniya.md` v97 (`666ece7`); derived documents aligned in `1469569`;
  memory note "tech account = super-admin" — "next resx Story before pilot stage 5".
- Code today: `src/ClassroomAgent.Web/Security/ConnectionInstructionTextKeys.cs`
  (`TechnicalAccountStatements`, rendered by
  `src/ClassroomAgent.Web/Views/ConnectionInstruction/Index.cshtml`; the XML comments
  still cite §7 item 10 as unverified and must follow the new meaning),
  `WorkspaceConnectionTextKeys.TechnicalAccountHint`, `AccessCheckTextKeys`,
  `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` and `.en.resx`.
- Tests today: `tests/ClassroomAgent.Tests/TestInfrastructure/ConnectionInstructionTestData.cs`
  (`TechnicalAccountStatements`, `All`), `ConnectionInstructionTechnicalAccountTests`,
  `ConnectionInstructionHandoverTests`, `ConnectionInstructionTranslationTests`,
  `WorkspaceConnectionPageTests`, `AccessCheckTranslationTests`.
- `tests/ClassroomAgent.Tests/Infrastructure/Google/GoogleClassroomReaderTests.cs`
  line ~110 has a comment saying the impersonation user is "never a super-admin" —
  a comment in a file whose test is not otherwise touched; the specification decides
  whether it is in scope as a supporting change.
- The texts are user-visible and go only to the school's own staff; no school data
  leaves the installation (SC-13 unchanged).
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint changes); DB_DESIGN
  NOT_APPLICABLE (no schema change; translations are resource files).
- Manual check: the live-domain access check after this Story, on the pilot server,
  is also the verification of §7 item 29 — record it in the implementation report's
  manual section, like US-047, US-049 and US-050.
