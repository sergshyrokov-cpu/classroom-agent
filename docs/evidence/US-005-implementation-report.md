---
artifact_type: implementation_report
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T14:55:37Z
updated_at: 2026-09-17T14:55:37Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-005-installation-legitimacy-check.md
    version: null
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/decisions/US-005-open-decisions.md
    version: 1
  - path: docs/designs/api/US-005-api-design.md
    version: 1
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
  - path: docs/designs/database/US-005-db-design.md
    version: 1
  - path: docs/designs/database/US-005-entity-model.md
    version: 1
  - path: docs/tests/US-005-test-strategy.md
    version: 1
  - path: docs/tests/US-005-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 73
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-005 Implementation Report — Installation legitimacy check and grace period

## 1. Summary

- **Implemented.** The installation host (`ClassroomAgent.Web`) with its settings validation, Serilog file
  logging, the legitimacy check background service, `LegitimacyState` and its first migration, the read-only
  determination, readiness/liveness on the private port and the empty public port; the installation's HTTP
  client of the service channel; the wire contract in `ClassroomAgent.Contracts`; in the Control Plane the
  check endpoint, the compatibility decision, `InstanceLicenseCheck` with its migration, and the last-check
  section of the installation detail page in `uk` and `en`.
- **Status.** Every Acceptance Criterion AC-001 … AC-015 is implemented; every compile-only skeleton member of
  OD-002 is replaced (no `NotImplementedException` remains).
- **Validation.** Build 0 warnings / 0 errors; full suite **882 / 882 pass, 0 skipped**; `dotnet format
  --verify-no-changes` clean.
- **Limitations.** Excluded scenarios of the test strategy §6 stay untested (certificate validation, the
  production handler's redirect setting, `SaveFailed`, log rotation). One test was corrected with the human's
  explicit approval (§7 D-1). Points for SECURITY_REVIEW are listed in §7.

## 2. Source Artifacts

| Artifact | Path | Version / status |
|---|---|---|
| User Story | `docs/stories/US-005-installation-legitimacy-check.md` | authored |
| Specification | `docs/specifications/US-005-spec.md` | v1 APPROVED (HUMAN_SPEC_APPROVAL 2026-09-17T13:29:44Z) |
| Open Decisions | `docs/decisions/US-005-open-decisions.md` | v1 APPROVED; OD-001, OD-002 RESOLVED option 1 |
| API design | `docs/designs/api/US-005-api-design.md`, `docs/designs/api/US-005-openapi.yaml` | v1 |
| Database design | `docs/designs/database/US-005-db-design.md`, `docs/designs/database/US-005-entity-model.md` | v1 |
| Test strategy / matrix | `docs/tests/US-005-test-strategy.md`, `docs/tests/US-005-ac-test-matrix.md` | v1 |
| Requirements | `trebovaniya.md` | v73 |

## 3. Implemented Acceptance Criteria

| AC | Implementation (file → symbol) | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `Web/Configuration/InstallationSettingsReader.cs` → `Read`; `InstallationSettingException`; `InstallationLogging.WriteStartupRefusal`; `Web/Program.cs` → `Main` | `Web.Configuration.InstallationConfigurationTests` | PASS |
| AC-002 | `Web/BackgroundServices/LegitimacyCheckBackgroundService.cs` → `ExecuteAsync`, `CheckOnceAsync`; `Web/Configuration/ReleaseVersion.cs`; `Application/Models/InstallationIdentity.cs` | `Web.BackgroundServices.LegitimacyCheckScheduleTests` | PASS |
| AC-003 | `ControlPlane/Controllers/LegitimacyCheckController.cs` → `Check`; `ControlPlane/Services/LegitimacyCheckService.cs` → `CheckAsync`; `Contracts/*` | `ControlPlane.Controllers.LegitimacyCheckEndpointTests`, `Infrastructure.ControlPlane.LegitimacyChannelContractTests`, `Architecture.ProjectReferenceTests` | PASS |
| AC-004 | `ControlPlane/Services/CompatibilityPolicy.cs` → `Decide`, `FromConfiguration`; `InstallationVersion` | `ControlPlane.Controllers.LegitimacyCheckCompatibilityTests` | PASS |
| AC-005 | `LegitimacyCheckService.ReplaceLastCheckAsync`, `LogAnswered`; `Persistence/InstanceLicenseCheck.cs`; `InstanceLicenseCheckConfiguration`; migration `AddInstanceLicenseCheck` | `ControlPlane.Controllers.InstanceLicenseCheckRecordTests`, `ControlPlane.Persistence.InstanceLicenseCheckSchemaTests`, `ControlPlane.Persistence.MigrationTests` | PASS |
| AC-006 | `LegitimacyCheckController.ReadInputAsync`, `LegitimacyCheckInput`, `ApplicationVersionAttribute`; `LegitimacyCheckService.LogUnknownInstallation` | `LegitimacyCheckEndpointTests`, `LegitimacyChannelContractTests` | PASS |
| AC-007 | `Application/UseCases/CheckLegitimacyUseCase.cs` → `ExecuteAsync`, `RecordAsync`; `Domain/Entities/LegitimacyState.cs`; `Infrastructure/ControlPlane/ControlPlaneClient.cs` → `CheckAsync`, `ParseAnswer` | `Application.UseCases.CheckLegitimacyRecordingTests`, `Web.Logging.LegitimacyLoggingTests`, `Infrastructure.ControlPlane.ControlPlaneClientTests` | PASS |
| AC-008 | `CheckLegitimacyUseCase` (failure → no write, `upgrade_required` → `RecordUpgradeRequired`); `ControlPlaneClient.Classify`; `LegitimacyCheckBackgroundService.LogCheckFailed` | `CheckLegitimacyRecordingTests`, `LegitimacyLoggingTests`, `ControlPlaneClientTests` | PASS |
| AC-009 | `Application/UseCases/GetLegitimacyModeQuery.cs` → `Determine`; `Domain/Rules/GracePeriod.cs` | `Application.UseCases.LegitimacyModeQueryTests` | PASS |
| AC-010 | `LegitimacyCheckBackgroundService.EvaluateModeAsync`, `LogOutcome`; `Application/UseCases/LegitimacyCheckMemory.cs` | `Web.Logging.LegitimacyLoggingTests` | PASS |
| AC-011 | `Infrastructure/Persistence/ClassroomAgentDbContext.cs`, `Configurations/LegitimacyStateConfiguration.cs`, `Repositories/LegitimacyStateRepository.cs`, migration `InitialLegitimacyState` | `Infrastructure.Persistence.InstallationDatabaseTests`, `LegitimacyModeQueryTests`, `CheckLegitimacyRecordingTests` | PASS |
| AC-012 | `ControlPlane/Views/Installations/Detail.cshtml` (section `installation-last-check`); `InstallationRegistry.GetAsync`; `InstallationLastCheckDto`; `InstallationDisplay.CompatibilityKey`; `SharedResource.uk/en.resx` | `ControlPlane.Controllers.InstallationLastCheckTests`, `ControlPlane.Localization.LastCheckTranslationTests` | PASS |
| AC-013 | `LegitimacyCheckController` (`[AllowAnonymous]`, `[IgnoreAntiforgeryToken]`, `[HttpPost]`); `Security/GlobalAntiforgeryFilter.cs` honours the SC-4 exemption | `LegitimacyCheckEndpointTests`, `ControlPlane.Security.AnonymousEndpointTests`, `ControlPlane.Security.AntiforgeryTests` | PASS |
| AC-014 | `Web/Security/PrivateEndpoints.cs` → `MapPrivateEndpoints`; `PrivatePortEndpointFilter`; `PublicPortMiddleware`; `Application/UseCases/GetReadinessQuery.cs` | `Web.Security.HealthEndpointTests`, `Web.Security.InstallationEndpointTests` | PASS |
| AC-015 | `LegitimacyCheckBackgroundService` log messages (categories/reasons/states only, exception type only); `LegitimacyCheckService` log messages (id, versions, status, compatibility); `InstallationLogging` (`System.Net.Http` at Warning) | `LegitimacyLoggingTests.Logs_CarryNoDomainClientIdOrExceptionTextFromTheCall`, `InstanceLicenseCheckRecordTests.KnownCall_IsLoggedAtInformation_UnknownAtWarning_WithoutDomainClientIdOrBody` | PASS |
| FR-014 | project references, package list of OD-001 | `Architecture.ProjectReferenceTests` | PASS |

## 4. Change Set

### Created — production

| File | Trace |
|---|---|
| `src/ClassroomAgent.Contracts/LegitimacyCheckRequest.cs` (skeleton, reworded comment) | FR-002; api-design §4 |
| `src/ClassroomAgent.Contracts/ServiceOutcome.cs` (skeleton + outcome constants) | FR-002; api-design §4 |
| `src/ClassroomAgent.Contracts/ServiceChannel.cs` | api-design §2, §4 (path, camelCase JSON, unknown properties ignored — DC-12) |
| `src/ClassroomAgent.Contracts/WireStatus.cs`, `WireCompatibility.cs` | api-design §4 (wire values shared by both sides) |
| `src/ClassroomAgent.Contracts/ContractVersion.cs`, `LegitimacyCheckResponse.cs`, `ClassroomAgent.Contracts.csproj` (skeleton, unchanged) | FR-002, I-7 |
| `src/ClassroomAgent.Domain/Entities/LegitimacyState.cs` | entity model §3.1; FR-009 |
| `src/ClassroomAgent.Domain/Rules/GracePeriod.cs` | FR-008, I-4; entity model §3.5 (`Domain.Rules`) |
| `src/ClassroomAgent.Domain/Enums/*.cs`, `ClassroomAgent.Domain.csproj` (skeleton, unchanged) | entity model §3.2 |
| `src/ClassroomAgent.Application/Models/CheckOutcome.cs`, `LegitimacyCheckFailure.cs` | entity model §3.5 (`CheckOutcome`); test strategy §3 (categories) |
| `src/ClassroomAgent.Application/Models/InstallationIdentity.cs` | FR-007 (id, release version, contract version) |
| `src/ClassroomAgent.Application/Models/ReadinessState.cs` | FR-013 |
| `src/ClassroomAgent.Application/Models/CheckFailureCategory.cs`, `ControlPlaneCheckReply.cs`, `LegitimacyMode.cs`, `LegitimacyModeReason.cs`, `Ports/IControlPlaneClient.cs`, `ClassroomAgent.Application.csproj` (skeleton, unchanged) | api-design §11 |
| `src/ClassroomAgent.Application/Ports/ILegitimacyStateRepository.cs`, `IUnitOfWork.cs` | entity model §3.4 (repository port, unit of work); AD-7 |
| `src/ClassroomAgent.Application/UseCases/CheckLegitimacyUseCase.cs` | FR-007; db-design §4.2 |
| `src/ClassroomAgent.Application/UseCases/GetLegitimacyModeQuery.cs` | FR-008 |
| `src/ClassroomAgent.Application/UseCases/GetReadinessQuery.cs` | FR-013 |
| `src/ClassroomAgent.Application/UseCases/LegitimacyCheckMemory.cs` | I-3 (in-process previous result and mode); FR-010, FR-013 |
| `src/ClassroomAgent.Infrastructure/ClassroomAgent.Infrastructure.csproj` (skeleton, unchanged) | OD-001 |
| `src/ClassroomAgent.Infrastructure/ControlPlane/ControlPlaneClient.cs` | FR-007, VR-003, I-6; api-design §6 |
| `src/ClassroomAgent.Infrastructure/Persistence/ClassroomAgentDbContext.cs`, `ClassroomAgentDbContextOptions.cs` | entity model §3.4; PC-1, PC-5 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/LegitimacyStateConfiguration.cs` | entity model §3.3; db-design §4.1 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/LegitimacyStateRepository.cs` | entity model §3.4; db-design §4.2, §4.3 |
| `src/ClassroomAgent.Infrastructure/Persistence/UnitOfWork.cs` | AD-7 (supporting) |
| `src/ClassroomAgent.Infrastructure/Persistence/TimestampInterceptor.cs` | PC-6; entity model §3.4 |
| `src/ClassroomAgent.Infrastructure/Persistence/DesignTimeClassroomAgentDbContextFactory.cs` | db-design §6.2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260917144721_InitialLegitimacyState.cs`, `.Designer.cs`, `ClassroomAgentDbContextModelSnapshot.cs` | db-design §6.2; PC-2 |
| `src/ClassroomAgent.Web/ClassroomAgent.Web.csproj` (skeleton + packages of OD-001) | OD-001; DC-10; PC-2 |
| `src/ClassroomAgent.Web/Program.cs` | FR-014, FR-001; test strategy §3 item 2 |
| `src/ClassroomAgent.Web/Configuration/InstallationSettings.cs`, `InstallationSettingsReader.cs`, `InstallationSettingException.cs` | FR-001, VR-001; api-design §10 |
| `src/ClassroomAgent.Web/Configuration/InstallationLogging.cs` | FR-012; DC-10 |
| `src/ClassroomAgent.Web/Configuration/ReleaseVersion.cs` | I-7; api-design §6 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | FR-014 (DI wiring) |
| `src/ClassroomAgent.Web/BackgroundServices/LegitimacyCheckBackgroundService.cs` | FR-003, FR-010, FR-012 |
| `src/ClassroomAgent.Web/Security/PrivateEndpoints.cs`, `PrivatePortEndpointFilter.cs` | FR-013; api-design §7; DC-6 |
| `src/ClassroomAgent.Web/Security/PublicPortMiddleware.cs` | FR-014, I-13; api-design §8 |
| `src/ClassroomAgent.ControlPlane/Controllers/LegitimacyCheckController.cs` | FR-004; api-design §5 |
| `src/ClassroomAgent.ControlPlane/Controllers/LegitimacyCheckInput.cs`, `ApplicationVersionAttribute.cs` | VR-002 (Data Annotations) |
| `src/ClassroomAgent.ControlPlane/Services/LegitimacyCheckService.cs`, `LegitimacyCheckResult.cs` | FR-004, FR-006, FR-012; entity model §2.5; db-design §3.2 |
| `src/ClassroomAgent.ControlPlane/Services/CompatibilityPolicy.cs`, `InstallationVersion.cs` | FR-005, VR-004, I-7, I-8 |
| `src/ClassroomAgent.ControlPlane/Services/InstallationLastCheckDto.cs` | FR-011; api-design §11 |
| `src/ClassroomAgent.ControlPlane/Persistence/InstanceLicenseCheck.cs`, `CompatibilityState.cs` | entity model §2.1, §2.2 |
| `src/ClassroomAgent.ControlPlane/Persistence/Configurations/InstanceLicenseCheckConfiguration.cs` | entity model §2.3; db-design §3.1 |
| `src/ClassroomAgent.ControlPlane/Persistence/Migrations/20260917144004_AddInstanceLicenseCheck.cs`, `.Designer.cs` | db-design §6.1; PC-2 |

### Modified — production

| File | Trace |
|---|---|
| `ClassroomAgent.sln` (at TEST_WRITING) | OD-002 |
| `src/ClassroomAgent.ControlPlane/ClassroomAgent.ControlPlane.csproj` | FR-014 (reference to `Contracts` only) |
| `src/ClassroomAgent.ControlPlane/Program.cs` | FR-005 (version settings at startup), DI of `CompatibilityPolicy`, `LegitimacyCheckService` |
| `src/ClassroomAgent.ControlPlane/Persistence/ControlPlaneDbContext.cs`, `TimestampInterceptor.cs`, `Migrations/ControlPlaneDbContextModelSnapshot.cs` | entity model §2.4 |
| `src/ClassroomAgent.ControlPlane/Security/GlobalAntiforgeryFilter.cs` | S-01, SC-4 exemption list (legitimacy check) |
| `src/ClassroomAgent.ControlPlane/Security/ControlPlaneExceptionHandler.cs` | api-design §5, §13 (`500` empty body on the channel) |
| `src/ClassroomAgent.ControlPlane/Services/InstallationDetailDto.cs`, `InstallationRegistry.cs` | FR-011; api-design §11; db-design §3.3 |
| `src/ClassroomAgent.ControlPlane/Controllers/InstallationDisplay.cs`, `Views/Installations/Detail.cshtml` | FR-011; api-design §9 |
| `src/ClassroomAgent.ControlPlane/Localization/SharedResource.uk.resx`, `SharedResource.en.resx` | FR-011; NFR-073; TC-8 (10 keys each) |

### Tests

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/InstallationLastCheckTests.cs` | AC-012; one line changed at IMPLEMENTATION with human approval (§7 D-1) |
| every other test file of `docs/evidence/US-005-test-generation-report.md` §3 | unchanged at IMPLEMENTATION; traced in the `ac_test_matrix` |

No secret, generated database file, `.xlsx`, IDE-local config or TRX output is in the change set (TRX files
land in the git-ignored `bin/`).

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Baseline (red) | recorded by TEST_WRITING, `docs/evidence/US-005-test-generation-report.md` §4 | 2 | 882 total, 588 pass, 294 fail for missing behaviour |
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| Control Plane increment | `dotnet test --solution ClassroomAgent.sln --no-build --filter-namespace "ClassroomAgent.Tests.ControlPlane*"` | 2 | 680 total, 679 pass, 1 fail (§7 D-1) |
| Installation increment | `dotnet test --solution ClassroomAgent.sln --no-build --filter-namespace …Web* …Application* …Infrastructure*` | 2 → 0 | first run 22 fail (singleton insert, §7 D-2); after the fix 185 / 185 pass |
| Migrations | `dotnet ef migrations add AddInstanceLicenseCheck --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations`; `dotnet ef migrations add InitialLegitimacyState --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web --output-dir Persistence/Migrations` | 0 | generated; model-drift tests pass |
| Full suite | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us005-green.trx` (2026-09-17T14:54Z) | 0 | **882 total, 882 pass, 0 fail, 0 skipped**; 47 s; Docker Testcontainers `postgres:17-alpine` |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no changes |
| Markers | search of `src` for `TODO`, `TBD`, `FIXME`, `???`, `NotImplementedException` | — | none |

Security-relevant suites inside the full run: `AnonymousEndpointTests`, `AntiforgeryTests`,
`HealthEndpointTests`, `InstallationEndpointTests`, `ProjectReferenceTests`, the log-leak assertions of
`LegitimacyLoggingTests` and `InstanceLicenseCheckRecordTests`.

## 6. Configuration Changes

| Host | Key | Rule | Required by |
|---|---|---|---|
| Installation | `Installation:Id` | required, UUID (`D` form, any case) | FR-001, api-design §10 |
| Installation | `ControlPlane:Address` | required, absolute `https`, host, no user info / query / fragment | FR-001, api-design §10 |
| Installation | `Hosting:PrivatePort` | required, 1–65535, differs from every port in `urls` | FR-001, I-2, api-design §10 |
| Installation | `ConnectionStrings:Installation` | required, non-empty | FR-001, PC-1 |
| Installation | `LogFile:Directory` | optional; `logs` next to the application when unset | DC-10; test strategy §3 item 1 |
| Control Plane | `Compatibility:MinimumSupportedVersion`, `Compatibility:RecommendedVersion` | optional `MAJOR.MINOR.PATCH`; set but invalid stops the host | FR-005, VR-004 |

No setting is committed with a value; no `appsettings.json` was added to `ClassroomAgent.Web` (test-generation
report §9). The installation adds a second Kestrel endpoint `http://*:{Hosting:PrivatePort}` next to the
configured `urls` (DC-6; see D-4). Packages added exactly as OD-001: `ClassroomAgent.Web` →
`Microsoft.EntityFrameworkCore.Design` 10.0.4 (`PrivateAssets=all`), `Serilog.AspNetCore` 10.0.0,
`Serilog.Sinks.File` 7.0.0 — the versions already used by the Control Plane.

## 7. Deviations and Discovered Problems

- **D-1 — Test corrected with human approval.** `InstallationLastCheckTests.AfterARealCheck_ShowsTimeVersionsStatusAndCompatibility`
  advanced the clock by 1 hour before opening the detail page with the Owner session; the approved US-001
  idle timeout of 30 minutes (SC-2) made the page answer `302 /sign-in`. The test contradicted an approved
  artifact, so the human was asked; they chose to correct it (2026-09-17, in the conversation). The advance is
  now 10 minutes; the assertions are unchanged and still show that the displayed time is the answer time, not
  "now". No other test was changed.
- **D-2 — `singleton` shadow column.** EF Core sends the CLR default `false` for a `bool` shadow property with a
  database default, which violates `ck_legitimacy_state_singleton`. `LegitimacyStateRepository.Add` sets it to
  `true` explicitly; schema and migration are exactly db-design §4.
- **D-3 — defensive retry of db-design §4.2 step 3 not implemented.** A unique violation on
  `uq_legitimacy_state_singleton` (impossible with one check at a time, FR-003) is treated like any failed save:
  outcome `SaveFailed`, state unchanged, next check in 15 minutes updates the existing row. Behaviour stays
  within FR-007.
- **D-4 — private endpoint interface (for SECURITY_REVIEW).** DC-6 requires the private endpoint to be bound
  only to the private network interface. No setting names that interface, so the host binds
  `http://*:{PrivatePort}`; the restriction to the private network must come from the deployment (firewall /
  network). Requests on any other port are refused by `PublicPortMiddleware` and the route-group filter,
  independent of headers. A dedicated bind-address setting would be a new configuration key — not introduced.
- **D-5 — public-port comparison.** VR-001 "differs from every public endpoint port" is checked against the
  `urls` setting (the key the tests fix). Ports given only through `http_ports` / `https_ports` or a `Kestrel`
  endpoints section are not compared.
- **D-6 — additional failure category and event.** An exception thrown inside the check is category
  `UnexpectedError` of `LegitimacyCheckFailed` (spec §8 "Exception inside the check"); the background service's
  own safety net logs `LegitimacyCheckException` with the exception type name only. Both are additive to the
  test strategy §3 names; no message text is logged (S-08).
- **D-7 — result-change logging.** The first check after startup has no previous result and is not logged as a
  change; the test strategy §7 leaves this open (2–3 accepted).
- **D-8 — placement.** `LegitimacyCheckMemory` (in-process state of I-3) sits in `Application.UseCases` next to
  its two users; wire helper types `ServiceChannel`, `WireStatus`, `WireCompatibility` were added to
  `Contracts`; `InstallationVersion` sits in `ControlPlane.Services`. No new namespace was created
  (`Domain.Rules` is listed in `package-map.md`).
- **D-9 — Control Plane wiring.** The antiforgery filter now skips endpoints carrying
  `[IgnoreAntiforgeryToken]`; the only such endpoint is the check (asserted by `AntiforgeryTests`). The
  exception handler answers `500` with an empty body for paths under `/service`.
- **D-10 — not used from OD-001.** `Microsoft.EntityFrameworkCore.Design` in `Infrastructure` and a
  `FrameworkReference` there were not needed and not added.

## 8. Open Decisions

None touched and none newly required. OD-001 and OD-002 were applied as resolved. D-4 and D-5 are recorded
for SECURITY_REVIEW; neither changes approved behaviour.

## Result Envelope

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-005
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-005-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "D-1: InstallationLastCheckTests clock advance 1 h → 10 min (Owner idle timeout 30 min), approved by the human in the conversation."
    - "D-4: private Kestrel endpoint binds http://*:{PrivatePort}; interface restriction left to deployment (DC-6) — review at SECURITY_REVIEW."
    - "D-5: private/public port clash checked against `urls` only."
    - "D-3: db-design §4.2 defensive singleton retry not implemented; conflict = SaveFailed."
    - "D-6: extra category UnexpectedError and event LegitimacyCheckException (type name only)."
```
