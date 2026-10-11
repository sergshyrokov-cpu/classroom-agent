---
artifact_type: security_review
story: US-044
version: 1
status: APPROVED
created_at: 2026-10-11T09:33:24Z
updated_at: 2026-10-11T09:33:24Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-044-meet-reports-export.md
    version: null
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/designs/api/US-044-api-design.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/designs/database/US-044-db-design.md
    version: 1
  - path: docs/designs/database/US-044-entity-model.md
    version: 1
  - path: docs/tests/US-044-test-strategy.md
    version: 1
  - path: docs/tests/US-044-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-044-open-decisions.md
    version: 2
  - path: docs/evidence/US-044-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-044 Security Review — Export the built-in Meet reports to Excel

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor findings; three informational notes.

US-044 adds two authenticated POST endpoints that render the US-033 Meet reports
into an in-memory `.xlsx` and return it as an attachment. The export reads only
already-synced data through the US-033 queries, calls no Google port, writes one
audit row (a declared BR-026 service write, so it works in read-only mode) and
logs ids and counts only. Principal controls verified: policy `ViewMeetReports`
(Admin, Dean) on both actions, global antiforgery with no exemption, strict JSON
body reading with member allow-list, actor and role from the session, a single
"not found" for an unknown course, unknown meeting and meeting of another course,
formula-injection quoting of every text cell, file names without personal data,
and `Cache-Control: no-store` from the host-wide rule.

The review was independent code, configuration and test review plus a fresh
build, full test run and vulnerability scan. It is not a penetration test.

## 2. Reviewed Artifacts

As listed in `inputs` above. The specification is `APPROVED` (HUMAN_SPEC_APPROVAL
recorded 2026-10-11T07:05:13Z); the other inputs are un-reviewed `DRAFT`
artifacts as this workflow variant expects; none is `SUPERSEDED`. All fourteen
Open Decisions are resolved.

## 3. Security-Relevant Scope

| Host | Exposed functionality |
|---|---|
| Data Plane (public port) | `POST /api/v1/exports/meet-reports-xlsx`, `POST /api/v1/exports/meet-meeting-xlsx`; export button on `MeetReports/Index` and `MeetReports/Meeting`; static `wwwroot/js/meet-export.js` |
| Control Plane | not touched |

Assets: Meet participation of students and staff (names, emails, durations),
course names, meeting codes (Google mirror); `audit_event` rows.

Trust boundaries: browser → installation (antiforgery-protected JSON POST);
controller → Application use cases; Application → PostgreSQL (read, audit write);
Application → `IReportRenderer` (ClosedXML, in memory). No new boundary to Google
or the Control Plane.

Changed components: `MeetExportController`, `ExportMeetReportsCommand`,
`ExportMeetMeetingCommand`, two workbook mappers, `MeetExportLayout`,
`MeetExportFileName`, `JournalExportFileName.CoursePart` (extracted, behaviour
unchanged), `ClosedXmlReportRenderer` (time/date-time/number formats, autofilter),
`AuditEvent` + migration `20261011075017_AddMeetExportAudit`,
`PermittedServiceWrites`, `MeetExportLog`, two Razor views, translations.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build` — 4893 total, 4893 passed,
  0 failed, 0 skipped (5 m 48 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` —
  no vulnerable packages in any of the seven projects (nuget.org source).
- `git diff HEAD -- '*.csproj'` — empty: no package or project reference added.
- Delegation: a `cheap-worker` run of the commands above failed on a session
  limit before producing output; the commands were then run directly by this
  reviewer. All code reading and judgement were done by the reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policy `ViewMeetReports` = Admin, Dean (`InstallationSecurityServices.cs`), the §2 row "Просмотр и экспорт статистики Meet-занятий"; no new role. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login path touched. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewMeetReports)]` on the controller; no `[AllowAnonymous]`, no antiforgery exemption (`TheExport_IsAProtectedPostOnlyEndpoint`); 401/403/400/405/415 answer with the API-6 body (`MeetExportAuthorizationTests`); export is POST, no GET state change. |
| SC-5 Read-only mode | PASS | No `IReadOnlyModeGuard` by design; the only write is `PermittedServiceWrite.AuditEvent`, declared in `PermittedServiceWrites` and scoped with `writeScope.Declare`; `InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi` for both endpoints and every cause. |
| SC-6 No DB UI | PASS | No diagnostic or database endpoint added. |
| SC-7 Key | NOT_APPLICABLE | No key or key reference handled. |
| SC-8 Google | PASS | No Google port in either command's dependency graph; tests assert no Google call. |
| SC-9 Channel | NOT_APPLICABLE | Control Plane channel untouched. |
| SC-10 Hygiene | PASS | API-6 bodies carry message keys only, never a submitted value; `MeetExportLog` logs ids, dates, counts, orientation, byte size; refusal log names field and rule only; `MeetExportLoggingTests` assert no name, email or meeting code in the log files. |
| SC-11 Audit | PASS | `AuditEvent.MeetReportsExported` / `MeetMeetingExported` — actor, role, course, period or meeting id, counts, format, request id; no personal data; committed before the file is returned; CHECK constraints `ck_audit_event_meet_*_export_shape`; `MeetExportAuditEventTests`, `MeetExportAuditSchemaTests`. Matches the SC-11 export row (meeting id and row counts). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and `ControlPlane` untouched. |
| SC-13 Outbound | PASS | The script posts only to the installation (`credentials: "same-origin"`, relative URLs); the file is produced and returned in-process. |
| SC-14 no-store | PASS | Host-wide rule in `Program.cs` applies to the file responses (as for US-028). |

## 6. Authentication and Authorization

Both actions require an authenticated Admin or Dean. A Dean on the forced
password change gets 403 (existing session rule, tested). The actor id and role
are read from claims (`ActorId()`, `ActorRole()`); nothing in the body can name
an actor. There is no roster scoping, consistent with the §2 matrix and SC-1.
Identifiers in the body can address any synced course — every Admin and Dean may
see every course's Meet reports, so this is not a horizontal escalation. A
meeting id is only accepted together with its own course
(`GetMeetingOfCourseAsync`); a mismatch is the same 404 as an unknown meeting.

Forbidden-role coverage (TC-5): v1 has no installation role outside the policy,
so the forbidden cases are anonymous (401) and a Dean pending a password change
(403); both are tested. See INFO-3.

## 7. Credentials, Key and Google Access

No password, credential, key or scope is handled. No Google port is reachable
from the new code.

## 8. Sensitive Data Exposure

- **Responses:** the file contains names, emails and Meet data of the course —
  exactly what the page shows to the same roles (OD-005, OD-013). Error bodies
  carry translated messages and field names only.
- **DTOs:** `MeetReportsExportAction` / `MeetMeetingExportAction` carry ids and
  dates only; no entity reaches a view (AD-8).
- **File name:** course part cleaned by the shared `CoursePart` rule, plus dates
  or start time; never a person's name, email or meeting code
  (`MeetExportFileNameTests`). The header is written through
  `ContentDispositionHeaderValue.SetHttpFileName`, so a course name from Google
  cannot inject header content.
- **Spreadsheet injection:** every text cell from Google data goes through
  `WorkbookCell.OfText`, which quotes leading `= + - @ \t \r`.
- **Logs and audit rows:** ids and counts only (see SC-10, SC-11).
- **Disk:** the workbook is rendered in memory; nothing is written to a file.
- **Telemetry and fixtures:** synthetic test data only (`MeetExportWorld`).

## 9. Input Validation

Content type must be JSON (415 otherwise). The body must be a JSON object; known
members must be strings and must not repeat; unknown members are ignored and
never echoed; any syntax failure is 400 `RequestMalformed`. Ids are positive
64-bit integers via `ParseCourseId`; dates go through the page's `ReadDate`;
`from > to` is refused; orientation is a closed set. Every failing field is named
with its key; the value is never returned or logged. Request body size is bounded
only by Kestrel's default — see INFO-1.

## 10. API Security

Only the two approved operations exist, both POST, matching the OpenAPI contract
(paths, request schemas, 200 attachment, 400/401/403/404/405/415 API-6 bodies).
The script sends the token in the `RequestVerificationToken` header; the token is
emitted by the view only when the export action is present. The page script uses
`textContent` for error messages (no `innerHTML`); it is a static file, so no
inline script is needed.

## 11. Persistence and Configuration

Migration `AddMeetExportAudit` adds two nullable columns to `audit_event`
(`export_meeting_id`, `export_meetings`), extends the action CHECK, rewrites the
export CHECKs so every other action keeps the new columns null, and adds one
shape CHECK per new action. No `EnsureCreated()`. No configuration or connection
string change. Audit rows remain insert-only (no update/delete path added).

## 12. Logging, Audit and Telemetry

Log events 5440–5442 contain no personal data (tested). Both successful exports
write and commit one audit row before the file is returned; a refused export
writes nothing. Hook telemetry is unaffected.

## 13. Dependencies

No package or project reference added (`*.csproj` unchanged). Vulnerability scan
clean for all projects. `Application` still references only `Domain`.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| Allowed roles | `MeetExportAuthorizationTests.AnAllowedRole_Exports_With200` (Admin, Dean × 2 endpoints) |
| Anonymous / forced change | `Anonymous_Is401_WithTheApiBody`, `ADeanOnTheForcedPasswordChange_Is403_WithTheApiBody` |
| Antiforgery | `WithoutTheToken_Is400_WithTheApiBody`, `TheExport_IsAProtectedPostOnlyEndpoint` |
| Method / content type / syntax | `AGet_Is405…`, `AFormBody_Is415…`, malformed-body theory |
| Read-only mode | `InReadOnlyMode_TheExportWorks_IsAudited_AndCallsNoGoogleApi` (both endpoints), `PermittedServiceWriteTests` |
| Audit | `MeetExportAuditEventTests`, `MeetExportAuditSchemaTests` |
| No personal data in logs | `MeetExportLoggingTests` |
| File name | `MeetExportFileNameTests` |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Cross-site POST from another origin | Antiforgery token required | tests 400 without token | PASS |
| Export in read-only mode | Works, audited, no Google call | read-only tests | PASS |
| Meeting id of another course | Same 404 as unknown | `GetMeetingOfCourseAsync`; not-found tests | PASS |
| Body names an actor or role | Ignored; session only | `ActorId()`, `ActorRole()` | PASS |
| Course name `=HYPERLINK(...)` from Google | Quoted as text | `WorkbookCell.OfText` | PASS |
| Course name with CR/LF or quotes | Header encoded, forbidden chars replaced | `SetHttpFileName`, `CoursePart` | PASS |
| Probing for course existence | Allowed roles already see all courses | §2 matrix | PASS |
| Very long period or large body | Not limited by requirements | INFO-1 | INFO |

## 16. Repository Hygiene

No secret-like, `.db` or `.xlsx` file in the change set. `google_credentials.json`,
`dac-classroom-agent-*.json`, `classroom_cache.db` and `*.xlsx` (except the
report templates) are git-ignored; credential files were not opened.

## 17. Deviations

None from the specification, API or DB design. `MeetExportController` repeats
the journal controller's JSON reader and actor helpers instead of sharing them
(implementation report; INFO-2).

## 18. Findings

### INFO-1 — No upper bound on the exported period or the request body

- Severity: Informational · Category: OTHER
- Files: `ExportMeetReportsCommand.cs`, `MeetExportController.cs`
- Evidence: the period has no maximum length; the JSON body is bounded only by
  Kestrel's default request size (30 MB).
- Risk: an authenticated Admin or Dean can request a very large export,
  consuming memory on the installation. Same as the page and the US-028 export.
- Correction: none required now; no requirement defines a limit. If one is
  wanted, it is a requirement change (`trebovaniya.md` §7), not a Story fix.
- Loop-back: none.

### INFO-2 — Duplicated request-reading and actor helpers

- Severity: Informational · Category: OTHER
- File: `MeetExportController.cs` (`ReadBodyAsync`, `ActorId`, `ActorRole`)
- Evidence: logic repeated from `JournalExportController` (stated in the
  implementation report; refactoring US-028 was out of scope).
- Risk: a future security fix to one copy may miss the other.
- Correction: none required; consider sharing in a later Story.
- Loop-back: none.

### INFO-3 — TC-5 forbidden-role case is structural

- Severity: Informational · Category: TEST_COVERAGE
- File: `MeetExportAuthorizationTests.cs`
- Evidence: both v1 installation roles are allowed, so the forbidden side is
  covered by anonymous 401 and the forced-password-change 403.
- Risk: none today. When Epic 7 adds Teacher/Student roles, this endpoint needs
  a forbidden-role test.
- Loop-back: none.

## 19. Positive Controls

Policy on the controller; global antiforgery without exemption; strict JSON
reading and allow-listed members; session-only actor; uniform 404 for meeting of
another course; read-only mode honoured via the declared service write; no
Google port in the graph; formula quoting; safe Content-Disposition; in-memory
rendering; `no-store`; audit row committed before delivery with DB CHECKs; log
lines free of personal data; `textContent` in the script; no new dependency.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- No penetration test or live browser session; behaviour verified through code,
  configuration and the integration test suite.
- The vulnerability scan relies on the nuget.org advisory data at run time.
- The generated `.xlsx` content was verified through the renderer tests, not by
  opening files in Excel.

## 22. Verdict Rationale

Green build and full test suite (4893/4893, none skipped), clean vulnerability
scan, every touched SC item PASS with evidence, required security tests present
and passing, all Open Decisions resolved. Only informational notes remain, none
requiring a change before `HUMAN_PR_APPROVAL`.
