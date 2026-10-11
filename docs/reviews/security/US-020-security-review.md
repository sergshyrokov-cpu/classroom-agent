---
artifact_type: security_review
story: US-020
version: 1
status: APPROVED
created_at: 2026-10-11T19:10:00Z
updated_at: 2026-10-11T19:10:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-020-course-list.md
    version: null
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 2
  - path: docs/designs/api/US-020-api-design.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
  - path: docs/designs/database/US-020-entity-model.md
    version: 1
  - path: docs/tests/US-020-test-strategy.md
    version: 1
  - path: docs/tests/US-020-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-020-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 88
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-020 Security Review — course list with filters and search

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor findings; three Informational notes.

US-020 adds one read-only page, `GET /workspace/courses`, for Admin and Dean. It shows synced Google data
(course names, sections, states, teacher names or emails, student counts). Principal controls, each verified in code
and by a passing test: a dedicated `ViewCourses` policy (Admin, Dean) on the controller under the global fallback
policy; server-side validation of all four query parameters in Application, with 400/404 refusals that never echo the
rejected value; a parameterised, literal search (no `LIKE` pattern, no raw SQL); Razor encoding of all Google data;
log lines with identifiers and counts only — never the search text, a name or an email; no write, no audit, no
Google port, and correct behaviour in read-only mode.

Independent re-run during this review: build 0 warnings / 0 errors; full suite 5 093 / 5 093 passed, 0 skipped;
no vulnerable packages; no package or project reference changed.

## 2. Reviewed Artifacts

See front matter `inputs`. The Story file carries no version field. No input is `SUPERSEDED`;
`HUMAN_SPEC_APPROVAL` is recorded in `history.jsonl` (2026-10-11T10:58:55Z).

## 3. Security-Relevant Scope

| Area | Change |
|---|---|
| Web | `CoursesController` (new, GET only), `Views/Courses/Index.cshtml` (new), `CourseListLog` (new), `CoursesTextKeys` (new), `ViewCourses` policy registration, DI registration, a link on the home page |
| Application | `GetCourseListQuery` (validation + reads), `CourseListUrl.Build` |
| Infrastructure | `CourseListSource` (EF Core read port), ICU collation `course_name_uk`, partial index `ix_course_membership_current_teacher`, migration `AddCourseListSupport` |
| Localization | 34 `Courses.*` keys in uk/en |

Assets: personal data of teachers (names, emails) and student counts per course; course names. Trust boundaries:
browser → installation host (public HTTPS); controller → Application use case; Application → `ICourseListSource`
→ PostgreSQL. No Google, Control Plane, key or credential boundary is touched.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker available (Testcontainers ran). Commands (delegated to a `cheap-worker`, facts only):
`dotnet build ClassroomAgent.sln` → 0 warnings, 0 errors; `dotnet test ClassroomAgent.sln` → 5 093 total, 5 093
passed, 0 failed, 0 skipped (5 m 48 s); `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` →
no vulnerable packages in all 7 projects; `git diff --stat HEAD` on `*.csproj` / `Directory.*.props` → empty.
Code reading, pattern sweeps and judgement were done by this reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policy requires `Admin` or `Dean` only (`InstallationSecurityServices.cs`); §2 row «Просмотр курсов и участников» = Admin, Dean. No new role. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in change. Forced password change still applies (`ADeanWithATemporaryPassword_IsSentToTheForcedChange`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | No login path touched. |
| SC-4 Authorization | PASS | `[Authorize(Policy = ViewCourses)]` on the controller; no `[AllowAnonymous]`, no `IgnoreAntiforgeryToken` (sweep); GET only, no state change; endpoint metadata test `ThePath_IsAProtectedEndpoint_WithTheViewCoursesPolicy`; existing `InstallationEndpointTests` / `PublicPortAuthorizationTests` enumerate endpoints and stayed green. |
| SC-5 Read-only mode | PASS | Viewing is permitted (FR-015); nothing written. `CourseListReadOnlyTests` (per read-only cause): data shown, nothing written, no Google call. |
| SC-6 No DB UI | PASS | No diagnostic or SQL surface added. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | No Google port injected into `GetCourseListQuery` or the controller; read-only tests assert no call. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | `CourseListLog`: account id, status, teacher participant id, `searchGiven` bool, page, count; refusals log parameter name and rule only. `Microsoft.*` logs at Warning (`InstallationLogging.cs`), so request URLs with `q` are not logged. Tests: `TheSearchText_IsNeverLogged`, `AMalformedParameter_LogsAWarningNamingIt_ButNotItsValue`, `AnUnknownTeacher_…_ButNotItsValue`, sensitive-values sweep. |
| SC-11 Audit | NOT_APPLICABLE | Viewing is not an SC-11 action; no audit row written (spec FR-015). |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` / Control Plane change. |
| SC-13 Outbound | PASS | No outbound call; the page loads no third-party resource (layout sweep), so no `Referer` carrying `q` leaves the host. |
| SC-14 no-store | PASS | Global `NoStoreMiddleware` (US-040) covers the new page. |

## 6. Authentication and Authorization

| Actor | Expected | Test |
|---|---|---|
| Admin | 200 with data | `AnAdminAndADean_CanOpenTheCourseList(Admin)` |
| Dean | 200 with data | `AnAdminAndADean_CanOpenTheCourseList(Dean)` |
| Anonymous | 302 to sign-in, no data, nothing written | `Anonymous_IsSentToSignIn` |
| Dean with temporary password | 302 to password change, no data | `ADeanWithATemporaryPassword_IsSentToTheForcedChange` |

Both v1 installation roles are allowed, so the TC-5 forbidden case is the unauthenticated request (same pattern as
US-033). `teacherId` filters among the installation's own synced teachers; there is one school per database (AD-1),
so no cross-tenant identifier exists. The actor id comes from the session claim, never the request (VR-006).

## 7. Credentials, Key and Google Access

No password, key or Google access is involved. No Google port is reachable from the query.

## 8. Sensitive Data Exposure

- View model: `CourseListPageModel` / `CourseListRow` / `TeacherOption` DTOs from Application; no entity reaches the
  controller or view (AD-8). A Domain enum (`CourseState`) in a DTO follows existing precedent (`JournalColumn`).
- Emails appear only where approved: as the name fallback (I-2) and to tell same-named teachers apart in the choice
  (I-3). Visible to Admin and Dean only. See INFO-2.
- Razor encodes every value; no `Html.Raw` (sweep); `GoogleData_IsHtmlEncoded` covers markup in Google data.
- Logs: §5 SC-10. Audit, exports, telemetry: nothing new.

## 9. Input Validation

All in `GetCourseListQuery` (Application), every occurrence of each parameter considered:

- `status`: exactly one value, ordinal-ignore-case match to the enum tokens; else 400.
- `teacherId`: one value, ASCII digits only, bounded length, `long`, > 0 (`JournalQueryRules.ParseCourseId`); else
  400; well-formed but not a current teacher → 404.
- `q`: repeated → 400; trimmed; > 100 chars → 400; any control character → 400; whitespace only = no search.
- `page`: one value, ≤ 6 ASCII digits, 1…100 000; else 400; past the end → last page.
- Unknown parameters are not read and never echoed (VR-005). A rejected value is not put back into the form or the
  switcher return path (`AfterARefusal_TheSwitcherDropsTheRejectedValue`).

Covered by `CourseListRequestValidationTests` and the page tests. Refusals use the page itself (api-design §2.4);
no route is under `/api/v1`, so the API-6 body does not apply.

## 10. API Security

Only the approved endpoint exists (`GET /workspace/courses`). No JSON endpoint (I-6). Response contains no internal
identifiers beyond course and participant ids already used by other pages, no stack trace or SQL. 400 and 404 render
translated messages only.

## 11. Persistence and Configuration

- Search: EF Core LINQ `Contains` over a bound parameter under the ICU collation — no `FromSql`, no string
  concatenation; `TheSearch_IsLiteral` proves `%`, `_`, `\` and a SQL-injection string are matched literally.
- All reads `AsNoTracking`, no `SaveChanges`.
- Migration `AddCourseListSupport`: one collation, one partial index; no table, column or data change; no
  `EnsureCreated`. Single installation database (AD-1).
- No configuration file or connection string changed.

## 12. Logging, Audit and Telemetry

Covered in §5 SC-10 and SC-11. Hook telemetry untouched.

## 13. Dependencies

No package or project reference added (diff empty). `--vulnerable --include-transitive`: none found (sources:
nuget.org, local SDK packages).

## 14. Security Test Coverage

| Requirement / abuse case | Test(s) |
|---|---|
| Role access, anonymous, forced change | `CourseListAuthorizationTests` (5) |
| Read-only mode, no Google, no write | `CourseListReadOnlyTests` (2 theories) |
| Literal search / SQL injection | `CourseListSourceTests.TheSearch_IsLiteral` |
| No search text / rejected values in logs | `CourseListLoggingTests` |
| HTML encoding of Google data | `CourseListPageTests.GoogleData_IsHtmlEncoded` |
| Malformed / oversized / repeated input | `CourseListRequestValidationTests` |
| URL encoding of `q` in links | `CourseListUrlTests.TheSearchText_IsPercentEncoded` |

The three test edits by the implementor (report §7) were inspected: a fixture word change, the migration count
16 → 17, and the landing-page "no teaching data" check now ignoring only the literal `href="/workspace/courses"`.
None weakens a security assertion.

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Anonymous request to the page | fallback + policy → sign-in | PASS |
| SQL / `LIKE` pattern in `q` | bound parameter, literal match | PASS |
| `<script>` in a synced course name | Razor encoding | PASS |
| Oversized or control-character `q`, huge `page`, signed or non-ASCII `teacherId` | VR-001…VR-004 → 400 | PASS |
| Probing teacher ids | 404 only reveals whether an id is a current teacher of this school — the same set the choice already lists | PASS |
| Searching personal data into server logs | search text never logged; framework request log below threshold | PASS |
| Using the page in read-only mode | allowed by spec; writes nothing | PASS |

## 16. Repository Hygiene

Change set: 16 modified, 7 untracked paths, all in Story scope (code, tests, migration, translations, workflow
files, implementation report). No secret-like file, database file or `.xlsx` export among them. Live credential
files were not opened.

## 17. Deviations

None found between the spec, API/DB designs and the code on security-relevant behaviour.

## 18. Findings

- **INFO-1** (DATA_EXPOSURE, SC-10). The free search text `q` travels in the query string (approved: spec, API
  design; flagged at API_DESIGN per US-039 INFO-1). It therefore lands in browser history and in the page markup
  (form value, paging links, switcher return path) of the same user. It is not written to server logs (verified)
  and leaves the host in no `Referer` (no third-party resources). The searched data is course names, not student
  records. No correction.
- **INFO-2** (DATA_EXPOSURE). Teacher emails are shown when a teacher has no profile name and to disambiguate
  same-named teachers in the choice (spec I-2, I-3, approved at the gate). Visible to Admin and Dean only. No
  correction.
- **INFO-3** (CONFIGURATION). The migration requires PostgreSQL built with ICU (db-design §2.3). On a server without
  ICU the migration fails at startup — an availability concern, not a security one; it belongs in the deployment
  runbook. No correction.

## 19. Positive Controls

Dedicated policy under a deny-by-default fallback; endpoint-metadata test; validation in Application before any
course read; refusal values never echoed or logged; parameterised literal search; untracked read-only port; Razor
encoding; global no-store; no new dependency.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-012 are resolved.

## 21. Review Limitations

Code, configuration and test review plus a full local build and test run; no penetration test. ICU behaviour was
verified only on the Testcontainers image (postgres:17-alpine), not on a production server.

## 22. Verdict Rationale

Green build and suite re-verified independently; every touched SC item is PASS; required TC-5 tests exist and pass;
no Critical, Major or Minor finding; no open security decision. Verdict PASS → `HUMAN_PR_APPROVAL`.
