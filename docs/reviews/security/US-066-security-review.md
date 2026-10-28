---
artifact_type: security_review
story: US-066
version: 1
status: APPROVED
created_at: 2026-10-28T19:57:09Z
updated_at: 2026-10-28T19:57:09Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: 1
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/designs/api/US-066-openapi.yaml
    version: 1
  - path: docs/designs/database/US-066-db-design.md
    version: 1
  - path: docs/tests/US-066-test-strategy.md
    version: 1
  - path: docs/tests/US-066-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-066-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-066-implementation-verification.md
    version: 1
supersedes: null
verdict: PASS
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-066 Security Review — namesakes shown with their email

## 1. Executive Summary

**PASS — 0 Critical, 0 Major, 0 Minor, 2 Informational.**

The Story adds one optional, nullable field (`NamesakeEmail`) to two existing student-row DTOs
(`JournalRow`, `GradingRow`), computed in Application (`NamesakeRule`) and rendered by the journal
page, the report page and the report/blank workbook mapper (`NamesakeLabel.Compose`). No endpoint,
policy, persistence, configuration, dependency, log line, audit event or outbound flow changed.

The only security-relevant effect is that a student's stored email (personal data, possibly of a
minor) now appears next to the name on rows whose name coincides with another row's. The email is
already visible to the same roles (Admin, Dean) on the course card and in the report's email variant
(spec §7); it reaches no new role and no new destination. Razor encodes it on screen; the workbook
cell keeps the existing formula-lead guard.

Independent build: 0 warnings, 0 errors. Independent full suite: 6796/6798, the 2 failures were
Npgsql read timeouts during test-host migration in unrelated classes, both green on a single-class
rerun (INFO-1).

## 2. Reviewed Artifacts

As listed in `inputs`. DB design: NOT_APPLICABLE record (v1). Implementation report v1 records build
0/0, full suite 6798/6798, format clean. Spec v1 APPROVED at HUMAN_SPEC_APPROVAL
(`docs/workflow/history.jsonl`). No input is SUPERSEDED.

## 3. Security-Relevant Scope

- Host: installation (`ClassroomAgent.Web`) only. Control Plane untouched.
- Changed operations (all existing, all GET page renders or the existing POST export):
  journal page (`JournalController`, policy `ViewJournal`), report page and report/blank export
  (`ReportController`, policy `UseReportTemplates`).
- Asset: student email (personal data). Trust boundaries: browser ↔ installation (output encoding),
  installation ↔ downloaded `.xlsx` (cell content).
- Changed files: `GradingRow.cs`, `JournalRow.cs`, `GetJournalQuery.cs`, `ReportBuilder.cs`,
  `ReportWorkbookMapper.cs`, `Journal/Index.cshtml`, `Report/Index.cshtml`; new `NamesakeLabel.cs`,
  `NamesakeRule.cs`; test files under `tests/ClassroomAgent.Tests/`.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers available).
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6798 total, 6796 passed, 2 failed, 0 skipped (18 m 22 s);
  reruns `InstallationOAuthSettingsTests` 44/44, `MeetExportTranslationTests` 98/98.
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable packages in any project.
- `git diff --stat HEAD` over `*.csproj` / `Directory.*.props` — empty.
- Free RAM at start 5.5 GB.
- Delegated (cheap-worker, facts only): build, full suite, single-class reruns, vulnerable-package
  scan, csproj diff. The code reading and all judgements were done by this reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed touched. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in code touched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | No new endpoint; `JournalController` keeps `[Authorize(Policy = ViewJournal)]`, `ReportController` keeps `[Authorize(Policy = UseReportTemplates)]`; existing allowed/forbidden-role tests of those endpoints still in the green suite. |
| SC-5 Read-only mode | PASS | Read-only features; no write use case added; openapi `x-writes: nothing`, `x-calls-google: false`. |
| SC-6 No DB UI | NOT_APPLICABLE | — |
| SC-7 Key | NOT_APPLICABLE | No key or configuration touched. |
| SC-8 Google | PASS | No Google port call added; the email is the stored mirror value. |
| SC-9 Channel | NOT_APPLICABLE | — |
| SC-10 Hygiene | PASS | No logger call added (diff sweep); no error path added; email not in any log. |
| SC-11 Audit | PASS | No audited action added; the export's existing audit event is unchanged and carries no email. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts`/Control Plane untouched. |
| SC-13 Outbound | PASS | Data stays in the installation's page and the file the user downloads. |
| SC-14 no-store | PASS | Page caching unchanged (spec §7). |

## 6. Authentication and Authorization

Unchanged. The email is placed on the student row only (`GradingRow` built from `ClassroomRole.Student`
members; journal members from `GetStudentMembersAsync`), so a teacher row never carries it (api-design
D-2). Namesake detection runs over the rows already authorized for the viewer; it reaches no other
course or installation.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

- Screens: `NamesakeLabel.Compose(...)` is emitted through `@( … )` — Razor HTML-encodes it (VR-002).
  No `Html.Raw` added.
- DTOs: `NamesakeEmail` is null except on a namesake student row with a non-empty email
  (`NamesakeRule.Email`); exposure is minimised to what the Story approves (BR-086).
- Exports: `ReportWorkbookMapper.StudentCell` composes «name (email)» into `WorkbookCell.OfText`,
  which keeps the formula-lead quote prefix and length cap; the composed text starts with the name,
  so the guard applies as before. The group-lessons blank reuses the same `StudentCell`.
- Logs, audit, telemetry: no change.
- Fixtures: synthetic (`CourseTestData.Email(...)`, `other.example`) — TC-4.

## 9. Input Validation

No new external input. The email is Google data stored as returned (mirror, v83); spec VR-001 takes
it unchecked and treats null/empty as "no email". Output encoding (screen) and formula guard (xlsx)
are the relevant controls and are in place.

## 10. API Security

No new endpoint, method or request field. One additive, nullable response/view-model field, approved
in `US-066-openapi.yaml`. Error behaviour unchanged.

## 11. Persistence and Configuration

No entity, migration or configuration change (DB_DESIGN NOT_APPLICABLE). No generated database file
or `.xlsx` in the working tree.

## 12. Logging, Audit and Telemetry

No logger or audit change in the diff. Hook telemetry not affected.

## 13. Dependencies

No package or project-reference change. Vulnerability scan clean.

## 14. Security Test Coverage

- Authorization: existing allowed/forbidden-role tests for the journal, report and export endpoints
  (unchanged policies) pass in the full suite.
- Exposure limits: `TwoTeachersWithTheSameName_AreShownWithoutAnEmail` (teacher never carries an
  email), no-namesake tests (no `@` anywhere), FR-002.5 email-labelled rows.
- Output encoding of a markup-bearing email is not tested (INFO-2).

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Email containing HTML/script in Google | Razor encoding | `@( … )` in both views; no `Html.Raw` | PASS (untested, INFO-2) |
| Email/name starting with `=`/`+`/`-`/`@` in export | Formula-lead quote prefix | `WorkbookCell.OfText` | PASS |
| Viewer without role opens journal/report | 403 by policy | unchanged policies + existing tests | PASS |
| Teacher's email leaking via namesake | Field only on student rows | D-2, teacher namesake test | PASS |

## 16. Repository Hygiene

`git status`: only Story code, tests and workflow artifacts; no secret-like file, no `.db`, no `.xlsx`.
Live credential files not opened.

## 17. Deviations

None security-relevant. `NamesakeLabel` living in `Models.Dtos` (verification INFO-2) has no security
effect.

## 18. Findings

- **INFO-1** (TEST_COVERAGE / environment) — full-suite run: `InstallationOAuthSettingsTests.InvalidPublicBaseAddress_HostDoesNotStart("")`
  and `MeetExportTranslationTests.EveryMeetText_ResolvesInBothLanguages(MeetDelayLoadedUpTo)` failed
  with `Npgsql … Timeout during reading attempt` inside `InstallationTestHost.MigrateAsync`, each
  after ~8 min; both classes green on a single-class rerun (44/44, 98/98). Unrelated to the Story;
  the known Docker/WSL stall. No correction required.
- **INFO-2** (TEST_COVERAGE, VR-002) — no test feeds an email with markup characters and asserts the
  encoded output on the page. Razor's default encoding makes this hold; a test would pin it against a
  future `Html.Raw`. No correction required for this Story.

## 19. Positive Controls

Unchanged authorization policies on both controllers; Razor default encoding of the composed label;
formula-lead guard on workbook text; email restricted to namesake student rows; no logging or audit
of the email; no new dependency; clean vulnerability scan; synthetic fixtures.

## 20. Open Decisions

No blocking security Open Decisions were identified (US-066 OD-001…OD-002 resolved a)).

## 21. Review Limitations

Code, configuration and test review plus an independent build and test run; no penetration testing.
The two environmental test timeouts were judged by rerun, not by root cause.

## 22. Verdict Rationale

No Critical, Major or Minor finding; every touched SC item PASS; build clean; the suite is green with
two environmental timeouts that pass on rerun; the new exposure of student emails is the behaviour
approved by `trebovaniya.md` v104 (BR-086) and the Specification, to the same roles that already see
those emails. PASS → HUMAN_PR_APPROVAL.

### Findings from earlier stages

The OPEN findings US-066/IMPLEMENTATION/INFO-1 and US-066/IMPLEMENTATION_VERIFICATION/m-1, INFO-1…3
are functional or environmental, not security; they are left OPEN for the Owner at HUMAN_PR_APPROVAL.
