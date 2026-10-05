---
artifact_type: open_decisions
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:16:54Z
updated_at: 2026-10-05T07:16:54Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-041-unknown-file-path-404.md
    version: null
  - path: trebovaniya.md
    version: 85
supersedes: null
---

# US-041 Open Decisions — An unknown file-like address answers 404 to anyone

The Story records one Open Decision, OD-001, already resolved by the Owner.
Writing the Specification raised no new one.

## OD-001 The Control Plane before setup

**Status:** resolved — `404`.

**Question.** Before the Owner account exists, should the Control Plane answer
an unknown file-like path with `404` or with the setup gate's `302 /setup`?

**Impact.** FR-002, AC-002 of the Specification.

**Resolution (the Owner, 2026-10-05):** `404`. One rule — an unknown address
answers `404` to anyone (SC-4 v66) — with no first-run exception; the setup
gate already leaves unmatched requests to the `404` handling, and only a person
setting the Control Plane up needs `/setup`, which they reach from `/`.
