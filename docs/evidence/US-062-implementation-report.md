---
artifact_type: implementation_report
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T21:20:41Z
updated_at: 2026-10-27T21:20:41Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
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
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-062 Implementation Report — data-health banner, «Стан даних», confirmation of deleted courses

## 1. Summary

Implemented: the problems of spec FR-001 computed from stored data in one Application assessor shared by the banner and
the page (`DataHealthRules`); the banner in the installation layout (FR-003, OD-001 a, I-4); the role-shaped page
`GET /workspace/data-health` (FR-002, FR-004, FR-005, OD-003, OD-004 a); the Admin's confirmation
`POST /workspace/data-health/course-deletion-confirmation` with the read-only guard, VR-001, the `FOR UPDATE` transaction,
VR-002 and the audit rows (FR-006, api-design §2.4–§2.6, m-1 accepted); the empty-list date and the migration
`AddDataHealth` with its backfill (FR-008, OD-002 a); the two new policies; translations in both languages.

Status: every AC-001 … AC-017 implemented. Build 0/0, full suite green (§5), format clean. Security-sensitive: a new
Admin-only write with audit, a new role-shaped page showing student names and grades, a new layout element.

Limitations: none beyond the test strategy §6–§7. Five pre-existing tests and three new tests needed corrections that
weaken nothing (§7, D-1 … D-4).

## 2. Source Artifacts

As the front matter `inputs`. Open Decisions OD-001 … OD-005, all resolved a); `trebovaniya.md` v103 behind the
Specification.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class) | Status |
|---|---|---|---|
| AC-001 | `DataHealthRules.AssessAsync`, `GetDataHealthCountsQuery`, `DataHealthBanner`, `_Layout.cshtml` | `DataHealthQueryTests`, `DataHealthBannerTests` | green |
| AC-002 | counts query returns null; layout renders nothing; page `NoProblems` line | `DataHealthQueryTests`, `DataHealthBannerTests`, `DataHealthPageTests` | green |
| AC-003 | `Assessment.TeachingCount` skips archived; `GetDataHealthQuery` archived list; view section | same three | green |
| AC-004 | current-state computation, nothing stored (FR-001.4) | `DataHealthQueryTests` | green |
| AC-005 | `CourseEntry`, `DataHealthSource` detail queries, `Views/DataHealth/Index.cshtml` | `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-006 | X1/X3 in `DataHealthRules`; `GetDataHealthQuery` nulls `Details` for the Dean; view diagnosis mapping | `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-007 | X2 (strictly > 24 h, not in read-only mode) | `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-008 | `SyncState.RecordCourseListRead` date; X4 | `DataHealthInvariantTests`, `CourseListEmptySinceTests`, `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-009 | X5 once, Admin list + form, Dean title + H3 | `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-010 | `ConfirmCoursesDeletedUseCase`, `Course.ConfirmDeletedInClassroom`, `AuditEvent.CoursesConfirmedDeleted`, `DataHealthController` PRG | `ConfirmCoursesDeletedUseCaseTests`, `CourseDeletionConfirmationHttpTests`, `DataHealthInvariantTests` | green |
| AC-011 | policy `ConfirmCoursesDeleted` (403); read-only guard first + refused audit row (409) | same | green |
| AC-012 | translations uk/en; banner and page in read-only mode | `DataHealthBannerTests`, `DataHealthPageTests` | green |
| AC-013 | X6 with both dates; H3 / H5 by role | `DataHealthQueryTests`, `DataHealthBannerTests`, `DataHealthPageTests` | green |
| AC-014 | X6 and X2 not raised in read-only mode; `ReadOnlyNotice`; upgrade reason in Admin details | `DataHealthQueryTests`, `DataHealthPageTests` | green |
| AC-015 | `DataHealthBanner.ForAsync` skips `/sign-in/change-password` | `DataHealthBannerTests` | green |
| AC-016 | migration backfill + CHECK; entity date logic | `DataHealthPersistenceTests`, `DataHealthInvariantTests`, `CourseListEmptySinceTests` | green |
| AC-017 | all-or-nothing transaction, no audit on stale | `ConfirmCoursesDeletedUseCaseTests`, `CourseDeletionConfirmationHttpTests` | green |

Method-level mapping is the `ac_test_matrix`; every row of it ran green (§5).

## 4. Change Set

Production — Domain:

- `Domain/Entities/SyncState.cs` — `CourseListEmptySince`, `RecordCourseListRead` sets/keeps/clears it (FR-008.1, entity model §1.1).
- `Domain/Entities/Course.cs` — `ConfirmDeletedInClassroom` (FR-006.2 step 4, entity model §1.2).
- `Domain/Entities/AuditEvent.cs` — `ConfirmedCourseIds`, `MaxConfirmedCourseIds`, the two factories (FR-006.2 step 5, api-design §2.5, entity model §1.4).
- `Domain/Enums/AuditAction.cs` — `CoursesConfirmedDeleted` (entity model §1.3; skeleton).

Production — Application:

- `Ports/IDataHealthSource.cs`, `Ports/ICourseRepository.cs` — entity model §2.1 (skeleton signatures).
- `Models/CourseHealthFact.cs`, `AboveMaximumGradeRow.cs`, `ItemNotInClassroomRow.cs`, `CourseDeletionConfirmationOutcome.cs`, `CourseDeletionConfirmationResult.cs`, `Models/Requests/CourseDeletionConfirmationForm.cs`, `Models/Dtos/*` (13 US-062 DTOs/enums) — entity model §2.2–§2.5 (skeleton, unchanged).
- `UseCases/DataHealthRules.cs` — new: the shared assessor of FR-001 and the counting of FR-001.5 (entity model §2.4 "one shared internal assessor").
- `UseCases/GetDataHealthCountsQuery.cs`, `UseCases/GetDataHealthQuery.cs` — FR-002, FR-003, FR-004, api-design §2.8.
- `UseCases/ConfirmCoursesDeletedUseCase.cs` — FR-006, VR-001, VR-002, api-design §2.4–§2.5.
- `Authorization/InstallationPolicies.cs` — two policy names (api-design §2.2; skeleton).
- `Localization/SharedResource.uk.resx`, `.en.resx` — 45 `DataHealth.*` keys each (FR-007, OD-003, NFR-073).

Production — Infrastructure:

- `Persistence/DataHealthSource.cs` — new: db-design §5.1, §5.3.
- `Persistence/Repositories/CourseRepository.cs` — `GetForAbsenceConfirmationAsync` with `FOR UPDATE` (db-design §4).
- `Persistence/Configurations/SyncStateConfiguration.cs`, `AuditEventConfiguration.cs`, `CourseWorkConfiguration.cs`, `SubmissionConfiguration.cs` — columns, CHECKs, action code, partial indexes (db-design §2, §3, §5.2); the skeleton's `Ignore` calls removed.
- `Persistence/Migrations/20261027202639_AddDataHealth.cs` (+ `.Designer.cs`, snapshot) — db-design §7, backfill `UPDATE` hand-inserted between the column and its CHECK.

Production — Web:

- `Controllers/DataHealthController.cs` — new: both routes (openapi, api-design §2.6).
- `Views/DataHealth/Index.cshtml` — new: the page; markup hooks of test strategy §3.2.
- `Views/Shared/_Layout.cshtml` — the banner in the header (openapi `x-layout-changes`).
- `Security/DataHealthBanner.cs` — new: the layout's banner source (api-design §2.3).
- `Security/DataHealthTextKeys.cs`, `Security/DataHealthLog.cs` — new: keys and log lines (api-design §3, §5; SC-10).
- `Security/SignInRoutes.cs` — two routes and the TempData key (api-design §2.1).
- `Security/InstallationSecurityServices.cs` — the two policies (api-design §2.2).
- `Configuration/InstallationServices.cs` — DI registration (supporting).
- `wwwroot/css/site.css` — the banner's strip (FR-003.3).

Tests (all in `tests/ClassroomAgent.Tests/`): the TEST_WRITING files listed in the test generation report, plus the
corrections of §7 in `DataHealthPersistenceTests.cs`, `CourseDeletionConfirmationHttpTests.cs`,
`AppUserMigrationTests.cs`, `PermissionMatrixTests.cs` (whitespace), `Web/Persistence/AccessCheckAuditSchemaTests.cs`,
`Web/Persistence/DeanAccountAuditSchemaTests.cs`.

No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Story classes (12) | `dotnet test --no-build --filter FullyQualifiedName~<Class>` one per run | first run 249/252 (3 test defects, §7 D-1…D-3); after fixes all green: Persistence 47/47, ConfirmationHttp 12/12, AppUserMigration 6/6; others green on the first run (Invariant 38, EmptySince 3, Query 35, UseCase 21, Banner 25, Page 26, SyncStateSchema 11, AuditSchema 29, PermissionMatrix 3) |
| Full suite 1 | `dotnet test ClassroomAgent.sln --no-build` | 6619 total, 6614 passed, 5 failed: 2 schema tests not adapted (D-4) and 3 Npgsql "Timeout during reading attempt" in fixture create/drop (environment) |
| Reruns | five classes one per run | AccessCheckAuditSchema 7/7, DeanAccountAuditSchema 13/13, InstallationStatusTranslation 11/11, AccessCheckTranslation 64/64, DeanAccountTranslation 41/41 |
| Full suite 2 | `dotnet test ClassroomAgent.sln --no-build` | 6619 total, 6618 passed, 0 skipped, 1 failed: `LanguageSwitcherTests.TheErrorPage_HasNoSwitcher_EvenWhenSignedIn_ButUsesTheUsersLanguage` — Npgsql connect timeout (environment) |
| Rerun | `--filter FullyQualifiedName~LanguageSwitcherTests` | 16/16 |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | first: 3 WHITESPACE errors in `PermissionMatrixTests.cs` (TEST_WRITING lines, mixed line endings); after `dotnet format whitespace --include` that file: exit 0 |

Fact-query plan (db-design §5.2): on a throwaway `postgres:17-alpine` with the migrated schema and a synthetic school of
200 courses × 30 items × 30 students (180 000 submissions, 5 above maximum, 3 items gone, 2 marked courses),
`EXPLAIN (ANALYZE, BUFFERS)` of the §5.1 shape: execution 37.5 ms, planning 3.3 ms. The above-maximum probes are
`Index Only Scan using ix_submission_graded`, the items-gone probes `Bitmap Index Scan on ix_course_work_not_in_classroom`
(executed once, hashed). The heap fetches were high only because the fresh table had no visibility map yet (no VACUUM).

Delegated (cheap-worker, Sonnet): the build and the 12 story-class runs; the build, 3 reruns and full suite 1; the build,
5 reruns, the format check and full suite 2. Inline: all production code, the test corrections, the migration backfill,
the compile-fix builds and the migration generation (they feed the next edit directly), the `LanguageSwitcherTests`
rerun, the EXPLAIN and the whitespace fix.

## 6. Configuration Changes

None. No `appsettings` key, no new package (Npgsql already maps `long[]` and `INCLUDE` indexes).

## 7. Deviations and Discovered Problems

- **D-1 (test defect)** `DataHealthPersistenceTests.TheDate_IsStoredAndReadBack` read `timestamptz` as `DateTimeOffset`;
  Npgsql returns a UTC `DateTime` (`InvalidCastException`). Now read as `DateTime` and compared as UTC, as
  `MeetSessionRepositoryTests` does. Same assertion.
- **D-2 (test defect)** `CourseDeletionConfirmationHttpTests.AStaleList_Is409…` asserted an empty audit table, but the
  Admin's sign-in writes `admin_sign_in` first. Now asserts the audit rows are unchanged by the post — stronger than
  filtering by action.
- **D-3 (test defect)** `AppUserMigrationTests` asserted index 23 ends `_AddDataHealth` but kept the count at 23; count
  set to 24.
- **D-4 (adaptation missed by TEST_WRITING)** `AccessCheckAuditSchemaTests.TheAuditTable_GainsNoColumn` and
  `DeanAccountAuditSchemaTests.TheTable_GainsNoColumn` pin the exact `audit_event` columns; `confirmed_course_ids` added
  (37 columns), as US-038 did for its columns. A requirement change of this Story (db-design §3.1).
- **D-5 (format)** whitespace of TEST_WRITING's lines in `PermissionMatrixTests.cs` fixed by `dotnet format`.
- **D-6 (presentation detail)** A student with no name and no email gets `StudentName = ""` from the read port (the DTO
  member is non-nullable per the entity model); the page words it with the journal's existing `Journal.Row.Unnamed`
  text. T1 rows reuse US-060's `DataMarks.Note.Points` / `Note.Draft` texts («Бали: 12 з 10»).
- **D-7 (presentation detail)** Texts the Specification does not fix were written here: section headings, the
  `Invalid` message («Виберіть від 1 до 1000 курсів зі списку»), the upgrade reason, the English «There are no data
  problems» and «Archived courses (not counted)», and the labels of the Admin's details. None is pinned by a test.
- **D-8 (Dean, archived X3 with teaching problems)** OD-004 a: for the Dean an archived course with both X3 and a teaching
  problem yields two archived entries — X3 without a name, and the teaching problems with the name (teaching data is
  visible to the Dean). For the Admin it is one entry. Not covered by a test; the case needs both an unknown state and
  stored teaching data on an archived course.
- **INFO-1 (TEST_WRITING)** confirmed in practice: `_ViewImports` imports `Application.Models`, so the view hit CS0104 on
  `SkippedCourse`; resolved with a `using` alias in the view and in `DataHealthRules`. No rename.
- **INFO-2 (TEST_WRITING)** the view follows strategy §3.2 hook by hook; the banner sits in the header, before `<main>`,
  so the page-search hint stays the first element after the title.

## 8. Open Decisions

None touched or required. No security-sensitive decision was made here: the refused audit row follows api-design §2.5
(m-1 accepted by the Owner).

## Result

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-062
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-062-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "D-1…D-5: three test defects, two schema tests not adapted and a whitespace issue from TEST_WRITING corrected without weakening an assertion"
    - "D-8: the Dean's archived X3 with teaching problems is split into two entries; untested"
  closed_findings:
    - {id: US-062/TEST_WRITING/INFO-1, resolution: FIXED, note: "Name kept per the entity model; the ambiguity resolved by using aliases in the view and DataHealthRules", ref: "docs/evidence/US-062-implementation-report.md §7"}
    - {id: US-062/TEST_WRITING/INFO-2, resolution: FIXED, note: "View follows strategy §3.2 hooks; page and banner tests green", ref: "docs/evidence/US-062-implementation-report.md §7"}
```
