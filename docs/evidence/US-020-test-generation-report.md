---
artifact_type: test_generation_report
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T16:30:00Z
updated_at: 2026-10-11T16:30:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
  - path: docs/designs/database/US-020-entity-model.md
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 2
  - path: docs/tests/US-020-test-strategy.md
    version: 1
  - path: docs/tests/US-020-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-020 Test Generation Report

## 1. Result

**PASS — red phase verified.** The build has 0 warnings and 0 errors. The full
suite (`dotnet test ClassroomAgent.sln --no-build`, Docker running) ran **5 093
tests: 4 896 passed, 197 failed, 0 skipped**, in 5 min 53 s. All 197 failures
are US-020 tests (196 new cases and the modified
`CourseMembershipSchemaTests.TheRequiredIndexes_Exist`), failing because the
feature is missing (§4). **No existing test regressed.**

## 2. Production skeleton (OD-012 a, Owner 2026-10-11)

The skeleton only compiles. Nothing is registered in DI, there is no
migration, and no existing behaviour changed.

- `Application/Models/Requests/CourseListRequest.cs`
- `Application/Models/` — `CourseListFilter`, `CourseListPersonRecord`,
  `CourseListCourseRecord`, `CourseListTeacherRecord`,
  `CourseListStudentCountRecord`, `CourseListPageOutcome`, `CourseListPageResult`
- `Application/Models/Dtos/` — `CourseStatusFilter`, `CourseListPageModel`,
  `TeacherOption`, `CourseListPersonName`, `CourseListPersonNameKind`,
  `CourseListResult`, `CourseListEmptyState`, `CourseListRow`,
  `CourseListMessageKey`
- `Application/Ports/ICourseListSource.cs` (entity model §2, verbatim)
- `Application/UseCases/GetCourseListQuery.cs`, `CourseListUrl.cs` (both throw)
- `Infrastructure/Persistence/CourseListSource.cs` (throws)
- Edits: `InstallationPolicies.ViewCourses` (constant only, not registered);
  `SignInRoutes.Courses = "/workspace/courses"`.

**Signatures binding on IMPLEMENTATION**, beyond the entity model:

- `GetCourseListQuery(ICourseListSource, SchoolTimeZone)`, with
  `ExecuteAsync(CourseListRequest, CancellationToken)` and `PageSize = 50`;
- `CourseListUrl.Path` and `CourseListUrl.Build(CourseStatusFilter, long?, string?, int)`
  — the openapi `CourseListUrl` rule; `q` via `Uri.EscapeDataString`
  (upper-case hex, space as `%20`);
- `CourseListSource(ClassroomAgentDbContext)`;
- `CourseListRow.UpdatedAt` is a local `DateTime?` in the school's zone;
  `CourseListRow.State` is the Domain `CourseState`;
- openapi `PersonName` is `CourseListPersonName(Kind, Text)` (name collision
  with US-027's `PersonName`, recorded in OD-012).
- The markup hooks of test strategy §4 and the 34 translation keys of
  `CourseListTestData.Keys.All`.

## 3. Files

Created (tests): `Application/UseCases/CourseListRequestValidationTests.cs`,
`CourseListQueryTests.cs`, `CourseListPersonNameTests.cs`, `CourseListUrlTests.cs`;
`Infrastructure/Persistence/CourseListSourceTests.cs`, `CourseListSchemaTests.cs`;
`Web/Pages/CourseListPageTests.cs`; `Web/Security/CourseListAuthorizationTests.cs`;
`Web/Localization/CourseListTranslationTests.cs`; `Web/Logging/CourseListLoggingTests.cs`;
`Web/UseCases/CourseListReadOnlyTests.cs`; `TestInfrastructure/CourseListTestData.cs`,
`FakeCourseListSource.cs`, `CourseListHostExtensions.cs` (all under
`tests/ClassroomAgent.Tests/`).

Modified (tests): `TestInfrastructure/CourseTestData.cs` (constant
`MembershipCurrentTeacher`); `Web/Persistence/CourseMembershipSchemaTests.cs`
(`TheRequiredIndexes_Exist` now expects `ix_course_membership_current_teacher`,
db-design §4.1 — it fails until the migration exists).

Created / modified (production skeleton): §2.

Delegated: the four classes `CourseListAuthorizationTests`,
`CourseListTranslationTests`, `CourseListLoggingTests` and
`CourseListReadOnlyTests` were written by a Sonnet worker against a fixed
brief and reviewed here. The design, the skeleton, the unit, integration,
schema and page tests and the red-phase verdict were done inline.

## 4. Execution

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~CourseList\|FullyQualifiedName~CourseMembershipSchemaTests"` | total 216, failed 196, passed 20 (47 s) |
| `dotnet test ClassroomAgent.sln --no-build` (Docker running) | total 5 093, failed 197, passed 4 896, skipped 0; 5 m 53 s — every failure in a US-020 class |

**Why the 196 fail** — all for missing production behaviour: 139
`NotImplementedException` from the skeleton (unit and source tests); 31 host
tests get `404` because the route does not exist; the schema tests find no
migration, collation or index (`42704: collation "course_name_uk" does not
exist`, empty sequences); translation tests find no `Courses.*` key; the
home-page test finds Journal as the first link; logging tests time out waiting
for the page's events.

**Why 20 pass** — 11 existing `CourseMembershipSchemaTests` cases (unchanged
behaviour) and these new ones, each legitimately green before implementation:

- `CourseListAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange`
  — the US-012 restricted session redirects every path; it must stay green
  once the route exists.
- `CourseListQueryTests.TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly`,
  `CourseListUrlTests.ThePath_IsTheRoute` — structural, fixed by the skeleton's
  binding signatures.
- `CourseListSchemaTests.NoColumn_UsesTheCollation` — a guard that the
  collation, once created, is applied to no column.
- `CourseListPageTests.OnePage_HasNoPagingLinks` passed vacuously on the first
  run (a `404` page has no paging nav); it was tightened to require `200`, the
  two rows and the total, and now fails for the missing route.

## 5. Untested Acceptance Criteria

None. Every AC-001 … AC-020 maps to tests (`US-020-ac-test-matrix.md`).

## 6. Open Decisions

OD-012 (compile-only skeleton) — resolved (a) by the Owner, 2026-10-11. None
open.

## 7. Notes for IMPLEMENTATION and SECURITY_REVIEW

- Keep the existing `HasPendingModelChanges` test green: the collation and the
  index go into the model and the migration together.
- `TheSearchText_IsNeverLogged` waits for any Information event (test strategy
  §8); a stricter wait is possible once the event id exists.
- `q` in the query string is flagged for SECURITY_REVIEW (api-design §2.6,
  US-039 INFO-1).
