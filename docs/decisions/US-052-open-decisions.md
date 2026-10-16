---
artifact_type: open_decisions
story: US-052
version: 1
status: APPROVED
created_at: 2026-10-16T13:42:23Z
updated_at: 2026-10-16T13:51:39Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-052-forbidden-role-tests.md
    version: null
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-052 Open Decisions — Every Admin-only and Dean-only write has a forbidden-role test

None. The Story's own "Open Decisions" section says none: the required behaviour
is the existing `trebovaniya.md` §2 matrix, SC-4 and TC-5. `trebovaniya.md` §7
(v96) has no open item this Story depends on.

The Specification's interpretations (I-1…I-7) are listed in its §12 and in the
Russian digest for the Owner to confirm at `HUMAN_SPEC_APPROVAL`. If the matrix
test later exposes an endpoint whose policy contradicts its §2 row, it is
recorded here as a new Open Decision (Specification FR-015).
