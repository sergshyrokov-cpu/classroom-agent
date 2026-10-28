---
artifact_type: security_review
story: US-063
version: 1
status: APPROVED
created_at: 2026-10-28T18:10:25Z
updated_at: 2026-10-28T18:10:25Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
  - path: docs/designs/api/US-063-api-design.md
    version: 1
  - path: docs/designs/database/US-063-db-design.md
    version: 1
  - path: docs/tests/US-063-test-strategy.md
    version: 1
  - path: docs/tests/US-063-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-063-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-063-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: false
runtime_checks: FULL
---

# US-063 Security Review

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 2 Informational.**

US-063 is display-only: two lines («Сформовано», «Дані з Google на») in the journal and report
headers and in a cell note of the template export and the group-lessons blank. No endpoint, route,
policy, request field, entity, migration, Google call, log statement, audit field or package is added
or changed. The values shown are the build instant `b` and `Course.LastCompleteReadAt` — operational
metadata, not personal data. Razor output is HTML-encoded (`@…`, no `Html.Raw`); the xlsx note is a
comment, not a formula cell. Independent build 0/0 and full suite 6767/6767 green.

## 2. Reviewed Artifacts

As listed in `inputs`. API and DB designs are NOT_APPLICABLE records (no contract or schema change).

## 3. Security-Relevant Scope

- Web host, Dean (and Admin, per existing policy) pages `Journal/Index`, `Report/Index` and the existing
  report/blank export endpoints — unchanged authorization; only view content added.
- Changed code: `DataMarkRules.Stamp`, `GetJournalQuery`, `ReportBuilder`, `ReportWorkbookMapper.CourseNote`,
  `GroupLessonsBlankMapper`, DTOs `JournalPageModel`/`ReportHeader` (+ new `CourseDataStamp`),
  `DataMarksTexts`/`DataMarksTextKeys`/`LocalizedReportTexts`, two resx files, two views.
- Assets touched: the course's last-read timestamp (operational). No credentials, key, personal fields.
- Trust boundary: Application → Razor view / xlsx renderer only. No Google, Control Plane, DB write path.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker/Testcontainers available. Commands (delegated to `cheap-worker`, results
verbatim): `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors; `dotnet test ClassroomAgent.sln` —
6767 total, 6767 passed, 0 failed, 0 skipped (14 m 03 s); `dotnet list … package --vulnerable
--include-transitive` — no vulnerable packages in any of 7 projects; `git diff --stat HEAD` on
`*.csproj`/`Directory.*.props` — empty. Code reading and judgement done here, not delegated.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or permission cell changed. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in code touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | No controller/route/attribute in the diff; views render inside existing protected actions; no new anonymous surface; no GET state change. |
| SC-5 Read-only mode | PASS | No write use case added; export allowed in read-only (BR-026). `GeneratedLinesReadOnlyTests` × 3 causes: lines present, teaching fingerprint unchanged, exactly the existing export audit row, `GoogleCallCount()==0`. |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | NOT_APPLICABLE | No configuration or key handling touched. |
| SC-8 Google | PASS | `Stamp` reads the stored `LastCompleteReadAt`; no port call added. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | No logger call in changed files; texts come from resx; no internals in output. |
| SC-11 Audit | PASS | No new audited action; export audit unchanged (asserted single row). |
| SC-12 Owner | NOT_APPLICABLE | `Contracts`/ControlPlane untouched. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

Unchanged. The stamp is computed only for a course already accepted as `known` (in the user's stored
course list) in `GetJournalQuery`, and for the report's resolved `courseRecord` — no new identifier
from the request reaches it.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

Views: two `<p>` elements, values encoded. DTOs: `CourseDataStamp` holds two instants, no entity (AD-8).
Exports: the note contains the localized label and a date-time; the prior US-060 note is kept first
(AC-004). No names, emails or grades added. Logs, audit, telemetry: unchanged.

## 9. Input Validation

No new external input. Date formatting uses the culture's `ShortDatePattern` — culture comes from the
existing user language setting (closed set uk/en).

## 10. API Security

No endpoint, response field (JSON) or status code added. The report page model gains `Stamp` only as a
Razor view-model field.

## 11. Persistence and Configuration

No entity, migration, query shape beyond the already-loaded `JournalCourseRecord`, or configuration
change. Nothing written to the database (AC-007 test).

## 12. Logging, Audit and Telemetry

No change. Hook telemetry untouched.

## 13. Dependencies

No package or project reference change; vulnerability scan clean.

## 14. Security Test Coverage

| Property | Test |
|---|---|
| Read-only: lines shown, no write, no Google | `GeneratedLinesReadOnlyTests` (3 causes) |
| Export note content, printed cells unchanged | `GeneratedLinesExportTests` |
| Header lines, B consistency, language | `GeneratedLinesPageTests` |

Role allow/forbid tests for the touched pages pre-exist (US-025, US-042); this Story adds no endpoint
requiring new ones (TC-5).

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| Export in read-only to force a write | No write | AC-007 test fingerprint | PASS |
| Pass a foreign course id to read its last-read time | Not reachable | `known` filter in `GetJournalQuery`; report course resolution unchanged | PASS |
| Inject markup through note/label | No markup execution | Razor encoding; xlsx note is a comment | PASS |

## 16. Repository Hygiene

`classroom_cache.db`, root `.xlsx` exports, `google_credentials.json`, `dac-classroom-agent-*.json`,
`.claude/settings.local.json` are git-ignored (status `!!`); not opened. No secret-like file in the
change set.

## 17. Deviations

None security-relevant. The implementation report's `security_sensitive: false` is confirmed.

## 18. Findings

- **INFO-1** (DATA_EXPOSURE) — the course-name note is written through the same cell-note mechanism as
  US-060, whose comment author is not asserted by a test (US-060 security INFO-1). This Story adds no
  author handling; the inherited gap stays as recorded there. No correction required here.
- **INFO-2** (OTHER) — the full suite took 14 m 03 s at ~4 GB free RAM, above the 10-minute foreground
  timeout; the run completed in the background and was green. Operational only.

## 19. Positive Controls

Razor HTML encoding of the new lines; read-only behaviour proven over three causes with zero Google
calls and an unchanged teaching fingerprint; no new endpoint or package; existing export audit intact.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 and OD-002 are resolved (a) and not
security-sensitive.

## 21. Review Limitations

Code, configuration and test review plus one full test run; no penetration testing. Generated xlsx
files were inspected only through the tests' assertions.

## 22. Verdict Rationale

No Critical, Major or Minor finding; every touched SC item PASS; build and full suite green
independently. The four OPEN Story findings (SPEC_REVIEW m-1, m-2; IMPLEMENTATION INFO-1;
IMPLEMENTATION_VERIFICATION m-1) are wording, display and maintainability items outside security
responsibility; none is closed here.
