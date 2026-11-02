---
artifact_type: open_decisions
story: US-070
version: 2
status: DRAFT
created_at: 2026-11-02T19:46:14Z
updated_at: 2026-11-02T19:51:17Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-070-search-in-lists-and-pickers.md
    version: null
  - path: trebovaniya.md
    version: 109
supersedes: docs/decisions/US-070-open-decisions.md@v1
---

# US-070 Open Decisions

The Story's own "Open Decisions" section records none. One was found while writing the Specification.

## OD-001 Organizer email on the Linked and Not-a-course lists

**State:** RESOLVED by the Owner, 2026-11-02.

**The gap.** `trebovaniya.md` Epic 2 (v108) and BR-087 say two things that meet here:

- the Meet codes search covers "code, organizer email, linked course name and section" and "runs over all three lists";
- an organizer's email is searched "only where it is shown on the screen".

Today only the **Unassigned** list shows organizers (US-032 FR-007; `Views/MeetCodes/Index.cshtml`). The Linked and Not-a-course
lists have no organizer column.

**Options.**

- **a) (recommended)** Add an "Organizers" column to the Linked and Not-a-course lists and search organizer emails on all three.
  Every list then answers the same question and both requirement sentences hold. Same roles, same stored data, no new right. Cost: one
  more column in tables that are already wide, and a change to two screens the Story did not name.
- **b)** Search organizer emails on the Unassigned list only, with no screen change. The literal minimum. A Dean searching by a teacher's
  email sees 0 next to Linked even when that teacher has linked codes, and may read it as "none exist".
- **c)** Search on all three without showing the column. Not offered as a real choice: it breaks "only where it is shown", and a row would
  appear with no visible reason.

**Impact.** FR-004 (searchable fields, table columns), AC-004, AC-005, the API design's row models, the read queries. Under every option, an erased or excluded
organizer is never found.

**Resolution:** option a). The Linked and Not-a-course lists get the "Organizers" column, and organizer emails are searched on all three lists. Specification v2, FR-004.
