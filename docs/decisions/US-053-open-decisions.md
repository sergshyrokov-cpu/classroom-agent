---
artifact_type: open_decisions
story: US-053
version: 1
status: APPROVED
created_at: 2026-10-16T15:32:00Z
updated_at: 2026-10-16T16:02:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-053-failed-sync-run-recorded.md
    version: null
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-053 Open Decisions — A failed synchronization run is always recorded as failed

No Open Decision is unresolved. `trebovaniya.md` §7 holds no open item about
how a failed run is recorded; the required behaviour is §4 "Состояние
синхронизации" (v80) as US-013 FR-006, US-017 FR-005/FR-006/FR-010, US-031
FR-010 and US-032 FR-006 already state it.

## OD-001 Stop the run, or skip the failing course and go on? — RESOLVED

Carried from the Story unchanged.

**Question.** Today a failure that is not "course gone" stops the whole run
(US-017 FR-005): the courses after the failing one and the Meet step are not
reached. This Story keeps that and only makes the failure visible. The other
option is to skip a course whose write the database rejects and continue, like a
course that vanished — the rest of the school's data and Meet keep loading, but a
course would be silently missing apart from a counter.

- **(a) Keep stopping the run; this Story only records the failure
  (recommended).** The smallest change, no new requirements version; the Admin
  sees the failure at once, and the pilot shows whether such failures really
  happen before the behaviour is changed.
- (b) Skip the course and continue. Changes behaviour the school sees, so it
  would need a new `trebovaniya.md` version and changes to US-017 FR-005 first.

**Resolution (the Owner, 2026-10-16):** (a) — the run still stops at the
failure (US-017 FR-005 unchanged); this Story only makes the failure recorded and
visible. Whether to skip a failing course instead is reconsidered after the pilot,
if such failures actually occur.

**Impact on the Specification:** FR-001 keeps the stop; no requirement of this
Specification changes what a run loads.
