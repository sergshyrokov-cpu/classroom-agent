---
artifact_type: test_generation_report
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T06:52:52Z
updated_at: 2026-10-04T06:52:52Z
produced_by: test-writer
inputs:
  - path: docs/tests/US-019-test-strategy.md
    version: 1
  - path: docs/tests/US-019-ac-test-matrix.md
    version: 1
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/database/US-019-db-design.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
supersedes: null
---

# US-019 Test Generation Report

## 1. Result

**PASS — red phase verified.** Build 0 warnings / 0 errors. Full suite
(`dotnet test ClassroomAgent.sln`): **2835 total, 2785 passed, 50 failed,
0 skipped**. All 50 failures are new US-019 tests failing for missing
production behaviour; **no existing test regressed** (zero failures outside the
new classes).

## 2. Production skeleton (OD-010 a, Owner 2026-10-04)

Compile-only, nothing registered in DI, no existing behaviour changed:

- `src/ClassroomAgent.Domain/Enums/AuditAction.cs` — `SynchronizationRequested` (declaration; no DB code mapping yet)
- `src/ClassroomAgent.Domain/Entities/AuditEvent.cs` — `SynchronizationRequested`, `SynchronizationRequestRefused` (throw `NotImplementedException`)
- `src/ClassroomAgent.Application/Models/SynchronizationRequestTiming.cs` (declaration)
- `src/ClassroomAgent.Application/Models/RequestSynchronizationOutcome.cs` (declaration)
- `src/ClassroomAgent.Application/Ports/ISynchronizationRequests.cs` (interface)
- `src/ClassroomAgent.Application/UseCases/RequestSynchronizationUseCase.cs` (throws)
- `src/ClassroomAgent.Web/BackgroundServices/CoordinatorSynchronizationRequests.cs` (throws)

## 3. Test files

Created (fixtures written by the orchestrating session; test classes by three
Sonnet subagents from fixed briefs, reviewed and the full suite run by the
orchestrating session):

| File | Tests (cases) |
|---|---|
| `TestInfrastructure/SynchronizationRequestTestData.cs` | fixture |
| `TestInfrastructure/FakeSynchronizationRequests.cs` | fixture |
| `TestInfrastructure/SynchronizationRequestHostExtensions.cs` | fixture |
| `Web/UseCases/SynchronizationRequestTests.cs` | 5 |
| `Web/UseCases/SynchronizationRequestRefusalTests.cs` | 18 |
| `Web/UseCases/SynchronizationRequestAuditTests.cs` | 3 |
| `Web/Security/SynchronizationRequestAuthorizationTests.cs` | 7 |
| `Web/Pages/SynchronizeButtonTests.cs` | 5 |
| `Web/Localization/SynchronizationRequestTranslationTests.cs` | 2 |
| `Web/Logging/SynchronizationRequestLoggingTests.cs` | 2 |
| `Web/BackgroundServices/CoordinatorSynchronizationRequestsTests.cs` | 4 |
| `Web/Persistence/SynchronizationRequestAuditSchemaTests.cs` | 7 |

Modified: none.

## 4. Commands

- `dotnet build ClassroomAgent.sln` — 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln` — 2785 passed, 50 failed, 0 skipped, 4 m 21 s
  (Docker running; Testcontainers PostgreSQL).
- Per-class filtered runs by each subagent with the same outcome.

## 5. Red reasons (50)

| Class | Failing | Reason |
|---|---|---|
| `SynchronizationRequestTests` | 5/5 | endpoint absent: `404` instead of `302` |
| `SynchronizationRequestRefusalTests` | 18/18 | `404` instead of `409` |
| `SynchronizationRequestAuditTests` | 3/3 | `404` instead of `302` |
| `SynchronizationRequestAuthorizationTests` | 5/7 | anonymous, missing token, `returnUrl`: `404` instead of `302`/`400` |
| `SynchronizeButtonTests` | 4/5 | no form `synchronization-request-form` on the pages |
| `SynchronizationRequestTranslationTests` | 2/2 | key `Synchronization.Request.Button` missing; endpoint `404` |
| `SynchronizationRequestLoggingTests` | 2/2 | no `SynchronizationRequested` / `SynchronizationRequestRefused` event (bounded wait) |
| `CoordinatorSynchronizationRequestsTests` | 4/4 | `NotImplementedException` from the adapter |
| `SynchronizationRequestAuditSchemaTests` | 7/7 | factories throw `NotImplementedException`; the shape theory hits `ck_audit_event_action` before the (absent) `ck_audit_event_sync_request_shape` |

## 6. Tests that pass before implementation (3) — explained

- `SynchronizationRequestAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange_AndNothingHappens`
  — the US-012 restricted session already redirects every authenticated request
  before routing. Kept: it guards that the new endpoint does not bypass it.
- `SynchronizationRequestAuthorizationTests.AGet_RequestsNothing` — `404` today;
  port and audit assertions hold vacuously. Kept: it guards that no `GET`
  handler is ever added that enqueues.
- `SynchronizeButtonTests.TheHomePage_HasNoButton_ForAnAdmin` — no form exists
  anywhere yet. Kept: it guards spec I-6 once the Dean's button exists.

None of the three is counted as evidence of new behaviour; each AC they touch
has a red test as well.

## 7. Untested Acceptance Criteria

None. AC-001 … AC-009 each map to at least one red test
(`docs/tests/US-019-ac-test-matrix.md`). AC-008 is satisfied by construction:
every test substitutes the synchronization-request port, and the host's Google
ports are already substituted (TC-4).

## 8. Notes for IMPLEMENTATION

- Use the fixed names in `SynchronizationRequestTestData`: path, text keys
  (`Synchronization.Request.*`), log events (`SynchronizationRequested`
  Information, `SynchronizationRequestRefused` Warning), markup ids
  (`synchronization-request-form`, `synchronization-request-message`).
- `ActionCode`/`ActionFromCode` must map the new member, or every audit read
  fails.
- Register the use case, the adapter (singleton over the coordinator) and the
  `StartSynchronization` policy; tests replace the port with the fake.
- The shape theory's case (b) (`wrong_password`) can only be confirmed to pass
  every other constraint once `ck_audit_event_action` accepts the action.

## 9. Open Decisions

OD-010 raised and resolved by the Owner (a). No open decision remains.
