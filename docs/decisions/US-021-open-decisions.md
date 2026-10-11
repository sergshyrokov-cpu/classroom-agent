---
artifact_type: open_decisions
story: US-021
version: 2
status: DRAFT
created_at: 2026-10-11T16:02:42Z
updated_at: 2026-10-11T16:30:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-021-course-detail.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-021 Open Decisions — Course detail with roster

Twelve decisions (OD-001 … OD-012) were written into the Story by the author and
**resolved by the Owner on 2026-10-11, before activation** (US-021 discovery),
each as option (a). They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **no new Open Decision**. The choices the Story
and `trebovaniya.md` leave open are stated as interpretations I-1 … I-9 in
`docs/specifications/US-021-spec.md` §11; each can be corrected at
`HUMAN_SPEC_APPROVAL`, and any of them may be turned into an Open Decision
there.

No item of `trebovaniya.md` §7 (v88) blocks this Story. The open items are 10
(minimum Google Workspace roles), 14 (submissions of a removed student), 26
(documentation), 27 (re-import of a purged leaver) and 28 (Meet `call_ended`
fields). Items 14 and 27 concern **whether** synchronization creates an
off-roster membership for a submitter or re-creates a purged leaver; the card
only shows the memberships that are stored, whatever their origin, so either
answer changes the data shown, not this Story's behaviour.

---

## Carried from the Story

### OD-001 Course details

**Resolution:** (a) — name, section, status, room, description heading and
description, creation and update times; no teacher folder or calendar id.
Owner, 2026-10-11.

### OD-002 Classroom link

**Resolution:** (a) — no "Open in Classroom" link, as in US-020 OD-008.
Owner, 2026-10-11.

### OD-003 Owner

**Resolution:** (a) — marked "owner" when a current teacher; otherwise not
shown. Owner, 2026-10-11.

### OD-004 Roster sections

**Resolution:** (a) — two sections, "Teachers" and "Students", each with a
count. Owner, 2026-10-11.

### OD-005 Leavers

**Resolution:** (a) — leavers in a separate section with the date last seen in
the roster. Owner, 2026-10-11.

### OD-006 Person

**Resolution:** (a) — name by the US-020 OD-004 rule, and the email.
Owner, 2026-10-11.

### OD-007 Order and paging

**Resolution:** (a) — sorted by name; no paging. Owner, 2026-10-11.

### OD-008 Roster search

**Resolution:** (a) — no roster search. Owner, 2026-10-11.

### OD-009 Links

**Resolution:** (a) — the course name in the list links to the card; the card
links to the journal, the Meet reports and the list (default filters; Back keeps
them). Owner, 2026-10-11.

### OD-010 Unknown course

**Resolution:** (a) — an unknown or malformed course id gets 404.
Owner, 2026-10-11.

### OD-011 Export

**Resolution:** (a) — no roster export. Owner, 2026-10-11.

### OD-012 Last synchronization time

**Resolution:** (a) — no last-synchronization time on the card.
Owner, 2026-10-11.

## Raised at TEST_WRITING

### OD-013 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet:
`ICourseCardSource` and its two records, `CourseCardRequest`,
`CourseCardOutcome`, `CourseCardResult`, the card DTOs of `US-021-openapi.yaml`,
`GetCourseCardQuery` and `CourseCardSource`. The test-writer may not change
production code. US-020 (OD-012) and the Stories before it resolved the same gap
the same way.

Options: (a) a compile-only skeleton — declarations only, exactly as the entity
model §2, §3, §5 names them; members throw `NotImplementedException`; nothing
registered in DI, no route, no view, no migration, no existing behaviour
changed; IMPLEMENTATION owns and completes it; (b) no skeleton — the tests do
not compile until IMPLEMENTATION and the red phase cannot be verified.

**Resolution:** (a) — Owner, 2026-10-11.
