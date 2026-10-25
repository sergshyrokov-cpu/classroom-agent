---
artifact_type: open_decisions
story: US-055
version: 1
status: APPROVED
created_at: 2026-10-25T10:10:00Z
updated_at: 2026-10-25T11:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: trebovaniya.md
    version: 97
supersedes: null
---

# US-055 Open Decisions — Points and grades of any size are stored as Google returned them

No Open Decision is unresolved. The one below is carried from the Story with the
Owner's resolution of 2026-10-25. No item of `trebovaniya.md` §7 bears on this
Story.

## OD-001 Widen the columns only, or also change what the run does after a failure? — RESOLVED

Carried from the Story (its "Open Decisions" section records the decision
without an id; it is numbered here).

**Question.** A value that overflows a column stops the whole run at the same
course on every run (US-017 FR-005). Fix only the columns, or also let the run
skip a failing course and continue?

- **(a) Only make the three columns fit any value; the rule that an unexpected
  error stops the run stays as it is (recommended).**
- (b) Also change the run so that one failing course does not stop the others.

**Resolution (the Owner, 2026-10-25):** (a).

**Impact:** FR-001 … FR-005 change storage only; FR-004 and section 10 keep
US-017 FR-005 and US-053 OD-001 unchanged.
