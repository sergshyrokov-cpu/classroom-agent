---
artifact_type: ac_test_matrix
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T12:43:32Z
updated_at: 2026-10-14T12:43:32Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-047-spec.md
    version: 1
  - path: docs/decisions/US-047-open-decisions.md
    version: 2
  - path: docs/tests/US-047-test-strategy.md
    version: 1
supersedes: null
---

# US-047 Acceptance Criteria → Test Matrix

Classes: **H** `Hosting.WindowsServiceHostingTests`, **D**
`Hosting.ConfiguredDirectoryResolutionTests`, **S**
`Hosting.SetupCodeServiceModeTests`, **P**
`Architecture.WindowsServicePackageTests`, **R** existing
`Web.BackgroundServices.SyncScheduleTests`. "Both" = theory over the
installation and the Control Plane. **M** = manual verification of OD-001 a),
recorded in the `implementation_report` (spec FR-009).

Status: **RED** — fails before implementation for the expected reason (skeleton
throws `NotImplementedException`, or the package is not referenced yet);
**GUARD** — passes before implementation and must stay green.

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | Installation built as a service has the Windows service lifetime | Host integration | H | `AsAService_TheHostRunsWithTheWindowsServiceLifetime(Installation)` | `IHostLifetime` is `WindowsServiceLifetime` | RED |
| AC-001 | Installation service content root is the application folder | Host integration | H | `AsAService_TheContentRootIsTheApplicationFolder(Installation)` | content root = `AppContext.BaseDirectory` | RED |
| AC-001 | Installation service reads the same configuration sources | Host integration | H | `AsAService_TheConfigurationSourcesAreThoseOfAConsoleStart(Installation)` | same JSON / env / command-line sources as a console build | RED |
| AC-001 | Started by the SCM: "running", public endpoint and private port answer, sync service runs | Manual | M | FR-009 steps 1–3, 5 | recorded with steps and outcome | manual |
| AC-002 | Control Plane built as a service has the Windows service lifetime | Host integration | H | `AsAService_TheHostRunsWithTheWindowsServiceLifetime(ControlPlane)` | `IHostLifetime` is `WindowsServiceLifetime` | RED |
| AC-002 | Control Plane service content root is the application folder | Host integration | H | `AsAService_TheContentRootIsTheApplicationFolder(ControlPlane)` | content root = `AppContext.BaseDirectory` | RED |
| AC-002 | Control Plane service reads the same configuration sources | Host integration | H | `AsAService_TheConfigurationSourcesAreThoseOfAConsoleStart(ControlPlane)` | same sources as a console build | RED |
| AC-002 | Started by the SCM: "running", HTTPS endpoint answers | Manual | M | FR-009 steps 1–3, 5 | recorded with steps and outcome | manual |
| AC-003 | Service stop / Windows shutdown: "stopped", stop event in the log file | Manual | M | FR-009 step 4 | recorded with steps and outcome | manual |
| AC-003 | Host stop cancels the schedule, starts no new run | Regression | R | `HostStop_StartsNoNewRun` | no run after stop | GUARD |
| AC-003 | Host stop is not recorded as a failed run | Regression | R | `HostStop_IsNotRecordedAsAFailedRun` | no failure record | GUARD |
| AC-004 | Console build keeps the console lifetime | Host integration | H | `FromTheConsole_TheHostKeepsTheConsoleLifetime` (both) | `ConsoleLifetime` | RED |
| AC-004 | Console build's content root is the working directory | Host integration | H | `FromTheConsole_TheContentRootIsTheWorkingDirectory` (both) | content root = working directory | RED |
| AC-004 | Console build still honours `--contentRoot` | Host integration | H | `FromTheConsole_AContentRootArgumentIsHonouredAsBefore` (both) | content root = given folder | RED |
| AC-004 | Everything else unchanged | Regression | — | the whole existing suite (`dotnet test ClassroomAgent.sln`) | green | GUARD |
| AC-005 | As a service with no Owner, the code goes to standard output and to no log or key file | Host integration + PostgreSQL | S | `AsAServiceWithoutOwner_TheSetupCodeReachesStandardOutputOnly` | code captured from stdout; absent (with and without hyphens) from every log and key file; probe event present | RED |
| AC-005 | As a service, the operator console is unchanged and no logging destination is added | Host integration | S | `AsAService_TheControlPlaneAddsNoLoggingDestinationForTheSetupCode` | `StandardOutputOperatorConsole`; Serilog factory; providers = console build's | RED |
| AC-005 | Console start: code on the operator console, never in the log | Regression | — | existing `ControlPlane.Services.SetupCodeStartupTests` | green | GUARD |
| AC-006 | Console: relative log directory resolves against the content root, not the working directory | Host integration | D | `FromTheConsole_ARelativeLogDirectory_ResolvesAgainstTheContentRoot` (both) | probe in `<contentRoot>/<rel>`; nothing in `<cwd>/<rel>` | RED |
| AC-006 | Console: relative key directory resolves against the content root | Host integration | D | `FromTheConsole_ARelativeKeyDirectory_ResolvesAgainstTheContentRoot` (both) | `key-*.xml` in `<contentRoot>/<rel>`; nothing in `<cwd>/<rel>` | RED |
| AC-006 | Service: relative log and key directories land in the application folder | Host integration | D | `AsAService_RelativeDirectories_ResolveIntoTheApplicationFolder` (both) | probe and key under `AppContext.BaseDirectory/<rel>` | RED |
| AC-006 | Absolute directories are used as given | Host integration | D | `AbsoluteDirectories_AreUsedAsGiven` (both hosts × both modes) | probe and key in the given folders | RED |
| AC-006 | Installation refusal record goes to the resolved log directory | Host integration | D | `FromTheConsole_TheInstallationRefusalRecord_GoesToTheResolvedLogDirectory` | `InstallationSettingException`; record naming the setting in `<contentRoot>/<rel>` | RED |
| AC-006 | Files written into the application folder, not the system folder, under the SCM | Manual | M | FR-009 step 5 | recorded | manual |
| AC-007 | As a service, logging is Serilog alone and no provider is added | Host integration | H | `AsAService_LoggingGoesThroughSerilogAlone_AndNoProviderIsAdded` (both) | Serilog factory in both modes; provider list = console build's | RED |
| AC-007 | As a service, log events reach the rolling file | Host integration | H | `AsAService_LogEventsGoToTheRollingFile` (both) | one `installation-*.log` / `control-plane-*.log` with the probe | RED |
| AC-008 | Each production project's packages are exactly the approved set | Architecture | P | `ProjectPackages_AreExactlyTheApprovedOnes` (Web, ControlPlane) | the WindowsServices package added to the hosts, nothing else | RED |
| AC-008 | The package is in no other project | Architecture | P | `ProjectPackages_AreExactlyTheApprovedOnes` (Domain, Contracts, Application, Infrastructure) | unchanged package sets | GUARD |
| AC-008 | The package follows the .NET 10 line | Architecture | P | `WindowsServicesPackage_FollowsTheDotNet10Line` (both) | version starts with `10.` | RED |

Every Acceptance Criterion has at least one automated test; AC-001 … AC-003
additionally carry the manual verification OD-001 a) requires.
