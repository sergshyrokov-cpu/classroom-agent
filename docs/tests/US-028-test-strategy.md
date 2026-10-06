---
artifact_type: test_strategy
story: US-028
version: 1
status: DRAFT
created_at: 2026-10-06T06:02:00Z
updated_at: 2026-10-06T06:02:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-028-excel-export.md
    version: null
  - path: docs/specifications/US-028-spec.md
    version: 1
  - path: docs/decisions/US-028-open-decisions.md
    version: 2
  - path: docs/designs/api/US-028-api-design.md
    version: 1
  - path: docs/designs/api/US-028-openapi.yaml
    version: 1
  - path: docs/designs/database/US-028-db-design.md
    version: 1
  - path: docs/designs/database/US-028-entity-model.md
    version: 1
supersedes: null
---

# US-028 Test Strategy — Export a journal to Excel using a school template

## 1. Scope

The export end to end: the request and its validation, the reuse of the report
query, the mapping of the report into the workbook model, the ClosedXML file,
the HTTP contract `POST /api/v1/exports/journal-xlsx` with the host-wide
`/api/v1` error rules, the export action on the report page, the audit row and
its schema, read-only mode, translations and logging.

Out of scope (spec §10): a school's own Excel layout (US-043), Word (US-029),
several courses (US-030), print settings per school.

## 2. Levels

| Level | What | Classes |
|---|---|---|
| Unit — use case (TC-1) | `ExportJournalCommand` over the US-027 report world, a fake text port, a fake renderer and a unit of work that records the declared BR-026 write | `ExportJournalCommandTests` |
| Unit — rules | `ReportWorkbookMapper`, `JournalExportFileName`, `AuditEvent.JournalExported` | `ReportWorkbookMapperTests`, `JournalExportFileNameTests`, `JournalExportedAuditEventTests` |
| Unit — adapter | `ClosedXmlReportRenderer`: bytes read back with ClosedXML, in memory | `ClosedXmlReportRendererTests` |
| Integration — PostgreSQL (TC-2) | the migration's six columns and four constraints | `JournalExportAuditSchemaTests` |
| Integration — HTTP (TC-3, TC-5) | file, headers, parity with the page, language, audit, read-only, refusals, page action | `JournalExportTests` |
| Security | protected POST-only endpoint; 401 / 403 / 400 / 404 / 405 as API-6 JSON; outside `/api/v1` unchanged | `JournalExportAuthorizationTests` |
| Localization (NFR-073) | every `ReportText` member and every new message in uk and en; sheet-name length | `JournalExportTranslationTests` |
| Logging (SC-10) | success Information line, refusal Warning, no personal data, no rejected value | `JournalExportLoggingTests` |

## 3. Scenarios

**Positive.** Admin and Dean export the built-in journal; a created template
(hiding materials); an empty report (zero rows); orientation portrait by default
and landscape on request; English user; read-only mode in all three causes.

**Negative.** Each malformed field (course absent/empty/non-digit, dates
malformed or absent, period inverted, template malformed, name source wrong case,
orientation wrong case / padded / unknown); several at once in contract order;
unknown course, unknown template, both; malformed body (not JSON, array, number
member, duplicate member); form body (415); no token; GET; unknown `/api/v1`
path; anonymous; Dean on the forced change; renderer failure.

**Boundary.** Grade labels `0`, `100`, `012`, `12.5`, `-3`, ` 12`; one-day
period; 40 000-character title cut to 32 767; file name over 100 characters,
only dots/spaces, control characters; sheet names ≤ 31.

**Validation.** VR-001 through the command (before any read: no field-source
or template-repository call) and over HTTP (API-6 `fieldErrors`, value not
echoed).

**Security.** S-01 … S-03 (protected, POST, token), S-05 (value never echoed or
logged), S-06 (audit row free of names, emails, titles, course name), S-08
(no formula; quote prefix), S-09 (file name), S-10 (`no-store`), S-04 (no Google
call: `FakeClassroomReader.ImpersonatedAs` empty; no Google port in the command).

**Persistence.** db-design §3: columns, types, nullability; each constraint by
name; a valid built-in and created-template row.

## 4. Fixtures

- `JournalExportWorld` — the US-027 `ReportTemplateWorld` plus `FakeReportTexts`
  (marker texts), `FakeReportRenderer` (captures the workbook, can fail) and
  `ScopeRecordingUnitOfWork` (records `ServiceWriteScope.Current` per commit).
- `JournalExportHostExtensions` — JSON body, token header, workbook from the
  response bytes, `filename*`, the export audit rows by raw SQL, and
  `ReportText(…)` resolving a port member through the host's translations.
- `FormClient.SendForBytesAsync` / `BinaryResponse` — a binary body is never
  read as text.
- Existing: `JournalHostExtensions` (sign-in per actor, seeded September
  journal), `ReportHostExtensions` (created templates), `ReadOnlyModeHost`.

All data synthetic (TC-4). No file is written to disk or the repository.

## 5. Decisions the tests pin

- Translation keys named by the API design: `Api.Error.SignInRequired`,
  `Api.Error.PasswordChangeRequired`, `Export.Validation.RequestMalformed`,
  `Export.Validation.OrientationInvalid`. One key chosen here:
  **`Report.Export.Action`** for the button (spec FR-013 left the name open).
  The file's texts are checked through `IReportTexts`, so their keys stay free.
- Page markup (FR-002): `<meta name="request-verification-token">`, the script
  `/js/report-export.js`, radio values `portrait` / `landscape`, and the action's
  `data-course-id` and `data-names` attributes (api-design §2.6 "`data-`
  attributes").
- Log event names `JournalExported` and `JournalExportRefused` (api-design §3).
- Workbook layout per entity model §2.1: header block rows 0–3, empty row 4,
  table header or empty-state message at row 5 (Excel row 6).

## 6. Excluded and limitations

- The browser script itself (download, error display) is not executed: there is
  no browser runner in the test project (TC). The page markup it relies on and
  the endpoint it calls are tested.
- Column-width "follows content" is asserted only as the 60-character cap.
- Printing is asserted through the page-setup properties, not a printer.
- The CSP of a later Story is not tested; "no inline script" is asserted only by
  the script file reference.

## 7. Open Decisions

OD-005 (compile-only skeleton) raised and resolved here. None open.
