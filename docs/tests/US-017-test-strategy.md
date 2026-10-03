---
artifact_type: test_strategy
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:34:54Z
updated_at: 2026-10-03T15:34:54Z
produced_by: test-writer
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
supersedes: null
---

# US-017 Test Strategy — Retry, backoff and permission-error handling

## Scope

Classification and retry of Google failures in the adapter (FR-001 … FR-004),
what a run does with a final failure (FR-005, FR-012), the stored diagnosis
(FR-006), the Admin's "Last synchronization" block (FR-007 … FR-009), logging
and log bounds (FR-010, FR-011). Seam: the compile-only skeleton of OD-010.

## Levels

| Level | Class | What it proves |
|---|---|---|
| Adapter over a scripted HTTP handler (AC-014) | `Infrastructure/Google/GoogleClassroomReaderRetryTests` | classes, retry count, pauses × jitter on a manual `TimeProvider`, `Retry-After`, token-endpoint errors, paging with retries, `SyncGoogleRetry` warnings, shutdown during a pause, no Google text in the failure |
| Unit, use case over `SyncWorld` (TC-1) | `Application/UseCases/SynchronizationFailureHandlingTests` | stop vs skip, committed courses kept, `ProcessedCount` (I-5), diagnosis codes, blank name, clearing on success |
| Unit, domain | `SyncStateInvariantTests` (rewritten parts) | `FailRun` stores exactly the name; undeclared value refused |
| Unit, query | `GetLastSynchronizationQueryTests` | status/time mapping; unknown values → `Unexpected` |
| Integration, PostgreSQL (TC-2) | `Infrastructure/Persistence/SyncDiagnosisPersistenceTests` | round trip, clearing, legacy raw values → `Unexpected`, no pending model change |
| HTTP, host (TC-5) | `Web/Pages/LastSynchronizationBlockTests` | block in uk/en, every status and diagnosis, culture-formatted times, legacy value, read-only, Dean `403`, translation completeness |
| Host logging | `Web/Logging/SynchronizationFailureLoggingTests` | `SyncRunFailed` level by cause, `SyncCourseGone`, `SyncCourseNameBlank`, `RunId`, 64-char bound, no exception message, interval after failure |

## Scenarios

- **Positive:** a transient failure retried to success; a course gone or
  blank-named skipped while the run completes; the block for every state.
- **Negative:** configuration failures never retried; unexpected failures stop
  the run; Dean refused; no raw Google or exception text anywhere.
- **Boundary:** exactly 4 attempts; pauses at factor 0.8 and 1.0; `Retry-After`
  5 s, 120 s cap, HTTP date, invalid and past values; 64-character log bound
  with a short-value control; "" and whitespace names.
- **Validation:** VR-001 (`Retry-After` parsing), VR-002 (exact name match),
  VR-003 (blank), VR-004 (bound).
- **Security:** Admin-allowed / Dean-forbidden pair (TC-5); read-only viewable;
  no leak of Google detail into the exception, `SyncState`, log or page.
- **Persistence:** codes and count round-trip in PostgreSQL; legacy rows read
  as `Unexpected`; model snapshot unchanged (no migration).

## Fixtures

`FixedRetryJitter`, `CapturingLogger`, `FailingClassroomReader`,
`SyncFailureHostExtensions`, `LastSynchronizationHostExtensions`,
`LastSynchronizationTestData` (OD-010 keys, culture formatting); `SyncWorld`
gained `FailOnListing`, `ClearFailures`, `RunUsing`.

## Excluded scenarios and limitations

- Raw Google text on the page: unreachable, nothing raw is stored.
- An empty `last_error`: the existing `ck_sync_state_error_length` makes it
  unstorable; pinned by `AnEmptyError_CannotBeStored_SoItIsNeverRead`.
- Skipped courses are asserted through the store and `ProcessedCount`, not
  through `SynchronizationRunOutcome.SkippedCourses`, whose shape for the new
  skip reasons no artifact fixes.
- The diagnosis code on the `SyncRunFailed` line is matched in any property
  (OD-010 fixes no property name); `RunId` by name.
- Retry pauses are asserted as pending timers of the manual `TimeProvider`: the
  implementation must pause through the injected `TimeProvider`, and each pause
  must equal nominal × factor (or the usable `Retry-After`) within 1 ms.
- The `RunId` on `SyncGoogleRetry` comes from the background service's existing
  log scope; the adapter test proves the warning, not the scope.
- `AConfigurationFailure_IsLoggedAtError_WithTheRunIdAndTheCode` asserts a
  single Error line in that host run; a new unrelated Error line would break it.

## Open Decisions affecting testing

OD-010 (skeleton and fixed names) — resolved by the Owner, option 1. None open.
