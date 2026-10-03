---
artifact_type: entity_model
story: US-017
version: 1
status: DRAFT
created_at: 2026-10-03T15:05:52Z
updated_at: 2026-10-03T15:05:52Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-017-spec.md
    version: 1
  - path: docs/designs/api/US-017-openapi.yaml
    version: 1
  - path: docs/designs/database/US-017-db-design.md
    version: 1
supersedes: null
---

# US-017 Entity Model — Retry, backoff and permission-error handling

## 1. New domain type

**`SyncDiagnosis`** — enum in `ClassroomAgent.Domain/Enums`, one file:

`ScopeNotAuthorized`, `TechnicalAccountUnknown`, `TechnicalAccountCannotRead`,
`ApiNotEnabled`, `KeyUnavailable`, `KeyRejected`, `GoogleUnavailable`,
`Unexpected`.

Business meaning: why the last synchronization failed (spec FR-006). The first
six mirror `AccessCheckStepOutcome` of the same names (Application, US-011) on
purpose — the name of one problem is the same in both places (OD-006). The
domain does not reference the Application enum (AD-3); the mapping between the
two lives in `Application`.

## 2. Changed entity method

**`SyncState.FailRun`**

| Before | After |
|---|---|
| `FailRun(DateTimeOffset finishedAt, int processedCount, string error)` | `FailRun(DateTimeOffset finishedAt, int processedCount, SyncDiagnosis diagnosis)` |

Writes `LastError = diagnosis.ToString()`. An undeclared enum value (a cast
integer) is refused with `ArgumentOutOfRangeException`, so `LastError` can only
ever hold a declared name. The truncation branch becomes unreachable and is
removed with the string parameter. `LastSuccessfulRunAt` untouched, as today.

No property changes: `LastError` stays `string?` (db design §2). `CompleteRun`,
`BeginRun`, `BeginFirstRun` unchanged.

## 3. Application-side types (named for traceability; design owned by the spec)

| Type | Layer | Purpose |
|---|---|---|
| a Google read failure with its class (transient / configuration + outcome / course gone / unexpected) | `Application` (`Ports` or `Models`) | what the adapter reports upward, no Google detail (spec FR-004) |
| `LastSynchronizationView` + `SyncDiagnosis` code in the DTO | `Application/Models` | the page block (API design §3); the DTO carries its own status enum `NeverRun / Running / Completed / Failed`, never the domain entity (AD-8) |
| a query port reading `SyncState` | `Application/Ports`, implemented in `Infrastructure` | db design §4 |

## 4. Mapping to business concepts and DTOs

| Business concept (§3 `SyncState`) | Entity | DTO (`LastSynchronizationView`) |
|---|---|---|
| статус | `Status` | `status` (+ `NeverRun` when no row) |
| время последней синхронизации | `StartedAt`, `FinishedAt` | `startedAt`, `finishedAt` |
| последняя успешная | `LastSuccessfulRunAt` | `lastSuccessfulRunAt` |
| последняя ошибка | `LastError` (a `SyncDiagnosis` name) | `diagnosis` (unknown → `Unexpected`) |
| счётчик | `ProcessedCount` | not shown (spec FR-007 lists no counter) |
