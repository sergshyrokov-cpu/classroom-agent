---
artifact_type: open_decisions
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-20T19:33:23Z
updated_at: 2026-09-20T19:33:23Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-010-connection-instructions.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-010 Open Decisions

Story-level Open Decisions for US-010 (Connection instructions for the school
super-admin). Every item is resolved only by a human; the resolution is written
next to the item and nothing is deleted.

| Id | Raised by | Status |
|---|---|---|
| OD-001 What the instruction says about the technical account's Workspace roles | the Story | RESOLVED 2026-09-20 (option 1) |
| OD-002 How the instruction is handed over | the Story | RESOLVED 2026-09-20 (option 1) |

## `trebovaniya.md` §7 — the open items and this Story

This Story **touches** §7 item 10, and that is why OD-001 exists.

- **Item 10 — "Минимальный набор ролей Google Workspace", достаточный техническому
  аккаунту** (Проверить при внедрении). The instruction is the one screen in the
  program where the technical account's Workspace roles would have to be written
  down for a human to act on, so the Story could not route around the item.
  OD-001 resolves the conflict *without* resolving item 10: the instruction states
  the **requirement** BR-015 already fixes and prints **no role name**. Item 10
  stays open in `trebovaniya.md` and is closed only by a new version of that
  document, after verification on a live domain.
- **Item 26 — documentation for the school and for the Owner** (Решить). §7 names
  the super-admin instruction the program itself shows as something that already
  exists *apart from* the documentation question. This Story builds exactly that
  screen and nothing beyond it, so item 26 is untouched and must not be
  pre-empted here.
- Item 14 (submissions of a removed student) is a Classroom API question and does
  not touch this Story, which makes no Google call at all.

The rest of what this Story implements is **closed** in the requirements, not
open: §6 (the service-account model, the three identifiers that must not be
confused, the fixed scope table and the two scopes new schools do not authorise),
§4 Epic 6 (the instruction itself), §2 (the permission-matrix row and the
read-only rules), §9 ("Технический аккаунт", key rotation), §3
(`LegitimacyState`), and BR-015, BR-020, BR-025, BR-026, BR-030, BR-032, BR-033,
BR-035, NFR-021, NFR-073.

No new Open Decision was raised by SPECIFICATION. Where the requirements leave a
detail to the implementer rather than to the Owner, it is written as an
**interpretation** in §11 of the Specification, so it can be corrected at
`HUMAN_SPEC_APPROVAL` instead of being discovered in code.

---

## OD-001 What the instruction says about the technical account's Workspace roles

**Raised by:** the Story author (US-010, "Open Decisions").
**Affects:** FR-005, FR-004, VR-005, AC-004, and the translation entries of
FR-012.

`trebovaniya.md` §9 and BR-015 fix that the technical account holds **read-only
roles for Classroom and Admin Reports only**, and §7 item 10 — "Минимальный набор
ролей Google Workspace, достаточный техническому аккаунту" — is still open under
"Проверить при внедрении": the exact minimum was never verified on a live domain,
and the prototype read data as a super-admin, which §9 calls a leftover of
experiments rather than a model.

The instruction is the one place in the program where those roles must be written
down for a human to act on, so the Story cannot avoid the question.

**Options as the Story stated them:**

1. **State the requirement, not a role list.** The instruction says the account
   must be able to read Classroom and Admin Reports and must hold nothing beyond
   that, and names the scopes to delegate (AC-003) — which is what the super-admin
   actually enters in the console. No Workspace admin-role name is printed until
   §7 item 10 is verified. Nothing becomes wrong when the minimum turns out to be
   narrower than someone guessed.
2. State a concrete role set now, chosen by the implementer from Google's
   documentation. Convenient for the school, and the first thing to become wrong:
   the value is unverified, it would be printed as if it were fixed, and a school
   granting more than necessary is a security regression.
3. Leave the roles out entirely and mention only the scopes. Simplest, but it
   drops a requirement BR-015 states, and the super-admin has to infer that the
   account needs Admin Reports access at all.

Whatever is chosen, the instruction must not imply that a super-admin account, or
any account with write access, is acceptable — §9 is explicit that it is not.

**Resolution:** **option 1**, decided by the Owner on 2026-09-20. The instruction
states the **requirement** — the account must be able to read Classroom and Admin
Reports and must hold nothing beyond that — together with the scope list of
AC-003, which is what the super-admin actually enters in the Google console.
**No Workspace admin-role name is printed** until `trebovaniya.md` §7 item 10 is
verified on a live domain.

**Consequences, as recorded with the resolution:**

- the instruction is correct whatever the verified minimum turns out to be, and
  nothing in it has to be retracted when item 10 closes;
- a school cannot be led into granting more than necessary by a value this
  program invented;
- the text must still say explicitly that a super-admin account, and any account
  with write access, is **not** acceptable (§9);
- when item 10 is verified, adding the concrete roles is a change to the
  translation files and their test — it needs no new decision, because the
  requirement sentence already frames them.

**How the Specification is written against it:** FR-005 lists the five statements
the technical-account part of the instruction makes and none of them is a role
name; VR-005 forbids a Workspace admin-role name anywhere in the instruction and
AC-004 is tested against that prohibition, so a helpful addition in a later
translation change fails a test rather than reaching a school.

---

## OD-002 How the instruction is handed over

**Raised by:** the Story author (US-010, "Open Decisions").
**Affects:** FR-008, FR-007, the API surface of this Story (one `GET`), S-09,
AC-009.

`trebovaniya.md` §4 (Epic 6) asks the admin panel to show "готовый к передаче
super-admin школы текст". It does not say in what form the Admin passes it on, and
the form is not a detail: a downloadable file is an endpoint of its own, with a
content type, a file name and a place in the SC-4 and antiforgery rules.

**Options as the Story stated them:**

1. **The page itself, with a copy-to-clipboard affordance.** No new endpoint, no
   file leaves the server, nothing to audit; the Admin pastes the text into
   whatever channel they already use. The page stays printable through the
   browser.
2. A generated file to download (`.txt` or `.pdf`). Tidier to forward, and it adds
   an endpoint that returns a document, which the API conventions then have to
   place — and a document is the kind of thing people store, so a stale copy
   outlives a rotated client ID.
3. Send it by email from the installation. Rejected on sight: the program has no
   mail channel, and SC-13 lists exactly two outbound destinations — adding a
   third is a requirements change, not a Story decision.

**Resolution:** **option 1**, decided by the Owner on 2026-09-20. The instruction
is handed over from **the page itself, with a copy-to-clipboard affordance**. No
download endpoint is added.

**Consequences, as recorded with the resolution:**

- the API surface of this Story is one `GET`, and the SC-4 and antiforgery rules
  need no new entry;
- nothing leaves the installation on its own: what the super-admin receives is
  what the Admin themselves sends (SC-13, AC-009);
- **the copy carries exactly what the page shows** — the client ID, the scopes,
  the technical-account requirements and the domain — and no secret;
- no stored document can outlive a rotated client ID, which is the point of
  AC-006: the current instruction is always the page;
- printing stays available through the browser, so a school that wants paper is
  not blocked;
- the affordance must degrade safely: with the clipboard unavailable the text
  stays selectable on the page, so the Admin is never left without it.

**How the Specification is written against it:** FR-007 is a single `GET` and
FR-008 describes the affordance as progressive enhancement over text that is
already complete and selectable in the rendered HTML — the page is correct with
scripting switched off, and the script adds only the copy. FR-008 also fixes that
the copied text is assembled from the same rendered values, never fetched from a
second endpoint, so there is nothing new to authorise.
