---
artifact_type: test_generation_report
story: US-049
version: 1
status: DRAFT
created_at: 2026-10-16T13:45:00Z
updated_at: 2026-10-16T13:45:00Z
produced_by: test-writer
inputs:
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
supersedes: null
---

# US-049 Test Generation Report

## 1. Story

US-049 — Session-signing keys are encrypted at rest with the service account's
Windows protection. Inputs: spec v1 (APPROVED), API and DB designs v1
(NOT_APPLICABLE), spec review v1 (PASS; m-3 addressed in §6).

## 2. Test files created

| File | Tests |
|---|---|
| `tests/ClassroomAgent.Tests/Hosting/KeyRingEncryptionTests.cs` | 12 (calibration ×2; five theories ×2 hosts) |
| `tests/ClassroomAgent.Tests/Hosting/NonWindowsStartRefusalTests.cs` | 2 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/ControlPlaneKeyEncryptionTests.cs` | 3 |
| `tests/ClassroomAgent.Tests/Web/Security/InstallationKeyEncryptionTests.cs` | 3 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/KeyFile.cs` | fixture (record) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/KeyRingFiles.cs` | fixture |

## 3. Test files modified

| File | Change |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/BuiltHost.cs` | `BuildOffWindows(host, logDirectory, keyDirectory)`; `DataProtectionApplicationName(host)` |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ControlPlaneTestHost.cs` | `Restart` takes optional `extraSettings` (restart over the same key directory); existing callers unchanged |

## 4. Compile-only skeleton

| Declaration | File |
|---|---|
| `public static WebApplication BuildApplication(string[] args, bool runsAsService, bool runsOnWindows)` — throws `NotImplementedException` | `src/ClassroomAgent.Web/Program.cs` |
| `public static WebApplication BuildApplication(string[] args, bool runsAsService, bool runsOnWindows)` — throws `NotImplementedException` | `src/ClassroomAgent.ControlPlane/Program.cs` |

FR-005 leaves the mechanism to a technical choice and names the US-047
`runsAsService` precedent; the overload follows it. Nothing registered, no
existing behaviour changed. IMPLEMENTATION owns both from here (expected: `Main`
passes `OperatingSystem.IsWindows()`, the two-argument overload delegates).

## 5. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj --no-build --filter "FullyQualifiedName~<Class>"`
  per class (`KeyRingEncryptionTests`, `NonWindowsStartRefusalTests`,
  `ControlPlaneKeyEncryptionTests`, `InstallationKeyEncryptionTests`,
  `ControlPlane.Security.DataProtectionTests`). Note: a filter joining several
  `FullyQualifiedName~` terms with `|` ran zero tests under Microsoft.Testing.Platform
  (exit 5); one class per run works.
- `dotnet test ClassroomAgent.sln --no-build` — full suite (delegated).

## 6. Results

Full suite: **6057 total, 6039 passed, 18 failed, 0 skipped** (7 min 38 s).
All 18 failures are new US-049 tests; no existing test failed.

### Passing new tests (2)

- `KeyRingEncryptionTests.TheDpapiScopeCheck_TellsTheCurrentUserFromTheMachine` (×2) —
  calibration of the fixture against ASP.NET Core's own `DpapiXmlEncryptor`; it
  is expected to pass before implementation. It proves the user/machine check of
  VR-001 is meaningful (flags word, offset 40, bit 0x4 — also confirmed with
  `ProtectedData` before writing the fixture).

### Expected failing new tests (18) — red phase verified

| Test(s) | Fails at | Meaning |
|---|---|---|
| `OnAnEmptyKeyDirectory_…` [both], `RestartedUnderTheSameAccount_…` [both] | `AssertAllEncryptedForCurrentUser` — `plain=True, decryptor=none` | Keys written plain — FR-001/FR-002 not implemented |
| `AKeyNoAccountCanDecrypt_…` [both] | line 84, new key plain | Everything before it passed (see below) |
| `AnUndecryptableNewestKey_…` [both] | line 119, new key plain | Everything before it passed (see below) |
| `AStillValidPlainKey_…` [both] | line 171, next key plain | Plain key read and left unchanged already; new key not encrypted |
| CPK/IKE `TheOwnerSetsUp…` / `ADeanSignsIn…` | `AssertAllEncryptedForCurrentUser` | Sign-in and form posts succeeded; keys plain |
| CPK/IKE `AfterARestartOverItsOwnEncryptedKeys_…` | `AssertAllEncryptedForCurrentUser` before restart | Keys plain |
| CPK/IKE `AfterARestartOverKeysTheAccountCannotDecrypt_…` | `MakeEveryKeyUndecryptable`: "carries no DPAPI-encrypted key" | Keys plain, nothing for another account to fail on |
| NWR (2) | `Assert.IsNotType<NotImplementedException>` | Skeleton; FR-005 not implemented |

Every failure is a missing production behaviour; none is a fixture, host or
syntax fault.

**Review m-3 — framework behaviour already proved.** In both AC-002 key-ring
theories every assertion before the encrypted-new-key check passed on the
unchanged hosts: the payload of the undecryptable key is refused with
`CryptographicException`, the host still protects and unprotects, a new key is
created, the unreadable file is byte-identical, and (only-key case) no blob text
reaches the log. In the mixed case the payload was confirmed to name the newest
key before it was corrupted, and the host then used a different key. So ASP.NET
Core skips an undecryptable key — including the newest default key — and creates
a new one without extra code; FR-003 needs only FR-001's encryptor. In the mixed
case the framework also wrote a new key (it did not fall back to the older one);
the test deliberately does not require either outcome.

**HTTP AC-002/AC-003 tests** stop at their key-encryption precondition before
implementation, so their HTTP assertions (302 to sign-in, 400 antiforgery, 200
after restart) first execute at IMPLEMENTATION. Their sign-in/form helpers are
already proven by the AC-001 HTTP tests, which pass their HTTP part now.

### Unexpected failures

None.

## 7. Untested Acceptance Criteria

- AC-004 — inspection of DC-2 (spec I-4); no automated test by design.
- AC-002 under a genuine second Windows account — manual (spec I-2),
  implementation report §3.1, pilot server.
- VR-003 — no configuration switch exists to try; left to verification by
  reading (test strategy §5).

## 8. Open Decisions

None new. OD-001…OD-003 resolved (a/a/a).

## 9. Delegation

Test design, fixtures, all test code and the skeleton were written inline (the
key-ring scenarios and the DPAPI stand-in needed judgement). The full-suite run
and the first filtered run were delegated to a `cheap-worker` (Sonnet) subagent;
the per-class runs were done inline after the multi-term filter ran nothing.

## 10. Overall result

PASS — every automatable AC mapped, tests compile with 0 warnings, red phase
verified, regression green.
