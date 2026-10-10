---
artifact_type: test_generation_report
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T06:31:21Z
updated_at: 2026-10-10T06:31:21Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: docs/designs/database/US-031-entity-model.md
    version: 1
  - path: docs/tests/US-031-test-strategy.md
    version: 1
  - path: docs/tests/US-031-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 2
supersedes: null
---

# US-031 Test Generation Report

## 1. Result

**PASS — red phase verified.** `dotnet build ClassroomAgent.sln`: 0 warnings,
0 errors. Full suite (`dotnet test ClassroomAgent.sln`, Docker running):
**3982 total, 3735 passed, 247 failed, 0 skipped**, 6 m 18 s.

- 245 failures are expected red (§3).
- 2 failures — `SetupValidationTests.Login_Boundaries` (one case) and
  `LegitimacyCheckScheduleTests.OnlyOneCheckRunsAtATime_EvenWhenACallHangs` —
  were Npgsql connection timeouts under load; re-run of both classes: 44/44
  passed. Environment, not a regression.
- **No existing regression test fails for any other reason.**

## 2. Production skeleton (OD-011 a, Owner 2026-10-10)

Compile-only; every new behaviour throws `NotImplementedException`; no EF
mapping, no migration, no view, no translation. IMPLEMENTATION owns and
completes it.

- **Domain:** `Enums/SyncStep`; `Rules/MeetConnection` (data, in full),
  `Rules/SchoolDomainAccount` (throws); `Entities/MeetSession`,
  `MeetParticipation` (constants and properties; `Store`, `Record`,
  `IsOtherParticipant` throw); `SyncState`: get-only throwing `MeetLoadedUpTo`,
  `FailedStep`, throwing `CompleteRun(finishedAt, count, meetLoadedUpTo)` and
  `FailRun(finishedAt, count, diagnosis, step)`; `RetentionPurgeCounts` gains
  `MeetSessions`, `MeetParticipations`; `AuditEvent`: get-only throwing
  `PurgedMeetSessions`, `PurgedMeetParticipations` (get-only so EF does not map
  them before the migration).
- **Application:** ports `IMeetReportsReader`, `IMeetSessionRepository`;
  models `MeetEventPage`, `MeetCallEndedEvent` (data, in full),
  `MeetEventRejection`, `MeetPullCounts`; `IRetentionPurgeStore` gains
  `GetExpiredMeetSessionIdsAsync`, `DeleteMeetSessionsAsync`;
  `LastSynchronizationView` gains optional `MeetLoadedUpTo` (`DateTime?`,
  school-local) and `FailedStep`; `SynchronizationRunOutcome` gains throwing
  `Meet` and `FailedStep`; `GetLastSynchronizationQuery` takes `SchoolTimeZone`;
  `RunSynchronizationUseCase` takes `IMeetReportsReader`,
  `IMeetSessionRepository` and has a throwing, uncalled `PullMeetAsync`.
- **Infrastructure:** `Google/GoogleMeetReportsReader` (constructor as
  `GoogleClassroomReader`; `ReadCallEndedAsync` throws),
  `Persistence/Repositories/MeetSessionRepository` (throws),
  `RetentionPurgeStore` (two throwing methods).
- **Web:** `InstallationServices` registers `IMeetReportsReader` and
  `IMeetSessionRepository` (the run cannot be constructed without them; tests
  substitute the port).

**Binding notes for IMPLEMENTATION**

1. Replace the old `SyncState.CompleteRun(2)` / `FailRun(3)` with the new ones
   (entity model §4); existing tests were already migrated to the new
   signatures (§5), so no test edit is needed.
2. Turn the throwing get-only `SyncState` and `AuditEvent` properties into
   mapped `{ get; private set; }` properties with the configuration and the
   migration `AddMeetPull` (db-design §8).
3. **Names the tests fix** (the spec and API design leave them open):
   - log events `SyncMeetStepCompleted` (Information; properties `RunId`,
     `WindowFrom`, `WindowTo`, `EventsRead`, `SessionsAdded`,
     `SessionsUpdated`, `ParticipationsAdded`, `ParticipationsUpdated`,
     `NotOfTheSchool`, `Skipped`) and `SyncMeetEventsSkipped` (Warning;
     `Reason` = a `MeetEventRejection` name, `Count`); the existing
     `SyncRunFailed` line gains property `Step` (`Meet` / `Classroom`);
   - translation keys `LastSync.Meet.LoadedUpTo`, `LastSync.Meet.NotLoaded`,
     `LastSync.Step.Classroom`, `LastSync.Step.Meet`;
   - the watermark rendered as the request culture's short date + ` HH:mm`
     of the school-local time, with no `UTC` suffix (`MeetTestData.FormatLocal`);
   - the email identifier type is `email_address` (Google's documented value;
     part of the live check of OD-009 / `trebovaniya.md` §7 item 28);
   - `MeetPullCounts` and `MeetEventRejection` as declared in the skeleton
     (entity model allowed TEST_WRITING to fix signatures it leaves open).
4. The adapter's configuration answer in the tests is a 403
   `accessNotConfigured` → `Configuration` / `ApiNotEnabled`, as for Classroom.

## 3. Files

Created (tests):
- `Application/UseCases/MeetPullTests.cs`, `MeetWindowTests.cs`,
  `MeetStepFailureTests.cs`, `MeetEventValidationTests.cs`,
  `MeetReadOnlyTests.cs`, `SchoolDomainAccountTests.cs`,
  `MeetSessionInvariantTests.cs`, `SyncStateMeetTests.cs`,
  `LastSynchronizationMeetQueryTests.cs`, `RetentionPurgeMeetAuditEventTests.cs`,
  `RetentionPurgeMeetTests.cs`
- `Infrastructure/Google/GoogleMeetReportsReaderTests.cs`,
  `GoogleMeetReportsReaderRetryTests.cs`
- `Infrastructure/Persistence/MeetSchemaTests.cs`, `MeetSessionRepositoryTests.cs`
- `Web/BackgroundServices/MeetPullHostTests.cs`
- `Web/Pages/LastSynchronizationMeetBlockTests.cs`
- `Web/Localization/MeetPullTranslationTests.cs`
- `Web/Logging/MeetPullLoggingTests.cs`
- `TestInfrastructure/MeetTestData.cs`, `FakeMeetReportsReader.cs`, `MeetReadCall.cs`

Modified (tests): `TestInfrastructure/SyncWorld.cs` (Meet port, in-memory
meetings, read-only reason), `InstallationFactory.cs`, `InstallationTestHost.cs`
(substituted Meet port); signature migration in
`Application/UseCases/GetLastSynchronizationQueryTests.cs`,
`SyncStateInvariantTests.cs`, `SynchronizationRunTests.cs`,
`RetentionPurgeAuditEventTests.cs`, `RetentionPurgeTests.cs`,
`CourseWorkFailureTests.cs`, `CourseWorkImportTests.cs`,
`SubmissionImportTests.cs`, `SubmitterWithoutRosterTests.cs`,
`Infrastructure/Persistence/SyncDiagnosisPersistenceTests.cs`.

Production files: the skeleton of §2. Artifact: OD-011 added to
`docs/decisions/US-031-open-decisions.md` (v2).

Commands: `dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln`;
per-class `dotnet test … --filter FullyQualifiedName~<Class>`.

**Delegation.** Test design, the skeleton, the fixtures, the Application/Domain
tests and the red-phase verdict were done inline. Three `cheap-worker` agents
wrote, from fully specified scenarios, (1) the adapter tests, (2) the schema,
purge, repository and host tests, (3) the page, translation and logging tests,
and ran their own classes; their files were then built and run here in the full
suite.

## 4. Red-phase classification (245 expected failures)

| Cause | Count | Classes |
|---|---|---|
| `NotImplementedException` from the skeleton, or the Meet step not yet called (empty calls, nothing stored, key not found) | 131 | `MeetPullTests` 20, `MeetEventValidationTests` 26, `MeetStepFailureTests` 12, `MeetWindowTests` 8, `MeetReadOnlyTests` 1 (control), `SchoolDomainAccountTests` 14, `MeetSessionInvariantTests` 13, `SyncStateMeetTests` 8, `LastSynchronizationMeetQueryTests` 6, `RetentionPurgeMeetAuditEventTests` 5, `MeetSessionRepositoryTests` 5, `GoogleMeetReportsReader*Tests` 13 |
| Existing tests migrated to the new `SyncState` signatures (throwing) | 44 | `GetLastSynchronizationQueryTests` 17, `SyncStateInvariantTests` 16, `SyncDiagnosisPersistenceTests` 9, `SynchronizationRunTests` 2 |
| No migration yet: 42P01 / 42703 | 46 | `MeetSchemaTests` 37, `RetentionPurgeMeetTests` 9 |
| Meet step not wired into the host run | 4 | `MeetPullHostTests` |
| Missing translation keys / page content | 16 | `LastSynchronizationMeetBlockTests` 12, `MeetPullTranslationTests` 4 |
| Log event not written (timeout waiting for it) | 4 | `MeetPullLoggingTests` |

Unexpected failures: **none**.

**Green by construction (5).** `MeetReadOnlyTests.InReadOnlyMode_TheMeetPortReceivesNoCall`
(3 reasons), `WithNoUsableConnection_TheMeetPortReceivesNoCall` and
`LastSynchronizationMeetQueryTests.WithNoWatermark_TheViewCarriesNone` assert
an absence, which the skeleton satisfies by doing nothing. Each has a red
control in its class (`OutsideReadOnlyMode_TheMeetPortIsCalled`; the summer,
winter and running watermark tests), so the absence becomes meaningful once
the control is green.

## 5. Untested Acceptance Criteria

None. Every AC-001 … AC-015 maps to tests in `docs/tests/US-031-ac-test-matrix.md`.

## 6. Open Decisions

OD-009, OD-010 (resolved at the gate) and OD-011 (resolved by the Owner,
2026-10-10, option a). No open decision remains.
