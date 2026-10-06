---
artifact_type: security_review
story: US-028
version: 1
status: APPROVED
created_at: 2026-10-06T10:05:00Z
updated_at: 2026-10-06T10:05:00Z
produced_by: security-reviewer
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
  - path: docs/tests/US-028-test-strategy.md
    version: 1
  - path: docs/tests/US-028-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-028-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-028 Security Review — Export a journal to Excel

## 1. Executive Summary

**Result: PASS.** No Critical or Major findings.

The Story adds one authenticated endpoint, `POST /api/v1/exports/journal-xlsx`
(Admin, Dean), that renders the report page's own report as `.xlsx` in memory
and commits one `journal_exported` audit row before returning the file. It also
changes host-wide error handling under `/api/v1` to the API-6 JSON body, and
adds ClosedXML 0.105.1 (OD-001).

Principal controls verified: role policy per `trebovaniya.md` §2; global
antiforgery covers the new POST; actor/role/language from the session only;
request members restricted to the six documented strings; no Google port in
the use case; read-only mode lets export run with the audit row as a declared
BR-026 service write; logs and audit carry ids, dates, codes and counts only;
spreadsheet cells are text values with a quote prefix on formula-leading
characters; nothing written to disk.

Build 0 warnings; 3 776 tests green (re-run independently); NuGet reports no
vulnerable packages. One Minor defense-in-depth recommendation (request body
size cap).

## 2. Reviewed Artifacts

As in the front matter. All inputs current; none `SUPERSEDED`. Specification v1
approved at `HUMAN_SPEC_APPROVAL` (history 2026-10-05T13:55:58Z). OD-001 …
OD-005 resolved.

## 3. Security-Relevant Scope

- **Installation host (`Web`)**: new `JournalExportController`; `/api/v1`
  error mapping in the cookie events, `TemporaryPasswordMiddleware`,
  `GlobalAntiforgeryFilter`, `ErrorController` and the `Program` fallback;
  report page gets the export button and a static script.
- **Application**: `ExportJournalCommand`, `ReportRequestReader`,
  `ReportBuilder` (split out of `GetReportQuery`), `ReportWorkbookMapper`,
  `JournalExportFileName`.
- **Infrastructure**: `ClosedXmlReportRenderer`; `audit_event` columns and
  check constraints (migration `AddJournalExportAudit`).
- **Assets**: student names, grades and submission data (personal data of
  minors) in the exported file; audit rows; session cookie / antiforgery token.
- **Trust boundaries**: browser → installation (public HTTPS); controller →
  Application; Application → renderer port; Application → PostgreSQL. No
  Google or Control Plane boundary is touched.
- Control Plane: not touched.

## 4. Environment and Tools

- .NET SDK 10.0.401, Windows 10; Docker available — Testcontainers tests ran.
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 3 776 total, 3 776 passed, 0 failed,
  0 skipped (3 m 35 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable packages in any project (source nuget.org).
- `dotnet list … package --include-transitive` for Infrastructure; nuspec
  licences.
- Delegated (read-only fact-gathering): build/test/package/licence runs and
  `git` change set (cheap-worker); pattern sweeps and endpoint/policy listing
  (quick-look). Every finding below rests on code read directly by the
  reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policy `UseReportTemplates` = `RequireRole(Admin, Dean)` (`InstallationSecurityServices.cs:85`); §2 "Просмотр и экспорт журнала успеваемости" ✔ Admin ✔ Dean (`trebovaniya.md:550`). No new role. |
| SC-2 Authentication | PASS | No authentication change. Restricted (temporary-password) Dean gets `403` under `/api/v1` instead of a redirect (`TemporaryPasswordMiddleware.RefuseApiAsync`); test `ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody`. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Sign-in path unchanged. |
| SC-4 Authorization | PASS | `[Authorize(Policy = UseReportTemplates)]` on the controller; POST only; no new `[AllowAnonymous]`; global antiforgery filter covers the action (no exemption); refusal is the API-6 body under `/api/v1`, page elsewhere. Tests: `TheExport_IsAProtectedPostOnlyEndpoint`, `Anonymous_Is401_WithTheApiBody`, `WithoutTheToken_Is400_WithTheApiBody` (both roles), `AGet_Is405_WithTheApiBody`, `AnUnknownApiPath_Is404_WithTheApiBody`, `OutsideTheApi_AnonymousStillGoesToSignIn`, `InstallationCookieTests.EveryStateChangingPublicEndpoint_WithoutToken_Returns400`. |
| SC-5 Read-only | PASS | No guard by design (BR-026: viewing and export work); the one write, the audit row, is committed inside `ServiceWriteScope.Declare(PermittedServiceWrite.AuditEvent)`; `ExportJournalCommand` is in the `PermittedServiceWrites` registry; no Google port in its constructor. Tests `InReadOnlyMode_…` (Web ×3, Application). |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | Key handling untouched. |
| SC-8 Google | PASS | Export reads synced data only (`IJournalFieldSource`, `IReportTemplateRepository`); no Google port in the graph. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene | PASS | API-6 bodies carry translated messages only, no exception text or type names (`ApiErrorResponse.Create`); log lines are ids, dates, codes, counts, status and rule names (`JournalExportLog`); refused values never logged — test `ARefusedValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged`; success line test asserts no personal data. Unknown body members ignored and never echoed. |
| SC-11 Audit | PASS | `AuditEvent.JournalExported`: actor, role, course target, period, template id / built-in marker, row count, format, request id — no names, no content. Check constraints shape the columns. Tests `AnExport_WritesOneAuditRow_WithoutPersonalData`, `ViewingTheReport_WritesNoAuditRow`, `JournalExportedAuditEventTests`, `JournalExportAuditSchemaTests`. Row committed before the file is returned. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and Control Plane untouched. |
| SC-13 Outbound | PASS | The script posts only to the installation's own origin (`credentials: "same-origin"`); the file goes to the requesting browser only; no new outbound destination. |
| SC-14 no-store | PASS | Host-wide `NoStoreMiddleware` applies; asserted in `AnAdmin_ExportsTheBuiltInJournal_AsAnXlsxAttachment`. |

## 6. Authentication and Authorization

| Caller | Result | Test |
|---|---|---|
| Admin | `200` file | `AnAdmin_ExportsTheBuiltInJournal_…` |
| Dean | `200` file | `JournalExportTests` (Dean cases) |
| Dean with temporary password | `403` API-6 | `ADeanOnTheForcedPasswordChange_Is403_…` |
| Anonymous | `401` API-6 (no redirect under `/api/v1`) | `Anonymous_Is401_WithTheApiBody` |
| Any role, no antiforgery header | `400` API-6 | `WithoutTheToken_Is400_…` |

There is no third installation role, so "forbidden role" is covered by the
anonymous and restricted-session cases (TC-5). No per-course scoping exists in
v1 (no roster-based visibility — Hard Stop), so any Admin/Dean may export any
synced course, as on the report page. Actor id and role come from claims
(`ActorId`, `ActorRole`), never the body.

## 7. Credentials, Key and Google Access

No password, key or Google access is touched. `TemporaryPasswordMiddleware`
reads the UI language from the server-issued claim or the school default.

## 8. Sensitive Data Exposure

- **File content**: personal data the report page already shows to the same
  roles (approved by the Story/spec). Built by the same `ReportBuilder` as the
  screen (FR-003), so no additional field reaches the file.
- **File name**: course name and period only (`JournalExportFileName`); control
  and Windows-forbidden characters replaced; header written via
  `ContentDispositionHeaderValue.SetHttpFileName` (RFC 6266 encoding, no header
  injection).
- **Formula injection**: text cells set with `SetValue(string)` (a string value,
  never a formula), plus `IncludeQuotePrefix` when the value begins with
  `= + - @ \t \r` (`WorkbookCell.FormulaLeads`). Data from Google cannot become
  an executable formula on open or on edit.
- **Disk**: rendered in a `MemoryStream`; nothing written to disk or temp.
- **Logs / audit**: see SC-10, SC-11.
- **Error bodies**: translated keys only.
- **View**: data attributes rendered through Razor encoding; values are
  validated ids, dates and codes — no student name in the page attributes or
  the request (spec, US-039 INFO-1). No `Html.Raw`, `innerHTML` or inline
  script; script error output uses `textContent`.

## 9. Input Validation

- Content type checked first (`415`), then JSON syntax: an object, known
  members strings, no duplicates; anything else `400` without naming a value
  (api-design §2.3).
- Field rules are the report page's own (`ReportRequestReader.Read(required:
  true)`); `orientation` an exact case-sensitive enum. Course existence → `404`.
- No body member can set actor, role, language or a template body.
- See finding M-1 on body size.

## 10. API Security

Only the documented operation exists (openapi `exportJournalXlsx`). Response
fields: the file, or the API-6 body. Host-wide `/api/v1` error mapping (401,
403, 400 token, 404, 405, 415, 500) each has a test asserting status and body
shape. Outside `/api/v1` behaviour is unchanged (test
`OutsideTheApi_AnonymousStillGoesToSignIn`). See I-1 for the `405` on the
anonymous fallback.

## 11. Persistence and Configuration

Migration `20261006062229_AddJournalExportAudit` adds six nullable typed
columns and check constraints (drops/re-adds only check constraints to extend
their value lists; no data loss). No `EnsureCreated`. No `appsettings` change.
`.gitignore` still excludes `*.xlsx` (except `docs/product/report-templates/`),
`classroom_cache.db` and the credential files.

## 12. Logging, Audit and Telemetry

Event 5270 (success): template reference, course id, period, account id, name
source code and origin, counts, byte length. Event 5271 (refusal): request id,
status, `field:rule` pairs. No names, emails, grades, titles or values. Audit
per SC-11 above. Hook telemetry not changed by this Story.

## 13. Dependencies

- ClosedXML 0.105.1 in `ClassroomAgent.Infrastructure` only — approved by
  OD-001. Committed with the skeleton (OD-005); no `.csproj` change in the
  working tree.
- New transitive packages and licences: ClosedXML.Parser 2.0.0 (MIT),
  DocumentFormat.OpenXml 3.1.1 (MIT), DocumentFormat.OpenXml.Framework 3.1.1
  (MIT), ExcelNumberFormat 1.1.0 (MIT), RBush.Signed 4.0.0 (MIT),
  SixLabors.Fonts 1.0.0 (Apache-2.0), System.IO.Packaging 8.0.1 (MIT).
  `System.Management` / `System.CodeDom` are pre-existing (from
  Google.Apis.Auth), not this Story.
- `dotnet list package --vulnerable --include-transitive`: none found against
  nuget.org at review time. This is advisory-database evidence, not a
  guarantee.
- No new project reference; `Application` still has no Infrastructure
  reference; no ClosedXML type outside `ClosedXmlReportRenderer`.

## 14. Security Test Coverage

| Requirement / abuse case | Test |
|---|---|
| Allowed roles | `JournalExportTests` (Admin, Dean) |
| Anonymous refused | `Anonymous_Is401_WithTheApiBody` |
| Restricted session refused | `ADeanOnTheForcedPasswordChange_Is403_…` |
| CSRF | `WithoutTheToken_Is400_…`; host-wide token test |
| POST only | `TheExport_IsAProtectedPostOnlyEndpoint`, `AGet_Is405_…` |
| Read-only works, audit as service write | `InReadOnlyMode_…`, `PermittedServiceWriteTests` |
| Audit without personal data | `AnExport_WritesOneAuditRow_WithoutPersonalData` |
| Logs without personal data / values | `JournalExportLoggingTests` (2) |
| File name has no person | `TheFileName_IsTheCourseAndPeriod_AndCarriesNoPerson` |
| Formula-leading text | `ReportWorkbookMapperTests`, renderer kind tests |
| Synthetic fixtures, no live Google | substituted ports (TC-4) |

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Cross-site POST from another origin | refused | global antiforgery; header token required | PASS |
| Anonymous direct call | `401`, no data | test | PASS |
| Dean bypassing forced password change via the API | `403` | test | PASS |
| Body with `actorId`/`role` members | ignored | `Members` allow-list; actor from claims | PASS |
| Student name `=HYPERLINK(...)` in Google data | stays text | `SetValue(string)` + quote prefix | PASS |
| Course name with `"`, CR/LF, `/` | safe file name and header | `JournalExportFileName`, `SetHttpFileName` | PASS |
| Malformed or oversized JSON | `400`, value not logged | parser + tests; size see M-1 | PASS (M-1) |
| Export in read-only mode | works, audited, no Google call | tests | PASS |

## 16. Repository Hygiene

No secret-like file, database file or `.xlsx` in the change set. Credential
files remain git-ignored (not opened). Out-of-scope files in the working tree:
`.claude/skills/dotnet-implementor/SKILL.md`, `.claude/skills/test-writer/SKILL.md`
— not Story code; the implementation report already says to commit them
separately (I-4).

## 17. Deviations

Implementation-report deviations D-1 … D-5 reviewed: none weakens a control.
D-2 (415 in the controller after the antiforgery filter) keeps the documented
order; D-3 (405 from the fallback) see I-1; D-4 (unescaped Cyrillic) still
escapes HTML-sensitive characters.

## 18. Findings

### M-1 — Minor — INPUT_VALIDATION — no explicit request-body limit on the export
- **File**: `src/ClassroomAgent.Web/Controllers/JournalExportController.cs` (`ReadBodyAsync`).
- **Evidence**: `JsonDocument.ParseAsync(Request.Body)` reads the whole body;
  only Kestrel's default limit (≈30 MB) bounds it. A legitimate body is under
  1 KB.
- **Expected**: not required by any approved artifact (rate/size limits are not
  defined for this Story).
- **Risk**: an authenticated Admin/Dean can make the server buffer up to the
  default limit per request. Low: requires a valid session and token.
- **Correction (recommendation)**: a small `[RequestSizeLimit]` on the action,
  if a future Story or Open Decision sets one. Not blocking.
- **Loop-back**: none.

### I-1 — Informational — API_SECURITY — `405` with `Allow` on the anonymous fallback
An anonymous `GET /api/v1/exports/journal-xlsx` gets `405` + `Allow: POST`
instead of `404`, revealing the route exists. Approved in api-design §2.5
rule (5); reveals no data.

### I-2 — Informational — DEPENDENCY — licence of SixLabors.Fonts
SixLabors.Fonts 1.0.0 is Apache-2.0 (all other new packages MIT). Compatible;
recorded for the Owner as FR-011 / S-12 asked.

### I-3 — Informational — DATA_EXPOSURE — the file holds personal data once downloaded
After download the file is outside the system's control (`Cache-Control:
no-store` prevents only caching). This is the approved purpose of the Story.

### I-4 — Informational — REPOSITORY_HYGIENE — skill files outside the Story
Two `.claude/skills/*/SKILL.md` changes in the working tree are not in the
Story's scope; commit separately, not in the Story commit.

## 19. Positive Controls

Role policy on the controller; global antiforgery with API-6 refusal; claims-only
actor; member allow-list; content-type check; shared validation rules; audit
committed before delivery; declared service write in read-only mode; no Google
port; in-memory rendering; text-only cells with quote prefix; RFC 6266 file
name; log lines without personal data or values; translated error bodies only.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- Code, configuration and test review plus a full test run; no penetration
  test and no manual browser session.
- The vulnerability check depends on the NuGet advisory database at review time.
- ClosedXML's own parsing of its output was not fuzzed; it only writes
  workbooks here, it never reads uploaded files.

## 22. Verdict Rationale

Green build and tests independently confirmed; no Critical or Major finding;
every touched SC item PASS; security-sensitive ACs (AC-006, AC-007, AC-008,
AC-009) covered by passing tests; no blocking Open Decision. One Minor and four
Informational findings are non-blocking. Verdict **PASS** → `HUMAN_PR_APPROVAL`.
