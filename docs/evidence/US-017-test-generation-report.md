---
artifact_type: test_generation_report
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:34:54Z
updated_at: 2026-10-03T15:34:54Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 2
  - path: docs/tests/US-017-test-strategy.md
    version: 1
  - path: docs/tests/US-017-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-017 Test Generation Report

**Overall: PASS (red phase verified).** Every Acceptance Criterion is mapped
(`docs/tests/US-017-ac-test-matrix.md`); the build is clean; every failure is a
new or re-expressed test failing for the expected reason; no existing test
regressed.

## Open Decision raised and resolved

**OD-010** — compile-only skeleton plus the names the tests fix (log events,
translation keys). Resolved by the Owner on 2026-10-03, option 1.

## Production skeleton (OD-010; IMPLEMENTATION owns it)

Created: `Domain/Enums/SyncDiagnosis.cs`,
`Application/Models/GoogleReadFailureKind.cs`,
`Application/Models/LastSynchronizationStatus.cs`,
`Application/Models/LastSynchronizationView.cs`,
`Application/Ports/GoogleReadFailedException.cs` (data type, written in full),
`Application/UseCases/GetLastSynchronizationQuery.cs` (throws),
`Infrastructure/Google/IGoogleRetryJitter.cs`.
Modified: `Domain/Entities/SyncState.cs` (new throwing
`FailRun(…, SyncDiagnosis)` overload; the string overload stays until
IMPLEMENTATION removes it — no test calls it any more),
`Infrastructure/Google/GoogleClassroomReader.cs` (new throwing constructor with
`TimeProvider`, `IGoogleRetryJitter`, `ILogger`). Nothing registered in DI.

## Test files

Created (`tests/ClassroomAgent.Tests/`):
`Infrastructure/Google/GoogleClassroomReaderRetryTests.cs`,
`Application/UseCases/SynchronizationFailureHandlingTests.cs`,
`Application/UseCases/GetLastSynchronizationQueryTests.cs`,
`Infrastructure/Persistence/SyncDiagnosisPersistenceTests.cs`,
`Web/Pages/LastSynchronizationBlockTests.cs`,
`Web/Logging/SynchronizationFailureLoggingTests.cs`;
fixtures `TestInfrastructure/FixedRetryJitter.cs`, `CapturingLogger.cs`,
`FailingClassroomReader.cs`, `SyncFailureHostExtensions.cs`,
`LastSynchronizationHostExtensions.cs`, `LastSynchronizationTestData.cs`.

Modified: `TestInfrastructure/SyncWorld.cs` (`FailOnListing`, `ClearFailures`,
`RunUsing`; defaults unchanged); `Application/UseCases/SyncStateInvariantTests.cs`
(string-overload tests for whitespace and truncation replaced by the closed-list
tests — that behaviour is removed by the entity model);
`SynchronizationRunTests.cs` (`AFailedRun_StoresACategoryAndAShortMessage` →
`AFailedRun_StoresACodeOfTheClosedList`); `CourseWorkFailureTests.cs` and
`CourseImportTests.cs` (old `RunFailed:` / `":"` assertions → `"Unexpected"`).
No test was deleted or disabled.

## Commands

`dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln` and per
class `--filter FullyQualifiedName~<Class>`. Docker running (Testcontainers).
The test-writing work was delegated in three parts to Sonnet subagents with
fixed briefs; the orchestrating session reviewed their reports and ran the full
suite itself.

## Execution evidence

Build: 0 warnings, 0 errors.

Full suite: **2685 total, 2543 passed, 142 failed, 0 skipped.**

| Class | Total | Failed | Passed | Red reason |
|---|---|---|---|---|
| GoogleClassroomReaderRetryTests | 33 | 33 | 0 | `NotImplementedException` from the skeleton constructor |
| SynchronizationFailureHandlingTests | 23 | 23 | 0 | old behaviour: `RunFailed:<Type>` instead of the code; gone / blank-name course fails the whole run |
| GetLastSynchronizationQueryTests | 19 | 19 | 0 | `NotImplementedException` (query, `FailRun`) |
| SyncStateInvariantTests | 18 | 11 | 7 | `NotImplementedException` from `FailRun` |
| SynchronizationRunTests | 7 | 2 | 5 | `NotImplementedException` |
| CourseWorkFailureTests | 3 | 1 | 2 | `RunFailed:InvalidOperationException` ≠ `Unexpected` |
| CourseImportTests | 12 | 1 | 11 | same |
| SyncDiagnosisPersistenceTests | 14 | 12 | 2 | `NotImplementedException` |
| LastSynchronizationBlockTests | 34 | 33 | 1 | translation keys `LastSync.*` missing |
| SynchronizationFailureLoggingTests | 10 | 7 | 3 | code missing on `SyncRunFailed`; Error instead of Warning; run `failed` instead of `completed`; values unbounded |

33 + 23 + 19 + 11 + 2 + 1 + 1 + 12 + 33 + 7 = **142** — exactly the failures of
the suite, so no pre-existing test fails.

## New tests passing in red, and why

- `SyncStateInvariantTests.TheClosedList_IsExactlyTheEightNames` — declaration
  guard on the enum.
- `SyncDiagnosisPersistenceTests.AnEmptyError_CannotBeStored_SoItIsNeverRead`,
  `Model_HasNoPendingChanges_SoNoMigrationIsNeeded` — characterisation of
  existing schema facts the design relies on.
- `LastSynchronizationBlockTests.TheTwoCultures_FormatTheTestInstantDifferently`
  — fixture sanity, so the uk/en date theory cannot pass on one format.
- `SynchronizationFailureLoggingTests.AShortCourseIdAndState_AreLoggedUnchanged`,
  `AShortSubmissionIdAndRawState_AreLoggedUnchanged` — the controls of the
  64-character bound.
- `SynchronizationFailureLoggingTests.AfterAConfigurationFailure_TheNextRunIsDueAfterTheNormalInterval`
  — US-013 OD-003 already schedules after a failure; a regression guard with a
  real scripted failure (the existing schedule test uses a hand-edited row).

The block tests fail on the missing keys before reaching HTTP; their host
fixtures (Admin with a stored language, seeded rows including the legacy value,
read-only mode, Dean 403) were proven by a temporary probe test, since deleted.

## Untested Acceptance Criteria

None. AC-011 holds by construction (ports or HTTP handler substituted, manual
`TimeProvider`).

## Notes for IMPLEMENTATION

- Pause through the injected `TimeProvider` (e.g. `Task.Delay(span,
  timeProvider, ct)`); each pause equals nominal × factor, or the usable
  `Retry-After` capped at 120 s, within 1 ms. Retry-After is not multiplied by
  the factor.
- The adapter's pause count is located by total requests (token + API).
- Register the new reader constructor in DI and remove the old one and the
  string `FailRun` overload.
- `SyncCourseGone`, `SyncCourseNameBlank` need the outcome to carry the skip
  reason up to the background service (Application has no logger).
- The `SyncRunFailed` line must carry the diagnosis code in some property.
- DB design §2 was corrected at TEST_WRITING time to name the existing
  `ck_sync_state_error_length` / `ck_sync_state_terminal_fields` checks it had
  wrongly called absent; no design decision changed.
