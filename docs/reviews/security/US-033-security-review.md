---
artifact_type: security_review
story: US-033
version: 1
status: APPROVED
created_at: 2026-10-11T05:55:25Z
updated_at: 2026-10-11T05:55:25Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-033-meet-reports.md
    version: null
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/decisions/US-033-open-decisions.md
    version: 3
  - path: docs/designs/api/US-033-api-design.md
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/designs/database/US-033-db-design.md
    version: 1
  - path: docs/designs/database/US-033-entity-model.md
    version: 1
  - path: docs/tests/US-033-test-strategy.md
    version: 1
  - path: docs/tests/US-033-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-033-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-033 Security Review — built-in Meet reports

## 1. Executive Summary

**Result: PASS.** US-033 adds two read-only GET pages (reports 1–3 for a course and period, and report 4 for one
meeting) that show names and emails of students and teachers. Both pages sit behind one new policy,
`ViewMeetReports` (Admin and Dean, matching the §2 row "Просмотр и экспорт статистики Meet-занятий"). They make no
write, no audit row, no Google call and no outbound call. They take three or four query values. Each one is checked
with the journal's rules before any data is read, and a rejected value is never echoed or logged. The pages send
`no-store` through the existing global middleware (US-040).

No Critical, Major or Minor findings. Three Informational notes (§18).

**Limitation, now resolved:** two full-suite runs during this review failed with 33 and 331 tests. Every failure was a
Npgsql connection or read timeout against the Testcontainers PostgreSQL, with no assertion failures. A third run on a
fresh Docker state passed 4549/4549. See INFO-3.

## 2. Reviewed Artifacts

The `inputs` in the front matter, all current, none `SUPERSEDED`. The specification is `APPROVED` (HUMAN_SPEC_APPROVAL
recorded in `history.jsonl`). Open Decisions: OD-001…OD-013, all resolved. None of them is security-sensitive.

## 3. Security-Relevant Scope

| Host / port | Capability | Who |
|---|---|---|
| Installation, public | `GET /workspace/meet-reports?courseId&from&to` | Admin, Dean |
| Installation, public | `GET /workspace/meet-reports/meetings/{meetingId:regex(^\d+$)}?courseId&from&to` | Admin, Dean |

- **Assets:** names and emails of course students and teachers, Meet participation durations, meeting codes. All
  are personal data of possibly minor students.
- **Trust boundaries:** browser → `MeetReportsController` → `GetMeetReportsQuery` / `GetMeetReportMeetingQuery`
  (Application) → `IMeetReportSource` → `MeetReportSource` (Infrastructure, EF Core, PostgreSQL).
- **Not touched:** authentication, passwords, the service-account key, Google ports, the Control Plane channel,
  `Contracts`, schema/migrations, audit, configuration, packages.

Change set: 10 modified and 58 untracked files. All of them are listed in the implementation report. There is no
`.csproj` change.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker Desktop 29.8.0 (12 CPU, 8 GB); Testcontainers PostgreSQL.
- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build --filter FullyQualifiedName~MeetReport`: 217/217 passed.
- `dotnet test ClassroomAgent.sln --no-build --filter FullyQualifiedName~ClassroomAgent.Tests.Web.Security`: 361/361
  passed.
- `dotnet test ClassroomAgent.sln --no-build` (third run): 4549/4549 passed, 0 skipped, 4 min 39 s. The two earlier
  runs failed on environment timeouts only (INFO-3).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`: no vulnerable packages in any of the
  seven projects.
- Delegated: a `cheap-worker` (Sonnet) ran the build, the first full test run and the package scan, and reported the
  facts verbatim. The code reading, the reruns and every judgement were done in this review.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | The policy requires `RoleName(Admin)` or `RoleName(Dean)` (`InstallationSecurityServices.cs`). There is no new role and no roster scoping. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in or password change. The forced-change redirect still applies (`ADeanWithATemporaryPassword_IsSentToTheForcedChange_OnBothPages`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin login is unchanged. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewMeetReports)]` is on the controller class. There is no `[AllowAnonymous]`, no antiforgery exemption and no state change on GET. Tests: `AnAdminAndADean_CanOpenBothPages`, `Anonymous_IsSentToSignIn_OnBothPages`, `BothPaths_AreProtectedEndpoints_WithTheViewMeetReportsPolicy`, plus the existing endpoint enumeration in `Web.Security` (361 green). |
| SC-5 Read-only mode | PASS | Viewing stays open (FR-015). The use cases write nothing and call no Google port. Tests: `InReadOnlyMode_TheMeetingIsStillShown` for each cause, and the page tests' row-count/fingerprint check (`MeetReportsHostExtensions`). |
| SC-6 No DB UI | PASS | No diagnostic or SQL surface was added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | Neither query references a Google port. Reads come only from stored tables. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | `MeetReportsLog` (5280–5283) logs ids, dates, counts and rule names only. Refusals name the parameter, never the value. Errors render the page with a translated message and echo no input. Tests: `MeetReportsLoggingTests` (4 tests, against `SensitiveValues`). |
| SC-11 Audit | NOT_APPLICABLE | Viewing is not an audited action (spec §7). No audit row is written. |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` or Control Plane change. |
| SC-13 Outbound | PASS | No outbound flow. |
| SC-14 no-store | PASS | The global `NoStoreMiddleware` (US-040) covers both pages. |

## 6. Authentication and Authorization

The §2 row allows Admin and Dean. The implementation is one policy on the whole controller and applies to both
actions. Both v1 roles are allowed, so the forbidden case is the unauthenticated request, as spec §7 records (TC-5,
AC-014). Identifiers in the request:

- `courseId` on the reports page must be one of the journal's course list, otherwise `404`.
- On report 4, the meeting is read by R3, which requires the meeting's code to be linked to the given course. An
  unknown course, an unknown meeting and a meeting of another course all answer the same "not found" (`404`). The
  page therefore does not reveal whether a meeting exists in another course.

There is a single installation per database (AD-1) and no roster scoping, so there is no cross-tenant identifier to
abuse.

## 7. Credentials, Key and Google Access

Nothing is touched. The pages read no secret and call no Google API.

## 8. Sensitive Data Exposure

- **Views and DTOs:** the page models are Application DTOs (`MeetReportsPageModel`, `MeetReportMeetingPageModel`,
  `PersonRef`, …). No entity reaches a view (AD-8). Razor encodes every value. No `Html.Raw` was found in the new
  views.
- **Data minimization:** connections without an email, and emails with no course membership anywhere, are counted
  as "other participants" (count and seconds only). Their addresses are never shown, as spec FR-010 and §7 require
  (`MeetReportRules.Classify`).
- **Query string:** carries only the internal course id, the meeting id and two dates. The language-switcher return
  path is rebuilt from the validated values, and `UiLanguageController` accepts it only as a local URL.
- **Logs:** see SC-10. **Exports:** none in this Story (US-044). **Fixtures:** synthetic (`school.example`).

## 9. Input Validation

| Input | Rule | On failure |
|---|---|---|
| `courseId` | exactly one value; ASCII digits; at most the maximum length; `long`, > 0 (`ParseCourseId`) | `400` with message; `404` if not in the course list |
| `from` / `to` | exactly one value; `yyyy-MM-dd`; year 2000–2100; `from` ≤ `to` | `400` with message |
| `meetingId` | route regex `^\d+$`, then `ParseCourseId` (length-bounded `TryParse`) | `404` |
| unknown parameters | not bound, not echoed | ignored |

All validation is server-side and runs before any report data is read. In the controller, `long.Parse(meetingId)`
runs only on the `Shown` path, after the query has already parsed the same value successfully, so it cannot throw.
Tests: `MeetReportsRequestValidationTests` and the page tests.

## 10. API Security

Only the two routes in the OpenAPI contract exist, both GET. Response codes are 200, 400 and 404, as api-design §2.3
specifies. A refusal renders the page itself, never an internal error. No `/api/v1` endpoint was added.

## 11. Persistence and Configuration

No entity, migration or configuration change. `MeetReportSource` makes untracked, parameterised LINQ reads (no
`FromSql`) and has no transaction or write. The email lookup is batched (1000 at a time) and limited to the emails of
the report's own connections (spec §9). There is one database per installation (AD-1).

## 12. Logging, Audit and Telemetry

The four new `LoggerMessage` events carry no personal data, which the logging tests prove. No audit change. Hook
telemetry is unchanged and git-ignored.

## 13. Dependencies

No package and no project reference added. The vulnerability scan is clean for all projects.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| Allowed roles (Admin, Dean) on both pages | `MeetReportsAuthorizationTests.AnAdminAndADean_CanOpenBothPages` |
| Forbidden case (anonymous) on both pages | `Anonymous_IsSentToSignIn_OnBothPages` |
| Policy declared on both paths | `BothPaths_AreProtectedEndpoints_WithTheViewMeetReportsPolicy`, `TheViewMeetReportsPolicy_IsRegistered` |
| Forced password change still applies | `ADeanWithATemporaryPassword_IsSentToTheForcedChange_OnBothPages` |
| Read-only mode: viewing works, nothing written | `InReadOnlyMode_TheMeetingIsStillShown`, `MeetReportsPageTests` (AC-010, AC-011) |
| No personal data / value in logs | `MeetReportsLoggingTests` (4) |
| Malformed input rejected | `MeetReportsRequestValidationTests`, page tests |
| Meeting of another course not revealed | `MeetReportMeetingQueryTests`, `MeetReportSourceTests` (R3) |

## 15. Abuse Case Review

| Scenario | Expected protection | Status |
|---|---|---|
| Anonymous request to either page | Sign-in redirect | PASS (tested) |
| Dean opens report 4 with a meeting id of another course | `404`, same as an unknown id | PASS (R3 + query) |
| Huge or non-numeric `meetingId` / `courseId` | Regex + length-bounded parse → `404` / `400`, no 500 | PASS |
| Script in a course name or Google full name | Razor HTML-encoding | PASS |
| Reading other participants' addresses | Counts only | PASS |
| Browser cache on a shared computer | `no-store` | PASS |
| Opening the page in read-only mode to trigger a write | No write path exists | PASS |

## 16. Repository Hygiene

No `.db`, `.xlsx`, `.env`, `appsettings` or key file in the change set. `classroom_cache.db`,
`google_credentials.json` and `dac-classroom-agent-*.json` are git-ignored (confirmed with `git check-ignore`; not
opened). No credential-like literal appears in the new code.

## 17. Deviations

None security-relevant. See INFO-1 for a comment that does not match its code.

## 18. Findings

**INFO-1 — OTHER — `src/ClassroomAgent.Infrastructure/Persistence/MeetReportSource.cs` (`GetPeopleAsync`).** The
comment says the name is "the lowest id's that has one [a membership]". The code takes the lowest-id participant with
a non-null name, whether or not that participant has a membership. The effect is limited to which Google name labels
an email that appears twice in `classroom_participant`. Both rows are this school's own mirror data, and the email
itself is displayed anyway. No exposure. Recommendation: check against db-design §4 and align the comment or the
code in a later Story. No loop-back.

**INFO-2 — AUTHORIZATION — course existence on report 4.** The reports page checks the course against the journal's
course list. Report 4 checks it against `GetCourseNameAsync` (any stored course). If the journal list ever filters
courses, report 4 could open a meeting of a course the drop-down does not offer. Both roles may see every course
(§2, no roster scoping), so this is not an access issue today. Keep the two consistent if course filtering is ever
introduced.

**INFO-3 — TEST_COVERAGE (environment) — full-suite instability under Testcontainers.** Two full runs in this review
failed: 33 failures, then 331. Every failure was a `Npgsql` connection or read timeout (`The operation has timed
out`, `Timeout during reading attempt`, `Failed to connect to 127.0.0.1:…`), with no assertion failures. A third run
on fresh Docker passed 4549/4549 in 4 min 39 s (the failing runs took 15–22 min). This is environmental and not
caused by US-033. Recorded so that a red full run at HUMAN_PR_APPROVAL is rerun before it is treated as a defect.

## 19. Positive Controls

- One controller-level policy matching the §2 row; deny by default kept.
- Validation of every query value before any read, with `400`/`404` and no echo.
- A meeting of another course cannot be told apart from an unknown one.
- Other participants shown only as aggregates.
- Logs carry no personal data, meeting code or rejected value, and tests prove it.
- No write, no Google call and no outbound call: read-only mode needs no guard.
- `no-store` on both pages.
- Synthetic fixtures; no new dependency; the vulnerability scan is clean.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- This is a code, configuration and test review, not a penetration test.
- Report rendering at large volume (hundreds of meetings) was not load-tested. The spec bounds it to one course and
  one period.

## 22. Verdict Rationale

The build is clean. The full suite passed 4549/4549, the 217 Story tests passed, and the 361 Web security tests
passed. Every SC item the Story touches is PASS with test evidence. There are no Critical, Major or Minor findings
and no open security decision. **PASS → HUMAN_PR_APPROVAL.**
