---
artifact_type: open_decisions
story: US-046
version: 2
status: APPROVED
created_at: 2026-10-13T17:42:15Z
updated_at: 2026-10-13T17:44:49Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-046-page-search-hint.md
    version: null
  - path: trebovaniya.md
    version: 91
supersedes: 1
---

# US-046 Open Decisions — Page-search hint on teaching-data pages

Two, both resolved by the Owner on 2026-10-13: OD-001 from the Story, OD-002
raised while writing the Specification and resolved at HUMAN_SPEC_APPROVAL.
`trebovaniya.md` §7 (v91) has no open item this Story depends on.

## OD-001 The Meet code course-choice page

The page where a Dean or Admin picks a course for a Meet code
(`/workspace/meet-codes/{meetingCode}/…`) shows the whole course list, not
paged. `trebovaniya.md` v91 names "коды встреч Meet" but does not say whether
this choice page counts as a teaching-data page or as a form.

- a) (Recommended) No hint — it is a form for one action, like the forms §5
  excludes.
- b) The ordinary hint — the course list on it can be long, and browser search
  works there because it is not paged.

**Resolved (Owner, 2026-10-13): a)** — no hint on the course-choice page.
Impact: FR-005, AC-005.

## OD-002 Where the hint goes on the Meet codes page

**Resolved (Owner, /so:approve, 2026-10-13): a)** — the hint goes directly
under the page's own (second) title; no title changes.

The shared layout renders the page title at the top of every installation page.
The Meet codes page (`MeetCodes/Index`) also renders the same title a second time
as its own heading, so the title appears twice. `trebovaniya.md` §5 (v91) puts
the hint "сразу под заголовком страницы", and the Story forbids changing page
titles (AC-007). On every other hint page the title appears once.

- **a) (Recommended)** Under the page's own (second) title — the hint sits
  directly above the page content, as on every other page; no title changes.
- b) Under the layout title, as on every other page — the page then reads
  title / hint / the same title again.
- c) Remove the duplicate title from the Meet codes page and place the hint as
  on every other page — a title change outside the Story's scope; needs the
  Owner's explicit approval; the course-choice page keeps its duplicate.

Impact: FR-002, AC-003, AC-009 — Meet codes page only.
