---
artifact_type: test_generation_report
story: US-001
version: 2
status: DRAFT
created_at: 2026-09-16T08:35:56Z
updated_at: 2026-09-16T10:37:11Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-001-test-strategy.md
    version: 2
  - path: docs/tests/US-001-ac-test-matrix.md
    version: 2
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
  - path: docs/designs/database/US-001-db-design.md
    version: 1
  - path: docs/decisions/US-001-open-decisions.md
    version: 4
supersedes: null
---

# US-001 Test Generation Report

## Overall result: PASS — red phase verified

95 test methods (158 test cases) are written for AC-001 … AC-012 and the database
design. The solution builds with no error and no warning. 157 cases fail, each for
missing production behaviour; 1 passes as an intended guard. No test fails because
of the test itself or the environment.

## History

- **Attempt 1 (2026-09-16T08:35Z) — BLOCKED.** No .NET 10 SDK and no Docker daemon;
  test design only, no source.
- **Attempt 2 (this revision).** Environment fixed by the human: .NET SDK 10.0.401,
  Docker Desktop engine 29.8.0 (WSL updated). The skeleton question left open in
  attempt 1 was raised as **OD-007** and resolved by the human (option 1: a
  compile-only skeleton created at TEST_WRITING).

## Environment evidence (2026-09-16, attempt 2)

| Check | Command | Result |
|---|---|---|
| .NET SDKs | `dotnet --list-sdks` | `9.0.200`, `9.0.314`, `10.0.401` |
| Docker | `docker version --format '{{.Server.Version}}'` | `29.8.0`; `docker run --rm hello-world` succeeded |
| PostgreSQL | Testcontainers `postgres:17-alpine` | started and removed by the run |

## Commands

| Step | Command |
|---|---|
| Solution | `dotnet new sln -n ClassroomAgent --format sln`; `dotnet sln add` for both projects |
| Control Plane project | `dotnet new web` (then trimmed: no `launchSettings.json`, no `appsettings.Development.json`, no `MapGet`) |
| Packages (OD-006 only) | `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3); test project: `Microsoft.NET.Test.Sdk` 18.10.1, `xunit.v3` 4.0.1, `xunit.runner.visualstudio` 4.0.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, `Testcontainers.PostgreSql` 4.15.0 |
| Build | `dotnet build ClassroomAgent.sln` → 0 errors, 0 warnings (`TreatWarningsAsErrors` in both projects) |
| Tests | `dotnet test --solution ClassroomAgent.sln` |
| One class | `dotnet test --no-build --filter "FullyQualifiedName~ControlPlaneReferenceTests"` → 1 passed (the `AGENTS.md` form works under Microsoft.Testing.Platform) |

## Files created

Compile-only skeleton and scaffolding (OD-007) — no logic, members throw
`NotImplementedException`, nothing registered in DI:

- `ClassroomAgent.sln`
- `global.json` — `test.runner = Microsoft.Testing.Platform` (xunit.v3 on the .NET 10
  SDK refuses the VSTest target: "Testing with VSTest target is no longer supported
  by Microsoft.Testing.Platform on .NET 10 SDK and later")
- `src/ClassroomAgent.ControlPlane/ClassroomAgent.ControlPlane.csproj`
- `src/ClassroomAgent.ControlPlane/Program.cs`, `appsettings.json` (template logging defaults)
- `src/ClassroomAgent.ControlPlane/Persistence/ControlPlaneDbContext.cs`, `AuditEvent.cs`
- `src/ClassroomAgent.ControlPlane/Services/ISetupCodeGenerator.cs`, `IOperatorConsole.cs`,
  `SetupCodeGenerator.cs`, `SetupCodeComparer.cs`, `FirstRunSetupService.cs`,
  `FirstRunSetupResult.cs`, `FirstRunSetupOutcome.cs`, `OwnerSignInService.cs`,
  `OwnerSignInResult.cs`, `OwnerSignInOutcome.cs`, `OwnerSessionDto.cs`
- `src/ClassroomAgent.ControlPlane/Localization/SharedResource.cs`

Test project:

- `tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj`, `GlobalUsings.cs`,
  `Properties/AssemblyFixtures.cs`
- `TestInfrastructure/`: `PostgreSqlFixture`, `ControlPlaneFactory`,
  `ControlPlaneTestHost`, `TestTimeProvider`, `FixedSetupCodeGenerator`,
  `CapturingOperatorConsole`, `ConfigurationKeys`, `TestData`, `FormClient`,
  `PageResponse`, `Html`, `SetCookieHeader`, `HostEndpoint`, `StaticFiles`,
  `AuditRow`, `OwnerRow`
- `ControlPlane/Security/`: `SetupGateTests`, `AnonymousEndpointTests`,
  `AuthorizationTests`, `ErrorPageTests`, `AntiforgeryTests`, `CookieAttributeTests`,
  `DataProtectionTests`, `SessionLifetimeTests`
- `ControlPlane/Controllers/`: `SetupPageTests`, `SetupSubmissionTests`,
  `SetupConcurrencyTests`, `SetupValidationTests`, `SetupAuditTests`, `SignInTests`,
  `SignInAuditTests`, `SignOutTests`
- `ControlPlane/Services/`: `FirstRunSetupConcurrencyTests`, `OwnerSignInLockoutTests`,
  `SetupCodeStartupTests`, `SetupCodeTests`
- `ControlPlane/Persistence/`: `OwnerSchemaTests`, `AuditEventSchemaTests`,
  `MigrationTests`, `SchemaQueries`
- `ControlPlane/Localization/`: `TranslationCompletenessTests`, `PageLanguageTests`
- `ControlPlane/Architecture/`: `ControlPlaneReferenceTests`

## Files modified

- `docs/tests/US-001-test-strategy.md` → v2, `docs/tests/US-001-ac-test-matrix.md` → v2,
  this report → v2.
- `docs/decisions/US-001-open-decisions.md` → v4: OD-007 added with the human's
  resolution. The Specification, API design and DB design record v2 of that file;
  OD-007 changes none of their content, so they are not stale in substance.

No production behaviour, User Story, Acceptance Criterion, Specification, API or
database design was changed.

## Test execution (final run, 2026-09-16)

`dotnet test --solution ClassroomAgent.sln --no-build` — total **158**, failed
**157**, passed **1**, skipped **0**, duration 10 s.

### Passing existing tests

None existed before (greenfield).

### Passing new test (guard)

- `Architecture.ControlPlaneReferenceTests.ControlPlane_DoesNotReferenceDomainOrApplication`
  — passes because the skeleton references no installation project. It is a guard
  that must stay green; the assertion is not weak (assembly and `.csproj` both
  checked).

### Expected failing new tests (157) — classification

| Failure | Cases | Missing production behaviour |
|---|---|---|
| `InvalidOperationException: The last page carried no antiforgery token field.` | 91 | no `/setup` or `/sign-in` page renders a form (every flow starts with GET, then POST with the token) |
| `No service for type IStringLocalizer<SharedResource>` | 21 | no translations registered |
| `NotImplementedException` | 12 | `SetupCodeComparer.Matches`, `SetupCodeGenerator.Generate` |
| status differs (`Expected: Found / OK / BadRequest`, `Actual: NotFound`) | 5 | no setup gate, pages or antiforgery |
| SQL state differs (`Expected: "23514"`; the table is missing, so the insert fails with `42P01`) | 11 | no check constraints (no schema) |
| `42P01: relation "owner" / "audit_event" does not exist` | 5 | no `InitialOwnerAndAudit` migration |
| collections differ (column catalogue empty), `NotNull` failure (index missing, cookie missing) | 4 | no schema; no antiforgery cookie |
| `No service for type ControlPlaneDbContext / FirstRunSetupService / IAuthorizationPolicyProvider` | 4 | nothing registered in DI, no authorization |
| `NotEmpty` / `Contains` failures (no key file, no endpoints) | 2 | no Data Protection configuration, no endpoints |
| `Equal` failure `Expected: 1, Actual: 0` generator calls | 1 | no startup code generation |
| `The Control Plane has no static file under wwwroot/…` | 1 | no static files |

Every failure message matches the scenario and points at production code that does
not exist yet.

### Unexpected failures corrected during the run

- **`53300: sorry, too many clients already` (57 cases, first full run).** Test
  setup defect: every test starts its own host and database in parallel and the
  container kept PostgreSQL's default `max_connections=100`. Fixed in the test
  infrastructure only — the container runs with `max_connections=1000`, and each
  host clears its connection pool on disposal. Rerun: no environment or test-setup
  failure remains.
- **`Testcontainers` obsolete constructor** and **VSTest refused on .NET 10** —
  build/run configuration, fixed as above (image passed to the constructor;
  `global.json`).

## Untested Acceptance Criteria

None. Every AC maps to at least one written, compiling test (matrix v2).
Exclusions with reasons are in the test strategy §5.

## Open Decisions

None open. OD-007 was raised and resolved by the human during this run.

## Notes for IMPLEMENTATION

- The seams, configuration keys, names, cookie names and field names the tests rely
  on are listed in the test strategy §3.
- The skeleton files are production files owned by IMPLEMENTATION from now on; they
  may be reshaped together with the tests.
- Control Plane logging (DC-10, v68) is asserted only as far as AC-007 needs (no
  secrets in the log files, which must exist); the rest binds SECURITY_REVIEW.
