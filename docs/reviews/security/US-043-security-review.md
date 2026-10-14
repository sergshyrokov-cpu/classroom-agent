---
artifact_type: security_review
story: US-043
version: 1
status: APPROVED
created_at: 2026-10-14T08:12:38Z
updated_at: 2026-10-14T08:12:38Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: docs/specifications/US-043-spec.md
    version: 2
  - path: docs/decisions/US-043-open-decisions.md
    version: 2
  - path: docs/designs/api/US-043-api-design.md
    version: 1
  - path: docs/designs/api/US-043-openapi.yaml
    version: 1
  - path: docs/designs/database/US-043-db-design.md
    version: 1
  - path: docs/tests/US-043-test-strategy.md
    version: 1
  - path: docs/tests/US-043-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-043-implementation-report.md
    version: 2
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-043 Security Review — export into the shipped "Бланк для групових занять"

## 1. Executive Summary

**Result: PASS.** The Story adds a second layout to the existing US-028 endpoint
`POST /api/v1/exports/journal-xlsx`: the same report filled, copy by copy, into a
form shipped inside `ClassroomAgent.Infrastructure` as an embedded resource. No new
endpoint, policy, role, Google call, outbound destination, package, entity or
migration. Nothing is uploaded; the only file read is the program's own embedded
form.

Principal controls verified: unchanged `UseReportTemplates` policy (Admin, Dean)
with antiforgery; strict server-side validation of the three new members
(`layout` exact string, per-copy values whole JSON numbers in range); a 100-sheet
limit checked before rendering (bounds output size); values only — no formula is
written, formula-leading text gets the quote prefix; in-memory rendering; logs
carry codes and counts only; audit row unchanged.

No Critical, Major or Minor findings. Three Informational notes (§18).

## 2. Reviewed Artifacts

See front matter `inputs`. Spec v2 and open decisions v2 are `APPROVED`
(HUMAN_SPEC_APPROVAL 2026-10-14T05:59:11Z); OD-013 resolved by the Owner
2026-10-14. No input is `SUPERSEDED`. DB design is `NOT_APPLICABLE` (no entity
model by design).

## 3. Security-Relevant Scope

- Host: installation (Data Plane) only. Control Plane untouched.
- Exposed functionality: the existing journal export endpoint, new body members
  `layout`, `studentsPerCopy`, `lessonsPerCopy`; the report page gains a layout
  radio group and two number inputs (`Views/Report/Index.cshtml`,
  `wwwroot/js/report-export.js`).
- Assets: student names and grades, lesson titles, teacher names, course name —
  all written into the downloaded file (approved by the Story, as US-028).
- Trust boundaries: browser → installation (JSON body); controller → use case
  (`ExportJournalCommand`); Application → new port `IBlankFormRenderer`
  (`ClosedXmlBlankFormRenderer` in Infrastructure); Application → PostgreSQL
  (existing report read and audit write).
- Changed security components: request parsing in `JournalExportController`,
  validation in `ExportJournalCommand`, new log message
  `JournalExportBlankFormLog`, DI registration.

## 4. Environment and Tools

- .NET 10 SDK; Docker Desktop 29.8.0 (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 5640 total: 5639 pass, 1 fail, 0 skipped
  (6 min 28 s). The failure is `MeetingCodeLinkRepositoryTests.AMark_RoundTrips`
  (US-032 persistence, file not touched by this Story); re-run of that class 3×
  in isolation: 10/10 pass each time. The implementation report records a full
  green run (5640/5640). Treated as an intermittent test outside scope (INFO-1).
- `dotnet list package --vulnerable --include-transitive` for Application,
  Infrastructure, Web — no vulnerable packages for current sources.
- Static inspection of the form file's OOXML parts (shared strings, formulas,
  defined names, external links, macros) with a Python zip reader run with `-I`.
- Delegated (cheap-worker): build, full test run, vulnerability scans, csproj diff.
  Code review and all judgements inline.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role or matrix cell added; policy unchanged (`JournalExportController` `[Authorize(Policy = InstallationPolicies.UseReportTemplates)]`). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No Admin login change. |
| SC-4 Authorization | PASS | Same endpoint and policy; no `[AllowAnonymous]`/`[IgnoreAntiforgeryToken]` in changed files; POST with `RequestVerificationToken` header (JS unchanged in that respect). Tests: `GroupLessonsBlankAuthorizationTests` (Admin/Dean allowed, anonymous 401, forced-password-change Dean 403, missing token 400 — all with API-6 body). |
| SC-5 Read-only mode | PASS | Export is a read (BR-026); only write is the existing audit row through `ServiceWriteScope`; no `IReadOnlyModeGuard` by design (spec FR-009); no Google port injected into `ExportJournalCommand`. |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | NOT_APPLICABLE | No key or credential touched. |
| SC-8 Google | PASS | No Google port used by the new code. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | New log line `JournalExportBlankFormLog.ExportedToBlankForm` (EventId 5272): codes, ids, counts only. Validation failures log field + rule, never value (`GroupLessonsBlankLoggingTests`). Error bodies via `ApiErrorResponse`; `TooManySheets` returns a translated message without internals. |
| SC-11 Audit | PASS | Same `JournalExported` row as US-028, no per-copy data, no content (spec FR-010); refused requests write none. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts`/Control Plane untouched. |
| SC-13 Outbound | PASS | No new destination; file returned to the requesting browser only. |
| SC-14 no-store | PASS | Host-wide rule; nothing added (AC-012). |

## 6. Authentication and Authorization

Admin ✔, Dean ✔ per `trebovaniya.md` §2 "Просмотр и экспорт журнала"; Teacher /
Student have no account. Actor, role and UI language come from the session
(`ActorId()`, `ActorRole()`, `CultureInfo.CurrentUICulture`), never from the body
(VR-003). Course access is the US-028 report builder's existing check (unknown
course → 404, tested before the sheet limit: `AnUnknownCourse_Is404_BeforeTheSheetLimit`).
No identifier added to the request that could select another installation's or
account's data.

## 7. Credentials, Key and Google Access

No passwords, keys or Google calls in the change. The embedded form is a program
resource, not a credential.

## 8. Sensitive Data Exposure

- Response: the xlsx file, same content scope as US-028 (approved). Form sheets
  with markers are deleted from the output; only filled copies remain.
- Logs: no names, grades, titles, teachers or course names (verified in code and
  by `ASuccessfulBlankExport_LogsTheLayoutAndValues_AndNoPersonalData`).
- Audit: unchanged.
- Form file: shared strings are form labels and the ten `{{…}}` markers only; no
  student or staff data (spec S-06, AC-013). Core properties name the author
  account of the workstation that last saved it (INFO-2).
- Formula injection: text goes through `WorkbookCell.OfText` (quote prefix on
  formula-leading characters, length cap) and `IXLCell.SetValue(string)` — never
  `FormulaA1`. The only formulas in the output are the form's own (`A9+1`,
  `A4+1`, `A5+1` numbering) where not cleared by `XLClearOptions.Contents`.

## 9. Input Validation

- `layout`: exact, case-sensitive `program` | `group-lessons-blank`; absent,
  empty or JSON `null` → program; any other string → 400 `fieldErrors.layout`;
  a non-string → malformed body (existing rule).
- `studentsPerCopy`/`lessonsPerCopy` (only with the form): JSON number, whole,
  1…30 / 1…23 via `TryGetDecimal` (overflowing numbers fail parsing → invalid);
  string/bool/array/object → 400 naming the field. Duplicate members → malformed
  body (existing `seen` set covers the new members).
- Sheet limit: ≤ 100 sheets computed from report counts before rendering;
  `Map` additionally guards its ranges with `ArgumentOutOfRangeException`.
- Unknown members ignored, never echoed. Body size: existing host limits.

## 10. API Security

Endpoint, method, policy, content type and response headers unchanged; request
members match openapi `JournalExportRequest` v1 of this Story; new 400
`TooManySheets` without `fieldErrors` per api-design §2.5; response
`ExportAction.BlankForm` limits only (no data). No undocumented endpoint or field.

## 11. Persistence and Configuration

No schema change, no migration, model snapshot unchanged (DB design
`NOT_APPLICABLE`). No configuration keys added. The form is embedded at build
time from `docs/product/report-templates/group-lessons-blank.xlsx` — the
`.gitignore` exception `!docs/product/report-templates/*.xlsx` is the existing
rule for versioned blank templates.

## 12. Logging, Audit and Telemetry

Covered in §5 (SC-10, SC-11). EventId 5272 shares the EventName
`JournalExported` with 5270 (INFO-3). Hook telemetry not touched.

## 13. Dependencies

No package added; ClosedXML 0.105.1 already referenced by Infrastructure. Only
csproj change is the `EmbeddedResource` item. No vulnerable packages reported.
No new project reference; ClosedXML types stay inside
`ClosedXmlBlankFormRenderer` (AD-4); port `IBlankFormRenderer` in
`Application/Ports`.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01/S-02/S-03 roles, anonymous, antiforgery | `GroupLessonsBlankAuthorizationTests` (4) |
| S-04 read-only, no Google | `GroupLessonsBlankExportTests` (read-only cases per AC matrix) |
| S-08 logs without data / values | `GroupLessonsBlankLoggingTests` (5) |
| S-09 values only, quote prefix | `GroupLessonsBlankExportTests` / `GroupLessonsBlankFormTests` (AC-010 rows of the matrix) |
| VR-001/VR-002 | export tests incl. `MoreThanAHundredSheets_…`, `ExactlyAHundredSheets_…` |
| S-06 form has no personal data | AC-013 test (form vs. `school-journal-full.xlsx`) |

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Huge per-copy numbers / `1e400` / fractions | 400 naming field | `TryReadPerCopy` | PASS |
| `studentsPerCopy: 1, lessonsPerCopy: 1` on a large course to blow up output | 100-sheet cap before rendering | `ExportJournalCommand`, VR-002 tests | PASS |
| Student name or topic `=HYPERLINK(...)` | stored as text with quote prefix | `WorkbookCell.OfText`, `Write` | PASS |
| Name containing `{{grade}}` | not re-interpreted: markers are located once in the untouched form, never in filled copies | `FindMarkers` runs before copying | PASS |
| Export in read-only mode | allowed, audit row only | spec FR-009, tests | PASS |
| Anonymous / no token / forced-password Dean | 401 / 400 / 403 | authorization tests | PASS |
| Values echoed into logs | never | logging tests | PASS |

## 16. Repository Hygiene

Untracked/modified files are Story code, tests, artifacts, workflow state and the
versioned form. No generated `.xlsx` export, database file, `.env` or secret-like
file in the change set. Live credential files not opened; they remain ignored.

## 17. Deviations

None from the security requirements S-01…S-12.

## 18. Findings

**INFO-1 (TEST_COVERAGE)** — `tests/ClassroomAgent.Tests/Infrastructure/Persistence/MeetingCodeLinkRepositoryTests.cs:103`
`AMark_RoundTrips` failed once in the full run (loaded row null), passed 3/3 in
isolation; the file is outside US-043. Likely an intermittent ordering/timing
issue. No correction required for this Story; worth a separate look so the suite
stays reliably green (Definition of Done item 2 is satisfied by the
implementation report's green run).

**INFO-2 (DATA_EXPOSURE)** — `docs/product/report-templates/group-lessons-blank.xlsx`
`docProps/core.xml` carries `creator`/`lastModifiedBy` = the Owner's workstation
account name, and a Google Sheets `xl/metadata` part. If ClosedXML keeps these
when saving (not verified), every school's export names that account as author.
Not student data; low risk. Optional: clear the document properties in the form.

**INFO-3 (LOGGING)** — `src/ClassroomAgent.Web/Security/JournalExportBlankFormLog.cs`
EventId 5272 reuses EventName `JournalExported` (5270) by design (spec FR-011).
Anyone filtering logs by EventId must include both.

No loop-back target: no correction required.

## 19. Positive Controls

Policy + antiforgery on the endpoint; session-derived actor; strict typed
validation of new members with field-named 400; output-size cap before render;
no formula written, quote prefix applied; markers located only in the pristine
form; in-memory rendering; logs and audit free of personal data; no new package,
endpoint, Google call or outbound flow; ClosedXML confined to Infrastructure.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-013 are resolved
or superseded.

## 21. Review Limitations

- No penetration testing; code, configuration and test review plus one full
  test run.
- INFO-2 metadata carry-over into exports not verified at runtime.
- Phone-width layout and a broken embedded form (500) are not tested (recorded in
  the test generation report; no seam).

## 22. Verdict Rationale

Green build, implementation report's green suite, vulnerability scan clean, every
touched SC item PASS, security tests present and passing, no security-sensitive
Open Decision. The one failure in this review's run is outside the Story and not
reproducible in isolation. Hence PASS; three Informational notes go to
`non_blocking_findings`.
