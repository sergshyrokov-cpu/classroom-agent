---
artifact_type: test_generation_report
story: US-006
version: 2
status: DRAFT
created_at: 2026-09-19T09:40:00Z
updated_at: 2026-09-19T08:55:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-006-test-strategy.md
    version: 1
  - path: docs/tests/US-006-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/designs/database/US-006-db-design.md
    version: 1
  - path: docs/designs/database/US-006-entity-model.md
    version: 1
supersedes: docs/evidence/US-006-test-generation-report.md@v1
---

# US-006 Test Generation Report

## 1. Result

**Tests written, compiling, and the red phase is verified.**

- `dotnet build ClassroomAgent.sln` -> **0 errors, 0 warnings**
  (`TreatWarningsAsErrors`, nullable enabled).
- Test discovery enumerates **527 test methods / 1085 cases**, of which
  **104 methods / 194 cases** are new in US-006.
- `dotnet test ClassroomAgent.sln` -> **total 1085, failed 176, passed 909,
  skipped 0** (8 m 07 s). The run was repeated through the xUnit v3 runner with a
  TRX report and reproduced case for case.
- **Every one of the 176 failures traces to US-006.** No test of US-001 ... US-005
  regressed: of the 891 pre-existing cases, 880 pass and the 11 that fail are the
  cases this Story deliberately changed in four existing classes (section 3).
- Every failure message is a missing-production-behaviour message (section 4).
  None is a compilation, import, fixture, test-host or Docker failure.

The environment block recorded in v1 is resolved: Docker Desktop runs
(server 29.8.0, `npipe://./pipe/docker_engine`), Testcontainers connects and the
PostgreSQL container passes its readiness checks, so nothing was weakened to get
a run (TC-2 respected - no InMemory, no SQLite).

**Verdict: PASS.**

## 2. Test files created

| File | Cases | Covers |
|---|---|---|
| `tests/…/ControlPlane/Controllers/InstallationPushAddressTests.cs` | 19 | AC-002 |
| `tests/…/ControlPlane/Controllers/InstallationPushAddressValidationTests.cs` | 43 | AC-002 (VR-001), S-14, SC-10 |
| `tests/…/ControlPlane/Controllers/InstallationPushAddressAuditTests.cs` | 8 | AC-003 |
| `tests/…/ControlPlane/Controllers/InstallationPushAddressDetailTests.cs` | 7 | AC-004 |
| `tests/…/ControlPlane/Persistence/InstallationPushAddressSchemaTests.cs` | 16 | db-design §3, §7 |
| `tests/…/ControlPlane/Localization/PushAddressTranslationTests.cs` | 15 | AC-015 |
| `tests/…/ControlPlane/Security/PushAddressSecurityTests.cs` | 7 | AC-013 (Control Plane side), S-11 |
| `tests/…/ControlPlane/Push/StatusPushDeliveryTests.cs` | 20 | AC-005 … AC-008 |
| `tests/…/ControlPlane/Push/StatusPushLoggingTests.cs` | 6 | AC-005, AC-006 (FR-011) |
| `tests/…/Web/Security/StatusPushEndpointTests.cs` | 19 | AC-009, AC-010, AC-012, AC-013, AC-014 |
| `tests/…/Web/BackgroundServices/PushCheckCoordinationTests.cs` | 9 | AC-009, AC-011 |
| `tests/…/Web/Logging/StatusPushLoggingTests.cs` | 6 | AC-009 … AC-012 (FR-011) |
| `tests/…/Web/Configuration/PrivateAddressConfigurationTests.cs` | 19 | AC-001 |
| **Total** | **194 cases / 104 methods** | |

Test infrastructure created:

- `TestInfrastructure/PushTestData.cs` — paths, field and property names,
  canonical addresses, translation keys, `Hosting:PrivateAddress`.
- `TestInfrastructure/PushClientStub.cs` — the network behind the Control Plane's
  named HTTP client `status-push`: scripted answers (status, connection failure,
  never answering) and recorded attempts with their clock instants.
- `TestInfrastructure/PushHostExtensions.cs` — register with an address, submit the
  push address form, read and set the column.

## 3. Test files modified

| File | Change | Why |
|---|---|---|
| `TestInfrastructure/ControlPlaneFactory.cs` | optional `configureServices` | substitute the push HTTP handler (TC-4) |
| `TestInfrastructure/ControlPlaneTestHost.cs` | optional `ManualTimeProvider`; `ReadLogEventsAsync` | retry schedule on a controlled clock; log-event assertions |
| `TestInfrastructure/InstallationTestHost.cs` | request bodies on `SendPrivateAsync` / `SendPublicAsync`; `Hosting:PrivateAddress` in the default settings | the receiver takes a JSON body |
| `TestInfrastructure/InstallationConfigurationKeys.cs` | `PrivateAddressValue` | the new required setting |
| `TestInfrastructure/Html.cs` | `ElementText` | read the detail page's push address row |
| `Web/Configuration/InstallationConfigurationTests.cs` | required settings 5 → 6 | AC-001 |
| `Web/Security/InstallationEndpointTests.cs` | the receiver joins the routed-endpoint enumeration; a new case asserts it is the only unsafe endpoint, POST only, anonymous, antiforgery-exempt; the public port still answers `404` on its path | AC-013, S-01, S-02 |
| `ControlPlane/Security/InstallationAuthorizationTests.cs` | `/installations/{id}/push-address` joins the operation, page and pattern lists | AC-013, S-11 |
| `ControlPlane/Persistence/MigrationTests.cs` | five Control Plane migrations, the fifth `AddInstallationPushAddress` | PC-2, db-design §5 |

No production source file was touched: `src/` is unchanged by this stage.

## 4. Execution evidence

Environment: Docker Desktop, server **29.8.0**, endpoint
`npipe://./pipe/docker_engine`, WSL2 kernel 6.18.33.2, Docker API 1.56.
Testcontainers started the PostgreSQL container and `pg_isready` passed after
about 2 s. No test called a live Google API or a live Control Plane (TC-4).

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | success - 0 errors, 0 warnings |
| `dotnet test ClassroomAgent.sln` | **total 1085, failed 176, passed 909, skipped 0**, 8 m 07 s |
| `ClassroomAgent.Tests.exe -result-trx results.trx` | same run through the xUnit v3 runner: **Total 1085, Errors 0, Failed 176, Skipped 0**, 8 m 06 s - reproduced case for case |
| `ClassroomAgent.Tests.exe -list tests` | 527 test methods discovered |

### 4.1 Where the 1085 cases land

| Group | Cases | Passed | Failed |
|---|---:|---:|---:|
| Pre-existing (US-001 ... US-005), untouched | 880 | 880 | 0 |
| Pre-existing cases this Story deliberately changed | 11 | 0 | 11 |
| New US-006 cases | 194 | 29 | 165 |
| **Total** | **1085** | **909** | **176** |

### 4.2 Failures per test class

| Test class | Failed | New in US-006 |
|---|---:|:--:|
| `ControlPlane.Controllers.InstallationPushAddressValidationTests` | 42 | yes |
| `ControlPlane.Controllers.InstallationPushAddressTests` | 16 | yes |
| `ControlPlane.Push.StatusPushDeliveryTests` | 15 | yes |
| `ControlPlane.Persistence.InstallationPushAddressSchemaTests` | 15 | yes |
| `ControlPlane.Localization.PushAddressTranslationTests` | 15 | yes |
| `Web.Security.StatusPushEndpointTests` | 13 | yes |
| `Web.Configuration.PrivateAddressConfigurationTests` | 12 | yes |
| `Web.BackgroundServices.PushCheckCoordinationTests` | 9 | yes |
| `ControlPlane.Security.InstallationAuthorizationTests` | 8 | changed |
| `ControlPlane.Security.PushAddressSecurityTests` | 7 | yes |
| `Web.Logging.StatusPushLoggingTests` | 6 | yes |
| `ControlPlane.Push.StatusPushLoggingTests` | 6 | yes |
| `ControlPlane.Controllers.InstallationPushAddressDetailTests` | 5 | yes |
| `ControlPlane.Controllers.InstallationPushAddressAuditTests` | 4 | yes |
| `Web.Security.InstallationEndpointTests` | 2 | changed |
| `ControlPlane.Persistence.MigrationTests` | 1 | changed |
| **Total** | **176** | |

The four `changed` classes fail on exactly the cases section 3 says this Story
alters: the fifth Control Plane migration `AddInstallationPushAddress`, the push
receiver joining the routed-endpoint enumeration, and
`/installations/{id}/push-address` joining the Owner-only route lists.
`Web.Configuration.InstallationConfigurationTests` was also changed, and it
passes: its assertion counts the test host's own default settings, and the
production requirement for the sixth setting is carried - red - by
`PrivateAddressConfigurationTests`.

### 4.3 Why the failures fail

Every failure message names missing production behaviour:

| Failure shape | Cases | What is missing |
|---|---:|---|
| `Assert.Equal() Failure: Values differ` / `Strings differ` / `Collections differ` | 88 | the push-address page, the receiver and the detail row do not exist, so the status code, body or row differs |
| `Timed out waiting for N push attempt(s); 0 made` | 19 | the Control Plane sends no push |
| `column "push_address" does not exist` (`Npgsql.PostgresException`) | 16 | the migration `AddInstallationPushAddress` is not written |
| `Translation key 'Installation.PushAddress.*' is missing` | 15 | the `Installation.PushAddress.*` keys are in neither catalogue |
| `Assert.ThrowsAny() Failure: No exception was thrown` | 12 | `Hosting:PrivateAddress` is neither required nor validated at startup |
| `Timed out waiting for N Control Plane call(s)` | 11 | `PushCheckCoordinator` does not start a check |
| `Assert.Single()` / `Assert.NotNull()` failures | 11 | no audit row, no log event, no pending-check flag |
| `Docker`, `TypeLoad`, `MissingMethod`, fixture or import failures | **0** | - |

No failure is caused by a syntax error, an invalid import, a missing test
dependency, an invalid fixture or an incorrect test-host configuration, so the
Red-Phase Verification criteria of this Skill are met.

## 5. New cases that pass before the implementation

v1 of this report claimed that every new US-006 case must fail. **That claim was
wrong and is corrected here.** 29 of the 194 new cases pass before any production
code exists, and all 29 assert that *nothing happens* - an absent route, an
absent push, an absent audit row, an absent index, a method that must not reach
the receiver. A negative assertion of this kind is green before the
implementation by construction and must stay green after it; it is not evidence
that the test is weak, provided the matching positive case is red now. Every one
of them has such a sibling:

| Class | Cases passing | Positive sibling that is red |
|---|---:|---|
| `Web.Configuration.PrivateAddressConfigurationTests` | 7 (`ValidAddress_HostStarts`, all 7 addresses) | the 12 red cases: the setting is required and every invalid value refuses startup |
| `Web.Security.StatusPushEndpointTests` | 6 (public port 404, forged host, GET/PUT/DELETE, another installation's id) | 13 red cases: the receiver itself answers 202/400/404/413 |
| `ControlPlane.Push.StatusPushDeliveryTests` | 5 (no address, non-status change, `AllowedAdmin` changes, no-op action, the Owner is not kept waiting) | 15 red cases: a real status change delivers the push with its retry schedule |
| `ControlPlane.Controllers.InstallationPushAddressAuditTests` | 4 (refused, unchanged, registration-only, rows carry no address) | 4 red cases: setting, changing and clearing each write one row, transactionally, and the row is not updatable |
| `ControlPlane.Controllers.InstallationPushAddressTests` | 3 (unknown installation -> 404 on three routes) | 16 red cases: the page and the change itself |
| `ControlPlane.Controllers.InstallationPushAddressDetailTests` | 2 (earlier Stories' content intact, nothing about push results) | 5 red cases: the push-address row and the missing-address warning |
| `ControlPlane.Persistence.InstallationPushAddressSchemaTests` | 1 (`PushAddress_HasNoUniqueIndex`) | 15 red cases: the column, its type, its nullability and its check constraint |
| `ControlPlane.Controllers.InstallationPushAddressValidationTests` | 1 (`RefusedSubmission_IsNotWrittenToTheLog`, SC-10) | 42 red cases: every rejected address shape |

Two of these deserve a note for IMPLEMENTATION, recorded as non-blocking
findings and not as defects:

- `PushAddress_HasNoUniqueIndex` asserts that no index on `installation`
  mentions `push_address`. It would also be green if the column were never
  added; the column's existence is proved by the other 15 cases of the same
  class, which are red.
- `ValidAddress_HostStarts` starts the host with each valid
  `Hosting:PrivateAddress` value. Today an unknown configuration key is simply
  ignored, so the case is green for a weaker reason than it will be after the
  implementation. That Kestrel really binds to the configured address is the
  scenario section 6 records as untestable under `TestServer`.

The two GUARD rows of the matrix behave as required:
`ControlPlane.Security.AnonymousEndpointTests`, `AntiforgeryTests` and
`ControlPlane.Localization.TranslationCompletenessTests` are all green, so the
new routes and keys have not widened the SC-4 anonymous list and have not broken
NFR-073 catalogue completeness.

No new case passes because the functionality already exists, and none bypasses
the layer it is meant to exercise.

## 6. Requirements that could not be tested

| Requirement | Reason | Compensation |
|---|---|---|
| Kestrel binds the private endpoint to the configured address only (AC-001, third bullet) | `WebApplicationFactory` runs on `TestServer`; there is no listening socket | every invalid value is asserted to refuse startup; the binding itself is the DC-2 deployment check (spec §10) |
| The real `StatusPushClient` against a live local HTTP server (spec §9) | would add a listening socket for a classification table the sender already exercises through `PushClientStub` | non-blocking finding: if the implementor puts the attempt classification inside the client rather than the sender, a client-level test must be added during IMPLEMENTATION |

## 7. Untested Acceptance Criteria

None. AC-001 … AC-015 each have at least one mapped case in
`docs/tests/US-006-ac-test-matrix.md`.

## 8. Open Decisions

None open. OD-001 is RESOLVED (option 2 — one pending check, `trebovaniya.md`
v77); its behaviour is covered by `PushCheckCoordinationTests` and
`Web.Logging.StatusPushLoggingTests.DeferredPush_IsNotLogged_…`.

## 9. Contracts the implementation must honour

The tests observe behaviour through fixed names; an implementation that renames
one of them fails for the wrong reason. They are listed in the test strategy §3:
the named HTTP client `status-push`, the injected `TimeProvider` for both the
retry pauses and the 10-second attempt timeout, the paths, the form field and
JSON property, the audit action code, the `Installation.PushAddress.*`
translation keys, and the log event names and properties of api-design §9.
