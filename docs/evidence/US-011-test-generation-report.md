---
artifact_type: test_generation_report
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T09:01:17Z
updated_at: 2026-09-21T09:01:17Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 2
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/designs/database/US-011-db-design.md
    version: 1
  - path: docs/tests/US-011-test-strategy.md
    version: 1
  - path: docs/tests/US-011-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-011 Test Generation Report

## 1. Result

**Red phase verified.** 10 new test classes, 91 methods, 185 cases; one existing test modified. All build with 0
errors and 0 warnings. Whole suite: **1976 total, 1793 passed, 183 failed, 0 skipped**. Every failure is in the
US-011 set and names missing production behaviour; no test of US-001 … US-010 changed state.

## 2. Open Decision raised at this stage

**OD-006 — compile-only skeleton** (resolved by the Owner on 2026-09-21, option 1; recorded in
`docs/decisions/US-011-open-decisions.md` v2). The decisive tests substitute the first real Google port, whose type
did not exist. Six production files were created as a compile-only skeleton — members throw
`NotImplementedException`, nothing is registered in DI — and IMPLEMENTATION owns them from here:

- `src/ClassroomAgent.Application/Ports/IGoogleAccessProbe.cs`
- `src/ClassroomAgent.Application/Models/AccessCheckStepOutcome.cs`
- `src/ClassroomAgent.Application/Models/DelegatedToken.cs`
- `src/ClassroomAgent.Application/Models/DelegationAttempt.cs`
- `src/ClassroomAgent.Infrastructure/Google/GoogleAccessProbe.cs`
- `src/ClassroomAgent.Infrastructure/Google/GoogleServiceAccountSettings.cs`

No NuGet package was added: the skeleton compiles without the Google packages, which OD-001 lets IMPLEMENTATION add.

## 3. Files

**Created (tests):**
`Web/Security/AccessCheckAuthorizationTests.cs` (11 methods), `Web/Pages/AccessCheckPageTests.cs` (6),
`Web/UseCases/AccessCheckRunTests.cs` (13), `Web/UseCases/AccessCheckFailureTests.cs` (11),
`Web/UseCases/AccessCheckRefusalTests.cs` (9), `Web/UseCases/AccessCheckAuditTests.cs` (5),
`Web/BackgroundServices/StartupSelfCheckTests.cs` (9), `Web/Localization/AccessCheckTranslationTests.cs` (4),
`Web/Persistence/AccessCheckAuditSchemaTests.cs` (6), `Infrastructure/Google/GoogleAccessProbeTests.cs` (17).

**Created (fixtures, `TestInfrastructure/`):** `AccessCheckTestData`, `AccessCheckHostExtensions`,
`FakeGoogleAccessProbe`, `ProbeCall`, `ProbeCallKind`, `SeededConnection`, `RenderedStep`, `DictionarySecretStore`,
`SyntheticServiceAccountKey`.

**Modified:** `Infrastructure/Persistence/AppUserMigrationTests.cs` — 3 → 4 installation migrations with
`_AddAccessCheckAudit` last, as US-011 db-design §8 fixes. Traced; no other existing test was touched.

## 4. Commands

- `dotnet build ClassroomAgent.sln` — 0 errors, 0 warnings.
- `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~AccessCheck|FullyQualifiedName~StartupSelfCheckTests|FullyQualifiedName~GoogleAccessProbeTests|FullyQualifiedName~AppUserMigrationTests"` — run 1:
  191/191 failed on `DockerUnavailableException` (Docker Desktop was not running — an environment failure, not a
  result; Docker Desktop was started); run 2: 191 total, 183 failed, 8 passed, 7 min 51 s.
- `dotnet test ClassroomAgent.sln --no-build` — 1976 total, 1793 passed, 183 failed, 0 skipped, 21 min 8 s.

## 5. Classification of the 183 failures

All are "fails because production behaviour is not implemented yet":

| Class | Failing cases | Failure | Missing behaviour |
|---|---|---|---|
| GoogleAccessProbeTests | 29 | `NotImplementedException` | the adapter |
| AccessCheckTranslationTests | 64 | `Translation key '…' is missing for 'uk'` | the 21 keys |
| AccessCheckAuthorizationTests | 11 | `No policy found: RunAccessCheck`; endpoint absent | the policy and the endpoints |
| AccessCheckPageTests | 8 | 404 where 200 expected | the page |
| AccessCheckRunTests | 18 | 404 where 200 expected; no verdict, no steps | the run and the use case |
| AccessCheckFailureTests | 14 | 404 / no steps; first call never made (time-limit case) | the use case, the outcome rendering |
| AccessCheckRefusalTests | 17 | 404 where 409 expected; no audit row | the guard in the use case, the refusals |
| AccessCheckAuditTests | 5 | no `access_check_run` row | the audit write |
| AccessCheckAuditSchemaTests | 4 | `ck_audit_event_action` rejects `access_check_run` | the migration |
| AppUserMigrationTests | 1 | 3 migrations where 4 expected | the migration |
| StartupSelfCheckTests | 12 | `Timed out waiting for 'AccessSelfCheck…'`; first call never made | the startup self-check |
| **Total** | **183** | | |

None fails on a fixture, an import, a host configuration or an assertion contradicting an approved artifact.

## 6. Tests passing before implementation

- 5 pre-existing `AppUserMigrationTests` cases — unchanged, still green.
- 3 new **guards**: `AnUnknownAction_IsStillRejected`, `NoTableIsAdded`, `TheAuditTable_GainsNoColumn`. They are true
  today by construction and must stay true after the migration — a migration that dropped the action constraint,
  added a result table or a column would turn them red. They are not evidence of new behaviour and are marked GUARD in
  the matrix.

The vacuous-pass trap was checked for every absence assertion: each HTTP test asserts the expected status (or
`NotEmpty` calls/rows) **before** asserting an absence, so a 404 cannot satisfy "0 calls", "no token in the page" or
"no personal data in the rows"; `TheAnonymousList_GainsNothing` asserts the endpoint exists first.

## 7. Untested Acceptance Criteria

None. AC-009 is a property of the suite (the only Google seams are the fake port and the scripted transport).
Two scenarios are deliberately excluded (test strategy §6): the lower bound of the 30-second limit and "only once per
start" — both would need a real-time wait.

## 8. Notes for IMPLEMENTATION

- Names fixed by this stage: test strategy §5 (setting `Google:ServiceAccountKeyReference`, markup markers, log
  events `AccessSelfCheckCompleted`/`AccessSelfCheckSkipped` with properties `Verdict`/`Reason`, 21 translation keys,
  the adapter constructor). Correct them only together with the tests.
- The fake port is registered by replacing `IGoogleAccessProbe` in DI; the production registration must be a plain
  service registration of that interface so the replacement takes effect, and the self-check must resolve the port
  from DI rather than construct it.
- The run's 30-second limit must use the injected `TimeProvider` (the tests advance a manual clock).
- The adapter must disable the Google client library's own retry of `5xx`/`429` (spec FR-005: not retried within a
  check) and must send nothing when the key is missing or unusable.
- The HTTP tests distinguish the Admin's run from the self-check through `IHttpContextAccessor`; the self-check must
  run outside any request scope — which a hosted service does naturally.
