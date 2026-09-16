---
artifact_type: security_review
story: US-001
version: 1
status: APPROVED
created_at: 2026-09-16T12:59:43Z
updated_at: 2026-09-16T12:59:43Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/evidence/US-001-implementation-report.md
    version: 1
  - path: docs/designs/api/US-001-api-design.md
    version: 1
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
  - path: docs/designs/database/US-001-db-design.md
    version: 1
  - path: docs/designs/database/US-001-entity-model.md
    version: 1
  - path: docs/tests/US-001-test-strategy.md
    version: 2
  - path: docs/tests/US-001-ac-test-matrix.md
    version: 2
  - path: docs/decisions/US-001-open-decisions.md
    version: 4
  - path: trebovaniya.md
    version: 68
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-001 Security Review — Owner first-run setup

## 1. Executive Summary

**Result: PASS.** No Critical or Major finding. One Minor finding (F-01, the
HTTPS-only listener is not stated as a deployment step) and five Informational
observations.

Principal controls, each checked in code and backed by a passing test: the
one-time setup code (130 bits, CSPRNG, console only, constant-time compare,
voided on creation); a database-level singleton for the Owner account; the SC-2
v66 sign-in sequence with 5/15-minute lockout, not built on `PasswordSignInAsync`;
indistinguishable refusals; deny-by-default authorization with an anonymous list
equal to SC-4; global antiforgery with no exemption; `__Host-` cookies that are
`Secure`, `HttpOnly`, `SameSite=Strict` and non-persistent; 30-minute idle and
8-hour absolute session limits with server-side invalidation at sign-out through
the security stamp; immutable audit rows at the entity, DbContext and trigger
levels; no reference from the Control Plane to any installation project.

Limitations: code, configuration and test review plus an independent test run;
no penetration test, no deployed-host check (HTTPS binding, file permissions of
the key directory), request id on log lines not observed at runtime (§21).

Recommended next action: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

As listed in `inputs` above. None is `SUPERSEDED`. The Specification records
`trebovaniya.md` v67 and Open Decisions v2; the designs and tests record v68 and
v4. The v68 additions (session lifetime, static files in SC-4, Control Plane
logging) and OD-006/OD-007 do not change a security requirement of the
Specification, and all are implemented — not a stale input in substance.

## 3. Security-Relevant Scope

**Host:** `ClassroomAgent.ControlPlane` only; nothing in an installation.

**Exposed functionality:** `GET/POST /setup`, `GET/POST /sign-in`,
`POST /sign-out`, `GET /`, `/error/{statusCode}`, the `404` catch-all, static
files under `wwwroot`.

**Assets:** the Owner password hash; the one-time setup code (process memory);
the session cookie and the Data Protection key ring that protects it; the
security stamp; the lockout state; `audit_event` rows.

**Trust boundaries:** the Owner's browser → Control Plane (private network, HTTPS,
DC-6); controllers → `ControlPlane.Services`; services → PostgreSQL; operator
console (standard output) → the person deploying the service.

**Security components:** `Security/*` (cookie auth, fallback policy, setup gate,
antiforgery filter, exception handler, error page, culture provider),
`Services/*` (setup, sign-in, session, setup code), `Persistence/*` (schema,
interceptor, migration with trigger), `Controllers/*` request types and validation
attributes, `Program.cs` (pipeline, Serilog).

## 4. Environment and Tools

| Item | Value |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | Docker engine 29.8.0; `postgres:17-alpine` |
| `dotnet test --solution ClassroomAgent.sln` | exit 0; 158 total, 158 passed, 0 failed, 0 skipped (run by this review) |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | exit 0; no vulnerable package in either project (source: nuget.org) |
| Static searches | `UseDeveloperExceptionPage`, Swagger/OpenAPI, `MapGet`/`MapPost`, health checks, `IgnoreAntiforgeryToken`, `DisableAntiforgery`, `EnableSensitiveDataLogging`, `Include Error Detail` — none in production code |
| `git check-ignore` | credentials, telemetry, `bin/`, `obj/`, database cache, `.xlsx`, `logs/` ignored |

Not run: the application on a real listener, a browser, a penetration test.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | one role `Owner` (`Security/OwnerSession.OwnerRole`); no Teacher/Student; no installation role touched |
| SC-2 Authentication | PASS (F-01 Minor on deployment wording) | §6, §7 |
| SC-3 AllowedAdmin | NOT_APPLICABLE | no Admin login in this Story |
| SC-4 Authorization | PASS | §6, §10 |
| SC-5 Read-only mode | NOT_APPLICABLE | Control Plane has no read-only mode; no installation write |
| SC-6 No DB UI | PASS | no diagnostic endpoint; no developer exception page in any environment |
| SC-7 Secrets and keys | PASS (Info F-05) | Data Protection `PersistKeysToFileSystem(DataProtection:KeyDirectory)`; host refuses to start without the setting; no key or connection string in the repository; no service-account key in this Story |
| SC-8 Google | NOT_APPLICABLE | no Google access |
| SC-9 Channel | NOT_APPLICABLE | no service-channel endpoint yet; private-network reachability is deployment (S-16) |
| SC-10 Hygiene | PASS (Info F-03) | §8, §12 |
| SC-11 Audit | PASS | §12 |
| SC-12 Owner and teaching data | PASS | `ControlPlaneReferenceTests` (assembly and `.csproj`); no `Contracts` change; no data-plane type |
| SC-13 Outbound | PASS | no HTTP client, no external service, no breached-password check; logs to a local file |

## 6. Authentication and Authorization

**Requirements:** SC-2 (Owner login format, password policy, lockout, refusal
message, sequence, setup code, session lifetime, cookies, HTTPS), SC-4, FR-001,
FR-007–FR-011, FR-014, S-03, S-05–S-10, S-18.

**Access per endpoint (observed in code and enumerated by tests):**

| Endpoint | Access | Evidence |
|---|---|---|
| `GET/POST /setup` | anonymous (SC-4 "First-run setup") | `[AllowAnonymous]`, `[SetupGateExempt]` on `SetupController` |
| `GET/POST /sign-in` | anonymous (SC-4 "Owner sign-in"); gated before setup | `[AllowAnonymous]` on `SignInController`, no gate exemption |
| `POST /sign-out` | policy `Owner` | `[Authorize(Policy = Owner)]`; no GET action |
| `GET /` | policy `Owner` | `[Authorize(Policy = Owner)]` |
| `/error/{statusCode}` | anonymous (SC-4 "Error page") | `[AllowAnonymous]`, exempt from gate |
| catch-all | anonymous `404` | `MapFallback(...).AllowAnonymous()` |
| static files | anonymous, read-only | `UseStaticFiles()` before routing; only `wwwroot/css/site.css` |

- **Fallback policy** = authenticated user in role `Owner`
  (`ControlPlaneSecurityServices`: `FallbackPolicy = GetPolicy("Owner")`) — stricter
  than "authenticated". Verified by `AnonymousEndpointTests.OnlySc4EndpointsAllowAnonymous`,
  which enumerates `EndpointDataSource` and fails on any anonymous endpoint outside
  the SC-4 entries; allowed and forbidden principals by `AuthorizationTests`
  (`200` for the Owner, `403` error page for a principal without the role).
- **Setup gate** (`SetupGateMiddleware`) runs after routing and before
  authentication; it caches only "exists", so it cannot keep the setup page open
  after creation nor close it before; `SetupGateTests` (6).
- **Setup code (S-03, Critical if bypassable):** `SetupCodeState.Matches` returns
  false while no code is set; the code is set by `SetupCodeStartup`, an
  `IHostedService` that completes before the web server starts accepting
  requests; creation voids it (`Void`) and the database singleton prevents a
  second account regardless of the code. No path creates an `owner` row outside
  `FirstRunSetupService.CreateOwnerAsync`. Tests: `SetupSubmissionTests` wrong and
  missing code, `SetupCodeStartupTests.RestartWithoutOwner_NewCodeWorks_PreviousCodeRefused`,
  `…WithPreviousCodeAfterOwnerCreated_DoesNotCreateSecondOwner`.
- **Sign-in sequence (S-06):** `OwnerSignInService.AttemptAsync` — unknown login →
  lockout in force (password not verified) → wrong password (+1; 5th sets
  `lockout_end = now + 15 min` and resets the count, I-4) → success (count 0,
  `lockout_end` null). No `SignInManager` is registered or used. Lockout time comes
  from `TimeProvider`. Tests: `OwnerSignInLockoutTests` (6) incl. no permanent
  lockout. A parallel brute-force cannot skip the counter: `concurrency_stamp` is a
  concurrency token and a conflicting save is retried against fresh state.
- **Indistinguishable refusals (S-05):** `OwnerSignInResult` carries no reason; the
  controller renders one view with `SignIn.Refused` and `401` for all three;
  unknown login also runs a hash verification to equalise timing.
  `SignInTests.UnknownLoginWrongPasswordAndLockout_ProduceIdenticalResponses`.
- **Session (S-18, SC-2 v68):** `IsPersistent = false`; `ExpireTimeSpan = 30 min`
  with sliding expiration; `OwnerSession.ValidatePrincipalAsync` rejects a ticket
  older than 8 h from the `signed_in_at` claim and one whose security stamp no
  longer matches the database; cookie authentication and the check read the
  injected `TimeProvider`. `SessionLifetimeTests` (2),
  `CookieAttributeTests.SessionCookie_HasNoExpiresOrMaxAge`.
- **Sign-out (FR-011):** POST only; rotates `security_stamp`, then signs out;
  `SignOutTests.AfterSignOut_ReplayedOldCookie_IsNotAuthenticated`,
  `GetSignOut_Returns404_SessionStillValid`.
- **No return URL (I-6):** `OnRedirectToLogin` redirects to a fixed `/sign-in`;
  success always redirects to `/` — no open redirect.
  `SignInTests.ReturnUrl_IsIgnored`, `AnonymousEndpointTests.Home_AnonymousWithOwner_RedirectsToSignIn_NoReturnUrl`.
- **HTTPS / HSTS:** no `UseHttpsRedirection`, no `UseHsts`, all cookies `Secure` —
  as SC-2 fixes for the Control Plane. The HTTPS-only listener is deployment
  configuration: F-01.

## 7. Credentials, Key and Google Access

- **Password:** hashed with ASP.NET Core Identity `PasswordHasher<Owner>` (PBKDF2,
  V3 format), stored in `owner.password_hash varchar(256)`; `SuccessRehashNeeded`
  re-hashes. Never on `OwnerSessionDto`, a view model or an audit row. Plaintext
  and confirmation are form-bound, used for validation/hashing and dropped; views
  render `password`, `passwordConfirmation` and `setupCode` with `value=""`.
  `SetupSubmissionTests.ValidSetup_StoresPasswordOnlyAsVerifiableHash`,
  `SetupValidationTests.ValidationFailure_DoesNotEchoPasswordConfirmationOrCode`.
- **Password policy (SC-2):** 15–128 code points (`CodePointLengthAttribute`,
  `EnumerateRunes`), no composition rule, may not equal/contain the login
  case-insensitively (`DoesNotContainLoginAttribute`), ordinal confirmation;
  Identity `PasswordValidator` not used. Boundaries covered by
  `SetupValidationTests` (8 methods, incl. 128/129 emoji).
- **Login format:** `[StringLength(64, MinimumLength = 4)]` + `^[A-Za-z0-9._-]*$`;
  sign-in applies no format rule (VR-005), so its response never depends on login
  shape.
- **Setup code:** `SetupCodeGenerator` — `RandomNumberGenerator.GetString` over the
  32-character Crockford alphabet, 26 characters (130 bits), grouped by hyphens;
  compared by `CryptographicOperations.FixedTimeEquals` after normalization;
  written only by `StandardOutputOperatorConsole` (`Console.Out`), never through
  `ILogger`. Constant-time behaviour is reviewed, not measured (test strategy §5).
- **Service-account key, Google scopes, impersonation:** NOT_APPLICABLE.

## 8. Sensitive Data Exposure

| Surface | Result |
|---|---|
| Views / view models | `SetupPageModel(Login, AlreadyCreated)`, `SignInPageModel(Login, Refused)`, `ErrorPageModel(MessageKey, BackLink)` — no entity, hash, stamp or lockout data; the login refill is Razor-encoded |
| DTOs | `OwnerSessionDto(OwnerId, UiLanguage, SecurityStamp)`; the stamp enters only the Data Protection–encrypted cookie |
| `409` setup conflict | shows neither login (`SetupSubmissionTests.Setup_WhenOwnerExists_ReturnsConflictWithSignInLink`) |
| Exceptions / `500` | `ControlPlaneExceptionHandler` logs and returns false; `/error/500` renders only `Error.Internal`; `ErrorPageTests.UnhandledException_ShowsErrorPageWithoutInternals` breaks the schema under the host and asserts no exception, type, namespace, SQL or path text |
| Logs | see §12 |
| Audit rows | factories take ids, enums, time and request id only — no string that can carry a login, password or code; `SignInAuditTests.AuditRows_ContainNoTypedLoginOrPassword`, `SetupAuditTests.SetupAuditRows_ContainNoCodeLoginOrPassword` |
| Exports / telemetry | none in this Story; `docs/hooks/tool-usage.jsonl` git-ignored |
| Test fixtures | synthetic logins and passwords (`TestData`); no personal data |

## 9. Input Validation

- **Constraints:** `SetupRequest` and `SignInRequest` DataAnnotations plus the three
  custom `ValidationAttribute`s; setup code has no binding rule (OD-004) and is
  treated as wrong when empty.
- **Runtime activation:** these are MVC view controllers (no `[ApiController]`,
  no `/api/v1`); both actions check `ModelState.IsValid` first and return `400`
  with the form — proven by `SetupValidationTests` and
  `SignInTests.EmptyLoginOrPassword_Returns400_NoAuditNoCounterChange`. API-6
  `fieldErrors` does not apply (no REST endpoint, api-design §1, §7).
- **Order (OD-004):** binding failure returns before the service runs, so nothing
  is audited and no code is compared; `SetupAuditTests.WrongCodeWithInvalidFields_IsNotAudited`.
- **Mass assignment:** request types carry only the contract fields; no role, id,
  language or state is bindable.
- **Oversized input:** framework form limits apply; setup fields are length-checked
  before hashing; sign-in has no length rule by requirement (VR-005) — see F-06.
- **Messages:** translation keys only; no submitted value is echoed except the
  login refill.

## 10. API Security

- Exactly the routes of api-design §2; no undocumented endpoint (enumeration test).
  Methods: setup and sign-in GET/POST, sign-out POST, home GET, error page any
  method (read-only; antiforgery still validated on a direct non-GET), catch-all
  any method → `404`.
- **Antiforgery (S-08):** `GlobalAntiforgeryFilter`, an MVC global authorization
  filter, validates every non-safe method before model binding; a refusal is
  `400` + `Error.PageExpired` with a back link limited to `/setup`, `/sign-in` or
  `/`. It skips validation only for an internal status-code or exception
  re-execution of an already-received request, which a client cannot trigger
  directly. The catch-all is not MVC and writes nothing. No exemption metadata
  exists. `AntiforgeryTests` (4) enumerate every endpoint accepting an unsafe
  method and post without a token (anonymous forms anonymously, sign-out as the
  Owner).
- **Antiforgery cookie:** `__Host-cp-antiforgery`, `HttpOnly`, `Secure`,
  `SameSite=Strict`, `Path=/` (`CookieAttributeTests`).
- **GET safety (S-09):** GET handlers change nothing; the setup gate and error page
  read only; `GET /sign-out` → `404`.
- **Error behaviour:** status codes per spec §8; `403` for a denied principal via
  `OnRedirectToAccessDenied` + status-code re-execution, not a redirect.

## 11. Persistence and Configuration

- **Schema** (`20260916104914_InitialOwnerAndAudit`): `owner` without email/phone;
  `uq_owner_normalized_user_name`, `uq_owner_singleton` + `ck_owner_singleton`
  (one account under concurrency, mapped to `409` in `FirstRunSetupService`, loser
  rolled back — `FirstRunSetupConcurrencyTests`, `SetupConcurrencyTests`);
  `ck_owner_ui_language`, `ck_owner_access_failed_count`; `audit_event` with five
  check constraints, no FK, no personal-data column; `trg_audit_event_immutable`.
  Every string has a max length. `MigrationTests.Model_HasNoPendingChangesAgainstMigrations`.
- **Migrations only:** no `EnsureCreated`/`Migrate` at startup; tests apply
  migrations explicitly.
- **Database separation:** own `ControlPlaneDbContext` and connection string
  `ConnectionStrings:ControlPlane`; no installation project referenced.
- **Configuration:** `appsettings.json` holds no secret, no connection string;
  required keys (`ConnectionStrings:ControlPlane`, `DataProtection:KeyDirectory`,
  `LogFile:Directory`) have no default and stop startup when absent — a missing
  key ring path cannot silently fall back to an in-memory or default location.
  No `appsettings.Development.json`, no `launchSettings.json`.
- **EF Core:** no `EnableSensitiveDataLogging`; Npgsql error detail not enabled,
  so constraint violation messages do not carry row values.
- **Generated files:** none in the working tree.

## 12. Logging, Audit and Telemetry

- **Logging (DC-10 v68):** Serilog file sink, compact JSON, daily files, 30
  retained, 50 MB cap; `Microsoft.*` at `Warning` except hosting lifetime at
  `Information` (start/stop); unhandled exceptions at `Error`; console sink only in
  `Development`. No application log statement carries a login, password, code,
  cookie or token (only `ControlPlaneExceptionHandler` logs, with a fixed message).
  `SetupCodeStartupTests.StartupWithoutOwner_LogFileDoesNotContainCode` reads every
  log file after a run with setup, sign-in and refusals and finds neither code
  (with or without hyphens), login nor password. Request id on each line: F-03.
- **Audit (SC-11):** the five Control Plane events of this Story are written with
  actor, action, target, outcome, category, `occurred_at` from `TimeProvider`,
  and `request_id = HttpContext.TraceIdentifier`; success and counted failures are
  saved in the same transaction as the state change; the setup success row is in
  the account transaction and rolls back with it. Sign-out and validation
  failures are not audited, as specified. Immutability: no setter/update method,
  interceptor throws on `Modified`/`Deleted`, database trigger rejects `UPDATE`
  and `DELETE` — `AuditEventSchemaTests` (6), `SignInAuditTests` (5),
  `SetupAuditTests` (6).
- **Telemetry:** `docs/hooks/tool-usage.jsonl` git-ignored; not in the change set.

## 13. Dependencies

| Project | Package | Approved |
|---|---|---|
| ControlPlane | `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 | OD-006 |
| ControlPlane | `EFCore.NamingConventions` 10.0.1 | OD-006 |
| ControlPlane | `Microsoft.EntityFrameworkCore.Design` 10.0.4 (`PrivateAssets=all`) | OD-006 |
| ControlPlane | `Serilog.AspNetCore` 10.0.0, `Serilog.Sinks.File` 7.0.0 | OD-006 |
| Tests | `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` | OD-006 |
| Tool | `dotnet-ef` 10.0.12 (local tool) | OD-006 |

No unapproved package; `Microsoft.AspNetCore.Identity.EntityFrameworkCore` not
referenced; no test package in the production project; no project reference.
Vulnerability scan (nuget.org, transitive included): no known vulnerable package
on 2026-09-16. This is not a guarantee against undisclosed vulnerabilities.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-03 setup needs the current code | `SetupSubmissionTests` (wrong, missing, previous code), `SetupCodeStartupTests` (restart) |
| S-04 code to console only | `SetupCodeStartupTests` (console line; log files clean) |
| S-05/S-06 policy, lockout, sequence, identical refusals | `SetupValidationTests`, `OwnerSignInLockoutTests`, `SignInTests` |
| S-07 anonymous closed list, allowed/forbidden role (TC-5) | `AnonymousEndpointTests`, `AuthorizationTests`, `SetupGateTests` |
| S-08/S-09 antiforgery, GET safety | `AntiforgeryTests`, `SignOutTests` |
| S-10 cookies | `CookieAttributeTests` |
| S-11 key ring persisted | `DataProtectionTests` |
| S-12 audit, immutability | `SignInAuditTests`, `SetupAuditTests`, `AuditEventSchemaTests` |
| S-13 no internals | `ErrorPageTests` |
| S-14 no teaching-data path | `ControlPlaneReferenceTests` |
| AC-004 concurrency | `FirstRunSetupConcurrencyTests`, `SetupConcurrencyTests`, `OwnerSchemaTests` |

Tests assert observable outcomes (status, body, cookies, rows, SQL states), not
mere method calls. No test calls an external service (TC-4). Not covered by tests,
by design: constant-time comparison, HTTPS-only binding, private-network
reachability (test strategy §5).

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Claim the Owner account without the console code | refused, audited, `400` | `SetupCodeState`, wrong/missing code tests | PASS |
| Guess the setup code by brute force | 130-bit space; each attempt audited; no attempt limit by OD-005 | `SetupCodeGenerator`, audit tests | PASS (F-04 Info) |
| Race two setups with different logins | one account, loser `409`, no partial rows | singleton index, concurrency tests | PASS |
| Post a stale setup form after creation | `409`, no second account, no audit | `Setup_WithPreviousCodeAfterOwnerCreated_…` | PASS |
| Enumerate the Owner login via sign-in | identical `401` responses, equalised hashing work | `SignInTests` identical responses | PASS |
| Brute-force the password, incl. in parallel | lockout after 5, not extended during lockout; concurrency token | lockout tests, `concurrency_stamp` | PASS |
| Login CSRF / sign-out CSRF | antiforgery on anonymous forms and sign-out; `SameSite=Strict` | `AntiforgeryTests`, `SignOutTests` | PASS |
| Replay a cookie after sign-out | stamp rotated, validated per request | `AfterSignOut_ReplayedOldCookie_IsNotAuthenticated` | PASS |
| Use a stolen cookie indefinitely | 30-min idle, 8-h absolute | `SessionLifetimeTests` | PASS |
| Open redirect via `ReturnUrl` / back link | fixed targets only | `ReturnUrl_IsIgnored`; `GlobalAntiforgeryFilter.BackLink` | PASS |
| Provoke an error to read internals | `500` page with translated text only | `ErrorPageTests` | PASS |
| Rewrite or delete audit history | interceptor + trigger | `AuditEventSchemaTests` | PASS |
| Reach the Owner UI over plain HTTP after a misconfigured deployment | HTTPS-only listener | deployment only | F-01 |

## 16. Repository Hygiene

- Live credential files of the prototype are present in the root and git-ignored
  (`google_credentials.json`, `dac-classroom-agent-*.json`); not opened.
- `classroom_cache.db`, `*.xlsx` (except versioned templates), `bin/`, `obj/`,
  `logs/`, IDE folders and telemetry are ignored.
- The change set (untracked `src/`, `tests/`, `ClassroomAgent.sln`, `global.json`,
  `.config/dotnet-tools.json`, Story artifacts) contains no secret-like file, no
  database file, no key ring and no log file.
- The Data Protection and log directories are outside the repository by
  configuration.

## 17. Deviations

| Deviation | Security effect |
|---|---|
| Entity model §4 names `UserManager` over `OwnerUserStore`; the implementation uses Identity's `PasswordHasher` and `ILookupNormalizer` directly (implementation report §7.1), because `UserManager` in .NET 10 reads the system clock | neutral: same hashing, normalization, stamp and lockout fields; the SC-2 sequence and lockout are verified by tests; no Identity table added; `SignInManager` absent, which removes the S-06 risk (F-02 Info) |
| Error page moved to `Security` (report §7.2) | none; aligns with `package-map.md` |
| `AuditEventSchemaTests.cs` modified before IMPLEMENTATION with no record (report §1) | reviewed against `ac_test_matrix` rows 101–106: no weakened assertion found (F-06 Info) |

No undocumented security behaviour, no omitted control, no permissive default
found.

## 18. Findings

### F-01 — HTTPS-only listener is not a stated deployment step

- **Severity:** Minor · **Category:** CONFIGURATION · **SC:** SC-2 (HTTPS per host), DC-6
- **Affected:** `docs/architecture/deployment-conventions.md` DC-2 step 1;
  `src/ClassroomAgent.ControlPlane/Program.cs` (no endpoint configuration).
- **Evidence:** the code configures no Kestrel endpoint; with no `urls`/`Kestrel`
  configuration ASP.NET Core binds plain HTTP by default. DC-2 step 1 issues the
  certificate and deploys the service but does not say to configure an HTTPS-only
  endpoint with no HTTP URL. The test strategy §5 places the listener in
  deployment.
- **Expected:** the Control Plane has no HTTP port (SC-2, DC-6, FR-015).
- **Risk:** a deployment that keeps the default binding would carry the Owner
  password in clear inside the private network on the first sign-in POST. Cookies
  are `Secure` and `__Host-`-prefixed, so browsers refuse to store them over HTTP
  and no session or antiforgery token works there — the exposure is limited to
  the submitted form values.
- **Required correction:** none in IMPLEMENTATION (the approved artifacts leave
  the binding to deployment). Recommended: state in DC-2/DC-3 that the Control
  Plane's Kestrel configuration declares only an HTTPS endpoint with its
  certificate, and verify it at the first deployment.
- **Loop-back:** none (documentation follow-up for a human decision).
- **Verification:** deployed Control Plane refuses connections on HTTP.

### F-02 — Entity-model deviation (Identity `UserManager`)

- **Severity:** Informational · **Category:** AUTHENTICATION
- Documented in §17 and implementation report §7.1. Security behaviour verified.
  Suggest correcting entity model §4 in a later revision.

### F-03 — Request id on log lines not observed at runtime

- **Severity:** Informational · **Category:** LOGGING · **SC:** SC-10, DC-10
- `Enrich.FromLogContext()` with `AddSerilog` carries the ASP.NET Core hosting
  scope (`RequestId` = `HttpContext.TraceIdentifier`, the same value written to
  `audit_event.request_id`). Established from the framework wiring, not by
  reading a log line; tests delete their log directories. Check once on the first
  deployment.

### F-04 — Setup-code attempts are unlimited and each writes an audit row

- **Severity:** Informational · **Category:** AUDIT
- Accepted by OD-005 ("no separate attempt limit"); 130 bits make guessing
  infeasible, and the endpoint is reachable only from the private network and
  only before setup. Repeated attempts grow `audit_event`; no requirement limits
  it. No action.

### F-05 — Data Protection key ring protection at rest

- **Severity:** Informational · **Category:** SECRET_MANAGEMENT · **SC:** SC-7
- Keys are persisted to the configured directory without an additional
  key-encryption mechanism (on Linux ASP.NET Core stores them unencrypted). SC-7
  requires the directory to be readable only by the application process and
  excluded from backups — a deployment control (DC-3, DC-13) that this review
  cannot observe.

### F-06 — Undocumented pre-IMPLEMENTATION test modification

- **Severity:** Informational · **Category:** TEST_COVERAGE
- `tests/ClassroomAgent.Tests/ControlPlane/Persistence/AuditEventSchemaTests.cs`
  changed after TEST_WRITING without a record. Its six methods and eight theory
  rows match `ac_test_matrix` v2 rows 101–106 and assert SQL state and constraint
  name, trigger rejection of `UPDATE`/`DELETE`, the interceptor guard and the
  column catalogue. No weakening found; the prior content is not recoverable, so
  the human reviewer should be aware of it.

## 19. Positive Controls

Independently observed: CSPRNG setup code, console-only output, constant-time
compare, voiding on creation; database singleton for the Owner; SC-2 v66 sequence
without `SignInManager`; lockout from injectable clock; identical refusals with
equalised hashing; code-point password length and no composition rules;
deny-by-default fallback (`Owner` role) and SC-4-equal anonymous list, enforced by
an enumeration test; global antiforgery with no exemption, enforced by an
enumeration test; `__Host-` cookies `Secure`/`HttpOnly`/`SameSite=Strict`,
non-persistent; 30-minute idle, 8-hour absolute, security-stamp invalidation;
no return URL; single exception handler with translated `500`; required
configuration without insecure defaults; Data Protection keys in the configured
directory; immutable audit rows at three levels with no personal-data column;
no installation reference, no outbound call; approved dependencies only, no known
vulnerable package; secrets and data files git-ignored.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration test; no running host on a real listener; no browser test of
  cookie handling.
- HTTPS-only binding, private-network reachability and key-directory permissions
  are deployment properties (F-01, F-05).
- Request id on log lines not observed (F-03).
- Constant-time comparison and timing equalisation reviewed in code, not measured.
- Vulnerability data limited to the nuget.org advisory database at review time.

## 22. Verdict Rationale

The implementation report records a green build and tests with concrete output;
this review re-ran the full suite (158/158). Every SC item the Story touches is
`PASS`; there is no Critical or Major finding and no open security decision.
F-01 is a deployment-documentation gap with limited exposure and no code fix
defined by the approved artifacts; the Informational items need no correction.
**Verdict: PASS → `HUMAN_PR_APPROVAL`.**
