---
artifact_type: open_decisions
story: US-020
version: 2
status: DRAFT
created_at: 2026-10-11T10:23:09Z
updated_at: 2026-10-11T16:00:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-020-course-list.md
    version: null
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-020 Open Decisions — Course list with filters and search

Eleven decisions (OD-001 … OD-011) were written into the Story by the author and
**resolved by the Owner on 2026-10-11, before activation** (US-020 discovery),
each as option (a). They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised **no new Open Decision**. TEST_WRITING raised
OD-012, resolved by the Owner the same day. The choices the
Story and `trebovaniya.md` leave open are stated as interpretations I-1 … I-9
in `docs/specifications/US-020-spec.md` §11; each can be corrected at
`HUMAN_SPEC_APPROVAL`, and any of them may be turned into an Open Decision
there.

No item of `trebovaniya.md` §7 (v88) blocks this Story. The open items are 10
(minimum Google Workspace roles), 14 (submissions of a removed student), 26
(documentation), 27 (re-import of a purged leaver) and 28 (Meet `call_ended`
fields). None concerns which courses exist, their state, or who is a current
teacher or student on a roster; item 27 may change how many memberships a
re-imported leaver has, which this Story only counts when `OnRoster` is true.

---

## Carried from the Story

### OD-001 Row contents

**Resolution:** (a) — name and section, status, teachers, number of students in
the roster, last update time in Google. Owner, 2026-10-11.

### OD-002 Status filter

**Resolution:** (a) — ACTIVE by default; any of the five states or "all".
Owner, 2026-10-11.

### OD-003 Teacher

**Resolution:** (a) — anyone with the teacher role currently in the course's
roster, owner and co-teachers alike. Owner, 2026-10-11.

### OD-004 Teacher name

**Resolution:** (a) — "Прізвище Ім'я" from the Google profile, email when there
is no name. Owner, 2026-10-11.

### OD-005 Search

**Resolution:** (a) — substring, case-insensitive, in the name and the section.
Owner, 2026-10-11.

### OD-006 Paging

**Resolution:** (a) — 50 per page, sorted by name. Owner, 2026-10-11.

### OD-007 Links

**Resolution:** (a) — to the journal and the Meet reports with the course
chosen; their drop-downs stay. Owner, 2026-10-11.

### OD-008 Classroom link

**Resolution:** (a) — no "Open in Classroom" link. Owner, 2026-10-11.

### OD-009 Filters in the address

**Resolution:** (a) — filters in the address; the teacher as an internal id; the
language switcher's return-path override set (US-025 I-2). Owner, 2026-10-11.

### OD-010 Navigation

**Resolution:** (a) — "Courses" is the first menu entry and the first link on the
home page. Owner, 2026-10-11. (Specification I-1 reads both as the first link of
the home page's Google Workspace section.)

### OD-011 Export

**Resolution:** (a) — no export of the list in this Story. Owner, 2026-10-11.

---

## Raised at TEST_WRITING

### OD-012 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist yet:
`ICourseListSource` and its five records, `CourseListRequest`, the outcome and
result, the page DTOs and enums of `US-020-openapi.yaml`, `GetCourseListQuery`,
`CourseListUrl`, `CourseListSource`, the policy name `ViewCourses` and the route
constant `SignInRoutes.Courses`. The test-writer may not change production code.
US-025 … US-044 resolved the same gap the same way.

Options: (a) a compile-only skeleton — declarations only; members throw
`NotImplementedException`; nothing registered in DI, no migration, no existing
behaviour changed; IMPLEMENTATION owns and completes it; (b) no skeleton.

**Resolution:** (a) — Owner, 2026-10-11.

Naming note: the openapi schema `PersonName` is the C# record
`CourseListPersonName` (with `CourseListPersonNameKind`), because US-027's
`PersonName` already exists in `Application.Models.Dtos` with a different shape.
Fields and meaning are the contract's.
