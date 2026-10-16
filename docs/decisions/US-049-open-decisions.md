---
artifact_type: open_decisions
story: US-049
version: 1
status: APPROVED
created_at: 2026-10-16T12:20:00Z
updated_at: 2026-10-16T12:50:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-049-encrypt-data-protection-keys.md
    version: null
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-049 Open Decisions — Session-signing keys are encrypted at rest

All three Open Decisions of the Story were resolved by the Owner on 2026-10-15
and are carried here with their ids and resolutions. No new Open Decision was
raised while writing the Specification.

`trebovaniya.md` §7 (v96) has no open item this Story depends on; none concerns
the Data Protection keys, DPAPI or the Windows account a service runs under.

## OD-001 Record the rule in the requirements first? — RESOLVED (a)

**Question.** Encrypting the keys at rest is a security rule; does it go into
`trebovaniya.md` before the Story is activated?

- **(a) Yes — a new requirements version adds it to §8 (and SC-7, DC-2) first.**
- (b) No — the Story alone defines it.

**Resolution (the Owner, 2026-10-15):** (a). Done: `trebovaniya.md` v94 §8
("Ключи Data Protection"), `security-conventions.md` SC-7 and
`deployment-conventions.md` DC-2 steps 2 and 6 carry the rule.

**Impact on the Specification:** the Specification cites §8 v94 as the rule
(FR-001 … FR-006) and treats the DC-2 text as already in place (FR-007, I-4).

## OD-002 Keys already on disk without encryption — RESOLVED (a)

**Question.** A host deployed before this Story has unencrypted keys. What
happens to them?

- **(a) Nothing special — they keep being read and expire on their own
  schedule; every new key is written encrypted.**
- (b) The deployment instructions say to delete the key folder once after the
  upgrade.

**Resolution (the Owner, 2026-10-15):** (a).

**Impact on the Specification:** FR-004, AC-005.

## OD-003 A host started on a non-Windows system — RESOLVED (a)

**Question.** DPAPI exists only on Windows. What does a host do elsewhere?

- **(a) It refuses to start with a clear log line; no path writes an unencrypted
  key.**
- (b) Outside Windows the keys stay unencrypted.

**Resolution (the Owner, 2026-10-15):** (a).

**Impact on the Specification:** FR-005, AC-006.
