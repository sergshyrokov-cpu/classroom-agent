---
artifact_type: test_generation_report
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T12:19:58Z
updated_at: 2026-10-28T12:19:58Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/designs/database/US-065-db-design.md
    version: 1
  - path: docs/designs/database/US-065-entity-model.md
    version: 1
  - path: docs/tests/US-065-test-strategy.md
    version: 1
  - path: docs/tests/US-065-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-065 Test Generation Report

## 1. Result

**PASS (red phase verified).** 118 new test cases in 8 classes plus 3
adapted existing tests. Every one of AC-001 … AC-021 has at least one test
(`docs/tests/US-065-ac-test-matrix.md`). The solution builds with 0 warnings and
0 errors. All existing tests that were run are green. Of the new cases, 111 fail
because the behaviour is not implemented yet, and 7 pass by design (§5).

## 2. Files

Created (tests):

- `tests/ClassroomAgent.Tests/Application/UseCases/EnterErasureExclusionUseCaseTests.cs` (36 cases)
- `tests/ClassroomAgent.Tests/Infrastructure/Persistence/ErasureExclusionInAdvanceSchemaTests.cs` (25)
- `tests/ClassroomAgent.Tests/Web/UseCases/ErasureExclusionEntryTests.cs` (7)
- `tests/ClassroomAgent.Tests/Web/UseCases/ErasureExclusionInAdvanceSyncTests.cs` (15)
- `tests/ClassroomAgent.Tests/Web/Pages/ErasureExclusionEntryPageTests.cs` (13)
- `tests/ClassroomAgent.Tests/Web/Security/ErasureExclusionEntryAccessTests.cs` (13)
- `tests/ClassroomAgent.Tests/Web/Localization/ErasureExclusionEntryTranslationTests.cs` (5)
- `tests/ClassroomAgent.Tests/Web/Logging/ErasureExclusionEntryLoggingTests.cs` (4)

Modified (tests and fixtures):

- `TestInfrastructure/ErasureUnitWorld.cs` — `Enter`; read-source fake `FindParticipantsByEmailAsync` (`LoadedRows`, `EmailLookups`); exclusion fake `AnyWithFingerprintAsync` (`"exclusion:any"`), the two sync members throw; the id lookup no longer assumes a non-null id.
- `TestInfrastructure/SyncWorld.cs` — `NoErasures` implements the three new port members.
- `TestInfrastructure/ErasureHostExtensions.cs` — `SeedExclusionInAdvanceAsync`, `EnterExclusionAsync`, `PostEntryAsync`; `ExclusionRow.GoogleUserId` nullable; the audit query includes `erasure_exclusion_entered`.
- `TestInfrastructure/ErasureTestData.cs` — path, pattern, operation, markup hooks, keys, code, names (strategy §4–§6).
- `Infrastructure/Persistence/ParticipantErasureSchemaTests.cs` — adapted: `TheExclusionTable_HasExactlyTheDesignedColumns` (id nullable); `AnExclusion_CannotBeChanged_ButCanBeDeleted` asserts `check_violation` only, because the rule is now the trigger (db-design §2.2–§2.3).
- `Web/Security/PermissionMatrixTests.cs` — row `POST settings/erasure/exclusions` → `EraseParticipantData`.

## 3. Compile-only skeleton

Every body throws `NotImplementedException`. Nothing is registered in DI. No migration, route, view or translation was added. Existing behaviour is unchanged.

| Declaration | File |
|---|---|
| `ErasedParticipant.EnterInAdvance(EmailFingerprint, DateTimeOffset)` | `src/ClassroomAgent.Domain/Entities/ErasedParticipant.cs` |
| `AuditAction.ErasureExclusionEntered` | `src/ClassroomAgent.Domain/Enums/AuditAction.cs` |
| `AuditEvent.ErasureExclusionEntered(long, long, DateTimeOffset, string?)` | `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` |
| `IErasedParticipantRepository.AnyWithFingerprintAsync`, `GetUnmatchedByFingerprintsAsync`, `FillGoogleUserIdAsync` | `src/ClassroomAgent.Application/Ports/IErasedParticipantRepository.cs`; implementations throw in `src/ClassroomAgent.Infrastructure/Persistence/Repositories/ErasedParticipantRepository.cs` |
| `IParticipantErasureReadSource.FindParticipantsByEmailAsync(string, CancellationToken)` → `IReadOnlyList<ParticipantSearchResult>` | `src/ClassroomAgent.Application/Ports/IParticipantErasureReadSource.cs`; throws in `src/ClassroomAgent.Infrastructure/Persistence/ParticipantErasureReadSource.cs` |
| `UnmatchedExclusion` | `src/ClassroomAgent.Application/Models/UnmatchedExclusion.cs` |
| `ExclusionEntryOutcome`, `ExclusionEntryResult` | `src/ClassroomAgent.Application/Models/ExclusionEntryOutcome.cs`, `ExclusionEntryResult.cs` |
| `EnterErasureExclusionUseCase` (constructor, `Operation = "ErasureExclusion.Enter"`, `ExecuteAsync`) | `src/ClassroomAgent.Application/UseCases/EnterErasureExclusionUseCase.cs` (`#pragma warning disable CS9113` until IMPLEMENTATION reads the dependencies) |

Choices the entity model left open that were made here:

- The "already loaded" lookup is placed on `IParticipantErasureReadSource`, the port that already serves the erasure search (entity model §3).
- The use case's constructor dependencies are fixed.

Left to IMPLEMENTATION because no test needs them yet:

- `ErasedParticipant.GoogleUserId` becoming `string?` and the configuration losing `IsRequired()`. These come with the migration. The test code compiles either way.
- `ErasureMessageKey.ExclusionEntered`, `EntryBlock`, `ErasurePageModel.Enter`, the controller action, the view, the resources, DI, `GetStoredGoogleUserIdsAsync`.

## 4. Commands and runs

The commands run, in order:

1. `dotnet build ClassroomAgent.sln` → 0 warnings, 0 errors.
2. `dotnet test ClassroomAgent.sln --no-build --filter "FullyQualifiedName~<Class>"`, one class per run.
3. `--filter "FullyQualifiedName~ClassroomAgent.Tests.Application"` for every unit test.

Docker was running, with 5.3 GB of RAM free.

| Class | Total | Failed | Passed |
|---|---|---|---|
| EnterErasureExclusionUseCaseTests | 36 | 35 | 1 |
| ErasureExclusionInAdvanceSchemaTests | 25 | 23 | 2 |
| ErasureExclusionEntryTests | 7 | 7 | 0 |
| ErasureExclusionInAdvanceSyncTests | 15 | 14 | 1 |
| ErasureExclusionEntryPageTests | 13 | 12 | 1 |
| ErasureExclusionEntryAccessTests | 13 | 11 | 2 |
| ErasureExclusionEntryTranslationTests | 5 | 5 | 0 |
| ErasureExclusionEntryLoggingTests | 4 | 4 | 0 |
| ParticipantErasureSchemaTests (adapted) | 29 | 1 | 28 |
| PermissionMatrixTests (adapted) | 3 | 1 | 2 |
| Regression: LiftErasureExclusionUseCaseTests, ErasureExclusionSyncTests, ErasureExclusionMeetTests, ErasurePageTests, ErasureAuthorizationTests | 80 | 0 | 80 |
| All Application unit tests | 2082 | 35 (all new) | 2047 |

The schema class was first run with 3 passing. `AnEntryAuditRowWithErasureCounts_IsRejected` passed vacuously, because the old action check rejected the row. It was strengthened to assert `ck_audit_event_erasure_counts_absent` and now fails as it should.

## 5. Red-phase classification

Each new test fails for a reason that comes from missing production behaviour:

- **Unit:** `NotImplementedException` from the skeleton. The 5 read-only cases get `NotImplementedException` where they expect `ReadOnlyModeException`.
- **Schema, sync and seeding:** `23502 null value in column "google_user_id"`, because the migration is missing. `ck_audit_event_action` rejects the new code. The trigger, the migration id and the shape check are absent. The column list differs.
- **Integration entry:** the use case is not registered in DI. In the race test the entry did not wait, because it failed at once.
- **HTTP:** the route is absent (status differs), or the markup is absent. Translation keys are missing. Logging cases fail on the expected status (302 / 400 / 200) before the log is read.
- **Permission matrix:** drift — the row has no endpoint yet.

Tests that pass by design today. Each one asserts that something is *not* done, and must stay green after IMPLEMENTATION:

| Test | Why it passes now |
|---|---|
| U `TheExclusion_StillHasNoPropertyForANameOrAnEmail` | Guards S-05 against a new name or email property |
| DB `AnExclusionFromAnErasure_CannotBeUpdated` (2 cases) | Today's immutability check rejects; afterwards the trigger must |
| SY `AnExclusionFromAnErasure_IsNotMatchedByFingerprint_NorChanged` | Today nothing matches by fingerprint; afterwards I-1 must hold |
| A `ADeanWithATemporaryPassword_IsSentToTheForcedChange` | The restricted session redirects every path |
| A `AGetToTheEntryAddress_StoresNothing` | No route yet; afterwards a GET must still store nothing |
| P `AQueryStringEmail_IsNotAnEntry` | Guards S-08 |

Adapted `AnExclusion_CannotBeChanged_ButCanBeDeleted` passes both before and after: the check today, the trigger afterwards.

Unexpected failures: none. Environment failures: none.

## 6. Delegation

**Done inline:**

- Test design, strategy and matrix.
- The skeleton.
- Unit, schema, entry-integration and sync tests.
- Fixture changes.
- Red-phase verdict.

**Delegated to a `cheap-worker`** against a fixed brief (names, hooks and helpers given):

- The four HTTP classes: page, access, translation, logging.

I checked the page class by reading it and checked all four by their red-phase failure messages.

## 7. Findings

**Closed:** DB_DESIGN N-1. These tests prove that the trigger exists and is enabled, row-level, before update, calling `erased_participant_fill_only`, and that it rejects a second update:

- `TheFillOnlyTrigger_ExistsOnTheTable_BeforeUpdate_ForEachRow`
- `AFilledExclusion_CannotBeChangedAgain`
- `AnExclusionFromAnErasure_CannotBeUpdated`
- `AFillThatAlsoChangesAnotherColumn_IsRejected`
- `AnUpdateOfAnUnmatchedExclusion_ThatDoesNotFillIt_IsRejected`

**INFO-1** (for IMPLEMENTATION):

- db-design §4 step 1 says "`FindAsync` once per run". Taken literally — reading the salt before the first course transaction — it would miss the salt created by an entry that the course transaction waited for.
- `AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite(saltBefore: false)` proves the spec FR-006/FR-011 behaviour.
- The run must read the salt inside the course transaction, after the fence, while none has been found. It may keep the salt once found, as the Meet load already does (`progress.Salt ??=`).

**INFO-2:** the trigger raises `check_violation` with no constraint name in the design. The tests therefore assert only the SQLSTATE for trigger rejections. A test cannot tell which rule refused a row.

## 8. Untested Acceptance Criteria and Open Decisions

None untested. No Open Decision is open (OD-001 … OD-003 resolved).
