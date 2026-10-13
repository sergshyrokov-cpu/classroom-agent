---
artifact_type: security_review
story: US-024
version: 1
status: APPROVED
created_at: 2026-10-13T14:18:00Z
updated_at: 2026-10-13T14:18:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-024-database-statistics.md
    version: null
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 3
  - path: docs/designs/api/US-024-api-design.md
    version: 1
  - path: docs/designs/api/US-024-openapi.yaml
    version: null
  - path: docs/designs/database/US-024-db-design.md
    version: 1
  - path: docs/designs/database/US-024-entity-model.md
    version: 1
  - path: docs/tests/US-024-test-strategy.md
    version: 1
  - path: docs/tests/US-024-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-024-implementation-report.md
    version: 1
  - path: trebovaniya.md
    version: 90
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-024 Security Review — Database statistics and last synchronization

## 1. Executive Summary

**Result: PASS.** No Critical, Major or Minor findings; three Informational notes.

US-024 adds one read page (`GET /database`, new policy `ViewDatabaseStatistics`, Admin + Dean) and one write press
(`POST /database/synchronization-requests`, existing policy `StartSynchronization`) that calls the unchanged US-019
`RequestSynchronizationUseCase`. The page shows only aggregate counts and the synchronization state with three times;
no name, email, Meet code or stored failure diagnosis reaches the response or the log. Read-only mode is enforced by
the US-019 use case's guard (Application), not by the view. No schema change, no new package, no new Google scope, no
new outbound flow.

Build 0 warnings / 0 errors; full suite 5497/5497 green (re-run independently for this review); no vulnerable
packages reported.

## 2. Reviewed Artifacts

As listed in `inputs` above. Spec v1 is APPROVED at HUMAN_SPEC_APPROVAL (2026-10-12, OD-011 resolved a); OD-012
resolved (a) by the Owner 2026-10-13. No input is SUPERSEDED.

## 3. Security-Relevant Scope

| Host | Element | Change |
|---|---|---|
| Web (installation, public port) | `GET /database` | new, `ViewDatabaseStatistics` |
| Web | `POST /database/synchronization-requests` | new, `StartSynchronization`, antiforgery |
| Web | Home page | new `#database-section` link (navigation only) |
| Application | `GetDatabaseStatisticsQuery`, `IDatabaseStatisticsSource`, DTOs | new, read only |
| Infrastructure | `DatabaseStatisticsReadSource` | new, one constant SQL statement of counts |
| Web | `InstallationSecurityServices`, `InstallationServices`, `SignInRoutes` | policy + DI + route constants |

Assets: aggregate counts derived from student/teacher data (not personal data by themselves); `SyncState` (its
stored diagnosis is withheld); the synchronization queue and its audit rows. Trust boundaries: browser → installation
host; controller → Application; Application → PostgreSQL. Control Plane and Google are not touched.

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 (Testcontainers available). Commands run (delegated to a `cheap-worker` subagent,
results read verbatim): `dotnet build ClassroomAgent.sln` (0 warnings, 0 errors), `dotnet test ClassroomAgent.sln`
(5497 total, 5497 passed, 0 failed, 0 skipped, 5m51s), `dotnet list ClassroomAgent.sln package --vulnerable
--include-transitive` (no vulnerable packages in any of the 7 projects), `git diff HEAD --stat -- '*.csproj'` (no
change). Code reading, test reading and judgement were done by the reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Policy requires roles Admin, Dean only (`InstallationSecurityServices.cs`); matches §2 row "Управление БД (статистика)" ✔/✔. |
| SC-2 Authentication | PASS | No change; forced-password-change redirect proven for both paths (`DatabaseStatisticsAuthorizationTests`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Sign-in untouched. |
| SC-4 Authorization | PASS | Both actions carry `[Authorize(Policy=…)]`; `TheTwoPaths_AreProtectedEndpoints_WithTheirPolicies`; anonymous → sign-in for GET and POST; `PublicPortAuthorizationTests.OnlySc4EndpointsAllowAnonymous` green; POST without token → 400, nothing enqueued, no audit row; GET changes no state. |
| SC-5 Read-only mode | PASS | Press goes through the US-019 use case guard (`ReadOnlyModeException` → 409 by host handler); `InReadOnlyMode_ThePressIs409…` + `ReadOnlyAndUnconfigured…`; page viewing permitted and proven to write nothing and call no Google port (`DatabaseStatisticsReadOnlyTests`). |
| SC-6 No DB UI | PASS | The "Database" page shows eleven fixed counters from a constant statement; no table browser, no query input, no parameter bound. |
| SC-7 Key | NOT_APPLICABLE | Not touched. |
| SC-8 Google | PASS | No Google port in the query's constructor; tests assert no Google call. |
| SC-9 Channel | NOT_APPLICABLE | Not touched. |
| SC-10 Hygiene | PASS | DTO has no diagnosis/step field (S-08); log `DatabaseStatisticsShown` carries actor id only; `AnIgnoredParameter_IsNeverLogged`; language switcher return path fixed to `/database` (no query echo). |
| SC-11 Audit | PASS | Press audited by the unchanged use case; one success row and one refusal row asserted (`DatabaseSynchronizationRequestTests`). Viewing is not an SC-11 action. |
| SC-12 Owner | NOT_APPLICABLE | No `Contracts` / Control Plane change. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

Admin and Dean open the page (theory over both actors); anonymous is redirected to sign-in; a Dean with a temporary
password is redirected to the forced change for both GET and POST. The role used for guidance and the US-019 call is
read from the session (`IsDean()`), never from the request. v1 has no third installation role, so the forbidden-role
case is anonymous / not-yet-usable session (see INFO-3).

## 7. Credentials, Key and Google Access

Not touched. No Google call on either path.

## 8. Sensitive Data Exposure

Response: counts and four state lines only. `GetDatabaseStatisticsQuery` maps `SyncState` to state + three times;
`LastError`/failed step/processed counter have no DTO field. Teachers/students are `count(DISTINCT participant_id)` —
no identity leaves the database. The Meet-code counter returns a number, not codes. Logs: actor id; the press reuses
`SynchronizationRequestLog` (actor id, trace id, outcome/category). No export.

## 9. Input Validation

GET binds no parameter; extra query parameters are ignored and not logged (AC-019, logging test). POST binds no field;
a submitted `returnUrl` is ignored and the redirect is the constant `/database` (`ASubmittedReturnUrl_IsIgnored_NoOpenRedirect`).

## 10. API Security

Only the two approved endpoints exist; methods match the openapi (GET read, POST write, PRG 302). Error responses are
the existing host handler's (409 read-only, 400 antiforgery page, 500 error page). US-019 endpoint behaviour unchanged
(`TheUs019Endpoint_StillReturnsToTheRolesOwnPage`).

## 11. Persistence and Configuration

No entity, migration or configuration change. `DatabaseStatisticsReadSource` uses `Database.SqlQuery` with a constant
interpolated string (no holes → no injection surface; see INFO-2); counts narrowed with `checked`. No database file or
export staged.

## 12. Logging, Audit and Telemetry

New event 5466 `DatabaseStatisticsShown` — `{AppUserId}` only. Audit coverage via US-019 use case, asserted. Hook
telemetry not affected.

## 13. Dependencies

No package or project reference added; `Application` references only ports. Vulnerability scan clean (nuget.org feed).

## 14. Security Test Coverage

| Requirement / abuse case | Test |
|---|---|
| Allowed roles | `AnAdminAndADean_CanOpenTheDatabasePage` |
| Anonymous refused (GET, POST) | `Anonymous_IsSentToSignIn_ForTheGet` / `…ForThePress` |
| Forced password change | `ADeanWithATemporaryPassword_…` ×2 |
| Antiforgery | `WithoutAnAntiforgeryToken_ThePressIs400_AndNothingHappens` |
| Policies declared, not anonymous | `TheTwoPaths_AreProtectedEndpoints_WithTheirPolicies`, `OnlySc4EndpointsAllowAnonymous` |
| Read-only (Application) | `DatabaseStatisticsReadOnlyTests` ×2, `InReadOnlyMode_ThePressIs409…`, `ReadOnlyAndUnconfigured…` |
| Audit | `AnAcceptedPress_…`, `WithoutAUsableConnection_ThePressIs409…` |
| Open redirect | `ASubmittedReturnUrl_IsIgnored_NoOpenRedirect` |
| No personal data in logs | `DatabaseStatisticsLoggingTests` ×3 |

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Press while read-only | use-case guard → 409, nothing enqueued, refusal audited | PASS |
| Cross-site press | global antiforgery → 400 | PASS |
| `returnUrl` open redirect | constant redirect | PASS |
| Query string echoed / logged | no binding, fixed switcher path, not logged | PASS |
| Reading the failure diagnosis via the page | no DTO field | PASS |
| Anonymous access | fallback + policy | PASS |

## 16. Repository Hygiene

Credential files remain git-ignored; no `.db`, `.xlsx` or `.env` tracked outside `report-templates`; no secret-like
content in the change set.

## 17. Deviations

None security-relevant. D-1…D-3 in the implementation report are test-data / flakiness notes.

## 18. Findings

- **INFO-1** (API_SECURITY) — `Views/Database/Index.cshtml` renders `T[Model.SynchronizationMessageKey]`, a key read
  from TempData. TempData is the Data-Protection-sealed cookie, so the key cannot be chosen by a client; same pattern
  as US-019's landing page. No correction.
- **INFO-2** (PERSISTENCE) — `DatabaseStatisticsReadSource` uses raw SQL (`Database.SqlQuery`) with no interpolation
  holes. Safe as written; a future edit must keep any value as an interpolation hole (parameterized), never string
  concatenation. No correction.
- **INFO-3** (TEST_COVERAGE) — TC-5's forbidden-role test is satisfied by the anonymous and forced-change cases,
  since both v1 installation roles are allowed. When Epic 7 adds Teacher/Student roles, this page needs a
  forbidden-role test. No correction now.

## 19. Positive Controls

Deny-by-default fallback; per-endpoint policies; global antiforgery; Application-layer read-only guard reused, not
reimplemented; session-derived role; constant redirect; DTO minimization (no diagnosis); actor-id-only logging;
bounded constant SQL with checked narrowing; no Google port on a read page.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001…OD-012 resolved.

## 21. Review Limitations

Code, configuration and test review plus a full test run; no penetration testing; the page was not exercised in a
browser against a live installation.

## 22. Verdict Rationale

Green build and tests independently reproduced; every touched SC item PASS with test evidence; no Critical, Major or
Minor findings; no unresolved security decision. **PASS → HUMAN_PR_APPROVAL.**
