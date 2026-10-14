---
artifact_type: test_strategy
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T12:43:32Z
updated_at: 2026-10-14T12:43:32Z
produced_by: test-writer
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
supersedes: null
---

# US-047 Test Strategy — Run the Control Plane and the installation as Windows services

## 1. Scope

The two host projects' start-up wiring (spec FR-001 … FR-008): whether a host
built "as a service" gets the Windows service lifetime and the application
folder as content root, that a host built otherwise is unchanged, how relative
and absolute log / key directories resolve, where the Control Plane's setup code
and every log event go, and which projects reference the new package.

API_DESIGN and DB_DESIGN are NOT_APPLICABLE: no endpoint, contract, entity or
migration — so no contract, authorization, antiforgery or persistence test is
added (TC-3, TC-5 have nothing new to cover; the existing ones guard AC-004).

Starting under the real Service Control Manager (AC-001 … AC-003: "running",
"stopped", Windows shutdown, endpoints answering) is the **manual** verification
of OD-001 a), recorded by IMPLEMENTATION in the `implementation_report`
(spec FR-009). The automated tests below cover everything that runs without it.

## 2. The seam (spec I-1, OD-003 a)

Both hosts expose `Program.BuildApplication(string[] args, bool runsAsService)`,
which builds the host without running it. `runsAsService` is the FR-001
detection result passed as a parameter — never a setting, variable or argument
(VR-002). A compile-only skeleton (throws `NotImplementedException`) was created
at TEST_WRITING under OD-003 a); IMPLEMENTATION completes it and makes the entry
points call it with `WindowsServiceHelpers.IsWindowsService()`.

Tests build the host and inspect it **without starting it**: the Windows service
lifetime cannot start outside a Service Control Manager. Configuration travels
as command-line arguments (`--Key=value`), the way a console start receives it;
the environment is `Test` (no console sink, DC-10). No database is reached at
build, except in the AC-005 case below (real PostgreSQL via Testcontainers,
TC-2). No Google API or Control Plane is called (TC-4).

## 3. Test levels

| Level | Classes | What |
|---|---|---|
| Host integration (built, not started) | `Hosting.WindowsServiceHostingTests` | lifetime, content root, configuration sources, logging per mode, both hosts |
| Host integration | `Hosting.ConfiguredDirectoryResolutionTests` | relative / absolute log and key directories, both hosts, both modes; installation refusal record |
| Host integration + PostgreSQL | `Hosting.SetupCodeServiceModeTests` | setup code destinations as a service (AC-005) |
| Architecture | `Architecture.WindowsServicePackageTests` | package placement and the exact package set (AC-008) |
| Regression | the whole existing suite | AC-004; `SyncScheduleTests` host-stop tests for AC-003's cancellation |

## 4. Scenarios

**Positive**
- Built as a service, each host's `IHostLifetime` is `WindowsServiceLifetime`
  (FR-002) and its content root is the application folder (FR-003).
- As a service, a log event lands in the host's rolling file (FR-007, DC-10).
- As a service, relative log and key directories resolve into the application
  folder (FR-004).
- Absolute directories are used as given, in both modes and both hosts (VR-001).
- From the console, a relative directory resolves against the content root —
  shown with a content root distinct from the working directory
  (`--contentRoot`), so the test tells the two apart (FR-004).

**Negative**
- From the console, the lifetime stays `ConsoleLifetime` and the content root
  stays the working directory (FR-002, FR-003, I-2, AC-004).
- A relative directory leaves nothing under the working directory when the
  content root differs (FR-004).
- As a service, the logger factory is Serilog's and the registered logging
  providers are exactly those of a console build — the package's Event Log
  provider is not added (FR-007, I-4, AC-007).
- As a service with no Owner, the setup code reaches standard output and appears
  in no log file and no key file; the operator console is still
  `StandardOutputOperatorConsole` (FR-006, AC-005).
- The package is in no project but the two hosts, and no other package is added
  anywhere (FR-008, AC-008).

**Boundary**
- Console build with an explicit `--contentRoot`: still honoured, as before
  (AC-004).
- The installation's refusal record — written before the host exists — goes to
  the resolved relative log directory (FR-004, last sentence).

**Validation (VR-001)** — absolute vs relative values as above. Missing
required settings keep their existing refusal tests
(`Web.Configuration.InstallationConfigurationTests` and the Control Plane
settings tests).

**Security** — setup code never in a file or the Event Log (SC-10, §9 v35/v92);
no new log destination (DC-10, SC-10); key ring never silently in the working
directory (SC-7, FR-004).

**Persistence** — none (DB_DESIGN NOT_APPLICABLE). The AC-005 test only migrates
a fresh Control Plane database so the Owner check of `SetupCodeStartup` runs.

## 5. Fixtures

- `TestInfrastructure.HostBuildArguments` — required settings of each host as
  arguments; a connection string that is never opened.
- `TestInfrastructure.HostFolders` — a private temporary root and a unique
  relative directory name; disposal removes it under the temporary root, the
  application folder and the working directory.
- `TestInfrastructure.BuiltHost` / `BuiltHostKind` — builds either host through
  `BuildApplication` and reads lifetime, content root, logging, configuration
  sources; writes a probe log event; creates a Data Protection key.
- `ControlPlaneTestHost.MigrateAsync` made public for the AC-005 test.
- `PostgreSqlFixture` (assembly fixture) for AC-005.

## 6. Excluded scenarios

- Registering / starting / stopping a real Windows service, Windows shutdown,
  "running" / "stopped" states, endpoints answering under the SCM — manual,
  OD-001 a), FR-009.
- Secrets as the service account's environment variables (OD-002) — operational,
  FR-009 step 3.
- The installation's default log directory when `LogFile:Directory` is unset —
  unchanged code path (`<application folder>/logs`); testing it would write into
  the test output folder shared by every test.
- Drive-relative (`C:logs`) and root-relative (`\logs`) values (I-5): the
  Specification says they are not used as given but does not fix the exact
  resulting folder; any test would assert an invented location. See §7.
- Shutdown timeout (I-6) — no new behaviour.

## 7. Known limitations

- **Service content root vs working directory.** Under `dotnet test` the
  working directory is the test output folder, which is also
  `AppContext.BaseDirectory`. So `AsAService_TheContentRootIsTheApplicationFolder`
  and `AsAService_RelativeDirectories_ResolveIntoTheApplicationFolder` would
  also pass if a service used the working directory. The resolution rule itself
  is proved distinctly by the console tests with a separate content root; that a
  service's content root is the application folder is additionally checked by
  hand (FR-009 step 5). Changing the process working directory in a test is
  not done: it is process-wide and tests run in parallel.
- **AC-005 captures standard output** process-wide for the duration of one
  `StartAsync` call. No other test writes to standard output (every other
  Control Plane test substitutes the operator console; the console log sink is
  Development-only).
- **I-5** exact resolution of `C:logs` / `\logs` is untested (see §6) — reported
  as a non-blocking finding.

## 8. Open Decisions affecting testing

- OD-001 a) — resolved; drives the manual / automated split.
- OD-003 a) — raised and resolved at TEST_WRITING (Owner, 2026-10-14): the
  compile-only skeleton of `BuildApplication`.
- None open.
