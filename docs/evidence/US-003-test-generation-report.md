---
artifact_type: test_generation_report
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T10:05:00Z
updated_at: 2026-09-17T10:05:00Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-003-test-strategy.md
    version: 1
  - path: docs/tests/US-003-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
supersedes: null
---

# US-003 Test Generation Report

## Overall result: PASS — red phase verified

63 new test methods (140 test cases) cover AC-001 … AC-012 and the database
design; two US-001/US-002 tests and one test helper were adjusted. The solution
builds with no error and no warning. Full suite: 499 cases, 363 pass, 136 fail.
Every failure is missing production behaviour; no failure is caused by the tests
or the environment. No production file was modified.

## Environment

| Check | Command | Result |
|---|---|---|
| Docker | `docker info --format '{{.ServerVersion}}'` | `29.8.0` |
| PostgreSQL | Testcontainers `postgres:17-alpine` | started and removed by the run |
| Build | `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| Tests | `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us003-red.trx` | exit 2 (failures expected); 55.9 s |

## Files

### Created

| File | Purpose |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/AllowedAdminTestData.cs` | synthetic `*.example.test` emails, route paths |
| `tests/ClassroomAgent.Tests/TestInfrastructure/AllowedAdminRow.cs` | stored `allowed_admin` row |
| `tests/ClassroomAgent.Tests/TestInfrastructure/AllowedAdminHostExtensions.cs` | add / revoke over HTTP, read rows, insert rows directly |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminListTests.cs` | AC-001, AC-007 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminAddTests.cs` | AC-002, AC-006 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminValidationTests.cs` | AC-003 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminUniquenessTests.cs` | AC-004 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminRevocationTests.cs` | AC-005, AC-006, AC-008 |
| `tests/ClassroomAgent.Tests/ControlPlane/Controllers/AllowedAdminAuditTests.cs` | AC-009 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/AllowedAdminAuthorizationTests.cs` | AC-010 |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/AllowedAdminAntiforgeryTests.cs` | AC-011 |
| `tests/ClassroomAgent.Tests/ControlPlane/Localization/AllowedAdminTranslationTests.cs` | AC-012 |
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/AllowedAdminSchemaTests.cs` | db-design §3 |

### Modified

| File | Change |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/HostEndpoint.cs` | `SamplePath` substitutes a UUID for `{adminid}` so the US-001 enumeration tests reach the new endpoints |
| `tests/ClassroomAgent.Tests/ControlPlane/Persistence/MigrationTests.cs` | expects `allowed_admin` and migration `_AddAllowedAdmin` (db-design §7) |
| `tests/ClassroomAgent.Tests/ControlPlane/Security/InstallationAuthorizationTests.cs` | the exact US-002 route list excludes `installations/{id}/admins…` (asserted by `AllowedAdminAuthorizationTests`) |

## Execution result

| Class | Methods | Cases | Fail | Pass |
|---|---|---|---|---|
| `AllowedAdminAddTests` | 8 | 10 | 10 | 0 |
| `AllowedAdminAntiforgeryTests` | 5 | 7 | 7 | 0 |
| `AllowedAdminAuditTests` | 5 | 5 | 5 | 0 |
| `AllowedAdminAuthorizationTests` | 5 | 18 | 17 | 1 |
| `AllowedAdminListTests` | 6 | 9 | 9 | 0 |
| `AllowedAdminRevocationTests` | 11 | 16 | 12 | 4 |
| `AllowedAdminSchemaTests` | 12 | 25 | 25 | 0 |
| `AllowedAdminTranslationTests` | 3 | 21 | 21 | 0 |
| `AllowedAdminUniquenessTests` | 3 | 4 | 4 | 0 |
| `AllowedAdminValidationTests` | 5 | 25 | 25 | 0 |
| **New total** | **63** | **140** | **135** | **5** |
| `MigrationTests` (changed) | 2 | 2 | 1 | 1 |
| All other existing tests | — | 357 | 0 | 357 |

### Failure causes (all expected)

| Cause | Cases |
|---|---|
| `42P01 relation "allowed_admin" does not exist` — table not migrated yet (tests inserting or reading entries) | 48 |
| Translation key missing for `uk` | 21 |
| Status or value differs (routes not implemented → 404 instead of 200/302/400/403/409, route list empty) | 56 |
| Schema catalogue values missing (columns, index definitions, FK and constraint lists) | 10 |
| `MigrationTests`: table list and migration count | 1 |

(Counts from the first line of each failure message in `TestResults/us003-red.trx`.)

### Passing new cases (GUARD)

- `AllowedAdminRevocationTests.NonUuidRouteValues_Return404ErrorPage` *(4)* — a
  non-GUID segment already falls to the anonymous 404 catch-all of US-001; must
  stay green once the routes exist with `guid` constraints.
- `AllowedAdminAuthorizationTests.BeforeSetup_RedirectsToSetup(GET /installations/{id})`
  — the detail route exists since US-002 and the setup gate applies.

`MigrationTests.Model_HasNoPendingChangesAgainstMigrations` passes because no
model change exists yet; it guards that `AddAllowedAdmin` matches the model.

### Existing regression tests

All 357 unchanged existing cases pass, including the US-001 enumeration tests
(`AnonymousEndpointTests`, `AntiforgeryTests`) with the `SamplePath` change and the
adjusted `InstallationAuthorizationTests`.

## Untested Acceptance Criteria

None. Every AC-001 … AC-012 has RED tests (see matrix).

## Notes for IMPLEMENTATION

- The `WrongDomain` and `DomainHint` translations must take the domain as `{0}`.
- The revoke confirmation form must post to its own path with `method="post"`.
- Warning element `id="allowed-admins-warning"`, note element
  `id="revoke-fewer-than-two-note"` (API design §4).
- Rows are compared as records in several tests, so revoking and adding must not
  touch the `installation` row (including `updated_at`).

## Open Decisions

None.
