---
artifact_type: implementation_report
story: US-015
version: 1
status: DRAFT
created_at: 2026-10-03T07:02:38Z
updated_at: 2026-10-03T07:02:38Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-015-sync-coursework-and-submissions.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/decisions/US-015-open-decisions.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/designs/database/US-015-entity-model.md
    version: 1
  - path: docs/tests/US-015-test-strategy.md
    version: 1
  - path: docs/tests/US-015-ac-test-matrix.md
    version: 1
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-015 Implementation Report — Sync coursework and submissions

## 1. Summary

The synchronization run now imports, for every course, its coursework and
materials (both Classroom resources, `PUBLISHED` only) and all its submissions,
in the course's single transaction, behind the existing read-only guard. A
course not yet in the database whose last activity is older than
`Retention:Years` is not imported at all. A submitter never seen on a roster
gets a participant from the `userId` alone and an off-roster `student`
membership. `Retention:Years` is required at startup.

**Status:** complete. Build 0 errors / 0 warnings; **2428 tests, 2428 passed,
0 failed, 0 skipped**; `dotnet format --verify-no-changes` clean.

**Security-sensitive:** yes — the Story stores grades and submission facts of
students (potentially minors), adds two Google reads under delegation, and adds
two log lines. No endpoint, page, policy, route, audit action or scope was added.

**Limitation inherited, not introduced:** Google's real response shapes are
proved only against synthetic fixtures (TC-4); `trebovaniya.md` §7 item 14 stays
open (OD-010) and does not change any line of code.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| User Story | `docs/stories/US-015-sync-coursework-and-submissions.md` | — |
| Specification | `docs/specifications/US-015-spec.md` | 2 (APPROVED) |
| Open Decisions | `docs/decisions/US-015-open-decisions.md` | 2 (OD-001 … OD-012 resolved) |
| API design | `docs/designs/api/US-015-api-design.md` | 2 (NOT_APPLICABLE, no OpenAPI) |
| Database design | `docs/designs/database/US-015-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-015-entity-model.md` | 1 |
| Test strategy | `docs/tests/US-015-test-strategy.md` | 1 |
| AC test matrix | `docs/tests/US-015-ac-test-matrix.md` | 1 |

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | `GoogleClassroomReader.ReadCourseWorkAsync` (both resources, paging, `PUBLISHED` filter, FR-008 cascade, due-date rule); `RunSynchronizationUseCase.ImportCourseWorkAsync`; `CourseWork` entity, `CourseWorkConfiguration`, migration | `CourseWorkImportTests`, `GoogleClassroomReaderTests` (`CourseWorkAndMaterials_ArePagedToTheEnd`, `OnlyPublishedItems_AreHandedOn`, `TheItemDateCascade_AndTheDueDate_AreAppliedInTheAdapter`), `CourseWorkSchemaTests`, `CourseWorkInvariantTests` | PASS |
| AC-002 | `GoogleClassroomReader.ReadSubmissionsAsync` (`courseWorkId = "-"`, paging, latest `TURNED_IN` from the history); `RunSynchronizationUseCase.ImportSubmissionsAsync`, `TryParseSubmissionState`; `Submission` entity, `SubmissionConfiguration` | `SubmissionImportTests`, `GoogleClassroomReaderTests` (`Submissions_AreReadOncePerCourse_AndPagedToTheEnd`, `TheLastTurnIn_IsTheLatestTurnedInTransition`), `SubmissionSchemaTests`, `SubmissionInvariantTests` | PASS |
| AC-003 | upsert on `(course_id, resource, google_id)` and `(course_work_id, google_id)` in the use case; unique indexes in the migration | `CourseWorkImportTests`, `SubmissionImportTests` (second-run cases), schema uniqueness tests | PASS |
| AC-004 | `RunSynchronizationUseCase.ResolveSubmitterAsync` (participant from `userId`, `CourseMembership.FirstSeen` + `NotOnRoster`) | `SubmitterWithoutRosterTests` | PASS |
| AC-005 | `CourseWork.CreationTime/UpdateTime`, `Submission.UpdateTime`, `TimestampInterceptor`, FK/cascade in the configurations | `CourseWorkSchemaTests`, `SubmissionSchemaTests` | PASS |
| AC-006 | the existing guard in `RunSynchronizationUseCase` runs before any new read; `PermittedServiceWrites` unchanged | `CourseWorkRefusalTests` | PASS |
| AC-007 | coursework and submissions inside the course's `ExecuteInTransactionAsync`; failures propagate to US-013's handler | `CourseWorkFailureTests` | PASS |
| AC-008 | `SynchronizationBackgroundService` log lines `SyncSubmissionStateUnrecognised` (5127) and `SyncCourseSkippedByAge` (5128) carry identifiers only; no audit row | `SubmissionImportLoggingTests`, `SubmissionSchemaTests.AScheduledRun_WritesNoAuditRow` | PASS |
| AC-009 | adapter reached only through `IClassroomReader`; injected transport; read-only coursework scopes | `GoogleClassroomReaderTests.TheCourseWorkToken_AsksOnlyForTheReadOnlyCourseWorkScopes`, all schema tests on Testcontainers PostgreSQL | PASS |
| AC-010 | `RunSynchronizationUseCase.IsOlderThanRetention` (new courses only, whole years, run instant); `InstallationSettingsReader.RetentionYears`; `RetentionSettings` registration | `CourseAgeRuleTests`, `RetentionConfigurationTests` | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Domain/Entities/CourseWork.cs` | skeleton bodies implemented: import, update, truncation, required id/title/date | FR-005, FR-008, VR-002, entity model §2 |
| `src/ClassroomAgent.Domain/Entities/Submission.cs` | skeleton bodies implemented: import, update, state + raw unrecognised value | FR-006, VR-003, VR-004 |
| `src/ClassroomAgent.Application/Models/SynchronizationRunOutcome.cs` | carries unrecognised submissions and courses skipped by age | FR-011, FR-017, VR-004 |
| `src/ClassroomAgent.Application/Models/UnrecognisedSubmission.cs` | new: submission id + raw state for the Warning line | VR-004, OD-005 |
| `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` | coursework/submission import, age rule, off-roster submitter, state classification | FR-001, FR-004 … FR-014 |
| `src/ClassroomAgent.Infrastructure/Google/GoogleClassroomReader.cs` | the two port members; per-call scope sets | FR-002 … FR-004, FR-008, FR-009, VR-005, S-03, S-04 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseWorkConfiguration.cs` | new: explicit mapping, keys, indexes, checks | db-design §3, PC-4, PC-7 |
| `src/ClassroomAgent.Infrastructure/Persistence/Configurations/SubmissionConfiguration.cs` | new: explicit mapping, keys, indexes, state check | db-design §4, VR-004 |
| `src/ClassroomAgent.Infrastructure/Persistence/ClassroomAgentDbContext.cs` | two `DbSet`s | db-design, FR-018 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20260929052443_AddCourseWorkAndSubmissions.cs` (+ `.Designer.cs`) | new: the one migration | FR-018, PC-2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Migrations/ClassroomAgentDbContextModelSnapshot.cs` | regenerated snapshot | PC-2 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseWorkRepository.cs` | skeleton implemented; `#pragma CS9113` removed | entity model §7, AD-7 |
| `src/ClassroomAgent.Infrastructure/Persistence/Repositories/SubmissionRepository.cs` | idem | entity model §7, AD-7 |
| `src/ClassroomAgent.Infrastructure/Persistence/TimestampInterceptor.cs` | stamps the two new entities | PC-6, db-design §3.1, §4.1 |
| `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs` | two log lines | FR-017, VR-004, I-5 |
| `src/ClassroomAgent.Web/Configuration/InstallationSettings.cs` | `RetentionYears` | FR-012 |
| `src/ClassroomAgent.Web/Configuration/InstallationSettingsReader.cs` | `RetentionYearsKey`, required and validated | FR-012, VR-008, I-2 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | real `RetentionSettings` replaces the skeleton placeholder | FR-012, supporting DI |
| `docs/architecture/persistence-conventions.md` | PC-3 parent-scoped keys; PC-13 gains `updateTime` | FR-022 |
| `docs/architecture/deployment-conventions.md` | DC-3 names `Retention:Years` | FR-012 |
| `tests/.../Infrastructure/Google/GoogleClassroomReaderTests.cs` | six adapter tests for the new reads (D-2) | VR-005, FR-008, FR-009, OD-004, S-04; AC-001, AC-002, AC-009 |
| `tests/.../TestInfrastructure/InstallationTestHost.cs` | host settings carry `Retention:Years` | FR-012 — supporting, the host refuses to start otherwise |
| `tests/.../Application/UseCases/CourseWorkInvariantTests.cs` | no-date case builds the record directly (D-1) | db-design §3.3 |
| `tests/.../Infrastructure/Persistence/DeanAccountSchemaTests.cs` | table set grows by the two tables (D-3) | FR-018, US-013 D-2 precedent |
| `tests/.../Web/Persistence/AccessCheckAuditSchemaTests.cs` | idem (D-3) | idem |
| `tests/.../Web/UseCases/AdminLoginCheckEveryTimeTests.cs` | idem (D-3) | idem |
| `tests/.../Web/Configuration/InstallationConfigurationTests.cs` | setting count 10 → 11, test renamed (D-4) | FR-012 |

No secret, no generated database file, no `.xlsx`, no IDE-local file is in the
change set; the Python prototype is untouched.

## 5. Validation Evidence

| Check | Command | Exit | Result |
|---|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | 0 | 0 errors, 0 warnings |
| Touched classes | `dotnet run --no-build --project tests/ClassroomAgent.Tests/ClassroomAgent.Tests.csproj -- -class …` (5 classes) | 0 | 67 total, 0 failed |
| Whole suite | `dotnet test ClassroomAgent.sln` | 0 | **2428 total, 2428 passed, 0 failed, 0 skipped** (3 m 18 s) |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | no file flagged |

Baseline before this session's changes (Docker started first; an initial run
without Docker failed every test on `DockerUnavailableException`, an environment
fault): 2422 total, 2418 passed, **4 failed** — exactly the four guards of D-3
and D-4. The 2428 now are the 2422 plus the six adapter tests of D-2.

The skeleton debt listed in the test generation report §4 is cleared: no
`NotImplementedException`, no `#pragma warning disable CS9113`, no
`RetentionSettings(0)` placeholder remains in `src/`; the log event name
`SyncSubmissionStateUnrecognised` and the properties `RawState` /
`SubmissionGoogleId` match the tests; `Retention:Years` has its constant.

## 6. Configuration Changes

- **`Retention:Years`** — new required installation key, a whole number ≥ 1, no
  default; missing or invalid stops the start (FR-012, VR-008, DC-3 updated).
  There is no `appsettings*.json` in the repository to update.

## 7. Deviations and Discovered Problems

- **D-1 (test fixture, earlier in this stage).**
  `CourseWorkInvariantTests.ItemWithNoCascadeDate_IsRefused` passed `default` to
  a helper whose parameter is `DateTimeOffset?`, so it meant `null` and the helper
  substituted a real instant. The test asserted nothing. It now builds the record
  directly. No assertion was weakened.
- **D-2 (coverage gap closed).** VR-005 says the test that proves paging
  "constructs the adapter", but the TEST_WRITING paging tests ran against the
  in-memory `SyncWorld` reader, so a single-page adapter would have passed them.
  Six adapter tests were added to the existing `GoogleClassroomReaderTests`,
  following its US-014 pattern: paging of both resources and of submissions, the
  `"-"` wildcard, the `PUBLISHED` filter, the date cascade with both due-date
  cases, the latest `TURNED_IN`, and the token's subject and exact scopes. Each
  would fail against the skeleton, which threw `NotImplementedException`.
- **D-3 (expected, US-013 D-2 precedent).** Three earlier Stories' guards compare
  the installation's tables against a literal set. Each keeps its own assertion,
  and only the sample set grew by `course_work` and `submission`. The comment on
  each says so.
- **D-4.** `InstallationConfigurationTests` (US-008) asserted ten settings and
  named the retention period as excluded. FR-012 makes it required, so the count
  is eleven. The test is renamed `…_NoTimeZoneOrLanguage`, and its summary
  records which Story changed it.
- **Scopes per call.** The adapter now asks each token for only the scopes of
  that call: the existing three for courses and rosters, and the two coursework
  scopes for the new reads. All five were already on the fixed list of six in
  §6, and none was added (FR-002).
- **Unimportable item.** Following the entity model and the invariant tests, an
  item with no id, title or cascade date is passed on as absent and refused by
  the entity. That fails the course's transaction, and the run records it through
  US-013's handler (spec §8). No skipping happens in the adapter.

## 8. Open Decisions

None touched, none raised. OD-001 … OD-012 remain resolved; OD-010 leaves
`trebovaniya.md` §7 item 14 open, and no code depends on its answer.
