---
artifact_type: security_review
story: US-017
version: 2
status: APPROVED
created_at: 2026-10-03T16:12:48Z
updated_at: 2026-10-03T16:31:02Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-017-retry-backoff-permission-errors.md
    version: null
  - path: trebovaniya.md
    version: 80
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/decisions/US-017-open-decisions.md
    version: 2
  - path: docs/designs/api/US-017-api-design.md
    version: 1
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
  - path: docs/designs/database/US-017-db-design.md
    version: 1
  - path: docs/designs/database/US-017-entity-model.md
    version: 1
  - path: docs/tests/US-017-test-strategy.md
    version: 1
  - path: docs/tests/US-017-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-017-test-generation-report.md
    version: 1
  - path: docs/evidence/US-017-implementation-report.md
    version: 2
supersedes: docs/reviews/security/US-017-security-review.md
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 4
security_sensitive: true
runtime_checks: FULL
---

# US-017 Security Review — Retry, backoff and permission-error handling

## Version 2 — re-review after IMPLEMENTATION attempt 2

**Result: PASS.** F-1 is resolved. There are no Critical, Major or Minor findings, and the four Informational notes
of version 1 stand. Next stage: HUMAN_PR_APPROVAL.

This version supersedes version 1, which was rewritten in place at the same path. Version 1's sections below are kept
for the record, with F-1 marked resolved.

**What changed (implementation report v2, §9):**
- `GoogleRetryHandler` now calls `response.Content.LoadIntoBufferAsync(attemptToken.Token)` right after the inner
  `SendAsync`, inside the same `using` scope as the linked attempt token.
- If buffering fails, the response is disposed and the exception is rethrown.
- There is a new optional `TimeSpan? attemptTimeout` on `GoogleRetryHandler` and as the 7th constructor parameter of
  `GoogleClassroomReader`. The default is `GoogleRetryHandler.DefaultAttemptTimeout` (100 s).
- Two new tests were added to `GoogleClassroomReaderRetryTests`.

**Verification of F-1:**

| Check | Evidence | Result |
|---|---|---|
| The body is bounded by the attempt timeout | The buffering call uses `attemptToken.Token`, which links the caller token and the attempt CTS, and runs before the token sources are disposed. Afterwards `HttpClient`'s own buffering finds the content already buffered and has nothing left to wait on. | PASS |
| Same probe as v1, with the fixed shape | The scratch probe was changed only by adding the same buffer-under-attempt-token call. It now prints `TaskCanceledException after 2,0s (attempt timeout 2s; caller cancel 12s)`. In v1 the read ended only at 12 s. | PASS |
| Token requests are covered | `ServiceAccountCredential` gets the same `TransportFactory` (`HttpClientFactory = httpClients`), so token POSTs pass through the same `GoogleRetryHandler`. | PASS |
| A body timeout counts as a transient attempt | The timeout raises `OperationCanceledException` without the caller's cancellation, so it is caught and paused, then retried. On the 4th attempt it propagates, and `GuardAsync` maps it to `Transient` / `GoogleUnavailable`. An I/O failure while buffering surfaces as `HttpRequestException` and takes the same path. Caller cancellation is still not caught and passes through `GuardAsync` unchanged. | PASS |
| Response disposal | A failed buffer disposes the response before rethrowing. A buffered `429`/`5xx` is disposed before the pause or the final throw, as in v1. No undisposed response is left on any retry path. | PASS |
| Worst case is bounded | At most 4 × 100 s per attempt plus 3 pauses capped at 120 s, about 12.7 min per request, consistent with spec §9. | PASS |
| No Google text leaks | Buffering keeps the body only inside the response object, as `HttpClient` did before. The final failure is still the bare `HttpRequestException("Google is unavailable.")`, and the leak tests still pass. | PASS |

**The seam cannot weaken production:**
- `InstallationServices` is unchanged by attempt 2. Its `GoogleClassroomReader` registration passes 6 arguments and no
  `attemptTimeout`, so production uses `DefaultAttemptTimeout` = 100 s.
- The constructor refuses zero and negative values with
  `ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout ?? 1 tick, TimeSpan.Zero)`. This includes
  `Timeout.InfiniteTimeSpan` (−1 ms), so the seam cannot switch the bound off.
- The value comes from no configuration key or request; only the code that builds the reader can set it.
- The seam is `public` only because the test project has no `InternalsVisibleTo`. This is acceptable; it adds no
  reachable surface.

**The new tests are meaningful and bounded:**
- `AStalledResponseBody_TimesOutTheAttempt_AndIsRetried`: a 200 ms real-time attempt timeout; the first response's
  content never completes (`StalledContent` waits on its cancellation token). The test asserts one retry warning,
  2 requests and the second page returned.
- `EveryResponseBodyStalling_StopsAtFourAttempts_AsATransientFailure`: steps through the 2/8/30 s pauses on the manual
  clock and asserts `Transient` / `GoogleUnavailable` after exactly 4 requests.
- Both tests wait at most 10 s of real time and need no real sleeping beyond the 200 ms timeouts (AC-011 holds for
  the pauses).
- The report states both tests fail with the buffering call removed. That matches the v1 analysis: without the fix,
  `HttpClient` buffers under an infinite timeout and the bounded wait fails.
- The scripted transport hands back the stalled content directly. The real `SocketsHttpHandler` content stream honours
  the same token, as the probe confirms.

**No regressions:**
- Build: 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln`: **2687 total, 2687 passed, 0 failed, 0 skipped** (3 m 44 s).
- `dotnet list package --vulnerable --include-transitive`: no vulnerable package in any of the 7 projects.
- No `.csproj` diff.
- Attempt 2 touched only `GoogleRetryHandler.cs`, `GoogleClassroomReader.cs` (seam) and
  `GoogleClassroomReaderRetryTests.cs`. Everything else matches v1's review.

## 1. Executive Summary (version 1, kept for the record)

**Result of version 1: CHANGES_REQUIRED, loop back to IMPLEMENTATION.** One Major finding (F-1, resolved in
version 2).

The Story adds a retrying HTTP transport under the Google client library, a shared failure classifier, a closed
diagnosis code in `SyncState`, a "Last synchronization" block on the Admin's connection page, re-levelled
failure logging and a 64-character bound on Google-supplied log values. Most security properties hold and are
proven by passing tests: only reads are retried, at most four attempts per request; configuration refusals are never
retried; no Google text (message, reason, body, header, URL) reaches `SyncState`, a log line, an exception or the
page; every Google-supplied log value is bounded; the block is Admin-only and a Dean gets `403`; the new query
writes nothing and the block stays viewable in read-only mode; the classifier moved without a behaviour change; no
dependency changed.

**F-1 (Major):** the Story set every Google `HttpClient` timeout to infinite and put a 100 s timeout on each attempt in
`GoogleRetryHandler`. That timeout covers only the inner `SendAsync`, which returns once the response **headers**
arrive. The response **body** is read later by `HttpClient`, and nothing bounds that read except the host's shutdown
token. A Google answer, or a half-open TCP connection, that stalls after its headers now blocks the
synchronization run until the process restarts. Before this Story the 100 s `HttpClient` timeout covered the body. A
scratch probe reproduced this (§4). This breaks the Specification's bounded worst case (§9, "a timeout" is transient
per FR-001). Because only one run happens at a time, the stall stops all synchronization, and the Admin's new block
shows "Running" indefinitely.

Build 0/0, full suite 2685/2685 green, no vulnerable package. Next: IMPLEMENTATION.

## 2. Reviewed Artifacts

See front matter `inputs`. Specification v1 is `APPROVED` (HUMAN_SPEC_APPROVAL 2026-10-03T14:58:23Z). OD-001 … OD-010
are resolved. No input is `SUPERSEDED`. Workflow: `current_stage: SECURITY_REVIEW`, attempt 1, after IMPLEMENTATION
`PASS`.

## 3. Security-Relevant Scope

- **Installation, public HTTPS:** `GET /admin/workspace-connection` and the re-rendered rejected `POST` (US-009)
  gain the read-only "Last synchronization" block. There is no new route, method, policy or antiforgery rule.
- **Background service → Google:** `GoogleClassroomReader` now runs every token and API request through
  `GoogleRetryHandler`. `GuardAsync` maps failures to `GoogleReadFailedException`
  (`Transient` / `Configuration` / `CourseGone` / `Unexpected`).
- **Application:** `RunSynchronizationUseCase` stops or skips per class, stores `SyncDiagnosis` by name and the committed
  count, and skips blank-named courses. `GetLastSynchronizationQuery` is a no-tracking read.
- **Domain:** `SyncState.FailRun(…, SyncDiagnosis)` (undeclared value → `ArgumentOutOfRangeException`).
- **Logging:** `SyncGoogleRetry` (2420), `SyncRunFailed` (5123) with the level chosen per diagnosis,
  `SyncCourseGone` (5130), `SyncCourseNameBlank` (5131), and `GoogleLogValue.Bounded` on every Google-supplied value.
- **Assets:** students' personal data (untouched by the new code paths), the service-account key (resolved per call,
  unchanged), `SyncState`, the Google quota that every school shares, the availability of synchronization.
- **Trust boundaries:** Google → adapter (answers are external input, VR-001); Controller → Application query;
  Application → PostgreSQL.

## 4. Environment and Tools

- .NET SDK 10.0.401, Windows 10; Docker running (Testcontainers PostgreSQL).
- `dotnet build ClassroomAgent.sln`: succeeded, 0 warnings, 0 errors.
- `dotnet test ClassroomAgent.sln`: 2685 total, 2685 passed, 0 failed, 0 skipped (3 m 44 s).
- `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`: no vulnerable package in any of the
  seven projects.
- `git diff` on `*.csproj` / `Directory.*.props`: empty.
- **Scratch probe (outside the repository, scratchpad only):** a BCL-only console app with a loopback TCP server that
  sends `200` headers with `Content-Length: 100`, then stalls. The client was an `HttpClient` with
  `Timeout = Infinite` over a handler that links a 2 s attempt timeout around `inner.SendAsync`, the same shape as
  `GoogleRetryHandler`, with a 12 s caller token standing in for `stoppingToken`. Output:
  `inner SendAsync returned (headers)`, then `TaskCanceledException after 12,0s`. The attempt timeout never fired; only
  the caller's token ended the read.
- No live Google API, no Control Plane, no database other than test containers. Credential files, `classroom_cache.db`
  and `.xlsx` files were not opened.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | No role or policy added; the block sits behind the existing `ConfigureWorkspaceConnection` policy (Admin). |
| SC-4 Authorization | PASS | `[Authorize(Policy = ConfigureWorkspaceConnection)]` is on the controller class (`WorkspaceConnectionController`) and the policy requires role Admin (`InstallationSecurityServices`). No new endpoint, `[AllowAnonymous]` or antiforgery exemption. Test `ADean_IsRefusedAndSeesNothingOfTheBlock`: `403`, with neither language's title nor diagnosis text in the body, against an Admin control. |
| SC-5 Read-only | PASS | `GetLastSynchronizationQuery` uses `GetForReadAsync`, which is `AsNoTracking().SingleOrDefaultAsync`. The query has no unit of work and no Google port. `InReadOnlyMode_TheBlockIsStillShown` (3 causes). The read-only skip of a run is unchanged and covered by existing US-007/US-013 tests. Skipped runs do not touch `SyncState` (I-8). |
| SC-8 Google | PASS (v2; FINDING in v1) | Scopes unchanged (5 read-only scopes in the reader, `GoogleDelegationScopes` untouched); the reader issues no write call (only `List`). Impersonation is unchanged. Retries are bounded (4 attempts, `Retry-After` capped at 120 s). Permission failures are neither retried nor swallowed: they stop the run with a stored code and an `Error` line (`AConfigurationRefusal_AtTheTokenEndpoint_IsFinalAtOnce`, `A403_…_AfterOneAttempt`). **F-1 (v1):** after the headers, a request was no longer time-bounded — resolved in v2 (body buffered under the attempt token). |
| SC-10 Hygiene | PASS | `GuardAsync` drops every inner exception. `GoogleReadFailedException` carries kind and code only. The final transient failure is a bare `HttpRequestException("Google is unavailable.")`. `SyncState.LastError` holds an enum name. `SyncRunFailed` carries the code plus the exception **type** name only for `Unexpected`. `SyncGoogleRetry` carries attempt, pause and integer status, with no URL or header. Tests: `TheFailure_CarriesNoTextGoogleSent` (marker proven present in the scripted body), `OneRetriedAttempt_WritesOneWarning…` (marker in the 503 body is absent from the warning), `AnUnexpectedFailure_IsLoggedAtError_AndLeaksNoExceptionMessage` (marker absent from every log file and the row), `ALegacyRunFailedValue_IsShownAsUnexpected_AndLeaksNothing`. |
| SC-11 Audit | PASS | No user action added; spec FR-013 says no audit event. `AuditAction` is unchanged. |
| SC-13 Outbound | PASS | Retries go through the same shared `SocketsHttpHandler` to the same Google endpoints. No new destination. |
| SC-2, SC-3, SC-6, SC-7, SC-9, SC-12 | NOT_APPLICABLE | No sign-in, AllowedAdmin, DB UI, channel or Contracts change. Key handling (`LoadKey`) is unchanged and still resolved per call; the move made no edit to it. |

## 6. Authentication and Authorization

- Requirement: the block is Admin-only (spec FR-008, §2 matrix v80); the Dean is forbidden.
- Endpoint access: GET and POST `/admin/workspace-connection`, Admin only (class-level policy). The block is built in
  `PageAsync`, which only those two actions reach. No service path exposes `SyncState` to another role.
- Anonymous endpoints: unchanged. The existing endpoint-enumeration test passes in the green suite.
- Tests: Admin positive tests (all `LastSynchronizationBlockTests`) and Dean `403`. Findings: none.

## 7. Credentials, Key and Google Access

- No password handling.
- Key: still resolved from the secret store per call and never stored or logged. A missing key gives
  `KeyUnavailable` with nothing sent (`AMissingKey_IsKeyUnavailable_AndNothingIsSent`).
- Scopes and impersonation: unchanged; the technical account is still the subject.
- Retries repeat a read only. The handler wraps the reader's own clients, so only Classroom `list` GETs and the
  JWT-bearer token POST (a grant, not a Workspace write) pass through it.
- Quota: at most 4 attempts per request. A run under a persistent `429` stops after the first exhausted request
  (`EveryAttemptFailing_StopsAtFourAttempts…`). Configuration refusals make exactly one attempt. The client library's
  backoff stays `None`.
- Classifier move: `ClassifyTokenError`, `ClassifyApiError` and `IsUnavailable` in `GoogleFailureClassifier` are
  textually identical to the methods deleted from `GoogleAccessProbe`, which now calls them at the same three sites.
  The probe's US-011 tests pass unchanged.
- Finding: F-1.

## 8. Sensitive Data Exposure

- Responses and views: the block shows a status, UTC instants and a translated diagnosis. No personal data. The stored
  value is never echoed (legacy `RunFailed:…` is rendered as the `Unexpected` text, and a test asserts it is absent).
- DTOs: `LastSynchronizationView` (Application record). It holds the Domain **enum** `SyncDiagnosis`, which is not an
  entity; the approved API design names it. See INFO-3.
- Logs: run id, Google course or submission ids (bounded), raw states (bounded), diagnosis code, exception type name,
  attempt, pause and status. No names, emails or grades. `SyncCourseNameBlank` logs the id, never the name.
- Exceptions: no Google detail, as in §5 SC-10.
- Audit, exports, telemetry: unchanged.

## 9. Input Validation

- Google answers (VR-001): status and `Retry-After` are read only to choose a pause. A negative or past value gives
  the nominal pause and a value above 2 min gives 2 min (`RetryAfter_ReplacesTheNominalPause_WhenUsable`). Bodies are
  read only by the library's error parser and classifier, and nothing is kept.
- Diagnosis read (VR-002): ordinal exact-name match against `Enum.GetNames`, not `Enum.TryParse`. Numeric, case and
  padding variants are rendered as `Unexpected` (`AValueThatIsNotADeclaredName_IsShownAsUnexpected`, 7 cases).
- Blank course name (VR-003): `string.IsNullOrWhiteSpace`, skipped before any read or transaction.
- Log bound (VR-004): `GoogleLogValue.Bounded` on all five call sites that log a Google value (`SyncCourseSkipped` ×2
  values, `SyncSubmissionStateUnrecognised` ×2, `SyncCourseSkippedByAge`, `SyncCourseGone`, `SyncCourseNameBlank`).
  `rg` finds no other log template that takes a Google value, and Application has no logger. Tests cover 200-char
  values plus short-value controls.
- No new request input. Findings: none.

## 10. API Security

No new operation, path, method or status. The view-model delta matches the approved `US-017-openapi.yaml` (status,
three instants, diagnosis). Errors are unchanged (`403` is the host error page). Findings: none.

## 11. Persistence and Configuration

- No schema change and no migration (db design). `last_error` stays `varchar(512)`. `FailRun` writes only a declared
  enum name.
- Failed runs now store the committed count (I-5), with no sensitive content.
- No configuration file changed. The retry parameters are constants.
- DI registers `RandomGoogleRetryJitter` as a singleton and the reader with the injected `TimeProvider`.
- No generated database file or `.xlsx` in the change set.
- Findings: none.

## 12. Logging, Audit and Telemetry

- Logging: reviewed in §8 and §9. The level split matches FR-010: `GoogleUnavailable` is logged at `Warning`, and
  configuration and unexpected failures at `Error`. Logs use compact JSON, so control characters in an id are escaped.
- Audit: none required.
- Hook telemetry: unchanged and git-ignored.
- Findings: none.

## 13. Dependencies

No package or project reference added (empty diff on `*.csproj`). The vulnerability scan is clean for all seven
projects, transitive dependencies included.

## 14. Security Test Coverage

| Requirement / abuse case | Test(s) | Result |
|---|---|---|
| Retry only transient, ≤ 4 attempts, pauses, `Retry-After` cap | `GoogleClassroomReaderRetryTests` (`ATransientStatus_…`, `ADroppedConnection_…`, `ATimeout_ThatIsNotTheCallersCancellation_IsRetried`, `EveryAttemptFailing_…`, `RetryAfter_…`, `ATokenRequest_Answering503_IsRetried`) | pass |
| Configuration refusals not retried | `AConfigurationRefusal_AtTheTokenEndpoint_IsFinalAtOnce`, `A403_ForADisabledApi_…`, `AnotherA403_…` | pass |
| No Google text in the failure, log, row or page | `TheFailure_CarriesNoTextGoogleSent`, `OneRetriedAttempt_WritesOneWarning…`, `AnUnexpectedFailure_…LeaksNoExceptionMessage`, `ALegacyRunFailedValue_…LeaksNothing` | pass |
| Log bound | `ALongCourseIdAndState_…`, `ALongSubmissionIdAndRawState_…` (+ controls) | pass |
| Dean forbidden / Admin allowed (TC-5) | `ADean_IsRefusedAndSeesNothingOfTheBlock` + Admin block tests | pass |
| Read-only: block viewable, query writes nothing | `InReadOnlyMode_TheBlockIsStillShown`; existing US-007/US-013 run-skip tests | pass |
| Shutdown during a pause | `ShutdownDuringAPause_EndsTheReadPromptly_AndSendsNothingFurther` | pass |
| **A request that stalls is bounded** | none (the implementation report records this gap) | **missing (F-1)** |

**The two tests corrected at IMPLEMENTATION were not weakened.**

- `SyncDiagnosisPersistenceTests` checks persistence round-trips, not read-only mode. The commit backstop correctly
  refused its direct write in a never-confirmed host. Starting it writable with no connection keeps the host's own run
  from touching `sync_state` (I-8), and every assertion is unchanged.
- `SynchronizationFailureLoggingTests` counts only `Sync*` events for "one Error" / "no Error". The excluded line is the
  US-011 startup self-check of the keyless host. Every `SyncRunFailed` assertion is unchanged, and the leak test still
  scans every log file unfiltered.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Dean opens the connection page to read sync diagnostics | `403`, nothing rendered | Dean test | PASS |
| Google returns a crafted error body or reason meant for a log or the page | Only the code and reason are matched; nothing is kept | classifier code, leak tests | PASS |
| Google returns a very long id or state to grow the log | Truncated to 64 characters | log-bound tests | PASS |
| Hostile `Retry-After` (huge, negative, past) to stall or hammer | Capped at 120 s; unusable values give the nominal pause | `RetryAfter_…` | PASS |
| Persistent `429` from the shared quota | 4 attempts, then the run stops | `EveryAttemptFailing_…` | PASS |
| Read-only installation keeps calling Google through retries | No run starts, so there are no retries | existing tests, FR-009 | PASS |
| Stored `last_error` tampered to numeric or odd-cased text | Shown as `Unexpected` | query tests | PASS |
| Response stalls after its headers (slow server or half-open TCP) | The attempt is bounded and counts as transient | probe in §4 | **FAIL (F-1)** |

## 16. Repository Hygiene

The working tree holds only Story source, tests, Story artifacts and workflow state. `classroom_cache.db`, the two
root `.xlsx` files, `google_credentials.json` and `dac-classroom-agent-*.json` show as ignored (`!!`) and were not
opened. No secret-like file is untracked. The scratch probe lives in the session scratchpad, not the repository.

## 17. Deviations

- Implementation report §7.1 (HttpClient timeout off, 100 s per attempt on `TimeProvider.System`): the per-attempt
  timeout runs on the system clock, which is acceptable in production. However, it does not bound the whole request
  (F-1), so the report's statement that "each attempt is bounded" is incomplete.
- Implementation report §7.2: the Owner-approved test corrections were verified as not weakened (§14).
- No other deviation from the approved security requirements.

## 18. Findings

### F-1 — Major — GOOGLE_ACCESS (availability) — SC-8; spec FR-001, FR-002, §9 — RESOLVED in version 2

**Resolution:** implementation attempt 2 buffers the body under the attempt token in `GoogleRetryHandler`, adds a
validated `attemptTimeout` seam with the default of 100 s left unchanged in DI, and adds two stalled-body tests. The
v1 probe now ends at the attempt timeout. Evidence is in the Version 2 section above. The text below is the original
version-1 finding.

- **Affected:** `src/ClassroomAgent.Infrastructure/Google/GoogleRetryHandler.cs` (`SendAsync`, attempt timeout
  scope); `src/ClassroomAgent.Infrastructure/Google/GoogleClassroomReader.cs` (`CreateService` `HttpClientTimeout =
  Timeout.InfiniteTimeSpan`, `TransportFactory.CreateHttpClient` `client.Timeout = Timeout.InfiniteTimeSpan`).
- **Observed:** the linked attempt token wraps only `_invoker.SendAsync(request, attemptToken.Token)`.
  `SocketsHttpHandler` completes that call once the headers are read, and the `CancellationTokenSource` is disposed at
  the end of the `try` block. The body is then buffered by `HttpClient.SendAsync` (default `ResponseContentRead`)
  under the client's own timeout, now infinite, linked only to the caller's token. For a synchronization run that is
  `stoppingToken`. The scratch probe (§4) shows a 2 s attempt timeout not firing and the read ending only on the caller
  token. The token request takes the same path, because the credential uses the same factory. Before US-017 the 100 s
  `HttpClient` timeout bounded the body.
- **Expected:** every attempt, body included, ends within the per-attempt bound and is then treated as transient
  (FR-001 "a timeout"). The worst case of one request stays finite, about 4 × 100 s + 3 × 120 s.
- **Risk:** a stalled body hangs the only synchronization run until process restart. `SyncRunCoordinator` allows one
  run at a time, so no scheduled or requested run starts, `SyncState` stays `Running`, and the Admin's new block reports
  "Running" indefinitely. The run produces no log line. This is a regression introduced by this Story; no
  confidentiality impact.
- **Required correction:** bring the response body under the per-attempt timeout. One way is to buffer the content
  inside `GoogleRetryHandler` under the attempt token (`await response.Content.LoadIntoBufferAsync(token)` before
  returning) and treat a timeout or `HttpRequestException` during buffering like a failed attempt (dispose, retry while
  attempts remain). The design is the implementor's choice, but the bound must cover the whole attempt for both API and
  token requests.
- **Loop-back:** IMPLEMENTATION.
- **Verification after correction:** a test through the real adapter over a scripted handler whose response content
  stalls after the headers must show the attempt ends as transient and the read fails with `GoogleUnavailable` after
  4 attempts. To keep that test fast without real sleeping (AC-011, TC-4), the attempt timeout needs a seam: an
  injectable duration, or a timer source separate from the pause clock. This also closes the gap recorded in
  implementation report §7.1. Adding this test at IMPLEMENTATION needs the Owner's agreement, as the §7.2 corrections
  had. Otherwise the orchestrator routes it through TEST_WRITING. Re-run the full suite.

### INFO-1 — OTHER — `GoogleRetryHandler`

The per-attempt timer runs on `TimeProvider.System` while pauses use the injected provider. This is correct in
production and documented in the class. Once F-1 adds a seam, the attempt timeout becomes testable. No separate
correction.

### INFO-2 — GOOGLE_ACCESS — `GoogleFailureClassifier.ClassifyApiError`

A `403` whose reason is `rateLimitExceeded`, `userRateLimitExceeded` or `quotaExceeded` is classified
`TechnicalAccountCannotRead`. It is not retried and is shown as a delegation problem. This errs on the
quota-protecting side and is exactly the US-011 probe behaviour that OD-006 requires sharing. Classroom signals quota
with `429`. No correction; if the Admin is misdirected in practice, a later Story can raise an Open Decision.

### INFO-3 — DATA_EXPOSURE — `LastSynchronizationView`

The view model carries the Domain enum `SyncDiagnosis`, and the view adds `@using ClassroomAgent.Domain.Enums`.
AD-8 forbids entities, not enums, and the approved API design specifies this type. No personal data. No correction.

### INFO-4 — OTHER — `GoogleRetryHandler`

The same `HttpRequestMessage` is re-sent on retry. This is safe for the reader's bodiless GETs and the buffered token
form. It would need revisiting if a streamed request body were ever routed through the handler, which is outside
read-only Google access. No correction.

## 19. Positive Controls

- Closed-list classification with no Google text kept, and inner exceptions dropped.
- Exact-name diagnosis parsing that refuses numeric, case and padding variants.
- `FailRun` refuses an undeclared enum value.
- A bounded number of attempts and a capped `Retry-After`.
- Configuration refusals are never retried.
- The pause is cancelled at shutdown, and no request is sent after that.
- Every Google-supplied log value is bounded.
- The failure log level matches the cause.
- The block is Admin-only and the Dean gets `403`.
- The no-tracking query works in read-only mode.
- Read-only scopes, technical-account impersonation and per-call key resolution are unchanged.
- No new package, endpoint, configuration key or outbound destination.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 … OD-010 are resolved.

## 21. Review Limitations

- This was a code, configuration and test review plus the full automated suite and a vulnerability scan.
- No penetration test and no manual browser session.
- F-1 was reproduced with a BCL-only probe of the same handler shape, not with the production `GoogleRetryHandler`
  (it is `internal`, and the review does not modify the repository). The conclusion follows from `HttpClient`
  buffering the body outside the handler, which holds whatever the Google library's completion option is: with
  `ResponseHeadersRead` the body would still be read outside the attempt token.

## 22. Verdict Rationale

**Version 2:** implementation report v2 records a green build and suite, and I re-observed them (2687/2687, 0
skipped). F-1 is resolved with code, test and probe evidence. Every touched SC item, including SC-8, is now PASS. There
is no Critical, Major or Minor finding and no blocking Open Decision. The security-sensitive Acceptance Criteria
(AC-002, AC-003, AC-005, AC-007, AC-008, AC-009) are covered by passing tests. Verdict **PASS**, so the next stage is
HUMAN_PR_APPROVAL.

**Version 1 (kept for the record):**

The implementation report records a green build and suite, re-observed here (2685/2685). All Open Decisions are
resolved and every input is current. One Major finding (F-1) stands: the Story removed the only bound on the response
body phase of a Google request, and an unbounded hang stops synchronization for the installation. That contradicts
the Specification's bounded worst case and FR-001's treatment of a timeout. The artifacts are correct and the code
does not meet them, so the verdict is **CHANGES_REQUIRED** with `loop_back_stage: IMPLEMENTATION` (key
`changes_required`). Every other touched SC item is PASS. The four Informational notes require no correction.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-017
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-017-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "F-1 (v1 Major) RESOLVED: GoogleRetryHandler buffers the body under the attempt token (API and token requests); attemptTimeout seam defaults to 100 s, DI passes none, zero/negative/infinite refused; two stalled-body tests, bounded, fail without the fix; probe ends at the attempt timeout."
    - "INFO-1: per-attempt timer on TimeProvider.System — acceptable in production; now testable through the seam."
    - "INFO-2: 403 rateLimitExceeded/userRateLimitExceeded/quotaExceeded classify as TechnicalAccountCannotRead (not retried) — quota-safe, identical to the US-011 probe per OD-006."
    - "INFO-3: LastSynchronizationView carries the Domain enum SyncDiagnosis (not an entity; approved API design) — no correction."
    - "INFO-4: GoogleRetryHandler re-sends the same HttpRequestMessage — safe for bodiless GETs and the buffered token form only."
```
