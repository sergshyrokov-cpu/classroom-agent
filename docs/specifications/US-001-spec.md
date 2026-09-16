---
artifact_type: specification
story: US-001
version: 3
status: DRAFT
created_at: 2026-09-16T07:54:22Z
updated_at: 2026-09-16T08:02:24Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: trebovaniya.md
    version: 67
  - path: docs/decisions/US-001-open-decisions.md
    version: 2
supersedes: null
---

# US-001 Specification — Owner first-run setup

## 1. Overview

The first Story of the whole system. It brings up the Control Plane
(`ClassroomAgent.ControlPlane`) far enough for the service Owner to claim the
single Owner account, sign in and sign out:

- a one-time setup code printed to the server console while no Owner account
  exists;
- a first-run setup page that creates the Owner account with that code;
- an Owner sign-in page with the SC-2 lockout policy, and sign-out;
- a minimal home page for the signed-in Owner;
- the Control Plane `AuditEvent` table and the four audit events this Story
  produces;
- the host's security baseline this Story is the first to need: deny-by-default
  authorization, global antiforgery, cookie attributes, HTTPS only, the error
  page, the `404` catch-all, persisted Data Protection keys;
- Ukrainian and English translations for everything shown.

Sources: `trebovaniya.md` v67 §3 (Owner, AuditEvent), §5 (audit, language), §8
(antiforgery, error page, cookies, HTTPS, Data Protection, validation), §9 (Owner
role, password policy, network isolation, one-time code); `security-conventions.md`
SC-2, SC-4, SC-7, SC-9, SC-10, SC-11, SC-12, SC-13; `api-conventions.md` API-4,
API-5, API-7; `architecture.md` AD-1, AD-3, AD-7, AD-8, AD-9;
`package-map.md` (`ClassroomAgent.ControlPlane`); `persistence-conventions.md`
PC-1, PC-2, PC-6, PC-9; `deployment-conventions.md` DC-2, DC-3, DC-4, DC-6;
BR-001, BR-005; NFR-025, NFR-026, NFR-072, NFR-073.

**All Open Decisions are resolved.** OD-002 … OD-005 were resolved by the human
on 2026-09-16, each with its recommended option (section 11); the requirements
below state those resolutions.

## 2. Business Goal

The Control Plane is the root of the authorization model: without the Owner
account there is no `Installation`, no `AllowedAdmin`, no Admin login and no
Workspace connection (DC-2 step 2). The Owner account has no external authority
behind it, so it is claimed exactly once, at first run, only by someone who can
read the server console, and it can never be claimed again through the
application (`trebovaniya.md` §9, v35).

## 3. Business Flow

### 3.1 First run

1. The Owner deploys the Control Plane, its database and migrations (DC-2 step 1,
   DC-4) and starts it.
2. At startup the Control Plane finds no Owner account, generates the one-time
   setup code and prints it to the server console (FR-002).
3. The Owner opens any Control Plane page through the VPN or tunnel and is
   redirected to the setup page (FR-001).
4. The Owner enters the code from the console, a login and a password and
   submits (FR-004).
5. The account is created, the success is audited, the code becomes void, the
   Owner is signed in and lands on the home page (FR-005).

### 3.2 Normal use

1. The Owner opens the Control Plane; without a session they are sent to the
   sign-in page (FR-007).
2. They submit login and password; the sign-in sequence runs (FR-008); success
   and every refusal are audited (FR-012).
3. They work (in later Stories) and sign out with a POST (FR-011).

### 3.3 Restart before setup

A restart while no Owner account exists generates a new code; the previous one
stops working (FR-002).

## 4. Functional Requirements

### FR-001 Setup gate while no Owner account exists

- While the Control Plane database holds no Owner account, every request to a
  Control Plane page or endpoint added by this Story is answered with a redirect
  to the setup page, **except**:
  - the setup page itself (GET and POST);
  - the error page;
  - the anonymous `404` catch-all for an unmatched address, which keeps
    answering `404` (SC-4, v66).
- The gate applies to anonymous requests; no session can exist before the
  account does.
- No other Control Plane function is reachable until setup completes.
- The gate is a host-wide rule, so an endpoint added by a later Story is covered
  without its own check.

### FR-002 One-time setup code

- At startup, if and only if no Owner account exists, the Control Plane
  generates a random one-time setup code.
- The code is written to the server console (standard output) and **never** to
  the log file or any other sink of the logging pipeline, and never to the
  database, a file or configuration (SC-2, SC-10). It is held only in process
  memory.
- The code becomes void the moment the Owner account is created (FR-005).
- A restart while no Owner account exists generates a new code; the previous
  code is gone with the process and no longer works.
- At startup with an Owner account present, no code is generated or printed.
- Length, alphabet, randomness source and comparison: **OD-005** (resolved:
  ≥128 bits from the OS cryptographic RNG, 26 upper-case Crockford
  Base32 characters in groups of 4 or 5 separated by `-`; hyphens, spaces and
  case ignored on input; constant-time comparison; no separate attempt limit).

### FR-003 Setup page (GET)

- **No Owner account:** shows the setup form with four fields — setup code,
  login, password, password repeated — and the antiforgery token. The code and
  both password inputs are never pre-filled.
- **Owner account exists** (AC-003):
  - anonymous visitor — redirected to the sign-in page; the response reveals
    nothing about the existing account, its login included;
  - signed-in Owner — redirected to the home page *(interpretation I-1)*.
- No second account can be created by any path.

### FR-004 Setup submission (POST) — processing order

Each step runs only if the previous one passed. The order of steps 2 and 4 is
**OD-004** (resolved: fields first, shown below).

1. **Antiforgery.** Global validation (FR-015). A missing or invalid token: `400`
   and the error page with the "page expired" text; nothing created, nothing
   audited (AC-011).
2. **Field validation at binding** (VR-001 … VR-003, VR-007). On failure: `400`;
   the setup form is shown again naming each failed field and why; the login
   value may be shown again, the code and both password fields are empty;
   nothing audited.
3. **Owner account already exists.** Refused as a conflict — response per
   **OD-003** (resolved: `409`, the setup page with one translated
   form-level message "the Owner account has already been created; sign in" and
   a link to sign-in); nothing about the existing account shown; not audited
   (v67 audits a code refusal only while no account exists).
4. **Setup code check** (VR-004). Missing or not equal to the current code:
   `400`; the setup form is shown again with an error on the code field that
   does not reveal the correct code; an audit row "setup refused — wrong setup
   code" is written (FR-012).
5. **Create the account** (FR-005).

### FR-005 Owner account creation

- In one transaction in `ControlPlane.Services` (AD-7):
  - exactly one Owner account is created through ASP.NET Core Identity in the
    Control Plane database;
  - the login is stored so that it is compared case-insensitively (FR-008);
  - the password is stored only as an Identity password hash — never in plain
    text, never recoverable, never returned by any response or DTO (AD-8);
  - the account's UI language is set to Ukrainian (`uk`) (NFR-073);
  - the audit row "Owner account created" is written (FR-012).
- After commit: the setup code is voided, the Owner is signed in (session cookie,
  FR-010), and redirected to the home page.
- Nothing is written to any installation database or `AppUser` table; the
  Control Plane does not reference `ClassroomAgent.Domain` (BR-005, AD-1).

### FR-006 Concurrent setup submissions

- When two valid setup submissions arrive at the same time, exactly one Owner
  account is created. The guarantee must hold under true concurrency: it is
  enforced so that two transactions cannot both commit an Owner account (the
  mechanism is `db-designer`'s), not by an in-memory check alone.
- The losing request is refused as a conflict — response per **OD-003** — never
  with `500`.
- No partially created account remains: the loser's transaction, audit row
  included, is rolled back entirely.
- The winner's success audit row is the only setup row for the created account.

### FR-007 Sign-in page (GET)

- **Owner account exists, no session:** shows the sign-in form — login, password,
  antiforgery token.
- **Signed-in Owner:** redirected to the home page *(interpretation I-1)*.
- **No Owner account:** redirected to the setup page (FR-001).
- After a successful sign-in the Owner lands on the home page; this Story accepts
  no return URL *(interpretation I-6)*.

### FR-008 Owner sign-in sequence (POST)

After global antiforgery validation (FR-015) and binding (VR-005), the sequence
of SC-2 (v66) runs. Each step runs only if the previous one passed. The Owner has
no disabled or temporary-password state, so the Dean's steps 4 and 5 do not
apply.

| # | Condition | Result | Counter | Audit (FR-012) |
|---|---|---|---|---|
| 1 | no account with this login (case-insensitive) | common refusal message | — | refused, actor anonymous, "unknown login" |
| 2 | sign-in lockout in force | common refusal message; **password not checked** | unchanged | refused, actor Owner id, "locked out" |
| 3 | wrong password | common refusal message | +1; on reaching 5 the lockout starts | refused, actor Owner id, "wrong password" |
| 4 | success | signed in; redirected to the home page | reset to 0 | succeeded, actor Owner id |

- The sequence is **not** built on `SignInManager.PasswordSignInAsync` or
  `CheckPasswordSignInAsync`, which check lockout and password in a different
  order (SC-2, v66).
- The common refusal message is one translated text: "wrong login or password,
  or sign-in is temporarily locked after several failed attempts — try again
  later" (`trebovaniya.md` §9). It never reveals whether the login exists or is
  locked; the response status and form are identical for steps 1–3
  *(interpretation I-3 for the status)*.
- The login value may be shown again on a refusal; the password field is empty.

### FR-009 Lockout

- 5 consecutive failed attempts (step 3) lock sign-in for 15 minutes from the
  fifth failure.
- During the lockout every attempt, with the correct password too, is refused at
  step 2; it neither extends the lockout nor changes the counter.
- After 15 minutes the correct password signs in again.
- A successful sign-in resets the counter.
- When a lockout ends, counting of consecutive failures starts again from zero
  *(interpretation I-4)*.
- There is no permanent lockout and no other way to unlock (SC-2).

### FR-010 Session

- Sign-in (FR-005, FR-008) issues the Control Plane session cookie: `httpOnly`,
  `Secure`, `SameSite=Strict` (SC-2, NFR-072). No password or credential is stored
  client-side.
- The cookie is encrypted with Data Protection keys persisted per FR-017, so a
  restart neither signs the Owner out nor voids an open form.
- Session lifetime (persistence, idle expiry, absolute limit): **OD-002**
  (resolved: non-persistent cookie, 30 minutes sliding idle expiry,
  8-hour absolute limit, no "remember me").

### FR-011 Sign-out

- Sign-out is a POST with the antiforgery token, available only to the signed-in
  Owner.
- It ends the session: the cookie is removed, and a request carrying the old
  cookie afterwards is not authenticated.
- After sign-out the Owner is redirected to the sign-in page
  *(interpretation I-7)*.
- A GET to the sign-out address does not sign the Owner out (API-4).
- Sign-out is not an audited action (not in the §5 list).

### FR-012 Audit events

The Control Plane `AuditEvent` table is created by this Story (SC-11: the first
Story that introduces an audited action creates the table and its writing path).
This Story writes exactly these events:

| Event | Actor (id, role) | Target | Outcome | Refusal category |
|---|---|---|---|---|
| Owner sign-in | Owner account id, `Owner` | Owner account | succeeded | — |
| Owner sign-in refused, login exists | Owner account id, `Owner` | Owner account | refused | "wrong password" or "locked out" |
| Owner sign-in refused, login unknown | anonymous, no id | none | refused | "unknown login" |
| Owner account created at first run | the new Owner account id, `Owner` | Owner account | succeeded | — |
| First-run setup refused, missing or wrong code (only while no Owner account exists) | anonymous, no id | none | refused | "wrong setup code" |

Every row carries: UTC timestamp, actor, action, target (entity type and
internal id, when there is one), outcome, and the request identifier that links
it to the log lines of the same request (`trebovaniya.md` §5).

- No row carries the login, password or setup code typed, or any other personal
  data (`trebovaniya.md` §5, v45, v67).
- Rows are never updated and never deleted: no use case, endpoint or page can
  change or remove a Control Plane audit row; they are kept indefinitely (SC-11,
  PC-9, v45). The purge of PC-11 does not exist in the Control Plane.
- An audit row belongs to the same transaction as the action it records where
  there is one (FR-005); a refusal row is written on its own.
- Actor and target ids have no foreign key (PC-9).

### FR-013 Home page

- Reachable only by the signed-in Owner.
- Shows that the Owner is signed in and offers sign-out (FR-011). It offers no
  other function: `Installation` and `AllowedAdmin` management arrive with US-002
  and US-003 *(interpretation I-8)*.

### FR-014 Authorization baseline

- A fallback authorization policy requires an authenticated Owner for every
  endpoint (SC-4 deny by default). The home page and sign-out require the Owner.
- Anonymous access is allowed only for the SC-4 entries this Story adds:
  - first-run setup (GET, POST) — protected by the private network and the
    one-time code;
  - Owner sign-in (GET, POST) — private network and the lockout;
  - the error page and the anonymous fallback catch-all answering `404`.
- No other endpoint added by this Story is anonymous; a test enumerating the
  host's endpoints proves it (TC-5).
- A signed-in Owner denied by a policy would get `403` with the error page; with
  a single role this Story has no such case, but the `AccessDenied` path leads to
  the error page with `403` (SC-4, v66).
- The service-channel endpoints (legitimacy check, Admin login check) are not
  part of this Story.

### FR-015 Antiforgery, GET safety, cookies and transport

- Antiforgery validation is applied **globally** to every POST, PUT, PATCH and
  DELETE of the host, never per action (SC-4). The setup form, the sign-in form
  and sign-out all carry the token; none of them is on the SC-4 exemption list.
- A request without a valid token is refused with `400`; a Razor form gets the
  error page with the translated "page expired — reload it and try again" text
  and a link back to the same page; no account is created, nobody is signed in,
  submitted values are not kept (`trebovaniya.md` §8, v64).
- No state-changing action this Story adds is reachable by GET (API-4).
- Cookies: session cookie `httpOnly`, `Secure`, `SameSite=Strict`; antiforgery
  cookie `httpOnly`, `Secure`, `SameSite=Strict`; every other cookie `Secure`
  (SC-2, v61, v64).
- The Control Plane listens on HTTPS only; it has no HTTP port and sends no HSTS
  (SC-2, DC-6).

### FR-016 Error page and catch-all

- One error page for the host, anonymous, with a distinct translated text for
  each of: antiforgery refusal (`400`), not permitted (`403`), not found (`404`),
  internal error (`500`). It shows only that text — no detail, no data — reads
  and writes nothing (SC-4, SC-10).
- Language: the signed-in Owner's language; for an anonymous visitor, Ukrainian
  (`trebovaniya.md` §8, §5).
- An unmatched request answers `404` with the error page to anyone, including an
  anonymous visitor and including before setup (SC-4, v66).
- The choice between the API-6 body and the error page is by path: this Story
  adds nothing under `/api/v1`, so every error it produces is the page (AD-9).
- Unhandled exceptions are mapped by the host's single `IExceptionHandler`
  (AD-9, API-10); `500` never leaks internals.

### FR-017 Data Protection keys

- The Control Plane keeps its ASP.NET Core Data Protection key ring in the
  directory named in its configuration, on a persistent volume, outside the
  container, repository, database and backups (SC-7, DC-3, `trebovaniya.md` §8,
  v64).
- A key ring held only in memory, in the database or shared with an installation
  is not acceptable (SC-7).

### FR-018 Persistence

- The Control Plane uses its own database and its own `DbContext` in
  `ControlPlane.Persistence`, used only from `ControlPlane.Services` (AD-3,
  PC-1).
- This Story introduces the Owner account (ASP.NET Core Identity store) and
  `AuditEvent`, with their EF Core migration in the Control Plane project
  (PC-2, DC-4). Migrations are applied by the deployment step, never at startup;
  no `EnsureCreated()`.
- Timestamps UTC (PC-6). Exact schema: `db-designer`.
- The Owner entity holds login, password hash, lockout state (failed-attempt
  counter, lockout end) and UI language — nothing else is required by this
  Story (`trebovaniya.md` §3).

### FR-019 Localization

- Every page and message of this Story — setup page, sign-in page, home page,
  field and form messages, the common refusal message, the error page texts —
  comes from `ClassroomAgent.ControlPlane.Localization`, with every key present in
  both Ukrainian and English (NFR-073, TC-8). No user-visible string is
  hard-coded.
- Shown in Ukrainian by default; the signed-in Owner sees their account's
  language, which is `uk` for the account this Story creates. Choosing a
  language is US-039.
- Date and number formats follow the Ukrainian locale.
- The setup code printed to the console is operator output, not UI; its
  surrounding console text is not translated *(interpretation I-9)*.

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for their
wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | First run offers setup | FR-001, FR-003 |
| AC-002 | Owner account is created | FR-004, FR-005, FR-010, FR-018, FR-019 |
| AC-003 | Setup is single-use | FR-003, FR-004 step 3, FR-002 |
| AC-004 | Concurrent setup attempts create one account | FR-006, OD-003 |
| AC-005 | Sign-in after setup | FR-007, FR-008, FR-009, FR-010 |
| AC-006 | Weak input is rejected | FR-004 step 2, VR-001 … VR-003, VR-007, section 7 |
| AC-007 | Setup requires the one-time code | FR-002, FR-004 step 4, VR-004, OD-005 |
| AC-008 | Sign-in is audited | FR-012 |
| AC-009 | Pages are translated, Ukrainian by default | FR-019, FR-016 |
| AC-010 | Only the setup, sign-in and error pages are anonymous | FR-014, FR-016 |
| AC-011 | State-changing forms are protected from CSRF | FR-015, FR-011, FR-010 |
| AC-012 | First-run setup is audited | FR-012, FR-004 step 4, FR-005, OD-004 |

## 6. Validation Rules

Declared with DataAnnotations on request types; custom rules as
`ValidationAttribute` / `IValidatableObject` (`trebovaniya.md` §8). Framework
defaults (Identity's `PasswordOptions`, `UserOptions`) are not relied on: they
are configured to impose nothing beyond these rules, and these rules are checked
explicitly.

### VR-001 Setup — login

| Rule | Value |
|---|---|
| required | yes; empty or whitespace-only is invalid |
| length | 4 to 64 characters inclusive |
| allowed characters | Latin letters `A–Z`, `a–z`, digits `0–9`, `.`, `-`, `_` — nothing else (no spaces, no Cyrillic, no `@`) |
| comparison | case-insensitive everywhere (sign-in, "contains" check) |
| invalid examples | `abc` (3), 65 characters, `own er`, `владелец`, `owner@x` |

### VR-002 Setup — password

| Rule | Value |
|---|---|
| required | yes |
| length | 15 to 128 characters inclusive, counted in Unicode characters (code points), not bytes or UTF-16 units *(interpretation I-5)* |
| allowed characters | any, spaces included; not trimmed |
| composition | none — no required digit, upper case or symbol |
| valid examples | 15 lower-case letters with spaces; exactly 128 characters; 15 Cyrillic letters |
| invalid examples | 14 characters; 129 characters |

### VR-003 Setup — password versus login

- The password may not **equal** the login, compared case-insensitively.
- The password may not **contain** the login as a substring, compared
  case-insensitively. The Owner login is always at least 4 characters, so the
  "contains" check always applies (SC-2, v65).
- Error attached to the password field.

### VR-007 Setup — password repeated

- Required.
- Must be exactly equal to the password: an ordinal, case-sensitive comparison
  of the submitted strings, with no trimming or normalization.
- On a mismatch the error is attached to the repeated-password field ("the
  passwords do not match"); the message never shows either value.
- Checked at binding together with VR-001 … VR-003 (OD-004); a mismatch is not
  audited.
- The repetition is used only for this check: it is never stored, logged,
  audited or returned (SC-10).

### VR-004 Setup — code

- Not a binding-time required field (**OD-004**); in the use case an
  empty value is treated exactly like a wrong code.
- Normalization and comparison per **OD-005**.
- Error attached to the code field, never revealing the expected value.

### VR-005 Sign-in

- Login and password are required (non-empty). An empty field: `400`, the form
  shown again naming the missing field; no sign-in attempt, no audit, no counter
  change *(interpretation I-2)*.
- No format or policy rule is applied at sign-in: a login that could never exist
  is simply "unknown login" (FR-008 step 1), so the response never differs by the
  shape of the login.

### VR-006 Messages

- Each field error names the field and the rule in translated, display-safe
  terms (e.g. "the password must be at least 15 characters") — never a stack
  trace, type name or internal detail (SC-10).
- The submitted password, its repetition and the setup code never appear in a
  response, a log line or an audit row (SC-10, FR-012).

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | Owner account only in the Control Plane database; never in an installation or `AppUser` | SC-2, BR-005 |
| S-02 | Password stored only as an Identity hash; never returned, logged or on a DTO | SC-2, AD-8, SC-10 |
| S-03 | Setup succeeds only with the current one-time code; a path working without it is Critical | SC-2 |
| S-04 | Setup code to console only; never to the log file, database, file or configuration | SC-2, SC-10 |
| S-05 | Password policy and lockout exactly per SC-2; no composition rule, no permanent lockout, no distinguishing message | SC-2 |
| S-06 | Sign-in sequence per SC-2 v66, not built on `PasswordSignInAsync` / `CheckPasswordSignInAsync` | SC-2 |
| S-07 | Deny-by-default fallback policy; anonymous only setup, sign-in, error page, `404` catch-all | SC-4 |
| S-08 | Global antiforgery on every non-GET, anonymous forms included; no exemption added | SC-4, API-7 |
| S-09 | GET changes nothing; sign-out is POST | SC-4, API-4 |
| S-10 | Cookie attributes fixed per SC-2; HTTPS only, no HTTP port, no HSTS | SC-2, DC-6 |
| S-11 | Data Protection keys persisted in the configured directory, outside DB and backups | SC-7 |
| S-12 | Audit rows per FR-012, no personal data, no typed login/password/code, never updated or deleted | SC-11, PC-9 |
| S-13 | Error responses carry no stack trace, SQL, type or path; the error page shows translated text only | SC-10, AD-9 |
| S-14 | No path to teaching data; no reference to `ClassroomAgent.Domain`; nothing added to `Contracts` | SC-12, AD-1 |
| S-15 | No outbound call to any service; no breached-password service, no telemetry or error tracker | SC-13 |
| S-16 | Whole Control Plane reachable only from the Owner's private network (deployment, not code) | SC-9, DC-6 |
| S-17 | No database browser or diagnostic endpoint; developer exception page only in local development | SC-6 |
| S-18 | Session: non-persistent cookie, 30-minute sliding idle expiry, 8-hour absolute limit, no "remember me" | OD-002 |

## 8. Error Handling

| Situation | Status | What the Owner sees | Audit |
|---|---|---|---|
| Any page before setup (except setup, error page, unmatched address) | `302` | setup page | — |
| Unmatched address (any time) | `404` | error page "not found" | — |
| Setup GET after the account exists, anonymous | `302` | sign-in page | — |
| Setup GET after the account exists, signed in | `302` | home page (I-1) | — |
| Missing/invalid antiforgery token on any form or sign-out | `400` | error page "page expired" | — |
| Setup: field validation failed | `400` | setup form with field errors (order per OD-004) | — |
| Setup: account already exists / concurrent loser | `409` | setup page with the form-level "already created — sign in" message and a sign-in link (OD-003) | — |
| Setup: code missing or wrong, no account yet | `400` | setup form, error on the code field | refused, "wrong setup code" |
| Sign-in: empty login or password | `400` | sign-in form, field error | — |
| Sign-in: unknown login / locked out / wrong password | identical for all three (I-3: `401`) | sign-in form, common refusal message | refused, per FR-008 |
| Protected page without a session (account exists) | `302` | sign-in page | — |
| Signed-in request denied by a policy | `403` | error page "not permitted" | — |
| Unhandled exception | `500` | error page "internal error" | — |

Expected outcomes (validation failure, refusal, conflict) are results, not
exceptions; exceptions are for failures (AD-9). The concurrent loser's database
conflict is caught in `ControlPlane.Services` and turned into the conflict result,
never surfaced as `500`.

## 9. Non-Functional Requirements

- **NFR-072** — session in an `httpOnly` cookie; no credential client-side.
- **NFR-073** — Ukrainian and English, Ukrainian by default (FR-019).
- **NFR-025** — audited actions in `AuditEvent`, never updated; Control Plane rows
  kept indefinitely (FR-012).
- **NFR-026** — no path to teaching data (S-14).
- **NFR-062** — .NET 10; `Nullable` enabled, warnings as errors.
- **NFR-032** — schema only through EF Core migrations, applied by deployment.
- **Logging** — every log line of the Control Plane obeys SC-10: no password,
  setup code, login typed, cookie or token. The request identifier written into
  audit rows is the one on the request's log lines.
- **Testability** — integration tests run against PostgreSQL via Testcontainers
  (TC-2); the setup code is obtainable in tests without reading console output
  or the log (a substitutable source is a design matter for `dotnet-implementor`
  within FR-002); lockout timing is testable without waiting 15 minutes (an
  injectable clock).

## 10. Out of Scope

- Anything in a school installation (`ClassroomAgent.Web`, `Application`,
  `Domain`, `Infrastructure`).
- `Installation` and `AllowedAdmin` management (US-002, US-003); the legitimacy
  check, Admin login check and push (US-005, US-006, US-003).
- Password reset, password change and account recovery; a second Owner account.
- The Owner choosing their UI language (US-039).
- Viewing the audit log (EPIC-9).
- Two-factor authentication — not required by `trebovaniya.md`.
- Network isolation, certificates and VPN — deployment (DC-2, DC-6), not code.
- Control Plane health checks and backups (DC-11, DC-13) — not asked by this
  Story.

## 11. Open Decisions

Full text, options and recommendations: `docs/decisions/US-001-open-decisions.md`.

| Id | Decision needed | Status | Affected requirements | Resolution |
|---|---|---|---|---|
| OD-001 | Owner password and lockout policy | RESOLVED (v62, v64, v65) | VR-001 … VR-003, FR-008, FR-009 | — |
| — | Where the Owner switches language | RESOLVED (US-039) | FR-019 | — |
| OD-002 | Owner session lifetime | RESOLVED (2026-09-16) | FR-010, S-18; tests of AC-005, AC-011 | non-persistent cookie, 30 min idle, 8 h absolute |
| OD-003 | Response to a setup POST when the account already exists | RESOLVED (2026-09-16) | FR-004 step 3, FR-006, section 8; AC-003, AC-004 | `409`, setup page with a form-level message and a sign-in link |
| OD-004 | Field validation before or after the code check | RESOLVED (2026-09-16) | FR-004, VR-004, section 8; AC-006, AC-007, AC-012 | fields first; empty code treated as wrong in the use case |
| OD-005 | Setup code form, strength, comparison | RESOLVED (2026-09-16) | FR-002, VR-004; AC-007 | ≥128-bit CSPRNG, Crockford Base32, constant-time compare |

OD-002 … OD-005 were each resolved with the recommended option. OD-002 is
recorded for the Owner session only; the same question for Admin and Dean
sessions is not decided by this Story.

### Interpretations for human review

Behaviour not literally fixed by the Story or `trebovaniya.md`, derived here
from the conventions cited. Confirm or correct at `HUMAN_SPEC_APPROVAL`.

- **I-1** A signed-in Owner requesting the setup page or the sign-in page is
  redirected to the home page.
- **I-2** An empty login or password on sign-in is a validation failure (`400`),
  not a sign-in attempt: no audit row, no counter change.
- **I-3** All refused sign-ins answer `401` (API-5 "authentication failed") with
  the same page; the essential rule is that steps 1–3 are indistinguishable.
- **I-4** After a lockout ends, consecutive failures are counted from zero.
- **I-5** "Characters" in the password length rule means Unicode code points.
- **I-6** No return URL after sign-in: the Owner always lands on the home page,
  which also rules out an open redirect.
- **I-7** Sign-out redirects to the sign-in page.
- **I-8** The home page shows only the signed-in state and sign-out.
- **I-9** The console text around the setup code is operator output and is not
  translated.
- **I-10** Validation `400` and code refusal `400` follow API-5 ("validation
  failure") although the forms are Razor pages, not `/api/v1` calls.

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-003, FR-016 | — | S-07 | — |
| AC-002 | FR-004, FR-005, FR-010, FR-018, FR-019 | VR-001, VR-002, VR-003, VR-007 | S-01, S-02 | — |
| AC-003 | FR-002, FR-003, FR-004 step 3 | — | S-03 | OD-003 |
| AC-004 | FR-006, FR-004 step 3 | — | S-03 | OD-003 |
| AC-005 | FR-007, FR-008, FR-009, FR-010 | VR-005 | S-05, S-06, S-10, S-18 | OD-001, OD-002 |
| AC-006 | FR-004 step 2 | VR-001, VR-002, VR-003, VR-006, VR-007 | S-05, S-13 | OD-001, OD-004 |
| AC-007 | FR-002, FR-004 step 4 | VR-004, VR-006 | S-03, S-04 | OD-004, OD-005 |
| AC-008 | FR-012 | — | S-12 | OD-001 |
| AC-009 | FR-019, FR-016 | VR-006 | — | — |
| AC-010 | FR-014, FR-016 | — | S-07 | — |
| AC-011 | FR-015, FR-011, FR-010 | — | S-08, S-09, S-10, S-11 | OD-002 |
| AC-012 | FR-012, FR-004 step 4, FR-005 | VR-004 | S-12 | OD-004, OD-005 |

Requirements with no single AC but required by conventions the Story cites:
FR-017 (Data Protection keys — Story Notes, SC-7), FR-018 (persistence — PC-2),
S-14 … S-17 (Story Notes, SC-6, SC-9, SC-12, SC-13).
