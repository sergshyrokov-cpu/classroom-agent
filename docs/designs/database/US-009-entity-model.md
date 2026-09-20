---
artifact_type: entity_model
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T13:40:00Z
updated_at: 2026-09-20T13:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/designs/database/US-009-db-design.md
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
supersedes: null
---

# US-009 Entity Model — Configure WorkspaceConnection

## 1. Business concepts → entities

| Business concept (`trebovaniya.md`) | Entity / type | Where |
|---|---|---|
| `WorkspaceConnection` — the installation's connection to one Workspace domain (§3) | `WorkspaceConnection` | `Domain.Entities` |
| The school's technical account, the impersonation user (BR-015, §9) | `WorkspaceConnection.ImpersonationUserEmail` | `Domain.Entities` |
| The domain the Owner approved for this school (§9, BR-020) | read from `LegitimacyState.Domain` (US-005) | `Domain.Entities` |
| Saving or changing the connection, as an audited action (§5) | `AuditAction.WorkspaceConnectionSaved` | `Domain.Enums` |
| What that action acted upon | `AuditTargetType.WorkspaceConnection` | `Domain.Enums` |
| Why a save was refused | four new `AuditRefusalCategory` members | `Domain.Enums` |
| Whether the connection may be used at all (OD-002) | `WorkspaceConnectionState` | `Application.Models` |

## 2. Installation — `ClassroomAgent.Domain`

### 2.1 `WorkspaceConnection` (`Domain.Entities`)

| Property | Type | Null | Rule |
|---|---|---|---|
| `Id` | `long` | no | surrogate, generated on add (PC-3) |
| `Domain` | `string` | no | lower-cased, ≤253, VR-002 |
| `ImpersonationUserEmail` | `string` | no | lower-cased, ≤254, VR-001, domain part equals `Domain` |
| `CreatedAt` | `DateTimeOffset` | no | interceptor (PC-6) |
| `UpdatedAt` | `DateTimeOffset` | no | interceptor (PC-6) |

- Private setters and a private constructor; the entity is created through
  `Create(string domain, string impersonationUserEmail)` and changed through
  `ChangeTo(string domain, string impersonationUserEmail)`, the shape
  `AppUser` and `LegitimacyState` already use.
- Both factories **normalise** — trim, lower-case with the invariant culture,
  strip one trailing dot from the domain — and both **reject** a pair whose email
  domain differs from the domain (`ArgumentException`). The entity cannot be
  brought into an internally inconsistent state, which is the invariant
  `ck_workspace_connection_email_domain` also holds in the database.
- The entity does **not** know the `Installation` domain and therefore does not
  enforce BR-020: that comparison is against `LegitimacyState`, which is another
  aggregate, and it lives in the use case (spec FR-006, FR-007). The entity
  guards its own consistency, the use case guards the Owner's rule.
- No navigation property, no foreign key, nothing references it (PC-8).
- `Domain` (the property) and `ClassroomAgent.Domain` (the namespace) collide in
  name only; the implementation keeps the property because `trebovaniya.md` §3
  calls it the domain, and a qualified type name resolves the ambiguity where it
  arises.

### 2.2 Enum growth (`Domain.Enums`)

| Enum | New member | Code in the database |
|---|---|---|
| `AuditAction` | `WorkspaceConnectionSaved` | `workspace_connection_saved` |
| `AuditTargetType` | `WorkspaceConnection` | `workspace_connection` |
| `AuditRefusalCategory` | `DomainMismatch` | `domain_mismatch` |
| `AuditRefusalCategory` | `ImpersonationDomainMismatch` | `impersonation_domain_mismatch` |
| `AuditRefusalCategory` | `DomainNotConfirmed` | `domain_not_confirmed` |
| `AuditRefusalCategory` | `ReadOnlyMode` | `read_only_mode` |

`AuditTargetType`, deliberately empty since US-008, gets its first member here —
the case US-008 I-11 described. Its converter, which throws on any value while no
member existed, becomes reachable and is now covered by a test.

### 2.3 `AuditEvent` factories (`Domain.Entities`)

Two new factories, accepting no free string beyond the request id — the rule the
entity has carried since US-008:

- `WorkspaceConnectionSaved(long actorId, long connectionId, DateTimeOffset occurredAt, string? requestId)`
  → `succeeded`, target type and target id set;
- `WorkspaceConnectionSaveRefused(long actorId, AuditRefusalCategory category, DateTimeOffset occurredAt, string? requestId)`
  → `refused`, target type set, target id null, category required.

The second rejects a category that belongs to sign-in (the five US-008 members),
as `AdminSignInRefused` already rejects `CallbackFailed`: a category from the
wrong action is a programming error, not an input.

## 3. Installation — `ClassroomAgent.Infrastructure.Persistence`

### 3.1 `WorkspaceConnectionConfiguration` (`Persistence.Configurations`)

Maps the table of db-design §3: explicit lengths and nullability, the nine check
constraints, the shadow `Singleton` property with its default and its unique
index. No enum conversion is needed — the entity has no enum property.

### 3.2 `AuditEventConfiguration` (amended)

Three constraint texts change and one is added (db-design §4, §4.1). The
converters gain the new codes in both directions; an unknown code still throws,
so a row written by an older or newer version is never silently mapped to a
neighbouring member.

### 3.3 `ClassroomAgentDbContext`

Gains `DbSet<WorkspaceConnection>` and applies the new configuration. The
existing `TimestampInterceptor` covers the entity unchanged (PC-6).

### 3.4 Repository (`Persistence.Repositories`)

`WorkspaceConnectionRepository` implements the port of §4:

- `GetForReadAsync` — the single row, **untracked** (db-design §3.4, US-008
  security-review F-4);
- `GetForUpdateAsync` — the single row, tracked, for the change path;
- `Add` — stages a new row.

No method takes or returns a DTO, and none commits: the unit of work does (AD-7).

## 4. Installation — `ClassroomAgent.Application`

| Type | Namespace | Role |
|---|---|---|
| `IWorkspaceConnectionRepository` | `Application.Ports` | the port of §3.4 (AD-4) |
| `WorkspaceConnectionState` | `Application.Models` | the four-member state of spec FR-002 |
| `WorkspaceConnectionView` | `Application.Models.Dtos` | the DTO behind the page's view model |
| `SaveWorkspaceConnectionRequest` | `Application.Models.Requests` | the one-field form request, with its DataAnnotations |
| `WorkspaceDomainAttribute`, `ServiceAccountEmailAttribute` | `Application.Validation` | VR-001 and VR-002 as attributes |
| `DomainComparison` | `Application.UseCases` (internal static) | the single comparison rule of spec FR-007 |
| `GetWorkspaceConnectionQuery` | `Application.UseCases` | the read of spec FR-002 |
| `SaveWorkspaceConnectionUseCase` | `Application.UseCases` | the write of spec FR-006 |
| `SaveWorkspaceConnectionOutcome` | `Application.Models` | success or one of the three refusal reasons, as data (AD-9) |

`SaveWorkspaceConnectionUseCase` takes `IReadOnlyModeGuard` in its constructor,
so the US-007 structural rule is satisfied without any entry in
`PermittedServiceWrites`: saving a connection is not on the BR-026 closed list
and the list is not widened (spec FR-008). The audit row of a read-only refusal
is committed by declaring `PermittedServiceWrite.AuditEvent` around that one
commit (spec I-8).

`Application` still references no NuGet package and `Domain` keeps zero package
references (AD-3); nothing in this model changes that.

## 5. Mapping to API DTOs

| Entity / value | API schema (US-009 openapi) | Notes |
|---|---|---|
| `WorkspaceConnection.Domain` | `WorkspaceConnectionPageModel.savedDomain` | data, never translated |
| `WorkspaceConnection.ImpersonationUserEmail` | `WorkspaceConnectionPageModel.savedImpersonationUserEmail` | shown only to an Admin |
| `LegitimacyState.Domain` | `WorkspaceConnectionPageModel.installationDomain` | null while no check has succeeded |
| `WorkspaceConnectionState` | `WorkspaceConnectionPageModel.state` | four members, one-to-one |
| derived | `WorkspaceConnectionPageModel.connectionUsable` | true only for `Configured` |
| `SaveWorkspaceConnectionRequest.ImpersonationUserEmail` | `SaveWorkspaceConnectionForm.impersonationUserEmail` | the only field of the request |
| `SaveWorkspaceConnectionOutcome` | the status code and `messageKey` | success `302`; the three refusals `409` |
| `AuditEvent` | — | never leaves the database in this Story (no audit screen until EPIC-9) |

No entity appears in a controller signature, a request body, a response body or
a view model (AD-8). No schema carries a key, a secret or a reference to either
(SC-7, PC-9).

## 6. Control Plane

Unchanged. No entity, no configuration, no migration, no wire type: the
connection is the school's own data (spec S-11).
