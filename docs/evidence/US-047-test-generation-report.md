---
artifact_type: test_generation_report
story: US-047
version: 1
status: DRAFT
created_at: 2026-10-14T12:43:32Z
updated_at: 2026-10-14T13:05:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-047-test-strategy.md
    version: 1
  - path: docs/tests/US-047-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-047-spec.md
    version: 1
  - path: docs/decisions/US-047-open-decisions.md
    version: 2
supersedes: null
---

# US-047 Test Generation Report

## Result

**PASS.** 33 new tests compile and fail for the expected reason only; the 5644
existing tests (plus 4 new guard cases) pass; every Acceptance Criterion is
mapped in the `ac_test_matrix`.

## Files created

- `tests/ClassroomAgent.Tests/Hosting/WindowsServiceHostingTests.cs` — AC-001, AC-002, AC-004, AC-007 (16 cases)
- `tests/ClassroomAgent.Tests/Hosting/ConfiguredDirectoryResolutionTests.cs` — AC-006 (11 cases)
- `tests/ClassroomAgent.Tests/Hosting/SetupCodeServiceModeTests.cs` — AC-005 (2 cases, one with PostgreSQL)
- `tests/ClassroomAgent.Tests/Architecture/WindowsServicePackageTests.cs` — AC-008 (8 cases)
- `tests/ClassroomAgent.Tests/TestInfrastructure/BuiltHost.cs`, `BuiltHostKind.cs`,
  `HostBuildArguments.cs`, `HostFolders.cs` — fixtures (test strategy §5)

## Files modified

- `tests/ClassroomAgent.Tests/TestInfrastructure/ControlPlaneTestHost.cs` —
  `MigrateAsync` made public (used by the AC-005 test).
- `src/ClassroomAgent.Web/Program.cs`, `src/ClassroomAgent.ControlPlane/Program.cs`
  — **compile-only skeleton** under OD-003 a) (Owner, 2026-10-14): one
  `public static WebApplication BuildApplication(string[] args, bool runsAsService)`
  each, throwing `NotImplementedException`. `Main` / the entry point, the
  package references and all behaviour unchanged. IMPLEMENTATION owns it.
- `docs/decisions/US-047-open-decisions.md` — v2: OD-003 raised and resolved.

## Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~ClassroomAgent.Tests.Hosting|FullyQualifiedName~WindowsServicePackageTests"`
- `dotnet test ClassroomAgent.sln --no-build` — total 5677, failed 33, passed 5644, skipped 0.

## Expected failing new tests (33)

- 29 × `NotImplementedException` from the skeleton `BuildApplication` (every
  `Hosting.*` case; the refusal-record test sees it as the wrong exception type
  in `Assert.Throws<InstallationSettingException>`).
- 4 × `WindowsServicePackageTests` on the two hosts: the package is not
  referenced yet (`ProjectPackages_AreExactlyTheApprovedOnes` collection
  mismatch; `WindowsServicesPackage_FollowsTheDotNet10Line` key not found).

## Passing new tests (4) — guards

`ProjectPackages_AreExactlyTheApprovedOnes` for Domain, Contracts, Application,
Infrastructure: they pass now and must stay green (the package stays out of
them, no other package is added — AC-008).

## Validation of the tests themselves

A **throwaway prototype** of the implementation (both `BuildApplication`s with
the Windows service lifetime, application-folder content root in service mode,
`Path.GetFullPath(value, contentRoot)` for both directories; the package at
10.0.12) was applied locally, run, and **reverted** — none of it remains:

- new tests: 35 / 37 passed at first; the 2 failures were a test
  over-strictness (setting `ContentRootPath` adds an in-memory configuration
  provider). `BuiltHost.ConfigurationSources` now ignores in-memory providers
  (they hold host defaults such as the content root, which differs by design,
  FR-003); then 37 / 37.
- full suite on the prototype: 5677 / 5677 passed — the existing
  `WebApplicationFactory` hosts work through a `BuildApplication`-based entry
  point (AC-004).

This confirms the tests can turn green without bending, and the red phase is
caused only by missing production behaviour.

## Unexpected failures

None.

## Untested Acceptance Criteria

None automated-uncovered. AC-001 … AC-003 under a real Service Control Manager
are the manual verification of OD-001 a) (spec FR-009), to be recorded by
IMPLEMENTATION in the `implementation_report`.

## Non-blocking findings

1. **Vacuous in this environment:** the service-mode content-root assertions
   (`AsAService_TheContentRootIsTheApplicationFolder`,
   `AsAService_RelativeDirectories_ResolveIntoTheApplicationFolder`) cannot tell
   the application folder from the working directory under `dotnet test` — both
   are the test output folder. The resolution rule is proved by the console
   tests with a distinct content root; the service folder is also checked by
   hand (FR-009 step 5).
2. **Spec I-5 unpinned:** the exact folder for `C:logs` / `\logs` is not fixed
   by the Specification (`Path.GetFullPath(value, contentRoot)` puts `\logs` at
   the content root's drive root, not inside the content root). Untested;
   IMPLEMENTATION should state its choice in the `implementation_report`.
3. **Package version:** the prototype restored
   `Microsoft.Extensions.Hosting.WindowsServices` 10.0.12 from nuget.org; the
   test requires only the `10.` line (FR-008).
4. **Lifetime registration:** the package's own `AddWindowsService` /
   `UseWindowsService` re-check `IsWindowsService()` internally and would leave
   a test-built "service" host on the console lifetime; IMPLEMENTATION must
   register `WindowsServiceLifetime` itself when `runsAsService` is true (the
   prototype did, guarded by `OperatingSystem.IsWindows()` for CA1416). That
   also keeps the package's Event Log provider out (FR-007).

## Open Decisions

OD-001 a), OD-002 a) (Story); OD-003 a) (TEST_WRITING) — all resolved. None open.

## Delegation

None. The skill asks for `cheap-worker` runs of build/test; the work was small
(one build, three test runs) and was done inline.
