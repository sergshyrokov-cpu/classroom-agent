---
artifact_type: open_decisions
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T18:35:09Z
updated_at: 2026-10-27T18:35:09Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: trebovaniya.md
    version: 103
supersedes: null
---

# US-062 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on (items 14, 26–29 concern
documentation, re-loading a purged leaver, Meet fields and delegation with 2SV). No new Open Decision
was raised while writing the Specification; its choices where the requirements leave room are listed
as interpretations I-1…I-11 in the Specification, section 11.

## OD-001 Which pages carry the banner

Carried from the Story. "Every installation page" (BR-047); pages a signed-in user sees are meant.

- **a) (recommended)** — every page rendered for a signed-in Admin or Dean except the forced password
  change: that page exists only to change the password, and its link would lead nowhere until the
  password is changed.
- b) — every page of the signed-in layout without exception.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-003.1, AC-015.

## OD-002 The empty-list date of an installation upgraded while the flag is set

Carried from the Story. At migration a stored `LastCourseListEmpty = true` has no date.

- **a) (recommended)** — take the start of the last run as the date: the latest certain instant; it
  understates the duration, never invents a longer one.
- b) — leave it without a date until the streak ends.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-008.2, AC-016.
