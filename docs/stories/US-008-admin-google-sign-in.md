---
id: US-008
epic: EPIC-6
title: Admin sign-in via Google OAuth with AllowedAdmin verification
slug: admin-google-sign-in
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v78.
---

# User Story

As the **Owner** of the service

I want an Admin to enter their school's installation only through Google OAuth
with the account I approved, with the approval asked of the Control Plane at
every single sign-in

So that I decide who may configure each school without ever going to its server,
a revoked Admin stops at the door the moment I revoke them, and the school's
first human user finally has a way in.

---

# Business Value

This is the first of the Owner's three points of control that actually fires at a
human (`trebovaniya.md` §9, "Три точки контроля Владельца"): the login check
decides **who in a school may configure the program**. Until it exists, an
installation has no users at all — no Admin, therefore no `WorkspaceConnection`
(US-009), no connection instructions (US-010), no "check access" (US-011) and no
Dean accounts (US-012). Everything in EPIC-6 and everything after it stands on
this Story.

It is also the first installation-side **page**, so it brings the host baseline
US-005 and US-006 deliberately left behind: Identity, deny-by-default
authorization, antiforgery, the error page, the localization baseline and the
session policy. That baseline is written once here and reused by every later
screen — including the HTTP `409` mapping of a read-only refusal that US-007
recorded as still missing (US-007 OD-001).

---

# Scope

**In scope:**

- the installation host baseline on the public port: Razor layout and static
  files, HTTPS redirection and HSTS, the deny-by-default fallback authorization
  policy, global antiforgery, the single error page, the catch-all `404`, the
  session and cookie policy, and four new required installation settings — the
  Data Protection key directory, the public base address, the OAuth client id and
  the secret-store reference to the OAuth client secret (OD-001);
- the localization baseline — Ukrainian and English translation files in
  `Application.Localization`, the school's default language from installation
  configuration (Ukrainian if unset), no hard-coded user-visible string;
- ASP.NET Core Identity in the installation: `AppUser` (email identifier,
  application role, sign-in method, UI language, time of the last successful
  sign-in), the roles Admin and Dean, and the first migration that creates them;
- Google OAuth as an external login for the Admin: the start (POST with the
  antiforgery token) and the callback (`state` and correlation cookie), with
  identity scopes only — no Classroom and no Reports scope is requested;
- the `AllowedAdmin` check on **every** Admin sign-in: a call to the Control
  Plane through a port in `Application/Ports`, with no copy and no cache of the
  list, and refusal when the Control Plane does not answer;
- the Admin login check endpoint in the Control Plane and its wire contract in
  `ClassroomAgent.Contracts`;
- just-in-time creation of the Admin's `AppUser` at the first successful sign-in,
  and recording the time of each successful sign-in;
- the installation's `AuditEvent` table and the sign-in and refused-sign-in rows,
  with their refusal categories and without personal data;
- sign-out;
- the signed-in user's landing page: who they are, their role, and the
  installation's legitimacy status with the read-only reason when it applies;
- mapping a read-only refusal that reaches HTTP to `409` with a translated
  message — the closure of US-007 OD-001;
- the two Minor findings carried out of the US-007 security review (F-1, F-2).

**Out of scope:** the Dean sign-in page, the Dean password and lockout policy,
and creating or managing Dean accounts (US-012) — this Story configures Identity
only as far as the Admin's external login needs; letting a user choose their own
UI language (US-039), so only the school default applies here; configuring
`WorkspaceConnection` (US-009); the connection instructions (US-010); "check
access" and the startup self-check (US-011); any call to a Google **data** API
and any data scope (EPIC-1); viewing the audit trail (EPIC-9); the retention
purge that removes an `AppUser` or an audit row (EPIC-10); the school time zone
and retention period, which stay optional until the Stories that first use them;
anything the Owner sees in the Control Plane about sign-ins — the login check is
not shown on the school's page and is not audited there.

---

# Acceptance Criteria

## AC-001 The installation starts only with its new mandatory configuration

**Given** an installation being started

**When** any of the settings this Story makes required is missing or malformed —
the Data Protection key directory (not a directory the process can write to), the
installation's public base address (not an absolute `https://` address), its
Google OAuth client id, or the secret-store reference to its OAuth client secret

**Then**:

- the installation does not start;
- the log states which setting is wrong, **without its value** — the client
  secret reference and the client id are never logged (SC-10, DC-3);
- with the settings valid, the installation starts and keeps its key ring in that
  directory — not in memory, not in the database, not shared with another host
  (SC-7);
- the OAuth client secret is read from the secret store through the reference and
  is never in configuration, in the database or in any log (SC-7, OD-001);
- the redirect URI is derived from the configured public base address, not from
  the incoming request's host (OD-001);
- the school's default UI language stays optional: `uk` or `en` when set, and
  Ukrainian when unset; any other value stops the start the same way (NFR-073).

## AC-002 The public port denies by default

**Given** the installation is running

**When** any public-port endpoint or Razor page is requested without an
authenticated user

**Then**:

- the request is refused — an endpoint that declares no policy closes rather than
  opens (SC-4, API-9);
- the only anonymous endpoints on the public port are the Google OAuth start and
  callback, the error page, the catch-all `404` and static files — the SC-4
  closed list, and a test enumerates every endpoint and fails on any other
  anonymous one;
- the private port keeps answering exactly what US-005 and US-006 gave it, and
  gains nothing from this Story (DC-6, SC-9).

## AC-003 The Admin starts and completes a Google sign-in

**Given** an anonymous visitor on the sign-in page

**When** they start the Google sign-in and Google returns them to the callback

**Then**:

- the start is a `POST` carrying the antiforgery token; a `GET` does not start it
  (`trebovaniya.md` §8, v64);
- the callback is the one `GET` allowed to change state, and it is protected by
  the OAuth `state` parameter and the correlation cookie — a callback with a
  missing, unknown or already used `state`, or without the correlation cookie, is
  refused and creates nothing (SC-4);
- the authorization request asks for identity scopes only — `openid`, `email`,
  `profile`; no Classroom and no Admin Reports scope is requested anywhere in
  this Story (NFR-021, SC-8);
- the authorization request carries the installation's domain as the account-picker
  hint when `LegitimacyState` knows it, and carries none while no legitimacy check
  has ever succeeded (OD-002); the hint is never treated as the access decision;
- the email Google returns is lower-cased before it is used for anything
  (BR-079, SC-3).

## AC-004 AllowedAdmin is asked of the Control Plane at every sign-in

**Given** an Admin who has signed in successfully before

**When** they sign in again

**Then**:

- the installation calls the Control Plane again, with the installation id and
  the lower-cased login email in the **body** of a `POST` — never in the address
  (`trebovaniya.md` §8, v64);
- no answer is cached, and no copy of `AllowedAdmin` exists anywhere in the
  installation database or in memory between sign-ins (BR-012, SC-3);
- a first-login-only check fails the test that asserts a second sign-in calls
  again.

## AC-005 The Control Plane answers the Admin login check

**Given** an `Installation` registered in the Control Plane

**When** its installation calls the login check with the installation id and an
email

**Then**:

- the answer says whether an `AllowedAdmin` entry with that email exists for
  **this** `Installation`, and nothing else — no entry id, no list, no other
  school's data (`trebovaniya.md` §9);
- the comparison is on the stored lower-cased email, exact match, and an entry of
  another `Installation` never matches;
- a suspended `Installation` answers the same way — the status is the legitimacy
  check's business (US-005), not this endpoint's;
- the call is logged with the `Installation` id and the outcome, never the email
  (SC-10);
- nothing is written to the Control Plane audit and nothing to
  `InstanceLicenseCheck` (`trebovaniya.md` §5).

## AC-006 The Control Plane refuses an unknown or malformed login check

**Given** the Control Plane is running

**When** the login check is called with an installation id that no `Installation`
has, or with a body that is missing, malformed or fails validation

**Then**:

- the answer says the installation is unknown, or the request is invalid — never
  a server error, and never a hint about whether some email exists somewhere
  (SC-10);
- an unknown installation id is logged at `Warning`; a rejected body is not
  written to the log (SC-10);
- the endpoint accepts `POST` only, without an antiforgery token and without a
  signed-in Owner — it is on the SC-4 service-channel exemption list, and the
  endpoint enumeration test of US-001 knows it;
- no other Control Plane endpoint becomes anonymous.

## AC-007 An approved Admin gets an AppUser at the first sign-in

**Given** an email that is in `AllowedAdmin` for this `Installation` and has
never signed in here

**When** the sign-in succeeds

**Then**:

- an `AppUser` is created with that email as its identifier, role Admin and
  Google as its sign-in method — no seeding step, no Owner action in the school's
  database (BR-011);
- the `AppUser` has **no local password** — not an empty one, not a random one
  (BR-010, SC-2);
- the time of the last successful sign-in is recorded on it (PC-11 counts the
  retention period from there);
- signing in again reuses the same `AppUser` — a second row for the same email
  is impossible, enforced by the database and not only by a check before insert.

## AC-008 A revoked Admin is refused and keeps their AppUser

**Given** an Admin who has an `AppUser` here, whose `AllowedAdmin` entry the
Owner has since revoked

**When** they sign in

**Then**:

- the sign-in is refused with a plain message that does not reveal whether the
  email is known to the school;
- the `AppUser` row is **not** deleted and not modified beyond the audit trail —
  it is kept for history and removed only by the retention purge (BR-012, PC-11);
- the time of the last successful sign-in does not change;
- a Dean's sign-in and the background legitimacy check are unaffected.

## AC-009 A Control Plane that does not answer refuses the Admin sign-in

**Given** the Control Plane is unreachable, does not answer within the configured
timeout, answers with an error, or answers something that does not parse

**When** an Admin signs in

**Then**:

- the sign-in is refused with a translated message saying the approval could not
  be confirmed and to try again later — not a raw error and not a stack trace
  (SC-10);
- no `AppUser` is created and no session is issued;
- falling back to an earlier answer, to a cached decision, or to "allow because
  they signed in before" fails the test that asserts a refusal;
- the reason is logged at `Error` as a category — unreachable, timeout, error
  answer, unparseable answer, unknown installation — without the response body
  (SC-10, DC-10);
- Dean sign-in and synchronization are not affected by this refusal (BR-012).

## AC-010 Sign-in and refused sign-in are audited without personal data

**Given** any sign-in attempt in the installation

**When** it succeeds or is refused

**Then**:

- an `AuditEvent` row is written with the time in UTC, the actor, the action, the
  outcome and the request id that ties it to the log (SC-11);
- the actor is the `AppUser` id and role when the account exists — including a
  revoked Admin — and "anonymous" without an identifier when it does not; the
  email entered is never written (`trebovaniya.md` §5, v45);
- a refusal records its category: not in `AllowedAdmin`, Control Plane
  unavailable, or the OAuth callback itself failed;
- the row is never updated afterwards, and an installation's rows are deleted
  only by the retention purge (SC-11, PC-11);
- no row carries a name, an email or any other personal datum.

## AC-011 Admin sign-in works in read-only mode

**Given** an installation in read-only mode — suspended, past its grace period,
or never legitimated (US-007)

**When** an approved Admin signs in

**Then**:

- the sign-in succeeds: creating the Admin's `AppUser` at the first sign-in,
  recording the time of the last successful sign-in, Identity's failed-attempt
  bookkeeping and the audit rows are the BR-026 service writes, and they run;
- they run through the existing `PermittedServiceWrite` members
  `SignInBookkeeping` and `AuditEvent` — the closed list is **not** widened, and
  no use case carries a private exemption (US-007 FR-004);
- the landing page shows the mode and its reason (AC-017);
- nothing else this Story adds writes anything in read-only mode.

## AC-012 A read-only refusal that reaches HTTP answers 409

**Given** the installation now has a presentation layer

**When** an `Application` use case refuses an action because of read-only mode

**Then**:

- a REST call receives `409` with the standard error body (API-5, API-6), whose
  message names the reason — suspended, grace period expired, or legitimacy not
  yet confirmed — in the user's language;
- a Razor form receives the error page with the same translated reason;
- the reason travels as data from `Application`, which holds no user-visible
  string, and is translated in presentation (AD-6, NFR-073);
- **API-5 is satisfied end to end from this Story onwards** — US-007 recorded
  that it was not (US-007 OD-001).

## AC-013 Session, cookies and HTTPS

**Given** a signed-in Admin

**When** their session is examined

**Then**:

- the session cookie is `httpOnly`, `Secure` and `SameSite=Lax`, and is not
  persistent — there is no "remember me" (NFR-072, `trebovaniya.md` §8, v64,
  v68);
- the antiforgery cookie is `httpOnly`, `Secure` and `SameSite=Strict`;
- the session ends after 60 minutes of inactivity, and in any case 8 hours after
  the sign-in (NFR-072);
- every state-changing request on the public port carries the antiforgery token
  by one global rule, not a per-action attribute; a request without it or with a
  wrong one is refused with `400` (SC-4);
- the public port redirects HTTP to HTTPS and sends HSTS; the private port stays
  plain HTTP with no cookie and no session (SC-9, DC-6).

## AC-014 Sign-out ends the session

**Given** a signed-in user

**When** they sign out

**Then**:

- it is a `POST` with the antiforgery token — a `GET` does not sign anyone out
  (`trebovaniya.md` §8);
- the session cookie is cleared and the previous cookie no longer authenticates a
  request;
- the user lands on the sign-in page.

## AC-015 The UI is Ukrainian and English

**Given** the school's default language from installation configuration

**When** any page, message or hint this Story adds is rendered

**Then**:

- every user-visible string comes from `Application.Localization` in both
  Ukrainian and English — a hard-coded string is a defect, and a test asserts the
  two resource sets have the same keys with no empty value;
- the school default applies to every user, since choosing a personal language is
  US-039;
- dates follow the rendered language (NFR-073);
- the email Google returned and any other datum from Google is shown as is, never
  translated.

## AC-016 One error page serves every failure

**Given** the installation's public port

**When** an antiforgery refusal (`400`), a forbidden action (`403`), an unmatched
request (`404`) or an unhandled exception (`500`) happens

**Then**:

- one error page answers all four, each with its own translated text, and the
  antiforgery text says the page is out of date and offers the same page again
  (`trebovaniya.md` §8, v64, v65, v66);
- the page shows translated text only — no exception message, no stack trace, no
  request body, no personal datum (SC-10, NFR-023);
- the developer exception page exists only in local development (SC-6, DC-3);
- the page reads and writes nothing, and is reachable anonymously as SC-4 allows.

## AC-017 The signed-in user sees who they are and the installation's status

**Given** a signed-in Admin

**When** they open the landing page

**Then**:

- the page shows their email as it is stored and their role;
- it shows the installation's legitimacy status: whether the installation is in
  read-only mode and, if it is, the reason and the time of the last successful
  check (`trebovaniya.md` §2, permission matrix — visible to both Admin and
  Dean);
- the status is read from `LegitimacyState` through `Application` (US-005), not
  recomputed in the view (AD-3, AD-6);
- the page shows no teaching data — none exists yet.

## AC-018 Logs carry identifiers only

**Given** every log line this Story writes, in the installation and in the
Control Plane

**When** it is written

**Then**:

- it carries internal identifiers, categories, outcomes and states only — never
  an email, a token, an OAuth code, a `state` value, a cookie or a response body
  (SC-10, DC-10);
- lines written inside a request carry the request id, so an audit row and its
  log line can be tied together (SC-11);
- a refused request body is never logged, even on validation failure (SC-10).

---

# Open Decisions

## OD-001 Where the installation's Google OAuth client credentials come from

`trebovaniya.md` fixes that the Admin signs in through Google OAuth, but never
says where the OAuth **web client** of an installation comes from. DC-3's list of
per-installation configuration has no entry for it, and `Installation.ClientId`
in the Control Plane is the *service account's* numeric client id for
domain-wide delegation (BR-020, v43) — a different thing, not reusable here.

Undecided:

- whether one OAuth client in the Owner's Cloud project serves every
  installation, or each school gets its own alongside its own service account
  (SC-7 already requires a separate service account per school so that one leak
  compromises one school);
- where the client id and client secret live — the same secret store as the
  service-account key, with only the reference in configuration (SC-7), or
  configuration directly;
- how the redirect URI is set per school, since each installation has its own
  public host;
- whether the Owner or the school's super-admin creates it.

Options:

1. One OAuth client in the Owner's Cloud project for all installations, with
   every school's redirect URI registered on it; the secret is placed by the
   Owner at deployment, referenced from configuration like the service-account
   key. Fewest moving parts, but one leaked secret touches every school.
2. One OAuth client per school in the Owner's Cloud project, mirroring the
   per-school service account of SC-7; one more step at deployment (DC-2), and a
   leak compromises one school.
3. The school's own Cloud project creates it. Rejected on sight unless the Owner
   says otherwise: it hands a school control over its own Admins' sign-in and
   contradicts BR-006.

**Resolution:** option 2, decided by the Owner on 2026-09-19. Each school gets
its own OAuth web client in the Owner's Cloud project, mirroring the per-school
service account of SC-7: a leaked secret compromises one school, and the secret
is rotated per school without touching the others. The client must be of
Google's "External" user type — an "Internal" client would admit only the
Owner's own Workspace domain, never a school's Admin — and, since the sign-in
requests identity scopes only (`openid`, `email`, `profile`) and no data scope,
it publishes without Google's app verification.

Two details settled with it:

- **The client secret follows the service-account key**: it lives in the
  configured secret store, and only the *reference* to it is in the
  installation's configuration (SC-7). The client id is not a secret and may sit
  in configuration directly.
- **The redirect URI is per school**, so the installation needs to know its own
  public address rather than infer it from a request behind a reverse proxy.
  Adding that required setting is a consequence of this resolution, not a
  separate question: the Specification carries it into DC-3 alongside the client
  id and the secret reference.

This changes `trebovaniya.md` §5, DC-3, SC-7 and the deployment steps of DC-2.
**The requirements change lands first, in its own commit, before the
Specification** — `trebovaniya.md` is never edited inside a Story commit.

## OD-002 Whether the sign-in restricts the account picker to the school's domain

Google's authorization request can carry the school's domain so the picker offers
only accounts in it. The installation knows the domain — it is in
`LegitimacyState` from the legitimacy check (US-005, v54).

`trebovaniya.md` does not mention it. The `AllowedAdmin` check is the real gate
either way: a personal Gmail account that somehow signed in would simply not be
in the list and would be refused with an audit row (AC-008, AC-010). So this is
about clarity for the Admin, not about security.

Options:

1. Restrict the picker to the installation's domain when it is known, and do not
   restrict while no legitimacy check has ever succeeded and the domain is
   therefore unknown. Fewer confusing refusals; one branch of behaviour to test.
2. Never restrict. Simplest; a person who picks the wrong Google account learns
   it only from the refusal.

**Resolution:** option 1, decided by the Owner on 2026-09-19. The account picker
is restricted to the installation's domain when `LegitimacyState` knows it, and
unrestricted while no legitimacy check has ever succeeded — otherwise sign-in
would be impossible exactly when a freshly deployed school needs its Admin most.

School staff routinely keep a personal Google account and a work account in the
same browser; without the hint they pick the wrong one and receive a refusal
whose reason is opaque to them, and every such slip writes an
`AllowedAdmin` refusal into the audit that reads like an outsider's attempt. The
cost is one extra branch with its tests; the gain is fewer confusing refusals and
a quieter audit trail.

The domain is never the authorization decision in either branch: the hint is a
convenience Google itself describes as non-binding, and the `AllowedAdmin` check
stays the gate (SC-3). The Specification must state that plainly so no later
reader mistakes the hint for a control.

---

# Notes

- **The requirements change from OD-001 comes first.** A new version of
  `trebovaniya.md` adds the installation's OAuth web client — the per-school
  client, the secret in the secret store with only its reference in
  configuration, and the public base address — to §5, and `docs/architecture/`
  follows in DC-3, SC-7 and DC-2. That lands in its own commit before the
  Specification stage, never inside a Story commit. Until it does, the
  Specification would be writing a setting no requirement defines.
- This Story writes the installation host baseline once. The Specification should
  state plainly which parts are baseline (Identity, authorization, antiforgery,
  error page, localization, session, Data Protection) and which are the Story's
  own subject (OAuth, the `AllowedAdmin` check, JIT provisioning, audit), so a
  later Story extends the baseline rather than re-deciding it.
- The Control Plane call goes through a port in `Application/Ports` implemented
  in `Infrastructure` (AD-4), next to `IControlPlaneClient` from US-005. Whether
  it is the same port with a second method or a separate one is the
  Specification's call; no Google SDK type and no HTTP type crosses into
  `Application` either way.
- The login check joins the legitimacy check and the push receiver as the third
  service-channel request. All three are `POST`, all three are protected by
  network isolation rather than a token, and the login check carries the email in
  the body (`trebovaniya.md` §8, v64). Unlike the other two, it is served on the
  Control Plane's **own** port, not on the installation's private port.
- The two Minor findings of the US-007 security review land here because this
  Story is the next to touch their files: **F-1** — construct the bare
  `ReadOnlyModeGuard` and `UnitOfWork` inside the decorator factory closures in
  `InstallationServices` and drop the two bare registrations, so no type can
  inject the undecorated unit of work; **F-2** — rethrow `ReadOnlyModeException`
  ahead of the general handler in `CheckLegitimacyUseCase` instead of folding it
  into `SaveFailed`.
- US-007's `IGoogleDataPort` was a synthetic marker with no real port behind it
  (security review F-5). This Story adds **no** Google data port — OAuth is
  authentication only and the sign-in never touches Classroom or Reports — so
  F-5 stays open for the first Story that adds a real one (US-011).
- The `AuditEvent` entity of the installation is a new table, not a copy of the
  Control Plane's: the actor types, actions and refusal categories differ. The
  Control Plane's implementation is a reasonable shape to follow — immutable
  rows, private setters, factory methods that accept no free string that could
  carry an email or a code.
- The installation's `AuditEvent` gains the two sign-in actions only. Every other
  audited action of `trebovaniya.md` §5 arrives with the Story that performs it,
  and the enum grows there.
- No test calls Google. The OAuth handler is substituted and the returned
  identity is synthetic (TC-4). The Control Plane port is substituted in the
  installation's tests, and the Control Plane's login-check endpoint gets its own
  integration tests against PostgreSQL (TC-2).
- Every endpoint this Story adds needs both an allowed-role and a forbidden-role
  test, and the read-only behaviour is proven in `Application`, not as UI state
  (TC-5).
- The Dean sign-in page is on the SC-4 anonymous list but does not exist yet. The
  Specification must not leave a half-built Dean login behind: Identity is
  configured so US-012 can add the page, and nothing anonymous is opened for it
  now (AC-002).
- Time comes from the injectable clock introduced in US-005, so the session
  timeouts (AC-013) and the recorded sign-in time (AC-007) are tested without
  waiting.
- The first version has no Owner-visible view of school sign-ins, by design:
  `trebovaniya.md` §9 keeps the Owner out of a school's data, and a sign-in trail
  is the school's audit (EPIC-9), not the Owner's.
