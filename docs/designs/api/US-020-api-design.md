---
artifact_type: api_design
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T14:14:06Z
updated_at: 2026-10-11T14:14:06Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
supersedes: null
---

# US-020 API Design — Course list with filters and search

Companion to `docs/designs/api/US-020-openapi.yaml`.

Delegated: nothing to subagents at this stage. The US-033 and US-044 contracts,
`SignInRoutes`, `InstallationPolicies`, `JournalRequest` and the home page view
(`Views/Home/Index.cshtml`) were read directly; the YAML was checked by a parser.

## 1. Scope of the contract

| Operation | Change | Policy | Calls Google | Writes |
|---|---|---|---|---|
| `GET /workspace/courses` | **new** — filter form and one page of courses | `ViewCourses` (new) | no | nothing |
| `GET /` (US-008) | additive markup — "Courses" first in the Google Workspace section | `ViewLegitimacyStatus` (unchanged) | no | nothing |

No `/api/v1` path, no `POST`, no Control Plane change, no private-port change.
`ContractVersion.Current` stays **1**. The journal and the Meet reports pages
are not changed (OD-007); the list only links to them.

## 2. Decisions this stage made

### 2.1 Path `/workspace/courses`, route constant `SignInRoutes.Courses`

The spec's indicative route is kept: the page sits in the Google Workspace
section beside `/workspace/journal`, `/workspace/meet-codes` and
`/workspace/meet-reports`, and follows their pattern — one `GET` page whose form
submits to itself by `GET`. The plural noun leaves room for the course card of
US-021 under `/workspace/courses/{courseId}`.

### 2.2 Parameter names and tokens

The spec's indicative names are fixed: `status`, `teacherId`, `q`, `page`.
`status` tokens are the lower-case state names plus `all` —
`active`, `archived`, `provisioned`, `declined`, `suspended`, `all` — accepted
case-insensitively, emitted in lower case. They are the names `trebovaniya.md`
§3 uses, so a bookmarked address reads naturally in either UI language.

### 2.3 Raw query collected by the host, decided in `Application`

As the journal (`JournalRequest`), the controller passes every occurrence of
each of the four parameters (`CourseListRequest`, name indicative, in
`Application/Models/Requests`) so "exactly one value" and every VR rule are
decided in `Application`, not by model binding. Unknown parameters are not
collected (spec VR-005).

### 2.4 Validation answers with the page itself; 400 before 404

As in the journal and the Meet reports, a failure re-renders this page with the
form and closed-enum message keys, never the host's error page:

- `400` — any parameter malformed; **all four are checked** and every failing
  one gets its key (spec FR-012 "every message is shown");
- `404` — every parameter well-formed, but `teacherId` is not in the choice.

Existence is checked only when the shape passes, so a request with a malformed
`status` and an unknown `teacherId` answers `400` with `StatusMalformed` only.
This is the journal's order (shape, then existence) and keeps one status code
per answer; the unknown teacher is reported once the status is corrected.

`q` gets three keys rather than one: `SearchRepeated`, `SearchTooLong`,
`SearchControlCharacter` (spec VR-003 leaves the repeated-parameter wording to
implementation; a separate key fixes it now). At most one key per parameter;
for `q` the check order is repeated → too long → control character.

### 2.5 The teacher choice is read on every answer

The form needs the choice on `200`, `400` and `404`, and the `404` check is
membership in that same choice — no separate existence query. Reading the
choice is not reading courses for the list, so spec FR-012 / S-07 ("no course is
read for the list before validation passes") holds.

### 2.6 One canonical URL rule

Paging links and the language switcher's return path come from one builder
(`CourseListUrl` in the contract): fixed parameter order, defaults omitted,
validated values only. This makes AC-010 and S-05/S-06 a single test surface.
After a validation failure the rejected parameter and `page` are omitted from
the return path, so switching language after an error shows the defaults rather
than repeating the failure. The filter form itself carries no `page` input, so
submitting it restarts at page 1 (spec FR-009).

`q` is free text in the address. The spec allows it (S-05, OD-009) and it is
never logged; a typed teacher's name would be the user's own input, not data the
page puts in a URL. Raised here for SECURITY_REVIEW in line with US-039 INFO-1.

### 2.7 Names decided in `Application`, labels in the view

`PersonName` carries which branch of the FR-007 rule applied (`ProfileName`,
`Email`, `NoName`) and the stored text. The only translated part, "teacher
without a name", is chosen by the view from `kind`, so `Application` holds the
rule and the view holds no business logic. The duplicate-label email (I-3) is
decided in `Application` and travels as `disambiguatingEmail`.

### 2.8 Dates as local date-times

`updatedAt` is converted to the school's time zone in `Application` and carries
no offset (`yyyy-MM-ddTHH:mm`), like US-033's local times; the view formats it
for the UI language. Tests of AC-018 assert the DTO value.

### 2.9 Page past the end

A valid `page` greater than `pageCount` answers `200` with the last page, and
`list.page` holds the page actually shown; the return path carries that page.
With nothing matching, `page` is 1 and `emptyState` is set.

### 2.10 One new policy, `ViewCourses`

Its own policy for the matrix row "Просмотр курсов и участников" (one policy per
row, US-019 I-5), allowing Admin and Dean; US-021 and US-022 will reuse it. It
is a new constant in `InstallationPolicies` registered in
`InstallationSecurityServices`.

### 2.11 No read-only guard, no audit, no new cache header

Viewing (§2) works identically in every BR-025 cause; there is nothing for
`IReadOnlyModeGuard` to block. Viewing is not in SC-11. The host-wide
`no-store` of US-040 covers the page.

## 3. Operation notes

### `GET /workspace/courses`

Order: authentication (`302 /sign-in`) → restricted session
(`302 /sign-in/change-password`) → `ViewCourses` → shape of `status`,
`teacherId`, `q`, `page` (`400`) → teacher choice → `teacherId` in choice
(`404`) → count, page, teachers and student counts of the page (`200`).
Database round trips are a fixed set (see `x-database-round-trips`); DB_DESIGN
fixes the queries, the Ukrainian-aware ordering and the indexes. One
`Information` log line on `200` (actor id, effective status, teacher id or none,
search given yes/no, page shown, total) and one `Warning` line per rejected
parameter (parameter and rule only), per spec FR-018.

### `GET /`

"Courses" becomes the first link of `nav#workspace-section`, before "Journal",
for Admin and Dean, in every mode.

## 4. Authentication and authorization model

| Operation | Policy | Admin | Dean | Dean, temporary password | Anonymous |
|---|---|:---:|:---:|---|---|
| `GET /workspace/courses` | `ViewCourses` | ✔ | ✔ | `302 /sign-in/change-password` | `302 /sign-in` |

Both v1 roles are allowed, so the TC-5 forbidden cases are the anonymous request
and the Dean on the forced password change (spec S-02). No per-course or
roster-based restriction (S-03). The endpoint enumeration test classifies the
path as protected.

## 5. Error model

| Case | Status | Rendered | Key |
|---|---|---|---|
| `status` not a token, or repeated | 400 | course-list page, `list` null | `StatusMalformed` |
| `teacherId` not a positive int64, or repeated | 400 | same | `TeacherMalformed` |
| `q` repeated | 400 | same | `SearchRepeated` |
| `q` longer than 100 after trimming | 400 | same | `SearchTooLong` |
| `q` with a `Cc` character | 400 | same | `SearchControlCharacter` |
| `page` not 1…100000, or repeated | 400 | same | `PageMalformed` |
| `teacherId` well-formed, not in the choice | 404 | same | `TeacherUnknown` |
| `page` past the last | 200 | last page | — |
| no course stored / none matches | 200 | form + message | `emptyState` |
| not signed in / temporary password | 302 | — | — |
| unexpected | 500 | single error page | — |

No `/api/v1` body is involved (API-6 applies to JSON endpoints only). Rejected
values never reach the model, a link, the return path or a log (SC-10).

## 6. Acceptance Criterion → operation map

| AC | Operation / contract element |
|---|---|
| AC-001 | `getCourseList` — `CourseListRow` |
| AC-002, AC-003 | `getCourseList` — `status` |
| AC-004 | `getCourseList` — `teacherId`, `teacherChoice` |
| AC-005 | `PersonName` |
| AC-006 | `getCourseList` — `q` |
| AC-007 | `getCourseList` — all filters together |
| AC-008 | `CourseListResult` (order, `pageSize` 50), `CourseListUrl` |
| AC-009 | `CourseListRow.courseId` → journal and Meet reports links |
| AC-010 | `CourseListUrl`, `returnPath` |
| AC-011 | `CourseListResult.emptyState` |
| AC-012 | `getLanding` |
| AC-013 | `400` / `404` rows of §5; `CourseListMessageKey` |
| AC-014 | `getCourseList` in each BR-025 cause (`x-read-only-mode`, `x-writes`) |
| AC-015 | §4 |
| AC-016 | translation keys `Courses.*`; Google data as stored |
| AC-017 | `x-calls-google: false` |
| AC-018 | `CourseListRow.updatedAt` |
| AC-019 | `q` literal matching; row order |
| AC-020 | `page`, `x-database-round-trips` |

## 7. Compatibility

All changes are additive: a new path, a new policy, new translation keys, one
new link on the home page. No existing operation, status code or view model
changes.

## 8. Open questions for later stages

None blocking.

- DB_DESIGN: the Ukrainian-aware ordering mechanism and case-insensitive literal
  substring search (spec I-7, FR-006) and the indexes of spec FR-016.
- IMPLEMENTATION: `SignInRoutes.Courses`, `InstallationPolicies.ViewCourses`,
  translation keys under `Courses.*` (`Status`, `Validation`, `Empty`,
  `Teacher.NoName`, navigation, headers, paging) in both resource files.
