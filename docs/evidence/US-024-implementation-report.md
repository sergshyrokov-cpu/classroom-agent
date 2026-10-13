---
artifact_type: implementation_report
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-13T14:11:27Z
updated_at: 2026-10-13T14:11:27Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-024-database-statistics.md
    version: null
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 3
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
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-024 Implementation Report — Database statistics and last synchronization

## 1. Summary

- **Capability.** The new page `GET /database` (policy `ViewDatabaseStatistics`, Admin and Dean) shows the
  eleven counters, the course total and the synchronization block. Its own press,
  `POST /database/synchronization-requests` (policy `StartSynchronization`, OD-011 a), calls the unchanged US-019
  `RequestSynchronizationUseCase` and always returns to `/database`. The home page gains the `database-section`
  after "Reports and templates".
- **Reads.** Q1 counts in one SQL statement through the new `DatabaseStatisticsReadSource`. Q2 is the existing
  `ISyncStateRepository.GetForReadAsync`. Times are converted to the school's zone in `GetDatabaseStatisticsQuery`.
- **No schema change.** No migration, no new audit action, no new package.
- **Validation.** The build has 0 warnings, the suite is green (§5) and `dotnet format --verify-no-changes` is
  clean.
- **Security-sensitive.** The Story adds a new policy, a new POST endpoint that triggers a write (the US-019 audit row
  and the request), and the role-dependent guidance.
- **Test fixture changed (§7).** One test fixture value was changed with the Owner's approval.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-024-database-statistics.md` | — |
| Specification | `docs/specifications/US-024-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-024-open-decisions.md` | 3 (OD-001 … OD-012 resolved) |
| API design | `docs/designs/api/US-024-api-design.md`, `US-024-openapi.yaml` | 1 |
| DB design | `docs/designs/database/US-024-db-design.md`, `US-024-entity-model.md` | 1 |
| Tests | `docs/tests/US-024-test-strategy.md`, `US-024-ac-test-matrix.md` | 1 |

## 3. Implemented Acceptance Criteria

The test classes are those of the `ac_test_matrix`:

- **Q** — `DatabaseStatisticsQueryTests`
- **S** — `DatabaseStatisticsReadSourceTests`
- **P** — `DatabaseStatisticsPageTests`
- **A** — `DatabaseStatisticsAuthorizationTests`
- **R** — `DatabaseSynchronizationRequestTests`
- **RO** — `DatabaseStatisticsReadOnlyTests`
- **T** — `DatabaseStatisticsTranslationTests`
- **L** — `DatabaseStatisticsLoggingTests`

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `Views/Home/Index.cshtml` `nav#database-section` | P `TheHomePage_HasTheDatabaseSection_AfterTheReportTemplates` | pass |
| AC-002 | `DatabaseStatisticsReadSource.GetCountsAsync` (five state sub-queries); `GetDatabaseStatisticsQuery` (`CourseCounts`, total = sum) | Q `TheCourseCounters_…`, `AStateWithNoCourse_…`; S `EveryCounter_HasItsSeededValue`; P `TheSeededSchool_ShowsEveryCounter` | pass |
| AC-003 | Q1 `teachers` / `students` (distinct `participant_id`, `on_roster`) | S `TheFourPeopleOfAc003_…`, `TeachersOfAnyState_…` | pass |
| AC-004 | Q1 `course_work_items`, `submissions`, `meet_meetings`, `meet_codes_awaiting_decision` | S `EveryCounter_HasItsSeededValue`; Q `TheOtherCounters_ArePassedThrough_…` | pass |
| AC-005 | `GetDatabaseStatisticsQuery` state mapping; `Views/Database/Index.cshtml` `data-sync-line` | Q `TheState_IsMappedFromTheStoredStatus`, `TheBlock_IsTheSameForBothRoles`, `TheDtos_HaveNoField…`; P `ACompletedRun_…`, `TheSynchronizationBlock_IsTheSame_…` | pass |
| AC-006 | No `SyncState` row → `NotYet`, null times; view renders `Database.Sync.NotYet` | Q `WithNoRow_…`; S `AnEmptySchool_CountsZeroEverywhere`; P `AnEmptySchool_NeverSynchronized_…` | pass |
| AC-007 | `FailureGuidance` by role in the query; the view shows `#database-failure-hint` or `#database-connection-link` | Q `AFailedRun_…`, `AFirstRunThatFailed_…`; P `AFailedRun_ShowsFailed_…`, `ARunningOrCompletedRun_ShowsNoFailureGuidance` | pass |
| AC-008 | `DatabaseController.RequestSynchronization` → `RequestSynchronizationUseCase`, `302 /database` with TempData | R `AnAcceptedPress_…`, `APressDuringOtherWork_…`, `ASubmittedReturnUrl_IsIgnored_…`, `TheUs019Endpoint_StillReturns…` | pass |
| AC-009 | Query has no guard and no write; the press goes through the US-019 guard; the form is always rendered | RO both; R `InReadOnlyMode_…`, `ReadOnlyAndUnconfigured_…`; P `ThePage_CarriesTheSynchronizationForm_InEveryMode`; Q `OnePage_IsOneReadOfEach_…`, `TheQuery_DependsOnReadPortsOnly` | pass |
| AC-010 | Policies `ViewDatabaseStatistics` / `StartSynchronization` per action; global antiforgery | A all seven | pass |
| AC-011 | 20 new keys in `SharedResource.uk/en.resx`; `DatabaseTextKeys` | T all three | pass |
| AC-012 | No Google port in the query or controller | RO `…AndCallsNoGoogle`; P `TheSeededSchool_ShowsNoPersonalData` | pass |
| AC-013 | The DTO has no diagnosis or step field; the view renders none | P `AFailedRun_ShowsFailed_…_AndNoCause`; Q `TheDtos_HaveNoField…` | pass |
| AC-014 | `GetDatabaseStatisticsQuery.Local` (`TimeZoneInfo.ConvertTime`); the view formats short date + `HH:mm` | Q `TheThreeTimes_AreInTheSchoolsZone`, `InSummer_…`; P `ACompletedRun_…` | pass |
| AC-015 | Q1 `count(DISTINCT participant_id)`, no course-state join | S `APersonInSeveralCourses_…` | pass |
| AC-016 | `ConnectionNotUsable` → `409`, `View(Index)` with the role's key | R `WithoutAUsableConnection_…` | pass |
| AC-017 | Q1 is one statement and Q2 one read | S `ThePage_IsAFixedNumberOfCommands_…`, `TheSizedSchool_IsCountedInFull` | pass |
| AC-018 | Q1's last sub-query mirrors `MeetCodesReadSource.GetCountsAsync` Unassigned | S `TheCodesAwaitingDecision_Equal…`, `OnceEveryCodeIsDecided_…` | pass |
| AC-019 | Nothing is bound; the language switcher's return path is fixed to `/database` | P `AnyQueryString_IsIgnored_AndEchoedNowhere`; L `AnIgnoredParameter_IsNeverLogged` | pass |

Supporting tests also pass: L `OpeningThePage_…`, `AnAcceptedPress_…`; S `Reading_WritesNothing`; Q
`AnUnknownStatus_Throws`.

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Application/Models/DatabaseCounts.cs` | skeleton remark removed | entity model §2; OD-012 |
| `src/ClassroomAgent.Application/Models/Dtos/CourseCounts.cs`, `DatabaseStatisticsView.cs`, `FailureGuidance.cs`, `SynchronizationBlock.cs`, `SynchronizationDisplayState.cs` | skeleton remark removed | entity model §4; OD-012 |
| `src/ClassroomAgent.Application/Ports/IDatabaseStatisticsSource.cs` | skeleton remark removed | entity model §2; OD-012 |
| `src/ClassroomAgent.Application/UseCases/GetDatabaseStatisticsQuery.cs` | implemented | FR-001 … FR-008; entity model §3; AC-002, -005 … -007, -009, -013, -014 |
| `src/ClassroomAgent.Infrastructure/Persistence/DatabaseStatisticsReadSource.cs` | implemented (Q1) | db-design §3.1; entity model §6; AC-002 … -004, -015, -017, -018 |
| `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` | `ViewDatabaseStatistics` | FR-010; api-design §2.5 |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | policy registration (Admin, Dean) | FR-010; api-design §2.5 |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | `Database`, `DatabaseSynchronizationRequests` | api-design §2.1 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI: port + query (scoped) | entity model §6 (supporting) |
| `src/ClassroomAgent.Web/Controllers/DatabaseController.cs` | new: GET page, POST press | api-design §2.3, §3; FR-001, FR-007; AC-008, -010, -016, -019 |
| `src/ClassroomAgent.Web/Controllers/DatabaseStatisticsPageModel.cs` | new view model | entity model §5 |
| `src/ClassroomAgent.Web/Security/DatabaseTextKeys.cs` | new: translation keys | FR-012; strategy §5 |
| `src/ClassroomAgent.Web/Security/DatabaseStatisticsLog.cs` | new: `DatabaseStatisticsShown` (5466) | FR-013 |
| `src/ClassroomAgent.Web/Views/Database/Index.cshtml` | new page | FR-002 … FR-007; strategy §4 markup hooks |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | `database-section` | FR-009; AC-001 |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | 20 keys each | FR-012; AC-011 (supporting: translation entries) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/DatabaseStatisticsHostExtensions.cs` | marker `"Архів"` → `"Архів "` with a remark | §7 D-1 (Owner-approved) |

The other test files and the test-stage docs were produced at TEST_WRITING and are unchanged here. No secret, no
generated database file and no IDE-local config is in the change set. No migration was added.

## 5. Validation Evidence

All commands were run from the repository root, with Docker up (server 29.8.0).

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` (first) | exit 1: CS0246 `SchoolTimeZone` — a `using ClassroomAgent.Application.Models` was dropped in the query; restored |
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet test --filter "FullyQualifiedName~DatabaseStatistics\|FullyQualifiedName~DatabaseSynchronizationRequest"` | 86 total, 85 passed, 1 failed: `DatabaseStatisticsPageTests.TheSeededSchool_ShowsNoPersonalData` (`"Архів"` found in `Архівний`) → D-1 |
| `dotnet test ClassroomAgent.sln` (before D-1) | exit 2; 5497 total, 5495 passed, 2 failed: the D-1 test, and `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` (`Assert.NotNull` at line 103) |
| `dotnet test --filter "FullyQualifiedName~DatabaseStatisticsPageTests"` (after D-1) | exit 0; 22/22 |
| `dotnet test --filter "FullyQualifiedName~MeetingCodeLinkRepositoryTests"` ×3 | exit 0 each; 10/10 each — the earlier failure did not reproduce (§7 D-2) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no files |
| `dotnet build ClassroomAgent.sln` + `dotnet test ClassroomAgent.sln` (after D-1) | build exit 0, 0 warnings; test exit 2 — 5497 total, 5486 passed, 11 failed, none in a US-024 class: every failure was an Npgsql read timeout or a wait-for-event timeout in other Stories' tests, clustered in one ~6-minute stall of the container (D-3) |
| `dotnet test ClassroomAgent.sln` (final) | exit 0; **5497 total, 5497 passed, 0 failed, 0 skipped** (9 m 00 s) |

Delegated work:

- **cheap-worker:**
  - the 20 translation entries in both resx files;
  - every build and test run above;
  - the format check.
- **Inline:** the query, the read source, the controller, the view, the policy, the routes, DI, the D-1 decision
  and this report.

## 6. Configuration Changes

None. No `appsettings` key, no environment variable and no package was added.

## 7. Deviations and Discovered Problems

- **D-1 — test fixture contradicted the specification.** The sensitive marker `"Архів"` in
  `DatabaseStatisticsHostExtensions.SensitiveValues` was meant for the seeded course names "Архів 1…4". It is,
  however, a prefix of the status label `Courses.Status.Archived` = "Архівний". FR-012 requires that label on the
  page, and `DatabaseStatisticsTranslationTests` asserts it. Per this Skill the test should go back to
  TEST_WRITING, but the Owner instructed in the conversation (2026-10-13) to change the marker to `"Архів "`
  (trailing space). That value still matches every seeded course name and no label. The assertion is not weakened:
  every course name remains forbidden.
- **D-2 — unrelated flaky failure.** `MeetingCodeLinkRepositoryTests.AMark_RoundTrips` (US-032) failed once in the
  full run, then passed in three isolated reruns and in the final full run (§5). US-024 changes neither
  `MeetingCodeLinkRepository` nor its tests. This is recorded as an observation and was not investigated
  further.
- **D-3 — Testcontainers stall.** One full run lost 11 tests of other Stories to Npgsql timeouts during a ~6-minute
  stall of the PostgreSQL container (cf. US-033 security INFO-3). The next full run on the same code was green (§5).
  Nothing was changed for it.
- **Two controllers for one action.** `DatabaseController` repeats the ~15 lines of the US-019 controller's press
  (call, log, `409` re-render) instead of sharing a helper. The api-design §2.3 leaves this choice to
  implementation. The US-019 controller was left untouched (spec §10: no change to the existing buttons).
- **Placement of the stored codes.** The state and role codes in Q1 are literals in the SQL, as in
  `MeetCodesReadSource`. db-design §3.1 suggests keeping them "next to the existing converters", but those are
  private to the EF configurations. Exposing them would have meant refactoring outside scope. A rename of a stored
  code would still break `EveryCounter_HasItsSeededValue`.

## 8. Open Decisions

None touched or newly required. OD-012 (compile-only skeleton) is now fully superseded by the real
implementation: no `NotImplementedException` remains in the US-024 types.
