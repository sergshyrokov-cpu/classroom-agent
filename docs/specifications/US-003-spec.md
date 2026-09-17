---
artifact_type: specification
story: US-003
version: 1
status: APPROVED
created_at: 2026-09-17T09:15:08Z
updated_at: 2026-09-17T09:29:38Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: trebovaniya.md
    version: 70
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
supersedes: null
---

# US-003 Specification — Manage AllowedAdmin entries

## 1. Overview

The third Control Plane Story. On an installation's detail page (US-002) the
signed-in Owner keeps the list of email addresses allowed to be that school's
Admins:

- the list of an installation's `AllowedAdmin` entries (email, date added);
- adding an entry — the email must be in exactly the installation's domain;
- revoking an entry through a confirmation step — the entry and its email are
  deleted;
- a warning while an installation has fewer than two entries;
- two new Control Plane audit events;
- Ukrainian and English translations for everything shown.

Sources: `trebovaniya.md` v70 §2 ("Модель авторизации"), §3 (`AllowedAdmin` and
its record rules), §4 (Epic 8), §5 (audit list; AllowedAdmin emails deleted with
the entry), §8 (antiforgery, error page, validation), §9 ("Идентификация Админа",
"Несколько Админов на школу", "Три точки контроля Владельца");
`security-conventions.md` SC-3, SC-4, SC-10, SC-11, SC-12, SC-13;
`api-conventions.md` API-4, API-5; `architecture.md` AD-3, AD-8, AD-9;
`persistence-conventions.md` PC-2, PC-6, PC-9; BR-011, BR-013, BR-021, BR-022,
BR-023, BR-079; NFR-073. The US-001 host baseline (deny-by-default, global
antiforgery, setup gate, error page, session, localization) and the US-002
installation detail page are reused, not re-specified.

**There are no Open Decisions** (section 11). Behaviour not literally fixed by the
Story or `trebovaniya.md` is stated as interpretations I-1 … I-10 for review at
`HUMAN_SPEC_APPROVAL`.

## 2. Business Goal

`AllowedAdmin` decides **who** in a school may sign in to its installation and
configure it — the first of the Owner's three control points (`trebovaniya.md` §9).
No Admin can appear in a new installation without an entry (BR-011), so US-008,
US-009 and US-012 depend on it. It is also the recovery path when a school's Admins
leave: the Owner adds a new email and the new Admin signs in at once, without
anyone going to the school's server. Revoking an entry takes away only that
person's right to configure; it never stops the school (BR-022, BR-023).

## 3. Business Flow

### 3.1 Onboarding a school (DC-2 step 5)

1. The Owner has registered the school's `Installation` (US-002).
2. On its detail page the Owner sees no Admins and the warning that at least two
   are required (FR-001, FR-006).
3. The Owner adds the email of each domain administrator the school named
   (FR-002, FR-003); each addition is audited (FR-007).
4. With two or more entries the warning disappears.

### 3.2 A person leaves the school

1. On the school's detail page the Owner chooses to revoke that person's entry.
2. A confirmation page shows the email and the school's name and explains that
   revoking does not stop the school (FR-004).
3. The Owner confirms; the entry and its email are deleted and the revocation is
   audited (FR-005, FR-007). If fewer than two entries remain, the warning shows.
4. A wrong email is corrected the same way: revoke it and add the right one — there
   is no editing (`trebovaniya.md` §3, v70).

## 4. Functional Requirements

### FR-001 Admins section of the detail page

The installation detail page of US-002 (FR-005 there) gains an "Admins" section:

- Lists every `AllowedAdmin` entry of **that** installation and of no other, each
  with its email and the date it was added.
- Date display: date and time to the minute in UTC, marked "UTC", in the Owner's
  UI-language format — the same rule as the installation's creation date (US-002
  OD-003) *(interpretation I-3)*.
- Order: by email, ordinal — every stored email is lower case *(interpretation
  I-4)*.
- Not paginated *(interpretation I-8)*.
- With no entries: a translated "no Admins yet" message.
- Always offers the action to add an Admin (FR-002) and, per entry, the action to
  revoke it (FR-004).
- The warning of FR-006.
- Everything else on the detail page, and the installations list, stays as US-002
  specified it *(interpretation I-9)*.

### FR-002 Add form

**GET** — a form page for one installation *(interpretation I-1)*: the
installation's name and domain for orientation, the email field, a translated
hint that the email must be in this installation's domain (naming it), and the
antiforgery token. Unknown installation or non-UUID route value: `404` (FR-008).

### FR-003 Adding an entry

**POST** — each step runs only if the previous one passed:

1. **Antiforgery** — global validation of US-001 (FR-015 there). Missing or
   invalid token: `400`, error page "page expired"; nothing created.
2. **Installation exists** — else `404` (FR-008).
3. **Field validation** — VR-001 format rules. On failure: `400`, the form again
   with the field named and why, keeping the value typed; nothing created, not
   audited, value not logged.
4. **Normalization** — the email is converted to lower case (VR-001). Nothing is
   trimmed.
5. **Domain match** — VR-002: the domain part must equal the installation's
   domain. On failure: `400`, the form again with the error naming the expected
   domain *(interpretation I-7)*; nothing created, not audited.
6. **Uniqueness** — the installation already has an entry with this email: `409`,
   the form again with the translated message "this email is already an Admin of
   this installation"; nothing created, not audited.
7. **Create** — in one transaction in `ControlPlane.Services` (AD-3):
   - a new `AllowedAdmin` bound to the installation, with the normalized email,
     **added by** = the signed-in Owner account's internal id, **added at** = the
     current UTC instant (PC-6); neither changes afterwards;
   - the audit row "AllowedAdmin added" (FR-007).
   After commit: redirect (`302`, post/redirect/get) to the installation's detail
   page *(interpretation I-6)*.

- Allowed whatever the installation's status, suspended included (AC-006,
  `trebovaniya.md` §3, v70). Adding changes nothing on the `Installation` itself.
- No limit on the number of entries per installation.
- Uniqueness of (installation, email) is enforced by the database, so that two
  concurrent additions cannot both commit (mechanism: `db-designer`). The request
  that loses the race gets exactly the `409` of step 6, never `500`; its
  transaction, audit row included, is rolled back entirely (AC-004).
- The same email may exist as an entry of a different installation only if both
  installations share a domain, which BR-021 forbids — so in practice an email
  belongs to at most one installation. No cross-installation check is added.
- A previously revoked email is added as a new entry with new "added by" and
  "added at" values; the deleted entry is not restored.

### FR-004 Revoke confirmation

**GET** — a confirmation page for one entry *(interpretation I-2)*:

- shows the entry's email and the installation's name;
- a translated explanation: revoking takes away only this person's right to sign
  in to the installation and configure it; synchronization and Deans keep working;
  stopping a school is a separate action (BR-022, BR-023);
- if the entry is the last one or one of only two, a translated note that the
  installation will be left with fewer than two Admins *(interpretation I-10)*;
- a "revoke" button submitting FR-005 with the antiforgery token, and a "cancel"
  link back to the detail page. Cancel sends no state-changing request.
- Changes nothing (API-4).
- Unknown installation, non-UUID route value, unknown entry, or an entry of a
  different installation: `404` (FR-008).

### FR-005 Revoking an entry

**POST** — each step runs only if the previous one passed:

1. Antiforgery (as FR-003 step 1).
2. Installation exists and the entry exists **and belongs to it** — else `404`.
3. **Delete** — in one transaction in `ControlPlane.Services`:
   - the `AllowedAdmin` row is deleted, its email with it (`trebovaniya.md` §5);
   - the audit row "AllowedAdmin revoked" (FR-007), whose target id is the
     deleted entry's internal id.
   - If the delete affects no row — the entry was already deleted by a concurrent
     or repeated submission — the transaction writes nothing, and the response is
     `404`; no audit row (AC-008).
   After commit: redirect (`302`) to the installation's detail page.

- Allowed whatever the installation's status, suspended included (AC-006).
- Revoking the last entry is allowed (`trebovaniya.md` §9, v70).
- Revoking changes nothing on the `Installation` itself; it sends nothing to the
  installation (the next Admin login check simply finds no entry — US-008).

### FR-006 Fewer-than-two warning

- Shown in the Admins section of the detail page while the installation has 0 or
  1 entry; not shown with 2 or more (AC-007).
- Translated text: onboarding requires at least two Admins so that one person
  leaving never stops the school's configuration (BR-013).
- A notice only: never disables adding or revoking.
- Computed from the current entries at render time; nothing is stored.

### FR-007 Audit events

The Control Plane `AuditEvent` table exists (US-001). This Story adds exactly:

| Event | Actor (id, role) | Target | Outcome |
|---|---|---|---|
| AllowedAdmin added | Owner account id, `Owner` | `AllowedAdmin`, the entry's internal id | succeeded |
| AllowedAdmin revoked | Owner account id, `Owner` | `AllowedAdmin`, the entry's internal id | succeeded |

- Every row carries UTC timestamp, actor, action, target, outcome and the request
  identifier (`trebovaniya.md` §5).
- **No** email, installation name or domain in any column (SC-11, PC-9). The
  revoked entry's internal id stays in its rows after the entry is deleted; audit
  rows reference it without a foreign key.
- The row is written in the same transaction as the change.
- Refused or no-op requests — antiforgery, validation, domain mismatch,
  uniqueness, `404`, an already-deleted entry, opening the confirmation page,
  cancelling — write no row (AC-009).
- Rows are never updated or deleted; kept indefinitely (SC-11, US-001 FR-012).

### FR-008 Not found

- Every route of this Story is nested under the installation's UUID identifier
  (US-002 I-5); entry routes also carry the entry's identifier.
- `404` with the translated error page (US-001 FR-016) when: the installation
  identifier does not exist or is not a UUID; the entry does not exist, is
  malformed, or belongs to a different installation. The response is the same in
  every case and reveals nothing about which part failed.
- A `404` creates, deletes and audits nothing.

### FR-009 Authorization

- Every endpoint of this Story requires the signed-in Owner, declared by the
  Owner policy; none is anonymous and none is added to the SC-4 anonymous list or
  to the antiforgery exemption list (SC-4, API-9).
- A request without a session is redirected to the sign-in page; nothing is shown
  or changed (AC-010).
- While no Owner account exists, the US-001 setup gate redirects these endpoints
  to the setup page, like any other.
- The US-001 endpoint-enumeration test covers the new endpoints without a special
  case (TC-5). The Control Plane has one role, so the "forbidden role" case of
  TC-5 is the unauthenticated request.

### FR-010 GET safety and antiforgery

- Adding and revoking are POST; GET shows the detail page, the add form and the
  revoke confirmation only and changes nothing — opening the confirmation deletes
  nothing (API-4, AC-011).
- Antiforgery is the host's global validation; a missing or invalid token is `400`
  with the "page expired" error page.

### FR-011 Persistence

- New Control Plane entity `AllowedAdmin` in `ControlPlane.Persistence`, with its
  EF Core migration (PC-2, DC-4); applied by deployment, never at startup.
- Holds only: the owning installation, email, added by (Owner account internal
  id), added at (UTC) (`trebovaniya.md` §3), plus the key and technical fields the
  persistence conventions require. No password or secret (SC-3).
- Unique constraint on (installation, email) (FR-003).
- Email, added-by and added-at are never updated; no update path exists (no
  editing, `trebovaniya.md` §3, v70). Enforcing immutability at database level, as
  US-002 did for `Installation`, is `db-designer`'s decision.
- Rows are deleted only by revocation (FR-005). An `Installation` is never deleted
  (US-002), so no cascade from it is exercised.
- "Added by" references the Owner account by internal id; whether with a foreign
  key is `db-designer`'s decision.
- New audit action codes and the target type `AllowedAdmin` extend the existing
  Control Plane audit vocabulary.
- Exact schema: `db-designer`.

### FR-012 Localization

- Every page, label, hint, warning, note, button and message of this Story comes
  from `ClassroomAgent.ControlPlane.Localization`, each key in Ukrainian and
  English (NFR-073, TC-8). Shown in the Owner's UI language, Ukrainian by default.
- Emails and the installation's name and domain are shown as stored, never
  translated, HTML-encoded by the view (AC-012).

### FR-013 Logging

- Log lines written by these actions carry internal identifiers only — never an
  email, whether valid, rejected, duplicate or revoked (SC-10, DC-10;
  `non-functional-requirements.md`, v68).

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for their
wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | The Owner sees an installation's Admins | FR-001 |
| AC-002 | The Owner adds an Admin | FR-002, FR-003 step 7, FR-011 |
| AC-003 | Invalid email is rejected | FR-003 steps 3–5, VR-001, VR-002, VR-003, FR-013 |
| AC-004 | An email is listed once per installation | FR-003 step 6 and concurrency, FR-011 |
| AC-005 | The Owner revokes an Admin after confirming | FR-004, FR-005 |
| AC-006 | Entries of a suspended installation can be managed | FR-003, FR-005 |
| AC-007 | Fewer than two Admins is warned about | FR-006 |
| AC-008 | Unknown targets answer 404 | FR-008, FR-005 step 3 |
| AC-009 | Adding and revoking are audited | FR-007 |
| AC-010 | Only the Owner reaches these pages | FR-009 |
| AC-011 | State-changing forms are protected | FR-010 |
| AC-012 | Pages are translated | FR-012 |

## 6. Validation Rules

Format rules are declared with DataAnnotations on the request type; custom rules
as `ValidationAttribute` (`trebovaniya.md` §8). The domain match needs the
installation and is checked in `ControlPlane.Services`. Nothing is trimmed before
validation.

### VR-001 Email format

| Rule | Value |
|---|---|
| required | yes |
| length | 1 to 254 characters inclusive |
| shape | exactly one `@`, splitting the value into a name part and a domain part |
| name part length | 1 to 64 characters inclusive |
| name part characters | Latin letters `A–Z`, `a–z`, digits `0–9`, `.`, `_`, `-`, `'` — nothing else (no space, `+`, Cyrillic, non-ASCII) |
| name part dots | does not start or end with `.`; no two `.` in a row |
| domain part | non-empty; compared in VR-002 (its own character rules are the installation domain's, so VR-002 covers them) |
| edges | a leading or trailing whitespace character fails the character rules — rejected, not trimmed *(interpretation I-5)* |
| case | any case accepted in both parts; stored in lower case |
| valid examples | `ivan.petrenko@dac.ukr.education`; `O'Brien@DAC.Ukr.Education` (stored `o'brien@dac.ukr.education`); `admin-2_x@dac.ukr.education` |
| invalid examples | empty; `dac.ukr.education` (no `@`); `a@b@dac.ukr.education`; `@dac.ukr.education`; `.ivan@dac.ukr.education`; `ivan.@dac.ukr.education`; `iv..an@dac.ukr.education`; `ivan+x@dac.ukr.education`; `іван@dac.ukr.education`; ` ivan@dac.ukr.education`; a 65-character name part; 255 characters |

### VR-002 Email domain

| Rule | Value |
|---|---|
| match | the lower-cased domain part equals the installation's stored domain (already lower case, US-002 VR-002) — ordinal comparison, whole string |
| rejected | any other domain: a personal address (`gmail.com`), another school's domain, a subdomain (`mail.dac.ukr.education`), a parent domain (`ukr.education`), a trailing dot (`dac.ukr.education.`) |
| status | `400` with the field error naming the expected domain |

### VR-003 Messages

- Each field error names the field and rule in translated, display-safe terms,
  never a stack trace, type name or internal detail (SC-10).
- On a validation, domain or conflict failure the form keeps the value typed; it
  is HTML-encoded on output.
- Rejected values are never written to a log line or audit row (FR-013, FR-007).

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | Every endpoint requires the signed-in Owner; nothing added to the anonymous or antiforgery-exemption lists | SC-4, API-9 |
| S-02 | Adding and revoking are POST with the global antiforgery token; GET — including the revoke confirmation — changes nothing | SC-4, API-4 |
| S-03 | An entry is accepted only with an email in exactly its installation's domain, lower-cased | SC-3, BR-079, `trebovaniya.md` §3 v70 |
| S-04 | (installation, email) unique under concurrency, enforced by the database | BR-079 |
| S-05 | An entry is reachable and revocable only through its own installation; an entry id with another installation's identifier is `404` | FR-008 (derived from AD-3 ownership) |
| S-06 | Revocation deletes the entry with its email; no copy of the email remains in audit, logs or elsewhere | `trebovaniya.md` §5, SC-11 |
| S-07 | No password or secret is stored for an Admin | SC-3, `trebovaniya.md` §3 |
| S-08 | Audit rows per FR-007: internal ids only, same transaction, never updated or deleted | SC-11, PC-9 |
| S-09 | Logs carry no email | SC-10 |
| S-10 | Emails and installation values are HTML-encoded on every page | SC-10 (output safety), AD-8 |
| S-11 | Controllers and views see DTOs only; no `DbContext` outside `Persistence`/`Services` | AD-3, AD-8 |
| S-12 | Nothing is sent to an installation or added to `ClassroomAgent.Contracts`; no reference to `ClassroomAgent.Domain` | SC-12, AD-1 |
| S-13 | No outbound call: the email is not verified against Google or any service | SC-13 |
| S-14 | Error responses carry no stack trace, SQL, constraint or type name; a unique-constraint violation never surfaces as `500` | SC-10, AD-9 |

## 8. Error Handling

| Situation | Status | What the Owner sees | Audit |
|---|---|---|---|
| Any endpoint without a session (Owner exists) | `302` | sign-in page | — |
| Any endpoint before setup | `302` | setup page (US-001) | — |
| Missing/invalid antiforgery token | `400` | error page "page expired" | — |
| Add: format validation failed | `400` | the form with the field error and typed value | — |
| Add: domain part is not the installation's domain | `400` | the form with the error naming the expected domain | — |
| Add: email already an entry of this installation (pre-check or lost race) | `409` | the form with the conflict error | — |
| Add: success | `302` | detail page | added row |
| Revoke: success | `302` | detail page | revoked row |
| Revoke: entry already deleted (repeat or concurrent) | `404` | error page "not found" | — |
| Unknown / malformed installation or entry, or entry of another installation | `404` | error page "not found" | — |
| Unhandled exception | `500` | error page "internal error" | — |

Expected outcomes (validation failure, domain mismatch, conflict, not found) are
results of `ControlPlane.Services`, not exceptions (AD-9). The unique-constraint
violation of a lost race is caught in `ControlPlane.Services` and turned into the
conflict result.

## 9. Non-Functional Requirements

- **NFR-073** — Ukrainian and English, Ukrainian by default (FR-012).
- **NFR-025** — audited actions in `AuditEvent`, never updated, kept indefinitely
  (FR-007).
- **NFR-026** — no path to teaching data (S-12).
- **NFR-032** — schema only through EF Core migrations, applied by deployment.
- **NFR-062** — .NET 10; `Nullable` enabled, warnings as errors.
- **Testability** — integration tests run against PostgreSQL via Testcontainers
  (TC-2); the concurrent-addition case (AC-004) is tested against the real database
  constraint; the suspended-installation case (AC-006) sets the status directly in
  the test database, because US-004 is not delivered.

## 10. Out of Scope

- The Control Plane answer to an installation's Admin login check, the service
  channel and `ClassroomAgent.Contracts` (US-008, US-005).
- Google OAuth sign-in and just-in-time `AppUser` creation (US-008).
- Suspending and resuming an `Installation` (US-004).
- Editing an entry (none — `trebovaniya.md` §3, v70).
- Verifying that the email exists in Google Workspace or is a domain administrator:
  the Control Plane makes no outbound call (SC-13).
- An Admin count or warning on the installations list (I-9).
- Viewing the audit log (EPIC-9); the Owner choosing their UI language (US-039).
- Anything in a school installation.

## 11. Open Decisions

Full text: `docs/decisions/US-003-open-decisions.md`.

**None.** The Story's own section records none — its questions were decided in
`trebovaniya.md` v70 — and `trebovaniya.md` §7 has no open item this Story depends
on (items 10 and 14 concern Google access and synchronization).

### Interpretations for human review

Behaviour not literally fixed by the Story or `trebovaniya.md`, derived from the
conventions cited and from the choices confirmed for US-002. A rejected
interpretation becomes an Open Decision.

- **I-1** Adding uses a separate form page reached from the detail page, like the
  US-002 name and client ID forms — not an inline form on the detail page.
- **I-2** The revoke confirmation is a separate page (GET) with a "revoke" POST
  button and a "cancel" link; no browser dialog or script is involved, so the
  confirmation works without JavaScript and is testable on the server.
- **I-3** The date added is shown to the minute in UTC, marked "UTC", as the
  installation's creation date (US-002 OD-003).
- **I-4** Entries are ordered by email.
- **I-5** Surrounding spaces in the email are rejected, not trimmed, as the name in
  US-002.
- **I-6** Success redirects (`302`) to the detail page; validation and domain
  mismatch answer `400`, a duplicate `409` (API-5, as US-002 I-4 and I-10).
- **I-7** The domain-mismatch error names the expected domain — the Owner already
  sees it on the page, so nothing is disclosed.
- **I-8** The Admins list is not paginated: a school has a handful of Admins.
- **I-9** The installations list is unchanged; the fewer-than-two warning is shown
  only on the detail page, as the Story states.
- **I-10** The revoke confirmation also warns when revoking would leave fewer than
  two Admins — a notice only, it never blocks (extends BR-013's warning to the
  moment of the decision).

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-012 | — | S-01, S-10 | — |
| AC-002 | FR-002, FR-003, FR-011 | VR-001, VR-002 | S-03, S-07 | — |
| AC-003 | FR-003 steps 3–5, FR-013 | VR-001, VR-002, VR-003 | S-03, S-09, S-14 | — |
| AC-004 | FR-003 step 6, FR-011 | VR-001 (case) | S-04, S-14 | — |
| AC-005 | FR-004, FR-005 | — | S-02, S-06 | — |
| AC-006 | FR-003, FR-005 | — | — | — |
| AC-007 | FR-006 | — | — | — |
| AC-008 | FR-008, FR-005 step 3 | — | S-05 | — |
| AC-009 | FR-007 | — | S-06, S-08 | — |
| AC-010 | FR-009 | — | S-01 | — |
| AC-011 | FR-010 | — | S-02 | — |
| AC-012 | FR-012 | VR-003 | S-10 | — |

Requirements with no single AC but required by conventions the Story cites:
FR-011 (persistence — PC-2, PC-9), S-11 … S-13 (Story Notes, AD-3, AD-8, SC-12,
SC-13).
