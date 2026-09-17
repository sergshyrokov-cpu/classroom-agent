---
artifact_type: test_generation_report
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T14:25:00Z
updated_at: 2026-09-17T14:25:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-005-test-strategy.md
    version: 1
  - path: docs/tests/US-005-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
  - path: docs/designs/database/US-005-db-design.md
    version: 1
supersedes: null
---

# US-005 Test Generation Report

## 1. Result

**Red phase verified.** The solution builds with 0 warnings and 0 errors. Full suite:
**882 cases — 588 pass, 294 fail, 0 skipped.**

- Before this stage: 571 cases, all passing (US-004 final state).
- 568 unchanged existing cases pass.
- 3 changed existing cases fail for missing behaviour (the Control Plane has no check
  endpoint or migration yet): `MigrationTests.Migrations_…InstanceLicenseCheck_InOrder`,
  `AnonymousEndpointTests.OnlySc4EndpointsAllowAnonymous`,
  `AntiforgeryTests.OnlyTheLegitimacyCheckIsExemptFromAntiforgery`.
- 311 new cases (144 methods): 291 fail for missing behaviour, 20 pass (15 guards, 5
  seam checks — §5).
- No unexpected failure remains.

## 2. Production skeleton created (OD-002, option 1)

Compile-only; members throw `NotImplementedException`; nothing registered in DI.
IMPLEMENTATION owns these files and may reshape them together with the tests.

| Project | Files |
|---|---|
| `ClassroomAgent.Domain` (new, no references) | `ClassroomAgent.Domain.csproj`, `Entities/LegitimacyState.cs`, `Enums/InstallationStatus.cs`, `Enums/CompatibilityState.cs` |
| `ClassroomAgent.Contracts` (new, no references) | `ClassroomAgent.Contracts.csproj`, `LegitimacyCheckRequest.cs`, `LegitimacyCheckResponse.cs`, `ServiceOutcome.cs`, `ContractVersion.cs` |
| `ClassroomAgent.Application` (→ Domain) | `ClassroomAgent.Application.csproj`, `Ports/IControlPlaneClient.cs`, `Models/ControlPlaneCheckReply.cs`, `Models/CheckFailureCategory.cs`, `Models/LegitimacyMode.cs`, `Models/LegitimacyModeReason.cs`, `UseCases/GetLegitimacyModeQuery.cs` |
| `ClassroomAgent.Infrastructure` (→ Application, Domain, Contracts; packages `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `EFCore.NamingConventions` 10.0.1 per OD-001) | `ClassroomAgent.Infrastructure.csproj`, `Persistence/ClassroomAgentDbContext.cs`, `ControlPlane/ControlPlaneClient.cs` |
| `ClassroomAgent.Web` (→ all four; no package yet) | `ClassroomAgent.Web.csproj`, `Program.cs` (a named class `ClassroomAgent.Web.Program`, test strategy §3 item 2) |
| Solution | `ClassroomAgent.sln` — five projects added under `src` |

Not yet referenced, left to IMPLEMENTATION per OD-001: `Microsoft.EntityFrameworkCore.Design`
(Infrastructure, Web), `Serilog.AspNetCore`, `Serilog.Sinks.File` (Web); the
Control Plane's project reference to `Contracts`.

## 3. Test files

### Created

| File | Methods / cases |
|---|---|
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/LegitimacyCheckEndpointTests.cs` | 15 / 43 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/LegitimacyCheckCompatibilityTests.cs` | 9 / 25 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstanceLicenseCheckRecordTests.cs` | 7 / 7 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationLastCheckTests.cs` | 7 / 9 |
| `tests/ClassroomAgent.Tests/ControlPlane/Localization/LastCheckTranslationTests.cs` | 1 / 10 |
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/InstanceLicenseCheckSchemaTests.cs` | 6 / 15 |
| `tests/ClassroomAgent.Tests/Web/Configuration/InstallationConfigurationTests.cs` | 8 / 34 |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/LegitimacyCheckScheduleTests.cs` | 12 / 16 |
| `tests/ClassroomAgent.Tests/Web/Logging/LegitimacyLoggingTests.cs` | 8 / 12 |
| `tests/ClassroomAgent.Tests/Web/Security/HealthEndpointTests.cs` | 14 / 20 |
| `tests/ClassroomAgent.Tests/Web/Security/InstallationEndpointTests.cs` | 3 / 10 |
| `tests/ClassroomAgent.Tests/Application/UseCases/CheckLegitimacyRecordingTests.cs` | 9 / 16 |
| `tests/ClassroomAgent.Tests/Application/UseCases/LegitimacyModeQueryTests.cs` | 11 / 14 |
| `tests/ClassroomAgent.Tests/Infrastructure/ControlPlane/ControlPlaneClientTests.cs` | 11 / 43 |
| `tests/ClassroomAgent.Tests/Infrastructure/ControlPlane/LegitimacyChannelContractTests.cs` | 4 / 4 |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/InstallationDatabaseTests.cs` | 9 / 16 |
| `tests/ClassroomAgent.Tests/Architecture/ProjectReferenceTests.cs` | 5 / 12 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ManualTimeProviderTests.cs` | 5 / 5 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ManualTimeProvider.cs` | fixture |
| `tests/ClassroomAgent.Tests/TestInfrastructure/FakeControlPlaneClient.cs` | fixture |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationTestHost.cs` | fixture |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationFactory.cs` | fixture |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstallationConfigurationKeys.cs` | seam |
| `tests/ClassroomAgent.Tests/TestInfrastructure/LegitimacyStateRow.cs` | row |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InstanceLicenseCheckRow.cs` | row |
| `tests/ClassroomAgent.Tests/TestInfrastructure/LegitimacyCheckHostExtensions.cs` | helpers |
| `tests/ClassroomAgent.Tests/TestInfrastructure/LegitimacyCheckTestData.cs` | data |
| `tests/ClassroomAgent.Tests/TestInfrastructure/LogEvent.cs` | log parser |

### Modified

| File | Change |
|---|---|
| `tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj` | project reference to `ClassroomAgent.Web` |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ControlPlaneTestHost.cs` | optional extra settings; `CreateServerHandler()` |
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/MigrationTests.cs` | fourth migration `AddInstanceLicenseCheck`, table `instance_license_check` |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/AnonymousEndpointTests.cs` | SC-4 list includes the legitimacy check POST |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/AntiforgeryTests.cs` | `NoEndpointIsExemptFromAntiforgery` → `OnlyTheLegitimacyCheckIsExemptFromAntiforgery`; the token enumeration excludes the SC-4 exemption |

No existing assertion was weakened: the three changed tests follow the SC-4 lists,
which already name the legitimacy check (security-conventions SC-4).

## 4. Commands

| Step | Command | Outcome |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| Seam guard | `dotnet test --solution ClassroomAgent.sln --no-build --filter-class "ClassroomAgent.Tests.TestInfrastructure.ManualTimeProviderTests"` | 5/5 pass |
| Full suite | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us005-red.trx` (2026-09-17T14:23:27Z) | exit 2 (failures expected); 882 total, 588 pass, 294 fail; 46 s |

Docker was running (Testcontainers `postgres:17-alpine`).

## 5. Results by class

| Class | Fail | Pass | Failure reason (sampled messages) |
|---|---|---|---|
| `Application.UseCases.CheckLegitimacyRecordingTests` | 16 | 0 | host entry point not implemented; `legitimacy_state` does not exist |
| `Application.UseCases.LegitimacyModeQueryTests` | 14 | 0 | same |
| `Architecture.ProjectReferenceTests` | 1 | 11 | Control Plane does not reference `Contracts` yet |
| `ControlPlane.Controllers.InstallationLastCheckTests` | 8 | 1 | `instance_license_check` does not exist; section elements missing |
| `ControlPlane.Controllers.InstanceLicenseCheckRecordTests` | 6 | 1 | endpoint missing (404), table missing |
| `ControlPlane.Controllers.LegitimacyCheckCompatibilityTests` | 25 | 0 | endpoint missing (HTML 404 body); no startup validation |
| `ControlPlane.Controllers.LegitimacyCheckEndpointTests` | 41 | 2 | endpoint missing (404 instead of 200/400) |
| `ControlPlane.Localization.LastCheckTranslationTests` | 10 | 0 | keys missing in `uk` |
| `ControlPlane.Persistence.InstanceLicenseCheckSchemaTests` | 15 | 0 | table missing |
| `ControlPlane.Persistence.MigrationTests` | 1 | 1 | fourth migration missing |
| `ControlPlane.Security.AnonymousEndpointTests` | 1 | 1 | check endpoint not mapped |
| `ControlPlane.Security.AntiforgeryTests` | 1 | 3 | no exempt endpoint yet |
| `Infrastructure.ControlPlane.ControlPlaneClientTests` | 43 | 0 | `ControlPlaneClient.CheckAsync` not implemented |
| `Infrastructure.ControlPlane.LegitimacyChannelContractTests` | 4 | 0 | same |
| `Infrastructure.Persistence.InstallationDatabaseTests` | 16 | 0 | no installation migration (`42P01`) |
| `Web.BackgroundServices.LegitimacyCheckScheduleTests` | 16 | 0 | host entry point not implemented |
| `Web.Configuration.InstallationConfigurationTests` | 34 | 0 | host entry point not implemented; keys not named |
| `Web.Logging.LegitimacyLoggingTests` | 12 | 0 | host not implemented; table missing |
| `Web.Security.HealthEndpointTests` | 20 | 0 | host not implemented |
| `Web.Security.InstallationEndpointTests` | 10 | 0 | host not implemented |

**Passing new cases (20), each reviewed:**

- Guards that hold before and must hold after: `LegitimacyCheckEndpointTests.Checks_WriteNoAuditRow_EvenForUnknownAndInvalidCalls`,
  `LegitimacyCheckEndpointTests.Checks_DoNotChangeTheInstallation`,
  `InstanceLicenseCheckRecordTests.Answer_DoesNotEchoVersionsOrIdentifier`,
  `InstallationLastCheckTests.DetailPage_StaysOwnerOnly`; `ProjectReferenceTests` — 5
  project-reference rows (Domain, Contracts, Application, Infrastructure, Web as created
  by the skeleton), 3 framework-free projects, `ApplicationAssembly_…`,
  `ContractsAssembly_…`, `WebAssembly_…`. They pass because the skeleton already has
  the allowed shape and nothing leaks yet; they become meaningful as the implementation
  adds code.
- Seam checks: `ManualTimeProviderTests` (5).

## 6. Corrections made during the stage

- **Skeleton `Program` leaked arguments.** The first run showed 12 configuration tests
  passing and 13 failing on "value found": `WebApplicationFactory` passes settings as
  command-line arguments, and the skeleton's exception message echoed them. The message
  no longer carries the arguments; all 34 configuration cases now fail for the right
  reason.
- **English page test removed.** The Control Plane does not switch language until
  US-039; the test now asserts Ukrainian by default with `Accept-Language: en`, as the
  US-004 translation tests do.
- **Hanging-call test** keeps clock movement below the 30-second call limit, so a
  legitimate timeout cannot make it fail.
- **Test run command**: `--report-trx` is not available; the xUnit v3 reporter
  (`--report-xunit-trx`) is used as in US-004.

## 7. Untested Acceptance Criteria

None. Excluded sub-scenarios and why: test strategy §6 (certificate validation, the
production handler's redirect setting, `SaveFailed`, rotation, shutdown timing).

## 8. Open Decisions

None open. OD-001 and OD-002 resolved (option 1) and applied (§2).

## 9. Notes for IMPLEMENTATION

- Honour the seams of test strategy §3 — configuration keys, `ClassroomAgent.Web.Program`,
  `TimeProvider` timers, the DI-resolved `IControlPlaneClient` and
  `GetLegitimacyModeQuery`, the log event names and properties.
- The Web project should not add an `appsettings.json` with a name the Control Plane
  already copies into the test output (both are referenced by the test project); keep
  required settings out of committed files.
- The test project references both hosts; the Control Plane keeps its global
  `Program`.
