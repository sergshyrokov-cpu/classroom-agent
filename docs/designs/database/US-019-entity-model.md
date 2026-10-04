---
artifact_type: entity_model
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T06:35:40Z
updated_at: 2026-10-04T06:35:40Z
produced_by: db-designer
inputs:
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

# US-019 Entity Model — Trigger a synchronization from the UI

## 1. Business concepts → entities

| Business concept (`trebovaniya.md`) | Entity / type | Where |
|---|---|---|
| "ручной запуск синхронизации" (§5) as an audited action | `AuditAction.SynchronizationRequested` | `Domain/Enums/AuditAction.cs` (new member) |
| The audit row of a press | `AuditEvent` (existing entity) | `Domain/Entities/AuditEvent.cs` — two new factories |
| The request for a run | not an entity — process state of `SyncRunCoordinator` | `Web/BackgroundServices` (existing) |
| The run's progress and result | `SyncState` (existing, unchanged) | `Domain/Entities/SyncState.cs` |

## 2. Domain changes

### 2.1 `AuditAction`

Gains `SynchronizationRequested` after `RetentionPurgeRun`, with a doc comment
citing US-019 spec FR-005 and §5. Code `'synchronization_requested'`.

### 2.2 `AuditEvent` factories

Two new static factories, in the style of `AccessCheckRun` / `AccessCheckRefused`:

```csharp
public static AuditEvent SynchronizationRequested(
    long appUserId, AppRole actorRole, DateTimeOffset occurredAt, string? requestId);

public static AuditEvent SynchronizationRequestRefused(
    long appUserId, AppRole actorRole, AuditRefusalCategory category,
    DateTimeOffset occurredAt, string? requestId);
```

- `ActorType = AppUser`, `ActorId = appUserId`, `ActorRole = actorRole` — the
  first audited action of an installation that both roles perform, so the role is
  a parameter, never hard-coded.
- `TargetType = null`, `TargetId = null` (db-design §2.2).
- `SynchronizationRequestRefused` accepts only `ReadOnlyMode` and
  `ConnectionNotUsable`; any other category throws
  `ArgumentOutOfRangeException`, as `AccessCheckRefused` does.
- An undeclared `AppRole` value throws `ArgumentOutOfRangeException`.

No other entity changes. `AuditTargetType`, `AuditRefusalCategory`,
`AuditActorType`, `AuditOutcome`, `PermittedServiceWrite` are unchanged.

## 3. Infrastructure mapping

`AuditEventConfiguration`:

- `ActionCode` / `ActionFromCode` gain the pair
  `SynchronizationRequested` ↔ `"synchronization_requested"`;
- `ck_audit_event_action` lists 11 values;
- new `ck_audit_event_sync_request_shape` (db-design §2.3).

Migration `AddSynchronizationRequestAudit` (db-design §3).

## 4. Application types (not persisted; mapped from nothing in the database)

Named here so the API contract has a traceable owner; names indicative, as in
the spec.

| Type | Kind | Purpose | API mapping |
|---|---|---|---|
| `ISynchronizationRequests` | port, `Application/Ports` | `RequestAsync(CancellationToken)` → whether other work was in progress (spec FR-002) | — |
| `RequestSynchronizationUseCase` | use case, `Application/UseCases` | spec FR-001 | `POST /synchronization/requests` |
| `RequestSynchronizationOutcome` | result value | `Requested`, `RequestedAfterCurrentWork`, `Refused(ConnectionNotUsable state)` | → `SynchronizationMessageKey` |
| `SynchronizationMessageKey` | enum (DTO) | `Requested`, `RequestedAfterCurrentWork`, `ConnectionNotUsableAdmin`, `ConnectionNotUsableDean` | openapi `SynchronizationMessageKey` |

The adapter implementing `ISynchronizationRequests` over `SyncRunCoordinator`
lives in `ClassroomAgent.Web` (api-design §2.7). No `DbContext`, entity or Google
type crosses into these types (AD-3, AD-4, AD-8).

## 5. DTO additions to existing view models

| View model | Added field | Source |
|---|---|---|
| `WorkspaceConnectionPageModel` (US-009/US-017) | `CanRequestSynchronization` (always true), `SynchronizationMessageKey?` | TempData or the `409` re-render |
| `LandingPageModel` (US-008) | `CanRequestSynchronization` (Dean → true, Admin → false), `SynchronizationMessageKey?` | TempData or the `409` re-render |

Neither carries a `SyncState` field for the Dean (spec S-08).
