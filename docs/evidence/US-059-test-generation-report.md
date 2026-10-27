---
artifact_type: test_generation_report
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T12:50:00Z
updated_at: 2026-10-27T12:50:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/designs/database/US-059-db-design.md
    version: 1
  - path: docs/designs/database/US-059-entity-model.md
    version: 1
  - path: docs/tests/US-059-test-strategy.md
    version: 1
  - path: docs/tests/US-059-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-059 Test Generation Report

**Overall result: PASS.** Every AC-001 … AC-018 is mapped (`docs/tests/US-059-ac-test-matrix.md`); the solution builds
with 0 warnings and 0 errors; every new behaviour test fails for the expected reason; existing tests pass.

## 1. Files

Created (tests):

- `tests/ClassroomAgent.Tests/Application/UseCases/CourseListPresenceTests.cs` (22 cases)
- `tests/ClassroomAgent.Tests/Application/UseCases/CourseListPresenceInvariantTests.cs` (25 cases)
- `tests/ClassroomAgent.Tests/Application/UseCases/SaveWorkspaceConnectionAccountChangeTests.cs` (3)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/CourseListMarksPersistenceTests.cs` (14)
- `tests/ClassroomAgent.Tests/Web/BackgroundServices/CourseListPresenceSyncTests.cs` (4)
- `tests/ClassroomAgent.Tests/Web/BackgroundServices/NegativeValueSyncTests.cs` (2)

Modified (tests and test infrastructure):

- `tests/ClassroomAgent.Tests/TestInfrastructure/SyncWorld.cs` — `CourseRepository.GetAllAsync`; `ConnectionRepository.Stored`,
  `OnRead`, `Replace`; `ClassroomReader.AfterCourses`; the run built with `workspaceConnections: Connections`.
- `tests/ClassroomAgent.Tests/TestInfrastructure/FakeClassroomReader.cs` — `WithoutCourse`.
- `tests/ClassroomAgent.Tests/Infrastructure/Google/GoogleClassroomReaderValueRangeTests.cs` — `Negative` data, three
  new tests (15 + 3 cases); −7.9·10²⁸ removed from `JustInside` (v100 BR-043, FR-008).
- `tests/ClassroomAgent.Tests/Web/BackgroundServices/ValueOutOfRangeSyncTests.cs` — course fingerprint without the
  course row's `updated_at` (FR-003.1 records a sighting on a skipped listed course).
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` — 23 migrations, `_AddCourseListMarks` last.
- `tests/ClassroomAgent.Tests/Web/Persistence/SyncStateSchemaTests.cs` — 15 columns, `last_course_list_empty`.
- `tests/ClassroomAgent.Tests/Web/Persistence/CourseSchemaTests.cs` — `TheOnlyCheckConstraint_IsTheStateVocabulary`
  renamed `TheCheckConstraints_AreTheStateVocabularyAndTheAbsenceMark`, expects `ck_course_absence_mark` too.

Each adaptation is a requirement change of this Story (strategy §6), not a weakened assertion.

## 2. Compile-only skeleton

Every new member throws `NotImplementedException("US-059 TEST_WRITING skeleton: IMPLEMENTATION completes it.")`;
nothing registered in DI; no migration; existing behaviour unchanged.

| Declaration | File |
|---|---|
| `enum CourseAbsenceMark { NotInClassroom, NotVisibleToTechnicalAccount }` | `src/ClassroomAgent.Domain/Enums/CourseAbsenceMark.cs` |
| `static class CourseAbsenceRule` — `MarkFor(DateTimeOffset?, DateTimeOffset?)` | `src/ClassroomAgent.Domain/Rules/CourseAbsenceRule.cs` |
| `Course.AbsenceMark`, `AbsentSince`, `LastListedAt`; `MarkAbsent(CourseAbsenceMark, DateTimeOffset)`, `RecordListed(DateTimeOffset)` | `src/ClassroomAgent.Domain/Entities/Course.cs` |
| `SyncState.LastCourseListEmpty`; `RecordCourseListRead(bool)` | `src/ClassroomAgent.Domain/Entities/SyncState.cs` |
| `WorkspaceConnection.TechnicalAccountChangedAt`; `ChangeTo(string, string, DateTimeOffset)` (overload — the two-argument one stays until IMPLEMENTATION replaces it and its one call site, so the running code is unchanged) | `src/ClassroomAgent.Domain/Entities/WorkspaceConnection.cs` |
| `ICourseRepository.GetAllAsync(CancellationToken)`; `CourseRepository.GetAllAsync` throws | `src/ClassroomAgent.Application/Ports/ICourseRepository.cs`, `src/ClassroomAgent.Infrastructure/Persistence/Repositories/CourseRepository.cs` |
| `builder.Ignore(...)` for the five new properties until the migration maps them | `CourseConfiguration.cs`, `SyncStateConfiguration.cs`, `WorkspaceConnectionConfiguration.cs` under `src/ClassroomAgent.Infrastructure/Persistence/Configurations/` |
| `RunSynchronizationUseCase` constructor parameter `IWorkspaceConnectionRepository? workspaceConnections = null` (last), held in an unused field (the US-058 precedent) | `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` |

IMPLEMENTATION owns all of these from here: it removes `ChangeTo(string, string)` and passes `now` from
`SaveWorkspaceConnectionUseCase.StoreAsync`, removes the `Ignore` calls, maps the columns, the converter and
`ck_course_absence_mark` in the migration `AddCourseListMarks`, may make `workspaceConnections` required (the tests pass
it by name), moves `observedAt` before the connection read (R1), and rewrites the `Course` remark "There is deliberately
no `MarkMissing`" (entity model §2).

## 3. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors (after adding a missing `using` in a new test).
- `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~<Class>"` — one class per run, 16 classes
  (raw output kept in the session scratchpad).
- `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~ClassroomAgent.Tests.Application"` — 2025 total,
  1979 passed, 46 failed — exactly the 22 + 22 + 2 new red cases of CLP, INV and SWC; no other Application test fails.
- Docker Desktop had to be started first; no Testcontainers, Npgsql or WSL failure occurred.

## 4. Results per class

| Class | Total | Passed | Failed | Failure reasons |
|---|---|---|---|---|
| `CourseListPresenceInvariantTests` | 25 | 3 | 22 | `NotImplementedException` from the skeleton (20; 2 of them inside `Assert.Throws` expecting `ArgumentException` / `ArgumentOutOfRangeException`) |
| `CourseListPresenceTests` | 22 | 0 | 22 | no mark / no sighting / flag not set (the run does nothing new yet); `NotImplementedException` from `WorkspaceConnection.ChangeTo` where the test changes the account (in R2's test inside the run, so the run fails — `Assert.False(outcome.Failed)`); R1: the sighting is null |
| `SaveWorkspaceConnectionAccountChangeTests` | 3 | 1 | 2 | change instant null (the use case still calls the two-argument `ChangeTo`) |
| `GoogleClassroomReaderValueRangeTests` | 36 | 21 | 15 | no exception for a negative value — the reader converts it today |
| `CourseListMarksPersistenceTests` | 14 | 0 | 14 | `42703` column does not exist / column query empty; `NotImplementedException` from the entities |
| `CourseListPresenceSyncTests` | 4 | 0 | 4 | `42703` column does not exist; no new log line within 30 s |
| `NegativeValueSyncTests` | 2 | 1 | 1 | run `failed` — today a negative grade reaches the database check and stops the run (the defect FR-008 fixes) |
| `AppUserMigrationTests` | 6 | 5 | 1 | 22 migrations, expected 23 |
| `SyncStateSchemaTests` | 11 | 10 | 1 | 14 columns, expected 15 |
| `CourseSchemaTests` | 12 | 11 | 1 | `ck_course_absence_mark` missing |
| `ValueOutOfRangeSyncTests`, `SkippedCourseRecordTests`, `CourseWorkPresenceTests`, `CourseWorkPresenceSyncTests`, `SaveWorkspaceConnectionBranchTests`, `CourseImportTests` | 6 / 12 / 10 / 3 / 5 / 12 | all | 0 | regression: green |

## 5. Green before IMPLEMENTATION (Green\*), explained

- `AnImportedCourse_HasNoMarkAndNoSighting`, `ANewState_HasNoEmptyListFlag`, `ACreatedConnection_HasNoChangeInstant`
  (INV): the skeleton's new properties default to null / false, which is the specified initial state; each is paired
  with red tests that set and clear the value.
- `AnAccountChange_WritesNoAuditEventOfItsOwn` (SWC): FR-002.4 says nothing changes in the audit; it guards against
  IMPLEMENTATION adding an event.
- `Zero_IsConvertedAsToday` (RDR, ×3) and `ZeroValues_AreStored_AndTheCourseIsNotSkipped` (NEG): FR-008.3 pins that the
  check is strictly below zero; today's conversion already accepts zero.

## 6. Expected failing new tests

All failures above are for missing behaviour or schema; none is caused by syntax, fixtures, host configuration or an
assertion error. The 30-second wait of `EachRun_LogsOneLineCountingCourseMarks_AndNamesNothing` is the bounded wait for
a line that does not exist yet.

## 7. Unexpected failures

None.

## 8. Untested Acceptance Criteria

None. Limitations: the FR-007 counts are matched as a multiset because the event's property names are IMPLEMENTATION's
(strategy §5, §8).

## 9. Findings handled

- **US-059/DB_DESIGN/N-1** — no test calls `WorkspaceConnection.ChangeTo(string, string)` (the only caller is
  `SaveWorkspaceConnectionUseCase`); the new overload is tested directly (INV) and through the use case (SWC). Nothing
  to adapt.
- **US-059/DB_DESIGN/N-2** — every test clock (`ManualTimeProvider`, `TestTimeProvider`) is advanced by hand and
  returns the same instant until then, so no test depends on the order of clock reads; R1 itself is pinned by
  `TheRunInstant_IsTakenBeforeTheConnectionIsRead`.

## 10. Notes for IMPLEMENTATION

- `CourseImportTests.ACourseClassroomStoppedReturning_IsLeftUntouched` (US-014) says "not marked"; it runs its second
  run in a separate world, so it stays green, but its summary predates v100 — a comment for IMPLEMENTATION to correct
  when it rewrites the `Course` remark (no test change made here).

## 11. Delegation

Kept inline: strategy, matrix, skeleton, all test code (the scenarios depend on run-by-run reasoning), the red-phase
verdict. Delegated to a `cheap-worker` (Sonnet): the 16 per-class test runs and their failure summary.

## 12. Open Decisions

None.
