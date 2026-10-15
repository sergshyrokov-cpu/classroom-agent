---
artifact_type: security_audit
scope: whole solution (installation host and Control Plane)
commit: 71c8c4b
version: 1
status: DRAFT
created_at: 2026-10-15
produced_by: pre-pilot security audit 1 (pilot plan stage 0c), fresh context
critical_findings: 0
major_findings: 0
minor_findings: 3
informational_findings: 7
runtime_checks: PARTIAL
---

# Pre-pilot security audit — whole solution

A standalone review, not a Story review: no Story is active, so the
`security-reviewer` checklist and `docs/architecture/security-conventions.md`
SC-1 … SC-14 were applied directly to the code as of `71c8c4b`. Nothing was
changed during the audit. A finding that needs a code change becomes a Story
the Owner authors.

This is a code, configuration and test-suite review. It is not a penetration
test, and the deployment itself (ACLs, firewall, bindings on the pilot server)
is outside it — those are checked in pilot stages 1–5.

## 1. Executive summary

No Critical or Major finding. The security foundation written before the
fresh-context rule existed (US-001, 006, 007, 008, 009, 011, 012, 014, 015)
holds up when read independently: deny-by-default on both hosts, the closed
anonymous and antiforgery-exemption lists match SC-4 exactly, the Dean sign-in
follows the SC-2 six-step sequence without `SignInManager`, the `AllowedAdmin`
check is a Control Plane call on every Admin sign-in with no cache, read-only
mode has both a guard and a commit backstop, the service-account key is read
from the environment per call and never stored, Google scopes are read-only and
the OAuth sign-in asks for identity scopes only.

Three Minor findings: the Dean failed-attempt counter is not reset when a
lockout starts (F-1), the Data Protection key ring is stored unencrypted and
relies on the folder ACL alone (F-2), and no response carries
`X-Content-Type-Options`, a frame policy on every page, or a CSP (F-3).

## 2. Method and evidence

Read by the reviewing model, file by file: both `Program` classes, both
`Security` namespaces, every controller's authorization attributes, the sign-in
use cases (Google, Dean, Owner, first-run setup), the session and stamp
validation, read-only guard and backstop, the BR-026 registry, the secret
store and Google credential creation, the push receiver and private-port
filters, the Control Plane service-channel controllers, log message templates,
the exception handlers, audit factories and their callers, export renderers,
Razor views (raw HTML output), `.gitignore`, tracked files and git history
(secret patterns, values redacted).

Delegated (facts only, no judgement): `dotnet build`, the full `dotnet test`,
`dotnet list package --vulnerable --include-transitive` and `--deprecated`, to
a `cheap-worker` subagent. Results in §4.

## 3. Checklist SC-1 … SC-14

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | `AppRole` has Admin and Dean; DB check `ck_app_user_role` (`AppUserConfiguration.cs:20`); every policy in `InstallationSecurityServices.cs:211-332` names only Admin/Dean |
| SC-2 Authentication | FINDING F-1 | Six-step sequence `SignInDeanUseCase.cs:44-106`; timing equalisation on unknown login; policy 15–128, no composition, containment ≥4 (`DeanPasswordPolicy.cs`); Admin can never hold a password (`ck_app_user_password_hash`, `ck_app_user_role_sign_in_method`); cookies `__Host-`, httpOnly, Secure, Lax/Strict as fixed (`InstallationSecurityServices.cs:351-357`, `ControlPlaneSecurityServices.cs:216-219`); non-persistent, 60/30 min sliding, 8 h absolute + stamp check per request (`InstallationSession.cs:198-214`); setup code 130 bits CSPRNG, in memory only, voided on creation (`SetupCodeGenerator.cs`, `SetupCodeState.cs`, `FirstRunSetupService.cs`) |
| SC-3 AllowedAdmin | PASS | `CompleteGoogleSignInUseCase.cs:51` calls the Control Plane on every sign-in, refuses on `Unavailable`; CP compares lower-cased email of that installation only (`AdminLoginCheckService.cs:40-43`); `email_verified` required (`GoogleSignInEvents.cs:32`); `hd` hint is never the decision |
| SC-4 Authorization | PASS | Fallback policy on both hosts; anonymous: installation sign-in, error, catch-all, private `/health/*` and push; CP sign-in, setup, error, catch-all, legitimacy and admin-login checks — exactly the closed list; antiforgery global filter on both hosts, exemptions only the push receiver and the two CP service endpoints; OAuth callback forced to GET (`CallbackMethodMiddleware.cs`); no return URL honoured |
| SC-5 Read-only | PASS | Guard in every user write use case; commit backstop refuses any undeclared commit (`ReadOnlyModeUnitOfWork.cs:43-56`); BR-026 registry `PermittedServiceWrites.cs`; bulk deletes only in the retention purge (permitted) |
| SC-6 No DB UI | PASS | No such endpoint found; developer exception page only by framework default in Development |
| SC-7 Key | PASS, F-2 | Key reference from config, value from environment per call, never cached (`EnvironmentSecretStore.cs`, `GoogleClassroomReader.cs:556-580`); OAuth secret by reference (`InstallationSettingsReader.cs:233-240`); key ring per host in its own configured directory — stored unencrypted (F-2) |
| SC-8 Google | PASS | Six read-only scopes (`GoogleDelegationScopes.cs`), per-call least scope set, impersonation of the configured technical account, OAuth scopes cleared to openid/email/profile, `SaveTokens = false` |
| SC-9 Channel | PASS | Private port bound to an IP literal or explicit `*` (`InstallationSettingsReader.cs:347-363`); private paths only on the private port and vice versa (`PrivatePortEndpointFilter.cs`, `PublicPortMiddleware.cs`); push only triggers a check, foreign id → 404, body ≤4 KB, never logged (`StatusPushEndpoint.cs`); WorkspaceConnection domain from LegitimacyState, impersonation user must belong to it (`SaveWorkspaceConnectionUseCase.cs:58-93`); TLS validation to the CP is the framework default — no override anywhere |
| SC-10 Hygiene | PASS, I-5 | Log templates carry ids, codes and counts only (all placeholders reviewed); `RequestPath` replaced by the route template (`RequestRouteEnricher.cs`); no `Include Error Detail` / sensitive-data logging; error pages show only translated text |
| SC-11 Audit | PASS | Every SC-11 installation and CP action has a factory with a caller; `IAuditEventRepository` is add-only; the only delete is the retention purge (`RetentionPurgeStore.cs:114`); rows carry ids, categories and counts, no names or emails |
| SC-12 Owner | PASS | `ControlPlane` does not reference `Domain`; `Contracts` carries installation id, versions, status, compatibility, domain, client id, one email and a yes/no |
| SC-13 Outbound | PASS | Outbound clients: Google (library), Control Plane, CP → installation push; no other HTTP destination |
| SC-14 no-store | PASS | `NoStoreMiddleware` first in both pipelines, static files exempt |

## 4. Build, tests, dependencies

Run by the delegated `cheap-worker` on 2026-10-15 at `71c8c4b`, .NET SDK
10.0.401, Docker 29.8.0 available:

- `dotnet build ClassroomAgent.sln` — 7 projects, 0 errors, 0 warnings.
- `dotnet test ClassroomAgent.sln` — 5917 total, 5917 passed, 0 failed,
  0 skipped (8 min 07 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable package in any project (feed: nuget.org).
- `dotnet list ClassroomAgent.sln package --deprecated` — none.

No new package beyond those already approved; `Microsoft.EntityFrameworkCore.Design`
is `PrivateAssets=all` in both hosts. A clean scan is a point in time: the
monthly patching of pilot stage 7 should repeat it.

## 5. Findings

### F-1 (Minor, AUTHENTICATION, SC-2) — Dean failed-attempt counter is not reset when a lockout starts

- **Evidence:** `src/ClassroomAgent.Domain/Entities/AppUser.cs:181-191`
  increments `AccessFailedCount` and sets `LockoutEnd` once it reaches 5, but
  never resets the counter; only a successful sign-in or an Admin reset does.
  The Owner's sequence resets it when the lockout starts
  (`src/ClassroomAgent.ControlPlane/Services/OwnerSignInService.cs:77-83`,
  "the count of consecutive failures starts again", US-001 I-4) and has a test
  for it (`OwnerSignInLockoutTests.AfterLockoutEnds_CounterStartsFromZero`);
  the Dean path has none.
- **Effect:** after the first lockout, every single wrong password locks the
  Dean out for another 15 minutes, until a successful sign-in. A Dean who
  mistypes once after a lockout is locked again; anyone who knows a Dean's
  email can keep the account locked with one request per 15 minutes instead of
  five. SC-2 states the same policy for Owner and Dean ("5 consecutive failed
  attempts lock sign-in for 15 minutes") and explains that the Dean sign-in is
  public, which is why the lockout must not be easy to make permanent.
- **Correction:** reset the counter when the lockout starts, as the Owner path
  does, with a test of the counter after expiry. Small Story.

### F-2 (Minor, CONFIGURATION, SC-7) — Data Protection key ring stored unencrypted

- **Evidence:** `InstallationSecurityServices.cs:203-205` and
  `ControlPlaneSecurityServices.cs:209-211` call `PersistKeysToFileSystem`
  without `ProtectKeysWith…`. When an explicit repository is set, ASP.NET Core
  does not encrypt the keys at rest, also on Windows, and logs a warning on start.
- **Effect:** the only protection is the folder ACL that SC-7 demands
  ("readable only by the application process"). Anyone who can read the key
  folder can forge a session cookie for any Admin, Dean or the Owner. The pilot
  server is also the Owner's daily-work PC, which makes the ACL step load-bearing.
- **Correction:** (a) at deployment — verify the ACL step of the deployment
  kit README on the real server (pilot stages 3 and 5); (b) as defence in depth,
  `ProtectKeysWithDpapi()` — part of ASP.NET Core, no new package — so a copied
  key file is useless off that Windows account. Whether to add (b) is a
  technical choice for a small Story; it changes nothing users see.

### F-3 (Minor, API_SECURITY) — no `nosniff`, no frame policy on every page, no CSP

- **Evidence:** no `X-Content-Type-Options`, `Content-Security-Policy`,
  `frame-ancestors` or `Referrer-Policy` anywhere in `src/`. `X-Frame-Options:
  SAMEORIGIN` appears only on responses that generate an antiforgery token
  (framework default), so pages without a form can be framed.
- **Effect:** defence in depth only — no XSS sink was found (no `Html.Raw`,
  no `innerHTML`), and every state change is a POST with a token. These headers
  limit the damage of a future mistake and stop clickjacking of read-only pages.
- **Correction:** `trebovaniya.md` §8 does not require them, so adding them is
  a requirements decision (a new §8 rule, then a Story with one host-wide
  middleware per host, as SC-14 did for `no-store`).

### Informational

- **I-1 — concurrent wrong Dean passwords answer 500.** `AppUser` has a
  concurrency token; two parallel failures for one Dean make the second save
  throw `DbUpdateConcurrencyException`, which is not caught
  (`SignInDeanUseCase.cs:130-137`; `UnitOfWork.cs:37` catches only Meet links),
  so the caller gets the 500 page. The counter stays correct and nothing leaks;
  the Owner path retries instead (`OwnerSignInService.cs:36-46`). Worth aligning
  with F-1.
- **I-2 — revoking an Admin does not end a live session.** SC-3 checks
  `AllowedAdmin` at sign-in only; an Admin already signed in keeps access until
  60 minutes idle or 8 hours absolute. An Admin account cannot be disabled in
  the installation. This is the documented design; the Owner should know it
  when revoking someone in a hurry.
- **I-3 — the Google sign-in admits any existing account with that email.**
  `CompleteGoogleSignInUseCase.cs:61-71` does not check that the existing
  `AppUser` is an Admin. If the Owner puts a Dean's email into `AllowedAdmin`,
  that person signs in through Google as a **Dean**, skipping the Dean password
  and the temporary-password step. Needs an Owner action; the person still
  proves ownership of the Google account. Clarify what should happen when a Dean
  becomes an Admin.
- **I-4 — repository hygiene.** `.claude/settings.local.json` is tracked
  (contains no secret; IDE-local config, DoD item 8). An untracked
  `алгоритм.pdf` sits in the repository root and is not ignored — keep it out
  of commits. Git history holds no real key or secret; the matches are
  synthetic test fixtures.
- **I-5 — unhandled exceptions are logged in full.**
  `InstallationExceptionHandler.cs:40` and `ControlPlaneExceptionHandler.cs:16`
  log the exception with its message. Npgsql redacts parameter values by
  default and no code enables error detail, so no personal data was found on
  this path; a third-party message is the residual risk.
- **I-6 — the Control Plane's HTTPS-only, 127.0.0.1 binding is configuration,
  not code.** Nothing in `ControlPlane/Program.cs` refuses an `http://` or
  all-interfaces URL (the installation, by contrast, validates its private
  address). Verify the bindings at pilot stage 3; the `Secure` cookies would
  simply not work over HTTP.
- **I-7 — earlier notes re-checked.** US-045 closed the raw `RequestPath`
  note (route template only, fail-closed). The US-039 switcher note still holds
  and is still harmless: no query string carries personal data (`q` on the
  course list searches course name and section). US-032 INFO-1
  (`LinkMeetCodesStep` has no guard of its own) still holds; the commit
  backstop covers it. US-033 INFO-2 unchanged.

## 6. Positive controls verified

- Deny by default on both hosts; every controller or action declares a policy
  that matches §2 (Admin-only: workspace connection, instruction, access check,
  Dean accounts, erasure; Dean-only: own password; both: everything else).
- Antiforgery validated globally, anonymous forms included; OAuth start is a POST.
- Dean sign-in: unknown login, lockout, wrong password indistinguishable; the
  "disabled" message only with the correct password and no lockout; timing
  equalised on the unknown-login path.
- Sessions: `__Host-` cookies, non-persistent, sliding idle limit, 8-hour
  absolute limit, security stamp checked on every request, rotated on sign-out,
  disable, reset and password change.
- Read-only mode: guard first in each write use case, plus a commit backstop
  that refuses any undeclared write.
- Service-account key and OAuth secret: by reference only, resolved from the
  environment, never logged, never in the database or a DTO.
- Google: read-only scopes, least scope set per call, technical account impersonated.
- Audit: add-only repository, deletion only by the retention purge, no personal data.
- Spreadsheet export writes Google text as string cells (`SetValue`), never as formulas.
- No secret in the repository or its history.

## 7. Limitations

- No penetration test, no running instance, no check of the pilot server.
- The Google API and the Control Plane were not called live (TC-4).
- Text search gives no semantic certainty; conclusions about call paths rest on
  reading the code and on the existing architecture tests.
