---
artifact_type: entity_model
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T10:59:26Z
updated_at: 2026-09-17T10:59:26Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/designs/api/US-004-api-design.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
  - path: docs/designs/database/US-003-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 71
supersedes: null
---

# US-004 Entity Model — Control Plane: Installation status change

Companion: `US-004-db-design.md`. All types live in `ClassroomAgent.ControlPlane`
(AD-3): entities in `Persistence`, use cases and DTOs in `Services`.

## 1. Business concepts → entities

| Business concept (`trebovaniya.md` v71) | Entity / member | Change |
|---|---|---|
| School status "active / suspended" (§3) | `Installation.Status` (`InstallationStatus`) | written by this Story; type unchanged |
| Suspending / resuming a school (§9) | no entity — a status transition | new use case (§4) |
| Who and when changed the status (§3, §5) | `AuditEvent` with `AuditAction.InstallationSuspended` / `InstallationResumed` | two enum members |
| Reason, status-change time | — | not modelled (v71) |

## 2. Entities

### 2.1 `Installation` — unchanged shape

No property is added or removed. `Status` keeps its private setter.

- The status change is performed by the set-based update of db-design §5.1, not
  by a tracked-entity method; therefore **no `Suspend()` / `Resume()` method is
  added** to the entity. The transition rule (which status is expected for which
  target) lives in the service (§4).
- `Rename` and `ChangeClientId` stay as they are.

### 2.2 `InstallationStatus` — unchanged

`Active` ↔ `active`, `Suspended` ↔ `suspended`.

### 2.3 `AuditAction` — extension

| Member | Code |
|---|---|
| `InstallationSuspended` | `installation_suspended` |
| `InstallationResumed` | `installation_resumed` |

Added to the enum and to both switch expressions of
`AuditEventConfiguration` (`ActionCode`, `ActionFromCode`). No other audit enum
changes: `AuditTargetType.Installation` (`installation`), `AuditActorType.Owner`,
`AuditOutcome.Succeeded` exist.

### 2.4 `TimestampInterceptor` — unchanged

It already covers `Installation` for tracked updates. The set-based update sets
`UpdatedAt` itself (db-design §5.1) using the same injected `TimeProvider`, so
tests with a fake clock see a deterministic value.

### 2.5 `ControlPlaneDbContext` — unchanged

No new `DbSet`, no configuration change beyond §2.3, no migration.

## 3. Configuration

`InstallationConfiguration` — unchanged. `AuditEventConfiguration` — only the two
code mappings of §2.3.

## 4. Services and result types

In `ControlPlane.Services` (names indicative; the implementor may align them with
`InstallationRegistry` of US-002):

| Type | Role |
|---|---|
| `InstallationStatusTransition` | enum `Suspend`, `Resume` — which endpoint was called |
| `InstallationStatusChangeResult` | enum `NotFound`, `Changed`, `Unchanged` (AD-9) |
| `InstallationStatusConfirmationDto` | record `Identifier` (Guid), `Name`, `Domain`, `Status` |
| `InstallationRegistry` (existing) or a new `InstallationStatusService` | `GetStatusConfirmationAsync(Guid identifier, CancellationToken)` → `InstallationStatusConfirmationDto?`; `ChangeStatusAsync(Guid identifier, InstallationStatusTransition transition, long ownerId, string? requestId, CancellationToken)` → `InstallationStatusChangeResult` |

Transition table used by `ChangeStatusAsync`:

| Transition | Expected stored status | Target status | Audit action |
|---|---|---|---|
| `Suspend` | `Active` | `Suspended` | `InstallationSuspended` |
| `Resume` | `Suspended` | `Active` | `InstallationResumed` |

`ChangeStatusAsync` implements db-design §5.1 exactly: explicit transaction,
`ExecuteUpdateAsync` filtered on `Id` and expected `Status` setting `Status` and
`UpdatedAt`, audit row added and saved only when one row was affected, commit.

## 5. Mapping to API DTOs

| API schema (`US-004-openapi.yaml`) | Source |
|---|---|
| `InstallationStatusConfirmation` (`identifier`, `name`, `domain`, `status`) | `InstallationStatusConfirmationDto` ← `Installation.Identifier`, `Name`, `Domain`, `Status` |
| `InstallationDetailPage.installation` | existing `InstallationDetailDto` (US-002/US-003), unchanged; `Status` decides suspend vs resume link |
| `InstallationDetailPage.notice` | page model only, from the `notice` query value validated against `InstallationDetailDto.Status`; not persisted |
| `StatusChangeResult` (`NotFound`, `Changed`, `Unchanged`) | `InstallationStatusChangeResult` |
| `EmptyStatusChangeRequest` | no bound type; nothing from the request reaches persistence |

Never exposed: `Installation.Id` (internal key), `UpdatedAt`, audit rows.
