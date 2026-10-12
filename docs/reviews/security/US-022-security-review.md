---
artifact_type: security_review
story: US-022
version: 1
status: APPROVED
created_at: 2026-10-12T10:45:15Z
updated_at: 2026-10-12T10:45:15Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-022-teachers-and-their-courses.md
    version: null
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/decisions/US-022-open-decisions.md
    version: 2
  - path: docs/designs/api/US-022-api-design.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: null
  - path: docs/designs/database/US-022-db-design.md
    version: 1
  - path: docs/designs/database/US-022-entity-model.md
    version: 1
  - path: docs/tests/US-022-test-strategy.md
    version: 1
  - path: docs/tests/US-022-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-022-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-022 Security Review — Teachers and their courses

## 1. Executive Summary

**Result: PASS.** US-022 adds one read-only Razor page, `GET /workspace/teachers`,
listing synced teachers (name, email, course count) with a status filter and
paging. It is protected by the existing `ViewCourses` policy (Admin, Dean),
writes nothing, calls no Google port, logs no personal data, and rebuilds every
URL it emits from validated values. No Critical, Major or Minor findings. Two
informational notes. Build and the full suite were re-run independently: 0
warnings, 5411/5411 green. Recommended next stage: `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

All inputs listed in the front matter, at the versions shown; none
`SUPERSEDED`. The Specification is `APPROVED` (HUMAN_SPEC_APPROVAL recorded
2026-10-12T09:00:03Z). The Story file and the OpenAPI YAML carry no `version`
field. Code reviewed: `git diff 37deec1 e341250` (tests `aea4f2f`,
implementation `e341250`), 44 files.

## 3. Security-Relevant Scope

- **Host:** installation (Data Plane) only. No Control Plane, `Contracts` or
  channel change.
- **New endpoint:** `GET /workspace/teachers` (`TeachersController.Index`).
- **Changed page:** `Views/Home/Index.cshtml` — one navigation link.
- **Assets:** teacher names and emails (staff personal data from the Google
  mirror), internal participant ids, the session.
- **Trust boundaries:** browser → installation; controller → `GetTeacherListQuery`
  → `ITeacherListSource` → PostgreSQL.
- **Not touched:** authentication, passwords, key, Google access, audit,
  schema, configuration, dependencies.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available). Delegated to a
`cheap-worker` (read only, facts only): `dotnet build ClassroomAgent.sln`
(0 warnings, 0 errors), `dotnet test ClassroomAgent.sln` (5411 total, 5411
passed, 0 failed, 0 skipped), `--filter FullyQualifiedName~TeacherList` (104/104),
`dotnet list package --vulnerable --include-transitive` (no vulnerable packages
in any of the 7 projects), `.csproj`/`.props` diff (empty). Code reading,
pattern sweeps and judgement done by this reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role added; `ViewCourses` = Admin, Dean (`InstallationSecurityServices.cs:96-100`), matching §2 row "Просмотр курсов и участников". No roster or per-Dean scoping (S-03). |
| SC-2 Authentication | NOT_APPLICABLE | No authentication change; forced-password redirect still applies (test `ADeanWithATemporaryPassword_IsSentToTheForcedChange`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login change. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewCourses)]` on `TeachersController`; no `[AllowAnonymous]`; GET only, changes nothing; anonymous → redirect to sign-in. Endpoint metadata asserted by `ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy`. |
| SC-5 Read-only mode | PASS | Read-only page by design (FR-013); no write, no unit of work, no Google port. `TeacherListReadOnlyTests` covers all three causes in Application and on the page, asserting no write and no Google call. |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | No Google port injected into the query or controller. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | Invalid input answers 400 with the page and translated message keys only; the rejected value is not echoed (model has no raw field); log lines carry ids, status, page, total, parameter name and rule only (`TeacherListLog.cs`). Tested by `TeacherListLoggingTests`. |
| SC-11 Audit | PASS | Viewing is not an SC-11 action; no audit row written (S-12). |
| SC-12 Owner | NOT_APPLICABLE | No Control Plane change. |
| SC-13 Outbound | PASS | No new destination; only PostgreSQL reads. |
| SC-14 no-store | PASS | Global `NoStoreMiddleware` (US-040) covers the new response. |

## 6. Authentication and Authorization

| Caller | Expected | Evidence |
|---|---|---|
| Admin | 200 | `AnAdminAndADean_CanOpenTheTeacherList(Admin)` |
| Dean | 200 | same, `Dean` |
| Dean with temporary password | 302 → change password | `ADeanWithATemporaryPassword_IsSentToTheForcedChange` |
| Anonymous | 302 → sign-in, no data, nothing written | `Anonymous_IsSentToSignIn` |

Both roles are allowed by §2, so the forbidden-role case of TC-5 is the
unauthenticated and the forced-change session. The actor id comes from the
session claim, never the request (VR-004).

## 7. Credentials, Key and Google Access

No password, key or Google path touched. The use case's only dependency is
`ITeacherListSource`.

## 8. Sensitive Data Exposure

- **View model:** `TeacherListPageModel` / `TeacherListRow` are DTOs in
  Application; no entity reaches the view (AD-8, S-10). Fields: participant id,
  name (name rule), email, course count, course-list URL.
- **Razor:** names and emails rendered with `@` (HTML-encoded); no `Html.Raw`
  in the changed files (S-09).
- **URLs:** `TeacherListUrl.Build` emits `status` and `page` only;
  `CourseListUrl.Build(status, participantId, null, 1)` adds the internal id and
  no search text (S-05, S-11). The switcher return path is `Model.ReturnPath`,
  rebuilt from validated values (S-06).
- **Logs:** see SC-10. **Audit, exports, telemetry:** none.

## 9. Input Validation

`status` — one case-insensitive token of the closed `CourseStatusFilter` set;
`page` — ≤ 6 ASCII digits, 1…100 000. Repeated values are refused (exactly one
value). Other parameters are not collected (VR-003). Validation runs in
Application before any read (S-07, `GetTeacherListQuery.ExecuteAsync`). Paging
past the last page clamps to the last. The DB query uses LINQ only (bound
parameters); no raw SQL.

## 10. API Security

Only the approved endpoint exists; method GET; responses 200 / 400 / 302 as in
the OpenAPI. The 400 body is the page itself with message keys, no internals.

## 11. Persistence and Configuration

No entity change, no migration, no configuration change. Reads are
`AsNoTracking`, no transaction, no write. Two queries per request (count, page).

## 12. Logging, Audit and Telemetry

`TeacherListLog.Shown` (actor id, status, page, total) and `Refused` (request
id, parameter, rule). No name, email or value logged; tests assert it. No audit
event required. Hook telemetry untouched.

## 13. Dependencies

No package or project-reference change (props/csproj diff empty). Vulnerability
scan: no vulnerable packages reported against nuget.org.

## 14. Security Test Coverage

| Requirement | Tests |
|---|---|
| S-01, S-02 | `TeacherListAuthorizationTests` (4) |
| S-04 | `TeacherListReadOnlyTests` (2 theories × 3 causes) |
| S-05, S-06 | `TeacherListUrlTests`, `TeacherListPageTests` |
| S-07, S-08 | `TeacherListLoggingTests` (3), `TeacherListQueryTests` |
| S-09, S-11 | `TeacherListPageTests` |

All 104 `TeacherList` tests pass.

## 15. Abuse Case Review

| Scenario | Expected | Status |
|---|---|---|
| Anonymous direct GET | redirect to sign-in, no data | PASS (test) |
| Injected markup in a synced name/email | encoded on output | PASS (Razor encoding) |
| Malformed / repeated / huge `page`, unknown `status` | 400, value not echoed or logged | PASS (tests) |
| Extra query parameters (e.g. search text, email) | ignored, not echoed into links or logs | PASS (`IgnoredParameters_AreNeverLogged`, VR-003) |
| Use in read-only mode | page shown, nothing written, no Google call | PASS (tests) |

## 16. Repository Hygiene

Working tree clean. Live credential files remain git-ignored (checked by
`git check-ignore`, contents not opened). No tracked `.db` or generated `.xlsx`
outside `docs/product/report-templates/`.

## 17. Deviations

None from the approved security requirements. The supporting refactor
(`CourseListQueryRules`, `ParticipantLabel`) moves the course list's existing
rules unchanged; the course-list tests still pass.

## 18. Findings

- **INFO-1 (Informational, DATA_EXPOSURE).** `Views/Teachers/Index.cshtml`
  writes the internal participant id into a `data-participant-id` attribute and
  the course-list link. It is the same internal id the course list already
  uses (OD-005, US-020 OD-009), not personal data. No correction.
- **INFO-2 (Informational, DATA_EXPOSURE).** `Views/Shared/_LanguageSwitcher.cshtml:15`
  falls back to the raw path and query when a page sets no return path. This
  page always sets `ReturnPath` from validated values (S-06), so the fallback
  is not reached here; noted so later pages keep doing the same. (The request
  log's `RequestPath` is already replaced by the route template —
  `RequestRouteEnricher`.)

## 19. Positive Controls

Policy on the controller, verified via endpoint metadata; validation in
Application before any read; DTO-only view model; encoded output; URLs rebuilt
from validated values; logs free of personal data, tested; no write and no
Google call in any read-only cause, tested; global no-store.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

Code, configuration and test review plus an independent build and test run; no
penetration testing. Vulnerability scan limited to the NuGet advisory database.

## 22. Verdict Rationale

The implementation report's green build and tests were reproduced. Every
touched SC item is PASS with evidence; S-01…S-12 hold; no Critical, Major or
Minor finding. Verdict **PASS** → `HUMAN_PR_APPROVAL`.
