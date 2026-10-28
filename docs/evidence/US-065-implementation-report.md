---
artifact_type: implementation_report
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T13:38:48Z
updated_at: 2026-10-28T13:38:48Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/decisions/US-065-open-decisions.md
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
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-065 Implementation Report — An Admin enters an erasure exclusion by email before the person is loaded

## 1. Summary

The implementation is complete. An Admin can enter an exclusion by email on the erasure page. It works with no Google
connection saved and no salt yet.

- The entry is serialized with synchronization and Meet writes through the exclusive erasure fence.
- It answers "already loaded" or "already excluded" without writing anything.
- On success it stores only the fingerprint and one audit row, then redirects with a one-time message.

Synchronization uses the exclusion in each course transaction:

- It matches a roster entry's email by fingerprint against exclusions that have no Google user id.
- It skips that person exactly as an excluded id is skipped.
- It fills the exclusion's Google user id once, with a guarded set-based `UPDATE`.
- A matched id that is already a stored participant is synchronized as usual (OD-003 a).

The migration `AddErasureExclusionInAdvance` makes the id nullable, re-creates the value check and drops the
immutability check. It adds the fill-only trigger, the new audit action and the entry shape check.

Validation:

- Build: 0 warnings, 0 errors.
- Format: clean.
- Story test classes: all 15 green (215 cases).
- Adapted pre-existing classes: all 5 green.
- Full suite: no single fully green run yet. The first run found 5 failures caused by the code; all are fixed. The second
  run failed only on environment timeouts. See §5.

## 2. Source Artifacts

All are listed in the front matter: Story (unversioned), Specification v2 (APPROVED), Open Decisions v2 (OD-001 … OD-003
resolved a), API design and OpenAPI v1, DB design and entity model v1, test strategy and AC test matrix v1. None is
`SUPERSEDED`.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (ac_test_matrix) | Status |
|---|---|---|---|
| AC-001 | `EnterErasureExclusionUseCase.ExecuteAsync` / `EnterInTransactionAsync`; `ErasedParticipant.EnterInAdvance`; `AuditEvent.ErasureExclusionEntered` | `EnterErasureExclusionUseCaseTests`, `ErasureExclusionEntryTests`, `ErasureExclusionEntryPageTests` | green |
| AC-002 | use case (salt `GetOrCreateAsync`); `ErasureController.Index` reads only the legitimacy mode | `EnterErasureExclusionUseCaseTests.WithNoSaltYet…`, `ErasureExclusionEntryPageTests.ThePage_ShowsTheEntryForm_WithNoGoogleConnectionSaved` | green |
| AC-003, AC-013 | `RunSynchronizationUseCase.AddMatchedInAdvanceAsync` (joins the course's excluded set; owner cleared by the existing rule) | `ErasureExclusionInAdvanceSyncTests` | green |
| AC-004, AC-015 | `ErasedParticipantRepository.FillGoogleUserIdAsync`; trigger `tg_erased_participant_fill_only` | `ErasureExclusionInAdvanceSyncTests`, `ErasureExclusionInAdvanceSchemaTests` | green |
| AC-005 | unchanged Meet load (`GetExcludedFingerprintsAsync`) | `ErasureExclusionInAdvanceSyncTests.TheMeetLoad_…` | green |
| AC-006, AC-014 | `ParticipantErasureReadSource.FindParticipantsByEmailAsync`; view block `erasure-already-loaded` | unit, entry, page tests | green |
| AC-007 | `ErasedParticipantRepository.AnyWithFingerprintAsync` | unit, entry, page tests | green |
| AC-008, AC-021 | unchanged lifting; same salt | unit and sync tests | green |
| AC-009 | guard first, refusal audited via `AuditEvent.ErasureRefused(ErasureExclusionEntered, …)` under `PermittedServiceWrite.AuditEvent` | unit (3 causes), `ErasureExclusionEntryAccessTests` (409) | green |
| AC-010 | controller-level `EraseParticipantData` policy, route `SignInRoutes.ErasureExclusions` | `ErasureExclusionEntryAccessTests`, `PermissionMatrixTests` | green |
| AC-011 | shared `LiftErasureExclusionUseCase.Validate` | unit, page, logging tests | green |
| AC-012 | exclusive fence first in the entry; salt read inside the course transaction after the shared fence | `ErasureExclusionEntryTests`, `ErasureExclusionInAdvanceSyncTests.AnEntryCommittedWhileTheRunWaitsOnTheFence_IsSeenAtTheWrite` (both cases) | green |
| AC-016 | the fill skips ids held by another exclusion | sync tests | green |
| AC-017 | stored-id guard in `AddMatchedInAdvanceAsync` | sync tests | green |
| AC-018 | global antiforgery (no exemption) | access tests | green |
| AC-019 | view: entry button `disabled` in read-only mode | page tests | green |
| AC-020 | 8 new keys in `SharedResource.uk/en.resx`; `ErasureTextKeys` | translation tests | green |

## 4. Change Set

Production (new):

- `src/ClassroomAgent.Application/Models/Dtos/EntryBlock.cs` — openapi `EntryBlock`.
- `src/ClassroomAgent.Infrastructure/Persistence/Migrations/20261028123007_AddErasureExclusionInAdvance.cs` and its
  `.Designer.cs` — db-design §7. Up adds the trigger and function by SQL. Down refuses while unmatched exclusions exist,
  and the generated `defaultValue: ""` was removed so Down never fills ids.

Production (skeleton completed; created at TEST_WRITING):

- `EnterErasureExclusionUseCase.cs` — FR-002 … FR-005, FR-011 … FR-013.
- `ExclusionEntryOutcome.cs`, `ExclusionEntryResult.cs`, `UnmatchedExclusion.cs` — entity model §3–§4. Unchanged.

Production (modified):

- `Domain/Entities/ErasedParticipant.cs` — `GoogleUserId` is now `string?`; `EnterInAdvance` (entity model §1).
- `Domain/Entities/AuditEvent.cs` — `ErasureExclusionEntered`; `ErasureRefused` accepts the entry action with no
  participant (entity model §2).
- `Domain/Enums/AuditAction.cs` — `ErasureExclusionEntered` (skeleton).
- `Application/Ports/IErasedParticipantRepository.cs`, `IParticipantErasureReadSource.cs` — entity model §3 (skeleton).
- `Application/UseCases/RunSynchronizationUseCase.cs` — FR-006 … FR-008 (`AddMatchedInAdvanceAsync`,
  `ImportProgress.Salt`; `ImportCourseAsync` receives the progress).
- `Application/UseCases/LiftErasureExclusionUseCase.cs` — `Validate` changed from `private` to `internal`, so the entry
  shares it (VR-001).
- `Application/Models/Dtos/ErasureMessageKey.cs`, `ErasurePageModel.cs` — openapi.
- `Application/Localization/SharedResource.uk.resx`, `.en.resx` — FR-015 (8 keys). `Erasure.ReadOnlyNotice` now also
  names the entry.
- `Infrastructure/Persistence/ParticipantErasureReadSource.cs` — db-design §3 step 2.
- `Infrastructure/Persistence/Repositories/ErasedParticipantRepository.cs` — db-design §3 step 4 and §4 steps 3 and 6.
  It now takes `TimeProvider` (PC-6). `GetExcludedGoogleUserIdsAsync` ignores rows with no id.
- `Infrastructure/Persistence/Configurations/ErasedParticipantConfiguration.cs`, `AuditEventConfiguration.cs`,
  `Migrations/ClassroomAgentDbContextModelSnapshot.cs` — db-design §2 and §6.
- `Web/Controllers/ErasureController.cs` (action `Enter`), `Web/Security/SignInRoutes.cs`,
  `Web/Security/ErasureTextKeys.cs`, `Web/Views/Erasure/Index.cshtml` — openapi and markup hooks (strategy §4).
- `Web/Configuration/InstallationServices.cs` — DI registration (supporting).

Tests: the story classes and fixtures from TEST_WRITING are unchanged by this stage, except the shared fixture
`ErasureHostExtensions`. Pre-existing tests adapted to the approved design, each a consequence of the new migration or
the trigger (§7 D-1, D-2):

- `Infrastructure/Persistence/AppUserMigrationTests.cs` — 25 migrations; the new one is last.
- `Infrastructure/Persistence/DataHealthPersistenceTests.cs` — "after the last one" now means after the migration that
  was last when US-062 shipped, the precedent of `GradeColumnsPersistenceTests`.
- `Infrastructure/Persistence/InstallationAuditEventSchemaTests.cs` — the constraint list gains
  `ck_audit_event_exclusion_entry_shape`.
- `Web/UseCases/ParticipantErasureTests.cs` and `TestInfrastructure/ErasureHostExtensions.cs` — an old exclusion is
  inserted with its age (`SeedExclusionAsync(…, at)`) instead of being aged by an `UPDATE`, which the trigger now
  rejects.
- `Architecture/AuditDeletionConfinementTests.cs` — `ExecuteUpdate` is allowed in `ErasedParticipantRepository.cs` only.
  The new test `TheExclusionFill_NeverNamesAuditRows` covers that file.

No secret, generated database file or IDE-local configuration is in the change set.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors (after every change group and after the test adaptations) |
| Migration | `dotnet ef migrations add AddErasureExclusionInAdvance …` | exit 0 |
| Story classes | 15 × `dotnet test --no-build --filter FullyQualifiedName~<Class>` | all exit 0: `EnterErasureExclusionUseCaseTests` 36, `ErasureExclusionInAdvanceSchemaTests` 25, `ParticipantErasureSchemaTests` 29, `ErasureExclusionEntryTests` 7, `ErasureExclusionInAdvanceSyncTests` 15, `ErasureExclusionEntryPageTests` 13, `ErasureExclusionEntryAccessTests` 13, `ErasureExclusionEntryTranslationTests` 5, `ErasureExclusionEntryLoggingTests` 4, `PermissionMatrixTests` 3, `LiftErasureExclusionUseCaseTests` 21, `ErasureExclusionSyncTests` 7, `ErasureExclusionMeetTests` 5, `ErasurePageTests` 22, `ErasureAuthorizationTests` 25 |
| Full suite, run 1 | `dotnet test ClassroomAgent.sln --no-build` (4.2 GB free) | exit 2; 6737 total, 6732 passed, 5 failed. All 5 are adaptations listed in §4; none was environmental |
| Adapted classes | 5 × single-class runs | all exit 0: `ParticipantErasureTests` 20, `AppUserMigrationTests` 6, `AuditDeletionConfinementTests` 4, `DataHealthPersistenceTests` 47, `InstallationAuditEventSchemaTests` 29 |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no files |
| Full suite, run 2 | `dotnet test ClassroomAgent.sln --no-build` (5.4 GB free at start, 1.8 GB during) | exit 2 after 28m 47s; 6738 total, 6417 passed, 321 failed |
| Run 2 failures, rerun | each of the 38 affected classes alone | all exit 0, 0 failed |

Every one of the 321 failures in run 2 is an Npgsql/PostgreSQL environment failure, and none is an assertion failure:

| Message | Count |
|---|---|
| `TimeoutException` "The operation has timed out" | 244 |
| "Timeout during reading attempt" | 77 |
| "Failed to connect to 127.0.0.1:…" | 5 |
| Wrapped as "transient failure" | 11 |

They began together at about 12m 51s, the known Docker/WSL stall under low memory. The test suite is therefore taken as
PASS on this evidence:

- Run 1 had only code failures, and each is fixed and green alone.
- Run 2 had only environment failures, and each class is green alone.

A single fully green run is still to be shown. IMPLEMENTATION_VERIFICATION re-runs the suite (INFO-1).

Delegated to `cheap-worker`: every test and format run above, and the second build check. Done inline: all production
code, the migration SQL, the deviation decisions, compile checks during the edits, and this report.

## 6. Configuration Changes

None. Only DI registration was added (`AddScoped<EnterErasureExclusionUseCase>()`); no setting, appsettings key or
environment variable.

## 7. Deviations and Discovered Problems

- **D-1, a test narrowed.** `AuditDeletionConfinementTests.NoOtherCode_TouchesAuditRows` treated any `ExecuteUpdate`
  as a possible audit write. db-design §4 step 6 and §8 require `ExecuteUpdateAsync` for the fill, so that a concurrent
  lifting does not fail the course. The check now exempts `ErasedParticipantRepository.cs` from that one clause only. A
  new test asserts that the file names `db.ErasedParticipants` and never `AuditEvent` or `audit_event`. The
  independent review must judge whether this is acceptable.
- **D-2, four registry tests from earlier Stories adapted** to the new migration and trigger (§4). No assertion about
  behaviour was weakened. The retention-purge test now seeds the old row directly, because the trigger forbids the
  `UPDATE` it used.
- **D-3, the stored-id guard reuses `IClassroomParticipantRepository.GetByGoogleUserIdsAsync`.** The entity model's
  indicative `GetStoredGoogleUserIdsAsync` is not used; the entity model left the port to this stage. The guard is
  called only when an exclusion entered in advance matched, so existing runs make no extra query. The returned rows are
  tracked, and `ResolveParticipantsAsync` gets the same instances back.
- **D-4, the salt is read in each course transaction until found** (`ImportProgress.Salt ??=`), after the shared fence.
  This deliberately differs from db-design §4 step 1 ("once per run") and resolves TEST_WRITING INFO-1.
- **D-5, the read-only notice text changed.** It now says that entering an exclusion is unavailable too (FR-012 shows
  the notice with the block).
- **D-6, the migration Down refuses** with a PL/pgSQL exception while an exclusion without an id exists, instead of
  relying on the `NOT NULL` failure (db-design §7).
- **INFO-1:** the full suite has not had a single fully green run (§5); the environment had about 1.8 GB free during run
  2.
- TEST_WRITING INFO-2 is not changed: the trigger raises `check_violation` with no constraint name, as designed.

## 8. Open Decisions

None touched; none newly required. OD-001 … OD-003 are resolved.
