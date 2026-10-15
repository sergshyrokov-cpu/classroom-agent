---
artifact_type: test_generation_report
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T09:10:00Z
updated_at: 2026-10-15T09:10:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 3
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
  - path: docs/designs/database/US-038-entity-model.md
    version: 1
  - path: docs/tests/US-038-test-strategy.md
    version: 1
  - path: docs/tests/US-038-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-038 Test Generation Report

Story: **US-038** — erase all data of one participant on request.
Stage: TEST_WRITING, attempt 1. The run was interrupted after the test strategy,
the matrix, the tests and the skeleton were written; it was resumed to verify
them, run the suite and write this report. Nothing was regenerated.

## 1. Test files created

| File | Level |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/EraseParticipantUseCaseTests.cs` | unit |
| `tests/ClassroomAgent.Tests/Application/UseCases/LiftErasureExclusionUseCaseTests.cs` | unit |
| `tests/ClassroomAgent.Tests/Application/UseCases/ErasureQueryTests.cs` | unit |
| `tests/ClassroomAgent.Tests/Application/UseCases/ErasureInvariantTests.cs` | unit |
| `tests/ClassroomAgent.Tests/Web/UseCases/ParticipantErasureTests.cs` | integration |
| `tests/ClassroomAgent.Tests/Web/UseCases/ErasureSearchTests.cs` | integration |
| `tests/ClassroomAgent.Tests/Web/UseCases/ErasureExclusionSyncTests.cs` | integration |
| `tests/ClassroomAgent.Tests/Web/UseCases/ErasureExclusionMeetTests.cs` | integration |
| `tests/ClassroomAgent.Tests/Web/UseCases/ErasureReadOnlyHttpTests.cs` | HTTP |
| `tests/ClassroomAgent.Tests/Infrastructure/Persistence/ParticipantErasureSchemaTests.cs` | persistence |
| `tests/ClassroomAgent.Tests/Web/Security/ErasureAuthorizationTests.cs` | HTTP / security |
| `tests/ClassroomAgent.Tests/Web/Pages/ErasurePageTests.cs` | HTTP |
| `tests/ClassroomAgent.Tests/Web/Localization/ErasureTranslationTests.cs` | translation |
| `tests/ClassroomAgent.Tests/Web/Logging/ErasureLoggingTests.cs` | log |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ErasureHostExtensions.cs` | fixture (seeded world, by SQL) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ErasureTestData.cs` | fixture (names, constants) |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ErasureUnitWorld.cs` | fixture (substituted ports) |

## 2. Test files modified

Each change encodes the migration `AddParticipantErasure` of db-design §11:

| File | Change |
|---|---|
| `Infrastructure/Persistence/AppUserMigrationTests.cs` | two new tables; 19 migrations, the last `AddParticipantErasure` |
| `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` | five count columns and five constraints (db-design §9) |
| `Infrastructure/Persistence/MeetSchemaTests.cs` | `meet_session.organizer_email` nullable (db-design §4) |
| `Web/Persistence/CourseSchemaTests.cs` | `ix_course_owner_google_id` (db-design §6) |
| `Web/Persistence/DeanAccountAuditSchemaTests.cs` | `audit_event` has 36 columns |
| `TestInfrastructure/FakeClassroomReader.cs` | optional `ownerGoogleId` for a course (FR-012 tests); default unchanged |

## 3. Compile-only skeleton (OD-002, resolved a)

Production declarations the tests compile against; members throw
`NotImplementedException`; nothing registered in DI, no migration, no route, no
translation. IMPLEMENTATION owns and completes them.

- Domain: `Entities/ErasedParticipant.cs`, `Entities/ErasureSalt.cs`,
  `Rules/EmailFingerprint.cs`, `Rules/ErasureCounts.cs`; amended
  `Entities/AuditEvent.cs` (five counts, three factories),
  `Enums/AuditAction.cs`, `Enums/AuditTargetType.cs` (two values each).
- Application ports: `IErasedParticipantRepository`, `IErasureFence`,
  `IErasureSaltStore`, `IParticipantErasureReadSource`,
  `IParticipantErasureStore`.
- Application use cases and queries: `EraseParticipantUseCase`,
  `LiftErasureExclusionUseCase`, `SearchParticipantsForErasureQuery`,
  `GetErasureConfirmationQuery`; `Validation/ErasureMark.cs`.
- Application models: `ErasureConfirmationData`, `ErasureCourseRecord`,
  `ExclusionLiftingOutcome`, `ExclusionLiftingResult`,
  `ParticipantErasureOutcome`, `ParticipantErasureResult`,
  `ParticipantErasureTarget`, `ParticipantSearchPage`,
  `ParticipantSearchRecord`; DTOs `ErasureConfirmationPageModel`,
  `ErasureCourseEntry`, `ErasureCourseRole`, `ErasureMarkError`,
  `LiftFieldError`, `ParticipantSearchResult`, `SearchBlock`,
  `SearchFieldError`.
- Infrastructure: `AuditEventConfiguration` ignores the five new count
  properties until the migration maps them — IMPLEMENTATION removes the five
  `Ignore` lines.

`MeetSession.OrganizerEmail` and `RunSynchronizationUseCase` are unchanged, as
OD-002 fixes.

## 4. Commands

Run by a `cheap-worker` subagent; the console output was parsed for the counts
below.

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 errors, 0 warnings |
| `docker info` | Docker 29.8.0 running |
| `dotnet test ClassroomAgent.sln --output Detailed` | 5917 total, 5680 passed, 237 failed, 0 skipped; 6 min 45 s; exit code 2 |

A `--logger trx` / `--report-trx` run reported zero tests under the
Microsoft.Testing.Platform runner of `global.json`; the console run was used
instead.

## 5. Results

### 5.1 Existing tests

All 5680 pre-existing tests that were not amended pass. No regression.

The six amended schema tests (§2) fail, as expected: they assert the schema
after `AddParticipantErasure`, which does not exist yet.

| Test | Failure |
|---|---|
| `AppUserMigrationTests.TheMigrations_CreateLegitimacyStateThenAppUserAndAuditEvent` | expected `erased_participant`, found `legitimacy_state` |
| `CourseSchemaTests.TheOnlyIndexes_AreThePrimaryKeyAndTheGoogleId` | `ix_course_owner_google_id` missing |
| `MeetSchemaTests.MeetSession_HasExactlyTheDesignedColumns` | `organizer_email` still `NOT NULL` |
| `InstallationAuditEventSchemaTests.Columns_MatchTheDesign` | count columns missing |
| `InstallationAuditEventSchemaTests.PrimaryKeyAndConstraints_AreNamedAsTheDesignFixesThem` | erasure constraints missing |
| `DeanAccountAuditSchemaTests.TheTable_GainsNoColumn` | 31 columns, 36 expected |

### 5.2 New tests: failing as expected (231)

| Class | Failed | Passed | Failure causes (all from missing production behaviour) |
|---|---|---|---|
| `EraseParticipantUseCaseTests` | 16 | 0 | skeleton `NotImplementedException` (the class's static data builds an `ErasureCounts`, whose skeleton constructor throws) |
| `LiftErasureExclusionUseCaseTests` | 21 | 0 | skeleton `NotImplementedException` |
| `ErasureQueryTests` | 16 | 0 | skeleton `NotImplementedException` |
| `ErasureInvariantTests` | 32 | 1 | skeleton `NotImplementedException` |
| `ParticipantErasureTests` | 20 | 0 | use case not registered in DI (17); tables `erasure_salt` / `erased_participant` missing (3) |
| `ErasureSearchTests` | 18 | 0 | the two queries not registered in DI |
| `ErasureExclusionSyncTests` | 7 | 0 | `erasure_salt` missing (5); no fence, the wait times out (1); use case not registered (1) |
| `ErasureExclusionMeetTests` | 5 | 0 | `erasure_salt` missing (4); use case not registered (1) |
| `ParticipantErasureSchemaTests` | 29 | 0 | migration absent: tables, columns, indexes, checks missing; `organizer_email` still `NOT NULL` |
| `ErasureAuthorizationTests` | 18 | 7 | no route (`404` instead of `200`/`403`/`400`); policy `EraseParticipantData` not defined; audit columns missing |
| `ErasurePageTests` | 21 | 1 | no route / page; `erasure_salt` missing |
| `ErasureReadOnlyHttpTests` | 16 | 0 | no route (`404` instead of `200`/`409`); `erasure_salt` missing |
| `ErasureTranslationTests` | 7 | 0 | `Erasure.*` keys missing in `uk` and `en`; page absent |
| `ErasureLoggingTests` | 5 | 0 | no route; `erasure_salt` missing |

Every failure is an unregistered service, a missing table/column/policy/route/
translation key, or a skeleton `NotImplementedException`. None is a compile
error, a host misconfiguration, a fixture fault or a wrong assertion.

### 5.3 New tests that pass before implementation (9)

Each was examined; none is weak, and each must stay green after implementation.

| Test | Why it passes now | Why it is kept |
|---|---|---|
| `ErasureInvariantTests.TheExclusion_HasNoPropertyForANameOrAnEmail` | the skeleton `ErasedParticipant` already has the designed shape (entity model §3) | guards AC-006 against a later name or email property |
| `ErasureAuthorizationTests.AGetToTheErasureAddress_ErasesNothing` | no route: nothing happens | AC-020 — after implementation a GET must still change nothing |
| `ErasureAuthorizationTests.AMalformedParticipantId_Is404` ×4 (`abc`, 20 digits, `-5`, `0`) | no route: `404` | AC-015 — the route constraint must keep answering `404` |
| `ErasureAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange` ×2 (Page, Erasure) | the existing forced-change filter redirects any path | AC-008 — must hold once the routes exist |
| `ErasurePageTests.TheHomePage_LinksToTheErasurePage_ForTheAdminOnly(Dean)` | no link exists for anyone | AC-008 — the Dean must still see none; the Admin variant fails |

These are "X does not happen" tests; their Admin / positive counterparts fail
in the red phase, so together the scenario is not vacuous.

## 6. Untested Acceptance Criteria

None. All 21 AC (AC-001 … AC-021) map to tests in the matrix; all 118 test
methods named in the matrix exist in the sources. Excluded scenarios and known
limitations are in the test strategy §8, §9 (notably: `SyncWorld` must receive
the new constructor arguments when IMPLEMENTATION extends
`RunSynchronizationUseCase`).

## 7. Open Decisions

- OD-001 — resolved (a) at SPECIFICATION; AC-019 tested as resolved.
- OD-002 — raised at TEST_WRITING, resolved (a) by the Owner on 2026-10-15:
  compile-only skeleton (§3).

No open decision remains.

## 8. Delegation

Build, Docker check and the full test run were delegated to a `cheap-worker`
subagent. Test design, the skeleton, the matrix check and the red-phase
classification were done by the test-writer. Which parts of the test code the
interrupted run delegated is not recorded.

## 9. Overall result

**PASS** — tests compile, the build is clean, existing tests pass, every new
failure is caused by missing production behaviour, and every pre-implementation
pass is explained.
