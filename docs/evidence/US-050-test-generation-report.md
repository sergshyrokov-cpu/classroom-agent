---
artifact_type: test_generation_report
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T09:45:00Z
updated_at: 2026-10-16T09:45:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
  - path: docs/designs/api/US-050-api-design.md
    version: 1
  - path: docs/designs/database/US-050-db-design.md
    version: 1
  - path: docs/tests/US-050-test-strategy.md
    version: 1
  - path: docs/tests/US-050-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-050 Test Generation Report

## 1. Story

US-050 — Every response carries the browser security headers (spec v1, APPROVED).

## 2. Test files created

| File | Contents |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/SecurityHeaders.cs` | expected values (FR-003/FR-004), exactly-once check (VR-001/VR-002), endpoint enumeration (VR-004), CSP parser |
| `tests/ClassroomAgent.Tests/TestInfrastructure/InlineCode.cs` | view scan (VR-003 + SPEC_REVIEW m-1 `<style>`), script scan (FR-007) |
| `tests/ClassroomAgent.Tests/Web/Security/SecurityHeadersResponseTests.cs` | installation, 20 methods / 25 cases |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/SecurityHeadersResponseTests.cs` | Control Plane, 10 methods / 17 cases |
| `tests/ClassroomAgent.Tests/Architecture/InlineCodeRuleTests.cs` | AC-005 view scan, script scan, scanner self-tests |
| `tests/ClassroomAgent.Tests/Architecture/SecurityHeadersRuleTests.cs` | FR-001 one rule per host: no header named outside `Security`, no `<meta http-equiv>` |

## 3. Test files modified

None.

## 4. Compile-only skeleton

None needed: the tests address the hosts over HTTP and scan source files; they reference no new
production declaration. No production file was touched.

## 5. Commands

All run from the repository root (build and the first runs delegated to a `cheap-worker` subagent,
reruns inline):

1. `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
2. `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~SecurityHeadersResponseTests|FullyQualifiedName~InlineCodeRuleTests|FullyQualifiedName~SecurityHeadersRuleTests"`
   — first attempt with Docker Desktop stopped: all 75 failed in the assembly fixture
   (`DockerUnavailableException`); not a test result. Docker started, rerun: **75 total, 33 passed,
   42 failed**.
3. `dotnet test ClassroomAgent.sln` (whole suite, run twice):
   - run 1, right after Docker's cold start: 6037 total, 5675 passed, 362 failed — 42 new red tests +
     320 Npgsql timeouts (`The operation has timed out`, `Exception while reading from stream`) with
     13–14 min durations; a stalled container, not code.
   - run 2: **6037 total, 5983 passed, 54 failed, 0 skipped** (17 min 15 s) — the 42 new red tests +
     12 existing tests in 12 classes, each failing with `Npgsql.NpgsqlException : Exception while
     reading from stream` / `Timeout during reading attempt` after ~8 min 00 s (one container stall).
4. Rerun of those 12 classes (`InstallationDatabaseTests`, `CourseMembershipSchemaTests`,
   `PageSearchHintTests`, `ReportNameSourceLoggingTests`, `JournalSourceTests`,
   `WorkspaceConnectionPageTests`, `IncrementalSyncHostTests`, `MeetReportsExportTests`,
   `InstallationDetailTests`, `MeetCodeActionsHttpTests`, `RetentionPurgeTests`,
   `InstallationValidationTests`): **303 total, 303 passed** (58 s).

Conclusion: every existing test passes; the 12 were a transient Testcontainers/PostgreSQL stall, as
the identical ~8 min timeouts and the clean rerun show. No production code changed, so the new tests
cannot have altered existing behaviour.

## 6. Expected failing new tests (red phase) — 42

All fail on their header assertion, for the reason the Story exists — not on setup:

- Installation (25): every case of `SecurityHeadersResponseTests` in `ClassroomAgent.Tests.Web.Security`.
  Messages show all four headers `(absent)`; on `/sign-in` `X-Frame-Options` is `'SAMEORIGIN'`
  (antiforgery) and the other three absent; `TheSentPolicy_IsStrict` fails at `Assert.Single` (no CSP).
  `TheGoogleSignInStart_RedirectsToTheOriginTheFormActionAllows` reaches its header assertion after
  `POST /sign-in/google → 302` — the start redirects with the real handler configuration, no request
  leaves the process. Enumeration lists show the expected statuses (private paths 404 on the public
  port, health 200 on the private port, 302/400 elsewhere).
- Control Plane (17): every case of `SecurityHeadersResponseTests` in
  `ClassroomAgent.Tests.ControlPlane.Security`; same pattern (`SAMEORIGIN` on `/sign-in` and `/setup`,
  otherwise all absent; strictness test fails at `Assert.Single`).

## 7. New tests passing before implementation — 33 (by design)

All of `InlineCodeRuleTests` and `SecurityHeadersRuleTests`. They are guards:

- `EveryView_CarriesNoInlineCode` (both hosts) — spec FR-006 states every view already complies.
- `TheScan_ReachesTheViewsThatIncludeScripts` — proves the scan covers the views with scripts.
- `EveryScript_RunsUnderTheStrictPolicy` (both hosts) — the scripts use no refused construct.
- `NoSourceFileOutsideTheSecurityNamespace_NamesASecurityHeader`, `NoView_SetsAHeaderThroughMeta`
  (both hosts) — nothing names the headers today.
- `TheScan_ReportsInlineCode` (7), `TheScan_AcceptsCompliantMarkup` (7),
  `TheScriptScan_ReportsRefusedConstructs` (5), `TheScriptScan_AcceptsAllowedConstructs` (4) — the
  scanners detect each violation kind (including the m-1 `<style>` block) and accept compliant markup,
  so the green guards are not vacuous.

## 8. Unexpected failures

None in the new tests. The existing-suite timeouts are explained in §5.

## 9. Untested Acceptance Criteria

None automated-untested. AC-004 has a manual part (real-browser Google sign-in) that a header test
cannot prove; it is recorded for the implementation report (Story notes, spec AC-004).

## 10. Open Decisions

None.

## 11. Overall result

PASS — every AC mapped (`docs/tests/US-050-ac-test-matrix.md`), tests compile with 0 warnings, the 42
behaviour tests are red only for the missing headers, the 33 guards are green with proven scanners,
and the existing suite is green once the transient container stall is rerun.
