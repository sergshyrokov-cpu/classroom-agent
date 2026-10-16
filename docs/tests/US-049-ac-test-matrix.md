---
artifact_type: ac_test_matrix
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
  - path: docs/tests/US-049-test-strategy.md
    version: 1
supersedes: null
---

# US-049 Acceptance Criteria → Test Matrix

Status column: state before IMPLEMENTATION (red phase, 2026-10-16).
`KRE` = `Hosting.KeyRingEncryptionTests`, `NWR` = `Hosting.NonWindowsStartRefusalTests`,
`CPK` = `ControlPlane.Security.ControlPlaneKeyEncryptionTests`,
`IKE` = `Web.Security.InstallationKeyEncryptionTests`. `[both]` = theory over
`Installation` and `ControlPlane`.

| Acceptance Criterion | Scenario | Test Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 (VR-001) | DPAPI scope check tells user from machine binding | Host-build | KRE | `TheDpapiScopeCheck_TellsTheCurrentUserFromTheMachine` (×2) | User blob accepted, machine blob rejected | Pass (calibration) |
| AC-001 (FR-001, FR-002, VR-002) | First key on an empty directory | Host-build | KRE | `OnAnEmptyKeyDirectory_TheFirstKey_IsWrittenEncryptedForTheCurrentUser` [both] | Protect/unprotect work; every key file DPAPI, current user, no `masterKey` | Red — key plain |
| AC-001 | Owner: setup form, sign-out form, sign-in form | HTTP integration | CPK | `TheOwnerSetsUpSignsOutAndSignsInAgain_WithEveryKeyWrittenEncrypted` | 200 / 302 / 302 → `/`; every key file encrypted | Red — key plain (HTTP part passes) |
| AC-001 | Dean: sign-in form, sign-out form | HTTP integration | IKE | `ADeanSignsInAndSubmitsAForm_WithEveryKeyWrittenEncrypted` | 200 / 302; every key file encrypted | Red — key plain (HTTP part passes) |
| AC-002 (FR-003, S-02, m-3) | Only key — newest default — undecryptable | Host-build | KRE | `AKeyNoAccountCanDecrypt_IsSkipped_AndANewEncryptedKeyIsCreated` [both] | Earlier payload refused; host protects; unreadable file byte-identical; new key encrypted; no blob text in log | Red — new key plain (framework skip proven) |
| AC-002 (FR-003, m-3) | Newest key undecryptable beside an older readable key | Host-build | KRE | `AnUndecryptableNewestKey_BesideAReadableOlderOne_IsNotUsed` [both] | Payload of the newest key refused; new payload uses another key and round-trips; files byte-identical; any new key encrypted | Red — new key plain (framework skip proven) |
| AC-002 (FR-003, S-02) | Restart over undecryptable keys | HTTP integration | CPK | `AfterARestartOverKeysTheAccountCannotDecrypt_EarlierSessionAndFormTokenAreRefused_AndTheOwnerSignsInAgain` | Old session → 302 `/sign-in`; old form token → 400; fresh sign-in works; files unchanged; new key encrypted; no blob in log | Red — no DPAPI key to stand in |
| AC-002 (FR-003, S-02) | Restart over undecryptable keys | HTTP integration | IKE | `AfterARestartOverKeysTheAccountCannotDecrypt_EarlierSessionAndFormTokenAreRefused_AndTheDeanSignsInAgain` | As above, sign-in page `/sign-in` | Red — no DPAPI key to stand in |
| AC-003 (FR-006) | Restart over its own encrypted keys | Host-build | KRE | `RestartedUnderTheSameAccount_TheHostReadsItsOwnEncryptedKeys` [both] | Keys encrypted; earlier payload unprotected; no new key; files unchanged | Red — key plain |
| AC-003 (FR-006) | Restart over its own encrypted keys | HTTP integration | CPK | `AfterARestartOverItsOwnEncryptedKeys_EarlierSessionAndFormTokenAreStillAccepted` | Old session → 200; old form token → sign-in 302 `/` | Red — key plain |
| AC-003 (FR-006) | Restart over its own encrypted keys | HTTP integration | IKE | `AfterARestartOverItsOwnEncryptedKeys_EarlierSessionAndFormTokenAreStillAccepted` | Old session → 200; old form token → sign-in 302 | Red — key plain |
| AC-004 (FR-007) | DC-2 names the first console run under the service account and the ACL check | Inspection | — | — (spec I-4: DC-2 steps 2 and 6, v94) | Present in `deployment-conventions.md` | Manual — inspection |
| AC-005 (FR-004, OD-002 a) | Still-valid plain key from before v94 | Host-build | KRE | `AStillValidPlainKey_IsReadAndLeftAsItIs_AndTheNextKeyIsEncrypted` [both] | Earlier payload unprotected; plain file byte-identical; next key (`IKeyManager.CreateNewKey`) encrypted | Red — new key plain |
| AC-006 (FR-005, OD-003 a, S-02) | Installation told it is not on Windows | Host-build | NWR | `TheInstallation_OffWindows_RefusesToStart_WithAFatalLogLine_AndWritesNoKey` | Build throws; `Fatal` line with `Reason` naming DPAPI; key directory empty | Red — skeleton `NotImplementedException` |
| AC-006 (FR-005, OD-003 a, S-02) | Control Plane told it is not on Windows | Host-build | NWR | `TheControlPlane_OffWindows_RefusesToStart_WithTheReasonInTheException_AndWritesNoKey` | Build throws; message names DPAPI; key directory empty | Red — skeleton `NotImplementedException` |

Manual check outside this matrix: AC-002 under a genuine second Windows account
(spec I-2) — implementation report §3.1, pilot server at deployment.

Regression: `ControlPlane.Security.DataProtectionTests.KeyRing_IsWrittenToConfiguredDirectory`
(US-002) and the US-047 directory-resolution tests keep the key-directory
behaviour (VR-004) — green.
