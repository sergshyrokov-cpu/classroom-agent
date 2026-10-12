---
artifact_type: open_decisions
story: US-022
version: 2
status: DRAFT
created_at: 2026-10-12T08:58:14Z
updated_at: 2026-10-12T09:30:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-022-teachers-and-their-courses.md
    version: null
  - path: trebovaniya.md
    version: 90
supersedes: null
---

# US-022 Open Decisions — Teachers and their courses

Nine decisions (OD-001 … OD-009) were written into the Story by the author and
**resolved by the Owner on 2026-10-12, before activation** (US-022 discovery),
each as recommended. They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **no new Open Decision**. The choices the Story
and `trebovaniya.md` leave open are stated as interpretations I-1 … I-7 in
`docs/specifications/US-022-spec.md` §11; each can be corrected at
`HUMAN_SPEC_APPROVAL`, and any of them may be turned into an Open Decision
there.

No item of `trebovaniya.md` §7 (v90) blocks this Story. The open items are 10
(minimum Google Workspace roles), 14 (submissions of a removed student), 26
(documentation), 27 (re-import of a purged leaver) and 28 (Meet `call_ended`
fields). Items 14 and 27 concern student memberships created off the roster;
the teacher list counts only current teacher memberships, so neither answer
changes this Story's behaviour.

---

## Carried from the Story

### OD-001 One teacher list page

**Resolution:** one teacher list page; each row links to the US-020 course list
filtered by the teacher. No teacher card. Owner, 2026-10-12.

### OD-002 Who is a teacher

**Resolution:** anyone currently in a course roster with the teacher role, owner
and co-teachers alike (US-020 OD-003); people who left every course are not
listed. Owner, 2026-10-12.

### OD-003 Status filter

**Resolution:** a course status filter, ACTIVE by default; the listing, the
course count and the link follow the chosen status. Owner, 2026-10-12.

### OD-004 Row

**Resolution:** name (US-020 OD-004 rule), email, number of courses. No other
column. Owner, 2026-10-12.

### OD-005 No name search

**Resolution:** no search by name or email — it would put personal data in the
address, the logs and the language switcher's return path. Owner, 2026-10-12.

### OD-006 Order and paging

**Resolution:** sorted by the shown name, 50 per page, as in US-020 OD-006.
Owner, 2026-10-12.

### OD-007 Menu

**Resolution:** "Teachers" is the second menu entry, after "Courses", and the
second home-page link. Owner, 2026-10-12.

### OD-008 No links from teacher names elsewhere

**Resolution:** teacher names on the course list and the course card stay plain
text. Owner, 2026-10-12.

### OD-009 No export

**Resolution:** the teacher list has no export. Owner, 2026-10-12.

---

## Raised by the Specification

None.

---

## Raised by TEST_WRITING

### OD-010 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet:
`ITeacherListSource`, `TeacherListRecord`, `TeacherListRequest`,
`GetTeacherListQuery`, `TeacherListPageResult`, `TeacherListPageOutcome`,
`TeacherListUrl`, the DTOs `TeacherListPageModel`, `TeacherListResult`,
`TeacherListRow`, `TeacherListMessageKey` (entity model §2–§4), and the
`Infrastructure` class `TeacherListSource`. The test-writer may not change
production code. US-020 … US-044 resolved the same gap the same way.

Options: (a) a compile-only skeleton — declarations only; members that would do
work throw `NotImplementedException`; nothing registered in DI, no route, no
migration, no existing behaviour changed; IMPLEMENTATION owns and completes it;
(b) no skeleton — the tests do not compile until IMPLEMENTATION and the red
phase cannot be verified.

**Resolved (a)** — Owner, 2026-10-12.
