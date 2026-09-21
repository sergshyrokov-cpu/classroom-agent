---
artifact_type: entity_model
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T08:02:19Z
updated_at: 2026-09-21T08:02:19Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/designs/database/US-011-db-design.md
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 1
supersedes: null
---

# US-011 Entity Model — Check access diagnostic

## 1. Business concepts → entities

| Business concept (`trebovaniya.md`) | Entity / type | Where |
|---|---|---|
| Running "Проверить доступ", as an audited action (§5) | `AuditAction.AccessCheckRun` | `Domain.Enums` |
| A check refused because there is no usable connection | `AuditRefusalCategory.ConnectionNotUsable` | `Domain.Enums` |
| A check refused in read-only mode (BR-026) | `AuditRefusalCategory.ReadOnlyMode` (existing, reused) | `Domain.Enums` |
| The technical account and domain checked (BR-015) | read from `WorkspaceConnection` (US-009) | `Domain.Entities` |
| The six scopes (§6) | `GoogleDelegationScopes.All` (US-010) | `Domain.Rules` |
| A check, its steps, its verdict | **no entity** — a transient `Application` value (OD-003) | `Application.Models` |

## 2. Installation — `ClassroomAgent.Domain`

### 2.1 Enum growth (`Domain.Enums`)

- `AuditAction` gains `AccessCheckRun`, documented with its source (spec FR-008,
  `trebovaniya.md` §5 "запуск «Проверить доступ»").
- `AuditRefusalCategory` gains `ConnectionNotUsable` (spec FR-008; api-design §2.4).
- `AuditTargetType` gains **nothing**: the target is the existing
  `WorkspaceConnection`.
- `AuditOutcome`, `AuditActorType` unchanged.

### 2.2 `AuditEvent` factories (`Domain.Entities`)

Two new factories, in the pattern of US-009 — no free string beyond the request id:

- `AccessCheckRun(long actorId, long connectionId, DateTimeOffset occurredAt, string? requestId)`
  → `succeeded`, target type `WorkspaceConnection`, target id set.
- `AccessCheckRefused(long actorId, AuditRefusalCategory category, long? connectionId, DateTimeOffset occurredAt, string? requestId)`
  → `refused`, target type `WorkspaceConnection`, target id as given, category
  required. It accepts **only** `ReadOnlyMode` and `ConnectionNotUsable`; any other
  category is a programming error and throws, as `WorkspaceConnectionSaveRefused`
  rejects the sign-in categories.

The factories accept no step outcome, verdict, scope, address or domain — there is
nowhere to put one (db-design §3.2).

## 3. Installation — `ClassroomAgent.Infrastructure.Persistence`

### 3.1 `AuditEventConfiguration` (amended)

- the two check constraints of db-design §3.1;
- `ActionCode`/`ActionFromCode` map `AccessCheckRun` ↔ `access_check_run`;
- `RefusalCategoryCode`/`RefusalCategoryFromCode` map `ConnectionNotUsable` ↔
  `connection_not_usable`.

No other configuration, no new `DbSet`, no new repository. The existing
`IAuditEventRepository` writes the rows.

### 3.2 Migration

`AddAccessCheckAudit` (db-design §7.1), with the model snapshot updated.

## 4. Installation — `ClassroomAgent.Application` (transient, not persisted)

The check's result is an `Application` model, built by the use case from the
port's step outcomes and never mapped to an entity (AD-8, OD-003):

| Type | Shape | Source |
|---|---|---|
| `AccessCheckStepOutcome` | enum, the nine members of spec FR-005 | `Application` (not `Domain`: it describes an external system's answer, not a domain invariant) |
| `AccessCheckStepKind` | `Delegation`, `ClassroomRead`, `ReportsRead` | spec FR-001 |
| `AccessCheckVerdict` | `AccessInPlace`, `NotConfigured`, `Inconclusive` | spec FR-001 |
| `AccessCheckStep` | kind, scope (nullable), outcome, not-attempted-because (nullable) | spec FR-001…FR-005 |
| `AccessCheckResult` | verdict, eight steps | spec FR-001 |

Names are indicative; IMPLEMENTATION may refine them within `package-map.md`.

## 5. Mapping to API DTOs

`AccessCheckPageModel` (openapi) ← built in `Application`/presentation from:

| View-model field | Source |
|---|---|
| `connectionState` | US-009 `GetWorkspaceConnectionQuery` → `workspace_connection` + `legitimacy_state` |
| `technicalAccount` | `workspace_connection.impersonation_user_email` via the same query |
| `domain` | `legitimacy_state.domain` via the same query (known only after a successful check, US-009 FR-003) |
| `readOnly`, `readOnlyReasonKey` | the read-only mode query (US-007) → `legitimacy_state` |
| `messageKey` | the use case's refusal outcome → a translation key in presentation |
| `result` | `AccessCheckResult` (§4) — never read from the database |

`legitimacy_state.domain` is rendered as data and HTML-encoded (spec VR-006); it has
only a length constraint (carried US-010 finding F-2), so no code may assume a
character set for it — including in a log line, which this Story never writes it
into (spec FR-014).

## 6. Control Plane

Nothing.
