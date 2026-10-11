---
artifact_type: api_design
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T07:08:01Z
updated_at: 2026-10-11T07:08:01Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/decisions/US-044-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
supersedes: null
---

# US-044 API Design — Export the built-in Meet reports to Excel

Companion to `docs/designs/api/US-044-openapi.yaml`, which extends the US-028
and US-033 contracts.

Delegated: none to subagents at this stage. The existing contracts (US-028,
US-033), `report-export.js` and the translation keys were read directly; the
YAML was checked by a parser.

## 1. Scope of the contract

| Operation | Change | Policy | Writes |
|---|---|---|---|
| `POST /api/v1/exports/meet-reports-xlsx` | **new** — reports 1–3 | `ViewMeetReports` | one `AuditEvent` on success |
| `POST /api/v1/exports/meet-meeting-xlsx` | **new** — report 4 | `ViewMeetReports` | one `AuditEvent` on success |
| `GET /workspace/meet-reports` | additive: `export` action + token meta tag when reports are shown | `ViewMeetReports` | nothing |
| `GET /workspace/meet-reports/meetings/{meetingId}` | additive: `export` action + token meta tag when a meeting is shown | `ViewMeetReports` | nothing |

Unchanged: the journal export and its page, the host-wide `/api/v1` error
mapping (US-028 §2.5), the Control Plane, the private port.
`ContractVersion.Current` stays **1**. No operation calls Google.

## 2. Decisions this stage made

### 2.1 Two operations of the API-3 export shape

As US-028 §2.1: an action whose success answer is a file is a `POST` to
`/api/v1/exports/{kind}` with a JSON body, called by a script with the token
header (API-3, API-7). Two kinds, because the two files differ in inputs,
content and audit action, and each operation then has one success media type
and one request schema:

- `meet-reports-xlsx` — the course workbook (spec FR-005);
- `meet-meeting-xlsx` — the meeting workbook (spec FR-006).

`meet-` separates them from US-028's `journal-xlsx`; the `-xlsx` suffix leaves
room for a later Word kind (OD-001) without touching these.

### 2.2 Body members mirror the page inputs, as strings

`courseId`, `from`, `to` carry the reports page's query names and are parsed by
the same `Application` parsers, so the same rule gives the same message (spec
VR-001). `meetingId` is the meeting page's route value. `orientation` is the
US-028 member with a different default (`landscape`, OD-009). All are JSON
strings; a wrong JSON type, a duplicate member or a non-object body is a syntax
failure — `400` `Export.Validation.RequestMalformed`, no `fieldErrors` — as
US-028 §2.3. Every member except `orientation` is required (spec VR-001,
VR-002).

The meeting export takes no period (spec I-10).

### 2.3 Messages: reuse, one new key

| Cause | Key |
|---|---|
| `CourseMalformed`, `CourseUnknown`, `FromMalformed`, `ToMalformed`, `PeriodInverted` | `MeetReports.Validation.*` (US-033) |
| `MeetingNotFound` | `MeetReports.Meeting.MeetingNotFound` (US-033) |
| `OrientationInvalid`, `RequestMalformed` | `Export.Validation.*` (US-028) |
| `MeetingMalformed` | **new** `Export.Validation.MeetingMalformed` |

US-033 answered a malformed meeting id in the page route as "not found". The
export answers `400` (spec VR-002, API-5), so it needs a message of its own;
the page always sends a valid id, so the user only meets it through a broken
client.

### 2.4 Order of checks and the 404s

Authentication → restricted session → policy → antiforgery → `Content-Type` →
syntax → field validation (all failing fields) → existence → build → map →
render → audit → `200`. For the meeting export, existence checks the course
first; only for an existing course is the meeting checked against it. An
unknown meeting and a meeting of another course give the same
`MeetingNotFound` (US-033 FR-013), so the answer reveals nothing about other
courses.

### 2.5 The page side

Each page's view model gains a nullable `export` member built only from
validated values: `{courseId, from, to}` — the **effective** period after the
current-month defaults — on the reports page, and `{courseId, meetingId}` on
the meeting page. It is non-null exactly when the page shows reports (empty
states included) or a meeting, and null on the form-only state, `400` and
`404` (spec FR-002, AC-001). With it, the page renders the button, the
orientation radio pair with `landscape` pre-selected, and
`<meta name="request-verification-token">`.

The script is a static file, no inline script. `report-export.js` is
hard-wired to `journal-xlsx` and the journal's members. The Meet pages get
their own static script, or a shared one driven by `data-` attributes (export
URL and members). Either is acceptable to this contract **only if the journal
page keeps its exact behaviour and its US-028 tests stay green** (spec I-7).
The choice is IMPLEMENTATION's.

### 2.6 File name and audit order

As US-028 §2.7 and §2.8: `Content-Disposition` with `filename` and `filename*`
via `SetHttpFileName`; the name is built in `Application` (spec FR-008.1). The
audit row is written and committed after rendering and before the response;
a failure of either is `500` with no file.

## 3. Operation notes

### `POST /api/v1/exports/meet-reports-xlsx`

- Builds with `GetMeetReportsQuery` (spec FR-003), including the delay line
  (`MeetLoadedUpTo`) the page shows.
- Audit `MeetReportsExported`: target course, period, student rows of report 3
  and meeting rows of report 2, format Excel (spec FR-013, I-9).
- Log (spec FR-014): `Information` with actor id, course id, period, both row
  counts, orientation, file size; `Warning` on refusal with field and rule
  names only.

### `POST /api/v1/exports/meet-meeting-xlsx`

- Builds with `GetMeetReportMeetingQuery`, plus the delay line (spec FR-004 row
  7), which the meeting page does not show today; reading it is a read of
  `LastSynchronizationView`, not a new query surface.
- Audit `MeetMeetingExported`: target course, meeting id, identified-person
  rows, format Excel.
- Log: as above with the meeting id instead of the period.

### `GET` pages

- `export` as §2.5; the meta tag only with the action. Viewing writes nothing.

## 4. Authentication and authorization model

- Cookie session; `ViewMeetReports` — Admin ✔, Dean ✔ (§2 "Просмотр и экспорт
  статистики Meet-занятий"). No new policy, no new matrix cell.
- Anonymous → `401`; Dean on forced password change → `403`; antiforgery header
  required (`400` otherwise).
- SC-4 anonymous list unchanged; the enumeration test sees both paths as
  protected and the SC-14 test sees both as `no-store`.
- Actor and UI language from the session (spec VR-004).

## 5. Error model

API-6 body for every error under `/api/v1` (US-028 host-wide rule).
`fieldErrors[].field` ∈ `courseId`, `from`, `to`, `meetingId`, `orientation`.
New translation key: `Export.Validation.MeetingMalformed` (uk, en). Page and file
keys are spec FR-015.

## 6. Acceptance Criterion → operation map

| AC | Operation / rule | Status codes |
|---|---|---|
| AC-001 | `getMeetReports`, `getMeetReportMeeting` — `export` member | 200 |
| AC-002 | `exportMeetReportsXlsx` | 200 |
| AC-003 | `exportMeetMeetingXlsx`; `getMeetReportMeeting` `export` | 200 |
| AC-004 | both exports — header block | 200 |
| AC-005 | both exports — autofilter, frozen header | 200 |
| AC-006 | both exports — typed cells | 200 |
| AC-007 | `Orientation` schema | 200, 400 |
| AC-008 | `Content-Disposition` (§2.6) | 200 |
| AC-009 | both exports — quote prefix | 200 |
| AC-010 | `x-writes` — audit on 200 only | 200, 400, 404 |
| AC-011 | both exports in read-only mode | 200 |
| AC-012 | validation and existence | 400, 404 |
| AC-013 | host-wide `/api/v1` rules; Admin and Dean allowed | 200, 401, 403 |
| AC-014 | both exports — language of the user | 200 |
| AC-015 | all — synthetic data, no Google | — |
| AC-016 | `Cache-Control`; journal export unchanged | 200 |
| AC-017 | `exportMeetReportsXlsx` — `Europe/Kyiv` boundaries | 200 |

TC-3: every status code of both operations (200, 400 ×3 causes, 401, 403 ×2,
404 — ×2 causes for the meeting export, 405, 415, 500) has a test asserting the
code and the API-6 shape.

## 7. Compatibility

Additive. Two view models gain one nullable member each. No existing operation,
schema or message changes. `ApiError.fieldErrors[].field` here lists this
Story's fields; the shape is unchanged.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: the audit storage of the meeting id and the
second row count (spec FR-013); the workbook-model additions of spec FR-007
(autofilter, time and date-time cells, number format), opt-in so the journal
file is unchanged; the `AuditAction` values.
