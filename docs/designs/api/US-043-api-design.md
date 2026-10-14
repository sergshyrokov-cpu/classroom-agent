---
artifact_type: api_design
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-14T06:02:01Z
updated_at: 2026-10-14T06:02:01Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 91
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
supersedes: null
---

# US-043 API Design — Fill the group-lessons blank form with a template report

Contract: `docs/designs/api/US-043-openapi.yaml` (a delta on
`docs/designs/api/US-028-openapi.yaml`).

Delegated (read only, before this stage): the US-028 contract, request record
and report-page markup were looked up during SPECIFICATION and re-read here
directly; no further delegation. YAML validity checked with a parser.

## 1. Scope of the contract

- **Changed:** `POST /api/v1/exports/journal-xlsx` — three optional body members,
  four message keys, one more `400` cause, one more `500` cause.
- **Changed:** `GET /reports` — the export action gains the layout choice and
  the two per-copy inputs; the view DTO `ExportAction` gains `blankForm`.
- **Unchanged:** every other operation; policies; the SC-4 anonymous list; the
  Control Plane; the private port; the Control Plane channel (`ContractVersion`
  stays 1).

## 2. Decisions this stage made

### 2.1 The same operation, not a new path

Spec FR-004 extends "the export request (US-028 FR-001, the same endpoint and
JSON body)". A new member `layout` selects the output; `program` is the default,
so every US-028 client and test is untouched (AC-004). A separate path
(`/api/v1/exports/journal-blank-xlsx`) was not chosen: the spec fixes the same
endpoint, and the file name, headers, audit and authorization are identical.

### 2.2 Member names and values

`layout` ∈ {`program`, `group-lessons-blank`} — the spec's own tokens (FR-004,
VR-001). `studentsPerCopy`, `lessonsPerCopy` — camelCase like `courseId`. The
fill method is **not** a member (spec I-3).

### 2.3 Per-copy members: any JSON value is a field error

US-028 binds its members as strings and treats a wrong JSON type as a syntax
failure (`RequestMalformed`, no field). Spec VR-001 requires that a string
`"10"`, a fraction or a boolean be refused **naming the field**. So the two
per-copy members are read as raw JSON values in `Application`: a JSON number
with a whole value in range is valid; everything else is `StudentsPerCopyInvalid`
/ `LessonsPerCopyInvalid`. JSON `null` = absent = default (spec I-12). `layout`
follows the US-028 string rule (a non-string is `RequestMalformed`; an unknown
string is `LayoutInvalid`), like `orientation`.

The record `JournalExportRequest` therefore gains `string? Layout` and two
members able to carry an arbitrary JSON value (for example `JsonElement?`) —
the exact C# type is IMPLEMENTATION's, within AD-8 (a DTO in
`Application/Models/Requests`).

### 2.4 Validation order and when the per-copy members count

`fieldErrors` keeps US-028's order and appends `layout`, `studentsPerCopy`,
`lessonsPerCopy`. The per-copy members are validated only when `layout` is
**valid and** `group-lessons-blank`; with `program`, or with an invalid
`layout`, they are not read (spec VR-001, I-10).

### 2.5 The sheet limit is a `400` without a field

OD-011 (a): more than 100 sheets → `400` `TooManySheets`. It needs the counts of
students and lessons, so it is checked **after** the `404` lookups and the report
build, before rendering (spec VR-002). No single field is wrong — the
combination with the report's size is — so it has no `fieldErrors` entry; the
message names both inputs and suggests a shorter period. Not `422`: API-5 maps
validation to `400`, and US-028 uses `400` for every request-content failure.

### 2.6 A broken embedded form is a `500`

Spec FR-003 / I-2: a program defect, through the single exception handler. No
new key — `Error.Unexpected`.

### 2.7 The page: limits from the view DTO

The number inputs need `min`, `max` and a default. Writing `30` and `23` into the
Razor view would put a business number in presentation (AD-3), so
`ExportAction` gains `blankForm` with the four values mapped in `Application`
from the form's capacities (spec FR-003). The browser's `min`/`max` are a
convenience only; the server validates (spec VR-001).

The script sends the per-copy values as **JSON numbers** when the input parses
as a number, otherwise the raw string — so the server, not the script, decides
and names the field.

### 2.8 Unchanged on purpose

File name, `Content-Type`, `Content-Disposition`, `Cache-Control` — US-028 for
both layouts (spec FR-007, I-9). Audit row — format `xlsx` for both (spec
FR-010, I-11). Logging — spec FR-011, not part of the wire contract.

## 3. Operation notes

### `POST /api/v1/exports/journal-xlsx`

| Step | Program layout | Group-lessons blank |
|---|---|---|
| 401 / 403 / antiforgery 400 / 415 / syntax 400 | US-028 | US-028 |
| Field validation | US-028 members + `layout` | + `studentsPerCopy`, `lessonsPerCopy` |
| 404 template / course | US-028 | US-028 |
| Report build | US-027 query | the same query (spec FR-005) |
| Sheet limit | — | `400` `TooManySheets` if > 100 |
| Render | `IReportRenderer` (US-028) | the form renderer port (spec FR-005: fill model → `Infrastructure`, ClosedXML) |
| Audit | `JournalExported`, `xlsx` | the same |
| Response | `200` file | `200` file |

Read-only mode: identical in every BR-025 cause; no guard call (spec FR-009).

### `GET /reports`

Only the export block changes (spec FR-004): the "Layout" radio group, the two
number inputs (shown with the form), the orientation pair (shown with the program
layout). Rendered in both languages from new keys (spec FR-012).

## 4. Authentication and authorization model

Unchanged from US-028: `UseReportTemplates` (Admin ✔, Dean ✔); deny by default;
anonymous `/api/v1` → `401` JSON; forced password change → `403`
`Api.Error.PasswordChangeRequired`; missing token → `400` `Error.PageExpired`.
TC-5 tests for the form export: Admin and Dean allowed; anonymous and a Dean on
the forced password change refused (AC-009).

## 5. Error model

API-6 body, unchanged shape. New message keys (uk + en):

| Key | Cause | `fieldErrors` |
|---|---|---|
| `Export.Validation.LayoutInvalid` | unknown `layout` string | `layout` |
| `Export.Validation.StudentsPerCopyInvalid` | not a whole number 1 … 30 | `studentsPerCopy` |
| `Export.Validation.LessonsPerCopyInvalid` | not a whole number 1 … 23 | `lessonsPerCopy` |
| `Export.Validation.TooManySheets` | > 100 sheets | none |

The rejected value is never echoed, logged or audited (SC-10).

## 6. Acceptance Criterion → operation map

| AC | Operation | Asserted |
|---|---|---|
| AC-001 | `POST …/journal-xlsx` (blank) | `200`; workbook cells, styles, footers vs the shipped form |
| AC-002 | `POST …/journal-xlsx` (blank, 10 / 23) | `200`; eight sheets, names, order, numbering |
| AC-003 | `POST …/journal-xlsx` (blank) | `200`; cleared rows / columns |
| AC-004 | `POST …/journal-xlsx` (no `layout`; `program`; `program` with junk per-copy members) | `200`; the US-028 workbook |
| AC-005 | `POST …/journal-xlsx` | `400` per key and field; no file; no audit row; value not logged |
| AC-006 | `POST …/journal-xlsx` (blank, template showing materials) | `200`; no material column; marks as on screen |
| AC-007 | `POST …/journal-xlsx` (blank, empty report) | `200`; audited |
| AC-008 | `POST …/journal-xlsx` (blank, each BR-025 cause) | `200`; audit row |
| AC-009 | `POST …/journal-xlsx` (blank) | `401` / `403` / allowed for Admin and Dean |
| AC-010 | `POST …/journal-xlsx` (blank) | quote-prefixed text cells |
| AC-012 | `POST …/journal-xlsx` (blank) | `Cache-Control: no-store` |
| AC-014 | `POST …/journal-xlsx` (blank, > 100 sheets) | `400` `TooManySheets`; no file; no audit row |
| AC-015 | `POST …/journal-xlsx` (blank) | footer sections per kind |
| AC-011, AC-013 | — (test data / the shipped file) | not an operation |

## 7. Compatibility

Additive: every US-028 request is valid and answers the same (`layout` absent →
`program`; per-copy members ignored). `fieldErrors[].field` and
`ExportMessageKey` gain values — the page script shows any message it receives,
so it needs no change for them. `ExportAction` gains a member. No breaking
change; API version stays `v1` (API-1).

## 8. Open questions for later stages

None blocking.

- **DB_DESIGN:** no entity or schema change is expected (no stored setting,
  audit format stays `xlsx`, spec I-11); DB_DESIGN may answer `NOT_APPLICABLE`.
- **IMPLEMENTATION:** the form renderer port's name and shape (a second port
  beside `IReportRenderer`, or a second method on it) — within AD-4, no
  ClosedXML type in `Application`.
