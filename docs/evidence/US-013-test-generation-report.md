---
artifact_type: test_generation_report
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T12:29:57Z
updated_at: 2026-09-27T12:29:57Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/designs/api/US-013-api-design.md
    version: 1
  - path: docs/designs/database/US-013-db-design.md
    version: 1
  - path: docs/designs/database/US-013-entity-model.md
    version: 1
  - path: docs/tests/US-013-test-strategy.md
    version: 1
  - path: docs/tests/US-013-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 2
supersedes: null
---

# US-013 Test Generation Report — Background synchronization service

## 0. Result

**PASS.** 84 new tests across 7 test classes; 83 fail for the one expected reason
(the production behaviour does not exist yet) and 1 passes as a guard that must
stay green. One existing test was modified as the database design foresaw and now
fails red. Every other test in the solution still passes. Build: **0 errors, 0
warnings**.

## 1. Test files created

| File | Tests | Level |
|---|---|---|
| `tests/.../Application/UseCases/SynchronizationRunTests.cs` | 7 | unit — the run lifecycle and what is written |
| `tests/.../Application/UseCases/SynchronizationRefusalTests.cs` | 8 | unit — read-only mode and the missing connection |
| `tests/.../Application/UseCases/SyncStateInvariantTests.cs` | 12 | unit — the entity's own rules |
| `tests/.../Web/BackgroundServices/SyncScheduleTests.cs` | 11 | integration — the schedule, the first run, shutdown |
| `tests/.../Web/BackgroundServices/SyncRunCoordinationTests.cs` | 6 | integration — one run at a time, the US-019 seam |
| `tests/.../Web/Logging/SyncLoggingTests.cs` | 9 | integration + security — the four events, no personal data, no audit row |
| `tests/.../Web/Security/SyncReadinessTests.cs` | 9 | integration + security — readiness and the private port |
| `tests/.../Web/Persistence/SyncStateSchemaTests.cs` | 11 | integration — the table and its six constraints |
| `tests/.../Web/Configuration/SyncIntervalConfigurationTests.cs` | 11 | configuration — the interval and its boundaries |

Test infrastructure created: `TestInfrastructure/SyncWorld.cs` (the use case with
every port in memory), `TestInfrastructure/SyncTestData.cs` (every constant this
Story names), `TestInfrastructure/SyncHostExtensions.cs` (a seeded and started host,
the `sync_state` rows, the readiness state, the coordinator and the marker).

## 2. Test files modified

- `tests/.../Infrastructure/Persistence/AppUserMigrationTests.cs` — the installation's
  migration count five → six with `_AddSyncState` last, and `sync_state` added to the
  expected table set. **Foreseen by db-design §8**, traced here as US-011 and US-012
  traced their own. `TheControlPlaneSchema_IsUnchangedByThisStory` was left untouched
  and still passes.

No other existing test was touched. No production file was modified beyond the
OD-008 skeleton (§6).

## 3. Commands used

```
dotnet build ClassroomAgent.sln
dotnet test ClassroomAgent.sln
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~Sync"
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~SyncStateSchemaTests"
dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~SyncStateSchemaTests|FullyQualifiedName~SyncReadinessTests"
```

Docker was running throughout, so the Testcontainers requirement of TC-2 met no
obstacle.

## 4. Execution evidence

**Build**

```
Предупреждений: 0
Ошибок: 0
```

**Whole suite**

```
итог: 2251
сбой: 84
успешно: 2167
пропущено: 0
длительность: 3m 58s
```

**US-013 tests alone** (`FullyQualifiedName~Sync`)

```
итог: 84
сбой: 83
успешно: 1
```

**The schema tests alone**, to confirm none of them passes vacuously on a table
that does not exist yet:

```
итог: 11
сбой: 11
успешно: 0
```

## 5. Classification of every result

| Class | Count | Classification |
|---|---|---|
| The 83 failing US-013 tests | 83 | **Fail because the production behaviour does not exist.** Each compiles, each host context is valid, and each failure message matches its scenario: the skeleton members throw `NotImplementedException`, the background service is not registered, `sync_state` is not in the schema, and `InstallationSettings` is not resolvable from the container. |
| `SyncReadinessTests.Readiness_IsNotReachableOnThePublicPort` | 1 | **Passes today, and must stay green.** Readiness has never been served on the public port (SC-9, DC-6) and this Story must not change that. It is marked `GUARD` in the matrix, not counted as coverage of new behaviour. |
| `AppUserMigrationTests.TheMigrations_Create…` | 1 | **Fails by design** — it now expects the sixth migration this Story ships (db-design §8). |
| Every other test in the solution | 2166 | **Passes.** No regression. |

**Investigated, as the Skill requires:** one test written in the first pass,
`BeforeTheFirstRun_ThereIsNoRow`, passed before implementation. On inspection it
asserted the state of the in-memory fixture rather than any production behaviour —
a vacuous pass. It was **deleted**, not weakened or kept: the same requirement
(spec I-2, "no row means never synchronized") is proved where it is observable, by
`InReadOnlyMode_TheScheduleKeepsRunning_AndWritesNothing` and by the schema tests
finding no row on a fresh database.

Two guards that fire automatically were checked deliberately, because US-012 met
them as a surprise: `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`
and `PermittedServiceWriteTests.TheRegistry_DeclaresOnlyWritesOnTheClosedList`
**both still pass**, and they must keep passing: `RunSynchronizationUseCase` calls
`IReadOnlyModeGuard` first, so it is a protected write path and `PermittedServiceWrites`
does not grow (spec FR-006). BR-026's closed list is untouched by this Story.

## 6. The OD-008 skeleton, and what IMPLEMENTATION must clean up

Ten items, exactly as the resolution lists them (open-decisions v2 §OD-008). Two
things IMPLEMENTATION owns beyond filling the bodies:

1. **`#pragma warning disable CS9113`** wraps three skeleton classes
   (`RunSynchronizationUseCase`, `SynchronizationBackgroundService`,
   `SyncStateRepository`). Their primary-constructor parameters are unread while the
   bodies only throw, and `TreatWarningsAsErrors` turns that into a build error. Each
   pragma is narrow and carries a note. **Every one of them must be gone when the
   members are implemented** — a remaining pragma is a finding.
2. **`SyncStateConfiguration` and `DbSet<SyncState>` are deliberately absent** from
   the skeleton: mapping the entity without its migration would leave the model and
   the database disagreeing. IMPLEMENTATION adds the mapping, the six check
   constraints, the unique index and the `AddSyncState` migration as one piece.

`InstallationSettings` gained `TimeSpan? SyncInterval = null` with a default so no
existing construction site broke, and `InstallationSettingsReader` gained only the
key constant `SyncIntervalKey`. **Parsing, validation and the default of one hour
are not written** — `SyncIntervalConfigurationTests` is red against them. The
configuration tests also require `InstallationSettings` to be **resolvable from the
container**, which it is not today; registering it is part of the Story (spec
FR-013, FR-018).

## 7. Names IMPLEMENTATION must honour or correct together with the tests

Table and migration: `sync_state`, `AddSyncState`. Columns: `status`, `run_id`,
`started_at`, `finished_at`, `processed_count`, `last_error`,
`last_successful_run_at`, `singleton`, `created_at`, `updated_at`. Status codes:
`running`, `completed`, `failed`. Constraints: `pk_sync_state`,
`uq_sync_state_singleton`, `ck_sync_state_singleton`, `ck_sync_state_status`,
`ck_sync_state_counter`, `ck_sync_state_finished_after_started`,
`ck_sync_state_terminal_fields`, `ck_sync_state_error_length`. Log events:
`SyncRunStarted`, `SyncRunCompleted`, `SyncRunFailed`, `SyncRunSkipped`, with
properties `RunId`, `ProcessedCount`, `Reason`. Configuration key
`Sync:IntervalMinutes`, bounds 1 … 1440, default 60 minutes. Operation name
`Sync.Run`. Coordinator members `TryStartScheduledRun`, `TryStartRequestedRun`,
`Request`, `RunCompleted`, `Changed`. Marker members `IsRunning`, `MarkRunning`,
`MarkStopped`.

## 8. Coverage limits recorded honestly

- **A stale `Running` row after a crash is not tested** — a test cannot kill the
  process and then assert against it. Spec I-3 tolerates the state; what is asserted
  is that a graceful stop records no failure and that the next run overwrites the row.
- **"The service is not running" is simulated** through the readiness marker, not by
  killing the `BackgroundService`: a test cannot make it die without tearing down the
  host it then queries.
- **The failed-run log level is asserted only where a failure is producible.** With an
  empty pipeline (OD-001) nothing inside a run can fail on demand, so `SyncRunFailed`
  is pinned as a name and a level in the strategy and in `SyncTestData`; US-014, which
  introduces a step that can fail, is where the level becomes assertable end to end.
  IMPLEMENTATION must emit the event at `Error` (spec I-7) even though today only a
  database failure would produce it.
- **The counter is asserted as zero everywhere**, because nothing increments it yet
  (spec I-1). US-014 is where a non-zero counter becomes assertable.
- **`" 5 "`, `"+5"` and `"1.5"` are expected to be rejected** on the reader's existing
  `NumberStyles.None` convention. If IMPLEMENTATION reads the value more leniently,
  that is a deviation to record, not a test to weaken.

## 9. Untested Acceptance Criteria

None. AC-001 … AC-009 all have mapped scenarios in the matrix, and every mandatory
one has at least one red test that must turn green.

## 10. Open Decisions

- **OD-008** raised here and resolved by the Owner on 2026-09-27 as option 1
  (open-decisions v2). Without it the test project could not compile.
- No other Open Decision was raised. OD-001 … OD-007 arrived resolved and every
  assertion above follows them.
