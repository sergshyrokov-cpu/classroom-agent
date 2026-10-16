---
artifact_type: security_review
story: US-049
version: 1
status: APPROVED
created_at: 2026-10-16T13:11:10Z
updated_at: 2026-10-16T13:11:10Z
produced_by: security-reviewer
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
  - path: docs/evidence/US-049-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-049-implementation-verification.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-049 Security Review — Data Protection keys encrypted with DPAPI

## 1. Executive Summary

**PASS.** Both hosts now call `ProtectKeysWithDpapi(protectToLocalMachine: false)`
right after `PersistKeysToFileSystem`, so every key written after this Story is
DPAPI-encrypted for the account the host runs under (§8 v94, SC-7). A host told
it is not on Windows refuses to start before any key-ring code runs, and no
configuration input can switch that decision. The change adds no endpoint, no
package, no schema change and no new log content beyond a fixed reason string.
Independent build (0 warnings) and full suite (6057/6057) are green; the
vulnerability scan is clean. 0 Critical, 0 Major, 0 Minor, 4 Informational.
The one gap is inherent: a run under a genuine second Windows account (AC-002)
can only be checked by hand on the pilot server.

## 2. Reviewed Artifacts

As in the front matter `inputs`. API and DB designs are NOT_APPLICABLE records.
`HUMAN_SPEC_APPROVAL` recorded (spec v1 APPROVED, `80d7724`). No input is
`SUPERSEDED`.

## 3. Security-Relevant Scope

- **Changed production code (5 files):**
  `Web/Security/InstallationSecurityServices.cs`,
  `ControlPlane/Security/ControlPlaneSecurityServices.cs` (DPAPI encryptor);
  `Web/Program.cs`, `ControlPlane/Program.cs` (`BuildApplication(args,
  runsAsService, runsOnWindows)` + refusal); `Web/Configuration/InstallationLogging.cs`
  (refusal line with a fixed reason).
- **Assets:** Data Protection key ring of each host (protects session cookies
  of Owner/Admin/Dean and antiforgery tokens).
- **Trust boundary:** the key directory on disk vs. other Windows accounts,
  other programs, backups and stolen disks.
- **Untouched:** endpoints, cookies, policies, antiforgery, Identity, Google
  access, secret store, database, Control Plane channel.

## 4. Environment and Tools

.NET SDK 10.0.401, Windows 10 Pro, Docker available (Testcontainers).
Delegated to a cheap worker (facts only, verdict mine): `dotnet build
ClassroomAgent.sln` — 0 errors, 0 warnings; `dotnet test ClassroomAgent.sln` —
6057 total, 6057 passed, 0 failed, 0 skipped (7 min 27 s, single run, no
stalls); `dotnet list package --vulnerable --include-transitive` — no vulnerable
packages in any of the 7 projects; `git diff --stat` and `.csproj`/props diff
(empty). Code, tests and configuration were read directly by the reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed touched. |
| SC-2 Authentication | PASS | Cookie options, Identity options, HTTPS/HSTS unchanged (diff). Sessions survive a same-account restart (`*KeyEncryptionTests.AfterARestartOverItsOwnEncryptedKeys_…`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched. |
| SC-4 Authorization | PASS | No endpoint added; antiforgery unchanged; undecryptable-key token refused through the existing refusal (`…CannotDecrypt_EarlierSessionAndFormTokenAreRefused_…`, both hosts). |
| SC-5 Read-only mode | NOT_APPLICABLE | No use case touched. |
| SC-6 No DB UI | NOT_APPLICABLE | Not touched. |
| SC-7 Keys | PASS | `ProtectKeysWithDpapi(protectToLocalMachine: false)` on both hosts; the only `AddDataProtection` calls in `src/` are these two. Key files proven DPAPI-encrypted, user-bound (flags word without `CRYPTPROTECT_LOCAL_MACHINE`), no plain `masterKey` (`KeyFile.IsEncryptedForCurrentUser`, `KeyRingEncryptionTests`). Key directory location unchanged. |
| SC-8 Google | NOT_APPLICABLE | Not touched. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | Refusal reasons are constants naming DPAPI, no path or key material. Tests assert no DPAPI blob appears in the log files after an undecryptable-key start (`InstallationKeyEncryptionTests:61`, `ControlPlaneKeyEncryptionTests:66`). Framework logs a skipped key element without child nodes. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` untouched. |
| SC-13 Outbound | NOT_APPLICABLE | No outbound flow. |

## 6. Authentication and Authorization

No change to who can reach what. A cookie or antiforgery token protected by a
key the account cannot decrypt is rejected: the user is treated as signed out
and the form post gets the existing refusal (FR-003) — tested on both hosts.
Same-account restart keeps sessions (FR-006) — tested on both hosts.

## 7. Credentials, Key and Google Access

The service-account key and OAuth client secret paths are untouched (S-06).
DPAPI binding is current user only (VR-001): the code passes
`protectToLocalMachine: false`, and a test reads the DPAPI blob's flags to
prove it (`TheDpapiScopeCheck_TellsTheCurrentUserFromTheMachine` calibrates the
check both ways).

## 8. Sensitive Data Exposure

No response, view, DTO, export or audit change. Logs: fixed reason strings
only; no blob in logs proven by test. Test fixtures write synthetic keys only;
`KeyFile.Describe()` prints flags and decryptor name, never key material.

## 9. Input Validation

The OS decision takes no configuration input: `Main` passes
`OperatingSystem.IsWindows()`; the two-argument overload does the same
(VR-003). `DataProtection:KeyDirectory` validation unchanged (VR-004).

## 10. API Security

No endpoint, request or response change (API_DESIGN NOT_APPLICABLE confirmed by
the diff).

## 11. Persistence and Configuration

No schema change, no migration needed (DB_DESIGN NOT_APPLICABLE confirmed). No
`appsettings` change. Plain keys written before v94 are read until expiry and
not rewritten (FR-004, `AStillValidPlainKey_IsReadAndLeftAsItIs_…`) — accepted
by OD-002 a and §8 v94.

## 12. Logging, Audit and Telemetry

Installation refusal writes one `Fatal` line with the reason before the host
exists (`NonWindowsStartRefusalTests`); Control Plane refusal is the exception
message. No audit change. Hook telemetry not touched by this Story.

## 13. Dependencies

No package or project reference added (`.csproj`/props diff empty). DPAPI
encryption is in the shared framework (S-05). Vulnerability scan: none found.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| FR-001/002, VR-001/002 | `KeyRingEncryptionTests.OnAnEmptyKeyDirectory_…`, `TheDpapiScopeCheck_…`; `*KeyEncryptionTests.…WithEveryKeyWrittenEncrypted` |
| FR-003 (AC-002 stand-in) | `KeyRingEncryptionTests.AKeyNoAccountCanDecrypt_…`, `AnUndecryptableNewestKey_…`; `*KeyEncryptionTests.AfterARestartOverKeysTheAccountCannotDecrypt_…` |
| FR-004 | `KeyRingEncryptionTests.AStillValidPlainKey_…` |
| FR-005, VR-003 | `NonWindowsStartRefusalTests` (2; key directory empty asserted) |
| FR-006 | `KeyRingEncryptionTests.RestartedUnderTheSameAccount_…`; `*KeyEncryptionTests.AfterARestartOverItsOwnEncryptedKeys_…` |
| S-02 | blob-not-in-logs assertions in both `*KeyEncryptionTests` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Key directory copied and read under another account / on another PC | DPAPI user-bound blob does not decrypt | user-bound flag test; undecryptable-key tests | PASS (genuine second account: manual, INFO-3) |
| Attacker forges a cookie with a stolen plain key file | No plain key written after this Story | `IsEncryptedForCurrentUser` on every written file | PASS |
| Host started on Linux to sidestep DPAPI | Start refused before any key write | `NonWindowsStartRefusalTests`; defensive throw in `Add…Security` | PASS |
| Setting/env/argument used to disable the OS check | No such input exists | Code read: only `OperatingSystem.IsWindows()` feeds it | PASS |
| Log file leaks key material on decryption failure | No blob in logs | log assertions | PASS |

## 16. Repository Hygiene

`google_credentials.json` and `dac-classroom-agent-*.json` remain git-ignored
(`.gitignore:3-4`; contents not opened). No database file, `.xlsx`, `.env` or
key file in the change set. `.claude/settings.local.json` is modified (one local
permission entry) — IDE-local config, must stay out of the commit (INFO-1).

## 17. Deviations

- Defensive `PlatformNotSupportedException` in both `Add…Security` methods,
  unreachable after the `BuildApplication` refusal; makes the CA1416 analyser
  happy and guarantees no plain-key path. Accepted (impl report, verification D-1).
- Mixed directory (readable + undecryptable newest key): the framework writes a
  new key. Matches FR-003. Accepted (verification D-2).

## 18. Findings

- **INFO-1** (REPOSITORY_HYGIENE) — `.claude/settings.local.json` modified;
  keep it out of the Story commit (DoD 8). No correction to code.
- **INFO-2** (CONFIGURATION, SC-7) — the installation creates its
  `WebApplicationBuilder` and log directory before the off-Windows refusal
  (verification N-2). Nothing is written to the key directory (asserted). No
  correction needed.
- **INFO-3** (TEST_COVERAGE, SC-7) — AC-002 under a genuine second Windows
  account is a manual check on the pilot server (impl report §3.1, I-2); the
  `/api/v1` `401` path for an undecryptable-key cookie is not separately tested
  (verification N-1) — authentication handling there is unchanged code.
- **INFO-4** (CONFIGURATION) — user-bound DPAPI needs the service account's
  profile loaded and survives only a normal password change; a forced reset
  signs everyone out (§8 v94, DC-2). Deployment point, already documented.

## 19. Positive Controls

DPAPI current-user encryptor on both hosts; refusal off Windows before key
writes, not configurable; test-level proof of file content (not only that the
host works); log hygiene asserted on the failure path; no new package or surface.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…003 resolved (a/a/a).

## 21. Review Limitations

No run under a genuine second Windows account or on a non-Windows OS (driven
through the supplied decision instead). No penetration testing — code,
configuration and test review plus an independent full test run.

## 22. Verdict Rationale

Green build and suite independently reproduced, every touched SC item PASS,
security-relevant ACs proven by tests (AC-002 by an equivalent stand-in, with
the manual check recorded), no Critical/Major/Minor finding, no open security
decision. Verdict **PASS**.
