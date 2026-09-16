---
artifact_type: implementation_report
story: US-001
version: 1
status: DRAFT
created_at: 2026-09-16T12:43:07Z
updated_at: 2026-09-16T12:43:07Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/decisions/US-001-open-decisions.md
    version: 4
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
  - path: trebovaniya.md
    version: 68
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-001 Implementation Report — Owner first-run setup

## 1. Summary

The Control Plane host (`ClassroomAgent.ControlPlane`) now provides everything
US-001 specifies: the setup gate, the one-time setup code on the operator
console, the first-run setup page, Owner sign-in with the SC-2 sequence and
lockout, sign-out with server-side session invalidation, the home page, the
error page and `404` catch-all, global antiforgery, cookie attributes, session
lifetime (OD-002), persisted Data Protection keys, Serilog file logging (DC-10,
v68), the `owner` and `audit_event` tables with their migration, and Ukrainian
and English translations.

**Status:** all twelve Acceptance Criteria implemented. Build: 0 warnings, 0
errors. Tests: 158 of 158 pass, 0 skipped. Format check clean.

**How this attempt started.** On entry the working tree already held an
uncommitted, near-complete implementation written after TEST_WRITING recorded
PASS (files dated 2026-09-16 10:48–10:53 UTC) with no workflow record of that
run, and `tests/.../Persistence/AuditEventSchemaTests.cs` had a later
modification (11:22 UTC). This attempt took that code as its starting point,
reviewed every production file against the Specification, the designs and the
architecture rules, made the correction in §7.2, and ran the full validation
below. The test file was compared with `ac_test_matrix` v2 rows 101–106: all six
methods and the eight theory rows are present with the matrix's assertions; no
assertion is weaker than the matrix describes. Its prior content cannot be
diffed (the file was never committed), so the change is disclosed here for
human review.

**Limitations:** see §7 (one deviation from the entity model, HTTPS-only
listener left to deployment).

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-001-owner-first-run-setup.md` | — |
| Specification | `docs/specifications/US-001-spec.md` | 3 (APPROVED) |
| Open Decisions | `docs/decisions/US-001-open-decisions.md` | 4 (all resolved) |
| API design | `docs/designs/api/US-001-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-001-openapi.yaml` | 1 |
| DB design | `docs/designs/database/US-001-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-001-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-001-test-strategy.md` | 2 |
| AC test matrix | `docs/tests/US-001-ac-test-matrix.md` | 2 |
| Requirements | `trebovaniya.md` | 68 |

## 3. Implemented Acceptance Criteria

Paths are relative to `src/ClassroomAgent.ControlPlane/`; tests to
`tests/ClassroomAgent.Tests/ControlPlane/`.

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Security/SetupGateMiddleware.InvokeAsync`, `Security/SetupGateExemptAttribute`, `Controllers/SetupController.Show`, `Views/Setup/Index.cshtml` | `Security.SetupGateTests` (6), `Controllers.SetupPageTests.SetupPage_WithoutOwner_ShowsCodeLoginPasswordAndConfirmation` | GREEN |
| AC-002 | `Services/FirstRunSetupService.CreateOwnerAsync`, `Controllers/SetupController.Submit`, `Security/OwnerSession.SignInAsync`, `Persistence/Owner`, `OwnerConfiguration` | `Controllers.SetupSubmissionTests` (4 of AC-002), `Architecture.ControlPlaneReferenceTests` | GREEN |
| AC-003 | `SetupController.Show` (redirects), `FirstRunSetupService` step 3 → `AlreadyExists`, `SetupCodeState.Void` | `SetupPageTests` (2), `SetupSubmissionTests.Setup_WhenOwnerExists_…`, `…WithPreviousCodeAfterOwnerCreated_…` | GREEN |
| AC-004 | `uq_owner_singleton` (`OwnerConfiguration`, migration); `FirstRunSetupService.IsOwnerUniquenessViolation` → `AlreadyExists` | `Services.FirstRunSetupConcurrencyTests`, `Controllers.SetupConcurrencyTests`, `Persistence.OwnerSchemaTests.SecondOwnerRowWithDifferentLogin_ViolatesSingleton` | GREEN |
| AC-005 | `Services/OwnerSignInService.SignInAsync` / `AttemptAsync`, `Controllers/SignInController`, `Security/OwnerSession.ValidatePrincipalAsync`, `ControlPlaneSecurityServices` (cookie) | `Controllers.SignInTests` (6), `Services.OwnerSignInLockoutTests` (6), `Security.SessionLifetimeTests` (2), `Security.CookieAttributeTests.SessionCookie_HasNoExpiresOrMaxAge` | GREEN |
| AC-006 | `Controllers/SetupRequest`, `CodePointLengthAttribute`, `DoesNotContainLoginAttribute`, `EqualsOrdinalAttribute`, `Views/Shared/_FieldErrors.cshtml` | `Controllers.SetupValidationTests` (8) | GREEN |
| AC-007 | `Services/SetupCodeGenerator`, `SetupCodeComparer`, `SetupCodeState`, `SetupCodeStartup`, `StandardOutputOperatorConsole`; wrong-code branch of `FirstRunSetupService` | `Services.SetupCodeStartupTests` (4), `Services.SetupCodeTests` (2), `SetupSubmissionTests` wrong/missing code (2) | GREEN |
| AC-008 | `Persistence/AuditEvent` factories, `AuditEventConfiguration`, `TimestampInterceptor` guard, migration trigger; audit writes in `OwnerSignInService` | `Controllers.SignInAuditTests` (5), `Persistence.AuditEventSchemaTests` (6) | GREEN |
| AC-009 | `Localization/SharedResource` + `.uk.resx` / `.en.resx` (35 keys each), `Security/OwnerAccountCultureProvider`, request localization in `Program.cs` | `Localization.TranslationCompletenessTests` (2), `Localization.PageLanguageTests` (2) | GREEN |
| AC-010 | fallback policy in `Security/ControlPlaneSecurityServices`, `[AllowAnonymous]` on setup, sign-in and error page only, `MapFallback(...).AllowAnonymous()`, `Security/ErrorController`, `ControlPlaneExceptionHandler` | `Security.AnonymousEndpointTests` (2), `Security.AuthorizationTests` (2), `Security.ErrorPageTests` (3) | GREEN |
| AC-011 | `Security/GlobalAntiforgeryFilter`, antiforgery and session cookie options, `Controllers/SignOutController`, `OwnerSessionService.EndSessionAsync`, Data Protection in `ControlPlaneSecurityServices` | `Security.AntiforgeryTests` (4), `Controllers.SignOutTests` (4), `Security.CookieAttributeTests` (3), `Security.DataProtectionTests` | GREEN |
| AC-012 | `FirstRunSetupService` (success row in the account transaction; refusal row on its own), `AuditEvent.OwnerFirstRunSetup*` | `Controllers.SetupAuditTests` (6) | GREEN |
| DB design | migration `20260916104914_InitialOwnerAndAudit`, both configurations | `Persistence.MigrationTests` (2), `Persistence.OwnerSchemaTests` (3) | GREEN |

## 4. Change Set

All files are new (greenfield; nothing was committed before). Files the
compile-only skeleton created at TEST_WRITING (OD-007) are marked *(skeleton)*;
IMPLEMENTATION completed them.

### 4.1 Scaffolding

| File | Trace |
|---|---|
| `ClassroomAgent.sln` *(skeleton)* | AD-2 solution; OD-007 |
| `global.json` *(skeleton)* | Microsoft.Testing.Platform runner; OD-007 |
| `.config/dotnet-tools.json` | local `dotnet-ef` 10.0.12 for the migration; OD-006 ("installed as a local tool") |
| `src/ClassroomAgent.ControlPlane/ClassroomAgent.ControlPlane.csproj` *(skeleton)* | project; `Nullable`, `TreatWarningsAsErrors`; packages exactly OD-006 (Npgsql EF provider, EFCore.NamingConventions, EF Design, Serilog.AspNetCore, Serilog.Sinks.File) |
| `src/ClassroomAgent.ControlPlane/appsettings.json` *(skeleton)* | template host settings; no secret, no connection string |
| `src/ClassroomAgent.ControlPlane/Program.cs` *(skeleton)* | DI and pipeline wiring: FR-001, FR-014–FR-017, FR-019; DC-10 logging; required configuration keys (test strategy §3) |

### 4.2 Persistence

| File | Trace |
|---|---|
| `Persistence/ControlPlaneDbContext.cs` *(skeleton)* | FR-018; db-design §2; entity model §6 |
| `Persistence/ControlPlaneDbContextOptions.cs` | PC-5 snake_case shared by host and design-time factory |
| `Persistence/DesignTimeControlPlaneDbContextFactory.cs` | PC-2 `dotnet ef migrations add` without the host |
| `Persistence/Owner.cs` | entity model §2.1 |
| `Persistence/AuditEvent.cs` *(skeleton)* | entity model §2.2, factories per db-design §4.1 |
| `Persistence/UiLanguage.cs`, `AuditActorType.cs`, `AuditAction.cs`, `AuditTargetType.cs`, `AuditOutcome.cs`, `AuditRefusalCategory.cs` | entity model §3 |
| `Persistence/TimestampInterceptor.cs` | PC-6; db-design §4.2 level 2 |
| `Persistence/Configurations/OwnerConfiguration.cs` | db-design §3, §3.1 |
| `Persistence/Configurations/AuditEventConfiguration.cs` | db-design §4 |
| `Persistence/Migrations/20260916104914_InitialOwnerAndAudit.cs`, `.Designer.cs`, `ControlPlaneDbContextModelSnapshot.cs` | db-design §6; PC-2; trigger per §4.2 level 3 |

### 4.3 Services

| File | Trace |
|---|---|
| `Services/FirstRunSetupService.cs` *(skeleton)* | FR-004 steps 3–5, FR-005, FR-006; db-design §4.3 |
| `Services/FirstRunSetupResult.cs`, `FirstRunSetupOutcome.cs` *(skeleton)* | entity model §5 |
| `Services/OwnerSignInService.cs` *(skeleton)* | FR-008, FR-009; S-05, S-06; db-design §4.3 |
| `Services/OwnerSignInResult.cs`, `OwnerSignInOutcome.cs`, `OwnerSessionDto.cs` *(skeleton)* | entity model §5 |
| `Services/OwnerSessionService.cs` | FR-001 (Owner exists), FR-011 (security stamp rotation), api-design §4 POST /sign-out |
| `Services/OwnerExistenceCache.cs` | api-design §3 step 3 ("must not cache 'no Owner'") |
| `Services/OwnerStamps.cs` | db-design §3 `security_stamp`, `concurrency_stamp` |
| `Services/ISetupCodeGenerator.cs`, `SetupCodeGenerator.cs` *(skeleton)* | FR-002, OD-005; test strategy §3 seam |
| `Services/SetupCodeComparer.cs` *(skeleton)* | OD-005 normalization, constant-time comparison |
| `Services/SetupCodeState.cs` | FR-002 process-memory code, voided at creation |
| `Services/SetupCodeStartup.cs` | FR-002 startup generation iff no Owner |
| `Services/IOperatorConsole.cs` *(skeleton)*, `StandardOutputOperatorConsole.cs` | FR-002, S-04; test strategy §3 seam |

### 4.4 Security

| File | Trace |
|---|---|
| `Security/ControlPlaneSecurityServices.cs` | FR-010, FR-014, FR-015, FR-017; SC-2, SC-4, SC-7; api-design §3–§6 |
| `Security/OwnerSession.cs` | FR-010, FR-011, OD-002 (30 min sliding, 8 h absolute, security stamp) |
| `Security/OwnerClaimTypes.cs` | api-design §6 principal |
| `Security/OwnerAccountCultureProvider.cs` | FR-019 (account language, Ukrainian for anonymous) |
| `Security/SetupGateMiddleware.cs`, `SetupGateExemptAttribute.cs` | FR-001 |
| `Security/GlobalAntiforgeryFilter.cs` | FR-015, S-08; API-7 |
| `Security/ControlPlaneExceptionHandler.cs` | FR-016, AD-9, API-10, S-13 |
| `Security/ErrorController.cs`, `ErrorPageModel.cs` | FR-016; package-map `Security` (error page) — moved here in this attempt, §7.2 |

### 4.5 Controllers, views, localization, static files

| File | Trace |
|---|---|
| `Controllers/SetupController.cs`, `SetupRequest.cs`, `SetupPageModel.cs` | FR-003, FR-004; VR-001–VR-004, VR-007; api-design §4–§5 |
| `Controllers/CodePointLengthAttribute.cs` | VR-002, spec I-5; api-design §5 |
| `Controllers/DoesNotContainLoginAttribute.cs` | VR-003 |
| `Controllers/EqualsOrdinalAttribute.cs` | VR-007 |
| `Controllers/SignInController.cs`, `SignInRequest.cs`, `SignInPageModel.cs` | FR-007, FR-008, VR-005, spec I-2, I-3, I-6 |
| `Controllers/SignOutController.cs` | FR-011 |
| `Controllers/HomeController.cs` | FR-013 |
| `Views/_ViewImports.cshtml`, `_ViewStart.cshtml`, `Shared/_Layout.cshtml`, `Shared/_FieldErrors.cshtml` | Razor infrastructure; FR-019 localizer; VR-006 field messages |
| `Views/Setup/Index.cshtml`, `SignIn/Index.cshtml`, `Home/Index.cshtml`, `Error/Index.cshtml` | FR-003, FR-007, FR-013, FR-016; field names per api-design §4 |
| `Localization/SharedResource.cs` *(skeleton)*, `SharedResource.uk.resx`, `SharedResource.en.resx` | FR-019, NFR-073; api-design §7 keys |
| `wwwroot/css/site.css` | api-design §2 static files; test strategy §3 (at least one static file) |

### 4.6 Tests

The test project was written at TEST_WRITING and is not changed by this
attempt. `tests/ClassroomAgent.Tests/ControlPlane/Persistence/AuditEventSchemaTests.cs`
carries an undocumented modification made before this attempt — see §1.

No other file is changed. No secret, generated database file, log file, key
ring file or IDE-local configuration is in the working tree.

## 5. Validation Evidence

Run 2026-09-16, .NET SDK 10.0.401, Docker engine 29.8.0.

| Check | Command | Exit | Result |
|---|---|---|---|
| Baseline tests (on entry) | `dotnet test --solution ClassroomAgent.sln --no-build` | 0 | 158 total, 158 passed, 0 failed, 0 skipped |
| Build (after §7.2) | `dotnet build ClassroomAgent.sln --no-incremental` | 0 | 0 warnings, 0 errors |
| Tests (after §7.2) | `dotnet test --solution ClassroomAgent.sln --no-build` | 0 | 158 total, 158 passed, 0 failed, 0 skipped, 24.6 s |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |
| Markers | search for `TODO`, `TBD`, `FIXME`, `???` in `src/`, `tests/` | — | none |

Test levels covered: unit (setup code, reference guard), HTTP integration,
security enumeration (anonymous endpoints, antiforgery), service integration
(concurrency, lockout) and persistence (schema, checks, trigger, migration
drift), all against PostgreSQL 17 via Testcontainers. No test calls an external
service.

The red → green transition was recorded at TEST_WRITING (157 failing, 1 guard
passing); the red phase was not re-run in this attempt because the production
code already existed on entry.

## 6. Configuration Changes

| Key | Where | Required by |
|---|---|---|
| `ConnectionStrings:ControlPlane` | environment / deployment configuration; the host refuses to start without it | FR-018, PC-1, DC-3; test strategy §3 |
| `DataProtection:KeyDirectory` | same; refuses to start without it | FR-017, SC-7 |
| `LogFile:Directory` | same; refuses to start without it; files `control-plane-YYYYMMDD.log`, compact JSON, daily, 30 files, 50 MB cap | DC-10 (v68) |

No value is committed. `appsettings.json` holds only the template logging and
`AllowedHosts` entries. Migrations are not applied at startup (PC-2); the
deployment step runs them.

## 7. Deviations and Discovered Problems

### 7.1 Identity `UserManager` and `OwnerUserStore` not used (entity model §4)

The entity model says the sign-in sequence uses `UserManager` over a custom
`OwnerUserStore`. `UserManager<T>` in the .NET 10 shared framework
(`Microsoft.Extensions.Identity.Core` 10.0.12) reads `DateTimeOffset.UtcNow` for
`IsLockedOutAsync` and `AccessFailedAsync` and has no `TimeProvider` (checked in
the assembly). The Specification §9 requires lockout timing to be testable with
an injectable clock, and the test strategy §3 makes `TimeProvider` "the only
clock for lockout". The Specification outranks the design, so the sequence is
implemented directly with the Identity components the design names:
`IPasswordHasher<Owner>` (`PasswordHasher<Owner>`) for hashing and verification,
`ILookupNormalizer` (`UpperInvariantLookupNormalizer`) for the case-insensitive
login, the security stamp and the `access_failed_count` / `lockout_end` fields.

Behaviour is unchanged against the design: the SC-2 v66 order (unknown login →
lockout in force, password not checked → wrong password → success), 5 failures
→ 15 minutes, the counter reset to 0 when a lockout starts (I-4), reset on
success, never `PasswordSignInAsync` / `CheckPasswordSignInAsync` (S-06). No
Identity table is involved. No `IdentityOptions` are configured because nothing
reads them.

**Recommendation:** at HUMAN_PR_APPROVAL, accept the deviation; the entity model
§4 can be corrected in a later revision. It is not a blocker.

### 7.2 Error page moved to the `Security` namespace

`package-map.md` places the error page and the catch-all in `Security`.
`ErrorController` and `ErrorPageModel` were in `Controllers`; they are moved to
`Security` (`_ViewImports.cshtml` and `GlobalAntiforgeryFilter` updated). No
test references the type names; all tests pass after the move.

### 7.3 HTTPS-only listener

FR-015 / S-10 require HTTPS only and no HTTP port. The code sets no HTTP
redirection and no HSTS, and all cookies are `Secure`; the listener endpoints and
certificate are Kestrel deployment configuration (DC-6), as the test strategy §5
records. Without that configuration the default ASP.NET Core binding would be
plain HTTP. For DEPLOYMENT / SECURITY_REVIEW: the deployment must configure an
HTTPS-only Kestrel endpoint.

### 7.4 Additional hardening within the design

- Unknown-login sign-in runs a password verification against a fixed dummy hash,
  so response time does not reveal whether the login exists (supports FR-008
  indistinguishability).
- A `DbUpdateConcurrencyException` on the Owner row (parallel sign-in attempts)
  is retried up to 3 times with a fresh read, so a concurrent failure is still
  counted and audited once.
- `SuccessRehashNeeded` re-hashes the password on successful sign-in.

## 8. Open Decisions

None touched and none newly required. OD-001 … OD-007 are resolved and applied.
The deviation in §7.1 follows the higher-authority Specification and changes no
behaviour, schema, API contract or security rule, so it is disclosed for human
review rather than raised as an Open Decision.
