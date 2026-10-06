---
artifact_type: api_design
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T05:43:12Z
updated_at: 2026-10-06T05:43:12Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/decisions/US-028-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 86
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
supersedes: null
---

# US-028 API Design — Export a journal to Excel using a school template

Companion to `docs/designs/api/US-028-openapi.yaml`, which extends the US-008,
US-027 and US-042 contracts.

## 1. Scope of the contract

| Operation | Change | Policy | Writes |
|---|---|---|---|
| `POST /api/v1/exports/journal-xlsx` | **new** — the export | `UseReportTemplates` | one `AuditEvent` on success |
| `GET /reports` | additive: `export` action + token meta tag when a report is shown | `UseReportTemplates` | nothing |
| host-wide, `/api/v1/*` | every error answer is the API-6 JSON body (401, 403, 400 token, 404, 405, 500) | — | — |

Unchanged: the template list and forms, the journal, Meet pages, the Control
Plane, the private port. `ContractVersion.Current` stays **1**. No operation
calls Google or reads a secret.

## 2. Decisions this stage made

### 2.1 `/api/v1/exports/{kind}`, not a form post

Spec FR-001 left the choice between the API-3 shape and a form post of the
report page. API-3 names the export shape explicitly ("a JSON body …; the UI
calls it by script with the antiforgery token in the header (API-7) and saves
the file") and API-2 / API-4 define its media type and `200`. A form post would
need a second, page-style error path (re-render the report page with messages)
for an action whose success answer is a file, not a page — exactly the case
API-3 was written for. So the export is `POST /api/v1/exports/journal-xlsx`.

### 2.2 `{kind}` = `journal-xlsx`

The kind names the report and the format: `journal-xlsx`. The success media type
is then fixed per operation, and US-029 adds `journal-docx` beside it without
touching this one. `journal` (not `report`) because §2 and §4 call the product a
journal and the Meet report will be a different kind. A format member in the
body was rejected: one operation would answer two media types.

### 2.3 Body members mirror the report query, as strings

`template`, `courseId`, `from`, `to`, `names` are the report page's own query
parameters under the same names, all JSON strings, validated by the **same
parsers in `Application`** (US-027 api-design §2.7, US-042 §2.1) — so the same
rule gives the same message (spec VR-001). `orientation` is the only new member.
`courseId` is a string, not a number, for that reason. A member of the wrong
JSON type (a number, an object), a duplicate member, or a body that is not a
JSON object is a **syntax** failure — `400` with
`Export.Validation.RequestMalformed`, no `fieldErrors` — because no field value
can be named honestly. The script never produces one.

`courseId`, `from`, `to` are **required** here (spec VR-001: no form-only state).
An absent one uses the malformed key of its field (`CourseMalformed` = "Choose a
course from the list."; `FromMalformed`, `ToMalformed`), so no new message is
needed. `template` and `names` keep the page's defaults (built-in; template's
setting) so the rules stay identical, though the page always sends both.

API-3 speaks of "courses" (plural); spec I-1 fixes one course per file. A later
Story (US-030) adds a new kind or an optional member; neither breaks this
contract (API-1).

### 2.4 Validation answers: all fields, first message on top

A `400` lists **every** failing field in `fieldErrors`, in the order of the
report page's message keys with `names` and `orientation` appended — the page
shows each. `message` is the first entry's text, so a client that shows one
line shows the first problem. A `404` uses the same shape (`TemplateNotFound`,
`CourseUnknown`), naming the problem as AC-009 requires. The value is never
echoed.

### 2.5 The first `/api/v1` resource completes the host's `/api/v1` rules

US-008 reserved the prefix and mapped only `409`. With a real client now, every
other mechanism that today redirects or renders HTML would hand a script an HTML
page with a `200` (a followed redirect) or an unreadable error:

| Mechanism | Outside `/api/v1` (unchanged) | Under `/api/v1` (new) | Message key |
|---|---|---|---|
| cookie challenge | `302 /sign-in` | `401` API-6 | `Api.Error.SignInRequired` (new) |
| restricted session (US-012) | `302 /sign-in/change-password` | `403` API-6 | `Api.Error.PasswordChangeRequired` (new) |
| policy denied | `403` error page | `403` API-6 | `Error.Forbidden` |
| antiforgery filter | `400` error page | `400` API-6 | `Error.PageExpired` |
| no route / wrong method | `404` error page | `404` / `405` API-6 | `Error.NotFound` |
| unmapped exception | `500` error page | `500` API-6 | `Error.Unexpected` |
| `ReadOnlyModeException` | `409` error page | `409` API-6 (US-008, unchanged) | BR-025 reason |

Each decision is made by path in the mechanism that already exists — the cookie
events, `TemporaryPasswordMiddleware`, `GlobalAntiforgeryFilter`, the status-code
re-execution, `InstallationExceptionHandler` — not by a second handler (AD-9,
API-10). The restricted session is still refused before any policy; only its
answer changes. Note for implementation: `TemporaryPasswordMiddleware` runs
before request localization, so the message is resolved with the account's
culture the way the exception handler already does (`RequestCultureScope`).

No operation of this contract can raise `409`: the export never calls the
read-only guard.

### 2.6 The page side: a button and a static script

The report page renders the action from a new view-model member `export`
(validated values only, `names` effective — spec FR-002), and the token in
`<meta name="request-verification-token">` (API-7). `wwwroot/js/report-export.js`
posts JSON with the header, saves a `200` under the name in
`Content-Disposition`, and shows `message` / `fieldErrors[].message` in an
`aria-live` region on error. No inline script; no `<form>` submission (a form
post would send `application/x-www-form-urlencoded` and get `415`). The
orientation radio pair defaults to `portrait` on every page load (OD-004 a).

### 2.7 File name header

`Content-Disposition: attachment` with both `filename` and `filename*`
(RFC 6266), produced by ASP.NET Core's `SetHttpFileName`: the fallback replaces
every non-ASCII character with `_`. The name itself is spec FR-006.1, built in
`Application`; the controller only passes it on.

### 2.8 Audit-before-file

The audit row is written and committed after rendering and before the response
starts; a failure of either is `500` with no file (spec FR-010). The file is a
byte array in memory, returned with `Content-Length`; nothing touches disk.

## 3. Operation notes

### `POST /api/v1/exports/journal-xlsx`

- Order: authentication → restricted session → policy → antiforgery →
  `Content-Type` (`415`) → body syntax → field validation → existence →
  build (the US-027/US-042 report query) → workbook model → render → audit →
  `200`.
- Read-only mode: identical in all three causes; no guard; the audit row is
  written (BR-026 closed list).
- Logging (spec FR-012): `JournalExported` `Information` with actor id, template
  id or built-in key, course id, period, name source and its origin, row and
  topic counts, file size in bytes; `JournalExportRefused` `Warning` with the
  field and rule names only; not-found as the report page. Never a value, name,
  email, title or file content.
- The workbook model and the `IReportRenderer` signature are `Application`
  types, not HTTP; their C# shape is fixed by DB_DESIGN's entity model (spec
  FR-004). The contract requires only: the renderer receives that model and the
  orientation and returns bytes; no ClosedXML type crosses into `Application`.

### `GET /reports`

- `export` non-null exactly when a report is shown (`200` with a course, an
  empty report included); null on form-only, `400`, `404`.
- The meta tag is rendered only with the action.

## 4. Authentication and authorization model

- Cookie session (API-7); `UseReportTemplates` — Admin ✔, Dean ✔ (§2
  "Использование шаблонов отчётов", "Просмотр и экспорт журнала успеваемости").
  No new policy, no matrix cell.
- Anonymous → `401`; restricted Dean → `403`; antiforgery header required.
- SC-4 anonymous list unchanged; the enumeration test sees the new path as
  protected.
- Actor and UI language come from the session (spec VR-002).

## 5. Error model

API-6 body for every error under `/api/v1` (§2.5). `fieldErrors[].field` is
one of `template`, `courseId`, `from`, `to`, `names`, `orientation`. New
translation keys (both `.uk` and `.en`, spec FR-013): `Api.Error.SignInRequired`,
`Api.Error.PasswordChangeRequired`, `Export.Validation.RequestMalformed`,
`Export.Validation.OrientationInvalid`, plus the page and file keys of spec
FR-013 (action, orientation labels, sheet names, header labels, student heading,
"course" fallback).

## 6. Acceptance Criterion → operation map

| AC | Operation / rule | Status codes |
|---|---|---|
| AC-001 | `exportJournalXlsx` (built-in template) | 200 |
| AC-002 | `exportJournalXlsx` (created templates) | 200 |
| AC-003 | `exportJournalXlsx` — cell types (spec FR-004.5) | 200 |
| AC-004 | `exportJournalXlsx` — language of the user | 200 |
| AC-005 | `Content-Disposition` (§2.7) | 200 |
| AC-006 | `exportJournalXlsx` in read-only mode; `getReport` shows the action | 200 |
| AC-007 | `x-writes` — audit on 200 only; `getReport` writes none | 200, 400, 404 |
| AC-008 | `api-v1-error-body` (1)–(3); allowed Admin, Dean | 200, 401, 403 |
| AC-009 | field validation, existence | 400, 404 |
| AC-010 | `Cache-Control` header | 200 |
| AC-011 | all — synthetic data, workbook read from response bytes | — |

TC-3: every status code of `exportJournalXlsx` (200, 400 ×3 causes, 401, 403
×2, 404, 405, 415, 500) has a test asserting the code and the API-6 shape.

## 7. Compatibility

Additive. `ReportPageModel` gains one nullable member. The `/api/v1` error
mapping changes only paths that had no operation before. Nothing outside
`/api/v1` changes.

## 8. Open questions for later stages

None blocking. For DB_DESIGN: the `AuditEvent` detail fields (spec FR-010) and
the built-in-template marker; the workbook model and `IReportRenderer` shape.
For SECURITY_REVIEW: ClosedXML 0.105.1 transitive packages and licences (spec
FR-011); the `/api/v1` mapping of §2.5 as a host-wide change.

Finding **A-1** (non-blocking): `Report.Validation.NameSourceMalformed` reads
"Unknown name source in the page address." Reused for the export as spec VR-001
requires ("same messages"); the wording still fits because the page sends the
value of its own address.
