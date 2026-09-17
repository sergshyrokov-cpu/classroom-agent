---
artifact_type: entity_model
story: US-003
version: 1
status: DRAFT
created_at: 2026-09-17T09:45:00Z
updated_at: 2026-09-17T09:45:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-003-spec.md
    version: 1
  - path: docs/designs/api/US-003-api-design.md
    version: 1
  - path: docs/designs/api/US-003-openapi.yaml
    version: 1
  - path: docs/decisions/US-003-open-decisions.md
    version: 1
  - path: docs/designs/database/US-002-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 70
supersedes: null
---

# US-003 Entity Model — Control Plane: AllowedAdmin

Schema: `docs/designs/database/US-003-db-design.md`.

Entities live in `ClassroomAgent.ControlPlane.Persistence`, configurations in
`…Persistence.Configurations`, services in `ClassroomAgent.ControlPlane.Services`
(AD-3). The Control Plane does not reference `ClassroomAgent.Domain`.

## 1. Business concepts → entities

| Business concept (§3 / glossary) | Entity | Table |
|---|---|---|
| **AllowedAdmin** — an email permitted to be Admin of one `Installation`, who added it and when | `AllowedAdmin` | `allowed_admin` |
| **Installation** (US-002) — owner of the entries | `Installation` (unchanged) | `installation` |
| **AuditEvent** for add / revoke | `AuditEvent` (extended) | `audit_event` |

## 2. Entities

### 2.1 `AllowedAdmin`

| Property | C# type | Column | Business meaning | Mutable |
|---|---|---|---|---|
| `Id` | `long` | `id` | internal key; audit target id | no (generated) |
| `Identifier` | `Guid` | `identifier` | route id `{adminId}` | no |
| `InstallationId` | `long` | `installation_id` | the installation the entry belongs to | no |
| `Email` | `string` | `email` | the Admin's Google Workspace email, lower case | no |
| `AddedByOwnerId` | `long` | `added_by_owner_id` | "кем добавлен" | no |
| `CreatedAt` | `DateTimeOffset` | `created_at` | "когда добавлен", UTC (PC-6) | no |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | PC-6; equals `CreatedAt` | no |

All required. `private set` on every property; private parameterless constructor
for EF Core. **No navigation properties** — services query by `InstallationId`;
the relationships are declared in configuration without navigations (PC-8
cardinality still explicit via `HasOne<Installation>().WithMany()` /
`HasOne<Owner>().WithMany()`).

**Construction:**

| Member | Contract |
|---|---|
| `static AllowedAdmin Add(long installationId, string email, long addedByOwnerId)` | `Identifier = Guid.NewGuid()`; `Email = email.ToLowerInvariant()`; other values as given. Callers pass an email already validated (VR-001) and domain-matched (VR-002); the method does not re-validate (AD-9) |

No other member. Nothing updates an entry; revocation deletes it (db-design §5).

### 2.2 `AuditEvent` — extensions

| Type | Added member | Code |
|---|---|---|
| `AuditAction` | `AllowedAdminAdded` | `allowed_admin_added` |
| | `AllowedAdminRevoked` | `allowed_admin_revoked` |
| `AuditTargetType` | `AllowedAdmin` | `allowed_admin` |

The converters in `AuditEventConfiguration` gain these mappings (update the XML
doc comment of `AuditTargetType`). No migration.

New factories — none accepts a string:

| Factory | Sets |
|---|---|
| `AllowedAdminAdded(long ownerId, long allowedAdminId, DateTimeOffset occurredAt, string? requestId)` | actor owner/`ownerId`; target allowed_admin/`allowedAdminId`; `allowed_admin_added`; succeeded |
| `AllowedAdminRevoked(long ownerId, long allowedAdminId, DateTimeOffset occurredAt, string? requestId)` | same shape; `allowed_admin_revoked` |

They reuse the existing private `OwnerActsOn` helper.

### 2.3 `TimestampInterceptor` — change

Add `AllowedAdmin` to the stamped entity types, so `created_at` and `updated_at`
are set on insert. An `AllowedAdmin` is never `Modified`.

### 2.4 `ControlPlaneDbContext` — change

Adds `DbSet<AllowedAdmin> AllowedAdmins` and applies `AllowedAdminConfiguration`.

## 3. Configuration (`AllowedAdminConfiguration`)

- `ToTable("allowed_admin")` with check constraints `ck_allowed_admin_email_lower`,
  `ck_allowed_admin_email_format` (db-design §3.1).
- `HasKey(Id).HasName("pk_allowed_admin")`, `UseIdentityByDefaultColumn()`.
- `Identifier` — required; unique index `uq_allowed_admin_identifier`;
  `ValueGeneratedNever()`.
- `InstallationId` — required;
  `HasOne<Installation>().WithMany().HasForeignKey(InstallationId)
  .HasConstraintName("fk_allowed_admin_installation")
  .OnDelete(DeleteBehavior.Restrict)`.
- `Email` — required, `HasMaxLength(254)`; unique index on
  (`InstallationId`, `Email`) named `uq_allowed_admin_installation_email`.
- `AddedByOwnerId` — required;
  `HasOne<Owner>().WithMany().HasForeignKey(AddedByOwnerId)
  .HasConstraintName("fk_allowed_admin_added_by_owner")
  .OnDelete(DeleteBehavior.Restrict)`; index `ix_allowed_admin_added_by_owner_id`.
- `CreatedAt`, `UpdatedAt` — required.
- Triggers only in the migration via `migrationBuilder.Sql` (as US-001/US-002).

The constraint name `uq_allowed_admin_installation_email` is the contract for
conflict mapping (`PostgresException.ConstraintName`).

## 4. Services and result types

In `ControlPlane.Services` (the service type name is the implementor's; an
`AllowedAdminRegistry` beside `InstallationRegistry` is the natural shape):

| Service method | Result |
|---|---|
| `InstallationRegistry.GetAsync(Guid identifier, ct)` (changed) | `InstallationDetailDto?` now including `Admins` (entries of that installation ordered by `Email`, ordinal) |
| `GetAddFormAsync(Guid installationIdentifier, ct)` | `AddAllowedAdminFormDto?` (null → `404`) |
| `AddAsync(Guid installationIdentifier, string email, long ownerId, string? requestId, ct)` | `AddAllowedAdminResult`: `Added` / `NotFound` / `WrongDomain(string expectedDomain)` / `Taken` |
| `GetRevokeConfirmationAsync(Guid installationIdentifier, Guid adminIdentifier, ct)` | `RevokeAllowedAdminConfirmationDto?` (null → `404`) |
| `RevokeAsync(Guid installationIdentifier, Guid adminIdentifier, long ownerId, string? requestId, ct)` | `RevokeAllowedAdminResult`: `Revoked` / `NotFound` |

- `AddAsync` receives an email that passed VR-001 binding validation; it
  lower-cases it, compares the domain part (text after the single `@`) ordinally
  with `installation.Domain`, pre-checks the unique pair, then runs the add
  transaction (db-design §5).
- Status is not consulted by either method (AC-006).
- Expected outcomes are result cases, not exceptions (AD-9).

## 5. Mapping to API DTOs

| DTO (API design §6) | Source |
|---|---|
| `InstallationDetailDto.Admins` | `AllowedAdmin` rows with `InstallationId = installation.Id`, ordered by `Email` |
| `AllowedAdminItemDto` | `Identifier`, `Email`, `AddedAt ← CreatedAt` |
| `AddAllowedAdminFormDto` | `Installation.Identifier`, `.Name`, `.Domain` |
| `RevokeAllowedAdminConfirmationDto` | `Installation.Identifier`, `.Name`; `AllowedAdmin.Identifier`, `.Email`; `LeavesFewerThanTwo ← count(entries of installation) ≤ 2` |

`Id`, `InstallationId` and `AddedByOwnerId` never reach a DTO (AD-8).
