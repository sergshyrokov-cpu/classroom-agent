---
artifact_type: test_strategy
story: US-049
version: 1
status: DRAFT
created_at: 2026-10-16T13:30:00Z
updated_at: 2026-10-16T13:30:00Z
produced_by: test-writer
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
  - path: docs/reviews/specifications/US-049-spec-review.md
    version: 1
supersedes: null
---

# US-049 Test Strategy — Data Protection keys encrypted at rest with DPAPI

## 1. Scope

Both hosts' Data Protection key rings (installation and Control Plane): what is
written to the key directory (FR-001, FR-002), how a key the account cannot
decrypt is handled (FR-003), keys written before this Story (FR-004), the
refusal to start outside Windows (FR-005) and key reuse across a restart under
the same account (FR-006). API_DESIGN and DB_DESIGN are NOT_APPLICABLE: no
endpoint, contract or schema is tested.

AC-004 (FR-007) is verified by inspection of `deployment-conventions.md` DC-2
(spec I-4) and has no automated test.

## 2. Test levels

| Level | What | Classes |
|---|---|---|
| Host-build (integration, no database) | Each host built through its `BuildApplication` and its key ring used directly; the key files read as XML | `Hosting/KeyRingEncryptionTests`, `Hosting/NonWindowsStartRefusalTests` |
| HTTP integration (Testcontainers PostgreSQL) | Sign-in, form submission, session and antiforgery acceptance across a restart over the same key directory | `ControlPlane/Security/ControlPlaneKeyEncryptionTests`, `Web/Security/InstallationKeyEncryptionTests` |

No unit tests: the behaviour is framework configuration and host start-up, so a
unit test would only restate framework behaviour.

## 3. How "encrypted for the current user" is proved (VR-001, VR-002)

A key file is accepted only when (`TestInfrastructure/KeyFile.IsEncryptedForCurrentUser`):

- no `masterKey` element (plain key material) is present;
- its `encryptedSecret` names `Microsoft.AspNetCore.DataProtection.XmlEncryption.DpapiXmlDecryptor`;
- the DPAPI blob's flags word (offset 40) does not carry
  `CRYPTPROTECT_LOCAL_MACHINE` (0x4) — the machine-wide binding VR-001 forbids.

The flags check is calibrated by
`TheDpapiScopeCheck_TellsTheCurrentUserFromTheMachine`, which runs ASP.NET Core's
own `DpapiXmlEncryptor` with `protectToLocalMachine` false and true. A
certificate or DPAPI-NG encryptor fails the decryptor-type check.

## 4. Stand-in for "another Windows account" (AC-002, spec I-2, review m-3)

A test cannot switch Windows accounts. A real DPAPI key (written by a key ring
the test builds with `ProtectKeysWithDpapi()` and the host's application name,
or by the host itself) has the last 16 bytes of its DPAPI blob inverted; DPAPI
then refuses to decrypt it under any account — the same failure a host sees for a
key written under another account. A run under a genuine second account is the
manual check of spec I-2 (implementation report §3.1, pilot server).

Review m-3 asks that the tests prove the framework's handling rather than assume
it. Two key-ring cases:

- **Only key, newest default:** the single key in the directory is valid and
  undecryptable. Expected: host builds, payload protected with it is refused
  (`CryptographicException`), a new key is created, it is encrypted for the
  current user, the unreadable file is byte-identical afterwards, no blob text in
  the host's log.
- **Newest key unreadable beside an older readable one:** the newest key — the
  one the payload was protected with (asserted by reading the key id from the
  payload) — is made undecryptable. Expected: the payload is refused, a new
  payload is protected with a different key and round-trips, both files are
  byte-identical, any new key is encrypted. Whether the framework falls back to
  the older key or creates a new one is not asserted: FR-003's outcome (the
  unreadable key is not used and the host works) holds either way.

## 5. Scenarios

### Positive
- First key on an empty directory is encrypted for the current user; protect and
  unprotect work (AC-001, both hosts, host-build).
- Owner: first-run setup form, sign-out form, sign-in form all succeed; every key
  file encrypted (AC-001, HTTP).
- Dean: password sign-in form and sign-out form succeed; every key file encrypted
  (AC-001, HTTP).
- Restart over its own encrypted keys: earlier payload unprotected, no new key
  written, files unchanged (AC-003, both hosts, host-build).
- Restart over its own encrypted keys: earlier session cookie accepted (200) and
  earlier sign-in form token accepted (sign-in redirects) (AC-003, HTTP, both hosts).
- Plain pre-v94 key: earlier payload unprotected; file left byte-identical; the
  next key created through `IKeyManager` is encrypted (AC-005, both hosts).

### Negative
- Restart over undecryptable keys: earlier session cookie → redirect to sign-in;
  earlier antiforgery token → `400` (the existing refusal); a fresh sign-in works;
  unreadable files unchanged; a new encrypted key written; no blob text in the
  log (AC-002, HTTP, both hosts).
- Host told it is not on Windows: build throws (not `NotImplementedException`);
  installation writes a `Fatal` line whose `Reason` names DPAPI; Control Plane's
  exception message names DPAPI; nothing written to the key directory (AC-006,
  both hosts).

### Boundary
- The undecryptable key is the newest, still-valid default key (review m-3),
  alone and beside an older readable key.

### Validation
- VR-001 machine binding rejected — calibrated flag check (§3).
- VR-002 file content checked, not only host behaviour.
- VR-003 (no configuration switches the OS decision) — not separately testable
  without inventing a switch to try; the decision is supplied as a
  `BuildApplication` parameter, never configuration, and is left to
  IMPLEMENTATION_VERIFICATION and SECURITY_REVIEW to read.
- VR-004 unchanged — covered by the existing US-047/US-008 directory tests.

### Security
- S-02: no corrupted DPAPI blob text appears in either host's log (AC-002).
- S-04: cookies, antiforgery and sessions unchanged — the existing cookie,
  antiforgery and session test classes stay green (regression).

### Persistence
- None — no schema change (DB_DESIGN NOT_APPLICABLE).

## 6. Fixtures

- `TestInfrastructure/KeyFile` — parsed form of one key file (record).
- `TestInfrastructure/KeyRingFiles` — read/snapshot key files, write a plain or
  DPAPI key with a test-built key ring, create a key with given dates, read the key
  id from a protected payload, make a DPAPI key undecryptable.
- `TestInfrastructure/BuiltHost.BuildOffWindows` — builds a host with the
  operating-system decision set to "not Windows";
  `BuiltHost.DataProtectionApplicationName`.
- `ControlPlaneTestHost.Restart` takes optional `extraSettings`, so a restart can
  reuse the first host's key directory (the installation's `Settings` already
  allow it).
- `HostFolders` (US-047) for private temporary directories.

All classes that touch DPAPI are `[SupportedOSPlatform("windows")]`; the tests
run on Windows (spec §9).

## 7. Excluded scenarios

- A run under a genuine second Windows account — manual (spec I-2), pilot server.
- AC-004 — inspection of DC-2 (spec I-4).
- A forced password reset of the service account — same DPAPI failure as §4.
- A genuine non-Windows run — the decision is driven by parameter (FR-005).

## 8. Known limitations

- The AC-002 key-ring tests may already pass before implementation: decrypting
  a key is driven by the decryptor type recorded in the file, so a host without
  `ProtectKeysWithDpapi` still skips an undecryptable DPAPI key. They exist to
  prove the framework behaviour (m-3); the encrypted-new-key assertions in the
  same tests are what fails before implementation.
- AC-003's "same account" path is "as today" for plain keys; the tests require
  the keys to be encrypted first, so they prove encrypted keys survive a restart.

## 9. Open Decisions affecting testing

None. OD-001…OD-003 are resolved (a/a/a).

The skeleton's `BuildApplication(string[] args, bool runsAsService, bool runsOnWindows)`
overload on both hosts is a technical choice FR-005 delegates ("the mechanism
is a technical choice", the US-047 `runsAsService` precedent), not a new
requirement.
