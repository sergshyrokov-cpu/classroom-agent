---
artifact_type: open_decisions
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:13:00Z
updated_at: 2026-10-31T14:13:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-061 Open Decisions

`trebovaniya.md` section 7 holds no open item this Story depends on. Items 26 and 27 concern
documentation and re-loading a purged leaver; items 14 and 29 concern submissions of removed students
and delegation; item 28 concerns the field names of the Meet `call_ended` event, which this Story
neither reads nor changes. Both Open Decisions below come from the Story and were resolved before
activation; no new one was raised.

## OD-001 Roster date when the last successful read is not known

Carried from the Story. `Course.LastCompleteReadAt` exists only since US-058, so an older marked
course may have none (as US-060 OD-002).

- **a) (recommended)** — show the course mark and the roster line without a date («состав курса — на
  дату последнего чтения»), worded together with US-060 OD-002 so both read the same.
- b) — omit the roster line when there is no date.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-005.1, AC-009.

## OD-002 Is the mark shown on the manual course-choice page?

Carried from the Story. v102 keeps the manual pick of a marked course allowed but does not say
whether the choice page shows the mark.

- **a) (recommended)** — yes, the course's mark with its date next to its name, so the Dean does not
  pick a deleted course by mistake; nothing else on the page changes.
- b) — no; the page stays as it is.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»). Applied in spec FR-003.2, AC-010.
