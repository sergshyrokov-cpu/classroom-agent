---
artifact_type: security_review
story: US-047
version: 1
status: APPROVED
created_at: 2026-10-14T13:25:14Z
updated_at: 2026-10-14T13:25:14Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-047-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 92
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: PARTIAL
---

# Security Review — US-047 Run as a Windows service

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor findings; four Informational.

US-047 changes only host start-up: each host detects whether the Service Control
Manager started it, registers `WindowsServiceLifetime` in that case, uses the
application folder as content root as a service, and resolves relative log and
Data Protection key directories against the content root. No endpoint, policy,
schema, Google access, audit action or outbound destination changes.

Principal controls verified: logging stays Serilog-only (no Event Log provider
reaches application events — the package's `AddWindowsService` helper is
deliberately not used); the setup code still goes only through
`IOperatorConsole` to standard output and is proven absent from the log and key
directories in service mode; the Data Protection key ring cannot silently land in
the Windows system folder; detection takes no configurable input (VR-002); the
new package is approved by `trebovaniya.md` v92 / spec FR-008 and is confined to
the two hosts.

Limitation: the real service run (FR-009, AC-001…AC-003) is a pending manual
step on the pilot server; deployment-time controls (dedicated per-service
account, its own environment variables, key-directory ACL) are not code and
were not verified here.

## 2. Reviewed Artifacts

See front matter `inputs`. API and DB designs are `NOT_APPLICABLE` records; no
`openapi.yaml` and no entity model exist by design.

## 3. Security-Relevant Scope

- Hosts: `ClassroomAgent.Web` (installation) and `ClassroomAgent.ControlPlane`.
- Changed: `Program.cs` of both hosts (`BuildApplication(args, runsAsService)`),
  `Web/Configuration/WindowsServiceHosting.cs`, `ConfiguredDirectory.cs`,
  `InstallationLogging.Directory`, `InstallationSettingsReader.KeyDirectory`,
  both host `.csproj` (one package).
- Assets: Data Protection key ring (protects session cookies and the antiforgery
  token), the Control Plane one-time setup code, log files, secrets in
  environment variables read by `EnvironmentSecretStore`.
- Trust boundaries touched: process ↔ Windows SCM; process ↔ file system
  (log/key directories); log destinations. Network exposure unchanged.

## 4. Environment and Tools

- .NET SDK 10.0.401, Windows 10 Pro; Docker available (Testcontainers ran).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — total 5677, passed 5677, failed 0, skipped 0.
- `dotnet list … package --vulnerable --include-transitive` (Web, ControlPlane)
  — no vulnerable packages for api.nuget.org.
- `dotnet list … package --include-transitive` — new: 
  `Microsoft.Extensions.Hosting.WindowsServices` 10.0.12 and transitive
  `System.ServiceProcess.ServiceController` 10.0.12 in both hosts.
- Delegated (cheap-worker, read only): the build, test and package commands
  above. Code reading and judgement were not delegated.
- Not performed: running either host as a real Windows service (FR-009).

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed change |
| SC-2 Authentication | PASS | Identity/cookie/HTTPS wiring moved verbatim into `BuildApplication`; full suite green (5677/5677) |
| SC-3 AllowedAdmin | NOT_APPLICABLE | untouched |
| SC-4 Authorization | PASS | pipeline, fallback `{*path}` 404, global antiforgery filter unchanged in both hosts (diff is indentation only); existing endpoint-enumeration tests green |
| SC-5 Read-only mode | NOT_APPLICABLE | no use case change |
| SC-6 No DB UI | PASS | no endpoint added |
| SC-7 Key / DP keys | PASS | key reference mechanism unchanged; DP key directory resolves to content root (`ConfiguredDirectory.Resolve`, CP `ResolveDirectory`), never the system folder; `ConfiguredDirectoryResolutionTests` |
| SC-8 Google | NOT_APPLICABLE | untouched |
| SC-9 Channel | PASS | private port binding (`UseUrls` with `PrivateAddress`) unchanged |
| SC-10 Hygiene | PASS | logger factory is `SerilogLoggerFactory` in service mode, provider set identical to console mode (`AsAService_LoggingGoesThroughSerilogAlone_AndNoProviderIsAdded`); setup code absent from logs/keys (`AsAServiceWithoutOwner_TheSetupCodeReachesStandardOutputOnly`); refusal record carries setting + rule only |
| SC-11 Audit | NOT_APPLICABLE | no audited action added |
| SC-12 Owner | PASS | no `Contracts` change; no project reference added |
| SC-13 Outbound | PASS | no new destination; Event Log receives no application events |

## 6. Authentication and Authorization

No change. Both host pipelines are moved unchanged into `BuildApplication`; the
diff of the Control Plane `Program.cs` is re-indentation plus the builder,
directory resolution and `return app`. Allowed/forbidden-role and
anonymous-endpoint enumeration tests from earlier Stories run against the same
pipeline and pass.

## 7. Credentials, Key and Google Access

- Secrets: still `EnvironmentSecretStore`; the per-service account and its own
  environment variables (OD-002 a) are deployment steps.
- Service-account key: untouched; not written anywhere by this Story.
- Detection (`WindowsServiceHelpers.IsWindowsService()`) takes no setting,
  variable or argument (VR-002) — an attacker-controlled configuration cannot
  switch content root.
- Google: untouched.

## 8. Sensitive Data Exposure

- Logs: Serilog only, file sink only (DC-10). `AddSerilog` replaces the logger
  factory; providers registered by the default builder (including the Windows
  Event Log provider on Windows) receive nothing. `AddWindowsService` is not
  called, so the package adds no provider either (verified in code and by test).
- Setup code: `StandardOutputOperatorConsole` only; a service's standard output
  reaches no one (accepted by §9 v92, I-3).
- Responses, DTOs, exports, audit: unchanged.

## 9. Input Validation

Configuration input only. `KeyDirectory` wraps resolution failures into the
existing `InstallationSettingException` (setting + rule, no value). Absolute
values are used as given; drive-/root-relative values are anchored under the
content root (I-5), which narrows rather than widens where files can land.

## 10. API Security

No endpoint, method, route or response change (API design NOT_APPLICABLE).

## 11. Persistence and Configuration

No schema or migration change; both model snapshots unchanged (full suite
includes migration-drift tests). No new configuration key. No configuration
file in the repository sets the log or key directory.

## 12. Logging, Audit and Telemetry

See §8. No audit change. Hook telemetry untouched.

## 13. Dependencies

`Microsoft.Extensions.Hosting.WindowsServices` 10.0.12 (Microsoft, MIT),
approved by `trebovaniya.md` v92 and spec FR-008, referenced by the two hosts
only (`WindowsServicePackageTests`). Transitive
`System.ServiceProcess.ServiceController` 10.0.12. Vulnerability scan: none
reported. No project reference added.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| FR-006 setup code only on stdout (AC-005) | `SetupCodeServiceModeTests` (both) |
| FR-007 Serilog-only, no added provider (AC-007) | `WindowsServiceHostingTests.AsAService_LoggingGoesThroughSerilogAlone_AndNoProviderIsAdded`, `AsAService_LogEventsGoToTheRollingFile` |
| FR-004 key/log dirs under content root (AC-006) | `ConfiguredDirectoryResolutionTests` (5 tests, both hosts) |
| VR-002 / config sources unchanged | `AsAService_TheConfigurationSourcesAreThoseOfAConsoleStart` |
| FR-008 package placement (AC-008) | `WindowsServicePackageTests` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Force service mode via env var / argument to relocate key ring | detection takes no input | `Program.Main` in both hosts | PASS |
| Relative key directory as a service lands in `C:\Windows\System32` (shared, wrong ACL) | resolve against application folder | `ConfiguredDirectory.Resolve`, CP `ResolveDirectory`, tests | PASS |
| Setup code leaks to Event Log (readable by more accounts) | no Event Log provider receives events | §8, tests | PASS |
| Secrets readable machine-wide | per-service account variables (OD-002 a) | deployment step | NOT_VERIFIED (manual, FR-009) |

## 16. Repository Hygiene

Working tree: only Story code, tests and workflow artifacts. Ignored (not
staged): `classroom_cache.db`, prototype `.xlsx` exports; live credential files
remain git-ignored (not opened). No secret-like file added.

## 17. Deviations

None from the approved spec. I-5 choice (drive-/root-relative → under content
root) is stated in the implementation report and matches the spec.

## 18. Findings

**INFO-1 — Runtime crash records outside the application** (LOGGING).
A start-up refusal is rethrown (`Web/Program.cs`, CP `RequiredSetting`); as a
service the .NET runtime / Windows record the unhandled exception in the
Application Event Log. This is outside the application (like SCM records,
FR-007) and the messages carry only setting names and rules, never values
(SC-10). No correction. Keep exception messages value-free in future Stories.

**INFO-2 — Duplicated path resolution** (CONFIGURATION).
`ControlPlane/Program.cs ResolveDirectory` duplicates
`Web/Configuration/ConfiguredDirectory.Resolve`; the Control Plane cannot
reference Web (AD-3). Both are covered by the same tests across both hosts. A
future divergence would be caught by `ConfiguredDirectoryResolutionTests`.

**INFO-3 — Deployment-time controls carry the secret protection** (SECRET_MANAGEMENT).
Per-service dedicated account, secrets as that account's own variables
(OD-002 a), and the key-directory ACL (DC-14) are manual. Verify them during
FR-009 on the pilot server before HUMAN_PR_APPROVAL is relied on for production.

**INFO-4 — Service-mode content-root assertions are vacuous under `dotnet test`**
(TEST_COVERAGE). Known from TEST_WRITING: test cwd equals `AppContext.BaseDirectory`;
the rule is proved via console tests with a distinct `--contentRoot` and by FR-009
step 5.

## 19. Positive Controls

- Detection independent of configuration (VR-002).
- `WindowsServiceLifetime` registered directly; no Event Log provider added.
- Serilog logger factory in both modes; identical provider set.
- Key ring and logs anchored to the application folder as a service.
- Package confined to hosts; no vulnerabilities reported.
- Pipelines (auth, antiforgery, fallback, no-store) unchanged; full suite green.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No real Windows service run (FR-009 pending on the pilot server).
- Deployment ACLs and service accounts not verifiable from code.
- Code/config/test review only; no penetration testing.

## 22. Verdict Rationale

Build and tests green (re-run independently); no Critical/Major/Minor findings;
every touched SC item PASS; no blocking Open Decision. **PASS** →
HUMAN_PR_APPROVAL. FR-009 manual verification must be recorded in the
implementation report §3.1 before the human approves.
