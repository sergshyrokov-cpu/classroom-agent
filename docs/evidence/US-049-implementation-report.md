---
artifact_type: implementation_report
story: US-049
version: 1
status: DRAFT
created_at: 2026-10-16T14:20:00Z
updated_at: 2026-10-16T14:20:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-049-encrypt-data-protection-keys.md
    version: null
  - path: docs/decisions/US-049-open-decisions.md
    version: 1
  - path: docs/specifications/US-049-spec.md
    version: 1
  - path: docs/designs/api/US-049-api-design.md
    version: 1
  - path: docs/designs/database/US-049-db-design.md
    version: 1
  - path: docs/tests/US-049-test-strategy.md
    version: 1
  - path: docs/tests/US-049-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-049-test-generation-report.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-049 Implementation Report

## 1. Summary

Both hosts now write every Data Protection key encrypted with Windows DPAPI bound
to the current Windows user (`ProtectKeysWithDpapi(protectToLocalMachine: false)`),
and refuse to start when their start-up decision says the process is not on
Windows. Keys that cannot be decrypted are skipped by ASP.NET Core itself (proved
at TEST_WRITING, review m-3); plain keys from before this Story keep being read.
No endpoint, cookie, policy, schema, translation or package changed.

Build 0 warnings; full suite 6057/6057 green; format check clean on every
changed file. One manual check remains (§3.1).

## 2. Source Artifacts

Story (unversioned), open decisions v1 (OD-001…003 resolved a/a/a),
specification v1 APPROVED, API design v1 NOT_APPLICABLE, DB design v1
NOT_APPLICABLE, test strategy v1, AC test matrix v1, test generation report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `InstallationSecurityServices.AddInstallationSecurity`, `ControlPlaneSecurityServices.AddControlPlaneSecurity` — `ProtectKeysWithDpapi(protectToLocalMachine: false)` | `KeyRingEncryptionTests.OnAnEmptyKeyDirectory_…` [both], `TheDpapiScopeCheck_…`; `ControlPlaneKeyEncryptionTests.TheOwnerSetsUp…`; `InstallationKeyEncryptionTests.ADeanSignsIn…` | Pass |
| AC-002 | Framework behaviour once keys are DPAPI-encrypted (FR-003, I-1); no extra code | `KeyRingEncryptionTests.AKeyNoAccountCanDecrypt_…`, `AnUndecryptableNewestKey_…` [both]; `ControlPlaneKeyEncryptionTests.AfterARestartOverKeysTheAccountCannotDecrypt_…`; `InstallationKeyEncryptionTests.AfterARestartOverKeysTheAccountCannotDecrypt_…` | Pass (automated stand-in); genuine second account — manual, §3.1 |
| AC-003 | Unchanged key directory and application names; DPAPI keys decrypt under the same account | `KeyRingEncryptionTests.RestartedUnderTheSameAccount_…` [both]; `…AfterARestartOverItsOwnEncryptedKeys_…` (both hosts) | Pass |
| AC-004 | No code (spec FR-007, I-4): DC-2 steps 2 and 6 already carry the rule since v94 | Inspection | Pass by inspection |
| AC-005 | Framework reads plain keys by their own descriptor; no rewrite code added | `KeyRingEncryptionTests.AStillValidPlainKey_…` [both] | Pass |
| AC-006 | `ClassroomAgent.Web.Program.BuildApplication(args, runsAsService, runsOnWindows)` — `Fatal` line via `InstallationLogging.WriteStartupRefusal(directory, reason, …)`, then `PlatformNotSupportedException`; `Program.BuildApplication(args, runsAsService, runsOnWindows)` (Control Plane) — `PlatformNotSupportedException` with the reason. Both refuse before any key-ring code runs | `NonWindowsStartRefusalTests` (2) | Pass |

### 3.1 Manual checks

- **AC-002 under a genuine second Windows account** (spec I-2) — on the pilot
  server at deployment: start a host once under an account other than its
  service account, over the service's key directory; expect every user signed
  out, the host running, the old key files untouched. Record the result here.
- **AC-004** — DC-2 steps 2 and 6 read and confirmed (v94 wording present).

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | DPAPI key encryptor (current user); throw off Windows | FR-001, FR-002, VR-001; AC-001 |
| `src/ClassroomAgent.ControlPlane/Security/ControlPlaneSecurityServices.cs` | Same for the Control Plane | FR-001, FR-002, VR-001; AC-001 |
| `src/ClassroomAgent.Web/Program.cs` | Skeleton overload completed: OS refusal, then the former body (now private `Build`); `Main` passes `OperatingSystem.IsWindows()`; the two-argument overload delegates | FR-005, VR-003; AC-006 |
| `src/ClassroomAgent.ControlPlane/Program.cs` | Same; `NotOnWindowsReason` | FR-005, VR-003, I-3; AC-006 |
| `src/ClassroomAgent.Web/Configuration/InstallationLogging.cs` | `NotOnWindowsReason`; `WriteStartupRefusal(directory, reason, …)` overload, the existing one delegates | FR-005 (refusal through the existing path); AC-006 |
| Test files of TEST_WRITING (6 created, 2 modified) | Unchanged in this stage | `ac_test_matrix` |
| `docs/evidence/US-049-implementation-report.md` | This report | `implementation_report` |

`.claude/settings.local.json` is modified in the working tree but is not part of
this Story (IDE-local config) — keep it out of the commit.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | 0 errors, 0 warnings |
| Story tests | `dotnet test tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj --no-build --filter "FullyQualifiedName~<Class>"` per class | KeyRingEncryptionTests 12/12, NonWindowsStartRefusalTests 2/2, ControlPlaneKeyEncryptionTests 3/3, InstallationKeyEncryptionTests 3/3, ControlPlane.Security.DataProtectionTests 1/1, ConfiguredDirectoryResolutionTests 11/11, WindowsServiceHostingTests 16/16 |
| Full suite | `dotnet test ClassroomAgent.sln --no-build` | 6057 total, 6057 passed, 0 failed, 0 skipped |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes --no-restore --include <13 changed files>` | exit 0 |

The red phase (18 failing) turned fully green; no test was changed.

Delegation: both test runs (story classes and full suite) went to a
`cheap-worker` (Sonnet) subagent; the build and format check were run inline as
single short commands. The code itself (five files, security configuration and
start-up refusal) was written inline — it is the security rule of the Story.

## 6. Configuration Changes

None. No setting, environment variable or argument was added; the operating
system decision is a parameter of `BuildApplication`, fed only by
`OperatingSystem.IsWindows()` (VR-003).

## 7. Deviations and Discovered Problems

- **Defensive throw in both `Add…Security` methods.** `ProtectKeysWithDpapi` is
  Windows-only (platform analyzer, CA1416, a build error under
  `TreatWarningsAsErrors`). The call is guarded by `OperatingSystem.IsWindows()`
  and the other branch throws `PlatformNotSupportedException` with the same
  reason, so no path can register a key ring that writes plain keys (FR-002).
  It is unreachable in practice: `BuildApplication` refuses earlier.
- **Mixed-key case (review m-3).** With an unreadable newest key beside a
  readable older one, ASP.NET Core writes a new key rather than falling back to
  the older one. FR-003's outcome holds; recorded for the reviewer.
- `InstallationSettingException` was not reused for the OS refusal — it is not a
  setting; a reason-string overload of the existing refusal writer was added
  instead (I-3: "through its existing start-up refusal").
- Review m-1 (I-3 mentions the Windows event log) — behaviour matches FR-005;
  the wording fix belongs to a later spec revision, not to code.

## 8. Open Decisions

None touched, none new.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-049
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-049-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "Defensive PlatformNotSupportedException in both Add…Security methods (CA1416 guard; unreachable after the BuildApplication refusal)"
    - "Mixed-key case: framework writes a new key instead of falling back to the older readable one; FR-003 holds"
    - "AC-002 under a genuine second Windows account remains a manual check on the pilot server (§3.1)"
```
