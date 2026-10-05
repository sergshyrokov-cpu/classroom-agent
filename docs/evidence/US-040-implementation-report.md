---
artifact_type: implementation_report
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-05T07:03:15Z
updated_at: 2026-10-05T07:03:15Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-040-no-store-responses.md
    version: null
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
  - path: docs/designs/api/US-040-openapi.yaml
    version: 1
  - path: docs/designs/api/US-040-api-design.md
    version: 1
  - path: docs/designs/database/US-040-db-design.md
    version: 1
  - path: docs/tests/US-040-test-strategy.md
    version: 1
  - path: docs/tests/US-040-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
attempt: 1
---

# US-040 Implementation Report — Responses are not cached

## 1. Summary

One middleware per host, registered first in the pipeline, sets
`Cache-Control: no-store` when the response starts, keeping a value that
already carries `no-store` (D-1, D-3, FR-006). The static-file middleware marks
a served file through its `OnPrepareResponse` hook and the rule skips it (D-4,
FR-004). In the installation the rule sits before the public-port branch, so
both ports are covered by one registration (D-5). No endpoint, entity,
migration, translation key or package is added.

## 2. Source artifacts

See front matter `inputs`. DB_DESIGN `NOT_APPLICABLE`; OD-001 resolved (a).

## 3. Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `ClassroomAgent.Web.Security.NoStoreMiddleware`; registered first in `src/ClassroomAgent.Web/Program.cs` | `Web.Security.NoStoreResponseTests` (18) | PASS |
| AC-002 | `ClassroomAgent.ControlPlane.Security.NoStoreMiddleware`; registered first in `src/ClassroomAgent.ControlPlane/Program.cs` | `ControlPlane.Security.NoStoreResponseTests` (15) | PASS |
| AC-003 | `NoStoreMiddleware.MarkStaticFile` as `StaticFileOptions.OnPrepareResponse` in both `Program.cs` | `AStaticFile_DoesNotCarryNoStore`, `AMissingFileUnderAStaticPath_CarriesNoStore` (both hosts) | PASS |
| AC-004 | one registration per host, no attribute | the four `EveryEndpoint_*` tests; `Architecture.NoStoreRuleTests` | PASS |
| AC-005 | no other change | full suite | PASS (see §5) |

## 4. Change set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Web/Security/NoStoreMiddleware.cs` | new | FR-001, FR-003, FR-004, FR-006; openapi `x-host-wide-rules.no-store`, `static-files` |
| `src/ClassroomAgent.ControlPlane/Security/NoStoreMiddleware.cs` | new | FR-002, FR-003, FR-004, FR-006; same |
| `src/ClassroomAgent.Web/Program.cs` | middleware first; `UseStaticFiles` with the marking hook | FR-001, FR-003, FR-004; D-3, D-5 |
| `src/ClassroomAgent.ControlPlane/Program.cs` | the same | FR-002, FR-003, FR-004; D-3 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/NoStore.cs` | TEST_WRITING | ac_test_matrix |
| `tests/ClassroomAgent.Tests/Web/Security/NoStoreResponseTests.cs` | TEST_WRITING | ac_test_matrix AC-001, AC-003, AC-004 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/NoStoreResponseTests.cs` | TEST_WRITING | ac_test_matrix AC-002, AC-003, AC-004 |
| `tests/ClassroomAgent.Tests/Architecture/NoStoreRuleTests.cs` | TEST_WRITING | ac_test_matrix AC-004 |

No test was changed at this stage. No secret, database file or IDE config.

## 5. Validation evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | 0 warnings, 0 errors |
| `dotnet test … --filter "FullyQualifiedName~NoStoreResponseTests\|FullyQualifiedName~NoStoreRuleTests"` | 0 | 35/35 passed (red phase: 18 failed) |
| `dotnet test ClassroomAgent.sln` | 2 | 3439 total, 3438 passed, 1 failed, 0 skipped (6 min 16 s) |
| `dotnet test … --filter "FullyQualifiedName~JournalPageTests"` | 0 | 16/16 passed (re-run of the one failure) |
| `dotnet format ClassroomAgent.sln --verify-no-changes --include <8 changed files>` | 0 | no changes |

The one full-suite failure was
`Web.Pages.JournalPageTests.ADraftGrade_IsShownInTheFullView_AndNotInTheShortView`:
`NpgsqlException: The operation has timed out` while **creating its test
database** (`InstallationTestHost.MigrateAsync`, before any request was sent).
It is unrelated to this change — no HTTP request was made — and passed on
re-run. Recorded as finding IMPL F-1.

## 6. Configuration changes

None (no appsettings or environment change).

## 7. Deviations and discovered problems

None against the designs. Finding **IMPL F-1**: an intermittent Npgsql connect
timeout during test-database creation under full-suite load.

## 8. Open Decisions

None touched or required.
