---
artifact_type: implementation_report
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T21:05:00Z
updated_at: 2026-10-26T21:05:00Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
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
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: false
---

# US-058 Implementation Report

## 1. Summary

Synchronization now (a) marks, in a full read of a course, every stored item Google no longer returns with the run's
instant and clears the mark when the item comes back; (b) records a course skipped for out-of-range values or an
unknown state in the new table `sync_skipped_course`, keeps the first instant on a repeated skip, and removes the
record on a full read, on aging out, or when the course has left a list read to its end; (c) stores the run's instant
as the course's last successful read; and (d) writes one log line per run with the two counts. One migration,
`AddSyncDataHealthRecords`. Nothing is displayed (Stories 2 and 3).

Validation: build 0 warnings; full suite 6278 run, 6274 passed and 4 failed — the four were registry tests that list
the installation's tables / `course` columns, updated as supporting changes and then green (33/33 in their four classes);
`dotnet format --verify-no-changes` exit 0. Not security-sensitive: no endpoint, role, policy, credential, audit or
Google-scope change; the log line carries two numbers (SC-10).

## 2. Source Artifacts

Story `docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md`; Specification v1 (APPROVED);
Open Decisions v1 (OD-001 resolved a); API design v1 (NOT_APPLICABLE); database design v1; entity model v1; test
strategy v1; AC matrix v1; test generation report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (all passing) |
|---|---|---|
| AC-001, AC-002 | `RunSynchronizationUseCase.ImportCourseWorkAsync` — stored items whose (resource, Google id) is not in the page → `CourseWork.MarkNotInClassroom(observedAt)`; `CourseWork.MarkNotInClassroom` | `CourseWorkPresenceTests` (AC-001, AC-002, key test), `SyncDataHealthInvariantTests.Marking_…` |
| AC-003 | `CourseWork.MarkNotInClassroom` returns false and keeps the first instant | `AMarkedItemStillAbsent_KeepsTheFirstInstant`, `MarkingAgain_…` |
| AC-004 | `CourseWork.UpdateFrom` clears `NotInClassroomSince`; use case counts it as unmarked | `AMarkedItemThatComesBack_…`, `UpdatingFromGoogle_ClearsTheMark_…` |
| AC-005 | marking happens only inside `ImportCourseAsync`, reached only for a full read (unchanged archive, out of range, `404`, blank name skip before it) | `ACourseNotReadInFull_…`, `ACourseReportedGone_…` |
| AC-006 | marks are staged in the course's transaction (`ExecuteInTransactionAsync`), so a failure rolls them back | `ARunStoppedByTheCourseRead_…`, `CourseWorkPresenceSyncTests.AFailureInsideTheCourseTransaction_RollsItsMarksBack` |
| AC-007 | `RecordSkipAsync` in the unknown-state and out-of-range branches → `SyncSkippedCourse.Record`, saved on its own; table/config/repository | `SkippedCourseRecordTests` (AC-007 group), `SyncDataHealthPersistenceTests`, `ARealRun_RecordsTheSkippedCourse_…` |
| AC-008 | `RecordSkipAsync` → `SyncSkippedCourse.SkipAgain` for an existing record | `ARepeatedSkip_…`, `TheSameSkipAgain_…`, `SkippingAgain_…` |
| AC-009 | `ImportCourseAsync` removes the record with the full read's last write | `ARecordedCourseReadInFull_LosesItsRecord` |
| AC-010 | after the course list is enumerated to its end, records whose id was not listed are removed and saved | `ARecordedCourseNoLongerListed_…` |
| AC-011 | no record is created on `404`, blank name, aged-out; no other outcome removes one | `OtherSkips_CreateNoRecord`, `ARecordedCourse_GoneMidRunOrBlankNamed_KeepsItsRecord` |
| AC-012 | `ImportProgress.CourseWorkMarked/Unmarked` from committed courses → `SynchronizationRunOutcome` → `SynchronizationBackgroundService.LogCourseWorkPresenceChanged` (event 5136 `SyncCourseWorkPresenceChanged`, Information, only when a count > 0, written whether the run completed or stopped) | `CourseWorkPresenceSyncTests.EachRun_LogsOneLineCountingMarkedAndUnmarkedItems_AndNamesNothing` |
| AC-013 | `Course.RecordCompleteRead(observedAt)` sets `LastCompleteReadAt` with `CompleteReadUpdateTime` | `AFullRead_StoresTheRunInstant_…`, `AnUnchangedArchive_KeepsItsLastReadInstant`, invariant and persistence tests |
| AC-014 | on `CourseResult.AgedOut` the record is removed inside that course's transaction | `ARecordedCourseAgedOut_IsNotLoaded_AndLosesItsRecord` |
| AC-015 | unlisted-record removal sits after the `await foreach`; a stop propagates before it | `AStoppedRun_KeepsTheMarksItCommitted_…`, `AListThatFailsPartWay_RemovesNoUnlistedRecord` |

## 4. Change Set

### Production (src)

| File | Trace |
|---|---|
| `Domain/Enums/CourseSkipReason.cs` (new, from skeleton) | entity model §3, VR-003 |
| `Domain/Entities/SyncSkippedCourse.cs` (new, completed) | entity model §3, FR-005, FR-006, VR-001, VR-002 |
| `Domain/Entities/CourseWork.cs` | entity model §1, FR-001 … FR-003 (`NotInClassroomSince`, `MarkNotInClassroom`, `UpdateFrom` clears) |
| `Domain/Entities/Course.cs` | entity model §2, FR-008 (`LastCompleteReadAt`, `RecordCompleteRead(readAt)` replaces the parameterless one); the stray US-018 summary that sat above it moved back onto `UpdateFrom` (same edit site) |
| `Application/Ports/ISyncSkippedCourseRepository.cs` (new) | entity model §4 |
| `Application/UseCases/RunSynchronizationUseCase.cs` | entity model §5, FR-001 … FR-008; constructor gains required `skippedCourses` (before the optional `linking`) |
| `Application/Models/SynchronizationRunOutcome.cs` | FR-004 (two counts carried to the host) |
| `Infrastructure/Persistence/Configurations/SyncSkippedCourseConfiguration.cs` (new) | db-design §4 |
| `Infrastructure/Persistence/Configurations/CourseConfiguration.cs`, `CourseWorkConfiguration.cs` | db-design §2, §3 (skeleton `Ignore` replaced by the mapping) |
| `Infrastructure/Persistence/ClassroomAgentDbContext.cs` | `DbSet<SyncSkippedCourse>` + `ApplyConfiguration` (entity model §3) |
| `Infrastructure/Persistence/TimestampInterceptor.cs` | PC-6 timestamps of `sync_skipped_course` (db-design §4) |
| `Infrastructure/Persistence/Repositories/SyncSkippedCourseRepository.cs` (new) | entity model §4 |
| `Infrastructure/Persistence/Migrations/20261026202733_AddSyncDataHealthRecords.cs` + `.Designer.cs` (new), `ClassroomAgentDbContextModelSnapshot.cs` | db-design §5, PC-2 |
| `Web/Configuration/InstallationServices.cs` | DI registration of the repository (supporting) |
| `Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-004 log event 5136 |

### Tests

| File | Trace |
|---|---|
| 5 new classes, `SyncWorld.cs`, `FakeClassroomReader.cs`, `AppUserMigrationTests.cs`, `IncrementalSyncRuleTests.cs`, `IncrementalSyncPersistenceTests.cs` | TEST_WRITING (test generation report §1) |
| `CourseWorkFailureTests.cs`, `CourseWorkImportTests.cs`, `SubmissionImportTests.cs`, `SubmitterWithoutRosterTests.cs` | supporting: they construct the use case directly and now pass `world.SkippedCourses` for the new required parameter |
| `Web/Persistence/CourseSchemaTests.cs` | supporting registry update: `course` has 19 columns, `last_complete_read_at` nullable (db-design §3) |
| `Infrastructure/Persistence/DeanAccountSchemaTests.cs`, `Web/UseCases/AdminLoginCheckEveryTimeTests.cs`, `Web/Persistence/AccessCheckAuditSchemaTests.cs` | supporting registry update: the installation's table list gains `sync_skipped_course` (db-design §4) |

No secret, generated database file or IDE-local config in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors (after the test registry fixes too) |
| Story + touched classes | `dotnet test ClassroomAgent.sln --filter FullyQualifiedName~<Class>`, one class per run, 12 classes | 142/142 passed (Invariant 20, Presence 10, SkipRecord 12, Persistence 12, PresenceSync 3, AppUserMigration 6, IncrementalSyncRule 37, IncrementalSyncPersistence 14, CourseWorkFailure 3, CourseWorkImport 10, SubmissionImport 11, SubmitterWithoutRoster 4) |
| Full suite | `dotnet test ClassroomAgent.sln` (8 m 19 s) | 6278 total, 6274 passed, 4 failed, 0 skipped; no Npgsql/Ryuk/WSL timeouts |
| The 4 failures, after the registry fix | `--filter` per class: `CourseSchemaTests`, `DeanAccountSchemaTests`, `AdminLoginCheckEveryTimeTests`, `AccessCheckAuditSchemaTests` | 12 + 7 + 7 + 7 = 33/33 passed |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 (run before and after the registry fixes) |

The full suite was not rerun after the registry fixes: they touched only those four test files, which were rerun in
full; no production file changed after the full run. Result: 6278/6278 by composition.

Delegated: the 12-class run and the full suite + first format check (cheap-worker, Sonnet), verified against their
logs. Inline: all production code, the four registry-test edits, the final format check and the 4-class rerun.

## 6. Configuration Changes

None (no `appsettings` key, no host option).

## 7. Deviations and Discovered Problems

- **Use-case constructor.** The skeleton added `skippedCourses` optional and last; it is now required and placed
  before the optional `linking` (the US-032 pattern keeps only genuinely optional steps optional). Four existing test
  files that build the use case by hand were adapted (§4). No artifact named the constructor; no behaviour change.
- **Registry tests missed at TEST_WRITING.** Four existing tests enumerate tables or `course` columns; TEST_WRITING
  updated only `AppUserMigrationTests`. Updated here as supporting changes that follow from db-design §3, §4.
- **Log event id.** FR-004.4 left id and name to IMPLEMENTATION: 5136 `SyncCourseWorkPresenceChanged` (next free in
  the sequence; 5129 left unused as before). The test finds it without pinning (strategy §5).
- **Skip-record key.** Records are matched by the Google id trimmed and cut to 64, exactly as the record stores it
  (VR-001); a listed course with a blank id gets no record (db-design §4).
- **Doc comment.** In `Course.cs` the US-018 summary "The upsert's update half" that sat above `RecordCompleteRead`
  (US-018 security INFO-1) was moved onto `UpdateFrom` while replacing the method at that very place.

## 8. Open Decisions

None touched or newly required.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-058
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-058-implementation-report.md
  next_stage: IMPLEMENTATION_VERIFICATION
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "D-1: use-case parameter skippedCourses made required; 4 hand-built test constructions adapted"
    - "D-2: 4 registry tests (table list / course column count) updated here, missed at TEST_WRITING"
    - "Log event 5136 SyncCourseWorkPresenceChanged chosen (FR-004.4)"
    - "Full suite not rerun after the test-only registry fixes; the 4 classes rerun 33/33"
```
