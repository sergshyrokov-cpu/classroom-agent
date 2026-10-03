---
artifact_type: implementation_report
story: US-017
version: 2
status: DRAFT
created_at: 2026-10-03T16:06:43Z
updated_at: 2026-10-03T16:25:33Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-017-retry-backoff-permission-errors.md
    version: null
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 2
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
  - path: docs/designs/api/US-017-api-design.md
    version: 1
  - path: docs/designs/database/US-017-db-design.md
    version: 1
  - path: docs/designs/database/US-017-entity-model.md
    version: 1
  - path: docs/tests/US-017-test-strategy.md
    version: 1
  - path: docs/tests/US-017-ac-test-matrix.md
    version: 1
  - path: docs/reviews/security/US-017-security-review.md
    version: 1
supersedes: null
attempt: 2
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-017 Implementation Report — Retry, backoff and permission-error handling

## 1. Summary

Implemented. A Google read made by a synchronization run is classified
(transient / configuration / course gone / unexpected) by a classifier now
shared with "Check access"; transient failures are retried in the adapter (at
most 4 attempts, 2/8/30 s × jitter, `Retry-After` capped at 120 s) on the
injected `TimeProvider`; the run stops on a final failure with a closed-list
diagnosis in `SyncState.LastError`, or skips a gone or blank-named course and
goes on. The Admin's connection page shows a "Last synchronization" block in
Ukrainian and English. Log lines follow §8 levels and bound every
Google-supplied value to 64 characters.

Build 0/0; full suite **2687/2687** green (attempt 2), 0 skipped; `dotnet format
--verify-no-changes` clean. Security-sensitive: yes (Google access, logging of
external data, an Admin-only page).

Work was done in two parts by Sonnet subagents from fixed briefs; this session
reviewed both reports, ordered one correction (HttpClient timeout, §7), made the
two Owner-approved test corrections (§7) and ran the final validation itself.

## 2. Source artifacts

As in the front matter. `trebovaniya.md` v80.

## 3. Acceptance Criteria

| AC | Implementation | Tests (`ac_test_matrix`) | Status |
|---|---|---|---|
| AC-001, AC-002 | `Infrastructure/Google/GoogleRetryHandler` (attempts, pauses, `Retry-After`, per-attempt timeout), `GoogleClassroomReader.GuardAsync` | `GoogleClassroomReaderRetryTests`, `SynchronizationFailureHandlingTests`, `SynchronizationFailureLoggingTests` | pass |
| AC-003 | `GoogleFailureClassifier` (shared with `GoogleAccessProbe`), `GoogleClassroomReader` key check, `RunSynchronizationUseCase` stop | same | pass |
| AC-004 | reader `CourseGone` on per-course 404; use case skip + `CoursesGone`; `SyncCourseGone` log | same | pass |
| AC-005 | `RunSynchronizationUseCase.DiagnosisOf` → `Unexpected`; type name only on the Error line | same | pass |
| AC-006, AC-013 | `GetLastSynchronizationQuery`, `WorkspaceConnectionPageModel.LastSynchronization`, `Views/WorkspaceConnection/Index.cshtml`, `LastSync.*` resx keys | `LastSynchronizationBlockTests`, `GetLastSynchronizationQueryTests`, `SyncDiagnosisPersistenceTests` | pass |
| AC-007 | unchanged `ConfigureWorkspaceConnection` policy | `LastSynchronizationBlockTests.ADean_IsRefusedAndSeesNothingOfTheBlock` | pass |
| AC-008 | query has no write and no read-only guard (viewing, BR-026) | `InReadOnlyMode_TheBlockIsStillShown` + existing read-only tests | pass |
| AC-009 | `Web/BackgroundServices/GoogleLogValue` (64-char bound) on every Google value in sync log lines | `SynchronizationFailureLoggingTests` | pass |
| AC-010 | use case skips a blank name before reads and transaction; `SyncCourseNameBlank` | `SynchronizationFailureHandlingTests`, `SynchronizationFailureLoggingTests` | pass |
| AC-011 | ports / HTTP handler substituted; manual `TimeProvider` | all of the above | pass |
| AC-012 | `Task.Delay(pause, timeProvider, ct)` in `GoogleRetryHandler` | `ShutdownDuringAPause_EndsTheReadPromptly_AndSendsNothingFurther` | pass |
| AC-014 | tests drive the real `GoogleClassroomReader` over `ScriptedHttpHandler` | `GoogleClassroomReaderRetryTests` | pass |

## 4. Change set

Production (`src/`):

| File | Trace |
|---|---|
| `Domain/Enums/SyncDiagnosis.cs` (new) | FR-006, entity model §1 |
| `Domain/Entities/SyncState.cs` | `FailRun(…, SyncDiagnosis)`, string overload removed — entity model §2 |
| `Application/Models/GoogleReadFailureKind.cs`, `Application/Ports/GoogleReadFailedException.cs` (new) | FR-001, FR-004, OD-010 |
| `Application/Models/LastSynchronizationStatus.cs`, `LastSynchronizationView.cs` (new) | API design §3 |
| `Application/Models/SynchronizationRunOutcome.cs` | FR-005, FR-010 — diagnosis, exception type (Unexpected only), gone / blank-name courses reported upward (Application has no logger) |
| `Application/UseCases/RunSynchronizationUseCase.cs` | FR-005, FR-006, FR-012, I-5; "RunFailed:" helper removed |
| `Application/UseCases/GetLastSynchronizationQuery.cs` (new) | FR-007, db design §4 (exact-name match) |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-007, OD-010 keys, NFR-073 |
| `Infrastructure/Google/GoogleFailureClassifier.cs` (new) | FR-001 — classification shared, moved out of the probe |
| `Infrastructure/Google/GoogleAccessProbe.cs` | calls the shared classifier; behaviour unchanged |
| `Infrastructure/Google/GoogleRetryHandler.cs` (new) | FR-002, FR-003, VR-001 |
| `Infrastructure/Google/GoogleClassroomReader.cs` | FR-001 … FR-004, FR-010 (`SyncGoogleRetry`); single constructor; HttpClient timeout off (§7) |
| `Infrastructure/Google/IGoogleRetryJitter.cs`, `RandomGoogleRetryJitter.cs` (new) | FR-003, I-2 |
| `Web/BackgroundServices/SynchronizationBackgroundService.cs` | FR-010, FR-011 — `SyncRunFailed` level by cause, `SyncCourseGone`, `SyncCourseNameBlank` |
| `Web/BackgroundServices/GoogleLogValue.cs` (new) | FR-011, I-4 |
| `Web/Configuration/InstallationServices.cs` | DI: reader constructor, jitter, query — supporting |
| `Web/Controllers/WorkspaceConnectionController.cs`, `WorkspaceConnectionPageModel.cs`, `Views/WorkspaceConnection/Index.cshtml` | FR-007, FR-008, API design §2 |

Tests: the files listed in `docs/evidence/US-017-test-generation-report.md`,
plus at this stage `GoogleClassroomReaderTests.cs` (constructor only),
`SyncDiagnosisPersistenceTests.cs` and `SynchronizationFailureLoggingTests.cs`
(§7). No migration (db design: no schema change). No configuration file
changed. No secret, database file or IDE file in the change set.

## 5. Validation evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln` | 2685 total, 2685 passed, 0 failed, 0 skipped (3 m 31 s) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |

Red → green: the 142 failures recorded at TEST_WRITING are all green; no other
test changed state.

## 6. Configuration changes

None. Retry parameters are constants from the requirements (FR-002), not
configuration (Story out of scope).

## 7. Deviations and discovered problems

1. **HttpClient timeout bounded the retry sequence** (found in review of part 1).
   The retry handler sits beneath `HttpClient`, whose 100 s default timeout
   would have cut off the pauses (up to 3 × 120 s) and never retried a real
   slow attempt. Fixed: every client the reader creates has an infinite
   `HttpClient` timeout; `GoogleRetryHandler` enforces a **100 s per-attempt**
   timeout (the library's former default, no new value invented), and an
   attempt that times out is transient. **That per-attempt timer runs on
   `TimeProvider.System`**, not the injected provider: on the manual clock it
   appears among the pending timers the retry tests read as the pause and broke
   7 tests intermittently. **No test covers the 100 s per-attempt timeout
   firing** — a recorded gap for SECURITY_REVIEW / a later Story.
2. **Two TEST_WRITING defects, corrected by Owner decision in conversation**
   (2026-10-03), assertions not weakened:
   - `SyncDiagnosisPersistenceTests` (9 cases) started a host that had never
     confirmed legitimacy — read-only, so the commit backstop correctly
     refused the test's own direct write. Now started not read-only and with no
     saved connection, so the host's own scheduled run is skipped and never
     touches `sync_state` (I-8).
   - `SynchronizationFailureLoggingTests` (2 cases) counted every Error line of
     the host, including the startup access self-check's own Error in the
     keyless test host. Now counted among synchronization events (`Sync*`)
     only; the `SyncRunFailed` assertions are unchanged.
3. `SyncRunFailed` is one `[LoggerMessage]` with a level parameter: two methods
   with one `EventName` are a build error (SYSLIB1025). Its `ExceptionType`
   property is the literal `none` unless the diagnosis is `Unexpected`; never a
   message.
4. Open findings US-014 F-1 and US-015 F-1 (unbounded Google values in logs) are
   **closed** by `GoogleLogValue`; US-014 I-5 (blank course name) is **closed**.

## 8. Open Decisions

None open. OD-001 … OD-010 resolved. No package added.

## 9. Attempt 2 — security review F-1

SECURITY_REVIEW v1 returned CHANGES_REQUIRED with **F-1 (Major)**: the 100 s
per-attempt timeout covered only the response headers; with the `HttpClient`
timeout infinite, a stalled body or half-open connection could hang the single
run forever. Fixed in `Infrastructure/Google/GoogleRetryHandler.cs`: after
`SendAsync`, `response.Content.LoadIntoBufferAsync(attemptToken)` runs under
the same attempt token, so the bound covers the whole attempt, token requests
included; a body timeout is a failed transient attempt (response disposed,
retried; after the 4th, `Transient` / `GoogleUnavailable`). Caller cancellation
still ends everything at once.

Seam: optional `TimeSpan? attemptTimeout` on `GoogleRetryHandler` and as an
optional 7th constructor parameter of `GoogleClassroomReader` (validated > 0;
default `GoogleRetryHandler.DefaultAttemptTimeout` = 100 s; DI unchanged; public
because the test project has no `InternalsVisibleTo`).

One new test pair authorised by the Owner at this stage (2026-10-03, option 1),
in `GoogleClassroomReaderRetryTests`:
`AStalledResponseBody_TimesOutTheAttempt_AndIsRetried`,
`EveryResponseBodyStalling_StopsAtFourAttempts_AsATransientFailure` (200 ms
real-time attempt timeout, pauses on the manual clock, waits bounded at 10 s).
Proven to fail without the fix: with the buffering call removed both failed on
their bounded wait; restored afterwards. This also closes the gap recorded in
§7.1 (no test of the per-attempt timeout firing).

Validation: build 0/0; `dotnet test ClassroomAgent.sln` **2687 total, 2687
passed, 0 skipped**; `dotnet format --verify-no-changes` exit 0.
