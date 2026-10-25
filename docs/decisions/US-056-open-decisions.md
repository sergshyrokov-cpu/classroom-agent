---
artifact_type: open_decisions
story: US-056
version: 1
status: APPROVED
created_at: 2026-10-25T14:45:00Z
updated_at: 2026-10-25T14:45:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: trebovaniya.md
    version: 98
supersedes: null
---

# US-056 Open Decisions — Members seen when a course is first loaded count from the course's creation

No Open Decision is unresolved. The five below are carried from the Story's "Open
Decisions" section, which records them without ids; they are numbered here in the
Story's order. The Owner chose option 1 on 2026-10-23 and answered 1a–5a on
2026-10-25; all five are written into `trebovaniya.md` v98. No item of
`trebovaniya.md` §7 bears on this Story.

## OD-001 Do people found through submissions at the first load count from creation? — RESOLVED

**Resolution (the Owner, 2026-10-25):** (a) yes, like the roster.
**Impact:** FR-001 a, AC-005.

## OD-002 Start when Google gave no `creationTime` — RESOLVED

**Resolution (the Owner, 2026-10-25):** (a) the date of the run that first loaded
the course.
**Impact:** FR-001 b, AC-006.

## OD-003 Multi-year courses with changing students — RESOLVED

**Resolution (the Owner, 2026-10-25):** (a) accepted as a known limitation.
**Impact:** Specification section 10 (Out of Scope).

## OD-004 Do memberships stored before v98 follow the rule? — RESOLVED

**Resolution (the Owner, 2026-10-25):** (a) yes.
**Impact:** FR-005, AC-009, I-3.

## OD-005 Where do the US-055 extreme-value rules go? — RESOLVED

**Resolution (the Owner, 2026-10-25):** (a) into the same requirements version
(v98), delivered by a separate Story (US-057).
**Impact:** Specification section 10 (Out of Scope).
