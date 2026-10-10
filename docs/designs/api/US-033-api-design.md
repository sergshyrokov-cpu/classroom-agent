---
artifact_type: api_design
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T19:45:00Z
updated_at: 2026-10-10T19:45:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/decisions/US-033-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
supersedes: null
---

# US-033 API Design — Built-in Meet reports for a course and period

Companion to `docs/designs/api/US-033-openapi.yaml`.

Delegated: nothing beyond the SPECIFICATION stage's `quick-look` report
(entities, `SchoolTimeZone`, the journal's course source, home page
navigation). This stage read the US-025 and US-032 contracts, `SignInRoutes`,
`InstallationSecurityServices` and `NoStoreMiddleware` directly.

## 1. Scope of the contract

The contract adds **two new** pages on the installation's public port and
changes one existing page additively:

| Operation | Purpose | Calls Google | Writes |
|---|---|---|---|
| `GET /workspace/meet-reports` (new) | form; delay notice; reports 1–3 of one course for a period | no | nothing |
| `GET /workspace/meet-reports/meetings/{meetingId}` (new) | report 4, one meeting of the course | no | nothing |
| `GET /` (US-008) | gains a "Meet reports" navigation entry | no | nothing |

There is no `/api/v1` path, no `POST`, no Control Plane change and no
private-port change. `ContractVersion.Current` stays **1**.

## 2. Decisions this stage made

### 2.1 Paths in the Google Workspace section, next to the journal

The reports page is `/workspace/meet-reports`, beside `/workspace/journal` and
`/workspace/meet-codes`, and follows the journal's pattern: one `GET` page whose
form submits to itself by `GET`. The query string carries only the course id
and the dates — no personal data (spec §7; US-039 INFO-1). Report 4 lives under
the same prefix. Its meeting id is in the route, since it names the resource;
the course id and period are in the query.

### 2.2 One new policy, `ViewMeetReports`

It is a separate policy, not a reuse of `ViewJournal`. It maps to its own
permission-matrix row ("Просмотр и экспорт статистики Meet-занятий"), so a
future change to one row does not silently change the other. It allows Admin
and Dean, is declared on both operations, and US-044's export will reuse it.

### 2.3 Validation answers with the page itself

As in the journal (US-025 api-design §2.3), a validation failure renders the
reports page with the form and message keys, never the host's error page:

- `400` for a malformed parameter or an inverted period;
- `404` for an unknown course.

Message keys form a closed enum (`MeetReportsMessageKey`). A rejected value is
never echoed: the model field is null.

### 2.4 Report 4 — what is checked and what is not

- The **course** must exist, and the **meeting** must be one of its meetings
  (FR-004's course condition). Otherwise the answer is `404` with one key,
  `MeetingNotFound`, whichever of the three failed. The page does not reveal
  whether a meeting id exists in another course.
- `courseId` is **required**. Without it there is no course to classify people
  against (FR-005), so absent or malformed gives `400` with `CourseMalformed`.
- `meetingId` uses a digits-only route constraint. A non-digit value never
  reaches the action and meets the host's catch-all `404`. Zero or an overflow
  gives `404` with `MeetingNotFound`, matching spec VR-003's "answered as not
  found".
- `from` / `to` are **not validated as input** to report 4: they only rebuild
  the link back. Invalid values are dropped from the link and are never
  echoed. The reports page then applies its own defaults. This keeps report 4
  independent of the period (spec I-1).

### 2.5 The view model carries seconds, the view renders minutes

DTOs carry durations in **seconds**, exactly as FR-006 computes them (capped
per person and meeting). Rounding to minutes, half up (spec I-5), happens once
in rendering. Report 3's total is a sum of capped seconds, rounded only when
shown. A test can then check the arithmetic (AC-009) without parsing text.
Date-times are local to the school's time zone and carry no offset, like
`LastSynchronizationView.MeetLoadedUpTo`.

### 2.6 Course list reused

The drop-down is the journal's `CourseOption` list. Whether the Application
layer shares `IJournalSource.GetCoursesAsync` or extracts a common course-list
query is an implementation choice; the wire shape is the same.

### 2.7 No read-only guard, no audit

Both pages are viewing (§2 matrix), so read-only mode answers exactly as
outside it. There is no guard to place (AD-6 concerns writes). Viewing is not
in SC-11.

### 2.8 Caching

The global `no-store` of US-040 already covers both pages. No per-action
header is added.

## 3. Operation notes

### `GET /workspace/meet-reports`

Order: authentication → restricted session → `ViewMeetReports` → query shape
(`400`) → course exists (`404`) → read and build (`200`). With no `courseId`,
it returns the form only, with the default period computed from the injected
`TimeProvider` in the school's time zone. The delay notice is part of
`MeetReports` and appears only with a chosen course (spec FR-001 order: form,
notice, reports). The number of database round-trips per request is fixed
(meetings + participations of the course and period, the course's
memberships, participants by the emails found, `SyncState`).

### `GET /workspace/meet-reports/meetings/{meetingId}`

Order: authentication → restricted session → `ViewMeetReports` → shape of
`courseId` (`400`) → course and meeting-of-course (`404`) → build (`200`). The
page shows the course name, meeting code, times, organizer with the "not a
teacher of this course" mark, one line per identified person, and one
"Other participants" line with a count and minutes.

### `GET /`

Additive markup only: the "Meet reports" entry for Admin and Dean, in every
mode.

## 4. Authentication and authorization model

| Operation | Policy | Admin | Dean | Anonymous |
|---|---|:---:|:---:|---|
| `GET /workspace/meet-reports` | `ViewMeetReports` | ✔ | ✔ | `302 /sign-in` |
| `GET /workspace/meet-reports/meetings/{meetingId}` | `ViewMeetReports` | ✔ | ✔ | `302 /sign-in` |

A Dean with a temporary password is redirected to change it (US-012). Both v1
roles are allowed, so the TC-5 forbidden case of each operation is the
unauthenticated request (spec AC-014). The US-008 endpoint enumeration test
must classify both paths as protected.

## 5. Error model

| Case | Status | Rendered | Key |
|---|---|---|---|
| malformed `courseId` / `from` / `to` (reports page) | 400 | reports page | `CourseMalformed`, `FromMalformed`, `ToMalformed` |
| inverted period | 400 | reports page | `PeriodInverted` |
| unknown course (reports page) | 404 | reports page | `CourseUnknown` |
| report 4 without a valid `courseId` | 400 | report-4 page | `CourseMalformed` |
| report 4, unknown course / meeting / not of course | 404 | report-4 page | `MeetingNotFound` |
| report 4, non-digit `meetingId` | 404 | host catch-all | — |
| not signed in | 302 | — | — |
| unexpected | 500 | single error page | — |

No `/api/v1` body is involved (API-6 applies to JSON endpoints only). Log lines
on validation failures name the parameter and the rule, never the value
(SC-10).

## 6. Acceptance Criterion → operation map

| AC | Operation(s) |
|---|---|
| AC-001 | `getMeetReports` (form, defaults, reports), `getLanding` (entry) |
| AC-002 … AC-009 | `getMeetReports` (`MeetReports`); AC-005, AC-007, AC-009 also `getMeetReportMeeting` |
| AC-010 | `getMeetReportMeeting` |
| AC-011 | `getMeetReports` (`MeetDelayNotice`) |
| AC-012 | `getMeetReports` (zeros, empty-state keys) |
| AC-013 | both (`400` / `404` rows of §5) |
| AC-014 | both (Admin ✔, Dean ✔, anonymous `302`) |
| AC-015 | both, in each BR-025 cause |
| AC-016 | both (translation keys; Google data as stored) |
| AC-017 | both (`x-calls-google: false`) |
| AC-018 | `getMeetReports` (period boundaries) |

## 7. Compatibility

All changes are additive. The new paths, the new policy and the new
translation keys do not change any existing operation. `getLanding` gains
markup only. US-044 will add an export `POST` under `/workspace/meet-reports`
with the same policy.

## 8. Open questions for later stages

None blocking.

- DB_DESIGN: decide whether the queries need an index, for example on
  `meet_session (meeting_code, started_at)` or a lower-case email lookup on
  `meet_participation`. `ix_classroom_participant_email_lower` already exists
  (US-032).
- IMPLEMENTATION: route constants in `SignInRoutes`
  (`MeetReports`, `MeetReportMeeting`) and the policy in
  `InstallationPolicies` / `InstallationSecurityServices`.
