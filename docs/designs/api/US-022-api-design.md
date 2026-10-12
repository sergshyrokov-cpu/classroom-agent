---
artifact_type: api_design
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T09:02:27Z
updated_at: 2026-10-12T09:02:27Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/decisions/US-022-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 90
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
supersedes: null
---

# US-022 API Design — Teachers and their courses

Companion to `docs/designs/api/US-022-openapi.yaml`.

Delegated: nothing to subagents at this stage. The US-020 contract and API
design, `SignInRoutes`, `CoursesController`, `CourseListUrl`, the home page view
and the `Courses.*` keys of `SharedResource.en.resx` were read directly; the
YAML was checked by a parser.

## 1. Scope of the contract

| Operation | Change | Policy | Calls Google | Writes |
|---|---|---|---|---|
| `GET /workspace/teachers` | **new** — status form and one page of teachers | `ViewCourses` (existing) | no | nothing |
| `GET /` (US-008) | additive markup — "Teachers" second in the Google Workspace section | `ViewLegitimacyStatus` (unchanged) | no | nothing |
| `GET /workspace/courses` (US-020) | **none** — the target of every row link | `ViewCourses` (unchanged) | no | nothing |

No `/api/v1` path, no `POST`, no Control Plane change, no private-port change.
`ContractVersion.Current` stays **1**.

## 2. Decisions this stage made

### 2.1 Path `/workspace/teachers`, route constant `SignInRoutes.Teachers`

The spec's indicative route is kept. The page sits in the Google Workspace
section beside `/workspace/courses` and follows its pattern — one `GET` page
whose form submits to itself by `GET`. A plural noun, per API-1. It needs no
sub-path: there is no teacher card (OD-001).

### 2.2 Parameters `status` and `page`, the course list's rules

Both parameters keep the course list's names, tokens and rules (spec VR-001,
VR-002): `status` ∈ `active`, `archived`, `provisioned`, `declined`,
`suspended`, `all`, case-insensitive, emitted lower-case; `page` from 1 to
100 000. The same tokens mean a status chosen on the teacher list carries over
to the course list unchanged in the row link. Implementation should reuse the
course list's parsing rather than copy it; how is left to IMPLEMENTATION.

### 2.3 Raw query collected by the host, decided in `Application`

As for the course list (US-020 api-design §2.3): the controller hands every
occurrence of `status` and `page` to `GetTeacherListQuery`, which decides
"exactly one value". Other parameters are not collected, so they cannot be
echoed (spec VR-003) — a hand-typed `teacherId` or `q` is simply ignored.

### 2.4 Status codes

- `200` — the page, including the empty message and a page past the last.
- `400` — `status` and/or `page` malformed; **this page** with the form and
  messages, no list (spec FR-010). Same choice as the course list.
- No `404`: there is no identifier in the query or the path. An unknown
  parameter is ignored, not refused.
- `302` — the host's existing answers for an anonymous request and for a Dean on
  the forced password change.
- `500` — the single error page.

### 2.5 One empty state, `isEmpty`

The spec has one empty message (FR-008, I-2), so the result carries a boolean
rather than the course list's two-member `emptyState`. New key
`Teachers.Empty`.

### 2.6 The row link is built in `Application`

`TeacherListRow.coursesUrl` is produced by the existing `CourseListUrl.Build`
(effective status, `teacherId` = participant id, no search, page 1), so the
view never assembles a URL and the link uses exactly the course list's tokens
and defaults. Example: ARCHIVED filter, participant 317 →
`/workspace/courses?status=archived&teacherId=317`; ACTIVE →
`/workspace/courses?teacherId=317`.

### 2.7 `TeacherListUrl`

A second URL rule in `Application`, beside `CourseListUrl`, builds the
teacher list's paging links and return path: `status`, `page` in that order,
each omitted at its default. It is the only source of teacher-list URLs
(spec FR-009, S-05, S-06).

### 2.8 Message keys and translations

`TeacherListMessageKey` has two members, `StatusMalformed` and `PageMalformed`,
rendered with the existing `Courses.Validation.StatusMalformed` and
`Courses.Validation.PageMalformed` texts — the problem and the wording are the
same. Reused unchanged: `Courses.Status.*`, `Courses.Form.Status`,
`Courses.Form.Submit`, `Courses.Teacher.NoName`, `Courses.Paging.Previous`,
`Courses.Paging.Next`. New keys (both resource files, spec FR-015), names
indicative: `Teachers.NavigationEntry`, `Teachers.Title`,
`Teachers.Column.Name`, `Teachers.Column.Email`, `Teachers.Column.Courses`,
`Teachers.Column.Link`, `Teachers.CoursesLink`, `Teachers.Paging.Total`,
`Teachers.Empty`.

### 2.9 Logging

As spec FR-016. The `Information` line carries actor id, effective status, page
shown and total; each `Warning` line carries the parameter name and the message
key. Neither carries a value from the query, a name or an email.

## 3. Operation notes

### `GET /workspace/teachers`

Evaluation order: authentication → restricted session → `ViewCourses` → shape
of `status` and `page` → count and page of teachers. On `400` nothing is read
from the database. The view model is `TeacherListPageModel`; `list` is null on
`400`. `returnPath` is set on every answer.

### `GET /`

"Teachers" is inserted between "Courses" and "Journal" in
`nav#workspace-section`. Visible to Admin and Dean, in read-only mode too; it
enforces nothing.

### `GET /workspace/courses`

Unchanged. The teacher list guarantees that each linked participant is a
current teacher of some course, hence in the course list's teacher choice, so
the link never meets the course list's `404 TeacherUnknown` (spec FR-007).

## 4. Authorization model

| Caller | `GET /workspace/teachers` |
|---|---|
| Anonymous | `302 /sign-in` |
| Dean on forced password change | `302 /sign-in/change-password` |
| Dean | `200` / `400` |
| Admin | `200` / `400` |

Teacher and Student have no account (Hard Stop). The existing `ViewCourses`
policy (US-020 FR-014) is declared on the action; no new policy. Read-only mode
changes nothing (spec FR-013).

## 5. Error model

Server-rendered page: errors are the page itself with translated messages, not
the JSON error body of API-5, which applies to `/api/v1` endpoints only — the
same choice as US-020. Unexpected failures go to the single error page with no
detail (API-10).

## 6. Acceptance Criterion → operation

| AC | Operation | Status |
|---|---|---|
| AC-001, AC-002, AC-003 | `GET /workspace/teachers` | `200` |
| AC-004 | `GET /workspace/teachers` → `GET /workspace/courses` | `200`, `200` |
| AC-005 | `GET /workspace/teachers?page=n` | `200` |
| AC-006 | `GET /workspace/teachers` — URLs in the page | `200` |
| AC-007 | `GET /` | `200` |
| AC-008 | `GET /workspace/teachers` | `200` with `isEmpty` |
| AC-009 | `GET /workspace/teachers` in read-only mode | `200` |
| AC-010 | `GET /workspace/teachers` anonymous / Dean / Admin | `302` / `200` / `200` |
| AC-011 | `GET /workspace/teachers` in uk and en | `200` |
| AC-012 | all — substituted Google ports | — |
| AC-013 | `GET /workspace/teachers?status=x` / `?page=0` | `400` |
| AC-014 | `GET /workspace/teachers` | `200` |
| AC-015 | `GET /workspace/teachers?page=1…3` | `200` |
| AC-016 | `GET /workspace/teachers` → `GET /workspace/courses` | `200`, `200` |

## 7. Compatibility

Additive only. No existing route, parameter, policy, view model or status code
changes. The course list's types are reused, not altered.

## 8. Open questions

None. No Open Decision was raised.
