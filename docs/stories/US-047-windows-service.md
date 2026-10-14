---
id: US-047
epic: EPIC-8
title: Run the Control Plane and the installation as Windows services
slug: windows-service
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v92.
---

# User Story

As the **Owner** who hosts the Control Plane and a school's installation on a
Windows server

I want both applications to run as Windows services

So that they start with the server after a power cut, without anyone signing in,
come back by themselves after a crash, and stop cleanly when the server shuts
down.

---

# Business Value

`trebovaniya.md` §9 (v92), `deployment-conventions.md` DC-14: the pilot server
is the Owner's own Windows PC, where sudden power cuts happen. Without running
as a service, a school is offline after every restart until the Owner signs in
and starts both programs by hand; with a console window, closing it or signing
out stops the school. v92 approves the Microsoft package
`Microsoft.Extensions.Hosting.WindowsServices` for this.

---

# Scope

**In scope:**

- `ClassroomAgent.Web` and `ClassroomAgent.ControlPlane` run as Windows services
  when the Windows Service Control Manager starts them, and as ordinary console
  applications otherwise (development, tests, the Control Plane's first-run
  setup) — detected by the application, no setting;
- stopping the service is an ordinary host shutdown, the same path as Ctrl+C
  today;
- relative paths in configuration (log directory, Data Protection key
  directory) keep pointing into the application's own folder when the process
  runs as a service, whose working directory is the Windows system folder;
- the package reference in the two host projects only (AD-3);
- tests for everything that does not need a real Service Control Manager.

**Out of scope:**

- registering, configuring or removing the services (service name, account,
  recovery settings, start type) — an operational step at deployment, DC-14;
  the Story only records the commands in its notes;
- HTTPS certificates, reverse proxies, firewall, backups;
- Linux, systemd or containers (`trebovaniya.md` §9, v92: not defined);
- any change to the first-run setup flow other than where its code may and may
  not appear (AC-005).

---

# Acceptance Criteria

## AC-001 The installation runs as a Windows service

**Given** `ClassroomAgent.Web` registered as a Windows service with a valid
configuration

**When** the Service Control Manager starts it

**Then** it reports "running", serves its public endpoint and its private port
as it does from the console, and its background synchronization service runs.

## AC-002 The Control Plane runs as a Windows service

**Given** `ClassroomAgent.ControlPlane` registered as a Windows service with a
valid configuration and an existing Owner account

**When** the Service Control Manager starts it

**Then** it reports "running" and serves its HTTPS endpoint as it does from the
console.

## AC-003 Stopping the service is an ordinary shutdown

**Given** either application running as a service

**When** the service is stopped, or Windows shuts down

**Then** the host shuts down the same way as on Ctrl+C from the console: a
running synchronization is cancelled through its cancellation token, the
application writes its ordinary stop event to its log file (DC-10), and the
service reports "stopped".

## AC-004 Outside a service nothing changes

**Given** either application started from a console, by `dotnet run` or by the
test host

**When** it runs

**Then** it behaves exactly as before this Story: same lifetime, same console
output, same configuration sources; the existing test suite stays green.

## AC-005 The setup code never leaves the console

**Given** the Control Plane with no Owner account yet, started as a service

**When** it starts

**Then** the one-time setup code is not written to the log file, to the Windows
Event Log or to any other file (`trebovaniya.md` §9, v35, v92); the first-run
setup can be completed only from a console start, as DC-2 step 2 describes.

## AC-006 Relative paths stay in the application's folder

**Given** a relative log directory or Data Protection key directory in
configuration

**When** the application runs as a service, whose working directory is the
Windows system folder

**Then** the path resolves against the application's content root — the folder
the application is installed in — not against the process working directory,
and outside a service it resolves the same way as before (AC-004). Absolute
paths are used as given.

## AC-007 Logs go only where DC-10 says

**Given** either application running as a service

**When** it writes log events

**Then** they go to its rolling log file under the rules of DC-10 and SC-10; the
application adds no Windows Event Log provider of its own. What the Service
Control Manager itself records about starting and stopping services is outside
the application.

## AC-008 The package stays in the hosts

**Given** the solution

**When** it is built

**Then** `Microsoft.Extensions.Hosting.WindowsServices` is referenced only by
`ClassroomAgent.Web` and `ClassroomAgent.ControlPlane` — never by `Domain`,
`Application`, `Infrastructure` or `Contracts` — and no other package is added.

---

# Open Decisions

## OD-001 How AC-001 … AC-003 are verified

An automated test cannot register a real Windows service: that needs
administrator rights and a real Service Control Manager, which the test suite
does not have, and the Definition of Done asks every AC to map to a passing test.

- a) **(Recommended)** Automated tests cover what can run without the Service
  Control Manager — the host is wired for the Windows service lifetime, AC-004,
  AC-005, AC-006, AC-007 and AC-008. AC-001 … AC-003 are checked once by hand on
  a Windows machine (install, start, stop, Windows shutdown), and the result is
  recorded in the `implementation_report` as a manual verification with its
  steps.
- b) An automated test that installs a real service, skipped when not run as
  administrator. Rejected in spirit: the Definition of Done forbids skipped
  tests.

**Resolution (Owner, 2026-10-14): a)** — automated tests for everything that runs
without the Service Control Manager; AC-001 … AC-003 verified once by hand and
recorded in the `implementation_report` with their steps.

## OD-002 Where a service's environment variables are set

Secrets reach the application as environment variables (`EnvironmentSecretStore`,
US-008 OD-004): the service-account key and the OAuth client secret. On the pilot
server the Owner also works daily under a standard Windows account, which must
not be able to read them (`trebovaniya.md` §9, SC-7).

- a) **(Recommended)** Each service runs under its own dedicated local Windows
  account, and the secrets are that account's own environment variables —
  readable by that account and administrators only. To be confirmed on the
  pilot server during this Story: that a service started under that account
  sees them; if it does not, fall back to b).
- b) The service's own `Environment` value in the registry
  (`HKLM\SYSTEM\CurrentControlSet\Services\<name>`), with that key's access
  narrowed to administrators, SYSTEM and the service account.
- c) Machine-wide environment variables. Rejected: every user of the server can
  read them.

**Resolution (Owner, 2026-10-14): a)** — a dedicated local account per service,
secrets as that account's own environment variables; confirmed on the pilot
server during this Story, falling back to b) if the service does not see them.

This decides an operational step, not code; it is recorded here because the
answer belongs in DC-14 and the Story's notes.

---

# Notes

- Expected stages: API_DESIGN is NOT_APPLICABLE (no endpoint, request or status
  code changes); DB_DESIGN is NOT_APPLICABLE (no persistence change).
- The one new package is approved by `trebovaniya.md` v92.
- Why AC-006 matters: a service starts with `C:\Windows\System32` as its working
  directory. Today a relative `LogFile:Directory` or `DataProtection:KeyDirectory`
  resolves against the working directory, so a service would write logs and keys
  into the Windows system folder or fail to start.
- Registration, for the record (DC-14, run as administrator, names are
  examples): `sc.exe create ClassroomAgent.ControlPlane binPath= "C:\ClassroomAgent\cp\ClassroomAgent.ControlPlane.exe" start= auto obj= ".\svc-classagent" password= ...`,
  then `sc.exe failure ClassroomAgent.ControlPlane reset= 86400 actions= restart/60000/restart/60000/restart/60000`;
  the same for the installation. The Control Plane is registered only after the
  Owner account exists (DC-2 step 2).
- Part of the pilot deployment; an exception to the frozen pilot scope, like
  US-046.
