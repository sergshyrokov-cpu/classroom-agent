---
artifact_type: api_design
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:08:50Z
updated_at: 2026-10-11T16:08:50Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/decisions/US-021-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
supersedes: null
---

# US-021 API Design — Course detail with roster

Companion to `docs/designs/api/US-021-openapi.yaml`.

Delegated: nothing to subagents at this stage. The US-020 contract,
`SignInRoutes` (including the `MeetReportMeeting` route constraint precedent),
`ErrorController`, the status-code re-execution in `Program.cs`, the
`Views/Courses/Index.cshtml` row markup and `CourseListPersonName` were read
directly; the YAML was checked by a parser.

## 1. Scope of the contract

| Operation | Change | Policy | Calls Google | Writes |
|---|---|---|---|---|
| `GET /workspace/courses/{courseId}` | **new** — the course card | `ViewCourses` (existing, US-020) | no | nothing |
| `GET /workspace/courses` (US-020) | additive markup — the course name links to the card | `ViewCourses` (unchanged) | no | nothing |

No `/api/v1` path, no `POST`, no Control Plane change, no private-port change.
`ContractVersion.Current` stays **1**. The journal and the Meet reports pages
are not changed; the card only links to them with `courseId`, which they already
accept.

## 2. Decisions this stage made

### 2.1 Path `/workspace/courses/{courseId}`

The card sits under the course list's path, a sub-resource by internal id, as
the Meet meeting detail sits under the Meet reports
(`/workspace/meet-reports/meetings/{meetingId}`). The constant's name
(`SignInRoutes.CourseCard`) is indicative.

### 2.2 Route constraint `^[0-9]{1,19}$`

The same shape as `MeetReportMeeting`'s digit constraint, bounded to 19 digits —
the length of `Int64.MaxValue`. Three consequences, all intended:

- a value with any non-digit, or longer than 19 digits, matches no route and
  gets the host's catch-all `404` before authentication — exactly what any
  unknown path gets, so it reveals nothing;
- `0` and 19-digit values above `Int64.MaxValue` match the route and are refused
  by the parse in `Application` (`CourseMalformed`);
- leading zeros match and parse (spec I-6); the system never produces them.

Bounding to 19 rather than `\d+` keeps the parse's overflow case small and
testable; it does not change the answer (both are `404`).

### 2.3 The raw value is decided in `Application`

The controller passes the path string to `GetCourseCardQuery`; `Application`
parses and validates it (§8, `package-map.md` `Models/Requests`), as US-020 and
US-033 do. The controller holds no rule: it maps the outcome to a status code,
writes the log line and renders.

### 2.4 One 404 for malformed and unknown

OD-010 asks for the ordinary not-found page. The controller returns `NotFound()`
with no body and the host's `/error/404` re-execution draws `Error.NotFound` —
the same page every unknown path gets. Unlike the US-020 list (which re-renders
itself with a message), the card has no form to redraw and nothing to say about
the value, so its own view is not used for `404`. The two outcomes stay
distinct only in the `Warning` log line (`CourseMalformed` / `CourseUnknown`),
which names the parameter, never the value.

### 2.5 One view model; three arrays

`CourseCardPageModel` carries the details, `teachers`, `students` and `leavers`
already sorted (spec FR-007) — counts are the array lengths, not separate fields,
so they cannot disagree. Owner (`isOwner`) is decided in `Application`; the view
only renders the mark.

### 2.6 Reuse of the US-020 name rule

The person's name is the US-020 `PersonName` (C# `CourseListPersonName`): same
rule, one type, one implementation. Only the `NoName` label differs: the card
renders a role-neutral key `Courses.Card.NoName` (spec I-2).

### 2.7 Dates

Course times are local date-times to the minute (as US-020 `updatedAt`); the
leaver's last-seen value is a local **date** (`format: date`, spec I-5). The
conversion to the school's time zone happens in `Application`; the view only
formats for the UI language.

### 2.8 No read-only guard, no audit, no new cache header

Viewing is allowed in read-only mode and not audited (spec FR-014, FR-015). The
host-wide `no-store` of US-040 already covers the page.

## 3. Operation notes

### `GET /workspace/courses/{courseId}`

Evaluation order: route match → authentication (`302 /sign-in`) → restricted
session (`302 /sign-in/change-password`) → `ViewCourses` → parse (`404`
`CourseMalformed`) → course read (`404` `CourseUnknown`) → memberships with
participants (`200`). Query parameters are ignored. The language switcher's
return path is the canonical card URL from the parsed id.

`200` log line (`Information`): actor id, course id, the three counts. `404` log
line (`Warning`): parameter `courseId` and the outcome name.

### `GET /workspace/courses`

In `Views/Courses/Index.cshtml` the `@row.Name` cell becomes a link to
`/workspace/courses/{row.CourseId}` built from the route constant. The section
beneath it stays plain text. Nothing else changes.

## 4. Authentication and authorization model

- `ViewCourses` (US-020), Admin and Dean (§2 "Просмотр курсов и участников").
  No new policy: one policy per matrix row (US-019 I-5), and this is the same
  row.
- Anonymous → `302 /sign-in`; a Dean with a temporary password →
  `302 /sign-in/change-password` (US-012). Exception: a value the route does not
  match never reaches authorization and answers the catch-all `404`, as any
  unknown path does.
- No per-course restriction; no Teacher or Student account (Hard Stop).
- Tests (TC-5): allowed — Admin, Dean; forbidden — anonymous, Dean on the forced
  password change. The endpoint enumeration test classifies the route as
  protected.

## 5. Error model

| Status | When | Body |
|---|---|---|
| `302` | not signed in; forced password change | redirect, nothing read |
| `404` | no route match; `CourseMalformed`; `CourseUnknown` | host's not-found page (`Error.NotFound`), no detail, value not echoed |
| `500` | unexpected failure | the single error page, no detail (API-10) |

There is no `400`: the only input is a path segment, and OD-010 answers every
bad value with `404`. No structured JSON error body applies (no `/api/v1`).

## 6. Acceptance Criterion → operation map

| AC | Operation | Contract element |
|---|---|---|
| AC-001 | `GET /workspace/courses/{courseId}` | `CourseCardDetails` |
| AC-002 | same | `teachers`, `students` |
| AC-003 | same | `CourseCardTeacher.isOwner` |
| AC-004 | same | `leavers`, `CourseCardLeaver.role`, `lastSeenOn` |
| AC-005 | same | `PersonName`, `CourseCardPerson.email` |
| AC-006 | same | array order (spec FR-007) |
| AC-007 | `GET /workspace/courses` (name link); card `courseId` links | `CourseCardDetails.courseId` |
| AC-008 | `GET /workspace/courses/{courseId}` | `404`, `CourseCardOutcome`, `CourseId` parameter |
| AC-009 | same | empty arrays → `Courses.Card.Empty.*` |
| AC-010 | same | `x-read-only-mode`, `x-writes` |
| AC-011 | same | `x-policy: ViewCourses`, `302` |
| AC-012 | same | translation keys; Google strings untranslated |
| AC-013 | same | `x-calls-google: false` |
| AC-014 | same | `createdAt`/`updatedAt` local date-time, `lastSeenOn` local date |
| AC-015 | same | array order |
| AC-016 | same | `CourseCardDetails.description` |
| AC-017 | same | `x-database-round-trips` |

## 7. Compatibility

Additive. The US-020 view model is unchanged — the row already carries
`courseId`. No existing status code, parameter or policy changes.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: confirm the index for "all memberships of one
course" and choose the ordering mechanism (the US-020 `course_name_uk` collation
in the query, or ordering in `Application`) — proved by spec AC-015.
