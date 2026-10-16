---
artifact_type: implementation_verification
story: US-049
version: 1
status: APPROVED
created_at: 2026-10-16T13:00:14Z
updated_at: 2026-10-16T13:00:14Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-049-encrypt-data-protection-keys.md
    version: null
  - path: docs/specifications/US-049-spec.md
    version: 1
  - path: docs/decisions/US-049-open-decisions.md
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
  - path: docs/evidence/US-049-implementation-report.md
    version: 1
supersedes: null
verdict: PASS
---

# US-049 Implementation Verification

## 1. Summary

**PASS.** Both hosts register `ProtectKeysWithDpapi(protectToLocalMachine: false)`
right after `PersistKeysToFileSystem`, and both `BuildApplication(args,
runsAsService, runsOnWindows)` refuse to start off Windows before any key-ring
code runs. Every AC is proved by a test that checks the key files themselves
(VR-002), not only that the host works. The tests failed in the red phase for the
right reason (plain keys written). Review m-3 is confirmed. No Critical or Major
finding. There are 3 Minor findings, plus 2 deviations, both disclosed and sound.

## 2. Reviewed Artifacts

Story (unversioned); specification v1 APPROVED; open decisions v1 APPROVED
(OD-001…003 a/a/a); API design v1 NOT_APPLICABLE (no openapi); DB design v1
NOT_APPLICABLE (no entity model); test strategy v1; AC test matrix v1; test
generation report v1; implementation report v1. Working tree: 5 modified source
files, 2 modified test-infrastructure files and 6 new test files, checked with
`git diff` and `git status`.

## 3. AC Verification

| AC | Tests | Proves the AC? | Verdict |
|---|---|---|---|
| AC-001 | `KeyRingEncryptionTests.OnAnEmptyKeyDirectory_…` [both], `TheDpapiScopeCheck_…` (calibration), `ControlPlaneKeyEncryptionTests.TheOwnerSetsUp…`, `InstallationKeyEncryptionTests.ADeanSignsIn…` | Yes. `KeyFile.IsEncryptedForCurrentUser` requires all of these: no `masterKey` element, the `DpapiXmlDecryptor` decryptor type, and the DPAPI blob's `CRYPTPROTECT_LOCAL_MACHINE` flag cleared. The calibration test shows that the flag check can tell a current-user key from a machine key, so VR-001 is checked, not assumed. The HTTP tests sign in and post a form (sign-out). | Met |
| AC-002 | `KeyRingEncryptionTests.AKeyNoAccountCanDecrypt_…`, `AnUndecryptableNewestKey_BesideAReadableOlderOne_…` [both]; `…AfterARestartOverKeysTheAccountCannotDecrypt_…` (both hosts) | Yes, with an automated stand-in for the other account (I-2): the DPAPI blob is corrupted, so no account can decrypt it. The tests assert all of this: the earlier payload, session and form token are refused (302 `/sign-in`, 400); a new sign-in works; the unreadable file is byte-identical; a new key is created (`Assert.NotEmpty`) and it is encrypted; and no blob text appears in the log (S-02). **m-3 confirmed:** the framework skips an undecryptable key, including when it is the newest default key, and writes a new one. | Met (a genuine second account is a manual check, report §3.1) |
| AC-003 | `KeyRingEncryptionTests.RestartedUnderTheSameAccount_…` [both]; `…AfterARestartOverItsOwnEncryptedKeys_…` (both hosts) | Yes. The tests check four things: the keys are encrypted before the restart; the earlier payload decrypts after it; the earlier session (200) and form token (302) are accepted; and no new key is written and no file changes. | Met |
| AC-004 | Inspection | I read `deployment-conventions.md` DC-2. Step 2 requires the console run under the service's own Windows account and a check of the directory's rights before the first start. Step 6 says the same for the installation. | Met by inspection |
| AC-005 | `KeyRingEncryptionTests.AStillValidPlainKey_…` [both] | Yes. A precondition asserts that the seeded key is really plain. The test then asserts that the payload decrypts, the plain file is byte-identical, and exactly one new key is written, encrypted. | Met |
| AC-006 | `NonWindowsStartRefusalTests` (2) | Yes. The build throws something other than `NotImplementedException`. The installation writes a `Fatal` line whose `Reason` names DPAPI; for the Control Plane the exception message names DPAPI. The key directory stays empty in both cases. The decision comes only from the `runsOnWindows` parameter; `Main` feeds it `OperatingSystem.IsWindows()` (VR-003). | Met |

## 4. Specification Coverage

- FR-001 / VR-001: `ProtectKeysWithDpapi(protectToLocalMachine: false)` in
  `InstallationSecurityServices.AddInstallationSecurity` and
  `ControlPlaneSecurityServices.AddControlPlaneSecurity`. The application names,
  the directory and the lifetime are unchanged. These are the only
  `AddDataProtection` registrations in `src/`.
- FR-002: no path writes a plain key. If the OS check fails, the `Add…Security`
  method throws instead of registering the key ring without encryption.
- FR-003 / I-1: framework behaviour, proven by tests. No file is deleted or
  rewritten.
- FR-004: framework behaviour, proven by tests. No re-encryption code.
- FR-005 / I-3: the installation reports the refusal through
  `InstallationLogging.WriteStartupRefusal(directory, reason, …)` (a new
  overload; the existing one delegates to it), then throws. The Control Plane
  throws `PlatformNotSupportedException` before `CreateBuilder`. Neither message
  contains a path or key material (S-02).
- FR-006: proven by the AC-003 tests.
- FR-007: no code change, as I-4 says.
- VR-004: key-directory validation is unchanged. The regression classes
  `DataProtectionTests` and `ConfiguredDirectoryResolutionTests` are green
  (report §5).
- No behaviour outside the scope of any artifact.

## 5. Design Conformance

API and DB designs are NOT_APPLICABLE. No endpoint, route, cookie, policy,
schema or migration changed (S-04). No package was added (S-05). AD-3, AD-4, AD-6
and AD-8 are not touched: the changes are confined to host composition (`Program`,
`Security`, `Configuration` in the presentation projects), where Data Protection
is registered today.

## 6. Deviations

| # | Deviation | Departs from | Reason | Verdict |
|---|---|---|---|---|
| D-1 | A defensive `PlatformNotSupportedException` in both `Add…Security` methods | Spec FR-005 places the refusal in the start-up path only | `ProtectKeysWithDpapi` is Windows-only. Without the platform guard, CA1416 under `TreatWarningsAsErrors` breaks the build. The throw keeps FR-002 true on any path, and `BuildApplication` already refuses earlier, so it is never reached. | Accepted — disclosed (report §7), sound |
| D-2 | Mixed keys: an unreadable newest key next to a readable older one makes the framework write a new key instead of falling back to the older one | Nothing; FR-003 says "creates a new key" | Framework behaviour. The FR-003 outcome holds. | Accepted — disclosed, matches FR-003 |

Also disclosed: a reason-string overload was added instead of reusing
`InstallationSettingException`. This is in line with I-3.

## 7. Report Accuracy

- The change set in §4 matches `git status`: 5 source files, 2 modified test
  files and 6 new test files, all traced to `ac_test_matrix`.
- `.claude/settings.local.json` is modified but not part of the Story. It is
  correctly flagged to stay out of the commit.
- The AC → test locations in §3 exist and match the matrix.
- Changed files contain no `TODO`, `TBD` or `FIXME`, no skipped test and no
  `NotImplementedException` left from the skeleton. The only occurrences of
  `NotImplementedException` are the two `IsNotType` assertions in
  `NonWindowsStartRefusalTests`.

## 8. Story Test Run

Delegated to `cheap-worker` (Sonnet). It was allowed to touch no files and to
report verbatim:

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `--filter FullyQualifiedName~KeyRingEncryptionTests` | 12/12 passed |
| `--filter FullyQualifiedName~NonWindowsStartRefusalTests` | 2/2 passed |
| `--filter FullyQualifiedName~InstallationKeyEncryptionTests` | 3/3 passed |
| `--filter FullyQualifiedName~ControlPlaneKeyEncryptionTests` | 3/3 passed |

20/20 Story tests are green, with 0 skipped. I did not rerun the full suite;
`security-reviewer` runs it.

## 9. Findings

- **N-1 (Minor)** — spec §8, FR-003: when a session cookie under `/api/v1`
  carries an undecryptable key, the API should return `401`. No test covers
  this; only the HTML redirect is tested. The behaviour comes from the existing
  cookie-authentication events, which this Story does not change.
- **N-2 (Minor)** — `ClassroomAgent.Web.Program.BuildApplication` calls
  `WindowsServiceHosting.CreateBuilder` before the OS refusal, because the
  refusal needs the log directory. The refusal still comes before any
  Data Protection registration, and the AC-006 test proves the key directory
  stays empty.
- **N-3 (Minor)** — the reason text is defined twice:
  `InstallationLogging.NotOnWindowsReason` and
  `ControlPlane.Program.NotOnWindowsReason`. Each host names itself, so this is
  acceptable.

## 10. Verdict

**PASS** — next stage SECURITY_REVIEW.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION_VERIFICATION
  story: US-049
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/implementation/US-049-implementation-verification.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "D-1 accepted: defensive PlatformNotSupportedException in both Add…Security methods (CA1416 guard, unreachable)"
    - "D-2 accepted: mixed keys — framework writes a new key rather than falling back; FR-003 holds"
    - "m-3 confirmed: framework skips an undecryptable key incl. newest default, writes a new encrypted one"
    - "N-1 Minor: /api/v1 401 for an undecryptable-key cookie not tested (framework path unchanged)"
    - "N-2 Minor: installation builds WebApplicationBuilder before the OS refusal; key directory proven empty"
    - "N-3 Minor: NotOnWindowsReason defined per host"
    - "AC-002 under a genuine second Windows account remains a manual check on the pilot server (impl report §3.1)"
```
