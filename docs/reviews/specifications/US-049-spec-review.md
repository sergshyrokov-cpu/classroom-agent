---
artifact_type: specification_review
story: US-049
version: 1
status: APPROVED
created_at: 2026-10-16T12:40:00Z
updated_at: 2026-10-16T12:40:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-049-encrypt-data-protection-keys.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/specifications/US-049-spec.md
    version: 1
  - path: docs/specifications/US-049-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-049-open-decisions.md
    version: 1
supersedes: null
---

# US-049 Specification Review

## Summary

**Verdict: PASS** — 0 Critical, 0 Major, 3 Minor.

The Specification restates `trebovaniya.md` §8 v94 faithfully (encryption with
DPAPI bound to the service account; no unencrypted key; no start outside
Windows; pre-v94 keys read until they expire; first console run under the
service account; forced password reset only signs users out). All four Story
ACs are kept with their ids; AC-005 and AC-006 make the resolutions of OD-002
and OD-003 testable. The four interpretations I-1…I-4 are all named in digest
section 2. The Minor findings concern one inaccurate sentence (I-3, spec and
digest), an unsourced performance statement, and a framework behaviour FR-003
relies on that only the tests can confirm.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-049-encrypt-data-protection-keys.md` | — |
| Requirements | `trebovaniya.md` (header v94 entry, §8 "Ключи Data Protection") | 96 |
| Specification | `docs/specifications/US-049-spec.md` | 1 (DRAFT) |
| Digest | `docs/specifications/US-049-spec-digest.ru.md` | 1 (DRAFT) |
| Open Decisions | `docs/decisions/US-049-open-decisions.md` | 1 (DRAFT) |

Also read: `security-conventions.md` SC-7, `deployment-conventions.md` DC-2
(steps 1–6), and for the code facts the Specification cites
`InstallationSecurityServices.cs`, `ControlPlaneSecurityServices.cs`,
`ClassroomAgent.Web/Program.cs`, `ClassroomAgent.ControlPlane/Program.cs`,
`InstallationLogging.cs`.

Inputs are current: the Specification names `trebovaniya.md` v96 and the Story;
the digest names Specification v1.

Delegation: none — every source was read directly.

## Fidelity

| Spec element | Source checked | Result |
|---|---|---|
| FR-001 DPAPI, current user, rest of key ring unchanged | §8 v94 «привязанными к учётной записи, под которой работает служба хоста»; Story scope "current Windows user (not the machine)" | Faithful |
| FR-002 no plain key written | §8 v94 «Незашифрованный ключ не записывается» | Faithful |
| FR-003 unreadable key: start, skip, new key, file untouched | §8 v94 «ключи станут нечитаемыми, и пользователям придётся войти заново; данные при этом не теряются»; DC-2 step 2 "creates new ones and signs everyone out" | Faithful; the skip-not-delete choice is I-1 |
| FR-004 pre-existing plain keys read until expiry | §8 v94 «ключи, записанные до v94, читаются, пока не истекут»; OD-002 a | Faithful ("before this Story" = "before v94" in practice: no host shipped encryption earlier) |
| FR-005 refuse outside Windows | §8 v94 «вне Windows хост не запускается»; OD-003 a | Faithful; reporting path is I-3 |
| FR-006 restart keeps sessions | §8 v64 «Без сохранения ключей каждый перезапуск … разлогинивал бы всех» | Faithful |
| FR-007 DC-2 already carries the steps | DC-2 step 2 (CP console run under service account, "same holds for any console start of the installation", confirm directory access) and step 6 | Confirmed in the file |
| VR-001 no machine binding / other encryptor | Story scope "not the machine"; §8 account binding | Sourced |
| VR-003 no configuration switch | follows from §8 "не записывается" (no path) and OD-003 a | Sourced |
| VR-004 key-directory validation unchanged | US-047 FR-004 | Sourced |
| S-01…S-06 | §8 v64/v94, SC-2, SC-4, SC-7, SC-10, DC-13, AGENTS.md stack | Sourced |
| §9 "No measurable start-up or request cost…" | none | Unsourced — m-2 |

Code facts checked: both `Add…Security` methods call `PersistKeysToFileSystem`
with no `ProtectKeysWith…`; the installation refuses start-up through
`InstallationLogging.WriteStartupRefusal` then rethrows; the Control Plane fails
through `RequiredSetting` exceptions before Serilog is configured; both
`BuildApplication` methods already take `runsAsService` as a parameter, which is
the precedent FR-005 points to. All as stated.

## Story Coverage

| Story AC | Spec AC | Note |
|---|---|---|
| AC-001 | AC-001 | Unchanged wording |
| AC-002 | AC-002 | Behaviour fixed by FR-003 as the Story asks; automated proof by stand-in key, manual check on a genuine second account (I-2) |
| AC-003 | AC-003 | Unchanged |
| AC-004 | AC-004 | Verified by inspection (I-4) |
| OD-002 | AC-005 | New, from the resolution |
| OD-003 | AC-006 | New, from the resolution |

Story scope items (both hosts, never unencrypted on Windows, deployment
instructions, old keys, tests) are all covered. Nothing goes beyond the Story:
the deployment kit README is left outside the commit, as the Story's Notes say.

## Consistency

- No endpoint, role, permission-matrix cell, read-only rule (BR-026), audit
  event (SC-11) or translation entry (NFR-073) is touched; the Specification
  says so explicitly (S-04, §9).
- Consistent with US-047 (start-up refusal paths, `runsAsService` precedent,
  key-directory validation) and with US-050 / US-047 deferred manual checks
  (the I-2 manual check follows the same pattern).
- Consistent with SC-7 and DC-2/DC-13 as updated for v94.

## Open Items

`trebovaniya.md` §7 (v96) has no item about Data Protection keys, DPAPI or
service accounts. OD-001…OD-003 are resolved by the Owner and carried with ids
and resolutions; OD-001's "done" claim (v94 §8, SC-7, DC-2) is confirmed in the
files. No silent resolution.

## Interpretations

1. **I-1** — an undecryptable key is skipped and left on disk, not deleted and
   not fatal. Named in the spec and digest §2.
2. **I-2** — AC-002 proved with an undecryptable key instead of a real second
   account; the real run is a manual check on the pilot server. Named.
3. **I-3** — each host reports the non-Windows refusal through its existing
   start-up failure path (installation log file; Control Plane exception).
   Named (but see m-1).
4. **I-4** — AC-004 needs no repository change; DC-2 already carries it. Named.
5. AC-006 "no file is written to the key directory" and FR-005 "before it can
   write any key" are a strict reading of "незашифрованный ключ не записывается",
   not a separate choice — no further interpretation is hidden.

No interpretation is missing from the digest.

## Digest Fidelity

Every digest statement traces to the Specification; the three resolved ODs are
correctly presented as already decided; the deferred manual check is stated
plainly. The language is plain Russian a non-developer can judge. One sentence
inherits the inaccuracy of spec I-3 (m-1).

## Findings

### Minor

- **m-1 — I-3 mentions the Windows event log for a refusal that only happens
  outside Windows.** Spec §11 I-3 ("visible on the console, and in the Windows
  Application event log for a service") and digest §2 I-3 («в консоли и в
  журнале событий Windows»). The refusal of FR-005 fires only on a non-Windows
  system, where there is neither a Windows service nor a Windows event log; the
  reason is visible on the console (or in whatever captures the process's
  output). Checked against FR-005 itself. Harmless to behaviour; the sentence
  should drop the event-log clause when the spec is next revised.
- **m-2 — §9 "No measurable start-up or request cost beyond one DPAPI call per
  key read and written" has no source.** Checked against
  `non-functional-requirements.md` and §8/§9: no performance figure applies. It
  is descriptive, not a requirement a test can check; advisory only.
- **m-3 — FR-003 relies on ASP.NET Core's own handling of an undecryptable key**
  (skip it as the default key and generate a new one, leaving the file).
  The Specification states the outcome correctly per §8 v94, but whether the
  framework does this without extra code is a fact to be proved by the AC-002
  tests at TEST_WRITING / IMPLEMENTATION, not assumed — including the case where
  the unreadable key is the newest, still-valid default key.

## Verdict

**PASS** — no Critical or Major findings. The three Minor findings go to the
Owner at `HUMAN_SPEC_APPROVAL`; none blocks approval.

```yaml
result:
  verdict: PASS
  stage: SPEC_REVIEW
  story: US-049
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/specifications/US-049-spec-review.md
  next_stage: HUMAN_SPEC_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-1: spec §11 I-3 and digest §2 I-3 mention the Windows event log for a refusal that only happens outside Windows"
    - "m-2: spec §9 performance statement has no source (descriptive only)"
    - "m-3: FR-003 relies on ASP.NET Core skipping an undecryptable key and generating a new one; AC-002 tests must prove it, incl. the newest-default-key case"
```
