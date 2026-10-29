---
artifact_type: open_decisions
story: US-064
version: 1
status: APPROVED
created_at: 2026-10-29T06:26:20Z
updated_at: 2026-10-29T07:07:33Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-064-disabled-dean-session-refused-on-next-request.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-064 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 26, 27, 14, 28, 29
concern documentation, re-loading a purged leaver, submissions of a removed student, Meet fields and
delegation with blocked sign-in). No new Open Decision was raised while writing the Specification;
its interpretations are listed in spec §11 for confirmation at HUMAN_SPEC_APPROVAL.

## OD-001 Pin the Admin-revocation limitation with a test

Carried from the Story.

v103 records that revoking an Admin in `AllowedAdmin` does not end their open session (known
limitation, `trebovaniya.md` §2 lines 684–687, BR-012, SC-3). A test could assert that the Admin's
session still works after revocation.

- **a) (recommended)** — no test: a limitation is something we accept, not a behaviour we promise;
  a test would turn it into a requirement and fail the day someone improves it.
- b) — a test asserting the current behaviour, renamed and inverted when the limitation is lifted.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Impact: no requirement of the
Specification; recorded in spec §10 Out of Scope.
