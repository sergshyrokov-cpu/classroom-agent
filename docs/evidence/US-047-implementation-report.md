---
artifact_type: implementation_report
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T13:20:00Z
updated_at: 2026-10-14T13:20:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-047-windows-service.md
    version: null
  - path: docs/specifications/US-047-spec.md
    version: 1
  - path: docs/decisions/US-047-open-decisions.md
    version: 2
  - path: docs/designs/api/US-047-api-design.md
    version: 1
  - path: docs/designs/database/US-047-db-design.md
    version: 1
  - path: docs/tests/US-047-test-strategy.md
    version: 1
  - path: docs/tests/US-047-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-047 Implementation Report — Run the Control Plane and the installation as Windows services

## 1. Summary

Both hosts now decide at start-up whether the Service Control Manager started
them (`WindowsServiceHelpers.IsWindowsService()`, FR-001) and build through
`Program.BuildApplication(args, runsAsService)` (OD-003). As a service: content
root = application folder (FR-003), `WindowsServiceLifetime` (FR-002), no Event
Log provider (FR-007). In both modes a relative `LogFile:Directory` /
`DataProtection:KeyDirectory` resolves against the content root (FR-004).
Outside a service the builder is exactly `WebApplication.CreateBuilder(args)`
(AC-004). The package `Microsoft.Extensions.Hosting.WindowsServices` 10.0.12 is
referenced by the two hosts only (FR-008).

Build, the full test suite and the format check pass.

**Pending — manual verification of FR-009 (OD-001 a).** Registering, starting,
stopping a real Windows service needs an administrator shell on a Windows
machine and changes that machine's service registry; it was not done by the
agent. Section 3.1 gives the steps; the Owner performs them and records the
outcome here before `HUMAN_PR_APPROVAL`.

security_sensitive is `true` because the Story touches where logs, Data
Protection keys and the setup code may go (SC-7, SC-10).

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-047-windows-service.md` | — |
| Specification | `docs/specifications/US-047-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-047-open-decisions.md` | 2 (OD-001 a, OD-002 a, OD-003 a) |
| API design | `docs/designs/api/US-047-api-design.md` | 1 (NOT_APPLICABLE) |
| DB design | `docs/designs/database/US-047-db-design.md` | 1 (NOT_APPLICABLE) |
| Test strategy | `docs/tests/US-047-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-047-ac-test-matrix.md` | 1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Web.Program.Main`, `Web.Program.BuildApplication`, `Web.Configuration.WindowsServiceHosting.CreateBuilder` | `WindowsServiceHostingTests.AsAService_*(Installation)` | automated PASS; manual pending (3.1) |
| AC-002 | Control Plane entry point, `Program.BuildApplication`, `Program.CreateBuilder` | `WindowsServiceHostingTests.AsAService_*(ControlPlane)` | automated PASS; manual pending (3.1) |
| AC-003 | no new code (FR-005): `WindowsServiceLifetime` turns an SCM stop into the ordinary host shutdown | `SyncScheduleTests.HostStop_*` (guard) | automated PASS; manual pending (3.1) |
| AC-004 | console branch = `WebApplication.CreateBuilder(new WebApplicationOptions { Args = args })` | `WindowsServiceHostingTests.FromTheConsole_*`; full suite | PASS |
| AC-005 | unchanged `SetupCodeStartup` / `StandardOutputOperatorConsole`; no new log destination | `SetupCodeServiceModeTests` (2) | PASS |
| AC-006 | `Web.Configuration.ConfiguredDirectory.Resolve`, `InstallationLogging.Directory(configuration, contentRoot)`, `InstallationSettingsReader.Read(…, contentRoot)`; Control Plane `Program.ResolveDirectory` | `ConfiguredDirectoryResolutionTests` (11) | PASS |
| AC-007 | lifetime registered directly (no `AddWindowsService`, so no `EventLogLoggerProvider`); Serilog stays the logger factory | `WindowsServiceHostingTests.AsAService_LoggingGoesThroughSerilogAlone_AndNoProviderIsAdded`, `AsAService_LogEventsGoToTheRollingFile` | PASS |
| AC-008 | `ClassroomAgent.Web.csproj`, `ClassroomAgent.ControlPlane.csproj` | `WindowsServicePackageTests` (8) | PASS |

### 3.1 Manual verification (FR-009) — to be recorded by the Owner

Run in an administrator shell on the Windows machine, for each host (names and
paths are examples; DC-14, Story Notes):

1. `dotnet publish` each host into its folder; for the Control Plane complete
   the first-run setup from a console first (DC-2 step 2).
2. `sc.exe create <name> binPath= "<folder>\<host>.exe" start= auto obj= ".\<account>" password= …`;
   `sc.exe failure <name> reset= 86400 actions= restart/60000/restart/60000/restart/60000`.
3. Put the secrets as the service account's own environment variables (OD-002 a).
4. `sc.exe start <name>`; `sc.exe query <name>` shows `RUNNING`. Installation:
   public endpoint and private port answer, the synchronization background
   service runs (log). Control Plane: HTTPS endpoint answers.
5. The service saw the secrets (start succeeded); otherwise record the OD-002 b)
   fallback used.
6. `sc.exe stop <name>`: `STOPPED`, and the `Microsoft.Hosting.Lifetime`
   "Application is shutting down" event in the log file. Repeat with a Windows
   restart.
7. The log file and the `key-*.xml` files are in the application folder (or the
   configured absolute path), not under `C:\Windows\System32`.

Outcome: **not yet performed.**

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Program.cs` | `Main` → `BuildApplication(args, IsWindowsService()).Run()`; body moved into `BuildApplication`, returns the app; content root passed to log and settings | FR-001 … FR-004, OD-003, AC-001, AC-004, AC-006 |
| `src/ClassroomAgent.Web/Configuration/WindowsServiceHosting.cs` (new) | builder per mode; `WindowsServiceLifetime` as a service | FR-002, FR-003, FR-007, AC-001, AC-004, AC-007 |
| `src/ClassroomAgent.Web/Configuration/ConfiguredDirectory.cs` (new) | relative → content root, absolute as given, I-5 handling | FR-004, VR-001, I-5, AC-006 |
| `src/ClassroomAgent.Web/Configuration/InstallationLogging.cs` | `Directory(configuration, contentRoot)` | FR-004, AC-006 |
| `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` | `Read(…, contentRoot)`; key directory resolved before VR-003 checks | FR-004, VR-001, AC-006 |
| `src/ClassroomAgent.Web/ClassroomAgent.Web.csproj` | package reference | FR-008, AC-008 |
| `src/ClassroomAgent.ControlPlane/Program.cs` | entry point → `BuildApplication(args, IsWindowsService()).Run()`; top-level body moved into `BuildApplication` (re-indented, otherwise unchanged); `CreateBuilder`, `ResolveDirectory`; `RequiredSetting` moved into the class | FR-001 … FR-004, FR-007, OD-003, AC-002, AC-004, AC-006, AC-007 |
| `src/ClassroomAgent.ControlPlane/ClassroomAgent.ControlPlane.csproj` | package reference | FR-008, AC-008 |
| `tests/…/Hosting/*.cs`, `tests/…/Architecture/WindowsServicePackageTests.cs`, `tests/…/TestInfrastructure/{BuiltHost,BuiltHostKind,HostBuildArguments,HostFolders}.cs`, `ControlPlaneTestHost.cs` | written at TEST_WRITING; unchanged here | `ac_test_matrix` |
| `docs/tests/US-047-*`, `docs/evidence/US-047-*`, `docs/decisions/US-047-open-decisions.md` | workflow artifacts | Git Policy closed list |

No secret, generated database file, `.xlsx` or IDE-local config in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0; 0 warnings, 0 errors |
| Story tests | `dotnet test tests/ClassroomAgent.Tests --no-build --filter "…Hosting\|…WindowsServicePackageTests\|…InstallationConfigurationTests"` | 71 / 71 passed |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | exit 0; total 5677, passed 5677, failed 0, skipped 0 |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --include <changed .cs files>` | exit 0 |

Red phase before implementation (TEST_WRITING report): 33 failing for the
expected reason; now all green.

Delegation: none. The change is two start-up files and three small
configuration types; every run was a single command whose output was filtered
inline, so a subagent would only have repeated the context.

## 6. Configuration Changes

No configuration key added, removed or renamed. Behaviour change of existing
keys: a relative `LogFile:Directory` or `DataProtection:KeyDirectory` now
resolves against the content root (FR-004) — outside a service the content root
is the working directory, so the result is unchanged unless `--contentRoot` is
given. No `appsettings` file changed.

## 7. Deviations and Discovered Problems

- **I-5 resolution chosen:** `C:logs` and `\logs` drop their drive / root prefix
  and resolve to `<content root>\logs` (the Specification says only "resolved
  against the content root like any relative value"). Not covered by an
  automated test (test strategy §6).
- **Lifetime registration:** the package's `AddWindowsService` /
  `UseWindowsService` were not used — they re-check detection internally and add
  an Event Log provider. `WindowsServiceLifetime` is registered directly, guarded
  by `OperatingSystem.IsWindows()` (CA1416). The service name is not set in code:
  a service in its own process is started by the SCM regardless.
- **Key directory:** a path that `Path.GetFullPath` rejects is now refused with
  the existing VR-003 message (same as a path that cannot be created).
- **Control Plane `Program.cs` diff** is large because the top-level statements
  were re-indented into `BuildApplication`; statements and comments are
  otherwise identical.
- Test-generation finding 1 (service content-root assertions vacuous under
  `dotnet test`) stands; manual step 7 covers it.

## 8. Open Decisions

OD-001 a), OD-002 a), OD-003 a) — resolved, applied. No new Open Decision.
