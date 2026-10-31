---
artifact_type: api_design
story: US-061
version: 1
status: DRAFT
created_at: 2026-10-31T14:31:47Z
updated_at: 2026-10-31T14:31:47Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-061 API Design — Meet reports of a marked course, and a marked course is not a Meet link candidate

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-061-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact; earlier Stories with no API change (US-052 …
US-060, US-063) were handled the same way. Downstream stages that list `openapi` among their inputs
read this document instead.

Delegation: none. The designer read the Specification directly and checked the routes, policies and
return types of `MeetReportsController`, `MeetCodesController` and `MeetExportController`.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the
Story does not change public API behaviour. The approved Specification (v1) states it:

- **§1:** "Nothing is read from Google, no schema changes, no endpoint is added."
- **§6:** "No new input: no endpoint, query parameter, form field or upload changes."
- **§7:** "No endpoint, page, role, policy or permission changes."
- **§8:** "No new error … The export's error contract is unchanged."
- **§9:** "API: no endpoint change; API_DESIGN expected NOT_APPLICABLE."

## 2. What changes, and why none of it is API contract

- **Meet reports pages** (`MeetReportsController`, `GET` `SignInRoutes.MeetReports` and
  `SignInRoutes.MeetReportMeeting`, policy `ViewMeetReports`). Server-rendered Razor pages
  (`return View(page)`); the notice line travels in the Application DTOs the pages already receive
  (FR-005.6, AD-8). No JSON endpoint serves them.
- **"Unlinked meetings" and course-choice pages** (`MeetCodesController`, `GET`
  `SignInRoutes.MeetCodes`, `GET` `SignInRoutes.MeetCodeCourseChoice`). Razor views; the candidate
  set shrinks (FR-002) and the choice page shows a mark next to a course name (FR-003.2). The form
  posts (`MeetCodeLink`, `MeetCodeConfirmation`, `MeetCodeNotACourseMark`, policy `LinkMeetCodes`)
  keep their fields, guard, status codes and audit (FR-003.3).
- **Meet exports** (`MeetExportController`, `POST /api/v1/exports/meet-reports-xlsx` and
  `POST /api/v1/exports/meet-meeting-xlsx`). Route, method, parameters, authorization policy,
  status codes, media type, file name and error body stay the same. Only the workbook content
  changes: text in the third cell of the Course header row (FR-005.3). The file's content is not
  part of the HTTP contract.
- **Automatic linking** runs inside synchronization; no endpoint is involved (FR-001).

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-003, AC-008, AC-009, AC-011 | existing Meet reports pages and the two existing Meet export endpoints, unchanged contracts |
| AC-004, AC-007 | none — synchronization's Meet step |
| AC-005, AC-010 | existing "unlinked meetings" and course-choice pages |
| AC-006 | existing `MeetCodeLink` form post, unchanged |

## 4. Open findings

US-061 has no OPEN finding; this stage closes none.
