---
artifact_type: open_decisions
story: US-048
version: 1
status: APPROVED
created_at: 2026-10-15T18:25:22Z
updated_at: 2026-10-15T19:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-048-dean-lockout-counter-reset.md
    version: null
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-048 Open Decisions — A Dean's failed-attempt count starts again when a lockout begins

The Story carries one Open Decision, resolved by the Owner before activation.
No new Open Decision was raised while writing the Specification.

`trebovaniya.md` §7 (v96) has no open item this Story depends on: items 10, 14,
26, 27 and 28 are open; none concerns sign-in, the failed-attempt count or the
lockout.

## OD-001 Two wrong passwords at the same moment

**Question.** The pre-pilot security audit also found (I-1) that two wrong
passwords for the same Dean arriving at the same moment make the second one fail
with the error page (`500`) instead of the common refusal: the account's
concurrency token rejects the second save and nothing catches it. No data leaks
and the count stays correct. The Owner's sign-in handles the same case by
re-reading the account and running the sequence again (up to 3 times). Fix it in
this Story?

- **(a) Yes, in this Story (recommended)** — the same code path and the same
  tests; the Dean sees the normal refusal message, like the Owner does.
- (b) No — leave it as an informational note.

**Status:** RESOLVED

**Resolution (the Owner, 2026-10-15):** (a) — fixed in this Story. Two wrong
passwords for the same Dean at the same moment both get the common refusal
message, never the error page, and the count still rises by exactly the number
of failures, as the Owner's sign-in does (see AC-005).

**Impact on the Specification:** FR-003…FR-006, AC-005…AC-009, interpretations
I-2…I-5.
