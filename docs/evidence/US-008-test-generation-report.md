---
artifact_type: test_generation_report
story: US-008
version: 2
status: DRAFT
created_at: 2026-09-20T00:00:00Z
updated_at: 2026-09-20T07:55:00Z
produced_by: test-writer
inputs:
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
supersedes: null
---

# US-008 Test Generation Report

**Overall result: PASS.** Tests written, compiling, and the red phase is verified on
real PostgreSQL. OD-004 — raised by this stage and the reason v1 of this report read
BLOCKED — was resolved by the Owner on 2026-09-20 (option 1), and the three cases it
had held back are now written (section 8).

Whole suite: **total 1499, passed 1209, failed 290, skipped 0**, 59.9 s. All 290
failures are in the 24 new US-008 classes and every one of them is a
missing-production-behaviour failure (section 4). **No test outside US-008 fails** —
the 1165 US-001 … US-007 cases are green.

**No production source file was created or modified by this stage.** US-005 and
US-007 each needed an Open Decision to authorise a compile-only skeleton in
`src/`; US-008 did not (section 2).

## 1. Test files created

24 test classes, 334 test cases.

| File | Acceptance Criteria |
|---|---|
| `ControlPlane/Controllers/AdminLoginCheckEndpointTests.cs` | AC-005, AC-006, AC-018 |
| `ControlPlane/Security/AdminLoginCheckSecurityTests.cs` | AC-006 |
| `Web/Configuration/InstallationOAuthSettingsTests.cs` | AC-001 (including the three OD-004 cases) |
| `Web/Security/PublicPortAuthorizationTests.cs` | AC-002 |
| `Web/Security/GoogleSignInStartTests.cs` | AC-003 |
| `Web/Security/GoogleSignInCallbackTests.cs` | AC-003 |
| `Web/Security/ReadOnlyConflictTests.cs` | AC-012 |
| `Web/Security/InstallationCookieTests.cs` | AC-013 |
| `Web/Security/InstallationSessionLifetimeTests.cs` | AC-013 |
| `Web/Security/InstallationSignOutTests.cs` | AC-014 |
| `Web/Security/InstallationErrorPageTests.cs` | AC-016 |
| `Web/UseCases/AdminLoginCheckEveryTimeTests.cs` | AC-004 |
| `Web/UseCases/AdminProvisioningTests.cs` | AC-007 |
| `Web/UseCases/RevokedAdminTests.cs` | AC-008 |
| `Web/UseCases/ControlPlaneUnavailableSignInTests.cs` | AC-009 |
| `Web/UseCases/AdminSignInAuditTests.cs` | AC-010, AC-014 |
| `Web/UseCases/SignInInReadOnlyModeTests.cs` | AC-011 |
| `Web/Localization/InstallationUiTranslationTests.cs` | AC-015 |
| `Web/Pages/LandingPageTests.cs` | AC-017 |
| `Web/Logging/SignInLoggingTests.cs` | AC-018 |
| `Infrastructure/Persistence/AppUserSchemaTests.cs` | AC-007 (db-design 3) |
| `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | AC-010 (db-design 4) |
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | db-design 7 |
| `Architecture/SignInWiringTests.cs` | FR-021 (US-007 F-1, F-2) |

Fixtures created under `tests/ClassroomAgent.Tests/TestInfrastructure/`:

| File | Purpose |
|---|---|
| `ScriptedHttpHandler.cs` | records requests, answers from a script; the one seam for both outbound destinations |
| `GoogleSignInStub.cs` | drives the real Google handler offline (section 2.2) |
| `SignInTestData.cs` | paths, cookie names, the 24 translation keys, the five refusal categories, synthetic emails |
| `AdminLoginCheckTestData.cs` | the channel path and request/response JSON |
| `AdminLoginCheckHostExtensions.cs` | `PostAdminLoginCheckAsync` over the Control Plane host |
| `AppUserRow.cs`, `InstallationAuditRow.cs` | the two new tables read with raw SQL |
| `InstallationSignInExtensions.cs` | a whole sign-in over HTTP; `state` extraction |
| `InstallationSchemaQueries.cs` | catalogue queries over the installation host |
| `ReadOnlyRefusalOverHttp.cs` | puts a `ReadOnlyModeException` through the host's own exception handler |

## 2. Test files modified

| File | Change |
|---|---|
| `TestInfrastructure/InstallationConfigurationKeys.cs` | the five new configuration keys of FR-001 and their synthetic test values |
| `TestInfrastructure/InstallationFactory.cs` | an optional scripted Control Plane transport; when given, the real typed client runs |
| `TestInfrastructure/InstallationTestHost.cs` | the four new required settings by default; `KeyDirectory`; a public-port `FormClient`; `SendPublicHttpAsync`; `Text`/`AllTexts`; `AppUsersAsync`, `AuditRowsAsync`, `AuditRowsAsJsonAsync`, `InsertAppUserAsync`, `TableNamesAsync`; `ScriptControlPlaneHttp` via `StartWithControlPlaneHttpAsync`; `UseGoogleStub` |
| `ControlPlane/Security/AnonymousEndpointTests.cs` | the new endpoint added to the SC-4 anonymous whitelist |
| `ControlPlane/Security/AntiforgeryTests.cs` | the new endpoint added to the SC-4 exemption whitelist |
| `Web/Configuration/InstallationConfigurationTests.cs` | the required-settings count 6 → 10, and an assertion that the language stays optional |

Nothing was removed, disabled, weakened or skipped. The three modified existing
test files stay **green**: the two whitelists gain a pattern that no endpoint
matches yet, and the count assertion follows FR-001 making four more settings
required.

### 2.1 No production skeleton was needed

US-005 (OD-002) and US-007 (OD-003) had to authorise a compile-only skeleton in
`src/`, because every scenario named a type that did not exist. US-008 avoids that
entirely: almost every Acceptance Criterion of this Story is observable through a
seam that already exists — an HTTP request on the public port, a row read with raw
SQL, a line in the Serilog file, a configuration key, a routed endpoint in
`EndpointDataSource`. The two places that would otherwise have needed a new type
are handled without one:

- the Control Plane question is asked through the installation's **real** client
  over a scripted transport, not through a substituted port whose new method would
  have to exist at compile time;
- the translations are resolved through `IStringLocalizerFactory.Create(baseName, location)`
  with the base name and assembly as strings, so no marker type is named.

`ClassroomAgent.Application` therefore still references no NuGet package, and
`Domain` still has zero package dependencies: the US-005 architecture test
`FrameworkFreeProjects_ReferenceNoPackage` passes unchanged.

### 2.2 Google and the Control Plane run their own code, offline

TC-4 forbids reaching a live Google API or a deployed Control Plane. It does not
require the framework's handler to be replaced — and replacing it would have
destroyed the evidence AC-003 asks for, because the OAuth `state` parameter and the
correlation cookie are the *handler's* behaviour. A stub validating `state` itself
would only test the stub.

So the real `Microsoft.AspNetCore.Authentication.Google` handler runs, and
`GoogleSignInStub` points its authorization, token and userinfo endpoints at local
addresses and its backchannel at a scripted handler. It reaches the options
instance by resolving the options type **by name**, so the test project keeps no
reference to the package — OD-003 added it to `ClassroomAgent.Web` and to no other
project, and that stays true. The reflection lives in one fixture; not one
assertion uses it. Before the handler exists the fixture fails with a message
naming the missing production behaviour, which is what 93 of the 290 red failures
are.

The Control Plane channel works the same way: `InstallationFactory` leaves the real
`ControlPlaneClient` registered and replaces the primary handler of its typed
`HttpClient`. That is what makes the `ClientClassification` table of api-design 2.4
provable on production code, including the row that matters most — a `404`
*without* the `unknown_installation` body is `Unavailable`, never "not approved".

OD-003's closing note anticipated "a synthetic authentication scheme registered in
the test host". Driving the real handler instead is strictly stronger and satisfies
the same constraint: no test performs a real authorization-code exchange, and no
request leaves the process.

## 3. Commands run

| Command | Result |
|---|---|
| `docker version` | 29.8.0 (Testcontainers needs the daemon — TC-2) |
| `dotnet build ClassroomAgent.sln` | succeeded, **0 errors, 0 warnings** (`TreatWarningsAsErrors` in every project) |
| `ClassroomAgent.Tests.exe -class "*<the 24 classes>"` | total 334, failed 290 |
| `ClassroomAgent.Tests.exe` (whole suite) | **total 1499, failed 290, skipped 0**, 59.9 s |

`dotnet test --nologo` is never used in this project: the flag reaches the test
application and produces a false "Zero tests ran" failure.

## 4. Red-phase classification

| Class | Passed | Failed |
|---|---|---|
| `AdminLoginCheckEndpointTests` | 6 | 33 |
| `AdminLoginCheckSecurityTests` | 1 | 3 |
| `InstallationOAuthSettingsTests` | 14 | 30 |
| `PublicPortAuthorizationTests` | 7 | 4 |
| `GoogleSignInStartTests` | 0 | 12 |
| `GoogleSignInCallbackTests` | 0 | 11 |
| `AdminLoginCheckEveryTimeTests` | 0 | 7 |
| `AdminProvisioningTests` | 0 | 9 |
| `RevokedAdminTests` | 0 | 8 |
| `ControlPlaneUnavailableSignInTests` | 0 | 15 |
| `AdminSignInAuditTests` | 0 | 9 |
| `SignInInReadOnlyModeTests` | 0 | 13 |
| `ReadOnlyConflictTests` | 0 | 9 |
| `InstallationCookieTests` | 1 | 7 |
| `InstallationSessionLifetimeTests` | 0 | 3 |
| `InstallationSignOutTests` | 0 | 5 |
| `InstallationUiTranslationTests` | 0 | 31 |
| `InstallationErrorPageTests` | 6 | 9 |
| `LandingPageTests` | 0 | 11 |
| `SignInLoggingTests` | 0 | 7 |
| `AppUserSchemaTests` | 1 | 21 |
| `InstallationAuditEventSchemaTests` | 1 | 28 |
| `AppUserMigrationTests` | 4 | 2 |
| `SignInWiringTests` | 3 | 3 |
| **Total** | **44** | **290** |

Every failure message names missing production behaviour, in nine shapes (counts from the final run):

| Shape | Count | What it means |
|---|---|---|
| "The Google authentication handler is not part of the host yet" | 93 | FR-006, FR-021: the package is not referenced by `ClassroomAgent.Web` |
| `No service for type IStringLocalizerFactory` | 32 | FR-017: no localization baseline yet |
| `42P01: relation "app_user" / "audit_event" does not exist` | 52 | FR-003, FR-012: the migration does not exist |
| `Assert.ThrowsAny: No exception was thrown` | 27 | FR-001: the settings are not validated yet, so the host starts — the two OD-004 cases included |
| `Assert.Equal: Expected OK, Actual NotFound` | 13 | FR-009: the Control Plane endpoint is not routed yet |
| "The last page carried no antiforgery token field" | 10 | FR-004, FR-005: `/sign-in` does not exist, so no token is rendered |
| "The host registers no IExceptionHandler" | 9 | FR-014: the `409` mapping does not exist |
| `Assert.Contains: Filter not matched in collection` | 4 | FR-002, FR-004: the public-port endpoints are not mapped |
| other assertion failures on absent behaviour | 50 | the rest of the surface |

No failure is a syntax error, an invalid import, a missing test dependency, a
broken test host, an invalid fixture, or a contradiction with an approved artifact.
No test is order-dependent: every one builds its own database, host, clock and
scripted transport.

## 5. One documented deviation from the approved API design

api-design 2.5 and `x-host-wide-rules.api-v1-reserved` say the `409` read-only
mapping is "proven by a test-only probe endpoint registered in the test host".
**That is not achievable from outside the host, and the reason is structural:**
FR-002 puts an anonymous catch-all last in the endpoint pipeline, so it matches
every unmatched path and a test middleware appended after the application's own
pipeline would never run; a middleware inserted *ahead* of it would sit outside the
exception handler under test. ASP.NET Core exposes no supported way for a test to
insert an endpoint in the middle of a host's pipeline.

`ReadOnlyRefusalOverHttp` therefore hands a `ReadOnlyModeException` to the host's
**registered `IExceptionHandler`** directly and asserts the status code, the
content type, the API-6 body shape and the translated message in both languages.
This tests the same production code with nothing simulated, and it needs no
endpoint that ships in no environment. The contract is not changed by this stage;
the deviation is recorded here for `SECURITY_REVIEW`, which may ask for it
differently.

## 6. Tests that pass before implementation, and why

44 cases (28 distinct methods) pass. Each was investigated as the stage rules
require; none is a weak or bypassing test.

| Tests | Why they pass today |
|---|---|
| `ThePrivatePort_IsUnchangedByThisStory`, `ThePrivateEndpoints_AnswerNothingOnThePublicPort`, `AnonymousUnknownAddress_Returns404_NotASignInRedirect` (3) | the private port and the "public port answers 404" behaviour hold now; these are the guarantee that US-008 does not break them while replacing the public-port rule |
| `TheControlPlaneSchema_IsUnchangedByThisStory`, `TheControlPlaneModel_HasNoPendingChanges`, `TheModel_HasNoPendingChangesAgainstTheMigrations`, `HostStart_DoesNotApplyMigrations` (4) | db-design 7.2 says a Control Plane migration here would be a defect; these fail the moment one appears |
| `AllRequiredSettingsValid_HostStarts`, `BoundaryValidPublicBaseAddress_HostStarts`, `ValidDefaultLanguage_HostStarts`, `DefaultLanguageIsOptional_HostStartsWithoutIt`, `OAuthCredentialsAreNotVerifiedAgainstGoogle_HostStarts` (5 methods, 14 cases) | the host ignores the new settings today, so it starts; they become meaningful the moment validation exists and they lock I-5 and VR-004's "optional" |
| `ClientIdAndSecretReference_AreNeverLogged`, `TheSecretItself_IsNotAConfigurationSetting`, `KeyRing_IsNotInTheDatabase` (3) | S-08 and S-18 hold now and must keep holding; they are regression guards against a log line or a column appearing |
| `NoColumn_HoldsAGoogleTokenOrProfileDatum`, `NoColumn_CouldCarryAPersonalDatum` (2) | the assertions are over the columns that exist; today neither table exists, so nothing offends. They are the guard that the migration does not add a token, subject id or free-text column (S-09, SC-11) |
| `Checks_WriteNothing_NoAuditNoLicenceCheckNoInstallationChange`, `UnknownInstallation_AnswerIsIdenticalForAnApprovedAndAnUnknownEmail`, `OtherMethods_AreNotHandled` (3) | S-17 and S-14 are satisfied trivially while the endpoint 404s; they become real evidence the moment it answers, and would catch an endpoint that wrote a row |
| `OwnerPages_StayAuthenticated`, `NoPublicPortEndpoint_IsExemptFromAntiforgery` (2) | the closed lists are correct now; these fail if this Story opens something |
| `TheErrorPage_LeaksNoInternals`, `TheDeveloperExceptionPage_IsNotInPlay`, `StaticFiles_ComeOnlyFromTheApplicationsOwnDirectory` (3) | SC-6, S-19 and SC-4 hold now; they are the guard for the page this Story adds |
| `ResolvingTheUnitOfWork_YieldsTheDecoratedChain`, `TheControlPlaneChannel_IsOnePort`, `AnOrdinarySaveFailure_IsStillAnOutcome` (3) | US-007 already decorates the unit of work, AD-4 already holds, and AD-9 already holds. The three findings that must change — F-1's two bare registrations and F-2's downgrade — are **RED** |

One test in this group was rewritten during the stage because it was weak:
`AReadOnlyRefusal_IsNotDowngradedIntoASaveFailure` originally asserted the US-007
backstop rather than F-2 and so passed for the wrong reason. It now constructs
`CheckLegitimacyUseCase` with a unit of work that refuses, and fails today exactly
as F-2 predicts.

## 7. Untested Acceptance Criteria

None. All 18 have at least one mapped test.

One **clause** of AC-001 is not exercised: the resolution of
`GoogleOAuth:ClientSecretReference` into an actual secret. Everything the
requirements fix about the reference is tested; the resolution is not, because no
artifact fixes the reference syntax or the store mechanism — OD-004.

Two scenarios are excluded by design and recorded in the test strategy, section 6:
browser rendering at phone width (NFR-070), which is a manual check, and real HSTS
enforcement, which is the browser's behaviour and not the server's.

## 8. Open Decisions

| Id | State | Effect |
|---|---|---|
| OD-001, OD-002 | RESOLVED before activation, in `trebovaniya.md` v78 | none; the tests are written against the resolutions |
| OD-003 | RESOLVED 2026-09-19 | none; the package is what section 2.2 drives |
| OD-004 | RAISED by this stage, **RESOLVED 2026-09-20 (option 1)** | none outstanding; three cases added |

**OD-004 — how the OAuth client secret reference is resolved into the secret.**
FR-001 says the secret is read "by the same `Infrastructure/Secrets` mechanism the
service-account key uses", and that mechanism did not exist: the service-account key
arrives with US-009, so nothing had yet needed to read a secret, and
`trebovaniya.md` deliberately leaves the store open ("secrets/Key Vault/переменные
окружения", the reference being "имя секрета или путь"). One configuration string
therefore had to be interpreted, and choosing the interpretation was a decision no
artifact supported.

**Resolved by the Owner on 2026-09-20, option 1:** the reference is the **name of an
environment variable**, set by the Owner on the server at deployment (DC-2) and
resolved once at start-up through `Infrastructure/Secrets` — the namespace
`package-map.md` already designates for reading from the configured secret store. An
absent or empty variable stops the start; a variable holding a *wrong* secret does
not, which is I-5. Neither `trebovaniya.md` nor the Specification needs a change:
§5 already lists environment variables among the stores, and FR-001's wording is
about placement, which option 1 satisfies. Full text in
`docs/decisions/US-008-open-decisions.md` (v4).

**Three cases were added once it was resolved**, in
`Web/Configuration/InstallationOAuthSettingsTests`:
`TheSecret_IsResolvedFromTheNamedEnvironmentVariable` (the resolved secret is what
the handler is configured with),
`AReferenceNamingAnAbsentOrEmptySecret_HostDoesNotStart` (two cases, fail fast), and
`TheSecretValue_IsNeverLogged` (S-08, SC-10). Each uses a variable name unique to
the test and removes it afterwards, so nothing collides with a test running beside
it.

## 9. Overall result

**PASS.** Everything this stage owes is complete and in the repository: the test
strategy, the Acceptance-Criteria matrix, 24 executable test classes with 334 cases
that compile with zero warnings, and a red phase verified against real PostgreSQL
with no regression anywhere in the existing 1165 cases. The one Open Decision this
stage raised is resolved and its coverage written. `IMPLEMENTATION` may proceed.

Correction to v1 of this report: it counted 331 US-008 cases and 45 passing. The
figures are **330 cases, 44 passing** before the OD-004 additions and **334 cases,
44 passing** after them; the existing suite is **1165** cases, not 1164. The
conclusions are unchanged.
