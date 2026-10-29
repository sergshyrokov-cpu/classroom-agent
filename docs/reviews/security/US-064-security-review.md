---
artifact_type: security_review
story: US-064
version: 1
status: APPROVED
created_at: 2026-10-29T07:56:08Z
updated_at: 2026-10-29T07:56:08Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-064-disabled-dean-session-refused-on-next-request.md
    version: null
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
  - path: docs/designs/api/US-064-api-design.md
    version: 1
  - path: docs/designs/database/US-064-db-design.md
    version: 1
  - path: docs/tests/US-064-test-strategy.md
    version: 1
  - path: docs/tests/US-064-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-064-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-064-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 2
security_sensitive: true
runtime_checks: FULL
---

# US-064 Security Review — A disabled Dean's open session is refused on the next request

## 1. Executive Summary

**PASS.** The Story is test-only: no file under `src/` changed (`git status` shows one new test
class plus workflow artifacts). The security property the Story proves — disabling a Dean ends their
open sessions on the next request (BR-014, SC-2, `trebovaniya.md` §2 v103) — was independently traced
in the production code and is proven end-to-end by four HTTP tests with real PostgreSQL. 0 Critical,
0 Major, 0 Minor, 2 Informational. One unrelated test failed in the full run by coincidence (INFO-1)
and is green on rerun.

## 2. Reviewed Artifacts

As listed in `inputs`. API_DESIGN and DB_DESIGN are NOT_APPLICABLE records (v1). Spec v1 APPROVED
at HUMAN_SPEC_APPROVAL; SPEC_REVIEW m-1, m-2 closed FIXED by the Owner. No OPEN finding for US-064 in
`findings.jsonl` before this review.

## 3. Security-Relevant Scope

- Host: Data Plane (`ClassroomAgent.Web`). Paths exercised: `GET /workspace/journal`,
  `POST /api/v1/exports/journal-xlsx`, the Dean-accounts state change (Admin), Dean sign-in.
- Assets: Dean session cookie, `app_user.security_stamp`, `is_disabled`; journal personal data
  (names, grades) behind the session; export audit rows.
- Trust boundary: browser → installation host; cookie handler → Application (`AccountSessionService`)
  → persistence.
- Changed code: none. New file: `tests/ClassroomAgent.Tests/Web/Security/DisabledDeanSessionTests.cs`.

## 4. Environment and Tools

- .NET SDK 10.0.401; Docker Desktop 29.8.0 (Testcontainers available); 6.3 GB free RAM before the run.
- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 6802 total, 6801 passed, 1 failed, 0 skipped (9 min 59 s); the
  failure analysed in INFO-1, its class rerun 5/5 green.
- `dotnet test --filter FullyQualifiedName~DisabledDeanSessionTests` — 4/4 green (rerun).
- `dotnet list ClassroomAgent.sln package --vulnerable` — no vulnerable packages in all 7 projects.
- Delegated to a `cheap-worker` (facts only): build, full test run, vulnerability listing,
  SDK version. Code reading and judgement were done here.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed changed |
| SC-2 Authentication (disabled account, session) | PASS | `AppUser.Disable` sets `IsDisabled` and calls `RotateSecurityStamp()` (`AppUser.cs:132-136`); `ReEnable` does not restore a stamp (`:142-146`); cookie handler `OnValidatePrincipal = InstallationSession.ValidatePrincipalAsync` (`InstallationSecurityServices.cs:241`) runs on every request and rejects + signs out when `AccountSessionService.IsCurrentAsync` (`AccountSessionService.cs:25-29`) finds the account disabled or the stamp different; the state is read `AsNoTracking` from the database each time (`AppUserRepository.cs:24-29`), no cache. No `SecurityStampValidator` and no `ValidationInterval` anywhere in `src` (ripgrep over `*.cs`). |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Admin revocation out of scope (OD-001 a) |
| SC-4 Authorization | PASS (unchanged) | replayed export after disabling answers `401` with the API-6 body, not a redirect and not a file (AC-002 test) |
| SC-5 Read-only mode | NOT_APPLICABLE | no write use case added; tests run in normal mode (spec FR-001) |
| SC-6 … SC-9 | NOT_APPLICABLE | no DB UI, key, Google or channel change |
| SC-10 Hygiene | PASS | `401` body carries only `timestamp/status/error/message/path` (asserted) |
| SC-11 Audit | PASS | the refused export writes no further export audit row (AC-002 asserts count unchanged) |
| SC-12, SC-13 | NOT_APPLICABLE | no Control Plane contract or outbound flow change |

## 6. Authentication and Authorization

Requirement: BR-014 / SC-2 — disabling takes effect on the next request. Implementation evidence in
§5 SC-2. Tests (`DisabledDeanSessionTests`):

- AC-001 — replayed cookie on the journal: `302` to `/sign-in`, no course name in the body.
- AC-002 — replayed export with the pre-disable antiforgery token: `401` API-6, no
  `Content-Disposition`, audit count unchanged.
- AC-003 — re-enabled account: old cookie still `302` to sign-in; fresh password sign-in works.
- AC-004 — the second Dean's cookie still `200`; the disabled Dean's cookie refused in the same test.

Each test first shows the same replayed request succeeding before the disabling (FR-003), so a refusal
cannot come from a broken fixture. The disabling goes through the real Admin page
(`SetDeanStateAsync`), not a direct database write.

## 7. Credentials, Key and Google Access

No password handling changed. Test passwords are fixture constants (`JournalTestData.DeanPassword`).
No Google port or key touched.

## 8. Sensitive Data Exposure

No response, DTO, log line or export changed. The refused export returns no file (asserted).
Fixtures are synthetic (`dean@school-one.example.test`, `second.dean@…`; seeded journal).

## 9. Input Validation

Not touched by the Story.

## 10. API Security

No endpoint added or changed. `POST /api/v1/exports/journal-xlsx` with a stale session: `401`, API-6
body, no redirect (`Location` null asserted).

## 11. Persistence and Configuration

No entity, migration or configuration change.

## 12. Logging, Audit and Telemetry

No logging change. Audit: no export audit row for the refused request (AC-002).

## 13. Dependencies

No package or project reference added. Vulnerability scan: none reported (§4).

## 14. Security Test Coverage

| Requirement / abuse case | Test |
|---|---|
| Disabled Dean, open tab, next page | `TheOldCookieOfADisabledDean_IsSentToSignIn_OnTheJournalPage` |
| Disabled Dean, one more export | `TheOldCookieOfADisabledDean_DownloadsNoExport` |
| Re-enable does not revive old cookie | `ReEnabling_DoesNotReviveTheOldCookie_ButANewSignInWorks` |
| Isolation between Deans | `DisablingOneDean_LeavesAnotherDeansSessionWorking` |

These close the gap the Story named: a reintroduced stamp-validation interval would now fail the suite.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Disabled Dean keeps a tab open and clicks on | refused at next request | AC-001 test + `ValidatePrincipalAsync` | PASS |
| Disabled Dean replays a captured cookie + antiforgery token to export | `401`, no file, no audit | AC-002 test | PASS |
| Copy of an old cookie used after re-enabling | refused (stamp rotated) | AC-003 test, `ReEnable` code | PASS |

## 16. Repository Hygiene

Working tree: one test file and Story artifacts untracked, `workflow-state.yaml` and `history.jsonl`
modified. No secret-like file, generated database or `.xlsx` export in the change set. Live credential
files were not opened.

## 17. Deviations

None. The implementation report's claim "no production change" matches `git status`.

## 18. Findings

### INFO-1 — Coincidental failure of an unrelated logging test (TEST_COVERAGE)

- Affected: `tests/ClassroomAgent.Tests/Web/Logging/GroupLessonsBlankLoggingTests.cs:83` (US-043).
- Evidence: full run failed `ARefusedPerCopyValue_IsAWarningNamingTheRule_AndTheValueIsNeverLogged`.
  The assertion "no log line contains `7777`" matched the `RetentionPurgeCompleted` line whose
  timestamp was `2026-10-29T07:46:34.7777111Z`. The refused value was not logged. The class reran 5/5 green.
- Risk: none to security; the test fails rarely, whenever any captured line's timestamp happens to contain those digits.
- Suggested correction (outside this Story): search the rendered message or property values instead of
  the whole JSON line, or use a test value that cannot appear in a timestamp.
- Loop-back: none.

### INFO-2 — Story test class is slow in isolation (OTHER)

- `DisabledDeanSessionTests` 4/4 took 7 min 44 s when run alone (container start under memory pressure
  was not measured). No correctness impact; recorded so a later slow-suite review can include it.
- Loop-back: none.

## 19. Positive Controls

- Per-request session check against the database stamp and disabled flag, no caching, no interval.
- Stamp rotation on disable; no stamp restoration on re-enable.
- API-6 `401` under `/api/v1` instead of a redirect.
- End-to-end regression tests for all four ACs, each with a before-disable proof.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 resolved a) (no test for the Admin
revocation limitation).

## 21. Review Limitations

- Code, configuration and test review plus automated runs; no penetration testing.
- A request already in progress at the moment of disabling is out of scope (Story) and not reviewed.

## 22. Verdict Rationale

Build green, full suite green except one coincidental unrelated failure that is green on rerun, no
production change, every touched SC item PASS with code evidence, all four ACs proven by tests that
can fail. PASS; two Informational notes go to `non_blocking_findings`.
