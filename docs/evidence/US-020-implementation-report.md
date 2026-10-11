---
artifact_type: implementation_report
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T17:30:00Z
updated_at: 2026-10-11T17:30:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-020-course-list.md
    version: null
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 2
  - path: docs/designs/api/US-020-api-design.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
  - path: docs/designs/database/US-020-entity-model.md
    version: 1
  - path: docs/tests/US-020-test-strategy.md
    version: 1
  - path: docs/tests/US-020-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-020 Implementation Report — Course list with filters and search

## 1. Summary

The course list `GET /workspace/courses` is implemented for Admin and Dean: the
filter form (status, teacher, search), one page of 50 courses in Ukrainian
alphabetical order with name and section, status, current teachers, current
student count and the Google update time in the school's zone, links into the
journal and the Meet reports, paging that keeps the filters, the two empty
states, the 400/404 answers with the page itself, and "Courses" as the first
link of the home page's Google Workspace section. The page only reads; it
works unchanged in read-only mode. The OD-012 skeleton is completed.
Security-sensitive: a new protected endpoint and policy, user input in the query
string, personal data (teacher names and emails) rendered.

Validation: build 0 warnings / 0 errors; `dotnet format --verify-no-changes`
clean; every US-020 test green. **No full-suite run was green in one pass on
this machine**: the last two full runs failed only with
`NpgsqlException: Timeout during reading attempt` in unrelated classes (57,
then 3), and every one of those classes passed on a targeted re-run (§5). This
is the known Testcontainers flakiness (US-033 security note INFO-3), not a
US-020 effect; SECURITY_REVIEW or the human may want one more full run.

## 2. Source Artifacts

As in `inputs`: Story; Specification v1 (APPROVED); Open Decisions v2 (OD-001 …
OD-012 resolved); API design and OpenAPI v1; database design and entity model
v1; test strategy and AC test matrix v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (see `US-020-ac-test-matrix.md`) | Status |
|---|---|---|---|
| AC-001 | `GetCourseListQuery.ExecuteAsync` (rows), `CourseListSource.GetCoursePageAsync/GetCurrentTeachersAsync/GetStudentCountsAsync`, `Views/Courses/Index.cshtml` | `CourseListQueryTests.TheRow_*`, `CourseListSourceTests.*Record*`, `CourseListPageTests.TheRow_ShowsEveryColumn` | green |
| AC-002, AC-003 | `GetCourseListQuery.ReadStatus`, `StateOf`; `CourseListSource.Matching` | `CourseListRequestValidationTests`, `CourseListQueryTests`, `CourseListSourceTests.TheStateFilter_*`, page tests | green |
| AC-004 | `CourseListSource.GetTeacherChoiceAsync`, teacher filter in `Matching`; 404 check in the query | `*TeacherChoice*`, `*TeacherFilter*`, `AFormerTeachersId_Is404` | green |
| AC-005 | `GetCourseListQuery.NameOf`, `Choice` (I-3); view `PersonText`, `TeacherLabel` | `CourseListPersonNameTests`, page tests | green |
| AC-006, AC-019 | `Matching` search under `course_name_uk` (`lower(... COLLATE ...)` + `strpos`), order in `GetCoursePageAsync` | `CourseListSourceTests.TheSearch_*`, `ThePage_IsInUkrainianOrder_*`, `CourseListSchemaTests` | green |
| AC-007 | `Matching` (AND) | `*Filters_Combine*` | green |
| AC-008, AC-020 | paging and clamping in the query; `Skip/Take` in the source; `CourseListUrl.Build`; paging nav | `CourseListQueryTests.Page*`, `TheNumberOfReads_*`, `CourseListSourceTests.EveryRead_IsOneDatabaseCommand`, `CourseListUrlTests`, `Paging_*` | green |
| AC-009 | view links from `SignInRoutes.Journal` / `MeetReports` + `CourseIdParameter` | `EachRow_LinksToTheJournalAndTheMeetReports_AndNotToClassroom` | green |
| AC-010 | `CourseListPageModel.ReturnPath` from `CourseListUrl.Build` of validated values; view sets the switcher override | `*ReturnPath*`, `TheAddress_*`, `AfterARefusal_TheSwitcherDropsTheRejectedValue` | green |
| AC-011 | empty states in the query; view `course-list-empty` | `NoCourseStored_*`, `NothingMatching_*` | green |
| AC-012 | `Views/Home/Index.cshtml` first link | `Courses_IsTheFirstLinkOfTheWorkspaceSection` | green |
| AC-013 | `ReadStatus/ReadTeacher/ReadSearch/ReadPage`; `CoursesController` 400/404; `CourseListLog.Refused` | `CourseListRequestValidationTests`, page and logging tests | green |
| AC-014, AC-017 | query takes only `ICourseListSource` + `SchoolTimeZone`; source untracked, no write | `CourseListReadOnlyTests`, `Reading_WritesNothing`, `TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly` | green |
| AC-015 | `InstallationPolicies.ViewCourses` registered in `InstallationSecurityServices`; `[Authorize(Policy = ViewCourses)]` | `CourseListAuthorizationTests` | green |
| AC-016 | `CoursesTextKeys`, 34 `Courses.*` keys in both resx files | `CourseListTranslationTests` | green |
| AC-018 | `GetCourseListQuery.LocalTime` | `TheUpdateTime_IsShownInTheSchoolsTimeZone_*`, `TheRow_ShowsEveryColumn` | green |

## 4. Change Set

Production (completing the OD-012 skeleton committed in `7355eed`):

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/UseCases/GetCourseListQuery.cs` | FR-001 … FR-012, FR-015; VR-001 … VR-004; api-design §2.4–§2.9 |
| `src/ClassroomAgent.Application/UseCases/CourseListUrl.cs` | openapi `CourseListUrl`; FR-009, S-05, S-06 (adds `Token`) |
| `src/ClassroomAgent.Infrastructure/Persistence/CourseListSource.cs` | entity model §2; db-design §3 Q1–Q6 |
| `src/ClassroomAgent.Infrastructure/Persistence/ClassroomAgentDbContext.cs` | db-design §2.1 `HasCollation` (+ constant `CourseListCollation`) |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseMembershipConfiguration.cs` | db-design §4.1 index |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261011145820_AddCourseListSupport.cs`, `.Designer.cs`, `ClassroomAgentDbContextModelSnapshot.cs` | db-design §5 migration (PC-2) |
| `src/ClassroomAgent.Web/Controllers/CoursesController.cs` | openapi `getCourseList`; FR-014, FR-018 |
| `src/ClassroomAgent.Web/Security/CourseListLog.cs` | FR-018 (event ids 5460, 5461) |
| `src/ClassroomAgent.Web/Security/CoursesTextKeys.cs` | FR-017 |
| `src/ClassroomAgent.Web/Views/Courses/Index.cshtml` | FR-003 … FR-011; test strategy §4 markup |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | FR-013, OD-010 |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | FR-014 policy registration |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | supporting: DI registration |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-017, test strategy §6 (34 keys) |

Tests (modified — see §7):

| File | Trace |
|---|---|
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/CourseListSourceTests.cs` | AC-006 fixture correction |
| `tests/ClassroomAgent.Tests/Web/Pages/LandingPageTests.cs` | FR-013 supporting update |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | db-design §5 supporting update (migration registry) |

No secret, generated database file or IDE-local config is in the change set.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "…CourseList…\|CourseMembershipSchemaTests\|LandingPageTests\|PendingModel\|InstallationDatabaseTests"` | 243 total, 241 passed, 2 failed (the two test defects of §7) |
| `dotnet test ClassroomAgent.sln --no-build` (2nd run) | 5 093 total, 5 092 passed, 1 failed (`AppUserMigrationTests` migration count, §7) |
| `dotnet test … --filter "AppUserMigrationTests\|LandingPageTests\|CourseListSourceTests"` | 32 total, 32 passed |
| `dotnet test ClassroomAgent.sln --no-build` (3rd run) | 5 093 total, 5 036 passed, 57 failed — all `NpgsqlException` / `TimeoutException` in 12 unrelated classes; 21 min |
| re-run of those 12 classes | 282 total, 282 passed |
| `dotnet test ClassroomAgent.sln --no-build` (4th run) | 5 093 total, 5 090 passed, 3 failed — `StatusPushLoggingTests`, `SynchronizationRequestAuthorizationTests`, `GoogleSignInCallbackTests`, each `Timeout during reading attempt` after 3 min 44 s; 0 skipped |
| re-run of those 3 classes | 33 total, 33 passed |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 (twice) |

Delegated: the view, `CoursesTextKeys`, the 34 translations in both files and
the home-page link went to a Sonnet worker against a fixed markup brief and were
reviewed here. Build and test runs were background shell jobs rather than a
subagent (they need no judgement and report verbatim). Business rules, the
query, the source, the persistence model, the controller and the log lines were
written inline.

## 6. Configuration Changes

None. The migration requires a PostgreSQL server built with ICU (db-design
§2.3) — the official `postgres` images are; noted for the deployment runbook.

## 7. Deviations and Discovered Problems

- **Fixture defect in a TEST_WRITING test.** `CourseListSourceTests.TheSearch_IsACaseInsensitiveSubstringOfTheNameOrTheSection`
  seeded «Єдність», which really contains «іст» (є-д-н-**іст**-ь), so the correct
  implementation also returns it for «ІСТ». The fixture became «Єва» (searches
  «єВ» / negative «ев»); the rule tested — Ukrainian case folding, no є/е or і/и
  folding — is unchanged. Not a weakening.
- **US-008 landing test.** `LandingPageTests.ThePage_ShowsNoIdentityFieldAndNoTeachingData`
  forbade the substring "course" anywhere in the home page; the approved route
  `/workspace/courses` (api-design §2.1) in the required first link (FR-013)
  now carries it. Only that `href` is set aside before the check; the "no
  teaching data" assertions stay.
- **Migration registry.** `AppUserMigrationTests` counts migrations (16 → 17,
  last `_AddCourseListSupport`), as every Story with a migration updates it.
- The first delegated translation run saw HTTP 500 on the page: the model
  already declared the collation while the migration did not exist yet (written
  in parallel). Resolved by the migration; not a code defect.

## 8. Open Decisions

None touched or newly required. OD-012 (skeleton) completed as resolved.
For SECURITY_REVIEW: `q` (free text) travels in the query string by design
(api-design §2.6, US-039 INFO-1); it is never logged.
