---
artifact_type: test_generation_report
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:20:30Z
updated_at: 2026-10-29T07:20:30Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/tests/US-064-test-strategy.md
    version: 1
  - path: docs/tests/US-064-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-064 Test Generation Report

## 1. Files

- **Created:** `tests/ClassroomAgent.Tests/Web/Security/DisabledDeanSessionTests.cs` (4 `[Fact]`s).
- **Modified:** none. Existing helpers reused unchanged: `JournalHostExtensions`,
  `DeanAccountHostExtensions` (`InsertDeanAsync`, `SignInAsDeanAsync`, `SetDeanStateAsync`),
  `JournalExportHostExtensions`, `ReportHostExtensions.AccountIdAsync`, `FormClient.ReplaceCookies`.
- **Compile-only skeleton:** none — the tests reference only existing declarations.
- **Production code:** not touched.

## 2. Delegation

Test design, the test class and these artifacts were written by the test writer. The build and the
three test runs were delegated to a `cheap-worker` subagent (no file changes, report only).

## 3. Execution evidence (2026-10-29)

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `dotnet test --filter FullyQualifiedName~DisabledDeanSessionTests` | 4 total, 4 passed, 0 failed, 0 skipped |
| `dotnet test --filter FullyQualifiedName~InstallationSignOutTests` (regression, replay model) | 5 total, 5 passed |
| `dotnet test --filter FullyQualifiedName~JournalExportAuthorizationTests` (regression, export 401 body) | 9 total, 9 passed |

Docker was running; free RAM 8.1 GB before the runs.

## 4. Red-phase classification

All four new tests **pass before implementation** — category "passes because existing behaviour
already satisfies the requirement". This is the expected state of this Story (Story Notes, spec §1,
FR-008: a red test here would be a production defect against BR-014). Why each is not vacuous:

| Test | Can it fail? |
|---|---|
| …_IsSentToSignIn_OnTheJournalPage | The same replay client gets `200` with the course name before the disabling; only the disabling stands between that and the asserted `302`. Without effective per-request validation the replay stays `200` and the test fails. |
| …_DownloadsNoExport | The identical request (cookies, token, body) returns an `.xlsx` and writes one audit row before the disabling, so the `401` is not an antiforgery, validation or fixture refusal (VR-001). |
| ReEnabling_… | Old cookie `200` before; after re-enable the account is active again, so only the stamp rotation of `Disable` keeps it refused; the fresh sign-in `200` proves the account works. |
| DisablingOneDean_… | Asserts both sides: the disabled Dean's cookie `302` (fails if the disabling did not happen — review m-1) and the other's `200`. |

A mutation run (removing the per-request check in `src` to watch the tests turn red) was not made:
it would change production code outside TEST_WRITING's remit, and the memory note "Auto mode blocks
src mutation" records that such a change needs the Owner's prior consent. The before/after
assertions above are the proof the tests can fail.

## 5. Unexpected failures

None.

## 6. Untested Acceptance Criteria

None — AC-001…AC-004 each map to one test (see the AC test matrix).

## 7. Open Decisions

None open. OD-001 resolved a) (no test pinning the Admin-revocation limitation).

## 8. Overall result

PASS. IMPLEMENTATION is expected to change no production code (FR-008): its work is to confirm the
full suite stays green and record the Story as test-only.
