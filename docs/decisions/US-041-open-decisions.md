---
artifact_type: open_decisions
story: US-041
version: 2
status: DRAFT
created_at: 2026-10-05T07:16:54Z
updated_at: 2026-10-05T07:23:00Z
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
API_DESIGN raised OD-002 (finding API F-1), resolved by the Owner on
2026-10-05.

## OD-001 The Control Plane before setup

**Status:** resolved — `404`.

**Question.** Before the Owner account exists, should the Control Plane answer
an unknown file-like path with `404` or with the setup gate's `302 /setup`?

**Impact.** FR-002, AC-002 of the Specification.

**Resolution (the Owner, 2026-10-05):** `404`. One rule — an unknown address
answers `404` to anyone (SC-4 v66) — with no first-run exception; the setup
gate already leaves unmatched requests to the `404` handling, and only a person
setting the Control Plane up needs `/setup`, which they reach from `/`.

## OD-002 The API-6 body for an unmatched path under `/api/v1`

**Status:** resolved — out of scope for US-041.

**Question.** Specification v1 (FR-001, FR-002, §6, §8) required an unmatched
path under `/api/v1` to answer `404` with the API-6 body, as SC-4 v66 states.
Neither host does that today for any unmatched path — the status-code
re-execution renders the HTML error page — and neither host has an `/api/v1`
resource. Should US-041 add that mechanism, or keep to file-like paths?

**Options.**

- **(1) Narrow the Specification.** Under `/api/v1` an unmatched path keeps
  answering `404` with the error page; the first Story that adds an `/api/v1`
  resource to a host makes its unmatched `/api/v1` paths answer with the API-6
  body. *Recommended.*
- **(2) Widen the Story.** Both hosts' error handling returns the API-6 body
  when the original path is under `/api/v1`.

**Impact.** FR-001, FR-002, §6, §8, §10 of the Specification.

**Resolution (the Owner, 2026-10-05):** (1). Specification v2 narrows the
Story; the gap is recorded in its §10 Out of Scope.
