---
artifact_type: test_generation_report
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-13T13:40:00Z
updated_at: 2026-10-13T13:40:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/designs/api/US-024-api-design.md
    version: 1
  - path: docs/designs/api/US-024-openapi.yaml
    version: 1
  - path: docs/designs/database/US-024-db-design.md
    version: 1
  - path: docs/designs/database/US-024-entity-model.md
    version: 1
  - path: docs/tests/US-024-test-strategy.md
    version: 1
  - path: docs/tests/US-024-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 3
supersedes: null
---

# US-024 Test Generation Report — Database statistics and last synchronization

## 1. Result

**The red phase is verified.** The solution builds with 0 warnings and 0 errors.

The full suite ran: 5497 tests, 5417 passed, 80 failed, 0 skipped.

- Every failure is a new US-024 test that fails because production behaviour is missing.
- No existing test failed.
- 6 new tests pass; §5 explains each one.

## 2. Production skeleton (OD-012 a, Owner 2026-10-13)

The skeleton is compile-only:
- nothing is registered in DI;
- there is no route, view, translation or migration;
- no existing behaviour changed;
- the members that would do work throw `NotImplementedException`.

Files:
- `Application/Models/DatabaseCounts.cs`
- `Application/Ports/IDatabaseStatisticsSource.cs`
- `Application/Models/Dtos/CourseCounts.cs`, `SynchronizationDisplayState.cs`, `FailureGuidance.cs`, `SynchronizationBlock.cs`, `DatabaseStatisticsView.cs`
- `Application/UseCases/GetDatabaseStatisticsQuery.cs` (`ExecuteAsync` throws)
- `Infrastructure/Persistence/DatabaseStatisticsReadSource.cs` (`GetCountsAsync` throws)

The signatures are those of entity model §2–§4 and §6. Some items belong with the route and the view at IMPLEMENTATION, so they were not added here:
- `SignInRoutes.Database` and `SignInRoutes.DatabaseSynchronizationRequests`;
- `InstallationPolicies.ViewDatabaseStatistics`;
- the controller and the host page model.

## 3. Files

**Test sources created** (under `tests/ClassroomAgent.Tests/`):

| File | Tests (cases) |
|---|---|
| `TestInfrastructure/DatabaseStatisticsTestData.cs` | fixture |
| `TestInfrastructure/DatabaseStatisticsHostExtensions.cs` | fixture |
| `TestInfrastructure/FakeDatabaseStatisticsSource.cs` | fixture |
| `Application/UseCases/DatabaseStatisticsQueryTests.cs` | 17 |
| `Infrastructure/Persistence/DatabaseStatisticsReadSourceTests.cs` | 10 |
| `Web/Pages/DatabaseStatisticsPageTests.cs` | 22 |
| `Web/Security/DatabaseStatisticsAuthorizationTests.cs` | 9 |
| `Web/UseCases/DatabaseSynchronizationRequestTests.cs` | 12 |
| `Web/UseCases/DatabaseStatisticsReadOnlyTests.cs` | 8 |
| `Web/Localization/DatabaseStatisticsTranslationTests.cs` | 5 |
| `Web/Logging/DatabaseStatisticsLoggingTests.cs` | 3 |

That is 86 new test cases. No existing test source was modified.

**Production skeleton:** see §2.

**Artifacts:**
- `docs/tests/US-024-test-strategy.md`
- `docs/tests/US-024-ac-test-matrix.md`
- this report
- `docs/decisions/US-024-open-decisions.md` v3, which adds OD-012.

**Delegation.** Done inline:
- the test design;
- the skeleton;
- the three fixtures;
- the unit and integration classes;
- the strategy and the matrix;
- the red-phase verdict.

One `cheap-worker` agent wrote the six HTTP classes against a fixed scenario brief. I checked its output by building, by the full run and by reading the page class.

The agent deviated from the brief in four places:
- the form test runs over all four modes;
- the after-current-work message is tested for Admin only;
- the press helper is duplicated in two classes;
- the home-page theory covers both roles × {normal, suspended}.

## 4. Execution

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us024-red.trx` | 5497 total, 5417 passed, 80 failed, 0 skipped, 8 m 58 s |

The first attempt could not run: the Docker engine was down ("Docker Desktop is unable to start"). Docker Desktop and WSL were restarted, and then the suite ran.

All 80 failures are in US-024 classes:

| Class | Failed | Cause |
|---|---|---|
| `DatabaseStatisticsQueryTests` | 15 | `NotImplementedException` from the skeleton (in `AnUnknownStatus_Throws`, it arrives in place of `ArgumentOutOfRangeException`) |
| `DatabaseStatisticsReadSourceTests` | 10 | `NotImplementedException` from the skeleton |
| `DatabaseStatisticsPageTests` | 22 | `404` where `200` is expected; the home page has no `nav#database-section` (4) |
| `DatabaseStatisticsAuthorizationTests` | 7 | `404` where `200`/`302`/`400` is expected; no endpoint enumerated for `database` |
| `DatabaseSynchronizationRequestTests` | 10 | `404` where `302`/`409` is expected |
| `DatabaseStatisticsReadOnlyTests` | 8 | 4 × `GetDatabaseStatisticsQuery` not registered in DI; 4 × `404` |
| `DatabaseStatisticsTranslationTests` | 5 | `Database.Title` key missing; `404` |
| `DatabaseStatisticsLoggingTests` | 3 | `404` where `200`/`302` is expected |

Counted by cause:
- 45 × `NotFound`;
- 26 × `NotImplementedException`;
- 4 × no `database-section` on the home page;
- 4 × not registered in DI;
- 1 × missing translation key.

**Existing regression tests:** 5411 passed. They include:
- the US-019 synchronization-request classes;
- the home-page tests;
- the endpoint-enumerating security tests;
- `MeetCodesReadSourceTests`.

## 5. New tests passing before implementation

- **`DatabaseSynchronizationRequestTests.TheUs019Endpoint_StillReturnsToTheRolesOwnPage`** (2 cases). This is a regression test of the unchanged US-019 endpoint, so it passes by design.
- **`DatabaseStatisticsAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange_ForTheGet` and `…_ForThePress`.** The US-012 forced-password-change redirect runs before routing for every path, so these pass now. They are not vacuous: they assert the `302` target and that nothing is requested. Once the route exists, they prove the page is not exempt from that redirect.
- **`DatabaseStatisticsQueryTests.TheQuery_DependsOnReadPortsOnly` and `TheDtos_HaveNoFieldForADiagnosis_AStep_OrTheProcessedCounter`.** These are structural guards on the shape fixed in the entity model, which the skeleton already has.

The anonymous GET and POST tests fail (`404`), unlike the US-022 analogue. So the strategy §8 caveat about a vacuous pass does not apply to them in this run.

## 6. Untested Acceptance Criteria

None. Every AC-001 … AC-019 maps to at least one test in the matrix. The phone layout (NFR-070) is checked at review (strategy §7).

## 7. Open Decisions

OD-012 (compile-only skeleton) was resolved (a) by the Owner on 2026-10-13. No Open Decision is open.

## 8. Notes for IMPLEMENTATION

**Binding contracts:**
- **Markup hooks and translation keys** in strategy §4 and §5 are binding. Counters and synchronization lines are elements whose only text is the value (`data-counter`, `data-sync-line`).
- **The `409` connection re-render** must show the Database page itself: counters, the form and `#synchronization-request-message` with the role's message.
- **Times** are shown in the culture's short date + ` HH:mm` of the wall-clock value; a missing time shows the `Database.Sync.NotYet` text.

**Wiring and placement:**
- **Registration:** register `GetDatabaseStatisticsQuery` and `DatabaseStatisticsReadSource` (scoped). Register `ViewDatabaseStatistics` beside the other policies.
- **Home page:** `nav#database-section` goes between `report-templates-section` and the Admin-only `admin-settings`; for a Dean it is the last `<nav>`.

**Constraints to keep:**
- **AC-018:** `TheCodesAwaitingDecision_EqualTheMeetCodesPagesUnassignedCount` compares with `MeetCodesReadSource.GetCountsAsync()`. Keep the SQL rule identical to that LINQ.
- **AC-017:** the counters must be exactly one command, and the page at most two.
