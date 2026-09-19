---
artifact_type: ac_test_matrix
story: US-007
version: 2
status: DRAFT
created_at: 2026-09-19T12:48:23Z
updated_at: 2026-09-19T13:05:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 4
  - path: docs/tests/US-007-test-strategy.md
    version: 1
supersedes: null
---

# US-007 Acceptance Criteria → Test Matrix

Every Acceptance Criterion of the Story has at least one executable scenario.
Class and method names are the ones actually written; `Status` is the red-phase
result recorded in `docs/evidence/US-007-test-generation-report.md`.

Levels: **I** integration (installation host + real PostgreSQL via
Testcontainers), **U** unit, **S** structural, **Sec** security-focused.

All test classes live under `tests/ClassroomAgent.Tests/`; the `Application`
classes under `Application/UseCases/`, the rest under `Architecture/`.

## AC-001 A write refuses while the installation is in read-only mode

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| Each of the three reasons refuses | I, Sec | `ReadOnlyModeGuardTests` | `InReadOnlyMode_TheWriteIsRefused` (theory ×3) | `ReadOnlyModeException` |
| One mode, not three behaviours | I | `ReadOnlyModeGuardTests` | same theory — one exception type for all three causes | same type |
| Nothing is written, nothing committed | I | `ReadOnlyModeGuardTests` | `ARefusedWrite_CommitsNothing` (theory ×3) | the rows are identical before and after; no marker row |
| Outside read-only mode the write proceeds | I | `ReadOnlyModeGuardTests` | `NotInReadOnlyMode_TheWriteProceeds` | the marker row is written |
| The refusal precedes the port | I, Sec | `GoogleDataPortRuleTests` | `RefusalHappensBeforeThePortIsReached` → `InReadOnlyMode_TheGooglePortIsNeverCalled` | zero port calls |

## AC-002 The refusal carries the reason

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| The reason names the cause | I | `ReadOnlyModeGuardTests` | `TheRefusal_NamesTheReason` (theory ×3) | `Reason` equals the seeded cause |
| Expired grace and suspension carry the last success | I | `ReadOnlyModeGuardTests` | `TheRefusal_CarriesTheLastSuccessfulCheck` (theory ×2) | equals the seeded success |
| Never confirmed carries no time | I | `ReadOnlyModeGuardTests` | `NeverConfirmed_CarriesNoLastSuccessfulCheck` | null |
| The operation is carried | I | `ReadOnlyModeGuardTests` | `TheRefusal_CarriesTheOperationConstant` | the caller's constant |
| The reason is data, not a parsed message | I | `ReadOnlyModeGuardTests` | `TheMessage_IsAFixedStringCarryingNoReason` | the message names neither the reason nor the time |
| No HTTP concept | S | `ReadOnlyEnforcementTests` | `TheRefusal_CarriesNoHttpConcept` | no status/HTTP/problem member (AD-9) |

## AC-003 The closed list of permitted service writes still runs

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| The legitimacy write runs in read-only mode | I | `PermittedServiceWriteTests` | `TheLegitimacyCheckWrite_RunsInReadOnlyMode` (theory ×3) | the row is written for all three causes |
| A declared service write commits | I | `ReadOnlyModeUnitOfWorkTests` | `ADeclaredServiceWrite_CommitsInReadOnlyMode` (theory ×3) | commit succeeds |
| The list has exactly the four BR-026 members | U | `PermittedServiceWriteTests` | `TheList_HasExactlyTheFourMembersOfBr026` | the four names |
| The registry names the live case | U | `PermittedServiceWriteTests` | `TheRegistry_DeclaresTheLegitimacyCheckUseCase` | `LegitimacyCheckState` |
| The registry has grown no further | U | `PermittedServiceWriteTests` | `TheRegistry_DeclaresNothingElse` | exactly one entry, in `Application.UseCases` |

## AC-004 Anything not on the list is refused by default

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| A use case that forgot the guard still cannot commit | I, Sec | `ReadOnlyModeUnitOfWorkTests` | `AUseCaseThatForgotTheGuard_StillCannotCommit` (theory ×3) | refused; rows unchanged |
| The backstop names itself in the refusal | I | `ReadOnlyModeUnitOfWorkTests` | `TheBackstopRefusal_NamesTheBackstopAsItsOperation` | `ReadOnlyModeUnitOfWork.Operation` |
| Outside read-only mode an undeclared commit succeeds | I | `ReadOnlyModeUnitOfWorkTests` | `OutsideReadOnlyMode_AnUndeclaredCommitSucceeds` | normal operation unchanged |
| Permission is not left open after a declared write | I | `ReadOnlyModeUnitOfWorkTests` | `TheDeclaration_DoesNotOutliveItsScope` | the next undeclared write is refused |
| With no declaration nothing is permitted | U | `ServiceWriteScopeTests` | `WithNoDeclaration_NothingIsPermitted` | `Current` is null |

## AC-005 Reading and exporting are never blocked

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| The mode query answers | I | `ReadsInReadOnlyModeTests` | `TheModeQuery_AnswersInReadOnlyMode` (theory ×3) | answers with a reason |
| The readiness query answers | I | `ReadsInReadOnlyModeTests` | `TheReadinessQuery_AnswersInReadOnlyMode` (theory ×3) | `Degraded` |
| Readiness stays HTTP 200 | I | `ReadsInReadOnlyModeTests` | `Readiness_StaysTwoHundred` (theory ×3) | `200` (US-005 FR-013) |
| A read takes neither guard nor scope | I, S | `ReadsInReadOnlyModeTests` | `AReadUseCase_TakesNeitherTheGuardNorTheScope` | neither constructor parameter |
| A read is not a protected path | S | `WritePathRuleTests` | `AReadUseCase_IsNotAProtectedPath` | the rule does not apply |

## AC-006 No call to Google is made in read-only mode

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| The marked port receives no call | I, Sec | `GoogleDataPortRuleTests` | `InReadOnlyMode_TheGooglePortIsNeverCalled` (theory ×3) | zero calls |
| Outside read-only mode the port is reached | I | `GoogleDataPortRuleTests` | `OutsideReadOnlyMode_TheGooglePortIsCalled` | one call — the test is not vacuous |
| The marker is an empty interface in `Application.Ports` | S | `GoogleDataPortRuleTests` | `TheMarker_IsAnEmptyInterfaceInApplicationPorts` | no members |
| The marker drags in no Google SDK | S, Sec | `GoogleDataPortRuleTests` | `TheMarker_CarriesNoGoogleSdkType` | no `Google*` referenced assembly (AD-4) |
| Every Google path passes the guard | S | `ReadOnlyEnforcementTests` | `TheApplicationAssembly_HasNoUnprotectedWritePath` | no violation |

## AC-007 The enforcement cannot be bypassed

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| A write path with neither remedy is flagged | S | `WritePathRuleTests` | `AWritePathWithNeitherGuardNorDeclaration_IsFlagged` | exactly one violation, naming the type |
| A Google path without the guard is flagged | S | `WritePathRuleTests` | `AGooglePathWithoutTheGuard_IsFlagged` | one violation |
| A guarded write path is accepted | S | `WritePathRuleTests` | `AGuardedWritePath_IsAccepted` | none |
| A guarded Google path is accepted | S | `WritePathRuleTests` | `AGuardedGooglePath_IsAccepted` | none |
| A registered service write is accepted | S | `WritePathRuleTests` | `ARegisteredServiceWrite_IsAccepted` | none |
| Removing the declaration is detected too | S | `WritePathRuleTests` | `ARegisteredServiceWrite_IsFlagged_OnceItsDeclarationIsRemoved` | one violation |
| Mixed set flags only the violations | S | `WritePathRuleTests` | `AllFourTogether_FlagOnlyTheViolations` | exactly the two unguarded types |
| The failure message tells the author what to do | S | `WritePathRuleTests` | `TheFailureMessage_NamesTheTypeAndBothRemedies` | names the type, both remedies, BR-026 and `trebovaniya.md` |
| The real assembly has no violation | S | `ReadOnlyEnforcementTests` | `TheApplicationAssembly_HasNoUnprotectedWritePath` | empty |
| The rule is not vacuous over the real assembly | S | `ReadOnlyEnforcementTests` | `TheLegitimacyCheckUseCase_IsARecognisedWritePath` | the one existing write path is seen |

## AC-008 The mode is evaluated at the moment of the write

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| The grace period expiring takes effect at once | I | `ReadOnlyModeTimingTests` | `TheGracePeriodExpiring_TakesEffectOnTheNextWrite_WithoutARestart` | write, advance, refusal |
| A suspension takes effect at once | I | `ReadOnlyModeTimingTests` | `ASuspensionRecorded_TakesEffectOnTheNextWrite_WithoutARestart` | refusal without a restart |
| The 7-day boundary is strict and moves with the clock | I | `ReadOnlyModeTimingTests` | `ExactlySevenDays_StillWrites_AndOneTickLaterDoesNot` | US-005 I-4 |
| Nothing is cached per process | I | `ReadOnlyModeTimingTests` | all four cases run in one host | two answers in one host |
| The guard and the scope are scoped, not singleton | I, S | `ReadOnlyEnforcementTests` | `TheGuardAndTheScope_AreScopedNotSingleton` | a new instance per DI scope |

## AC-009 Leaving read-only mode restores writes

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| A successful check ends the refusal | I | `ReadOnlyModeTimingTests` | `ASuccessfulCheck_EndsTheRefusal_InTheSameHost` | the next write commits |
| Resumption restores writes | I | `ReadOnlyModeTimingTests` | `AResumption_RestoresWrites_WithoutARestart` | the next write commits |
| The state write was permitted throughout | I | `PermittedServiceWriteTests` | `TheLegitimacyCheckWrite_RunsInReadOnlyMode` | the installation can leave the mode at all |

## AC-010 A refused write is logged, not audited

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| One `Warning` line naming operation and reason | I, Sec | `ReadOnlyRefusalLoggingTests` | `ARefusal_WritesOneWarningLine_NamingTheOperationAndTheReason` | exactly one, level `Warning` |
| The backstop's refusal is logged too | I, Sec | `ReadOnlyRefusalLoggingTests` | `TheBackstopRefusal_IsLoggedAsWell` | one `Warning` line |
| No personal data, no payload, no stack trace | I, Sec | `ReadOnlyRefusalLoggingTests` | `TheLine_CarriesNoPersonalDataAndNoPayload` | domain, client id and marker appear in no log file |
| The mode change is not re-logged | I | `ReadOnlyRefusalLoggingTests` | `RepeatedRefusals_DoNotRelogTheModeChange` | two refusals, at most one `ReadOnlyModeEntered` |
| No audit row and no data written | I, Sec | `ReadOnlyRefusalLoggingTests` | `ARefusal_WritesNoAuditRowAndNoData` | rows unchanged; no `audit_event` table |

## AC-011 Enforcement lives in Application only

| Scenario | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| Only `Application` decides the mode | S | `ReadOnlyEnforcementTests` | `TheGuard_IsImplementedOnlyInsideApplication` | only a `Logging…` decorator may live outside |
| `Web` holds no copy of the rule | S | `ReadOnlyEnforcementTests` | `TheWebLayer_HoldsNoCopyOfTheRule` | no guard and no unit of work in `Web` |
| The decorators are registered in front | I, Sec | `ReadOnlyEnforcementTests` | `TheDecorators_AreRegisteredInFrontOfTheGuardAndTheUnitOfWork` | resolved types are not the bare implementations |
| The US-005 architecture tests still pass unchanged | S | `ProjectReferenceTests` (existing, unmodified) | all | green, `FrameworkFreeProjects_ReferenceNoPackage` included |

## Validation rules

| Rule | Level | Test Class | Test Method | Expected Result |
|---|---|---|---|---|
| VR-001 empty or blank operation name | I | `ReadOnlyModeGuardTests` | `AnEmptyOperationName_IsRejected` (theory ×2) | `ArgumentException`; rows unchanged |
| VR-002 a declaration is visible while open | U | `ServiceWriteScopeTests` | `ADeclaration_IsVisibleWhileItIsOpen` | `Current` is the member |
| VR-002 disposing clears it | U | `ServiceWriteScopeTests` | `Disposing_ClearsTheDeclaration` | `Current` is null |
| VR-002 a second declaration is rejected | U | `ServiceWriteScopeTests` | `ASecondDeclaration_BeforeTheFirstIsDisposed_IsRejected` | `InvalidOperationException` |
| VR-002 an undefined member is rejected | U | `ServiceWriteScopeTests` | `AnUndefinedMember_IsRejected` | `ArgumentException` |
| VR-002 disposing twice is harmless | U | `ServiceWriteScopeTests` | `DisposingTwice_IsHarmless` | `Current` stays null |
| VR-003 the closed list | U | `PermittedServiceWriteTests` | `TheList_HasExactlyTheFourMembersOfBr026` | the four BR-026 members |

## Coverage summary

All eleven Acceptance Criteria and all three validation rules are mapped to
executable scenarios. Test classes: `ReadOnlyModeGuardTests`,
`ReadOnlyModeUnitOfWorkTests`, `PermittedServiceWriteTests`,
`ServiceWriteScopeTests`, `ReadsInReadOnlyModeTests`, `GoogleDataPortRuleTests`,
`ReadOnlyModeTimingTests`, `ReadOnlyRefusalLoggingTests` (Application), and
`WritePathRuleTests`, `ReadOnlyEnforcementTests` (Architecture).
