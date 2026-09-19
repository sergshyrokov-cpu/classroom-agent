---
artifact_type: specification
story: US-008
version: 2
status: APPROVED
created_at: 2026-09-19T17:13:12Z
updated_at: 2026-09-19T17:50:34Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: trebovaniya.md
    version: 78
  - path: docs/decisions/US-008-open-decisions.md
    version: 2
supersedes: null
---

# US-008 Specification — Admin sign-in via Google OAuth with AllowedAdmin verification

## 1. Overview

US-005 built the installation as a headless process: a host, a database, a
background check and a private port. Nobody can reach it. This Story gives the
installation its **first human user** and, because that user needs a browser, its
**first public-port pipeline**.

Two things are delivered together, and the Specification keeps them apart
throughout:

- **the subject of the Story** — Google OAuth as an external login, the
  `AllowedAdmin` question put to the Control Plane at *every* sign-in, the Control
  Plane endpoint that answers it, just-in-time creation of the Admin's `AppUser`,
  and the installation's first `AuditEvent` rows;
- **the host baseline** US-005 and US-006 deliberately deferred — Identity
  components and `AppUser`, deny-by-default authorization, global antiforgery, the
  single error page, the localization baseline, the session and cookie policy, and
  the Data Protection key ring.

The baseline is written once here and reused by every later screen. It also
closes a debt US-007 recorded openly: the mapping of a read-only refusal to HTTP
`409` with a translated message (US-007 OD-001). **After this Story API-5 is
satisfied end to end.**

Sources: `trebovaniya.md` **v78** §2 ("Модель авторизации", the permission matrix,
the BR-026 closed list, "Сценарий входа"), §3 (`AppUser`), §5 (audit, required
installation settings, UI language, the OAuth client and the public base address),
§6 (Google OAuth2, the service-account model and the per-school OAuth client), §8
(cookies, antiforgery, HTTPS, "GET ничего не меняет", the error page, session
duration, Data Protection keys), §9 ("Идентификация Админа", the account-picker
hint, "Три точки контроля Владельца"); `architecture.md` AD-1, AD-3, AD-4, AD-6,
AD-8, AD-9, AD-10; `package-map.md`; `api-conventions.md` API-5, API-6, API-7,
API-9, API-10; `security-conventions.md` SC-2, SC-3, SC-4, SC-7, SC-9, SC-10,
SC-11, SC-12, SC-13; `persistence-conventions.md` PC-2, PC-11;
`deployment-conventions.md` DC-2, DC-3, DC-6, DC-10; `testing-conventions.md`
TC-2, TC-3, TC-4, TC-5; BR-010 … BR-015, BR-026, BR-079; NFR-021, NFR-023,
NFR-072, NFR-073.

**Open Decisions — all three resolved.** OD-001 and OD-002 arrived from the Story
already resolved by the Owner and already carried into `trebovaniya.md` v78
(commit `0e31e50`), so this Specification is written against a settled
requirement, not a guess. **OD-003 was raised by this Specification**: the Google
authentication handler is a NuGet package that no project references, and
`AGENTS.md` requires an approved Open Decision before one is added. The Owner
resolved it on 2026-09-19 — `Microsoft.AspNetCore.Authentication.Google` in
`ClassroomAgent.Web` only. It is the one dependency change of this Story
(FR-021).

Behaviour not literally fixed by the Story or `trebovaniya.md` is stated as
interpretations I-1 … I-14 (section 11). They are accepted or corrected at
`HUMAN_SPEC_APPROVAL`.

## 2. Business Goal

`trebovaniya.md` §9 names three points where the Owner controls a school. US-005
and US-006 delivered the third — whether the school works at all. This Story
delivers the **first**: who in a school may configure the program.

Its value is that the answer is never cached. The Owner revokes an
`AllowedAdmin` entry in the Control Plane and the next sign-in attempt at the
school fails — no visit to the school's server, no restart, no synchronization
of a list. That is only true if the installation asks every time and refuses when
it cannot ask (BR-012, SC-3). An implementation that checks once, or falls back to
a remembered answer when the Control Plane is silent, would look correct in every
test that signs in successfully and would quietly destroy the control.

The second goal is that the school's staff have a door at all. Until this Story
an installation has no users, so nothing in EPIC-6 and nothing after it can be
built: no `WorkspaceConnection` (US-009), no connection instructions (US-010), no
"check access" (US-011), no Dean accounts (US-012).

## 3. Business Flow

### 3.1 The first Admin of a new school

The Owner has registered the `Installation`, added at least two `AllowedAdmin`
emails (BR-013) and deployed the installation with its own OAuth client (DC-2
step 3, v78). The Admin opens the school's address, is not authenticated, and
lands on the sign-in page. They press the Google button; the browser goes to
Google carrying the school's domain as the account-picker hint; they choose their
domain administrator account and return to the callback. The installation asks
the Control Plane whether that email is in `AllowedAdmin` for this
`Installation`; the answer is yes; an `AppUser` with role Admin is created on the
spot (BR-011), the time of this sign-in is recorded, an audit row is written, and
the session cookie is issued. They land on a page that says who they are and what
state the installation is in.

### 3.2 The same Admin tomorrow

Everything repeats, including the call to the Control Plane. The `AppUser`
already exists, so it is reused, not duplicated; only the time of the last
successful sign-in changes.

### 3.3 The Owner revokes an Admin

The Owner deletes the `AllowedAdmin` entry (US-003). Nothing is pushed and
nothing is synchronized. At the next sign-in the Control Plane answers "not
allowed", the sign-in is refused with a message that reveals nothing about the
school's records, and an audit row records the refusal with the existing
`AppUser` as its actor. The `AppUser` row itself stays, because the audit trail
and the history need it (BR-012, PC-11). The Deans of that school never notice,
and the background legitimacy check keeps running.

### 3.4 The Control Plane is unreachable

The check call times out. The Admin sign-in is refused — not allowed through on
the strength of yesterday's answer — with a translated message saying the
approval could not be confirmed. An audit row records the refusal with category
"Control Plane unavailable" and an anonymous actor when no `AppUser` exists yet.
Deans still sign in (from US-012 onward) and synchronization is untouched
(BR-012).

### 3.5 A school in read-only mode

The school is suspended, or past its grace period, or has never been confirmed
(US-005, US-007). An approved Admin still signs in: creating their `AppUser`,
stamping the sign-in time and writing the audit rows are on the BR-026 closed
list. The landing page tells them the installation is in read-only mode and why.
Nothing else this Story adds writes anything.

### 3.6 A later Story refuses a write

A use case added by US-009 or later calls `IReadOnlyModeGuard`, which throws
`ReadOnlyModeException` (US-007). Before this Story that exception reached no
HTTP layer, because none existed. Now the host's single exception handler turns
it into `409` with the API-6 body under `/api/v1`, or the translated error page
elsewhere. The author of that later Story writes no mapping code.

## 4. Functional Requirements

### FR-001 The installation's new required configuration

Four settings join the required list of `trebovaniya.md` §5 and DC-3. The
installation refuses to start when any is absent or malformed, in the same manner
US-005 established (`InstallationSettingsReader`, `InstallationSettingException`):

| Setting | Rule |
|---|---|
| Data Protection key directory | a directory path the process can create and write to |
| Public base address | an absolute `https://` address, host and optional port, no path, query, fragment or credentials |
| OAuth client id | non-empty after trimming |
| OAuth client secret reference | non-empty after trimming; it is a *reference* into the secret store, never the secret |

The school's default UI language stays **optional**: `uk` or `en`, Ukrainian when
absent; any other value is a start-up failure (NFR-073).

The start-up failure message names the offending setting by its configuration key
and **never prints its value** — this includes the client id and the secret
reference, which are identifiers a log must not carry (SC-10).

The OAuth **client secret** is read from the secret store through the reference,
by the same `Infrastructure/Secrets` mechanism the service-account key uses
(SC-7). It never appears in configuration, in the database, in a DTO, in a view
or in a log. Reading it at start-up, to fail fast on a missing secret, is
permitted; caching it in a static field is not (no static mutable state).

The redirect URI is **computed from the configured public base address**, never
from the incoming request's `Host` or `X-Forwarded-*` headers (v78). A single
place builds it, so the value registered in Google and the value sent to Google
cannot drift.

### FR-002 The public-port pipeline

`ClassroomAgent.Web` gains a public-port pipeline. Its order is fixed, because
several of its guarantees are order-dependent:

1. HTTPS redirection and HSTS (public port only);
2. the exception handler (FR-014);
3. status-code re-execution into the error page (FR-018);
4. static files, restricted to the application's own static-files directory
   (SC-4);
5. routing;
6. request localization (FR-017);
7. authentication, then authorization;
8. the global antiforgery filter (FR-005);
9. endpoints, with an anonymous catch-all answering `404` last.

The private port keeps exactly the surface US-005 and US-006 gave it. This Story
adds nothing to it, and the existing `PublicPortMiddleware` /
`PrivatePortEndpointFilter` separation continues to answer `404` on the public
port for private paths and vice versa (DC-6, SC-9). The private port stays plain
HTTP with no cookie, no session and no antiforgery.

### FR-003 `AppUser` and the Identity components

`AppUser` is a **plain entity in `ClassroomAgent.Domain.Entities`**, as
`package-map.md` places it, holding the fields Identity's components need without
depending on any Identity type (I-2, following the `Owner` precedent of US-001):

| Field | Rule |
|---|---|
| `Id` | surrogate key |
| `Email` | the account identifier; stored lower-cased; unique per installation (a unique index, not only a pre-insert check) |
| `NormalizedEmail` | the normalized form used for lookup |
| `Role` | `AppRole.Admin` or `AppRole.Dean` (`Domain.Enums`) |
| `SignInMethod` | `Google` or `Password` — an Admin is always `Google` |
| `PasswordHash` | **nullable, and null for every Admin** (BR-010, SC-2) |
| `SecurityStamp`, `ConcurrencyStamp` | Identity-shaped stamps |
| `AccessFailedCount`, `LockoutEnd` | sign-in bookkeeping; unused by the Admin path, present because `AppUser` is one table for both roles |
| `UiLanguage` | the user's chosen language; set to the school default at creation, changed only by US-039 |
| `LastSuccessfulSignInAt` | nullable; PC-11 counts the retention period from it, or from creation when never set |
| `IsDisabled` | false at creation; only US-012 sets it for a Dean |
| `CreatedAt`, `UpdatedAt` | by the existing `TimestampInterceptor` |

`AppRole` gets exactly its two documented members. Teacher and Student are Epic 7
and are not added speculatively (`package-map.md`).

One EF Core migration creates `app_user` with its unique index on the normalized
email and the `audit_event` table of FR-012 (PC-2). No `EnsureCreated()`, and no
schema change outside the migration.

**No ASP.NET Core Identity EF store is used**, and no `UserManager` /
`SignInManager`: US-001 established that `UserManager` has no injectable clock
and that the Identity components from the shared framework are used directly.
This Story follows it (I-2). The Admin path needs no password hasher at all.

### FR-004 Authorization: deny by default, anonymous is a closed list

Both the public endpoints and the Razor views declare an authorization policy.
The host sets a **fallback policy requiring an authenticated user**, so an
endpoint whose author forgot an attribute closes rather than opens (SC-4, API-9).

The anonymous endpoints of the public port are exactly:

| Endpoint | Protected by |
|---|---|
| the sign-in page | it shows only translated text and a form; it reads and writes nothing (I-1) |
| the Google sign-in start | `POST` with the antiforgery token (FR-006) |
| the Google sign-in callback | the OAuth `state` parameter and the correlation cookie, then the `AllowedAdmin` check (FR-007) |
| the error page and the catch-all `404` | translated text only; reads and writes nothing |
| static files | the application's own static-files directory, read-only |

A test **enumerates every endpoint of the host** and fails when an anonymous one
appears outside this list, in the manner of the US-001 enumeration test. The
private-port endpoints keep their own US-005/US-006 treatment and are not
re-declared here.

Role policies come from `Application.Authorization`, which holds the permission
matrix of `trebovaniya.md` §2. This Story needs two cells of it: the landing page
is reachable by **Admin and Dean** ("Просмотр статуса легитимности"), and nothing
in this Story is Admin-only. The rest of the matrix is not implemented
speculatively.

### FR-005 Global antiforgery

One global rule validates the antiforgery token on every `POST`, `PUT`, `PATCH`
and `DELETE` of the public port — not a per-action attribute (SC-4;
`trebovaniya.md` §8, v64). The Control Plane's `GlobalAntiforgeryFilter` is the
pattern to follow.

The exemption list for the installation's public port is **empty**. The service
channel's exemptions live on the private port (US-005, US-006) and the OAuth
callback needs none, because it is a `GET` (v64).

A missing or invalid token answers `400`: a Razor form gets the error page with
the "page is out of date, refresh and repeat" text and a link to the same page,
and the submitted data is not preserved; a call under `/api/v1` gets the API-6
body.

### FR-006 Starting the Google sign-in

The start is a **`POST`** carrying the antiforgery token. A `GET` to the same
path does not start a sign-in — `GET` changes nothing (`trebovaniya.md` §8).

The authorization request:

- requests **`openid`, `email`, `profile` only**. Requesting any Classroom or
  Reports scope here is a Critical finding (SC-7 v78, SC-8, NFR-021);
- carries a `state` value and sets the correlation cookie;
- carries the **account-picker hint with the installation's domain** when
  `LegitimacyState` knows it, and carries none while no legitimacy check has ever
  succeeded (OD-002, v78). The domain is read through `Application` from the state
  US-005 stores; the sign-in path never queries the Control Plane for it;
- uses the redirect URI built from the public base address (FR-001).

The hint is never the access decision. A test signs in with an account outside
the school's domain and asserts the refusal comes from the `AllowedAdmin` check,
with its audit category — not from the hint (SC-3).

### FR-007 The callback

The callback is the single `GET` permitted to change state (`trebovaniya.md` §8,
v64). Before anything is created or written it verifies the `state` parameter and
the correlation cookie. A callback with a missing, unknown, expired or already
used `state`, or without the correlation cookie, is refused: no `AppUser`, no
session, no call to the Control Plane, and the audit row of FR-012 with category
"the callback itself failed".

The email Google returns is **lower-cased before it is used for anything** —
comparison, the call to the Control Plane, the `AppUser` lookup and creation
(BR-079, SC-3). The email verification flag Google returns is required to be
true; an unverified email is refused like an invalid callback (I-6).

Nothing else Google returns is stored. No Google token — access or refresh — is
persisted anywhere: the session is the installation's own cookie, and teaching
data is never read on the Admin's behalf (`trebovaniya.md` §2, "Сценарий входа";
SC-8).

### FR-008 The Admin login check port

A port in `Application/Ports` asks the Control Plane the question. No Google SDK
type, no HTTP type and no `Contracts` type crosses into `Application` (AD-4): the
port speaks `Application.Models` types and `Infrastructure/ControlPlane`
translates them, as `IControlPlaneClient` already does.

Whether the question is a second method on `IControlPlaneClient` or a port of its
own is settled by API_DESIGN; either satisfies AD-4 (I-7).

The reply is a closed set of outcomes, not a boolean:

| Outcome | Meaning |
|---|---|
| `Allowed` | an `AllowedAdmin` entry with this email exists for this `Installation` |
| `NotAllowed` | it does not |
| `UnknownInstallation` | the Control Plane does not know this installation id |
| `Unavailable` | unreachable, timed out, an error answer, or an answer that did not parse |

Only `Allowed` admits the user. The three others refuse, each with its own audit
category and log category (FR-012, FR-020).

**Nothing is cached.** No copy of `AllowedAdmin` exists in the installation's
database, in memory, or in the session. A test signs the same Admin in twice and
asserts the port was called twice; a test makes the Control Plane refuse after a
successful sign-in and asserts the next sign-in is refused (BR-012, SC-3).

The wire contract lives in `ClassroomAgent.Contracts` and carries the
installation id, the email being checked and a yes/no answer — nothing else
(SC-12). The email travels in the **body** of a `POST`, never in the address
(`trebovaniya.md` §8, v64). The request timeout is the one US-005 uses for the
service channel (I-8).

### FR-009 The Control Plane's Admin login check endpoint

A new endpoint on the Control Plane's own port, on the service channel beside the
legitimacy check: anonymous, antiforgery-exempt, `POST` only, JSON in and out,
protected by network isolation (SC-4, SC-9). Its path follows the existing
`service/v1/…` convention; the exact value is API_DESIGN's.

HTTP mapping lives in `Controllers`; the rule lives in `Services` and is the only
caller of `Persistence` (AD-3). The body is read explicitly rather than by model
binding, so every malformed request — wrong content type, bad JSON, a wrong type
— becomes the same `400`, exactly as `LegitimacyCheckController` does.

Behaviour:

- a known `Installation` is answered whether an `AllowedAdmin` entry with the
  lower-cased email exists **for that `Installation`**; an entry of another
  `Installation` never matches;
- a **suspended** `Installation` is answered the same way. Status is the
  legitimacy check's business, not this endpoint's;
- an unknown installation id answers `404` with the existing
  `ServiceOutcome.UnknownInstallation`;
- a missing, malformed or invalid body answers `400` with
  `ServiceOutcome.InvalidRequest`;
- the answer carries **nothing else** — no entry id, no list, no other school's
  data, and no hint about whether that email exists elsewhere (SC-12);
- **nothing is written**: no audit row in the Control Plane, no
  `InstanceLicenseCheck`, no last-seen stamp (`trebovaniya.md` §5 — the Control
  Plane audits Owner actions, and a school's sign-in is the school's audit);
- the US-001 endpoint enumeration test is extended so the new anonymous endpoint
  is a declared member of the SC-4 list, not an exception to it.

### FR-010 The sign-in decision

One use case in `Application.UseCases` decides a sign-in, so the order is fixed in
one place and testable without a browser:

1. the callback has already proven `state` and the correlation cookie (FR-007);
2. lower-case the email;
3. ask the Control Plane (FR-008);
4. on `Allowed` — find or create the `AppUser` (FR-011), stamp the sign-in time,
   write the success audit row, and return success;
5. on `NotAllowed`, `UnknownInstallation` or `Unavailable` — write the refusal
   audit row with the matching category and return the refusal, without creating
   or modifying an `AppUser`.

The session is issued by the host only after the use case returns success. The
decision itself contains no HTTP concept and no user-visible string (AD-3, AD-6):
it returns the outcome as data, and presentation translates it.

A disabled `AppUser` is refused with the "account disabled" category. No Admin
can be disabled in this Story — only US-012 disables anyone, and only a Dean —
but the check is written and tested now, because leaving it to a later Story
would mean a disabled account could sign in in between (I-9).

### FR-011 Just-in-time `AppUser` and the sign-in stamp

On the first successful check for an email with no `AppUser`, one is created with
role Admin, sign-in method Google, `PasswordHash` null, `UiLanguage` set to the
school default, and `IsDisabled` false (BR-011). No seeding step, no Owner action
in the school's database.

On every successful sign-in, including the first, `LastSuccessfulSignInAt` is set
to the current time from the injectable clock.

Concurrency: two simultaneous first sign-ins of the same email must not create
two rows. The unique index on the normalized email is the guarantee; the use case
treats a unique-violation on insert as "someone else created it" and re-reads,
rather than surfacing an error (I-10).

A refused sign-in never creates an `AppUser` and never changes
`LastSuccessfulSignInAt`.

### FR-012 The installation's audit

`AuditEvent` in `ClassroomAgent.Domain.Entities` — the installation's own table,
not a copy of the Control Plane's, because the actors, actions and refusal
categories differ. It follows the Control Plane's shape: immutable rows, private
setters, and factory methods that accept no free string which could carry an
email or a token.

| Column | Rule |
|---|---|
| `Id` | surrogate key |
| `OccurredAt` | UTC, from the injectable clock |
| `ActorType` | `AppUser` or `Anonymous` (`System` exists for later Stories) |
| `ActorId` | the `AppUser` id, or null for anonymous |
| `ActorRole` | the role at the time of the action, or null |
| `Action` | `AdminSignIn` — the only action this Story adds |
| `TargetType`, `TargetId` | null here |
| `Outcome` | `Succeeded` or `Refused` |
| `RefusalCategory` | null on success; otherwise one of the closed set below |
| `RequestId` | ties the row to the log line (SC-11) |
| `CreatedAt`, `UpdatedAt` | by the interceptor |

Refusal categories added by this Story: `NotInAllowedAdmin`,
`ControlPlaneUnavailable`, `UnknownInstallation`, `CallbackFailed`,
`AccountDisabled`.

Rules that are not negotiable:

- the actor is the **existing `AppUser`'s id and role** when the account exists —
  including a revoked Admin — and `Anonymous` **without any identifier** when it
  does not. The email entered is never written (`trebovaniya.md` §5, v45);
- **no personal datum** in any row: no name, no email, no Google subject
  identifier;
- rows are **never updated**. A test asserts that an update attempt fails;
- rows are deleted only by the retention purge (PC-11), which is not in this
  Story;
- every other audited action of `trebovaniya.md` §5 arrives with the Story that
  performs it. The enum is not pre-populated (I-11).

### FR-013 Read-only mode on the sign-in path

Creating the Admin's `AppUser`, stamping the sign-in time and writing the audit
rows all happen in read-only mode. They are already members of the US-007 closed
list — `SignInBookkeeping` and `AuditEvent` — so this Story **registers the
sign-in use case against those members and does not widen the list**
(`PermittedServiceWrites`, US-007 FR-004, FR-005; BR-026).

`PermittedServiceWrite` gains no member. A test asserts the enum still has
exactly its four members, as US-007 established.

Nothing else this Story adds writes in read-only mode. The sign-in path reaches no
Google data port: OAuth authenticates a person and touches neither Classroom nor
Reports, so US-007's `IGoogleDataPort` rule has nothing to act on here and finding
F-5 of the US-007 security review stays open for US-011.

### FR-014 The exception handler and the `409` mapping

One `IExceptionHandler` for the host, in `Web.Exceptions` (AD-9, API-10).
Controllers do not `try/catch` to build error responses.

`ReadOnlyModeException` (US-007) maps to **`409`**:

- under `/api/v1` — the API-6 body, whose `message` names the reason in the
  user's language: grace period expired (with the time of the last successful
  check), suspended by the Owner, or legitimacy never confirmed (BR-025, API-5);
- elsewhere — the error page with the same translated reason.

The reason travels as data from `Application`, which holds no user-visible
string; presentation resolves it against the translation files (AD-6, NFR-073).
Any date inside a message is rendered in the school's time zone once that setting
exists; until then, and it does not exist in this Story, the reason text is
written so that it reads correctly with a UTC timestamp (I-12).

Every other unhandled exception becomes `500` with no internals: no stack trace,
no SQL, no class or namespace name, no file path, no Google error, no secret
(NFR-023, SC-10).

**This satisfies API-5 end to end**, closing what US-007 recorded as outstanding.
A test throws `ReadOnlyModeException` from a probe endpoint under `/api/v1` and
asserts the `409` status, the API-6 body shape and the reason in both languages.

### FR-015 Session and cookies

| Cookie | Attributes |
|---|---|
| session | `httpOnly`, `Secure`, `SameSite=Lax`, **not persistent** |
| antiforgery | `httpOnly`, `Secure`, `SameSite=Strict` |
| OAuth correlation | whatever the handler sets by default, plus `Secure` — the return from Google depends on it (v64) |

`SameSite=Lax` on the session cookie is required, not a preference: `Strict`
would leave the first page after the return from Google without a session
(`trebovaniya.md` §8, v64).

The session expires after **60 minutes of inactivity** and in any case **8 hours
after sign-in**, with no "remember me" (NFR-072). Both limits are driven by the
injectable clock so they are tested without waiting.

The public port redirects HTTP to HTTPS and sends HSTS. The Data Protection key
ring is persisted to the configured directory (FR-001), so a restart does not
sign everyone out and does not reject open forms (SC-7).

### FR-016 Sign-out

Sign-out is a **`POST`** with the antiforgery token (`trebovaniya.md` §8). It
clears the session cookie, and a request replaying the previous cookie is no
longer authenticated. The user lands on the sign-in page. Sign-out writes no
audit row: `trebovaniya.md` §5 audits sign-in and refused sign-in, not sign-out
(I-13).

### FR-017 Localization

Ukrainian and English resources in `Application.Localization` (NFR-073). Request
localization resolves the culture in this order: the signed-in user's
`UiLanguage`, otherwise the school default from configuration, otherwise
Ukrainian. The browser's `Accept-Language` is **not** consulted — the requirement
names the school default and the user's stored choice, and nothing else (I-14).

No user-visible string is hard-coded — not a screen, not an error message, not a
hint, not a refusal. A test asserts the two resource sets have the same keys with
no empty value.

Data from Google — the email shown on the landing page — is rendered as is, never
translated. Dates follow the rendered language.

Choosing a personal language is US-039; in this Story `UiLanguage` is only set at
creation.

### FR-018 The error page

One error page for the host, in `Web.Security` (`package-map.md`), serving `400`,
`403`, `404` and `500`, each with its own translated text (`trebovaniya.md` §8,
v64, v65, v66). The antiforgery text says the page is out of date and offers the
same page again.

It shows translated text only — no exception message, no stack trace, no request
body, no personal datum. It reads and writes nothing, and it is anonymous as SC-4
allows. The anonymous catch-all answering `404` for any unmatched request sits
last in the pipeline.

The developer exception page is enabled **only** in local development (SC-6,
DC-3).

### FR-019 The landing page

The page a signed-in user lands on shows:

- their email as stored, and their role;
- the installation's legitimacy status: whether it is in read-only mode and, when
  it is, the reason and the time of the last successful check
  (`trebovaniya.md` §2 permission matrix — Admin **and** Dean).

The status is read through `Application` (`GetLegitimacyModeQuery`, US-005), not
recomputed in the view (AD-3, AD-6). The view model is a DTO; no domain entity
appears in it (AD-8). No teaching data is shown — none exists yet.

### FR-020 Logging

Every line this Story writes, on both hosts, carries internal identifiers,
categories, outcomes and states only — never an email, a token, an OAuth code, a
`state` value, a cookie, a client id, a secret reference or a response body
(SC-10, DC-10).

| Event | Level |
|---|---|
| successful Admin sign-in | `Information`, with the `AppUser` id |
| refused: not in `AllowedAdmin` | `Warning`, with the `AppUser` id when one exists |
| refused: Control Plane unavailable / timeout / error / unparseable | `Error`, as a category |
| refused: unknown installation | `Warning` |
| refused: callback failed | `Warning`, as a category |
| the Control Plane answering a login check | `Information`, with the `Installation` id and the outcome |
| the Control Plane rejecting a malformed body | `Warning`, **without the body** (SC-10) |

Lines written inside a request carry the request id, so an audit row and its log
line can be tied together (SC-11).

### FR-021 Wiring, and the two findings carried from US-007

Dependencies arrive by constructor injection; registration lives in
`Web.Configuration` (`InstallationServices`).

**The one dependency change of this Story** (OD-003, resolved):
`Microsoft.AspNetCore.Authentication.Google` is referenced by
`src/ClassroomAgent.Web/ClassroomAgent.Web.csproj` and by no other project. It
must not reach `Application` or `Domain` — the sign-in decision of FR-010 takes
no authentication type (AD-3, AD-4). Its version is pinned to the same major
version as the framework and the other first-party references. The handler's
scope set is configured **explicitly** rather than inherited from the package
default, so a future default cannot silently widen what a school's Admin consents
to (FR-006, S-06). No other package is added, and `Domain` keeps zero package
references.

Two Minor findings of the US-007 security review are corrected here, because this
Story is the next to touch their files:

- **F-1 (SC-5)** — `InstallationServices` registers the bare `ReadOnlyModeGuard`
  and `UnitOfWork` so the decorator factories can resolve them, which leaves a
  route by which a `Web` type could inject the undecorated unit of work and commit
  past the backstop. Both are constructed **inside the factory closures** and the
  two bare registrations are removed. A test asserts that resolving `IUnitOfWork`
  yields the decorated chain and that no bare registration exists.
- **F-2 (SC-5)** — `CheckLegitimacyUseCase` folds any exception from its commit
  into `CheckOutcome.Failed(SaveFailed)`. A `catch (ReadOnlyModeException) { throw; }`
  is added ahead of the general handler, mirroring the existing
  `OperationCanceledException` filter, so a refusal can never be downgraded into
  an ordinary save failure.

Neither changes observable behaviour today; both are proven by a test that would
fail without them.

## 5. Acceptance Criteria

Each Story Acceptance Criterion is satisfied by the functional requirements
listed; full mapping in section 12.

| Story AC | Satisfied by |
|---|---|
| AC-001 The installation starts only with its new mandatory configuration | FR-001 |
| AC-002 The public port denies by default | FR-002, FR-004 |
| AC-003 The Admin starts and completes a Google sign-in | FR-005, FR-006, FR-007 |
| AC-004 `AllowedAdmin` is asked of the Control Plane at every sign-in | FR-008, FR-010 |
| AC-005 The Control Plane answers the Admin login check | FR-009 |
| AC-006 The Control Plane refuses an unknown or malformed login check | FR-009 |
| AC-007 An approved Admin gets an `AppUser` at the first sign-in | FR-003, FR-010, FR-011 |
| AC-008 A revoked Admin is refused and keeps their `AppUser` | FR-010, FR-011, FR-012 |
| AC-009 A Control Plane that does not answer refuses the Admin sign-in | FR-008, FR-010, FR-020 |
| AC-010 Sign-in and refused sign-in are audited without personal data | FR-012 |
| AC-011 Admin sign-in works in read-only mode | FR-013 |
| AC-012 A read-only refusal that reaches HTTP answers `409` | FR-014 |
| AC-013 Session, cookies and HTTPS | FR-002, FR-005, FR-015 |
| AC-014 Sign-out ends the session | FR-016 |
| AC-015 The UI is Ukrainian and English | FR-017 |
| AC-016 One error page serves every failure | FR-005, FR-018 |
| AC-017 The signed-in user sees who they are and the installation's status | FR-019 |
| AC-018 Logs carry identifiers only | FR-020 |

## 6. Validation Rules

Framework defaults are not relied on. Every rule below is stated explicitly.

### VR-001 The public base address

Absolute; scheme exactly `https`; a host; an optional port; **no** path beyond
`/`, no query, no fragment, no user info. Rejected at start-up with the key name
and without the value.

### VR-002 The OAuth client id and secret reference

Both non-empty after trimming. Neither is validated against Google at start-up —
a wrong value surfaces as a failed sign-in, not a failed boot (I-5). Neither is
ever logged.

### VR-003 The Data Protection key directory

A path the process can create if absent and write to. Unwritable or a file rather
than a directory is a start-up failure.

### VR-004 The default UI language

`uk` or `en`, case-insensitive, or absent. Any other value is a start-up failure.

### VR-005 The email returned by Google

Non-empty; a syntactically valid address; Google's verified flag true; lower-cased
before use. A failure of any of these is treated as a failed callback (FR-007),
never as "not allowed" — the distinction matters in the audit.

### VR-006 The Admin login check request (Control Plane side)

- `installationId` — present, a UUID;
- `email` — present, non-empty after trimming, at most 254 characters, a
  syntactically valid address, compared lower-cased.

Any failure is `400` with `ServiceOutcome.InvalidRequest`. The rejected body is
never logged or echoed (SC-10). Validation happens before any database access.

### VR-007 The `state` and correlation cookie

Present, well-formed, unexpired and not previously used. A failure refuses the
callback before any Control Plane call and before any write.

## 7. Security Requirements

| # | Requirement | Source |
|---|---|---|
| S-01 | Deny by default: a fallback policy requires an authenticated user; anonymous access is the closed list of FR-004, proven by an enumeration test | SC-4, API-9 |
| S-02 | `AllowedAdmin` is checked on **every** sign-in, by calling the Control Plane; no copy, no cache, no fallback to an earlier answer. A first-login-only check or a cached answer is a Critical finding | SC-3, BR-012 |
| S-03 | The Control Plane not answering **refuses** the Admin sign-in; Dean sign-in and synchronization are unaffected | SC-3, BR-012 |
| S-04 | An Admin `AppUser` has no local password — not empty, not random. `PasswordHash` is null | SC-2, BR-010 |
| S-05 | A revoked Admin's `AppUser` is kept, not deleted; login is refused | SC-3, BR-012, PC-11 |
| S-06 | The sign-in requests `openid`, `email`, `profile` only. Any Classroom or Reports scope on this channel is a Critical finding | SC-7 (v78), SC-8, NFR-021 |
| S-07 | The account-picker domain hint is never the access decision | SC-3, v78 |
| S-08 | The OAuth client secret lives in the secret store, reached by reference; never in configuration, the database, a DTO, a view or a log. There is no UI that accepts it | SC-7 |
| S-09 | No Google token, access or refresh, is persisted. The session is the installation's own cookie | SC-8 |
| S-10 | The callback is proven by `state` and the correlation cookie before anything is created or written | SC-4 |
| S-11 | Cookies: `Secure` everywhere; session `httpOnly` + `Lax` + non-persistent; antiforgery `httpOnly` + `Strict`. HTTPS redirection and HSTS on the public port | SC-4, SC-7, NFR-072 |
| S-12 | Antiforgery on every state-changing public request by one global rule; the installation's public-port exemption list is empty | SC-4 |
| S-13 | Audit rows carry no personal data; the actor is an id or "anonymous"; rows are never updated | SC-11 |
| S-14 | The refusal message reveals nothing about the school's records — a stranger cannot learn from it whether an email is known | SC-10 |
| S-15 | Logs carry identifiers, categories and states only; never an email, token, code, `state`, cookie, client id, secret reference or response body | SC-10, DC-10 |
| S-16 | `Contracts` gains only the installation id, the email checked and a yes/no answer. No teaching data, no school statistics | SC-12, SC-13 |
| S-17 | The Control Plane's login check writes nothing — no audit row, no `InstanceLicenseCheck` | §5 |
| S-18 | The Data Protection key ring is a directory on a persistent volume, outside the container, repository and database | SC-7 |
| S-19 | Errors leak no internals: no stack trace, SQL, type name, path, Google error or secret | NFR-023, SC-10 |
| S-20 | The undecorated `IUnitOfWork` is unreachable from DI (F-1), and `ReadOnlyModeException` is never downgraded to a save failure (F-2) | SC-5 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Missing or malformed required setting | the installation does not start; the log names the key, never the value |
| Antiforgery missing or invalid | `400`; error page with the "out of date" text, or the API-6 body under `/api/v1` |
| Callback fails `state` / correlation / email validation | refusal message; audit `CallbackFailed`; nothing created |
| Email not in `AllowedAdmin` | refusal message revealing nothing; audit `NotInAllowedAdmin` |
| Control Plane unreachable, timeout, error, unparseable | translated "approval could not be confirmed, try again later"; audit `ControlPlaneUnavailable`; log `Error` by category, without the body |
| Control Plane does not know the installation id | same shape of refusal; audit `UnknownInstallation`; log `Warning` |
| Disabled account | "account disabled, contact your Admin"; audit `AccountDisabled` |
| Authenticated but not permitted | `403` error page |
| Unmatched request | `404` from the anonymous catch-all |
| `ReadOnlyModeException` | `409` — API-6 body under `/api/v1`, error page elsewhere, reason named |
| Any other unhandled exception | `500` with no internals |
| Control Plane: malformed login-check body | `400` `invalid_request`; body never logged |
| Control Plane: unknown installation id | `404` `unknown_installation`; logged `Warning` |

Exceptions signal failures, not expected outcomes (AD-9). A refused sign-in is an
expected outcome and is returned as data, never thrown.

## 9. Non-Functional Requirements

- **NFR-072** — session in an `httpOnly` cookie, no credential client-side, `GET`
  changes nothing except the OAuth callback, 60-minute idle and 8-hour absolute
  limits, no "remember me".
- **NFR-073** — Ukrainian and English; school default from configuration,
  Ukrainian when unset; no hard-coded user-visible string; Google data untranslated.
- **NFR-070** — the sign-in page, the error page and the landing page work at
  phone width.
- **NFR-021** — every Google scope used anywhere in the system is read-only; this
  Story adds only identity scopes, which read no data at all.
- **NFR-023** — no personal data in logs or in an HTTP error body.
- **NFR-062 / coding conventions** — nullable enabled, warnings as errors, async
  methods end in `Async` and take a `CancellationToken`, no `.Result`, no
  `.Wait()`, constructor injection only, one public type per file.
- **Testing** (TC-2, TC-3, TC-4, TC-5): no test calls Google — the authentication
  handler is substituted and the returned identity is synthetic; the Control Plane
  port is substituted in the installation's tests; the Control Plane endpoint gets
  integration tests against PostgreSQL via Testcontainers; every endpoint added
  here has both an allowed-role and a forbidden-role test; read-only behaviour is
  proven in `Application`, not as UI state.

## 10. Out of Scope

- The Dean sign-in page, the Dean password and lockout policy, and creating,
  disabling, re-enabling or resetting a Dean account — **US-012**. Identity is
  configured only as far as the Admin's external login needs, and nothing
  anonymous is opened for a Dean page that does not exist.
- Choosing a personal UI language — **US-039**. Only the school default applies.
- `WorkspaceConnection` — **US-009**; the connection instructions — **US-010**;
  "check access" and the startup self-check — **US-011**.
- Any Google **data** API, any data scope, any Classroom or Reports call — EPIC-1
  onwards. US-007's `IGoogleDataPort` gains no real implementation here, so
  security-review finding F-5 stays open for US-011.
- Viewing the audit trail — EPIC-9. The retention purge that removes an `AppUser`
  or an audit row — EPIC-10 (PC-11).
- The school time zone and retention period, which stay optional until the
  Stories that first use them.
- Anything the Owner sees about school sign-ins: the login check is not shown on
  the school's page and is not audited in the Control Plane.
- Every audited action of `trebovaniya.md` §5 other than Admin sign-in.

## 11. Open Decisions

Full text, options and resolutions in
`docs/decisions/US-008-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 The installation's OAuth client and secret | **RESOLVED** by the Owner on 2026-09-19 (option 2), carried into `trebovaniya.md` v78 | none — FR-001 and S-08 are written against the resolution |
| OD-002 The account-picker domain hint | **RESOLVED** by the Owner on 2026-09-19 (option 1), carried into `trebovaniya.md` v78 | none — FR-006 and S-07 are written against the resolution |
| OD-003 The Google authentication handler package | **RESOLVED** by the Owner on 2026-09-19 (option 1): `Microsoft.AspNetCore.Authentication.Google` is added to `ClassroomAgent.Web` and to no other project | none — FR-006, FR-007 and FR-021 are written against the resolution |

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally. Each
is a decision this Specification makes explicit so it can be corrected at the
gate rather than discovered in code.

- **I-1** One sign-in page serves the host and is the SC-4 "Dean sign-in page"
  entry. In this Story it carries only the Google start form; US-012 adds the
  password form to the same page rather than opening a second anonymous one.
- **I-2** `AppUser` is a plain `Domain` entity with Identity-shaped fields, and
  the Identity components from the shared framework are used directly — no
  Identity EF store, no `UserManager`, no `SignInManager`. This follows the
  `Owner` precedent established by US-001 (implementation report §7.1) and keeps
  `Domain` free of package dependencies (AD-3). It also means no Identity NuGet
  package is added.
- **I-3** `AppUser` is one table for both roles, so the lockout and
  failed-attempt columns exist from this Story although the Admin path never uses
  them. US-012 fills them rather than migrating the table again.
- **I-4** The landing page is the post-sign-in destination for both roles. It is
  not a dashboard; later Stories replace its content.
- **I-5** The OAuth client id and secret are not verified against Google at
  start-up. A wrong value fails a sign-in, with the refusal and the log line of a
  failed callback — the installation still starts, so a Google outage or a
  mistyped secret does not take the school's read-only views down with it.
- **I-6** An email Google reports as unverified is refused, and is recorded as a
  failed callback rather than as "not in `AllowedAdmin`" — it is a defect of the
  identity, not a decision of the Owner.
- **I-7** Whether the login check is a second method on `IControlPlaneClient` or
  its own port is API_DESIGN's choice; both satisfy AD-4.
- **I-8** The login check reuses the service-channel request timeout of US-005
  rather than introducing a second timeout setting. A sign-in that waits longer
  than a background check would be worse, not better.
- **I-9** The disabled-account check is written and tested now although only
  US-012 can disable anyone, so no window exists in which a disabled account
  could sign in.
- **I-10** A unique-violation on the first insert of an `AppUser` is treated as a
  concurrent first sign-in: re-read and continue, not an error to the user.
- **I-11** The audit action and refusal-category enums gain only what this Story
  performs. A later Story extends them, as US-007 established for
  `PermittedServiceWrite`.
- **I-12** No message rendered by this Story depends on the school time zone,
  which is not yet a setting. The read-only reason text is written so it reads
  correctly with a UTC timestamp; US-009 onwards revisits it when the setting
  arrives.
- **I-13** Sign-out writes no audit row: `trebovaniya.md` §5 lists sign-in and
  refused sign-in, and the list is closed.
- **I-14** `Accept-Language` is not consulted. The culture is the user's stored
  choice, else the school default, else Ukrainian — nothing else, because
  NFR-073 names exactly those sources.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001 | VR-001, VR-002, VR-003, VR-004 | S-08, S-18 |
| AC-002 | FR-002, FR-004 | — | S-01 |
| AC-003 | FR-005, FR-006, FR-007 | VR-005, VR-007 | S-06, S-07, S-09, S-10, S-12 |
| AC-004 | FR-008, FR-010 | — | S-02 |
| AC-005 | FR-009 | VR-006 | S-16, S-17 |
| AC-006 | FR-009 | VR-006 | S-14, S-15, S-17 |
| AC-007 | FR-003, FR-010, FR-011 | VR-005 | S-04 |
| AC-008 | FR-010, FR-011, FR-012 | — | S-05, S-13, S-14 |
| AC-009 | FR-008, FR-010, FR-020 | — | S-03, S-15 |
| AC-010 | FR-012 | — | S-13 |
| AC-011 | FR-013 | — | S-20 |
| AC-012 | FR-014 | — | S-19 |
| AC-013 | FR-002, FR-005, FR-015 | — | S-11, S-12 |
| AC-014 | FR-016 | — | S-11, S-12 |
| AC-015 | FR-017 | VR-004 | — |
| AC-016 | FR-005, FR-018 | — | S-19 |
| AC-017 | FR-019 | — | S-01 |
| AC-018 | FR-020 | — | S-15 |
| — (carried US-007 findings) | FR-021 | — | S-20 |

Requirement sources: `trebovaniya.md` v78 §2, §3, §5, §6, §8, §9; BR-010, BR-011,
BR-012, BR-013, BR-014, BR-015, BR-026, BR-079; NFR-021, NFR-023, NFR-070,
NFR-072, NFR-073; AD-1, AD-3, AD-4, AD-6, AD-8, AD-9, AD-10; API-5, API-6, API-7,
API-9, API-10; SC-2, SC-3, SC-4, SC-5, SC-7, SC-8, SC-9, SC-10, SC-11, SC-12,
SC-13; PC-2, PC-11; DC-2, DC-3, DC-6, DC-10; TC-2, TC-3, TC-4, TC-5.
