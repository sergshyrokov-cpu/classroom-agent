---
artifact_type: specification
story: US-047
version: 1
status: APPROVED
created_at: 2026-10-14T12:19:55Z
updated_at: 2026-10-14T12:24:36Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-047-windows-service.md
    version: null
  - path: trebovaniya.md
    version: 92
  - path: docs/decisions/US-047-open-decisions.md
    version: 1
supersedes: null
---

# US-047 Specification — Run the Control Plane and the installation as Windows services

## 1. Overview

On the Owner's Windows server the Control Plane and every installation run as
**Windows services** (`trebovaniya.md` §9 v92, "Запуск на сервере Владельца";
`deployment-conventions.md` DC-14):

- they start with the server, before anyone signs in, independent of who is
  signed in to Windows;
- after a failure Windows restarts the service — a recovery setting chosen at
  deployment, not application code;
- stopping the service is an ordinary application shutdown: a running
  synchronization is cancelled the same way as on any other stop.

The application detects by itself whether it runs as a service; outside one —
development, tests, the Control Plane's first-run setup — it runs as an ordinary
console application. The mechanism is the Microsoft package
`Microsoft.Extensions.Hosting.WindowsServices`, approved by v92. Linux,
containers and systemd are not defined (§9 v92).

The Story changes only the two host projects, `ClassroomAgent.Web` and
`ClassroomAgent.ControlPlane`: their start-up wiring and how they resolve a
relative directory from configuration. No endpoint, request, response, status
code, entity, table or migration changes.

What the current code does, which this Specification must not contradict
(facts gathered from the repository at the time of writing):

- `ClassroomAgent.Web/Program.cs` and `ClassroomAgent.ControlPlane/Program.cs`
  both call `WebApplication.CreateBuilder(args)` with no options, so the content
  root is the process working directory.
- Installation: `InstallationLogging.Directory` returns `LogFile:Directory` as
  configured, or `<application folder>/logs` (`AppContext.BaseDirectory`) when
  unset. `InstallationSettingsReader` reads `DataProtection:KeyDirectory`
  (required) and creates the directory as given. A relative value of either is
  resolved by the OS against the process working directory.
- Control Plane: `LogFile:Directory` and `DataProtection:KeyDirectory` are both
  required settings, used as given (Serilog file sink;
  `PersistKeysToFileSystem`), so a relative value resolves against the working
  directory.
- Both hosts log through Serilog (`AddSerilog`) to a rolling file (DC-10); the
  console sink is added only in Development.
- Control Plane first-run setup: `SetupCodeStartup` generates the one-time code
  when no Owner account exists and writes it through `IOperatorConsole`, whose
  implementation `StandardOutputOperatorConsole` writes to standard output —
  never to the log (US-001 FR-002, SC-10).
- Secrets reach the installation as environment variables through
  `EnvironmentSecretStore` (US-008 OD-004).

## 2. Business Goal

The pilot server is the Owner's own Windows PC, where power cuts happen. Without
a service, a school stays offline after every restart until the Owner signs in
and starts both programs by hand, and closing a console window or signing out
stops the school (`trebovaniya.md` §9 v92, Story Business Value). With services,
the school and the Control Plane come back by themselves.

## 3. Business Flow

Deployment (operational, DC-2 and DC-14; not code of this Story):

1. The Owner publishes both applications to their folders on the server.
2. **Control Plane first-run setup comes before its service registration**
   (§9 v92, DC-2 step 2): the Owner starts the Control Plane from a console on
   the server, reads the one-time code there, creates the Owner account, stops
   the program.
3. The Owner registers the Control Plane and the installation as services
   (service name, dedicated local account per service — OD-002 a), automatic
   start, recovery "restart"), and places the secrets as that account's own
   environment variables (OD-002 a; fallback b).
4. From now on Windows starts both services at boot, restarts them after a
   failure and stops them at shutdown.

Runtime:

- Started by the Service Control Manager → the application runs with the
  Windows service lifetime: it reports "running" once started and "stopped"
  after an ordinary host shutdown.
- Started any other way (console, `dotnet run`, test host) → it runs exactly as
  before this Story.

## 4. Functional Requirements

**FR-001 Service detection.** Each host decides once, at start-up, whether the
process was started by the Windows Service Control Manager, using the detection
of `Microsoft.Extensions.Hosting.WindowsServices`. There is no configuration
key, command-line switch or environment variable that selects the mode (Story
Scope: "detected by the application, no setting"). (AC-001, AC-002, AC-004)

**FR-002 Service lifetime.** When FR-001 says "service", the host runs with the
Windows service lifetime of that package, so the Service Control Manager sees
"running" after start-up and "stopped" after shutdown. When FR-001 says "not a
service", the host lifetime, console output and configuration sources are
exactly those of today. (AC-001, AC-002, AC-004)

**FR-003 Content root of a service.** When FR-001 says "service", the host's
content root is the application's folder (the folder holding the published
application, `AppContext.BaseDirectory`), not the process working directory —
a service starts in the Windows system folder. Configuration files that are
read from the content root (`appsettings.json`,
`appsettings.{Environment}.json`) are therefore found in the application's
folder. When FR-001 says "not a service", the content root is unchanged
(today: the working directory). (AC-001, AC-002, AC-004, AC-006)

**FR-004 Relative directories resolve against the content root.** In both hosts,
a relative value of `LogFile:Directory` or `DataProtection:KeyDirectory` is
resolved against the host's content root (FR-003); an absolute value is used as
given. The installation's default log directory when `LogFile:Directory` is
unset stays `<application folder>/logs`. Outside a service the content root is
the working directory, so a relative value resolves to the same place as before
(AC-004). The resolved directory is what the existing rules then apply to —
VR-003 of US-008 (create if absent, refuse a file in its place) for the
installation key directory, and the log-file rules of DC-10. The installation's
refusal-to-start record (written before the host exists, US-005 FR-001) goes to
the same resolved log directory. (AC-006)

**FR-005 Stop is an ordinary shutdown.** Stopping the service, or Windows
shutting down, triggers the same host shutdown as Ctrl+C from the console: the
host's stopping token is signalled, a running synchronization is cancelled
through its `CancellationToken` as it is today, and the application writes its
ordinary stop event (the `Microsoft.Hosting.Lifetime` information events
already enabled by DC-10) to its log file. No new shutdown code path is added.
(AC-003)

**FR-006 The setup code stays on the console.** The Control Plane's one-time
setup code continues to be written only through `IOperatorConsole` to standard
output. This Story adds no other destination for it: not the log file, not the
Windows Event Log, not any other file. A service has no console, so its
standard output reaches no one; the first-run setup can be completed only from
a console start (§9 v92, DC-2 step 2). The Control Plane's behaviour when
started as a service with no Owner account is otherwise unchanged (see I-3).
(AC-005)

**FR-007 Logs go only where DC-10 says.** Running as a service, each host logs
only to its rolling log file under DC-10 and SC-10. The application adds no
Windows Event Log provider or sink of its own, and any Event Log logging
provider that the Windows-service package registers by default must not receive
the application's log events (see I-4). What the Service Control Manager itself
records about services starting and stopping is outside the application.
(AC-007)

**FR-008 Package placement.** `Microsoft.Extensions.Hosting.WindowsServices` is
referenced by `ClassroomAgent.Web` and `ClassroomAgent.ControlPlane` only —
never by `Domain`, `Application`, `Infrastructure` or `Contracts` (AD-3) — and
no other package is added (AGENTS.md: adding a package needs approval; this one
is approved by v92). Its version follows the .NET 10 package line used by the
other `Microsoft.*` references of the solution. (AC-008)

**FR-009 Manual verification of AC-001 … AC-003 (OD-001 a).** The
`implementation_report` records a one-time manual verification on a Windows
machine, with its steps and outcome, for each host:

1. register the service (DC-14, the Story's Notes give the `sc.exe` commands;
   a dedicated local account per service — OD-002 a);
2. start it; confirm the Service Control Manager reports "running"; for the
   installation, the public endpoint and the private port answer and the
   synchronization background service runs; for the Control Plane, its HTTPS
   endpoint answers;
3. confirm the service saw the secrets placed as its account's environment
   variables; if not, record the fallback to OD-002 b) that was used;
4. stop the service; confirm "stopped" and the ordinary stop event in the log
   file; repeat with a Windows shutdown;
5. confirm the log file and the Data Protection keys were written into the
   application's folder (or the configured absolute path), not into the Windows
   system folder.

## 5. Acceptance Criteria

Carried from the Story with the same ids.

- **AC-001 The installation runs as a Windows service.** Given
  `ClassroomAgent.Web` registered as a Windows service with a valid
  configuration, when the Service Control Manager starts it, then it reports
  "running", serves its public endpoint and its private port as it does from
  the console, and its background synchronization service runs.
- **AC-002 The Control Plane runs as a Windows service.** Given
  `ClassroomAgent.ControlPlane` registered as a Windows service with a valid
  configuration and an existing Owner account, when the Service Control Manager
  starts it, then it reports "running" and serves its HTTPS endpoint as it does
  from the console.
- **AC-003 Stopping the service is an ordinary shutdown.** Given either
  application running as a service, when the service is stopped or Windows shuts
  down, then the host shuts down the same way as on Ctrl+C: a running
  synchronization is cancelled through its cancellation token, the application
  writes its ordinary stop event to its log file (DC-10), and the service
  reports "stopped".
- **AC-004 Outside a service nothing changes.** Given either application
  started from a console, by `dotnet run` or by the test host, when it runs,
  then it behaves exactly as before: same lifetime, same console output, same
  configuration sources; the existing test suite stays green.
- **AC-005 The setup code never leaves the console.** Given the Control Plane
  with no Owner account yet, started as a service, when it starts, then the
  one-time setup code is not written to the log file, the Windows Event Log or
  any other file; the first-run setup can be completed only from a console
  start.
- **AC-006 Relative paths stay in the application's folder.** Given a relative
  log directory or Data Protection key directory, when the application runs as
  a service, then the path resolves against the content root — the
  application's folder — and outside a service it resolves the same way as
  before; absolute paths are used as given.
- **AC-007 Logs go only where DC-10 says.** Given either application running as
  a service, when it writes log events, then they go to its rolling log file
  under DC-10 and SC-10; the application adds no Windows Event Log provider of
  its own.
- **AC-008 The package stays in the hosts.** Given the solution, when it is
  built, then `Microsoft.Extensions.Hosting.WindowsServices` is referenced only
  by the two host projects and no other package is added.

## 6. Validation Rules

No new user input, request body, query or route parameter. Configuration input
only:

- **VR-001** `LogFile:Directory`, `DataProtection:KeyDirectory`: a value is
  either absolute (used as given) or relative (resolved per FR-004). Whether a
  value is absolute is decided by the platform's own path rules
  (`Path.IsPathRooted`/`Path.IsPathFullyQualified` semantics on Windows; a
  drive-relative value such as `C:logs` or a root-relative value such as
  `\logs` is **not** fully qualified and is resolved against the content root
  like any relative value — see I-5). Existing rules on these settings stay:
  the Control Plane refuses to start when either is missing or empty; the
  installation refuses to start when `DataProtection:KeyDirectory` is missing,
  empty, a file, or cannot be created (US-008 VR-003), and uses the default log
  directory when `LogFile:Directory` is unset. The refusal message names the
  setting and the rule, never the value (SC-10).
- **VR-002** Service detection takes no input (FR-001). An environment variable
  or argument cannot force service mode.

Test approach (OD-001 a): automated tests cover FR-001 … FR-008 for everything
that runs without the Service Control Manager — that a host started as "a
service" is wired with the Windows service lifetime and the application-folder
content root, that a host started otherwise is unchanged, path resolution,
setup-code destinations, log destinations and package placement. Driving the
"service" branch in a test needs a seam for the detection result that is not a
configuration key, environment variable or argument (VR-002) — see I-1.

## 7. Security Requirements

- **Setup code (SC-10, §9 v35/v92).** FR-006: never in a log, the Event Log or a
  file. The Story must not add a sink that could carry standard output into a
  file (for example redirecting a service's standard output).
- **Logs (DC-10, SC-10).** FR-007: the rolling file only; no new destination for
  log events. The Windows Event Log is readable by more accounts on the server
  than the application's log folder, which is a further reason it receives
  nothing.
- **Secrets (SC-7, OD-002 a).** Unchanged mechanism: environment variables read
  by `EnvironmentSecretStore`. Each service runs under its own dedicated local
  account, whose own environment variables hold the secrets; machine-wide
  variables are not used (OD-002 c rejected). The service-account key is never
  in the installation database or the UI (Hard Stops). No secret value is
  written by this Story to a log, the Event Log, a file or the registry by code;
  the registry fallback OD-002 b) is a manual deployment step.
- **Data Protection keys (SC-7, US-008 S-18).** FR-004 keeps the key ring in the
  configured directory, resolved into the application's folder for a relative
  value — never silently into the Windows system folder. Restricting the key
  directory's access to the service account and administrators is a deployment
  step (DC-14), not code.
- **Network exposure** is unchanged: the same endpoints, addresses and ports as
  from the console (§9 v92: on one server the private channel and the Control
  Plane listen on `127.0.0.1` only — deployment configuration).
- **No new outbound destination** (SC-13).
- **Authorization, audit, read-only mode**: unaffected; no endpoint changes.

## 8. Error Handling

- A host that cannot start as a service (for example a missing required
  setting) refuses to start exactly as from the console: the existing refusal
  is written to the log file (resolved per FR-004) and the process exits with
  an error; the Service Control Manager then applies the recovery setting chosen
  at deployment. No new error behaviour.
- An unhandled exception during start-up or run follows today's handling; the
  application does not write it to the Event Log (FR-007).
- Shutdown that exceeds the host's shutdown timeout behaves as today (the host
  default); this Story sets no new timeout (see I-6).

## 9. Non-Functional Requirements

- NFR-062: .NET 10; the package's .NET 10 line.
- AD-3: the package is a host concern only (FR-008).
- AC-004: zero behavioural change outside a service; the existing test suite
  stays green.
- `TreatWarningsAsErrors` holds; no warning from the new wiring.

## 10. Out of Scope

- Registering, configuring or removing the services (name, account, recovery,
  start type) — DC-14, an operational step; the Story's Notes record the
  commands.
- HTTPS certificates, reverse proxies, firewall, backups.
- Linux, systemd, containers (§9 v92: not defined).
- Any change to the first-run setup flow beyond where its code may and may not
  appear (FR-006).
- A Windows Event Log source, a service installer in code, a "install as
  service" command-line verb.
- Any endpoint, entity, migration, translation entry or UI change.

## 11. Open Decisions

Both carried from the Story and resolved by the Owner on 2026-10-14
(`docs/decisions/US-047-open-decisions.md`):

- **OD-001 a)** — AC-001 … AC-003 verified once by hand, recorded in the
  `implementation_report` (FR-009); everything else automated. Impact: §6 test
  approach, FR-009, traceability.
- **OD-002 a)** — a dedicated local Windows account per service; secrets as
  that account's own environment variables; fallback b) (the service's registry
  `Environment` value, access narrowed) if the service does not see them.
  Impact: operational only; FR-009 step 3; §7.

No new Open Decision. `trebovaniya.md` §7 (v92) has no open item this Story
depends on.

### Interpretations for the human gate

- **I-1 Test seam for service detection.** OD-001 a) requires an automated test
  that "the host is wired for the Windows service lifetime"; a test process is
  never a service. The detection result (FR-001) is therefore obtained through
  one point that the test project can substitute (for example an internal
  start-up method that takes the detection result as a parameter), which is
  **not** a configuration key, environment variable or command-line argument
  (VR-002). The exact shape is decided at TEST_WRITING / IMPLEMENTATION.
- **I-2 Content root only in service mode.** FR-003 changes the content root only
  when running as a service, so that a console start, `dotnet run` and the test
  host keep today's content root (AC-004). A uniform "always the application
  folder" rule was rejected: it would change where a console start reads
  `appsettings.json` and resolves relative paths.
- **I-3 Control Plane started as a service with no Owner.** §9 v92 says setup is
  done from the console before registration; it does not say the Control Plane
  must refuse to start as a service without an Owner account. The Specification
  keeps today's behaviour (the code is generated in memory and written to a
  standard output that reaches no one; the setup page stays the only page
  served) and adds no refusal and no log hint. A log line saying "setup pending,
  start from the console" would be harmless (no code in it) but is not required
  by any artifact — the gate may ask for it.
- **I-4 The package's default Event Log provider.** The Windows-service
  integration of `Microsoft.Extensions.Hosting.WindowsServices` may register a
  Windows Event Log logging provider when running as a service. Both hosts
  replace the logger factory with Serilog (`AddSerilog`), which by default does
  not forward to other providers; FR-007 requires that this stays true and that
  a test shows a service-mode host has no Event Log destination for application
  events.
- **I-5 Drive- and root-relative paths.** VR-001 treats `C:logs` and `\logs` as
  relative to the content root, not as absolute, because their meaning depends
  on the process's current drive and directory — exactly the dependency AC-006
  removes. Outside a service, with the content root equal to the working
  directory, a value like `logs` or `.\logs` resolves to the same place as
  before; `C:logs` / `\logs` could differ only if the working directory is on
  another drive — judged negligible.
- **I-6 Shutdown time.** Neither `trebovaniya.md` nor DC-14 sets a shutdown
  time budget; Windows grants services a limited time at system shutdown. The
  Story keeps the host's default shutdown timeout. If the manual verification
  (FR-009 step 4) shows a synchronization not stopping in time, that is a new
  decision, not part of this Story.

## 12. Traceability

| AC | Functional requirement / rule | Verified by |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004, FR-009 | manual (OD-001 a) + automated: service-mode wiring of the installation host (I-1) |
| AC-002 | FR-001, FR-002, FR-003, FR-004, FR-009 | manual (OD-001 a) + automated: service-mode wiring of the Control Plane host (I-1) |
| AC-003 | FR-005, FR-009 | manual (OD-001 a); existing cancellation tests of synchronization stay green |
| AC-004 | FR-001, FR-002, FR-003, FR-004, I-2 | automated: console-mode host unchanged; full existing suite green |
| AC-005 | FR-006, §7 | automated: setup code only through `IOperatorConsole`, never in the log file or an Event Log destination |
| AC-006 | FR-003, FR-004, VR-001, I-5 | automated: relative / absolute / unset path resolution in both hosts, both modes |
| AC-007 | FR-007, I-4 | automated: service-mode host's log destinations are the rolling file only |
| AC-008 | FR-008 | automated: project references of the six production projects |

API_DESIGN is expected NOT_APPLICABLE (no endpoint, request or status code
change); DB_DESIGN is expected NOT_APPLICABLE (no persistence change).

Delegation: none — the repository facts in §1 were gathered directly with
read-only searches.
