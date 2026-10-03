---
artifact_type: specification
story: US-039
version: 1
status: APPROVED
created_at: 2026-10-03T08:46:05Z
updated_at: 2026-10-03T08:50:09Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-039-choose-ui-language.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-039-open-decisions.md
    version: 1
supersedes: null
---

# US-039 Specification — Choose UI language (Admin, Dean and Owner)

## 1. Overview

`trebovaniya.md` §5 (v51) makes the interface bilingual — Ukrainian and English —
on both hosts. The school's default language is an installation setting; each
Admin and Dean may choose their own, stored on their account and followed on any
device; the Owner likewise chooses for themselves in the Control Plane, where the
default is Ukrainian (NFR-073).

The storage and the rendering already exist: `AppUser.UiLanguage` and the Control
Plane `Owner.UiLanguage` hold `uk` / `en`; each host's culture provider
(`AccountCultureProvider`, `OwnerAccountCultureProvider`) renders a signed-in
user's request in the language carried by a claim of the session cookie, and an
anonymous request in the default. What is missing is the **choice**: no path
changes the stored value.

This Story adds, on both hosts, a header switcher «УКР / ENG» on every page a
signed-in user sees, a POST action that stores the choice on the signed-in
user's own account and re-issues the session so the choice takes effect at once,
the permission for that write in read-only mode (BR-026), and a check of the
existing screens' date and number formats.

### Interpretations

Where the Story and `trebovaniya.md` leave a detail to this Specification, the
choice made is recorded here so the human gate can see it:

- **I-1 The session is re-issued, not re-read.** A culture claim in the cookie is
  what makes a choice visible (Story, Notes). The action re-issues the session
  cookie from the current one with only the language claim replaced (FR-005). The
  alternative — reading the language from the database on every request — would
  change the culture providers of US-001 and US-008 and is not needed.
- **I-2 The re-issued session keeps its sign-in time.** The 8-hour absolute limit
  (NFR-072, SC-2) counts from the original sign-in. A re-issue that restarted it
  would let a user extend a session indefinitely by switching language, which
  weakens SC-2; the sign-in time claim is therefore copied, not renewed.
- **I-3 The security stamp is not rotated.** Choosing a language is not a
  credential change; other sessions of the same account stay valid. They keep
  the language they were issued with until their next sign-in (§5 "works on any
  device" is satisfied at sign-in, AC-002).
- **I-4 "Stays on the same page" uses a validated local return path.** The
  switcher form submits the path (and query) of the page it is on; the action
  redirects there only when it is a local, application-relative path, and to the
  host's landing page otherwise. This closes an open redirect, as US-001 I-6 did
  for sign-in.
- **I-5 No switcher on the error page.** The error page is on SC-4's anonymous
  list and shows "only translated text, no data" (§8, v64). "Every page" in the
  Story is read as every page that requires sign-in; a signed-in user still sees
  the error page in their own language (§8).
- **I-6 An invalid value is a `400` with the error page.** The switcher never
  sends anything but `uk` or `en`; any other value is a tampered request. It gets
  the host's error page with status `400`, as a refused form does; there is no
  form to re-render.
- **I-7 The forced password change page** of a Dean with a temporary password
  (US-012 FR-006) shows the switcher too (OD-007, resolved option 1). See FR-012.

## 2. Business Goal

A school employee works in the language they read best, on any device, without
asking the Owner to change the school's default for everyone; the Owner does the
same in the Control Plane. Dates and numbers on screens read naturally in the
chosen language.

## 3. Business Flow

1. A signed-in Admin or Dean (installation) or the Owner (Control Plane) opens any
   page that requires sign-in. The header shows «УКР / ENG» with the current
   language marked.
2. They click the other language. The browser POSTs the choice, the antiforgery
   token and the current page's local path.
3. The host verifies the antiforgery token and the session, validates the value,
   stores it on the signed-in account — in read-only mode too — and re-issues the
   session cookie with the new language.
4. The browser is redirected to the same page, now in the chosen language. Every
   following page is in that language.
5. On a later sign-in, on any browser, the session is issued with the stored
   language.

## 4. Functional Requirements

### FR-001 What this Story adds

On each host: the header switcher (FR-002), the language-choice action (FR-003),
the account update (FR-004), the session re-issue (FR-005), the date/number
review (FR-009), and the temporary-password case (FR-012). No schema change: both `UiLanguage` columns exist (OD-003).

### FR-002 The header switcher

- Every page that requires sign-in renders a header containing the switcher:
  - in the installation, for the Admin and the Dean;
  - in the Control Plane, for the Owner (OD-004).
- The switcher shows both languages, each named as it names itself: «УКР» and
  «ENG». The current language is marked as current (visually and with
  `aria-current`) and is not a submit control; the other one is.
- Each choice is a POST form carrying the antiforgery token, the language code and
  the current page's local path and query (FR-006). It is never a link (GET).
- Pages that do not require sign-in — the sign-in pages, the Control Plane
  first-run setup and the error page — render no switcher (OD-002, I-5).
- The forced password change page shows it (OD-007, FR-012).

### FR-003 The language-choice action

- One action per host, POST only, requiring an authenticated user of that host
  (any role of the host: Admin or Dean; the Owner).
- Input: the language code (VR-001) and the return path (VR-002). It carries **no
  user identifier**: the account changed is always the one identified by the
  session (AC-004).
- Outcome:
  - valid value → the account is updated (FR-004), the session re-issued
    (FR-005), and the response redirects (`302`/`303`, the existing form
    convention) to the validated return path;
  - a value equal to the stored one → same outcome, nothing written beyond what
    FR-004 says (idempotent);
  - invalid value → `400`, the host's error page, nothing stored (I-6, §8).
- The exact route and the redirect status are fixed at `API_DESIGN`.

### FR-004 Storing the choice

- **Installation:** an Application use case loads the signed-in user's `AppUser`
  by the session's account id and sets its `UiLanguage`. It writes nothing else —
  no audit row (OD-005), no security stamp rotation (I-3), no sign-in time.
- **Control Plane:** a `Services` method does the same for the `Owner` row
  (`DbContext` stays in `Persistence`/`Services`, AD-3).
- An account that no longer exists or is disabled is not updated; the
  request ends as if the session were invalid (the per-request stamp check already
  rejects such a session before the action runs).
- The domain entity is not exposed: the host receives a DTO or a result value
  (AD-8).

### FR-005 Re-issuing the session

After a successful store, the host re-issues its session cookie so that the next
page renders in the new language:

- the new principal is the current one with **only** the language claim replaced;
- the role, email, account id, security stamp and **sign-in time** claims are
  copied unchanged (I-2, I-3), and so is the temporary-password claim if present
  (OD-007, FR-012): a choice never ends the forced-change state;
- the cookie keeps the host's SC-2 attributes: non-persistent, `httpOnly`,
  `Secure`, `SameSite=Lax` (installation) / `Strict` (Control Plane), the same
  idle timeout; the 8-hour absolute limit still counts from the original sign-in;
- the per-request security-stamp check (US-008 AC-014, US-001) keeps working
  unchanged on the re-issued cookie.

### FR-006 Returning to the same page

- The return path is the path and query of the page the switcher was rendered on.
- It is used only if it passes VR-002; otherwise the action redirects to the
  host's landing page (installation `/`, Control Plane `/`). It is never an
  absolute URL, a protocol-relative URL or a path to another host.

### FR-007 Read-only mode

- Choosing a language is on the BR-026 closed list ("выбор пользователем языка
  интерфейса", §2, v51). The installation use case of FR-004 declares
  `PermittedServiceWrite.SignInBookkeeping` around its commit and is registered in
  `PermittedServiceWrites.Declarations` — the only growth of that registry.
  `trebovaniya.md` and the `PermittedServiceWrite` enum do not change.
- The declaration covers that commit only: no other write path is opened, and the
  US-007 structural test of AC-007 keeps passing with the new entry.
- Proven in the Application layer (AD-6, TC-5).
- The Control Plane has no read-only mode; nothing to do there.

### FR-008 Anonymous visitors

Unchanged: the installation's anonymous pages render in the school default, the
Control Plane's in Ukrainian (§8, §9). The culture providers keep consulting no
`Accept-Language`, query string or cookie; the only culture sources stay the
account's claim and the default.

### FR-009 Dates and numbers on existing screens

Every existing screen that shows a date, a time or a number formats it by the
request culture (`uk` / `en`) (NFR-073, OD-006). The screens known at
specification time:

| Screen | Host | Today | Required |
|---|---|---|---|
| Landing — "last successful check" in read-only mode (`Home/Index`) | installation | fixed `dd.MM.yyyy HH:mm`, invariant culture, `UTC` suffix | the culture's short date + `HH:mm` + `UTC` |
| Dean accounts list — last successful sign-in (`DeanAccounts/Index`) | installation | fixed `yyyy-MM-dd` | the culture's short date |
| Installations list and installation card — created, last check, Admin added (`InstallationDisplay.UtcTime`) | Control Plane | the culture's short date + `HH:mm` + `UTC` | already compliant — verified, unchanged |

- Times stay in UTC with the `UTC` suffix; the time zone is not in scope.
- The implementation re-checks every view and every message with a date, time or
  number placeholder, and lists in the implementation report every such screen
  found and the correction made (AC-007).
- Screens that do not exist yet (journal, reports, exports) are out of scope.

### FR-010 Translations

- Every new user-visible string — the switcher labels, its accessible name, any
  message — comes from both translation files of its host
  (`Application.Localization`, `ControlPlane.Localization`); none is hard-coded.
- «УКР» and «ENG» are the same in both files (each language names itself).

### FR-011 Logging and audit

- No audit row (OD-005); SC-11 and `AuditAction` do not change.
- No application log line is required. If one is written, it carries the account
  id and the accepted code only; a rejected value is never logged (SC-10).

### FR-012 A Dean with a temporary password

OD-007 resolved option 1:

- the switcher is shown on the forced password change page (US-012 FR-006);
- the temporary-password gate (`TemporaryPasswordMiddleware`) lets the
  language-choice action through, in addition to the forced change form, sign-out
  and sign-in; every other path of that session is still sent to the form;
- the re-issued session keeps the temporary-password claim (FR-005), so after the
  redirect the Dean is still held at the form, now in the chosen language;
- the return path is validated as usual (VR-002); whatever it names, the gate
  sends that session back to the form, so the action opens no other page.

## 5. Acceptance Criteria

The Story's criteria, unchanged in meaning:

| Id | Criterion (Story) | Covered by |
|---|---|---|
| AC-001 | A signed-in user can switch the language from any page: same page shown again in the chosen language; every following page in it with no sign-out/sign-in; the switcher shows the current language | FR-002, FR-003, FR-005, FR-006, FR-012 |
| AC-002 | The choice is stored on the account and follows the user: after sign-out/sign-in or from another browser the pages are in the chosen language; stored on their own account only; another user's language unchanged | FR-004, FR-005, I-3 |
| AC-003 | Works in read-only mode, for every BR-025 cause; it is the BR-026 permitted write and nothing else; proven in the Application layer | FR-007 |
| AC-004 | Only a signed-in user, only for themselves: anonymous refused, SC-4 anonymous list unchanged; GET changes nothing; POST without valid antiforgery refused; no user identifier in the request; allowed and refused cases tested on both hosts | FR-003, VR-003, §7 |
| AC-005 | Only `uk` / `en`: anything else refused, nothing stored, the value not logged | VR-001, FR-003, FR-011 |
| AC-006 | Anonymous pages keep the default language and show no switcher | FR-002, FR-008 |
| AC-007 | Dates and numbers follow the chosen language; every such screen listed in the implementation report with any correction | FR-009 |
| AC-008 | Every new string translated in both files; language names as each names itself | FR-010 |

Additional, derived from this Specification:

| Id | Criterion | Covered by |
|---|---|---|
| AC-009 | A re-issued session keeps the original sign-in time: it expires at the 8-hour limit counted from the original sign-in, and the stamp check still rejects it after sign-out | FR-005, I-2 |
| AC-010 | A return path that is not a local application path leads to the landing page, never off-host | FR-006, VR-002 |

## 6. Validation Rules

### VR-001 The language code

- Required; exactly `uk` or `en` (lower case, no surrounding whitespace).
- Empty, missing, any other value, a different case, or longer than 2 characters
  is invalid → `400`, nothing stored (FR-003). Model binding must not coerce
  (for example an enum bound by number or name): the value is compared as a
  string against the two codes.
- The rejected value is never written to a log or echoed in the response.

### VR-002 The return path

- Optional; at most 2048 characters.
- Valid only when it starts with a single `/` and is not `//…` or `/\…`, and is
  accepted by the framework's local-URL check.
- Invalid or missing → the landing page. An invalid return path is **not** a
  `400`: the choice is still stored (FR-006).

### VR-003 Request shape

- Method POST only. A GET to the action's route changes nothing; it is not
  served as the action (the status — `404` or `405` with the error page — is
  fixed at `API_DESIGN`).
- The antiforgery token is validated by the hosts' global filter; there is no
  exemption (SC-4).
- No account identifier field is read from the request; any extra field is
  ignored.

## 7. Security Requirements

- **Authentication:** the action requires an authenticated session of its host
  (SC-2, SC-4). It is **not** added to SC-4's anonymous list. An anonymous POST is
  challenged by the host's existing mechanism (redirect to sign-in) and stores
  nothing.
- **Authorization:** a declared policy (API-9): installation — any signed-in
  Admin or Dean; Control Plane — the Owner policy. The target is always the
  session's own account; there is no way to name another account (AC-004).
- **Antiforgery:** required (SC-4, API-7); a missing or invalid token gets `400`
  and the host's error page, nothing stored.
- **GET changes nothing** (API-4, SC-4 names "choosing the UI language").
- **Session:** the re-issue keeps SC-2's cookie attributes, the idle timeout, the
  absolute limit from the original sign-in, and the security stamp (FR-005).
- **Read-only:** the write is declared as BR-026 sign-in bookkeeping, registered
  in `PermittedServiceWrites`, and opens nothing else (SC-5, FR-007).
- **Open redirect:** the return path is validated (VR-002).
- **Logging:** SC-10 — no rejected value, no email in a log line.
- **Audit:** none (OD-005).
- **Outbound data:** none; no Google call (SC-8, SC-13).

## 8. Error Handling

| Case | Response | Stored |
|---|---|---|
| Valid choice | redirect to the validated return path, or the landing page | yes |
| Invalid language code | `400`, error page | no |
| Missing / invalid antiforgery token | `400`, error page ("page expired") | no |
| Anonymous request | challenge → sign-in page | no |
| Session rejected by the stamp check or absolute limit | challenge → sign-in page | no |
| GET to the action route | not served as the action (`API_DESIGN`) | no |
| Database failure | `500`, error page (AD-9) | no |
| Read-only mode | the choice is stored (BR-026) — not a `409` | yes |

## 9. Non-Functional Requirements

- NFR-073: two languages, from translation files; dates and numbers follow the
  chosen language (FR-009, FR-010).
- NFR-072 / SC-2: session limits unchanged by a re-issue (FR-005).
- The switcher works without JavaScript (a plain POST form) and fits the
  responsive layout (§5, mobile version).
- Tests (TC-*): integration tests against PostgreSQL (TC-2), allowed- and
  refused-role tests on both hosts (TC-5), read-only proven in Application
  (TC-5). No Google call (TC-4).

## 10. Out of Scope

- A switcher before sign-in (OD-002).
- Changing the school's default language (installation configuration).
- A third language.
- Following a later change of the school default for users who never chose
  (OD-003) — no schema change.
- An audit row for the choice (OD-005).
- Date and number formats of screens that do not exist yet (journal, reports,
  exports).
- Propagating a choice to the user's other already-open sessions (I-3).
- Teacher and Student accounts (Epic 7).

## 11. Open Decisions

Full text in `docs/decisions/US-039-open-decisions.md`.

| Id | Topic | Status | Impact |
|---|---|---|---|
| OD-001 | Where the language is chosen | RESOLVED — header switcher | FR-002 |
| OD-002 | A switcher before sign-in | RESOLVED — none | FR-002, FR-008 |
| OD-003 | A later change of the school default | RESOLVED — keep the copy | FR-001, no schema change |
| OD-004 | One Story or two | RESOLVED — one | both hosts here |
| OD-005 | An audit row for the choice | RESOLVED — none | FR-011 |
| OD-006 | Dates and numbers on existing screens | RESOLVED — check and correct | FR-009 |
| OD-007 | The switcher on the forced password change page | RESOLVED — offer it | FR-002, FR-005, FR-012 |

OD-007 was raised by this Specification and resolved by the Owner at
`HUMAN_SPEC_APPROVAL` on 2026-10-03 (option 1). No Open Decision remains.

## 12. Traceability

| AC | Functional requirements | Validation / security |
|---|---|---|
| AC-001 | FR-002, FR-003, FR-005, FR-006, FR-012 | VR-002 |
| AC-002 | FR-004, FR-005 | §7 Authorization |
| AC-003 | FR-007 | §7 Read-only |
| AC-004 | FR-003, FR-012 | VR-003, §7 Authentication, Authorization, Antiforgery |
| AC-005 | FR-003, FR-011 | VR-001 |
| AC-006 | FR-002, FR-008 | §7 Authentication |
| AC-007 | FR-009 | — |
| AC-008 | FR-010 | — |
| AC-009 | FR-005 | §7 Session |
| AC-010 | FR-006 | VR-002, §7 Open redirect |
