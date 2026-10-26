---
artifact_type: test_generation_report
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T20:45:00Z
updated_at: 2026-10-26T20:45:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: docs/designs/api/US-058-api-design.md
    version: 1
  - path: docs/designs/database/US-058-db-design.md
    version: 1
  - path: docs/designs/database/US-058-entity-model.md
    version: 1
  - path: docs/tests/US-058-test-strategy.md
    version: 1
  - path: docs/tests/US-058-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-058 Test Generation Report

**Result: PASS (red phase verified).** 6278 tests, 6218 passed, 60 failed, 0 skipped. Every failure is in a class
this Story created or adapted, and each fails for missing production behaviour.

## 1. Files

### Created (tests)

| File | Tests |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/CourseWorkPresenceTests.cs` | 10 |
| `tests/ClassroomAgent.Tests/Application/UseCases/SkippedCourseRecordTests.cs` | 12 |
| `tests/ClassroomAgent.Tests/Application/UseCases/SyncDataHealthInvariantTests.cs` | 20 cases |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/SyncDataHealthPersistenceTests.cs` | 12 cases |
| `tests/ClassroomAgent.Tests/Web/BackgroundServices/CourseWorkPresenceSyncTests.cs` | 3 |

### Modified (tests and test infrastructure)

| File | Change |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/SyncWorld.cs` | `SkippedCourseRepository` (in-memory port, unique on the Google course id), `SkippedCourses` property, passed to the use case as `skippedCourses:` |
| `tests/ClassroomAgent.Tests/TestInfrastructure/FakeClassroomReader.cs` | `WithoutCourseWork(course, item)` — Classroom stops returning an item between host runs |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/AppUserMigrationTests.cs` | 22 migrations, `_AddSyncDataHealthRecords` last; `sync_skipped_course` in the table set |
| `tests/ClassroomAgent.Tests/Application/UseCases/IncrementalSyncRuleTests.cs` | 4 calls `RecordCompleteRead()` → `RecordCompleteRead(T)` (DB_DESIGN note) |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/IncrementalSyncPersistenceTests.cs` | 1 call likewise |

## 2. Compile-only skeleton

Every new member throws `NotImplementedException`; nothing is registered in DI; no migration; existing behaviour
unchanged. Build: 0 warnings, 0 errors.

| Declaration | File |
|---|---|
| `enum CourseSkipReason { ValueOutOfRange, UnknownCourseState }` | `src/ClassroomAgent.Domain/Enums/CourseSkipReason.cs` |
| `SyncSkippedCourse` (properties; `Record(...)`, `SkipAgain(...)` throw) | `src/ClassroomAgent.Domain/Entities/SyncSkippedCourse.cs` |
| `ISyncSkippedCourseRepository` (`GetAllAsync`, `Add`, `Remove`) | `src/ClassroomAgent.Application/Ports/ISyncSkippedCourseRepository.cs` |
| `CourseWork.NotInClassroomSince`, `CourseWork.MarkNotInClassroom(DateTimeOffset)` | `src/ClassroomAgent.Domain/Entities/CourseWork.cs` |
| `Course.LastCompleteReadAt`, `Course.RecordCompleteRead(DateTimeOffset)` (overload; the parameterless one stays until IMPLEMENTATION replaces it, so the running code is unchanged) | `src/ClassroomAgent.Domain/Entities/Course.cs` |
| `builder.Ignore(c => c.LastCompleteReadAt)` / `builder.Ignore(c => c.NotInClassroomSince)` until the migration maps them | `src/ClassroomAgent.Infrastructure/Persistence/Configurations/CourseConfiguration.cs`, `CourseWorkConfiguration.cs` |
| `RunSynchronizationUseCase` constructor parameter `ISyncSkippedCourseRepository? skippedCourses = null` (last), held in an unused field | `src/ClassroomAgent.Application/UseCases/RunSynchronizationUseCase.cs` |

IMPLEMENTATION owns all of these from here: it removes the parameterless `RecordCompleteRead()`, removes the two
`Ignore` calls, maps the columns and the table in the migration `AddSyncDataHealthRecords`, implements and registers
`SyncSkippedCourseRepository`, and may make `skippedCourses` required (the tests pass it by name).

## 3. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors (after one xUnit2013 analyzer fix in a new test).
- `dotnet test ClassroomAgent.sln` — full suite, 8 m 38 s, Docker running, no Npgsql/Ryuk/WSL timeouts.

## 4. Execution evidence

Total 6278 · passed 6218 · failed 60 · skipped 0. No failure outside the eight expected classes.

| Class | Failed | Reason (expected) |
|---|---|---|
| `CourseWorkPresenceTests` | 10 / 10 | no mark set / no last-read instant (`Assert.Equal` on null; `Assert.NotNull` on the mark after run 2) |
| `SkippedCourseRecordTests` | 11 / 12 | no record created (`Assert.Equal` counts, `KeyNotFoundException` on the record) |
| `SyncDataHealthInvariantTests` | 18 / 20 | `NotImplementedException` from `MarkNotInClassroom`, `RecordCompleteRead(readAt)`, `Record`, `SkipAgain` |
| `SyncDataHealthPersistenceTests` | 12 / 12 | column / table missing (`Assert.Single` on an empty catalogue query; `42P01`), repository not registered |
| `CourseWorkPresenceSyncTests` | 3 / 3 | `42703 column "not_in_classroom_since" does not exist`; `42P01 relation "sync_skipped_course" does not exist` |
| `AppUserMigrationTests` | 1 | table set lacks `sync_skipped_course`; migration count 21 ≠ 22 |
| `IncrementalSyncRuleTests` | 3 | adapted calls hit `NotImplementedException` in `RecordCompleteRead(readAt)` |
| `IncrementalSyncPersistenceTests` | 2 | same (`TheRememberedValue_RoundTripsAtStoragePrecision`, `TheLookup_AttachesNoEntity`) |

The 6 adapted regression tests are red only because the new signature is a stub; their expectations are unchanged and
they return to green when IMPLEMENTATION completes `RecordCompleteRead(readAt)`.

## 5. New tests green before implementation (explained)

| Test | Why it passes now | Why it is not vacuous after IMPLEMENTATION |
|---|---|---|
| `SkippedCourseRecordTests.OtherSkips_CreateNoRecord` (AC-011) | nothing creates records yet | it asserts absence for `404`, blank name and aged-out — the outcomes IMPLEMENTATION must NOT record; `ACourseSkippedForEitherReason_IsRecorded` proves records are created in the same world, and the test also asserts the aged-out course was skipped by age (control) |
| `SyncDataHealthInvariantTests.AnImportedItem_IsNotMarked` | the property defaults to null | pins that `Import` never marks (entity model §1) |
| `SyncDataHealthInvariantTests.AnImportedCourse_HasNoLastReadInstant` | the property defaults to null | pins FR-008.2 |

## 6. Delegation

- Delegated: the full `dotnet test` run (cheap-worker, Sonnet) — counts and failure classification above were checked
  against the log.
- Kept inline: test strategy, AC matrix, skeleton, all test code (every class depends on run-by-run reasoning about
  `SyncWorld` and host timing that a fixed brief could not carry), red-phase verdict.

## 7. Untested Acceptance Criteria

None. AC-001 … AC-015 each map to at least one test (matrix). Excluded scenarios and limitations: strategy §6, §7.

## 8. Open Decisions

None open. OD-001 resolved (a) — tested by AC-013.

## 9. Notes for IMPLEMENTATION

- The run-log line (FR-004) is found by the host test as the run's only event in 5100–5199 outside 5121 … 5135, carrying
  the run id and exactly two numeric properties (the counts). Any further numeric property on that line fails AC-012.
- `SyncWorld`'s in-memory unit of work does not roll back; the rollback of marks is proven only by
  `CourseWorkPresenceSyncTests.AFailureInsideTheCourseTransaction_RollsItsMarksBack`.
