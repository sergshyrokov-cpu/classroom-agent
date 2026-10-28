---
artifact_type: test_generation_report
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T20:12:05Z
updated_at: 2026-10-27T20:12:05Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/api/US-062-openapi.yaml
    version: 1
  - path: docs/designs/database/US-062-db-design.md
    version: 1
  - path: docs/designs/database/US-062-entity-model.md
    version: 1
  - path: docs/tests/US-062-test-strategy.md
    version: 1
  - path: docs/tests/US-062-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-062 Test Generation Report

**Overall result: PASS.** Every AC-001 … AC-017 is mapped (`docs/tests/US-062-ac-test-matrix.md`). The solution
builds with 0 warnings and 0 errors once the skeleton is in. Every new behaviour test that fails does so for the
expected reason: the skeleton, the missing service, the missing route or the missing schema. The existing tests
pass, apart from the four adapted schema / matrix tests, which are red for the same missing schema and routes.

## 1. Files

Created (tests and test infrastructure):

- `tests/ClassroomAgent.Tests/Application/UseCases/DataHealthInvariantTests.cs` (18 cases)
- `tests/ClassroomAgent.Tests/Application/UseCases/CourseListEmptySinceTests.cs` (3)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/DataHealthPersistenceTests.cs` (35)
- `tests/ClassroomAgent.Tests/Web/UseCases/DataHealthQueryTests.cs` (35)
- `tests/ClassroomAgent.Tests/Web/UseCases/ConfirmCoursesDeletedUseCaseTests.cs` (21)
- `tests/ClassroomAgent.Tests/Web/Pages/DataHealthBannerTests.cs` (25)
- `tests/ClassroomAgent.Tests/Web/Pages/DataHealthPageTests.cs` (26)
- `tests/ClassroomAgent.Tests/Web/Pages/CourseDeletionConfirmationHttpTests.cs` (12)
- `tests/ClassroomAgent.Tests/TestInfrastructure/DataHealthHostExtensions.cs`, `DataHealthTexts.cs`

Modified:

- `tests/ClassroomAgent.Tests/TestInfrastructure/SyncWorld.cs`: `CourseRepository.GetForAbsenceConfirmationAsync`
  (the fake implements the new port member).
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs`: the 24th migration ends
  `_AddDataHealth`.
- `tests/ClassroomAgent.Tests/Web/Persistence/SyncStateSchemaTests.cs`: 16 columns, `course_list_empty_since`.
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs`: the new column and the
  two new CHECKs.
- `tests/ClassroomAgent.Tests/Web/Security/PermissionMatrixTests.cs`: §2 rows 749 / 750 and the two endpoints.

Each adaptation follows from a requirement change in this Story (strategy §5). No assertion was weakened.

## 2. Compile-only skeleton

Every new member throws `NotImplementedException("US-062 TEST_WRITING skeleton: IMPLEMENTATION completes it.")`.
Nothing is registered in DI, and there is no migration, route or translation entry. Existing behaviour is
unchanged.

| Declaration | File |
|---|---|
| `SyncState.CourseListEmptySince` (property; `RecordCourseListRead` behaviour unchanged) | `src/ClassroomAgent.Domain/Entities/SyncState.cs` |
| `Course.ConfirmDeletedInClassroom(DateTimeOffset)` | `src/ClassroomAgent.Domain/Entities/Course.cs` |
| `AuditAction.CoursesConfirmedDeleted` | `src/ClassroomAgent.Domain/Enums/AuditAction.cs` |
| `AuditEvent.ConfirmedCourseIds`; `CoursesConfirmedDeleted(...)`, `CoursesConfirmedDeletedRefused(...)` | `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` |
| `IDataHealthSource` (3 members) | `src/ClassroomAgent.Application/Ports/IDataHealthSource.cs` |
| `ICourseRepository.GetForAbsenceConfirmationAsync`; `CourseRepository` throws | `src/ClassroomAgent.Application/Ports/ICourseRepository.cs`, `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseRepository.cs` |
| `CourseHealthFact`, `AboveMaximumGradeRow`, `ItemNotInClassroomRow`, `CourseDeletionConfirmationOutcome`, `CourseDeletionConfirmationResult` | `src/ClassroomAgent.Application/Models/*.cs` |
| `CourseDeletionConfirmationForm` | `src/ClassroomAgent.Application/Models/Requests/CourseDeletionConfirmationForm.cs` |
| `GetDataHealthCountsQuery`, `GetDataHealthQuery`, `ConfirmCoursesDeletedUseCase` (no constructor dependencies yet) | `src/ClassroomAgent.Application/UseCases/*.cs` |
| DTOs `DataHealthBannerModel`, `DataHealthPageModel`, `ReadOnlyNotice`, `TechnicalProblem`, `TechnicalDetails`, `NotVisibleCourse`, `CourseLabel`, `CourseProblems`, `SkippedCourse`, `ItemNotInClassroom`, `AboveMaximumGrade`; enums `TechnicalProblemKind`, `SkippedCourseVariant`, `DataHealthMessage` | `src/ClassroomAgent.Application/Models/Dtos/*.cs` (the view-model folder of US-060 and earlier) |
| `InstallationPolicies.ViewDataHealth`, `ConfirmCoursesDeleted` (constants only, not registered) | `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` |
| `builder.Ignore(...)` for `CourseListEmptySince` and `ConfirmedCourseIds` until the migration maps them | `SyncStateConfiguration.cs`, `AuditEventConfiguration.cs` |

The DTOs are positional records whose members are the OpenAPI properties in PascalCase. The tests read
properties and never construct the DTOs, apart from comparing `SkippedCourse`, `ReadOnlyNotice` and
`DataHealthBannerModel` by value. IMPLEMENTATION owns all of these from here on: the behaviour, DI, the
`AddDataHealth` migration, the routes, the views and the translation entries. It also removes the `Ignore`
calls.

## 3. Commands

- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~<Class>"`: one term per run, 17 runs.
  Raw output is in the session scratchpad (`runs/`).
- Docker Desktop had to be started first. There was no Testcontainers, Npgsql or WSL failure.

## 4. Results per run

| Run | Total | Passed | Failed | Failure reasons |
|---|---|---|---|---|
| `DataHealthInvariantTests` (+20 existing `SyncDataHealthInvariantTests` matched by the filter) | 38 | 21 | 17 | `NotImplementedException` from the skeleton: 8 direct, plus 5 inside `Assert.Throws` / `ThrowsAny` expecting `ArgumentOutOfRangeException` / `ArgumentException`. Date not set: 4 (`RecordCourseListRead` is unchanged) |
| `CourseListEmptySinceTests` | 3 | 1 | 2 | date null after an empty run |
| `DataHealthPersistenceTests` (+12 existing `SyncDataHealthPersistenceTests`) | 47 | 12 | 35 | no service `IDataHealthSource` (10); CHECK constraints absent (10); `42703` column does not exist (4); `NotImplementedException` (3); column, index or migration absent (8) |
| `DataHealthQueryTests` | 35 | 0 | 35 | no service `GetDataHealthQuery` / `GetDataHealthCountsQuery` (32); `42703` `course_list_empty_since` (3) |
| `ConfirmCoursesDeletedUseCaseTests` | 21 | 0 | 21 | no service `ConfirmCoursesDeletedUseCase` (17; for the 4 read-only cases this surfaces as the wrong exception type) |
| `DataHealthBannerTests` | 25 | 7 | 18 | banner text null, i.e. no banner yet (14); `404` for the page (2); no warning log line (1); restrained-markup hook absent (1) |
| `DataHealthPageTests` | 26 | 1 | 25 | `404` (route missing) (24); `42703` while seeding (1) |
| `CourseDeletionConfirmationHttpTests` | 12 | 1 | 11 | `404` (route missing) instead of 302 / 400 / 403 / 409 |
| `AppUserMigrationTests` | 6 | 5 | 1 | 23 migrations, index 23 absent |
| `SyncStateSchemaTests` | 11 | 10 | 1 | 15 columns, expected 16 |
| `InstallationAuditEventSchemaTests` | 29 | 27 | 2 | the new column and constraints missing |
| `PermissionMatrixTests` | 3 | 1 | 2 | the two rows have no endpoint and the two policies are not registered |
| `CourseListPresenceTests`, `CourseListPresenceInvariantTests`, `CourseListMarksPersistenceTests` | 22 / 25 / 14 | all | 0 | regression: green |
| namespace `ClassroomAgent.Tests.Application` | 2046 | 2027 | 19 | exactly the 17 + 2 new red cases above |
| namespace `ClassroomAgent.Tests.Web.Security` | 481 | 479 | 2 | exactly the two `PermissionMatrixTests` cases |

## 5. New tests that pass before implementation (Green\*), explained

- `ANewState_HasNoEmptyListDate` (INV) and `AnEmptyListWithNoCourseStored_SetsNoDate` (RUN): null is the
  specified initial state, and the skeleton property defaults to it. Each is paired with red tests that set
  and clear the date.
- `WithNoProblem_ThereIsNoBanner` for `/` and `/workspace/courses`, `AnArchivedCoursesProblemAlone_ShowsNoBanner`,
  `TheForcedPasswordChangePage_HasNoBanner`, `ThePageWithoutASession_HasNoBanner`, `AnApiResponse_HasNoBanner`
  (BAN): each proves the banner is absent. That is free while no banner exists. Each guards against
  IMPLEMENTATION showing it where FR-003.1 / OD-001 a / AC-002 / AC-003 forbid, and is paired with red tests
  where the banner must appear on the same pages.
- `ThePinnedPluralRule_MatchesTheSpecificationsExamples` (BAN): a check of the test's own plural rule against
  the Specification's examples. It involves no production code.
- `WithoutAFullSession_ThePageRedirects` and `WithoutAFullSession_ThePostRedirects_AndChangesNothing` for the
  restricted (temporary-password) session (PAG, CNF): `TemporaryPasswordMiddleware` redirects every path of a
  restricted session to the change page, missing routes included. The behaviour the tests pin exists already.
  The anonymous cases of the same theories are red (`404` from the fallback).

## 6. Expected failing new tests

All failures above are caused by missing behaviour, service, route or schema. None comes from syntax,
fixtures, host configuration or an assertion error.

## 7. Unexpected failures

None.

## 8. Untested Acceptance Criteria

None. The limitations are in strategy §6–§7: AC-004 is simulated by a stored-grade update, and texts the
Specification leaves open (the `Invalid` message, the upgrade-reason wording, some English texts) are not
pinned.

## 9. Findings

- **INFO-1** The new DTO `Application.Models.Dtos.SkippedCourse` shares its simple name with the existing
  `Application.Models.SkippedCourse` (US-014). It compiles, because the namespaces differ, but a file importing
  both namespaces would be ambiguous. IMPLEMENTATION may keep the name, as the entity model says, and alias
  where needed. Renaming it would need a design change.
- **INFO-2** The page markup hooks of strategy §3.2 (`data-health-*`, `course-deletion-confirmation`) are a test
  contract that IMPLEMENTATION's views must follow. The design left the markup open.

## 10. Notes for IMPLEMENTATION

- The banner failure log line must name the exception type and nothing of its message. The test's failing
  source puts a course-like marker in the message, and the line must not contain it.
- The 409 read-only page is expected to carry the existing `ReadOnly.Refused.*` texts
  (`SignInTestData.TextKeys.Refused*`), as for US-032.
- Banner plural rule: strategy §3.1.

## 11. Delegation

Kept inline: the strategy, the matrix, the skeleton, all test code (scenarios depend on the problem rules) and
the red-phase verdict. Delegated to a `cheap-worker` (Sonnet): the build and the 17 test runs with their
failure summary.

## 12. Open Decisions

None.
