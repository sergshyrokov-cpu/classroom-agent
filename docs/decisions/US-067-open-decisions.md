---
artifact_type: open_decisions
story: US-067
version: 1
status: APPROVED
created_at: 2026-10-29T08:09:29Z
updated_at: 2026-10-29T08:16:59Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-067-admin-changes-dean-login.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-067 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 26, 27, 14, 28, 29
concern documentation, re-loading a purged leaver, submissions of a removed student, Meet fields
and delegation with blocked sign-in). No new Open Decision was raised while writing the
Specification; its interpretations are listed in spec §11 for confirmation at
HUMAN_SPEC_APPROVAL.

## OD-001 Changing the login of a disabled Dean

Carried from the Story.

- **a) (recommended)** — allowed, and the account stays disabled: the Admin can bring the login up
  to date before re-enabling, and the change does not quietly let anyone back in.
- b) — refused while disabled: the Admin re-enables first, which briefly lets the Dean in under the
  old address.

**Resolved 2026-10-28 by the Owner: a)** («a, a»). Applied in spec FR-003, AC-006.

## OD-002 Entering the same address the account already has

Carried from the Story.

- **a) (recommended)** — nothing is written (no audit event, no stamp rotation) and the page says
  the login is unchanged — like an Owner status change that changes nothing (`trebovaniya.md` §9
  v71).
- b) — treated as a change: audited, and the Dean's open session ends.

**Resolved 2026-10-28 by the Owner: a)** («a, a»). Applied in spec FR-006.
