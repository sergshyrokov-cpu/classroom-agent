---
artifact_type: security_review
story: US-008
version: 2
status: APPROVED
created_at: 2026-09-20T12:19:13Z
updated_at: 2026-09-20T12:45:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/decisions/US-008-open-decisions.md
    version: 4
  - path: docs/designs/api/US-008-api-design.md
    version: 1
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/database/US-008-db-design.md
    version: 1
  - path: docs/designs/database/US-008-entity-model.md
    version: 1
  - path: docs/tests/US-008-test-strategy.md
    version: 2
  - path: docs/tests/US-008-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-008-implementation-report.md
    version: 2
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 3
security_sensitive: true
runtime_checks: PARTIAL
---

# US-008 Security Review — Admin sign-in via Google OAuth with AllowedAdmin verification

> **Version 2 — verdict PASS.** Version 1 returned BLOCKED on F-1 and Major F-2. The
> Owner chose option 1: `trebovaniya.md` **v79** adds the sign-out stamp rotation to the
> closed list of service writes in §2, in its own commit (`d5a9260`), with BR-026
> mirrored. F-2, F-3 and F-4 were fixed and are covered by tests. F-5 remains Minor and
> documented. Sections 18, 20 and 22 carry the re-review; everything else was re-checked
> against the same evidence and stands. Whole suite at re-review: **1505 passed, 0
> failed, 0 skipped**; build 0 errors, 0 warnings; format clean.

## 1. Executive Summary

**Verdict: PASS.** The security controls this Story exists to establish are present,
wired and independently verified, and the one Critical finding of version 1 was
resolved where its root cause lay — in the requirement, not the code.

`AllowedAdmin` is asked of the Control Plane on every sign-in with no cache and no
fallback, a silent Control Plane refuses the sign-in, an Admin cannot hold a
password at the database level, the public port denies by default with an anonymous
list that matches SC-4 exactly, only identity scopes are requested, the OAuth client
secret never reaches configuration, the database or a log, and every sign-in attempt
writes one audit row carrying no personal datum.

**F-1 (was Critical, now resolved):** sign-out rotates the account's security stamp,
and that write ran in read-only mode without BR-026 authorising it. The list is now
extended in `trebovaniya.md` §2 (v79), BR-026 and the
`PermittedServiceWrite.SignInBookkeeping` comment quote it, and the behaviour — which
AC-014 needs — is unchanged. The control is intact: the closed list grew by amending
the requirement, which is exactly the path BR-026 prescribes.

**F-2 (was Major, now resolved):** the write is covered in all three read-only causes,
including that a replayed cookie stops authenticating and that no audit row is written.

**F-3 and F-4 (were Minor, now resolved):** the callback refuses any method other than
GET or HEAD, so the handler's writing path is unreachable by POST; the per-request
session check reads untracked.

**F-5 remains Minor and documented:** a wrong method on `/sign-in/google` or
`/sign-out` answers `404` where the contract documents `405`. No security impact.

No secret is exposed, no dependency is vulnerable, and no personal data leaves the
installation.

**Recommended next action:** `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

Every input and version is in the front matter. The implementation reviewed is
commit `4a3dc91` plus the working tree, which is clean at review time.

## 3. Security-Relevant Scope

**Installation, public port (new):** `GET /sign-in`, `POST /sign-in/google`,
`GET /signin-google` (served by the authentication handler, not a routed endpoint),
`GET /`, `POST /sign-out`, `GET /error/{statusCode}`, an anonymous catch-all.

**Installation, private port (unchanged):** `/health/live`, `/health/ready`,
`POST /service/v1/status-pushes`.

**Control Plane (new):** `POST /service/v1/admin-login-checks`.

**Assets:** the Admin's email (personal data), the OAuth client secret, session and
antiforgery cookies, the OAuth correlation cookie and `state`, `AllowedAdmin` entries,
`app_user` rows, `audit_event` rows, the Data Protection key ring.

**Trust boundaries crossed:** browser → installation public port (HTTPS);
installation → Control Plane service channel (private network, HTTPS); installation →
Google (authorization endpoint and backchannel); application → PostgreSQL;
authentication handler → Application use case.

## 4. Environment and Tools

| Item | Value |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available; at v1 the suite was run by IMPLEMENTATION and not re-run by the review; at v2 the reviewer observed the corrected classes run (36 cases, all passing) and the whole-suite total of 1505 |
| `dotnet list package --vulnerable --include-transitive` | run: **no vulnerable packages** in any of the seven projects |
| Static review | full: every changed production file read |
| Runtime verification | **PARTIAL** — v1 relied on the suite IMPLEMENTATION ran (1498 passed) without re-executing it, and reasoned F-3 from the framework's contract rather than observing it. At v2 the suite is 1505 passed / 0 failed / 0 skipped and F-3's refusal is runtime-verified; the whole suite was still not re-executed by the reviewer independently |

No secret value was opened, printed or copied. `google_credentials.json` and
`dac-classroom-agent-*.json` were confirmed git-ignored without being read.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | `Domain/Enums/AppRole` has exactly `Admin`, `Dean`; a repository-wide search finds no Teacher or Student role, policy or seed; `ck_app_user_role` allows `admin`, `dean` only; the two policies in `Application/Authorization/InstallationPolicies` carry the one §2 cell this Story needs |
| SC-2 Authentication | PASS | `AppUser.CreateAdmin` takes no password parameter; `ck_app_user_role_sign_in_method` with `ck_app_user_password_hash` make an Admin row with any hash impossible, tested with `""` and `" "`; no password column reachable for an Admin, no reset flow; cookies `__Host-ca-session` (Secure, HttpOnly, Lax, no Expires/Max-Age) and `__Host-ca-antiforgery` (Secure, HttpOnly, Strict), every cookie of the flow Secure including the TempData one; 60-minute sliding idle and 8-hour absolute limit on the injectable clock; HSTS and HTTPS redirection on the public port only. No Dean or Owner password path is touched by this Story |
| SC-3 AllowedAdmin | PASS | `CompleteGoogleSignInUseCase.ExecuteAsync` calls the port before any decision, on every sign-in; `AdminLoginCheckEveryTimeTests` proves two sign-ins make two channel requests, that a revocation between them refuses at once, and that a restart does not resurrect an earlier answer; the installation has exactly three tables and none holds a copy of `AllowedAdmin`; `Unavailable` and `UnknownInstallation` both refuse |
| SC-4 Authorization | PASS | fallback policy requires an authenticated user (`DenyAnonymousAuthorizationRequirement` asserted); the only `[AllowAnonymous]` in `ClassroomAgent.Web` are the sign-in page, the start, the error page, the fallback and the two private groups — the SC-4 list exactly; the endpoint enumeration fails on anything else; `GlobalAntiforgeryFilter` validates every POST/PUT/PATCH/DELETE and the public-port exemption list is empty; the three `IgnoreAntiforgeryToken`/`DisableAntiforgery` uses in the solution are the two Control Plane service-channel POSTs and the installation's private push receiver, all three on the SC-4 exemption list; one error page serves 400/403/404/409/500 and the API-6 body is used under `/api/v1`; private routes answer on the private local port only, by `Connection.LocalPort`; the OAuth callback is the only writing GET — see F-3 for its method constraint |
| SC-5 Read-only mode | PASS (was FINDING F-1) | the sign-in path's writes are the BR-026 members `SignInBookkeeping` and `AuditEvent`, the enum still has exactly four members (test-locked), and the sign-out stamp rotation is now on the closed list of `trebovaniya.md` §2 (v79) with BR-026 and the enum comment quoting it; the write is covered in all three read-only causes |
| SC-6 No DB UI | PASS | no database browser, SQL console or diagnostic endpoint; `UseDeveloperExceptionPage` is never called and `WebApplication` adds it only in Development, while the test host runs as `Test` and deployment as Production |
| SC-7 Key and secrets | PASS | the OAuth client secret is resolved once at start-up from the environment variable the reference names (`Infrastructure/Secrets/EnvironmentSecretStore`), is never in configuration, the database, a DTO, a view or a log; `InstallationSettings` is a method parameter and is **not** registered in DI, so the secret is not resolvable from the container; the Data Protection key ring is persisted to the configured directory, not the database; no committed `appsettings.json` exists in `ClassroomAgent.Web`, so no configuration file can carry a secret |
| SC-8 Google | PASS | `options.Scope.Clear()` then `openid`, `email`, `profile` — stated explicitly, so a future package default cannot widen consent; no Classroom, Reports, Directory or Drive scope anywhere; `SaveTokens = false`, so no Google access or refresh token is kept; no Google data port exists in this Story |
| SC-9 Channel | PASS | the Admin login check is anonymous, antiforgery-exempt, POST only, on the Control Plane's own port, protected by network isolation; the email travels in the body and never in the address; the private port and its address rules are unchanged by this Story and their US-005/US-006 tests pass |
| SC-10 Hygiene | PASS | `SignInLog` writes the account id and a category only; the whole-flow log assertion proves the email, the authorization code, the `state`, the client id, the secret reference, the session cookie name, the Google subject and the display name are all absent; the Control Plane logs the installation id and the outcome and never the email, and a rejected body is never logged; the error page and the `409` body carry no stack trace, SQL, type name or path |
| SC-11 Audit | PASS | exactly one `audit_event` row per sign-in attempt, success or refusal; the actor is the account id and role when one exists and `anonymous` without an identifier otherwise; five refusal categories; ten check constraints; `ck_audit_event_immutable` plus private setters and factories that accept no free string; no index and no foreign key, as PC-9 requires; every stored row asserted free of personal data as JSON; sign-out writes no row (I-13) |
| SC-12 Owner | PASS | `Contracts` gains `AdminLoginCheckRequest` / `AdminLoginCheckResponse` only — the installation id, the email being checked and one boolean; the answer carries no entry id, list or other school's data, asserted by an exact property-set comparison; `ControlPlane` still does not reference `Domain`; nothing about school sign-ins reaches the Owner's pages |
| SC-13 Outbound | PASS | the only outbound HTTP clients in the solution are the installation's Control Plane channel and the Control Plane's status push; Google is reached only by the authentication handler's backchannel. No third destination |

## 6. Authentication and Authorization

The Admin's only door is Google OAuth, and the session it issues is the
installation's own cookie — the handler never signs the external principal in
(`context.HandleResponse()` in `OnTicketReceivedAsync` precedes any framework
sign-in, and the cookie enumeration over the whole flow finds no external cookie).
The session is issued **after** the use case returns success, never before.

Per-role access matches the one permission-matrix cell the Story needs: the landing
page admits Admin and Dean, sign-out admits any authenticated user, and both are
verified not anonymous by the endpoint enumeration. An anonymous request to the
landing page is challenged to `/sign-in` with no return-URL parameter, closing the
open-redirect surface api-design §2.6 names.

The callback's own protections — `state` and the correlation cookie — are the
framework handler's, not a stub's, because the tests drive the real handler offline.
A missing, unknown or replayed `state`, and a genuine `state` without the correlation
cookie, are each refused with nothing created and no Control Plane call.

## 7. Credentials, Key and Google Access

No password is involved: an Admin has none, and this Story touches no Dean or Owner
password path. The OAuth client secret's handling is covered under SC-7 above. Only
identity scopes are requested and no token is persisted, so the Admin's OAuth session
cannot reach a Google data API — there is no such port in this Story.

## 8. Sensitive Data Exposure

The only personal datum this Story stores is an email, in `app_user`. It appears on
the landing page to its own owner and nowhere else: the audit table cannot hold it
(no column could), no log line carries it, the Control Plane answer omits it, and the
`409` and error bodies carry translated text only. Response and view models are DTOs;
no domain entity crosses a controller or view boundary. `password_hash`,
`security_stamp`, `concurrency_stamp` and `access_failed_count` are asserted absent
from every rendered body. Test fixtures use synthetic addresses in
`school-one.example.test` (TC-4).

## 9. Input Validation

The channel body is read explicitly rather than by model binding, so a wrong content
type, malformed JSON, a wrong type and a broken rule all yield the same
`400 invalid_request` with the body never logged or echoed — seventeen malformed
shapes are covered. VR-006's two rules are enforced by
`ServiceChannelEmailAttribute` before any database access, with the 254/255 boundary
tested. The data Google returns is validated too: the email must be present,
syntactically valid and flagged verified, and a failure is a failed callback rather
than an `AllowedAdmin` refusal — the distinction the audit depends on. Start-up
configuration validation refuses ten malformed public base addresses and every blank
required setting, naming the key and never the value.

## 10. API Security

Only the approved operations exist. Status codes and bodies match the contract except
F-5. The `ApiError` shape is API-6's, its `message` is translated in the user's
language, and no operation exposes a field the contract does not define.

## 11. Persistence and Configuration

The single migration `InitialAppUserAndAuditEvent` creates both tables with the
columns, lengths, nullability, defaults, seven and ten check constraints, the one
unique index and no foreign key the design fixes; the Control Plane's schema and model
snapshot are untouched, asserted by a test. No `EnsureCreated()` or `EnsureDeleted()`
exists anywhere. The two databases stay separate (AD-1). No connection string is
committed. Generated databases and `.xlsx` exports remain git-ignored, with the
versioned report templates excepted.

## 12. Logging, Audit and Telemetry

Covered under SC-10 and SC-11. `docs/hooks/tool-usage.jsonl` is unchanged by this
Story and remains git-ignored.

## 13. Dependencies

One package was added: `Microsoft.AspNetCore.Authentication.Google` 10.0.12, in
`ClassroomAgent.Web` only, pinned to the framework major version — exactly what
OD-003 approved. No test package reached a production project, and no project
reference violates `package-map.md`: `Application` still references no NuGet package
and `Domain` none at all, proven by the unchanged US-005 architecture test.

`dotnet list package --vulnerable --include-transitive` reports **no vulnerable
packages** in any project, against nuget.org at review time. This is a point-in-time
result, not a guarantee.

## 14. Security Test Coverage

| Requirement | Test | Status |
|---|---|---|
| Allowed and forbidden role per protected endpoint (TC-5) | `PublicPortAuthorizationTests`, `InstallationSignOutTests` | PASS |
| Endpoint enumeration fails on unlisted anonymous access (TC-5) | `PublicPortAuthorizationTests.OnlySc4EndpointsAllowAnonymous`, `AdminLoginCheckSecurityTests` | PASS |
| Antiforgery on every state-changing endpoint (TC-5) | `InstallationCookieTests.EveryStateChangingPublicEndpoint_WithoutToken_Returns400` | PASS |
| Read-only mode tested in Application, Google ports uncalled (TC-5) | `SignInInReadOnlyModeTests`, US-007 suites unchanged | PASS |
| `AllowedAdmin` on every login; silent Control Plane refuses (TC-5) | `AdminLoginCheckEveryTimeTests`, `ControlPlaneUnavailableSignInTests` | PASS |
| Error bodies carry no internals (TC-3) | `ReadOnlyConflictTests`, `InstallationErrorPageTests` | PASS |
| No live Google or Control Plane; synthetic fixtures (TC-4) | scripted transport and offline handler | PASS |
| Audited actions proven by a test (SC-11) | `AdminSignInAuditTests` | PASS |
| Sign-out's stamp rotation in read-only mode | `SignInInReadOnlyModeTests.InReadOnlyMode_SigningOutEndsTheSession` (3 causes), `InReadOnlyMode_SigningOutWritesNoAuditRow` | PASS (was FINDING F-2) |
| A POST to the callback path | `GoogleSignInCallbackTests.TheCallback_RefusesAnyMethodOtherThanGet_AndWritesNothing` (3 methods) | PASS (was FINDING F-3) |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| A revoked Admin signs in again | refused; the row kept and its stamp unchanged | `RevokedAdminTests` | PASS |
| The Control Plane is silent | refused; no account, no session; no fallback to yesterday's answer | `ControlPlaneUnavailableSignInTests.AnEarlierSuccess_...` | PASS |
| A Control Plane too old to route the path (bare `404`) | `Unavailable`, never "not approved" | `ABare404_IsNeverReadAsNotApproved` | PASS |
| A forged or replayed `state`, or no correlation cookie | refused; nothing created; no channel call | `GoogleSignInCallbackTests` (4 cases) | PASS |
| A crafted `?refusal=` link renders attacker text on the school's page | impossible; the category travels in TempData | `ACraftedQueryParameter_RendersNoMessage` | PASS |
| A stranger learns whether an address is known | impossible; identical refusal pages | `TheRefusalIsIdentical_...` | PASS |
| An account outside the school's domain | refused by the `AllowedAdmin` check, not by the picker hint | `GoogleSignInStartTests` / `RevokedAdminTests` | PASS |
| A forged `X-Forwarded-Host` changes the redirect URI | impossible; the URI comes from configuration | `TheRedirectUri_IgnoresTheRequestHostAndForwardedHeaders` | PASS |
| A captured session cookie replayed after sign-out | no longer authenticates | `ThePreviousCookieReplayed_NoLongerAuthenticates` | PASS, but by the write F-1 questions |
| Two simultaneous first sign-ins of one address | one row, neither fails | `TwoSimultaneousFirstSignIns_...` | PASS |
| A POST to the callback path with a valid state | `405`, nothing created, audited or asked | `TheCallback_RefusesAnyMethodOtherThanGet_AndWritesNothing` | PASS (was FINDING F-3) |
| An oversized or malformed channel body | `400`, nothing recorded, body not logged | `InvalidRequest_...` (17 cases) | PASS |

## 16. Repository Hygiene

The change set is 114 files, all under `src/`, `tests/` and `docs/`. No secret-like
file, generated database, `.xlsx` export or IDE-local configuration is tracked.
`google_credentials.json` and `dac-classroom-agent-*.json` were confirmed still
ignored without being opened. No `.env` file exists.

## 17. Deviations

The implementation report's section 7 lists eight deviations. Assessed:

| Deviation | Security assessment |
|---|---|
| D-1 transaction on `IUnitOfWork` | **Accepted, security-positive.** It makes db-design §4.4 literally true, so a successful sign-in cannot leave an account without its audit row — an audit-integrity property SC-11 depends on. The two US-007 decorators pass it through, so the read-only backstop still sees every commit inside the transaction |
| D-2 sign-out rotates the security stamp | **The mechanism is sound and matches the US-001 Owner-session precedent; its authorisation is not.** See F-1. The per-request stamp check also correctly refuses a disabled account's session |
| D-3 `PublicPortMiddleware` inverted | Accepted. Port separation is preserved in both directions, by this middleware and by `PrivatePortEndpointFilter`; the US-005/US-006 private-port tests pass |
| D-4 the `409` page rendered from the handler | Accepted. The same error page, the same translated reason; no probe endpoint ships |
| D-5 VR-006's email rule, not US-003's | Accepted, and the reasoning is right: applying the stricter rule would answer `400` where the contract requires `allowed: false`, which would leak that an address cannot be an entry |
| D-6 `404` instead of `405` on a wrong method | Minor, no security impact. See F-5 |
| D-7 HSTS and HTTPS redirection scoped to the public port | Accepted, security-positive: applying them host-wide broke the private port, which SC-2 explicitly forbids |
| D-8 empty `AuditTargetType` | Informational. The converter throws on a value that cannot exist |

One further deviation the report does not list, and which is correct: SC-5's blanket
"in read-only mode no call to Google is made at all" is narrowed here to Google **data**
calls, because AC-011 requires the sign-in — and therefore the OAuth token exchange —
to succeed in read-only mode, and FR-013 states the narrowing. Recorded as I-2.

## 18. Findings

> **Re-review (v2).** F-1 resolved upstream by `trebovaniya.md` v79 (`d5a9260`); F-2,
> F-3 and F-4 fixed and covered by tests; F-5 stands as Minor. Each entry below keeps
> its original text and ends with what was verified at re-review.

### F-1 — Critical — READ_ONLY_MODE — SC-5, BR-026

**Affected:** `src/ClassroomAgent.Application/UseCases/PermittedServiceWrites.cs:25`,
`src/ClassroomAgent.Application/UseCases/AccountSessionService.cs`,
`src/ClassroomAgent.Web/Controllers/SignOutController.cs`.

**Observed.** `AccountSessionService.EndSessionsAsync` rotates
`app_user.security_stamp` and commits with `PermittedServiceWrite.SignInBookkeeping`
declared, and the type is registered in `PermittedServiceWrites.Declarations`. The
declaration makes the read-only backstop permit the commit, so signing out writes to
the database while the installation is suspended, past its grace period or never
confirmed.

**Expected.** BR-026 enumerates the permitted service writes and its sign-in
bookkeeping item is a closed list of five: Identity failed-attempt counting and
lockout, recording the time of the last successful sign-in, creating the `AppUser` of
an approved Admin at first login, a Dean changing their own password, and a user
choosing their UI language. A stamp rotation on sign-out is none of them. BR-026 ends:
"Any other write is refused; a new service write is permitted only by extending this
list in `trebovaniya.md` §2." SC-5: "A write path that bypasses the check, or a
service write not on that list, is a Critical finding." `PermittedServiceWrite`'s own
documentation repeats the rule.

**Risk.** Low in effect, high in principle. The write touches one non-personal column
of the signing-out user's own row and writes nothing else, so the immediate exposure
is small. What is damaged is the control itself: BR-026 is the single place the
permitted writes are enumerated, and a write added by registration rather than by
amending the requirement is exactly the silent growth AC-004 of US-007 exists to
prevent. Left standing, it is a precedent for widening read-only mode without a
requirements change.

**Required correction — a human decision, one of:**

1. *(Recommended)* **Extend BR-026.** Add the sign-out stamp rotation to the
   sign-in-bookkeeping item of `trebovaniya.md` §2, in its own commit before any Story
   commit, then mirror it in `business-rules.md` BR-026 and in the
   `PermittedServiceWrite.SignInBookkeeping` comment. The behaviour then stands as
   implemented and AC-014 holds in every mode. This is the option that keeps a user
   able to sign out of a suspended school.
2. **Remove the rotation** and amend AC-014 to what cookie authentication alone gives:
   the browser's cookie is cleared, and a captured cookie stays valid until the
   60-minute idle or 8-hour absolute limit. AC-014's sentence "the previous cookie no
   longer authenticates a request" would have to change, which is a Story amendment.
3. **Keep the rotation but do not declare it**, so sign-out is refused with `409` in
   read-only mode. Not recommended: it leaves a user of a suspended school unable to
   end a session, which is itself a security regression.

**Loop-back:** not `IMPLEMENTATION`. The root cause is that the requirement does not
cover a write the Acceptance Criterion needs, so the responsible artifacts are
`trebovaniya.md` §2 / BR-026 and, under option 2, the Story and Specification.

**Verification after correction.** Under option 1: BR-026 quotes the new write, the
enum comment matches, and a test proves sign-out succeeds in all three read-only
causes. Under option 2: a test proves the cookie's own limits are the only bound, and
AC-014 no longer claims otherwise.

**RESOLVED at re-review (option 1).** `trebovaniya.md` §2 now lists the rotation among
the permitted service writes, with the reason stated in the requirement itself; the
header changelog carries v79; `docs/product/business-rules.md` BR-026 and the
`PermittedServiceWrite.SignInBookkeeping` comment both quote it; the requirements change
is commit `d5a9260`, separate from any Story commit as the Git Policy requires. Verified
by reading the amended list and the mirrored rule, and by
`InReadOnlyMode_SigningOutEndsTheSession` passing for all three BR-025 causes.

### F-2 — Major — TEST_COVERAGE — SC-5, TC-5

**Affected:** `tests/ClassroomAgent.Tests/Web/UseCases/SignInInReadOnlyModeTests.cs`.

**Observed.** No test exercises sign-out while the installation is in read-only mode.
`InstallationSignOutTests` runs only against a host that is not read-only, and
`SignInInReadOnlyModeTests` contains no sign-out case. TC-5 requires each service write
on the BR-026 list to be asserted to still succeed in read-only mode.

**Risk.** The one mode where the new write's permission matters is unverified: a later
change that removed the declaration would refuse sign-out for a suspended school and no
test would notice.

**Required correction.** After F-1 is resolved, add a case asserting the resolved
behaviour for all three BR-025 causes — sign-out succeeding (option 1) or refusing with
`409` (option 3) — and, under option 2, that no write occurs.

**Loop-back:** `IMPLEMENTATION`, once F-1 is decided.

**RESOLVED at re-review.** `InReadOnlyMode_SigningOutEndsTheSession` covers the three
causes and asserts that the stamp moved and that a replayed cookie no longer
authenticates; `InReadOnlyMode_SigningOutWritesNoAuditRow` keeps I-13 true in that mode.
Both observed passing.

### F-3 — Minor — API_SECURITY — SC-4

**Affected:** `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs`
(`options.CallbackPath`).

**Observed.** The OAuth callback is served by
`RemoteAuthenticationHandler.HandleRequestAsync`, which claims the configured
`CallbackPath` **irrespective of the HTTP method**, and `OAuthHandler` reads the code
and `state` from `Request.Query`. A `POST /signin-google?code=…&state=…` therefore
reaches the same writing path, and because the callback is not an MVC endpoint the
global antiforgery filter does not apply to it. `TheCallback_NeedsNoAntiforgeryToken`
asserts only that no routed endpoint accepts an unsafe method, which is true and does
not cover this. **Not runtime-verified**; reasoned from the framework contract.

**Expected.** SC-4 and the contract describe the callback as the one **GET** that
writes.

**Risk.** Low. The protection is unchanged in substance: the request still needs a
`state` matching a correlation cookie the victim's own browser holds, so this is not a
CSRF path. What is wrong is that a method the contract does not describe reaches a
writing path, and the antiforgery guarantee "every state-changing request carries a
token" has an exception nobody declared.

**Required correction.** Constrain the callback to GET — for example a small middleware
ahead of authentication that answers `405` for any other method on that path — or record
the exception explicitly in the contract and the SC-4 exemption reasoning, with a test.

**Loop-back:** `IMPLEMENTATION` (or `API_DESIGN` if the contract is to document it
instead).

**RESOLVED at re-review.** `Web/Security/CallbackMethodMiddleware` answers `405` to any
method other than GET or HEAD on the callback path, ahead of authentication;
`TheCallback_RefusesAnyMethodOtherThanGet_AndWritesNothing` proves POST, PUT and DELETE
are refused with no session, no account, no audit row and no channel call. The behaviour
is now runtime-verified, which v1's reasoning was not.

### F-4 — Minor — PERSISTENCE — defence in depth

**Affected:** `src/ClassroomAgent.Infrastructure/Persistence/Repositories/AppUserRepository.cs`
(`FindByIdAsync`).

**Observed.** The per-request session check reads the account through a **tracked**
query, although it only compares a stamp. Any later `SaveChangesAsync` in the same
request would flush whatever that entity holds.

**Risk.** Low today: nothing in the authenticated request path mutates the entity after
the check. It is a trap for a later Story.

**Required correction.** Read untracked for the check, keeping the tracked read for the
rotation.

**Loop-back:** `IMPLEMENTATION`.

**RESOLVED at re-review.** `IAppUserRepository.GetSessionStateAsync` projects the stamp
and the disabled flag into `AccountSessionState` with `AsNoTracking`, and the tracked
`FindByIdAsync` is now reached only by the rotation.

### F-5 — Minor — API_SECURITY

**Affected:** `src/ClassroomAgent.Web/Program.cs` (the anonymous catch-all), reported
as D-6.

**Observed.** A `GET` to `/sign-in/google` or `/sign-out` answers `404`; the contract
documents `405`.

**Risk.** None. What `trebovaniya.md` §8 requires — that a GET starts no sign-in and
signs nobody out — holds and is asserted, and the Control Plane behaves identically.

**Required correction.** Either align the contract with the host's behaviour at the next
contract revision, or document it. No code change is required for security.

**Loop-back:** none required.

### I-1 — Informational — SECRET_MANAGEMENT

The client secret is copied into `GoogleOptions.ClientSecret`, which is resolvable from
the container as `IOptionsMonitor<GoogleOptions>`. This is inherent to the framework
handler and not a defect; it is recorded so a later reviewer does not read the absence
of `InstallationSettings` from DI as meaning the secret is unreachable in-process.

### I-2 — Informational — GOOGLE_ACCESS

SC-5's "in read-only mode no call to Google is made at all" is narrowed by AC-011 and
FR-013 to Google **data** calls: a sign-in in read-only mode necessarily performs the
OAuth token exchange. The narrowing is documented in the Specification; SC-5's wording
lists only data operations, so there is no contradiction, but the sentence reads broader
than it is.

### I-3 — Informational — AUDIT

`AuditTargetType` is an empty enum whose value converter throws. Unreachable while no
member exists (D-8). The first Story auditing a target adds one.

## 19. Positive Controls

Independently observed and verified:

- an Admin cannot hold a password hash — enforced by two cooperating check constraints,
  tested with an empty string, and by a factory that takes no password parameter;
- `AllowedAdmin` is asked every time, with no table, column, cache or in-memory copy in
  the installation, and a silent Control Plane refuses;
- a bare `404` from the channel is classified `Unavailable`, never "not approved", so a
  failed deployment cannot be read as a revocation;
- deny by default with an anonymous list that matches SC-4 exactly, proven by
  enumeration rather than by inspection;
- the antiforgery exemption list of the whole solution is three endpoints, all on the
  SC-4 list;
- identity scopes only, stated explicitly, with no token persisted;
- the redirect URI comes from configuration and resists a forged `X-Forwarded-Host`;
- the refusal category travels in Data-Protection-protected TempData, so no crafted link
  can render text on the school's sign-in page;
- refusal messages are identical for an unknown and a revoked address;
- one audit row per attempt, no personal datum in any row, immutable through EF Core;
- no personal datum, secret, token, code or `state` in any log line, across the whole
  flow;
- HSTS and HTTPS redirection on the public port only, the private port left plain HTTP;
- no vulnerable package; `Application` package-free and `Domain` dependency-free.

## 20. Open Decisions

OD-001 … OD-004 are all resolved and were implemented as resolved. F-1's decision was
taken by the Owner on 2026-09-20 (option 1) and landed as a requirements change rather
than a Story-level Open Decision, which is the right instrument: BR-026 is a
requirement, and `trebovaniya.md` is amended by a new version in its own commit.

No blocking security Open Decisions remain.

## 21. Review Limitations

- The test suite was **not re-executed** by this review; its result is taken from the
  implementation report and from the committed test sources, which were read.
- F-3 is reasoned from the ASP.NET Core authentication handler's documented behaviour
  and was **not** verified at runtime.
- No penetration testing, no fuzzing and no dependency-provenance analysis were
  performed. The vulnerability scan is a point-in-time nuget.org result.
- Google and the Control Plane were never contacted; their behaviour is assumed to match
  the contracts.
- The review is scoped to US-008 and the files it changed.

## 22. Verdict Rationale

**PASS.** No Critical and no Major finding remains, every SC item the scope touches is
`PASS`, the security tests `testing-conventions.md` requires are present and passing,
and no blocking security Open Decision is open.

F-1 was resolved the way it should have been: the closed list of service writes grew by
amending `trebovaniya.md` §2 in its own commit, not by a registration in code, so the
control BR-026 exists to provide is stronger after the correction than before it. F-2,
F-3 and F-4 are fixed and covered by tests — F-3's behaviour is now runtime-verified,
which v1 could only reason about. F-5 is Minor, has no security impact, and is recorded
for the contract's next revision.

Two implementation choices are worth carrying into `HUMAN_PR_APPROVAL` as
security-positive rather than merely acceptable: the transaction on `IUnitOfWork`, which
makes an account without its audit row impossible, and the scoping of HSTS and HTTPS
redirection to the public port, which keeps the private channel working as SC-2
requires.

Review limitations in section 21 still apply: the suite was not re-executed by the
reviewer at v1, and at v2 the reviewer observed the runs of the corrected classes and
the whole-suite total reported by IMPLEMENTATION. No penetration testing was performed.

**Recommended next stage: `HUMAN_PR_APPROVAL`.**
