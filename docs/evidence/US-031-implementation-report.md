---
artifact_type: implementation_report
story: US-031
version: 2
status: DRAFT
created_at: 2026-10-10T07:37:19Z
updated_at: 2026-10-10T07:56:18Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 2
  - path: docs/designs/api/US-031-api-design.md
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
  - path: docs/evidence/US-031-test-generation-report.md
    version: 1
  - path: docs/reviews/security/US-031-security-review.md
    version: 1
supersedes: docs/evidence/US-031-implementation-report.md@v1
attempt: 2
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-031 Implementation Report — Pull Meet events and keep history beyond 180 days

## 1. Summary

Every synchronization run now has a Meet step after the Classroom step. It reads
the `call_ended` events of Admin Reports as the technical account and stores
domain-organized meetings and their connections, deciding per conference across
the whole run. Writes are idempotent and committed a page at a time. Only a
completed run moves the watermark. A failed run records the step that stopped
it. The retention purge deletes meetings older than N years, in batches of 500,
and its audit row carries the two new counts. The Admin's block shows the
watermark in the school's time zone and the failed step, in uk and en.
Migration `AddMeetPull` creates and changes the schema exactly as db-design
§2–§5 specify.

Security-sensitive: personal data of minors (domain emails), Google access
(read-only, technical account, reports scope only) and logging (no event value).
See §7 for the deviations.

Validation (attempt 2): build 0 warnings / 0 errors; `dotnet test ClassroomAgent.sln` **3986 / 3986 passed**, 0 skipped; `dotnet format --verify-no-changes` clean.

**Attempt 2** (loop-back from `HUMAN_PR_APPROVAL`, rejected 2026-10-10T07:49:51Z): fixes security review finding M-1 only — see §9.

## 2. Source Artifacts

As listed in the front matter: Story (unversioned), Specification v1 (APPROVED),
Open Decisions v2 (OD-001…OD-011 resolved), API design v1 and OpenAPI v1,
DB design v1, entity model v1, test strategy v1, AC matrix v1, test generation
report v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (`docs/tests/US-031-ac-test-matrix.md`) | Status |
|---|---|---|---|
| AC-001 | `RunSynchronizationUseCase.PullMeetAsync` / `StoreMeetPageAsync`; `MeetSession.Store`; `MeetSessionRepository`; `MeetSessionConfiguration`, `MeetParticipationConfiguration` | `MeetPullTests`, `MeetPullHostTests`, `MeetSessionRepositoryTests` | PASS |
| AC-002 | `MeetSession.Record` (start = min join, end = max leave over all participations); repository loads every participation | `MeetPullTests`, `MeetSessionInvariantTests`, `MeetSessionRepositoryTests` | PASS |
| AC-003 | Upsert on conference id and (session, endpoint); unique indexes in the migration | `MeetPullTests`, `MeetPullHostTests`, `MeetSchemaTests` | PASS |
| AC-004 | Per-conference decision in `StoreMeetPageAsync`, with undecided conferences held across pages (`MeetProgress.Hold/Release`); `SchoolDomainAccount.IsDomainAccount` | `MeetPullTests`, `SchoolDomainAccountTests` | PASS |
| AC-005 | `RunSynchronizationUseCase.ConnectionOf` keeps an email only for an `email_address` identifier of a domain account | `MeetPullTests`, `MeetSessionInvariantTests` | PASS |
| AC-006 | Window in `PullMeetAsync` (`MeetHorizon`, `MeetOverlap`, clamp); `SyncState.CompleteRun(…, meetLoadedUpTo)` only on completion | `MeetWindowTests`, `SyncStateMeetTests` | PASS |
| AC-007 | `ExecuteAsync` tracks the step; `SyncState.FailRun(…, step)`; `failed_step` column; block step line | `MeetStepFailureTests`, `MeetPullHostTests`, `LastSynchronizationMeetQueryTests`, `LastSynchronizationMeetBlockTests`, `GoogleMeetReportsReaderRetryTests` | PASS |
| AC-008 | `GoogleMeetReportsReader` through `GoogleRetryHandler` and the US-017 classification | `GoogleMeetReportsReaderRetryTests`, `MeetStepFailureTests` | PASS |
| AC-009 | `GetLastSynchronizationQuery` converts the watermark with `SchoolTimeZone`; `Views/WorkspaceConnection/Index.cshtml` | `LastSynchronizationMeetQueryTests`, `LastSynchronizationMeetBlockTests` | PASS |
| AC-010 | Unchanged guard at the start of the run covers the Meet step | `MeetReadOnlyTests` | PASS |
| AC-011 | `RunRetentionPurgeUseCase` meeting step; `RetentionPurgeStore.GetExpiredMeetSessionIdsAsync` / `DeleteMeetSessionsAsync`; `AuditEvent.RetentionPurgeRun` counts | `RetentionPurgeMeetTests`, `RetentionPurgeMeetAuditEventTests`, `MeetSchemaTests` | PASS |
| AC-012 | `MeetEventValidator.Check`; `SynchronizationBackgroundService` `SyncMeetStepCompleted` / `SyncMeetEventsSkipped` (counts only) | `MeetEventValidationTests`, `MeetPullLoggingTests` | PASS |
| AC-013 | `MeetCallEndedEvent` carries only the FR-003 values; tables hold only the designed columns | `GoogleMeetReportsReaderTests`, `MeetSchemaTests` | PASS |
| AC-014 | Four keys in `SharedResource.uk.resx` / `.en.resx` | `MeetPullTranslationTests`, `LastSynchronizationMeetBlockTests` | PASS |
| AC-015 | Port substituted in every host test (`InstallationFactory`); adapter tested on scripted responses | `GoogleMeetReportsReaderTests`, `MeetPullHostTests` | PASS |

## 4. Change Set

**Domain**
- `Enums/SyncStep.cs` (new) — FR-010, entity model §3.
- `Rules/MeetConnection.cs`, `Rules/SchoolDomainAccount.cs` (new) — FR-006, FR-008.
- `Entities/MeetSession.cs`, `Entities/MeetParticipation.cs` (new) — FR-005, FR-008, FR-009, entity model §1–§2.
- `Entities/SyncState.cs` — watermark, failed step; `CompleteRun`/`FailRun` replaced (entity model §4).
- `Entities/AuditEvent.cs`, `Rules/RetentionPurgeCounts.cs` — two purge counts (FR-013, db-design §5).

**Application**
- `Ports/IMeetReportsReader.cs`, `Ports/IMeetSessionRepository.cs` (new) — FR-003, entity model §5.
- `Models/MeetEventPage.cs`, `MeetCallEndedEvent.cs`, `MeetEventRejection.cs`, `MeetPullCounts.cs` (new) — FR-003, FR-012.
- `Validation/MeetEventValidator.cs` (new) — VR-001.
- `UseCases/RunSynchronizationUseCase.cs` — the Meet step (FR-001…FR-010).
- `UseCases/RunRetentionPurgeUseCase.cs`, `Ports/IRetentionPurgeStore.cs`, `Models/RetentionPurgeStep.cs` — FR-013, db-design §6.
- `UseCases/GetLastSynchronizationQuery.cs`, `Models/LastSynchronizationView.cs` — FR-011, openapi.
- `Models/SynchronizationRunOutcome.cs` — FR-010, FR-012.
- `Localization/SharedResource.uk.resx`, `.en.resx` — FR-014 (four keys).

**Infrastructure**
- `Google/GoogleMeetReportsReader.cs` (new) — FR-003, FR-004.
- `Persistence/Configurations/MeetSessionConfiguration.cs`, `MeetParticipationConfiguration.cs` (new); `SyncStateConfiguration.cs`, `AuditEventConfiguration.cs` — db-design §2–§5.
- `Persistence/ClassroomAgentDbContext.cs`, `TimestampInterceptor.cs` — DbSets, PC-6 stamping.
- `Persistence/Repositories/MeetSessionRepository.cs` (new) — db-design §7.
- `Persistence/RetentionPurgeStore.cs` — db-design §6.
- `Persistence/Migrations/20261010064711_AddMeetPull.cs` (+ Designer), `ClassroomAgentDbContextModelSnapshot.cs` — db-design §8, PC-2.

**Web**
- `Configuration/InstallationServices.cs` — FR-015 (port and repository registration).
- `BackgroundServices/SynchronizationBackgroundService.cs` — FR-012 log lines, `Step` on `SyncRunFailed`.
- `Views/WorkspaceConnection/Index.cshtml` — FR-011 (watermark line, step line).

**Tests (changed at IMPLEMENTATION)**
- `Web/BackgroundServices/MeetPullHostTests.cs`, `Infrastructure/Persistence/MeetSessionRepositoryTests.cs` — fixture defect fixed (§7.2).
- Schema snapshots following the approved db-design: `Infrastructure/Persistence/AppUserMigrationTests.cs`, `DeanAccountSchemaTests.cs`, `InstallationAuditEventSchemaTests.cs`, `Web/UseCases/AdminLoginCheckEveryTimeTests.cs`, `Web/Persistence/AccessCheckAuditSchemaTests.cs`, `DeanAccountAuditSchemaTests.cs`, `SyncStateSchemaTests.cs` (§7.3).

The TEST_WRITING files (tests, fixtures, test artifacts, OD-011) are listed in the test generation report.
Workflow files are the orchestrator's. No secret, database file or IDE-local file is in the change set.

## 5. Validation Evidence

- `dotnet test ClassroomAgent.sln --no-build` (inline, after a clean build, Docker running): exit 0, **3982 total, 3982 passed, 0 failed, 0 skipped**, 4 min 07 s.

- Targeted runs during implementation (inline): Application/Domain classes
  185/185 after the fix in §7.1; adapter, schema, purge, repository, host and
  logging classes 86 → 84/86, then 86/86 after §7.2; snapshot classes 80/80
  after §7.3.
- `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
- `dotnet format ClassroomAgent.sln --verify-no-changes`: exit 0 (delegated run).

**Delegated:** the view and the translations (one `cheap-worker`, against a fixed
contract; its filtered run: 376/376); the first full validation run (one
`cheap-worker`, see below). The rest — domain, use cases, persistence, adapter,
logging, migration, snapshot updates and the meaning of every failure — was done
inline.

The first delegated full run (`dotnet test ClassroomAgent.sln`, 21 min 48 s)
reported 203 failures. Of these, 190 were Npgsql connection or read timeouts in
35 classes, many in the Control Plane and unrelated to this Story; the same
classes then passed in isolation (e.g. `InstallationTranslationTests`,
`AccessCheckRunTests`, `MeetPullLoggingTests`: 55/56, the one failure being a
snapshot). That was the environment. The rest were schema snapshots (§7.3).

## 6. Configuration Changes

None. No setting, key or `appsettings` entry was added; the adapter uses the
existing service-account reference (SC-7).

## 7. Deviations and Discovered Problems

1. **Per-conference decision across pages.** A first version decided per page,
   so a conference's connections read on an earlier page were lost when its
   domain organizer appeared on a later page (spec FR-005 says "read in this
   run"; caught by `OneDomainOrganizerAmongTheEventsOfAConference…`). Undecided
   conferences' events are now held in memory until the run ends; only those
   undecided events are held, never a whole window (FR-003 memory note).
2. **Two fixture defects in TEST_WRITING tests.** `MeetPullHostTests` and
   `MeetSessionRepositoryTests` cast a `timestamptz` scalar (a UTC `DateTime`
   from Npgsql) to `DateTimeOffset`. In the red phase this never ran because the
   table was missing. The reads now convert explicitly; no assertion changed.
3. **Schema snapshot tests.** Seven existing tests list every table, column,
   constraint or migration exactly. They were extended with what db-design §2–§5
   and §8 add. No assertion was weakened.
4. **Adapter helpers duplicated.** `GoogleMeetReportsReader` carries its own key
   loading, transport factory and failure guard. `GoogleClassroomReader` and
   `GoogleAccessProbe` already each carry such copies, and extracting a shared
   helper would refactor code outside this Story.
5. **`RetentionPurgeStep.ExpiredMeetings`** was added so a failed meeting batch
   is reported like every other purge unit. A failed batch stops the meeting step
   for this run (it would select the same ids again); the next run retries it.
6. **`NotOfTheSchool` counts events**, not meetings. Spec FR-005 says "such
   events are counted", and the tests seed one event per such meeting.

## 8. Open Decisions

None touched or required. OD-009 (`call_ended` field names, `email_address`)
stays open as `trebovaniya.md` §7 item 28 until the Owner checks on a live
domain; the mapping is in `GoogleMeetReportsReader.Map` and
`MeetEventValidator.EmailIdentifierType` alone.

## 9. Attempt 2 — security review M-1

**Reason:** `HUMAN_PR_APPROVAL` was rejected with the instruction to fix M-1 of
`docs/reviews/security/US-031-security-review.md` v1. An organizer or
participant email with an empty part before the `@`, or with more than one `@`,
counted as a domain account. Spec VR-001 and FR-005 say a malformed address is
"not a domain account".

**Change:**
- `src/ClassroomAgent.Application/Validation/MeetEventValidator.cs` —
  `IsUsableEmail` now also requires exactly one `@` with a non-empty part before
  it (VR-001, FR-005). The pure domain rule `SchoolDomainAccount.IsDomainAccount`
  (FR-006, "the part after its last `@`") is unchanged, as is its TEST_WRITING
  test. Both callers in `RunSynchronizationUseCase` (organizer, participant)
  already pass through `IsUsableEmail` before the domain rule.
- `tests/ClassroomAgent.Tests/Application/UseCases/MeetEventValidationTests.cs` —
  two new theories, each with `@<domain>` and `odd@local@<domain>`:
  `AMalformedOrganizer_IsNotADomainAccount_NotAnInvalidEvent` (AC-004, VR-001)
  and `AMalformedParticipant_IsAnOtherParticipant` (AC-005, VR-001). They are not
  yet listed in `docs/tests/US-031-ac-test-matrix.md` v1; they trace to AC-004 and
  AC-005 through this report.

**Evidence:**
- Red phase (inline): `--filter MeetEventValidationTests.AMalformed` — 4 / 4
  failed before the change.
- Green (inline): `MeetEventValidationTests`, `MeetPullTests`,
  `SchoolDomainAccountTests`, `MeetSessionInvariantTests` — 77 / 77.
- Full run (delegated to one `cheap-worker`, read only):
  `dotnet build ClassroomAgent.sln` 0 warnings / 0 errors;
  `dotnet format ClassroomAgent.sln --verify-no-changes` exit 0;
  `dotnet test ClassroomAgent.sln` exit 0, **3986 total, 3986 passed, 0 failed,
  0 skipped** (3 min 53 s).

No configuration, schema, endpoint or dependency change. The Informational
notes I-1…I-3 of the review were not acted on, as the gate decision asked.
