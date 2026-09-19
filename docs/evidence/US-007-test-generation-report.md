---
artifact_type: test_generation_report
story: US-007
version: 2
status: DRAFT
created_at: 2026-09-19T12:48:23Z
updated_at: 2026-09-19T13:12:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 4
  - path: docs/tests/US-007-test-strategy.md
    version: 2
  - path: docs/tests/US-007-ac-test-matrix.md
    version: 2
supersedes: null
---

# US-007 Test Generation Report

**Overall result: PASS. Tests written, compiling, and the red phase is verified
on real PostgreSQL.**

Whole suite: **total 1165, passed 1115, failed 50, skipped 0**. All 50 failures
are in the ten new US-007 classes and every one of them is a missing-production-
behaviour failure (section 4). **No test outside US-007 fails** — the 1085
US-001 … US-006 cases are green, unchanged.

## 1. Test files created

| File | Contents |
|---|---|
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyModeGuardTests.cs` | AC-001, AC-002, VR-001 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyModeUnitOfWorkTests.cs` | AC-003, AC-004 (the backstop) |
| `tests/ClassroomAgent.Tests/Application/UseCases/PermittedServiceWriteTests.cs` | AC-003, AC-009, VR-003 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ServiceWriteScopeTests.cs` | VR-002 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadsInReadOnlyModeTests.cs` | AC-005 |
| `tests/ClassroomAgent.Tests/Application/UseCases/GoogleDataPortRuleTests.cs` | AC-006 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyModeTimingTests.cs` | AC-008, AC-009 |
| `tests/ClassroomAgent.Tests/Application/UseCases/ReadOnlyRefusalLoggingTests.cs` | AC-010 |
| `tests/ClassroomAgent.Tests/Architecture/WritePathRuleTests.cs` | AC-007, the detection proof |
| `tests/ClassroomAgent.Tests/Architecture/ReadOnlyEnforcementTests.cs` | AC-007 over the real assembly, AC-011 |

Test fixtures added under `tests/ClassroomAgent.Tests/TestInfrastructure/`:
`ReadOnlyModeHost` (the three BR-025 causes as seeded hosts),
`SyntheticWriteUseCase`, `UnguardedWriteUseCase`, `DeclaredServiceWriteUseCase`,
`ISyntheticGooglePort`, `SyntheticGooglePort`, `SyntheticGoogleUseCase`,
`UnguardedGoogleUseCase`, `WritePathRule`.

## 2. Test files modified

| File | Change |
|---|---|
| `TestInfrastructure/InstallationFactory.cs` | an optional `configureServices` hook, applied last |
| `TestInfrastructure/InstallationTestHost.cs` | the `ConfigureServices` property that feeds it |

Nothing was removed, disabled or weakened. No existing test was changed.

## 3. Production files created — the OD-003 skeleton

OD-003 was resolved on 2026-09-19 (option 1): `TEST_WRITING` declares a
compile-only skeleton, `IMPLEMENTATION` owns those files from then on and may
reshape them together with the tests. Nothing is registered in DI.

| File | State |
|---|---|
| `src/ClassroomAgent.Application/UseCases/IReadOnlyModeGuard.cs` | the interface only |
| `src/ClassroomAgent.Application/UseCases/ReadOnlyModeGuard.cs` | `EnsureAllowedAsync` throws `NotImplementedException` |
| `src/ClassroomAgent.Application/UseCases/ReadOnlyModeUnitOfWork.cs` | `SaveChangesAsync` throws `NotImplementedException`; `Operation` constant declared |
| `src/ClassroomAgent.Application/UseCases/ServiceWriteScope.cs` | both members throw |
| `src/ClassroomAgent.Application/UseCases/PermittedServiceWrites.cs` | `Declarations` throws |
| `src/ClassroomAgent.Application/UseCases/PermittedServiceWrite.cs` | the four BR-026 members, declared |
| `src/ClassroomAgent.Application/Exceptions/ReadOnlyModeException.cs` | the reason, the last successful check and the operation |
| `src/ClassroomAgent.Application/Ports/IGoogleDataPort.cs` | the empty marker |

**Two of these carry real content rather than a throwing member, and it has to be
said plainly:** the enum's four members and the exception's three properties are
data the tests name by identifier, so they must exist for the suite to compile.
The consequence is that two tests pass before implementation
(`TheList_HasExactlyTheFourMembersOfBr026`, `TheRefusal_CarriesNoHttpConcept`);
their value is as regression guards against later drift from BR-026 and from
AD-9, not as proof that IMPLEMENTATION did anything.

`ClassroomAgent.Application` still references **no NuGet package** — the US-005
test `FrameworkFreeProjects_ReferenceNoPackage` passes unchanged, which is what
OD-002 option 3 was chosen to preserve.

## 4. Red-phase classification

| Class | Passed | Failed |
|---|---|---|
| `ReadOnlyModeGuardTests` | 0 | 17 |
| `ReadOnlyModeUnitOfWorkTests` | 1 | 8 |
| `PermittedServiceWriteTests` | 4 | 2 |
| `ServiceWriteScopeTests` | 0 | 6 |
| `ReadsInReadOnlyModeTests` | 10 | 0 |
| `GoogleDataPortRuleTests` | 2 | 4 |
| `ReadOnlyModeTimingTests` | 0 | 5 |
| `ReadOnlyRefusalLoggingTests` | 0 | 5 |
| `WritePathRuleTests` | 9 | 0 |
| `ReadOnlyEnforcementTests` | 4 | 3 |
| **Total** | **30** | **50** |

Every failure message names missing production behaviour, in four shapes:

| Shape | Count | What it means |
|---|---|---|
| `NotImplementedException` | 8 | the skeleton member IMPLEMENTATION must write |
| `Unable to resolve service for type IReadOnlyModeGuard` / `ServiceWriteScope` | 14 | FR-011 wiring does not exist yet |
| `Assert.Throws: exception type was not an exact match` | 23 | the guard threw `NotImplementedException` (or DI failed) where `ReadOnlyModeException` is required |
| `Assert.Throws: no exception was thrown` | 5 | the backstop does not exist, so an undeclared write still commits — exactly AC-004 |

No failure is a syntax error, an invalid import, a missing dependency, a broken
test host, an invalid fixture or a contradiction with an approved artifact.

## 5. Tests that pass before implementation, and why

Investigated as the stage rules require; none is a weak or bypassing test.

| Tests | Why they pass today |
|---|---|
| `WritePathRuleTests` (9) | the rule is a function in the **test** project; its whole job is to prove the detector detects before it is pointed at the real assembly (AC-007). It passing is the point |
| `ReadsInReadOnlyModeTests` (10) | reads are already unaffected, because nothing blocks them yet. They are the guarantee that IMPLEMENTATION does not start blocking them (AC-005) |
| `TheLegitimacyCheckWrite_RunsInReadOnlyMode` (3) | the US-005 write already runs; it becomes meaningful the moment the backstop exists, and would have caught a backstop that refused it (AC-003, AC-009) |
| `OutsideReadOnlyMode_AnUndeclaredCommitSucceeds` (1) | normal operation is unchanged today and must stay unchanged |
| `TheGuard_IsImplementedOnlyInsideApplication`, `TheWebLayer_HoldsNoCopyOfTheRule`, `TheLegitimacyCheckUseCase_IsARecognisedWritePath` (3) | layering guarantees that hold now and must keep holding (AC-011) |
| `TheMarker_*` (2) | the marker is part of the skeleton; the tests fix its shape (AD-4) |
| `TheList_HasExactlyTheFourMembersOfBr026`, `TheRefusal_CarriesNoHttpConcept` (2) | see section 3 — they guard the skeleton's data against drift |

## 6. Commands run

| Command | Result |
|---|---|
| `docker version` | 29.8.0 (after Docker Desktop was started; the first attempt of this stage failed with the daemon down) |
| `dotnet build ClassroomAgent.sln` | succeeded, **0 errors, 0 warnings** (`TreatWarningsAsErrors` in every project) |
| `ClassroomAgent.Tests.exe` (the ten US-007 classes) | total 80, failed 50 |
| `ClassroomAgent.Tests.exe` (whole suite) | **total 1165, failed 50, skipped 0**, 49.1 s |

Integration tests ran against real PostgreSQL in Testcontainers, one migrated
database per host (TC-2). No test called a live Google API or a live Control
Plane (TC-4).

## 7. Untested Acceptance Criteria

None. AC-001 … AC-011 and VR-001 … VR-003 all have executable scenarios; the
mapping is `docs/tests/US-007-ac-test-matrix.md` (v2).

Three limitations of a future green suite are recorded in the test strategy §7
and restated here so they are not mistaken for proof: only `LegitimacyCheckState`
of the four BR-026 members has a live use case; AC-006 is proven on a synthetic
port because no Google port exists; and the backstop is proven for writes that go
through `IUnitOfWork`, which AD-7 already requires of every write.

## 8. Open Decisions

| Id | Subject | Status |
|---|---|---|
| OD-001 | HTTP `409` mapping location | RESOLVED (option 1) |
| OD-002 | Where the refusal log line is written | RESOLVED (option 3) |
| OD-003 | How the red phase compiles | RESOLVED (option 1) — the skeleton of section 3 |

None remains open.

## 9. What IMPLEMENTATION must do to turn the suite green

1. Implement the eight skeleton members (section 3).
2. Register the wiring of FR-011: `IReadOnlyModeGuard` → `ReadOnlyModeGuard`
   behind its logging decorator, `ServiceWriteScope`, and `IUnitOfWork` →
   `ReadOnlyModeUnitOfWork` wrapping the `Infrastructure` `UnitOfWork`, behind
   its logging decorator — all scoped.
3. Add the two `Infrastructure` logging decorators of FR-009 and the
   `ReadOnlyWriteRefused` log event the logging tests look for.
4. Register `CheckLegitimacyUseCase` in `PermittedServiceWrites` and open the
   declaration scope around its commit (FR-005).
