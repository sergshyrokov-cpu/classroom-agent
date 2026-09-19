---
artifact_type: implementation_report
story: US-007
version: 1
status: DRAFT
created_at: 2026-09-19T13:22:33Z
updated_at: 2026-09-19T13:22:33Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 4
  - path: docs/designs/api/US-007-api-design.md
    version: 1
  - path: docs/designs/database/US-007-db-design.md
    version: 1
  - path: docs/tests/US-007-test-strategy.md
    version: 2
  - path: docs/tests/US-007-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-007-test-generation-report.md
    version: 2
  - path: trebovaniya.md
    version: 77
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-007 Implementation Report — Read-only mode enforcement

## 1. Summary

Read-only mode is now enforced, by default, in `ClassroomAgent.Application`:

- `ReadOnlyModeGuard` refuses a write or a Google call before the use case
  reaches a repository, a port or a transaction, throwing `ReadOnlyModeException`
  with the reason as data;
- `ReadOnlyModeUnitOfWork` refuses, at commit, any write that no use case
  declared a permitted service write — so forgetting the guard cannot write;
- the BR-026 closed list lives in one enum with one registry entry, and
  `CheckLegitimacyUseCase` declares its write so the installation can still leave
  read-only mode;
- two `Infrastructure` decorators write the one `Warning` line per refusal and
  rethrow unchanged;
- a structural test enumerates the write and Google paths of the real
  `Application` assembly and fails on one that is neither guarded nor declared.

Validation: **build 0 errors 0 warnings, whole suite 1165 passed / 0 failed /
0 skipped, `dotnet format --verify-no-changes` clean.** No migration, no entity,
no endpoint — as the Specification and both `NOT_APPLICABLE` designs said.

Limitation carried from the approved artifacts: **API-5 is not satisfied end to
end.** The refusal stops at `Application`; the HTTP `409` mapping and the
translated message arrive with US-008 (OD-001, resolved).

## 2. Source artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-007-read-only-mode.md` | — |
| Specification | `docs/specifications/US-007-spec.md` | 2 (APPROVED) |
| Open Decisions | `docs/decisions/US-007-open-decisions.md` | 4 (OD-001, OD-002, OD-003 all RESOLVED) |
| API design | `docs/designs/api/US-007-api-design.md` | 1 (`NOT_APPLICABLE`; no OpenAPI contract, deliberately) |
| Database design | `docs/designs/database/US-007-db-design.md` | 1 (`NOT_APPLICABLE`; no entity model, deliberately) |
| Test strategy | `docs/tests/US-007-test-strategy.md` | 2 |
| AC → test matrix | `docs/tests/US-007-ac-test-matrix.md` | 2 |
| Test generation report | `docs/evidence/US-007-test-generation-report.md` | 2 |
| Requirements | `trebovaniya.md` | 77 |

No input is `SUPERSEDED`. No unresolved Open Decision remains.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test |
|---|---|---|
| AC-001 | `ReadOnlyModeGuard.EnsureAllowedAsync` (throws before any repository or port); `ReadOnlyModeUnitOfWork.SaveChangesAsync` | `ReadOnlyModeGuardTests.InReadOnlyMode_TheWriteIsRefused`, `.ARefusedWrite_CommitsNothing` |
| AC-002 | `ReadOnlyModeException` (`Reason`, `LastSuccessfulCheckAt`, `Operation`); fixed English `Message` | `ReadOnlyModeGuardTests.TheRefusal_NamesTheReason`, `.TheRefusal_CarriesTheLastSuccessfulCheck`, `.NeverConfirmed_CarriesNoLastSuccessfulCheck`, `.TheMessage_IsAFixedStringCarryingNoReason` |
| AC-003 | `PermittedServiceWrite` (four members), `PermittedServiceWrites.Declarations`, `CheckLegitimacyUseCase` declaring `LegitimacyCheckState` around its commit | `PermittedServiceWriteTests.TheLegitimacyCheckWrite_RunsInReadOnlyMode`, `ReadOnlyModeUnitOfWorkTests.ADeclaredServiceWrite_CommitsInReadOnlyMode` |
| AC-004 | `ReadOnlyModeUnitOfWork.SaveChangesAsync` — refusal is the fallback when `ServiceWriteScope.Current` is null | `ReadOnlyModeUnitOfWorkTests.AUseCaseThatForgotTheGuard_StillCannotCommit`, `.TheDeclaration_DoesNotOutliveItsScope` |
| AC-005 | nothing added to the read path; the guard and the scope are taken by writes only | `ReadsInReadOnlyModeTests` (all), `WritePathRuleTests.AReadUseCase_IsNotAProtectedPath` |
| AC-006 | `IGoogleDataPort` marker + the guard called first by any use case holding one | `GoogleDataPortRuleTests.InReadOnlyMode_TheGooglePortIsNeverCalled`, `.OutsideReadOnlyMode_TheGooglePortIsCalled` |
| AC-007 | `WritePathRule` (test project) over `Application.UseCases` + `PermittedServiceWrites` | `WritePathRuleTests` (9 cases), `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath`, `.TheLegitimacyCheckUseCase_IsARecognisedWritePath` |
| AC-008 | both the guard and the backstop call `GetLegitimacyModeQuery` per call; scoped lifetimes | `ReadOnlyModeTimingTests` (all five) |
| AC-009 | no cached mode + the permitted `LegitimacyState` write | `ReadOnlyModeTimingTests.ASuccessfulCheck_EndsTheRefusal_InTheSameHost`, `.AResumption_RestoresWrites_WithoutARestart` |
| AC-010 | `RefusalLog` (`EventId 5201`, `ReadOnlyWriteRefused`, `Warning`) used by both decorators; no audit row written | `ReadOnlyRefusalLoggingTests` (all five) |
| AC-011 | the rule lives only in `Application`; `Infrastructure` holds the observing decorators; `Web` holds only DI wiring | `ReadOnlyEnforcementTests.TheGuard_IsImplementedOnlyInsideApplication`, `.TheWebLayer_HoldsNoCopyOfTheRule`, `.TheDecorators_AreRegisteredInFrontOfTheGuardAndTheUnitOfWork`, `.TheGuardAndTheScope_AreScopedNotSingleton`; `ProjectReferenceTests` unchanged and green |

## 4. Change set

Files created or modified **by this stage** (the skeleton and the tests landed in
the previous commit, `769ba72`, under OD-003; this stage owns and completes them).

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Application/UseCases/ReadOnlyModeGuard.cs` | implemented | FR-002, AC-001, AC-002, AC-006, VR-001 |
| `src/ClassroomAgent.Application/UseCases/ReadOnlyModeUnitOfWork.cs` | implemented | FR-006, AC-001, AC-003, AC-004 |
| `src/ClassroomAgent.Application/UseCases/ServiceWriteScope.cs` | implemented | FR-005, VR-002 |
| `src/ClassroomAgent.Application/UseCases/PermittedServiceWrites.cs` | registry populated | FR-005, AC-003, AC-007 |
| `src/ClassroomAgent.Application/UseCases/CheckLegitimacyUseCase.cs` | takes `ServiceWriteScope`, declares `LegitimacyCheckState` around its commit | FR-005, AC-003, AC-009 |
| `src/ClassroomAgent.Infrastructure/ReadOnly/LoggingReadOnlyModeGuard.cs` | new | FR-009, AC-010, OD-002 option 3 |
| `src/ClassroomAgent.Infrastructure/ReadOnly/LoggingUnitOfWork.cs` | new | FR-009, AC-010 |
| `src/ClassroomAgent.Infrastructure/ReadOnly/RefusalLog.cs` | new | FR-009, S-03, DC-10 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI wiring of guard, scope, backstop and decorators | FR-011 (supporting change: DI registration) |
| `docs/architecture/package-map.md` | one row for the new `Infrastructure.ReadOnly` namespace | supporting change, see section 7 |
| `tests/ClassroomAgent.Tests/TestInfrastructure/SyntheticWriteUseCase.cs` | the fixture's write no longer moves the state it is judged by | test-fixture defect, see section 7 |

No file is without a trace. No secret, generated database file or IDE-local
config is in the change set. No NuGet package was added anywhere.

## 5. Validation evidence

| Command | Exit | Result |
|---|---|---|
| `dotnet build ClassroomAgent.sln` | 0 | **0 errors, 0 warnings** across all seven projects (`TreatWarningsAsErrors`) |
| `ClassroomAgent.Tests.exe` (ten US-007 classes) | 0 | total 80, **failed 0**, skipped 0 |
| `ClassroomAgent.Tests.exe` (whole suite) | 0 | **total 1165, failed 0, skipped 0**, 50.8 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | 0 | clean |

The red-phase baseline was 50 failures, all in the US-007 classes
(test-generation report v2). After implementation: **0**. Nothing outside US-007
changed state — the 1085 US-001 … US-006 cases were green before and after.

Integration tests ran against real PostgreSQL in Testcontainers (Docker 29.8.0),
one migrated database per host (TC-2). No test called a live Google API or a live
Control Plane (TC-4).

## 6. Configuration changes

None. No `appsettings` key was added, changed or removed; no new setting is
required to run the installation. The only startup change is the DI wiring of
FR-011 in `InstallationServices`, which holds no business logic (AD-10).

## 7. Deviations and discovered problems

**7.1 A new `Infrastructure` namespace, recorded in `package-map.md`.**
Specification FR-009 (OD-002 option 3) puts the logging decorators in
`Infrastructure`, and none of the namespaces `package-map.md` listed fits a
read-only decorator — `Persistence` would have implied persistence knowledge the
decorator deliberately does not have. The decorators live in
`ClassroomAgent.Infrastructure.ReadOnly` and one row was added to the
`package-map.md` Infrastructure table. Leaving the derived document contradicting
the code would itself be a defect (`AGENTS.md`), so the row is a supporting
change, not an opportunistic edit.

**7.2 One test fixture was corrected; no assertion was weakened.**
`SyntheticWriteUseCase.WriteAsync` wrote the marker row through `RecordSuccess`,
which also set the status to active and moved the last successful check to "now".
The fixture therefore *changed the very mode the test was judging*: after a
permitted or successful write the installation silently left read-only mode, and
three cases could not fail —
`ReadOnlyModeUnitOfWorkTests.TheDeclaration_DoesNotOutliveItsScope`,
`ReadOnlyModeTimingTests.TheGracePeriodExpiring_…` and
`ReadOnlyModeTimingTests.ExactlySevenDays_…`. The fixture now writes through
`RecordUpgradeRequired`, which changes only the domain and leaves the status and
the last successful check alone (US-005 already proves compatibility alone never
causes read-only mode). The tests, their assertions and the matrix rows are
unchanged, and no production behaviour was changed to make anything pass.

**7.3 A deliberate refinement of interpretation I-5.**
I-5 says the mode is read twice per permitted-write path. The backstop skips the
read when a declaration is open, so a *declared* service write reads the mode
once, not twice; a guarded write still reads it twice (guard, then backstop).
This is strictly fewer reads with identical behaviour, and AC-008 is unaffected —
nothing is cached in either path. Recorded because it narrows an accepted
interpretation.

**7.4 Nothing else diverged.** No conflict was found between the approved
artifacts and repository reality; no loop-back is recommended.

## 8. Open Decisions

| Id | Subject | Status |
|---|---|---|
| OD-001 | Where the HTTP `409` mapping belongs | RESOLVED (option 1) — honoured: the refusal stops at `Application` |
| OD-002 | How `Application` writes the refusal log line | RESOLVED (option 3) — honoured: decorators in `Infrastructure`; `Application` still references **no** NuGet package and `ProjectReferenceTests.FrameworkFreeProjects_ReferenceNoPackage` passes unchanged |
| OD-003 | How the red phase compiles | RESOLVED (option 1) — the skeleton it allowed is now implemented in full |

None open. None newly required.
