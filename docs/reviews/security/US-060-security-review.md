---
artifact_type: security_review
story: US-060
version: 1
status: APPROVED
created_at: 2026-10-27T14:57:54Z
updated_at: 2026-10-27T14:57:54Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: docs/designs/api/US-060-api-design.md
    version: 1
  - path: docs/designs/database/US-060-db-design.md
    version: 1
  - path: docs/tests/US-060-test-strategy.md
    version: 1
  - path: docs/tests/US-060-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-060-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-060-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-060 Security Review

## 1. Executive Summary

**PASS.** US-060 is a display-only Story: it reads marks US-058/US-059 already store (course absence mark and
date, last complete read, skip record, item `NotInClassroomSince`, roster flag, first/last seen) and shows them on
the course list, course card, journal, report page and both export layouts. It adds no endpoint, no write path, no
Google call, no schema change, no package, no log line and no audit event. Independent build 0/0 and full suite
6444/6444 green; no vulnerable packages. No Critical, Major or Minor finding; two Informational notes.

## 2. Reviewed Artifacts

As in front matter. Specification v1 APPROVED at HUMAN_SPEC_APPROVAL; API_DESIGN and DB_DESIGN recorded
NOT_APPLICABLE (their records v1); nothing SUPERSEDED.

## 3. Security-Relevant Scope

- Host: Data Plane (`ClassroomAgent.Web`) only. Existing pages `Courses/Index`, `Courses/Card`, `Journal/Index`,
  `Report/Index`, and the existing POST exports (program layout, group-lessons blank). Control Plane untouched.
- Assets: students' personal data (names, last-seen dates, raw points) — already shown on these pages before this
  Story; the technical-account visibility fact ("not visible to the technical account").
- Trust boundaries crossed: Razor view ← Application DTO; Application ← persistence read ports; export renderer
  (ClosedXML) ← Application `WorkbookCell`.
- Changed components: 25 Application files (DTOs, records, query/use-case rules, `DataMarkRules`), 6 Infrastructure
  files (4 read sources, 2 renderers + `ExportMarks`), 7 Web files (4 views, CSS, `DataMarksTexts`/`TextKeys`,
  `LocalizedReportTexts`), 2 resx files.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available); free RAM 5.9 GB at start.
- Delegated to `cheap-worker` (facts only): `dotnet build ClassroomAgent.sln` → 0 warnings, 0 errors;
  `dotnet test ClassroomAgent.sln` → 6444 total, 6444 passed, 0 failed, 0 skipped (8 m 43 s), no timeouts;
  `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` → no vulnerable packages in any of the
  7 projects (nuget.org source).
- Read by this reviewer: the full `git diff` of `src/`, all new `src/` files, the read-only test, the spec §7.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role, policy or seed changed; Admin and Dean see the same (spec §2, BR-002). |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie code touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Not touched. |
| SC-4 Authorization | PASS | No controller, route, policy or `[AllowAnonymous]` changed; the marks render inside pages that already declare their policies; export stays POST with antiforgery. |
| SC-5 Read-only mode | PASS | No write use case added; `DataMarkRules` is pure; `DataMarksReadOnlyTests` (3 causes) proves list and export work, teaching-table fingerprint unchanged, Google call count 0. |
| SC-6 No DB UI | NOT_APPLICABLE | Not touched. |
| SC-7 Key | NOT_APPLICABLE | No key, configuration or Data Protection change. |
| SC-8 Google | PASS | No Google port referenced in any changed file; marks come from stored columns only. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | No logger call in any changed/new `src` file; Razor output is encoded (`@` of strings, no `Html.Raw`); localized strings take only dates/numbers as arguments; export text goes through `WorkbookCell.OfText` (length cut, formula quote prefix). |
| SC-11 Audit | PASS | No new audited action (spec §7); the export's existing event is unchanged. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` and Control Plane untouched. |
| SC-13 Outbound | PASS | No new HTTP client, no new destination. |

## 6. Authentication and Authorization

No endpoint or page added. The four views changed only render extra DTO fields. The course id selecting the
journal/report header line is the already-validated `known` id resolved against stored courses
(`GetJournalQuery`), so no foreign identifier reaches a new query. The skip-record lookup is an EF Core
`Any(...)` correlated subquery on `CourseGoogleId == c.GoogleId` — parameter-free, no raw SQL.

## 7. Credentials, Key and Google Access

No password, key or scope handling touched. No Google port is used by the new code (static check + test with
`GoogleCallCount() == 0`).

## 8. Sensitive Data Exposure

- Views: new values are a translated phrase with a date, a fixed label, or a CSS class; no new personal data
  category appears — the last-seen date and raw points belong to students already listed on the same page, to
  the same roles (spec §7).
- DTOs: new members are enums from `Domain.Enums`, dates, booleans and decimals — no entity (AD-8).
- Exports: notes are Excel comments with the date or the points/maximum; the comment author is set to the empty
  string so the rendering machine's account name is not written (`ExportMarks.Apply`). See INFO-1.
- The "not visible to the technical account" phrase discloses to Admin/Dean only what v102 / BR-049 require them
  to see; it names no account.
- Logs, audit rows, exceptions, telemetry: unchanged.

## 9. Input Validation

No new external input. Query and form parameters of the affected pages are those validated before this Story.
Data from Google reaches the marks only as already-stored, already-validated columns.

## 10. API Security

No REST endpoint, request or response shape changed (API_DESIGN NOT_APPLICABLE confirmed against the diff).

## 11. Persistence and Configuration

No migration, entity or configuration change; four read sources project three more existing columns plus one
correlated `Any`. Queries are `AsNoTracking`. No `EnsureCreated`.

## 12. Logging, Audit and Telemetry

No logging added; no audit change; hook telemetry not touched by this Story.

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability scan clean (section 4).

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Read-only mode leaves view/export working, writes nothing, no Google call (BR-026, SC-5, SC-8) | `DataMarksReadOnlyTests` |
| Marks rendered per role on existing protected pages | `CourseListMarksPageTests`, `JournalMarksPageTests`, `ReportMarksPageTests` (existing allowed/forbidden-role tests of these endpoints unchanged and green) |
| Export notes/fill | `ReportMarksExportTests`, `BlankFormMarksExportTests` |

TC-5 allowed/forbidden pairs: no new endpoint, so none required; existing pairs pass in the 6444 run.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Export a marked course in read-only mode | Works; no write to teaching data; no Google call | `DataMarksReadOnlyTests` | PASS |
| Course name/title with markup or formula reaches a mark line | Encoded on screen; `OfText` quote prefix in export | Razor encoding; `WorkbookCell.OfText`; notes are comments (never evaluated) | PASS |
| Journal for an unknown course id | No header line; existing handling | `known` resolution in `GetJournalQuery` | PASS |
| Inspect exported file for the rendering host's account | No author on comments | `ExportMarks.Apply` sets `Author = string.Empty` | PASS (INFO-1) |

## 16. Repository Hygiene

No `.db`, `.xlsx`, `.env` or secret-like file untracked or tracked outside `docs/product/report-templates/`;
`google_credentials.json` is git-ignored (checked with `git check-ignore`, not opened).

## 17. Deviations

None security-relevant. Implementation deviation D-1 (test fixture teacher labels) is a test-data fix only.

## 18. Findings

### INFO-1 — The empty comment author is not pinned by a test
- Category: TEST_COVERAGE (SC-10). File: `src/ClassroomAgent.Infrastructure/Export/ExportMarks.cs`.
- Observed: the comment author is cleared in code; no US-060 test asserts it, so a later refactor could reintroduce
  the server's Windows account name into exported files.
- Risk: low — an operator account name, not school data.
- Correction: optional; a one-line assertion in an export test. Loop-back: none.

### INFO-2 — The technical-account visibility phrase is shown to Dean
- Category: DATA_EXPOSURE. Spec-approved (v102, BR-049); recorded only so a future role (Epic 7) does not inherit
  it without a decision. Loop-back: none.

## 19. Positive Controls

Pure mark computation in Application with no port calls; encoded Razor output with no `Html.Raw`; DTO-only view
models; export text sanitised by `WorkbookCell.OfText`; notes as non-evaluated comments without author; read-only
behaviour proven with three causes; no new dependency.

## 20. Open Decisions

No blocking security Open Decisions were identified. US-060 Open Decisions OD-001…OD-003 are resolved (a).

## 21. Review Limitations

Code, configuration and test review plus an independent full test run; no penetration test. The on-screen
highlight colour was not inspected visually (functional, not security — IMPLEMENTATION_VERIFICATION INFO-2).

## 22. Verdict Rationale

Green build and suite, no Critical/Major/Minor finding, every touched SC item PASS, read-only behaviour tested,
no Open Decision blocking. The OPEN findings of US-060 from SPEC_REVIEW and IMPLEMENTATION_VERIFICATION are
functional or documentation matters outside this stage and stay OPEN for the human at HUMAN_PR_APPROVAL.
