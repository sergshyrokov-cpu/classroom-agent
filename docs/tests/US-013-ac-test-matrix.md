---
artifact_type: ac_test_matrix
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T12:29:57Z
updated_at: 2026-09-27T12:29:57Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-013-background-sync-service.md
    version: null
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
supersedes: null
---

# US-013 Acceptance Criteria → Test Matrix

`RED` = fails now because the production behaviour does not exist yet (the expected
pre-implementation state). `GUARD` = passes now and must stay green.

Namespaces are under `ClassroomAgent.Tests`; the test-class column gives the class
name only.

## AC-001 The service is hosted, and readiness knows whether it runs

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| The host registers the service, so a running installation is not `Unhealthy` | Integration | `SyncReadinessTests` | `WithTheServiceRunning_ReadinessIsNotUnhealthy` | readiness ≠ `Unhealthy` | RED |
| The in-process marker reports the service as running | Integration | `SyncReadinessTests` | `WithTheServiceRunning_TheMarkerSaysSo` | `IsRunning` is true | RED |
| The service not running is an outage | Integration | `SyncReadinessTests` | `WithoutTheServiceRunning_ReadinessIsUnhealthy503` | `503`, body `Unhealthy` | RED |
| Read-only mode stays a defined operating state | Integration | `SyncReadinessTests` | `InReadOnlyMode_ReadinessStaysDegraded200` (3 causes) | `200`, body `Degraded` | RED |
| Precedence: the service outranks read-only mode | Integration | `SyncReadinessTests` | `TheServiceNotRunning_OutranksReadOnlyMode` | body `Unhealthy` | RED |
| Readiness answers the state only | Integration | `SyncReadinessTests` | `Readiness_AnswersTheStateOnly` | one of three words, no detail | RED |
| Readiness stays off the public port (SC-9) | Security | `SyncReadinessTests` | `Readiness_IsNotReachableOnThePublicPort` | not `200` | GUARD |

## AC-002 A run starts on a schedule, and only one runs at a time

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| Default interval is one hour | Integration | `SyncScheduleTests` | `WithoutTheSetting_TheIntervalIsOneHour` | next run due one hour after completion | RED |
| The configured interval is used | Integration | `SyncScheduleTests` | `WithTheSetting_TheConfiguredIntervalIsUsed` | due at 15 minutes, not at the default | RED |
| Counted from completion, not from start | Integration | `SyncScheduleTests` | `TheIntervalCountsFromCompletion` | no timer at start + interval | RED |
| The next run happens and reuses the row | Integration | `SyncScheduleTests` | `WhenTheIntervalElapses_ASecondRunHappens` | one row, new run id | RED |
| The interval after a failure is unchanged (OD-003) | Integration | `SyncScheduleTests` | `AfterAFailedRun_TheIntervalIsUnchanged` | same interval | RED |
| The first run waits for the first legitimacy determination (OD-006) | Integration | `SyncScheduleTests` | `TheFirstRun_WaitsForTheFirstLegitimacyDetermination` | no row until the check answers, then a run | RED |
| The schedule survives a skipped run | Integration | `SyncScheduleTests` | `InReadOnlyMode_TheScheduleKeepsRunning_AndWritesNothing` (3 causes) | a next run scheduled, no row | RED |
| No second scheduled run while one is in progress | Integration | `SyncRunCoordinationTests` | `WhileARunIsInProgress_TheScheduleStartsNoSecondRun` | second attempt refused | RED |
| No parallel run from a request | Integration | `SyncRunCoordinationTests` | `WhileARunIsInProgress_ARequestStartsNoParallelRun` | request not startable | RED |
| The next run may start after completion | Integration | `SyncRunCoordinationTests` | `AfterARunCompletes_TheNextOneMayStart` | accepted | RED |
| The coordinator is one instance | Integration | `SyncRunCoordinationTests` | `TheCoordinator_IsOneInstanceForTheProcess` | same instance | RED |
| The row never holds two runs at once | Integration | `SyncRunCoordinationTests` | `TheRow_NeverHoldsTwoRunsAtOnce` | one row through three intervals | RED |
| The US-019 seam accepts a request while idle (OD-007) | Integration | `SyncRunCoordinationTests` | `ARequestMadeWhileIdle_IsAcceptedAndStartable` | accepted and startable | RED |

## AC-003 `SyncState` records the run and nothing else does

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| A first run creates the row and completes with a zero counter (I-1, I-2) | Unit | `SynchronizationRunTests` | `AFirstRun_CreatesTheRow_AndCompletesWithAZeroCounter` | `Completed`, counter 0, no error | RED |
| A completed run writes the instant of the last success | Unit | `SynchronizationRunTests` | `ACompletedRun_RecordsTheInstantOfTheLastSuccess` | equals the end instant | RED |
| One row, updated in place (OD-004) | Unit | `SynchronizationRunTests` | `ASecondRun_UpdatesTheSameRow` | one row, second run id | RED |
| Two commits per run, `Running` first (FR-006) | Unit | `SynchronizationRunTests` | `ARun_IsCommittedAsRunning_BeforeItsOutcome` | statuses `Running`, `Completed` | RED |
| The guard is consulted before anything is read | Unit | `SynchronizationRunTests` | `TheGuard_IsConsultedBeforeAnythingIsRead` | one operation, the declared name | RED |
| A first row starts as `Running` | Unit | `SyncStateInvariantTests` | `AFirstRun_StartsAsRunning` | nothing finished, nothing failed | RED |
| A new run clears the error and the counter, keeps the last success | Unit | `SyncStateInvariantTests` | `ANewRun_ClearsTheErrorAndTheCounter_AndKeepsTheLastSuccess` | as stated | RED |
| A run is finished once | Unit | `SyncStateInvariantTests` | `CompletingATerminalRow_IsRejected`, `FailingATerminalRow_IsRejected` | rejected | RED |
| Status and terminal fields never disagree | Unit | `SyncStateInvariantTests` | `TheTerminalFields_NeverDisagreeWithTheStatus` | as db-design §3.2 | RED |
| The migration creates the table (PC-2, PC-4) | Integration | `SyncStateSchemaTests` | `TheMigration_CreatesTheTable` | columns, nullability, lengths 16 / 512 | RED |
| Primary key and singleton index exist | Integration | `SyncStateSchemaTests` | `ThePrimaryKeyAndSingletonIndex_Exist` | `pk_sync_state`, `uq_sync_state_singleton` | RED |
| A second row is rejected by the database | Integration | `SyncStateSchemaTests` | `ASecondRow_IsRejected` | `23505` on the singleton index | RED |
| No foreign key, no other index (db-design §3.4) | Integration | `SyncStateSchemaTests` | `TheTable_HasNoForeignKey`, `TheOnlyIndex_IsTheSingletonOne` | as stated | RED |
| Migrations five to six, `sync_state` added (db-design §8) | Integration | `AppUserMigrationTests` | `TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` (existing, modified) | six migrations, `_AddSyncState` last | RED |

## AC-004 In read-only mode nothing runs, nothing is called, nothing is written

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| The run is skipped with its reason | Unit | `SynchronizationRefusalTests` | `InReadOnlyMode_TheRunIsSkipped_WithItsReason` | read-only reason, no run id | RED |
| **Nothing is written** (BR-026) | Unit | `SynchronizationRefusalTests` | `InReadOnlyMode_NothingIsWritten` | no row, no commit, no transaction | RED |
| The guard is asked first and nothing else is read (AD-6) | Unit | `SynchronizationRefusalTests` | `InReadOnlyMode_TheGuardIsAskedFirst_AndNothingElseIsRead` | zero reads of row and connection | RED |
| The refusal carries this use case's operation name | Unit | `SynchronizationRefusalTests` | `TheRefusal_CarriesThisUseCasesOperationName` | `Sync.Run` | RED |
| Leaving read-only mode resumes runs without a restart | Unit | `SynchronizationRefusalTests` | `WhenReadOnlyModeEnds_TheNextRunHappens` | the next run happens | RED |
| Nothing is written in a real host either, for all three causes | Integration | `SyncScheduleTests` | `InReadOnlyMode_TheScheduleKeepsRunning_AndWritesNothing` | `sync_state` empty | RED |
| The BR-026 closed list does not grow | Architecture | `PermittedServiceWriteTests`, `ReadOnlyEnforcementTests` | `TheRegistry_DeclaresOnlyWritesOnTheClosedList`, `TheApplicationAssembly_HasNoUnprotectedWritePath` (existing) | still green — the run is a guarded write path (FR-006) | GUARD |

## AC-005 Without a saved connection there is nothing to synchronize

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| No connection saved | Unit | `SynchronizationRefusalTests` | `WithoutAConnection_TheRunIsSkipped_AndNothingIsWritten` | `NotConfigured`, nothing written | RED |
| A connection for another domain | Unit | `SynchronizationRefusalTests` | `WithAConnectionForAnotherDomain_TheRunIsSkipped` | `DomainMismatch`, nothing written | RED |
| The two skips are distinguishable | Unit | `SynchronizationRefusalTests` | `TheTwoSkips_AreDistinguishable` | reason vs connection state | RED |
| Distinguishable in the log too | Integration | `SyncLoggingTests` | `TheTwoSkipReasons_AreDifferent` | different `Reason` | RED |

## AC-006 A failed run is recorded and does not stop the service

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| A failure keeps the instant of the last success (FR-008) | Unit | `SynchronizationRunTests` | `AFailedRun_LeavesTheInstantOfTheLastSuccessUntouched` | last success unchanged | RED |
| The stored message is a category plus a sentence (S-06) | Unit | `SynchronizationRunTests` | `AFailedRun_StoresACategoryAndAShortMessage` | ≤ 512, carries a category | RED |
| A long message is truncated, not rejected (db-design §3.3) | Unit | `SyncStateInvariantTests` | `ALongMessage_IsTruncatedToTheStoredLength` | exactly 512 | RED |
| A failure without a message is rejected | Unit | `SyncStateInvariantTests` | `AFailureWithoutAMessage_IsRejected` (2 values) | rejected | RED |
| A negative counter, an end before the start | Unit | `SyncStateInvariantTests` | `ANegativeCounter_IsRejected`, `AnEndBeforeTheStart_IsRejected` | rejected | RED |
| The database rejects the same three disagreements | Integration | `SyncStateSchemaTests` | `ATerminalFieldsDisagreement_IsRejected` (3 rows), `ANegativeProcessedCount_IsRejected`, `AFinishedAtEarlierThanStartedAt_IsRejected`, `AnUnknownStatus_IsRejected` | `23514` on the named constraint | RED |
| A failed run is logged at `Error` (I-7) | Integration | `SyncLoggingTests` | covered by the event name in `SyncTestData.LogEvents.RunFailed`; the level is asserted where the failure is producible — see §10 of the strategy | RED |

## AC-007 Every line of a run is traceable and carries no personal data

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| Run start at `Information` | Integration | `SyncLoggingTests` | `ARunStart_IsLoggedAtInformation` | level `Information` | RED |
| Run finish at `Information` with the counter (DC-10) | Integration | `SyncLoggingTests` | `ARunFinish_IsLoggedAtInformation_WithTheCounter` | `ProcessedCount` = 0 | RED |
| Every line of a run carries the run identifier | Integration | `SyncLoggingTests` | `EveryLineOfARun_CarriesTheRunIdentifier` | both lines carry it | RED |
| A skip is `Information` with a category (I-4) | Integration | `SyncLoggingTests` | `ASkippedRun_IsLoggedAtInformation_WithItsReason` (3 causes) | level and `Reason` | RED |
| No personal data, no connection string (SC-10) | Security | `SyncLoggingTests` | `TheLog_CarriesNoPersonalDataAndNoConnectionString` | absent | RED |
| A scheduled run writes no audit row (SC-11, FR-017) | Security | `SyncLoggingTests` | `AScheduledRun_WritesNoAuditRow` | `audit_event` empty | RED |

## AC-008 Shutdown is clean

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| A stop starts no new run | Integration | `SyncScheduleTests` | `HostStop_StartsNoNewRun` | the row keeps the first run | RED |
| A stop is not a failed run (FR-010, I-3) | Integration | `SyncScheduleTests` | `HostStop_IsNotRecordedAsAFailedRun` | status ≠ `failed`, no error | RED |

## AC-009 Tests never reach Google and never sleep

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| No Google port is involved at all | Unit | `SyncWorld` (fixture) | — | the fixture registers no Google port; the pipeline is empty (OD-001), so no call can be made | GUARD |
| A use case holding a Google port must take the guard | Architecture | `GoogleDataPortRuleTests` (existing) | US-007 FR-007 rule | still green — binds whatever port US-014 adds | GUARD |
| The schedule is driven by the injected clock | Integration | `SyncScheduleTests`, `SyncRunCoordinationTests` | every method uses `ManualTimeProvider` (`AdvanceWhenDueAsync`, `WaitForTimerAtAsync`) | no `Task.Delay` on production timing; log assertions wait for the event | RED |
| Integration tests run on real PostgreSQL (TC-2) | Integration | every class taking `PostgreSqlFixture` | — | Testcontainers; the InMemory provider is never used | GUARD |

## Configuration (spec FR-013, VR-001)

| Scenario | Level | Test class | Test method | Expected result | Status |
|---|---|---|---|---|---|
| Absent → the default of one hour (I-6) | Configuration | `SyncIntervalConfigurationTests` | `AbsentSetting_ResolvesToTheDefaultInterval` | one hour | RED |
| Blank after trimming → the default | Configuration | `SyncIntervalConfigurationTests` | `BlankSetting_ResolvesToTheDefaultInterval` | one hour | RED |
| A valid value is used | Configuration | `SyncIntervalConfigurationTests` | `AValidSetting_ResolvesToThatInterval` | 15 minutes | RED |
| Invalid values stop the start, naming the key and not the value | Configuration | `SyncIntervalConfigurationTests` | `InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue` (7 values) | the host refuses to start | RED |
| The boundaries `1` and `1440` are accepted | Configuration | `SyncIntervalConfigurationTests` | `BoundaryValidSetting_HostStarts` (2 values) | the host starts | RED |

## Coverage statement

Every Acceptance Criterion AC-001 … AC-009 has at least one mapped scenario, and
every mandatory one has a `RED` test that must turn green. Nothing in the matrix
maps to an HTTP contract, an authorization policy or a translation key, because the
Story has none (api-design §1, spec FR-016, FR-018) — that absence is itself held
in place by the existing endpoint-enumeration and registry guards.
