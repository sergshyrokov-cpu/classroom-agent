---
artifact_type: implementation_report
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:33:32Z
updated_at: 2026-10-29T07:33:32Z
produced_by: dotnet-implementor
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
  - path: docs/evidence/US-064-test-generation-report.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-064 Implementation Report — A disabled Dean's open session is refused on the next request

## 1. Summary

Test-only Story, as the Story Notes and spec §1 / FR-008 expected. The four story tests
(`DisabledDeanSessionTests`) were green against the existing code at TEST_WRITING and remain green;
FR-008 allows a production change only when a story test fails, so **no production file was changed**.
The behaviour the tests prove already exists: `AppUser.Disable` rotates the security stamp
(`src/ClassroomAgent.Domain/Entities/AppUser.cs:132-136`), and every request runs
`InstallationSession.ValidatePrincipalAsync` (wired as `OnValidatePrincipal`,
`InstallationSecurityServices.cs:241`), which rejects the cookie when
`AccountSessionService.IsCurrentAsync` finds the account disabled or the stamp changed. The full suite,
build and format check pass. Security-sensitive: yes (session validation, BR-014, SC-2) — but only by
adding evidence, not by changing behaviour.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | docs/stories/US-064-disabled-dean-session-refused-on-next-request.md | — |
| Specification | docs/specifications/US-064-spec.md | 1 (APPROVED) |
| Open Decisions | docs/decisions/US-064-open-decisions.md | 1 (OD-001 resolved a) |
| API design | docs/designs/api/US-064-api-design.md | 1 (NOT_APPLICABLE) |
| DB design | docs/designs/database/US-064-db-design.md | 1 (NOT_APPLICABLE) |
| Test strategy | docs/tests/US-064-test-strategy.md | 1 |
| AC test matrix | docs/tests/US-064-ac-test-matrix.md | 1 |
| Test generation report | docs/evidence/US-064-test-generation-report.md | 1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation location (existing, unchanged) | Test (`DisabledDeanSessionTests`) | Status |
|---|---|---|---|
| AC-001 | `AppUser.Disable`; `InstallationSession.ValidatePrincipalAsync`; `AccountSessionService.IsCurrentAsync`; cookie redirect to `/sign-in` in `InstallationSecurityServices` | `TheOldCookieOfADisabledDean_IsSentToSignIn_OnTheJournalPage` | PASS |
| AC-002 | same, plus the `/api/v1` `401` API-6 body in `InstallationSecurityServices.cs:212-224` | `TheOldCookieOfADisabledDean_DownloadsNoExport` | PASS |
| AC-003 | `AppUser.ReEnable` does not restore the old stamp (`AppUser.cs:142-146`); password sign-in unchanged | `ReEnabling_DoesNotReviveTheOldCookie_ButANewSignInWorks` | PASS |
| AC-004 | stamp is per account (`app_user.security_stamp`) | `DisablingOneDean_LeavesAnotherDeansSessionWorking` | PASS |

### 3.1 Manual checks

None required.

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| tests/ClassroomAgent.Tests/Web/Security/DisabledDeanSessionTests.cs | created at TEST_WRITING | ac_test_matrix AC-001…AC-004 |
| docs/tests/US-064-test-strategy.md | created at TEST_WRITING | `test_strategy` artifact |
| docs/tests/US-064-ac-test-matrix.md | created at TEST_WRITING | `ac_test_matrix` artifact |
| docs/evidence/US-064-test-generation-report.md | created at TEST_WRITING | `test_generation_report` artifact |
| docs/evidence/US-064-implementation-report.md | this report | `implementation_report` artifact |
| docs/workflow/workflow-state.yaml, docs/workflow/history.jsonl | orchestrator | Git Policy closed list |

No file under `src/` changed. No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence (2026-10-29)

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` | 0 | 6802 total, 6802 passed, 0 failed, 0 skipped (9 min 57 s) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no files listed |

Story class at TEST_WRITING: `DisabledDeanSessionTests` 4/4. Free RAM before the full run: 4.2 GB, which
is borderline, but the suite ran without environment timeouts.

Delegation: the build, the full test run and the format check were run by a `cheap-worker` subagent
(report only, no file changes). There were no edits to delegate, because no production code changed.

## 6. Configuration Changes

None.

## 7. Deviations and Discovered Problems

None. No story test failed, so FR-008's defect-fix path was not used.

## 8. Open Decisions

None touched or newly required. OD-001 (resolved a) needs no implementation.
