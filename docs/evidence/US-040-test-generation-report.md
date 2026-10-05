---
artifact_type: test_generation_report
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-05T06:47:34Z
updated_at: 2026-10-05T06:47:34Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/designs/api/US-040-api-design.md
    version: 1
  - path: docs/tests/US-040-test-strategy.md
    version: 1
  - path: docs/tests/US-040-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-040 Test-Generation Report

## 1. Files

Created (tests only; no production file touched):

- `tests/ClassroomAgent.Tests/TestInfrastructure/NoStore.cs` — VR-001 check, enumeration loop, `wwwroot` file list
- `tests/ClassroomAgent.Tests/Web/Security/NoStoreResponseTests.cs` — 18 cases
- `tests/ClassroomAgent.Tests/ControlPlane/Security/NoStoreResponseTests.cs` — 15 cases
- `tests/ClassroomAgent.Tests/Architecture/NoStoreRuleTests.cs` — 2 cases

Modified: none.

## 2. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~NoStoreResponseTests|FullyQualifiedName~NoStoreRuleTests"` — 35 total, 17 passed, 18 failed.
- `dotnet test ClassroomAgent.sln` — **3439 total, 3421 passed, 18 failed, 0 skipped** (4 min 38 s).

The first attempt failed for all 35 at the `PostgreSqlFixture` constructor
(Docker Desktop's VM was stopped); Docker Desktop was restarted and the runs
above are after that.

## 3. Results

**Existing regression tests: all pass.** Every one of the 18 failures is in the
two new `NoStoreResponseTests` classes; no other test fails.

**Expected failures (RED, 18)** — each fails with the header absent
(`Cache-Control is '(absent)'`), the missing rule:

- Web: `EveryEndpoint_OnThePublicPort_CarriesNoStore` ×3 (first items: `GET /health/live → 404`, `GET /health/ready → 404`, `POST /service/v1/status-pushes → 404`), `EveryEndpoint_OnThePrivatePort_CarriesNoStore` (`GET /health/live → 200`, `GET /health/ready → 200`, `POST /service/v1/status-pushes → 400`), `TheRedirectToSignIn` (302), `TheHttpsRedirection` (308), `A403Refusal` (403), `A404` ×2, `AMissingFileUnderAStaticPath` (302).
- Control Plane: `EveryEndpoint_CarriesNoStore` ×3 (first items: `GET /error/404 → 404`, `POST /error/404 → 400`, `POST /service/v1/admin-login-checks → 400/302`), `ARedirect` ×2 (302 /sign-in, 302 /setup), `A404` ×2, `AMissingFileUnderAStaticPath` (404).

## 4. Tests passing before implementation (17) — explained

- **Pages, antiforgery `400`, read-only `409`, exception-handler `500`** (Web 7, Control Plane 6): the framework already writes a `Cache-Control` containing `no-store` on these — antiforgery token generation writes `no-cache, no-store` on every page that renders a form (the layout carries one), and the exception handler writes `no-cache,no-store`. Spec FR-006 accepts such values. The tests are not vacuous: each asserts the status first (200/400/409/500), and the enumeration tests prove the responses the framework does not cover.
- **`AStaticFile_DoesNotCarryNoStore`** (×2): asserts the *absence* of `no-store` on served files — true today, and the test that catches a rule applied too widely.
- **`NoStoreRuleTests`** (×2): a guard; no `[ResponseCache]` exists today.

## 5. Corrections during the stage

- `Web … AMissingFileUnderAStaticPath_CarriesNoStore` first asserted `404`; the installation answers an anonymous `/css/no-such-file.css` with `302 /sign-in`, because the catch-all (`{*path:nonfile}`) does not match a file-like path and the fallback policy challenges. The spec (VR-003) requires only the header, so the test now asserts "not `200`" + `no-store`. See finding F-2.

## 6. Untested Acceptance Criteria

None. Excluded scenarios (no download endpoint, no `/api/v1` operation, Kestrel-level responses, no `403`/`409` on the Control Plane) are listed with reasons in the test strategy §6.

## 7. Findings (non-blocking)

- **F-1** 17 tests pass before implementation; explained in §4.
- **F-2** Pre-existing: on the installation's public port an anonymous request to a missing *file-like* path answers `302 /sign-in`, not `404` as SC-4 v66 says for "an unmatched request". Not this Story's scope; raised for the Owner.

## 8. Open Decisions

None open.

## 9. Overall result

PASS — red phase verified; every AC mapped; regression suite green.
