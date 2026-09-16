---
artifact_type: entity_model
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T13:53:54Z
updated_at: 2026-09-16T13:53:54Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
  - path: docs/designs/database/US-001-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 69
supersedes: null
---

# US-002 Entity Model — Control Plane: Installation

Schema: `docs/designs/database/US-002-db-design.md`.

Entities live in `ClassroomAgent.ControlPlane.Persistence`, configurations in
`…Persistence.Configurations`, services in `ClassroomAgent.ControlPlane.Services`
(AD-3). The Control Plane does not reference `ClassroomAgent.Domain`.

## 1. Business concepts → entities

| Business concept (§3 / glossary) | Entity | Table |
|---|---|---|
| **Installation** — a school registered by the Owner: identifier, name, Google Workspace domain, status, creation date, service-account client ID | `Installation` | `installation` |
| **Installation status** — active / suspended | `InstallationStatus` enum | `installation.status` |
| **AuditEvent** for installation actions | `AuditEvent` (US-001, extended) | `audit_event` |

## 2. Entities

### 2.1 `Installation`

| Property | C# type | Column | Business meaning | Mutable |
|---|---|---|---|---|
| `Id` | `long` | `id` | internal key; audit target id | no (generated) |
| `Identifier` | `Guid` | `identifier` | the installation id the Owner copies into configuration | no |
| `Name` | `string` | `name` | school name | yes — `Rename` |
| `Domain` | `string` | `domain` | Google Workspace domain, lower case | no |
| `ClientId` | `string` | `client_id` | service-account client ID (digits) | yes — `ChangeClientId` |
| `Status` | `InstallationStatus` | `status` (`'active'`, `'suspended'`) | active / suspended | not in this Story (US-004) |
| `CreatedAt` | `DateTimeOffset` | `created_at` | creation time, UTC (PC-6; shown to the Owner) | no |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | PC-6 | interceptor |

All properties are required (non-nullable). `private set` on every property; a
private parameterless constructor for EF Core.

**Construction and behaviour:**

| Member | Contract |
|---|---|
| `static Installation Register(string name, string domain, string clientId)` | `Identifier = Guid.NewGuid()`; `Domain = domain.ToLowerInvariant()`; `Status = Active`; name and client ID as given. Callers pass values already validated by the request rules (VR-001 … VR-003); the method does not re-validate user input (AD-9) |
| `void Rename(string name)` | sets `Name`; nothing else |
| `void ChangeClientId(string clientId)` | sets `ClientId`; nothing else |

No member changes `Identifier`, `Domain` or `Status`, and none deletes. Equality
of an unchanged name or client ID is decided by the service before calling these
methods (spec FR-006 step 4, FR-007 step 4), so an unchanged submission marks
nothing `Modified`.

### 2.2 `InstallationStatus`

Enum `Active`, `Suspended` in its own file. Stored through an explicit value
converter as `'active'` / `'suspended'` — never an integer, never the member name
(US-001 entity model §2.2). Adding a member needs a migration
(`ck_installation_status`).

### 2.3 `AuditEvent` — extensions

| Type | Added member | Code |
|---|---|---|
| `AuditAction` | `InstallationCreated` | `installation_created` |
| | `InstallationRenamed` | `installation_renamed` |
| | `InstallationClientIdChanged` | `installation_client_id_changed` |
| `AuditTargetType` | `Installation` | `installation` |

The converters in `AuditEventConfiguration` gain these mappings. No migration
(no check constraint on those columns).

New factories — the only way services create these rows; none accepts a string:

| Factory | Sets |
|---|---|
| `InstallationCreated(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId)` | actor owner/`ownerId`; target installation/`installationId`; `installation_created`; succeeded |
| `InstallationRenamed(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId)` | same shape; `installation_renamed` |
| `InstallationClientIdChanged(long ownerId, long installationId, DateTimeOffset occurredAt, string? requestId)` | same shape; `installation_client_id_changed` |

`requestId` is the same request identifier as in US-001 (the log correlation id);
it is not user input. A private helper generalising US-001's `OwnerActs` to take
a target type and id is the implementor's choice.

### 2.4 `TimestampInterceptor` — change

It stamps only the entity types it lists (`Owner`, `AuditEvent`). `Installation`
must be added, so `created_at` / `updated_at` are set on insert and `updated_at`
on rename and client ID change. `created_at` is never modified after insert.

### 2.5 `ControlPlaneDbContext` — change

Adds `DbSet<Installation> Installations` and applies
`InstallationConfiguration`.

## 3. Configuration (`InstallationConfiguration`)

- `ToTable("installation")` with check constraints `ck_installation_name_length`,
  `ck_installation_domain_lower`, `ck_installation_domain_format`,
  `ck_installation_client_id_format`, `ck_installation_status` (db-design §3.1).
- `HasKey(Id).HasName("pk_installation")`, `UseIdentityByDefaultColumn()`.
- `Identifier` — required; unique index `uq_installation_identifier`;
  `ValueGeneratedNever()`.
- `Name` — required, `HasMaxLength(200)`.
- `Domain` — required, `HasMaxLength(253)`, unique index `uq_installation_domain`.
- `ClientId` — required, `HasMaxLength(32)`, unique index
  `uq_installation_client_id`.
- `Status` — required, `HasMaxLength(16)`, value converter.
- `CreatedAt`, `UpdatedAt` — required.
- Triggers are created in the migration via `migrationBuilder.Sql` only, as for
  US-001's `audit_event` trigger; `BEFORE` row triggers are compatible with
  Npgsql's `RETURNING`, so the model needs no trigger declaration.

Unique-constraint names are the contract for conflict mapping: the services
compare `PostgresException.ConstraintName` with `uq_installation_domain` and
`uq_installation_client_id` (as US-001's `FirstRunSetupService` does for the
Owner indexes).

## 4. Services and result types

| Service method (`ControlPlane.Services`) | Result |
|---|---|
| `InstallationRegistry.ListAsync(ct)` | `IReadOnlyList<InstallationListItemDto>` ordered by name (ordinal ignore-case), then domain |
| `InstallationRegistry.GetAsync(Guid identifier, ct)` | `InstallationDetailDto?` (null → `404`) |
| `InstallationRegistry.RegisterAsync(name, domain, clientId, ownerId, requestId, ct)` | `RegisterInstallationResult`: `Registered(Guid identifier)` / `Conflict(domainTaken, clientIdTaken)` |
| `InstallationRegistry.RenameAsync(Guid identifier, name, ownerId, requestId, ct)` | `RenameInstallationResult`: `Renamed` / `Unchanged` / `NotFound` |
| `InstallationRegistry.ChangeClientIdAsync(Guid identifier, clientId, ownerId, requestId, ct)` | `ChangeClientIdResult`: `Changed` / `Unchanged` / `NotFound` / `ClientIdTaken` |

The service type name is the implementor's; the result cases are fixed by the
API design. Ordering: `OrderBy` on the materialised list with
`StringComparer.OrdinalIgnoreCase` (the database collation is not relied on);
at ~10 rows this is in memory by design (I-2). Validation outcomes are not
service results: requests reaching the service are already valid.

## 5. Mapping to API DTOs

No entity crosses into a controller, view or form model (AD-8).

| API element (openapi) | Direction | Entity / service | Fields |
|---|---|---|---|
| `RegisterInstallationRequest` (`name`, `domain`, `clientId`) | in | `RegisterAsync` → `Installation.Register` | `name` → `Name`; `domain` → `Domain` (lower-cased); `clientId` → `ClientId` |
| `RenameInstallationRequest` (`name`) | in | `RenameAsync` → `Rename` | `name` → `Name` |
| `ChangeInstallationClientIdRequest` (`clientId`) | in | `ChangeClientIdAsync` → `ChangeClientId` | `clientId` → `ClientId` |
| route `{id}` | in | lookup by `Identifier` | never `Id` |
| `InstallationListItem` | out | `InstallationListItemDto` | `Identifier`, `Name`, `Domain`, `Status`, `CreatedAt` |
| `InstallationDetail` | out | `InstallationDetailDto` | the same plus `ClientId` |

`Id` (the numeric key) and `UpdatedAt` never leave `ControlPlane.Services`. The
Owner id for the audit actor comes from the session principal (US-001
`OwnerSessionDto` / claims), not from the form.

## 6. Namespaces and files

| Type | Namespace | File |
|---|---|---|
| `Installation`, `InstallationStatus` | `ClassroomAgent.ControlPlane.Persistence` | `Persistence/<Type>.cs` |
| `InstallationConfiguration` | `…Persistence.Configurations` | one file |
| migration `AddInstallation` | `…Persistence.Migrations` | generated |
| `AuditAction`, `AuditTargetType`, `AuditEvent`, `AuditEventConfiguration`, `TimestampInterceptor`, `ControlPlaneDbContext` | existing | modified |
| registry service, result types, `InstallationListItemDto`, `InstallationDetailDto` | `ClassroomAgent.ControlPlane.Services` | one file each |
