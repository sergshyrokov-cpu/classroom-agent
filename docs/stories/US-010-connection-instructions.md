---
id: US-010
epic: EPIC-6
title: Connection instructions for the school super-admin
slug: connection-instructions
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v79.
---

# User Story

As an **Admin** of a school's installation

I want the admin panel to show me the exact text my school's super-admin needs —
this installation's own service-account client ID, the list of scopes to
authorise, and what the technical account must be — ready to hand over

So that domain-wide delegation is set up once, correctly, without anyone
guessing values or copying a stale instruction from another school.

---

# Business Value

`trebovaniya.md` §6 is blunt about it: **the connection instruction is
individual to each school** — it carries that school's own service-account
client ID — and "одной бумажки на всех быть не может". Until the admin panel
generates it, onboarding depends on the Owner emailing values by hand, which is
exactly the error-prone step DC-2 exists to remove.

It is also how a **key rotation** reaches a school without the Owner touching
the school's server: the Owner recreates the service account, updates the client
ID on the `Installation`, and the school's instruction shows the new one no
later than the next legitimacy check — within six hours (`trebovaniya.md` §5,
v43, v54).

The page is deliberately readable when nothing else works: it is built from
`LegitimacyState`, so it stays available **in read-only mode and with an
unreachable Control Plane** (BR-026, v39, v54). A school that cannot synchronise
is precisely the school whose super-admin may need to fix delegation.

---

# Scope

**In scope:**

- the instruction page in the admin panel, Admin only — the permission-matrix
  row "Просмотр инструкции по подключению" is `✔` Admin, `✘` Dean
  (`trebovaniya.md` §2, v39);
- the school's own **service-account client ID**, read from `LegitimacyState`
  (US-005, v54) — never asked of the Control Plane at render time and never
  cached anywhere else;
- the **scope list of `trebovaniya.md` §6**, exactly: the six read-only scopes
  and nothing else, with the two the prototype once requested explicitly absent;
- what the **technical account** must be (BR-015): created by the school's
  super-admin, no person behind it, not a super-admin, read-only roles for
  Classroom and Admin Reports only, and nobody signs in to the program with it;
- the school's domain, so the reader can see which school the instruction is
  for;
- behaviour when no legitimacy check has ever succeeded and the client ID is
  therefore unknown;
- the instruction in a form the Admin can hand over (OD-002);
- Ukrainian and English — the super-admin instruction is named in NFR-073 as
  translated text;
- reading the page writes nothing: no audit row, no state.

**Out of scope:** the "check access" diagnostic and the start-up self-check —
**US-011**; any Google call at all, including verifying that delegation is in
place (US-011 again; this page states what must be configured, it does not
confirm it); changing the client ID, which is the Owner's action in the Control
Plane (US-002, US-004); the `WorkspaceConnection` settings themselves —
**US-009**, already delivered; Dean accounts (US-012); a user's UI language
choice (US-039); documentation beyond this screen — `trebovaniya.md` §7 item 26
("Документация для школы и для Владельца") is an open question and this Story
does not pre-empt it.

---

# Acceptance Criteria

## AC-001 Only an Admin sees the instruction

**Given** the installation is running

**When** the instruction page is requested

**Then**:

- an Admin reaches it; a Dean is refused; an anonymous visitor is sent to sign
  in (`trebovaniya.md` §2, SC-4);
- both the allowed-role and the forbidden-role case are tested (TC-5);
- the SC-4 closed list of anonymous endpoints gains nothing;
- the page sits in the settings section US-009 established, not in a new one.

## AC-002 The instruction carries this school's own client ID

**Given** a successful legitimacy check has recorded the `Installation`'s
service-account client ID

**When** an Admin opens the instruction

**Then**:

- the client ID shown is the one from `LegitimacyState`, rendered exactly as
  stored (US-005, v54);
- it is not requested from the Control Plane while rendering, and not stored
  anywhere else in the installation;
- the page says plainly that this value belongs to this school alone — a
  neighbouring school's instruction carries a different one (§6);
- the **OAuth web client id** of the installation's own configuration and the
  `Installation` identifier are **not** shown: §6 names three identifiers that
  must not be confused, and only the service account's belongs here.

## AC-003 The scope list is exactly the one the requirements fix

**Given** the instruction page

**When** it is rendered

**Then**:

- it lists exactly the six scopes of `trebovaniya.md` §6 —
  `classroom.courses.readonly`, `classroom.rosters.readonly`,
  `classroom.profile.emails`, `classroom.coursework.students.readonly`,
  `classroom.courseworkmaterials.readonly`, `admin.reports.audit.readonly`;
- every one is read-only, and a test asserts the rendered list against the
  requirement list, so a scope added in code without a requirements change fails
  (NFR-021, SC-8);
- `drive.file` and `classroom.profile.photos` — the two the prototype requested
  and no Epic uses — appear nowhere, and a test asserts their absence (§6, v25);
- the identity scopes of the Admin's own sign-in (`openid`, `email`, `profile`)
  are **not** in this list: delegation and sign-in are different mechanisms
  (§6, v78).

## AC-004 The instruction states what the technical account must be

**Given** the instruction page

**When** an Admin reads it

**Then** it states, in their language (BR-015, `trebovaniya.md` §9):

- the school's super-admin creates the account at onboarding, together with
  domain-wide delegation;
- no person stands behind it, it is not a super-admin, and nobody signs in to
  the program with it;
- it holds read-only roles for Classroom and Admin Reports only (OD-001);
- its address is entered by the Admin in the connection settings (US-009), and
  its domain must be the school's domain.

## AC-005 The instruction is readable when nothing else works

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated — or one whose Control Plane is unreachable

**When** an Admin opens the instruction

**Then**:

- the page is served, with everything `LegitimacyState` knows
  (BR-026 names it explicitly: "инструкцию по подключению посмотреть можно");
- no Google call and no Control Plane call is made while rendering — in
  read-only mode the program reaches Google not at all (BR-026, SC-5);
- the read-only reason is stated, as on the other admin screens, without
  implying the instruction itself is unavailable.

## AC-006 A rotated client ID appears without anyone visiting the school

**Given** the Owner has recreated the school's service account and updated the
client ID on the `Installation`

**When** the next legitimacy check succeeds and an Admin opens the instruction

**Then**:

- the new client ID is shown — no restart, no cache to clear, no action by the
  Owner on the school's server (`trebovaniya.md` §5, v43, v54);
- the page reads the current `LegitimacyState` on every request;
- a test proves the change by updating the stored state between two renders.

## AC-007 Reading the instruction changes nothing

**Given** any number of visits to the page

**When** it is rendered

**Then**:

- no audit row is written — `trebovaniya.md` §5 lists the audited actions and
  viewing this page is not one of them;
- no table is written at all, in any mode;
- rendering performs no write even when the state is incomplete (AC-008).

## AC-008 Before the first successful check the instruction says what is missing

**Given** an installation where no legitimacy check has ever succeeded, so
neither the domain nor the client ID is known

**When** an Admin opens the instruction

**Then**:

- the page is still served, and the parts that do not depend on the school —
  the scope list and the technical-account requirements — are shown in full;
- the client ID and the domain are replaced by a plain statement that the
  installation has not yet confirmed its legitimacy and the values will appear
  once it has;
- nothing is invented, guessed or taken from configuration in their place
  (BR-020's principle: these values come from the Control Plane).

## AC-009 The instruction is ready to hand over

**Given** an Admin who must send the instruction to the school's super-admin

**When** they use the hand-over affordance of OD-002

**Then**:

- the handed-over text carries the same values the page shows — the client ID,
  the scope list, the technical-account requirements and the domain;
- it carries no secret: not the service-account key, not its reference, not the
  OAuth client secret, not a session identifier (SC-7 — a Hard Stop);
- what leaves the installation is only what the Admin themselves sends: the page
  contacts no outside service (SC-13).

## AC-010 Every string is translated

**Given** an installation whose UI language is Ukrainian or English

**When** the instruction is rendered in either language

**Then**:

- every sentence comes from the translation files — the super-admin instruction
  is named in NFR-073 as translated text, and it is the longest translated text
  in the program so far;
- the client ID, the domain and the scope identifiers are **data**: they are
  rendered as they are and never translated;
- both language files carry every key this Story adds.

---

# Open Decisions

## OD-001 What the instruction says about the technical account's Workspace roles

`trebovaniya.md` §9 and BR-015 fix that the technical account holds **read-only
roles for Classroom and Admin Reports only**, and §7 item 10 — "Минимальный
набор ролей Google Workspace, достаточный техническому аккаунту" — is still
open, listed under "Проверить при внедрении": the exact minimum was never
verified on a live domain, and the prototype read data as a super-admin, which
§9 calls a leftover of experiments rather than a model.

The instruction is the one place in the program where those roles must be
written down for a human to act on, so the Story cannot avoid the question.

Options:

1. **State the requirement, not a role list.** The instruction says the account
   must be able to read Classroom and Admin Reports and must hold nothing
   beyond that, and names the scopes to delegate (AC-003) — which is what the
   super-admin actually enters in the console. No Workspace admin-role name is
   printed until §7 item 10 is verified. Nothing becomes wrong when the minimum
   turns out to be narrower than someone guessed.
2. State a concrete role set now, chosen by the implementer from Google's
   documentation. Convenient for the school, and the first thing to become
   wrong: the value is unverified, it would be printed as if it were fixed, and
   a school granting more than necessary is a security regression.
3. Leave the roles out entirely and mention only the scopes. Simplest, but it
   drops a requirement BR-015 states, and the super-admin has to infer that the
   account needs Admin Reports access at all.

Whatever is chosen, the instruction must not imply that a super-admin account,
or any account with write access, is acceptable — §9 is explicit that it is not.

**Resolution:** option 1, decided by the Owner on 2026-09-20. The instruction
states the **requirement** — the account must be able to read Classroom and
Admin Reports and must hold nothing beyond that — together with the scope list
of AC-003, which is what the super-admin actually enters in the Google console.
**No Workspace admin-role name is printed** until `trebovaniya.md` §7 item 10 is
verified on a live domain.

Consequences:

- the instruction is correct whatever the verified minimum turns out to be, and
  nothing in it has to be retracted when item 10 closes;
- a school cannot be led into granting more than necessary by a value this
  program invented;
- the text must still say explicitly that a super-admin account, and any account
  with write access, is **not** acceptable (§9);
- when item 10 is verified, adding the concrete roles is a change to the
  translation files and their test — it needs no new decision, because the
  requirement sentence already frames them.

## OD-002 How the instruction is handed over

`trebovaniya.md` §4 (Epic 6) asks the admin panel to show "готовый к передаче
super-admin школы текст". It does not say in what form the Admin passes it on,
and the form is not a detail: a downloadable file is an endpoint of its own,
with a content type, a file name and a place in the SC-4 and antiforgery rules.

Options:

1. **The page itself, with a copy-to-clipboard affordance.** No new endpoint, no
   file leaves the server, nothing to audit; the Admin pastes the text into
   whatever channel they already use. The page stays printable through the
   browser.
2. A generated file to download (`.txt` or `.pdf`). Tidier to forward, and it
   adds an endpoint that returns a document, which the API conventions then have
   to place — and a document is the kind of thing people store, so a stale copy
   outlives a rotated client ID.
3. Send it by email from the installation. Rejected on sight: the program has no
   mail channel, and SC-13 lists exactly two outbound destinations — adding a
   third is a requirements change, not a Story decision.

The choice affects the API design of this Story, so it should be resolved before
SPECIFICATION rather than at the gate.

**Resolution:** option 1, decided by the Owner on 2026-09-20. The instruction is
handed over from **the page itself, with a copy-to-clipboard affordance**. No
download endpoint is added.

Consequences:

- the API surface of this Story is one `GET`, and the SC-4 and antiforgery
  rules need no new entry;
- nothing leaves the installation on its own: what the super-admin receives is
  what the Admin themselves sends (SC-13, AC-009);
- **the copy carries exactly what the page shows** — the client ID, the scopes,
  the technical-account requirements and the domain — and no secret;
- no stored document can outlive a rotated client ID, which is the point of
  AC-006: the current instruction is always the page;
- printing stays available through the browser, so a school that wants paper is
  not blocked;
- the affordance must degrade safely: with the clipboard unavailable the
  text stays selectable on the page, so the Admin is never left without it.

---

# Notes

- **No Google call, in any mode.** The page states what must be configured; it
  never checks whether it is. Confirming delegation is "check access" (US-011),
  which is also the Story that adds the first real Google port and therefore
  inherits the US-007 security-review finding F-5.
- **Three identifiers must not be confused** (§6, v78): the service account's
  numeric client ID — the only one this page shows — the OAuth web client id in
  the installation's configuration, and the `Installation` UUID of the service
  channel. The Specification should name all three and say plainly which belongs
  here, because printing the wrong one would send a school to authorise the
  wrong client.
- The scope list is a **requirement, not a configuration**: it is fixed in §6 and
  belongs in code as a constant, with a test tying it to the requirement text.
  It must not become an installation setting a school could edit.
- The client ID and the domain come from `LegitimacyState`, which US-005 keeps
  and US-009 already reads for the connection settings. This Story reads the
  same state and writes nothing to it.
- Read-only mode needs no new enforcement here: nothing is written, so there is
  nothing to guard. The page must still show the read-only reason, as the other
  admin screens do, and a test should prove the page is served in all three
  BR-025 causes.
- The settings section of the admin panel exists since US-009 (spec I-10). This
  Story adds the second entry to it; US-011 and US-012 add theirs next to it.
- This is the longest translated text in the program so far. The Specification
  should decide how it is broken into keys — one key per paragraph rather than
  one key for the whole instruction, so a change to one sentence does not
  invalidate both translations wholesale.
- Nothing in this Story touches the Control Plane, the service channel or
  `ClassroomAgent.Contracts`.
