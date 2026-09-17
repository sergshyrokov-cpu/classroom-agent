---
artifact_type: entity_model
story: US-005
version: 1
status: DRAFT
created_at: 2026-09-17T13:55:10Z
updated_at: 2026-09-17T13:55:10Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-005-spec.md
    version: 1
  - path: docs/designs/api/US-005-api-design.md
    version: 1
  - path: docs/designs/api/US-005-openapi.yaml
    version: 1
  - path: docs/decisions/US-005-open-decisions.md
    version: 1
  - path: docs/designs/database/US-003-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 73
supersedes: null
---

# US-005 Entity Model — InstanceLicenseCheck and LegitimacyState

Schema: `docs/designs/database/US-005-db-design.md`.

Two independent models (AD-1). The Control Plane entity lives in
`ClassroomAgent.ControlPlane.Persistence` and never references `Domain`. The
installation entity lives in `ClassroomAgent.Domain.Entities`, mapped by
`ClassroomAgent.Infrastructure.Persistence`. They share no type; the only shared
vocabulary is the wire strings of `ClassroomAgent.Contracts`.

## 1. Business concepts → entities

| Business concept (§3 / glossary) | Host | Entity | Table |
|---|---|---|---|
| **InstanceLicenseCheck** — the Control Plane's record of the last check of one `Installation` | Control Plane | `InstanceLicenseCheck` | `instance_license_check` |
| **Installation** (US-002) — owner of the record | Control Plane | `Installation` (unchanged) | `installation` |
| **LegitimacyState** — the installation's record of its last successful check, last known status, compatibility, domain and client ID | installation | `LegitimacyState` | `legitimacy_state` |
| **Compatibility state** (DC-12) | both | enum `CompatibilityState` — one per host | — |
| **Read-only mode and its reason** (BR-025) | installation | computed, not stored — `LegitimacyMode` (Application) | — |

## 2. Control Plane

### 2.1 `InstanceLicenseCheck` (`ControlPlane.Persistence`)

| Property | C# type | Column | Business meaning | Mutable |
|---|---|---|---|---|
| `Id` | `long` | `id` | internal key | no (generated) |
| `InstallationId` | `long` | `installation_id` | the installation checked | no |
| `AnsweredAt` | `DateTimeOffset` | `answered_at` | when the Control Plane answered (UTC) | via `Replace` |
| `ApplicationVersion` | `string` | `application_version` | version reported | via `Replace` |
| `ContractVersion` | `int` | `contract_version` | contract version reported | via `Replace` |
| `AnsweredStatus` | `InstallationStatus` | `answered_status` | status answered | via `Replace` |
| `AnsweredCompatibility` | `CompatibilityState` | `answered_compatibility` | compatibility answered | via `Replace` |
| `CreatedAt` | `DateTimeOffset` | `created_at` | PC-6 | no |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | PC-6 | interceptor |

All required. `private set`; private parameterless constructor for EF Core. No
navigation property; the relationship is declared in configuration.

| Member | Contract |
|---|---|
| `static InstanceLicenseCheck Record(long installationId, DateTimeOffset answeredAt, string applicationVersion, int contractVersion, InstallationStatus status, CompatibilityState compatibility)` | new row with the values; inputs already validated (VR-002) |
| `void Replace(DateTimeOffset answeredAt, string applicationVersion, int contractVersion, InstallationStatus status, CompatibilityState compatibility)` | sets the five values; `InstallationId` unchanged |

### 2.2 `CompatibilityState` (`ControlPlane.Persistence`)

`Supported` ↔ `supported`, `UpgradeRecommended` ↔ `upgrade_recommended`,
`UpgradeRequired` ↔ `upgrade_required`. Stored by a value converter as these
strings, like `InstallationStatus`. `InstallationStatus` (existing) is reused for
`AnsweredStatus`.

### 2.3 `InstanceLicenseCheckConfiguration`

- `ToTable("instance_license_check")` with check constraints
  `ck_instance_license_check_application_version`, `…_contract_version`,
  `…_answered_status`, `…_answered_compatibility` (db-design §3.1).
- `HasKey(Id).HasName("pk_instance_license_check")`, `UseIdentityByDefaultColumn()`.
- `InstallationId` — required; unique index
  `uq_instance_license_check_installation_id`;
  `HasOne<Installation>().WithOne().HasForeignKey<InstanceLicenseCheck>(InstallationId)
  .HasConstraintName("fk_instance_license_check_installation")
  .OnDelete(DeleteBehavior.Restrict)`.
- `ApplicationVersion` — required, `HasMaxLength(20)`.
- `ContractVersion` — required.
- `AnsweredStatus` — required, `HasMaxLength(16)`, string converter.
- `AnsweredCompatibility` — required, `HasMaxLength(24)`, string converter.
- `AnsweredAt`, `CreatedAt`, `UpdatedAt` — required.

### 2.4 Changes to existing types

- `ControlPlaneDbContext` — `DbSet<InstanceLicenseCheck> InstanceLicenseChecks`;
  applies the configuration.
- `TimestampInterceptor` — adds `InstanceLicenseCheck` to the stamped types.
- `AuditAction`, `AuditTargetType`, `AuditEvent` — **unchanged** (no audit, S-14).

### 2.5 Services (`ControlPlane.Services`)

| Type / method | Result |
|---|---|
| `LegitimacyCheckService.CheckAsync(Guid installationIdentifier, string applicationVersion, int contractVersion, CancellationToken ct)` | `LegitimacyCheckResult`: `Known(InstallationStatus status, CompatibilityState compatibility, string domain, string clientId)` / `UnknownInstallation` |
| `CompatibilityPolicy.Decide(string applicationVersion, int contractVersion)` | `CompatibilityState` (spec FR-005); reads the validated `Compatibility:*` options; supported contract versions `{1}` |
| `InstallationRegistry.GetAsync(Guid identifier, ct)` (changed) | `InstallationDetailDto?` now including `LastCheck` |

- `CheckAsync` loads the installation by `identifier`; none → `UnknownInstallation`
  (no write). Otherwise decides compatibility, replaces the record (db-design
  §3.2), returns `Known` with the installation's `Status`, `Domain`, `ClientId`.
- Versions are compared numerically by component; `CompatibilityPolicy` takes the
  version already validated by binding (VR-002).
- The controller maps `Known` → `200 LegitimacyCheckResponse`, `UnknownInstallation`
  → `404 {"outcome":"unknown_installation"}`; the enum-to-wire string mapping lives
  in the controller layer, the only place `Contracts` types meet the service.

## 3. Installation

### 3.1 `LegitimacyState` (`Domain.Entities`)

| Property | C# type | Column | Business meaning | Mutable |
|---|---|---|---|---|
| `Id` | `long` | `id` | internal key | no (generated) |
| `LastSuccessfulCheckAt` | `DateTimeOffset?` | `last_successful_check_at` | "время последней успешной проверки"; null = never confirmed | via `RecordSuccess` |
| `Status` | `InstallationStatus` | `status` | "последний известный статус" | via both record methods |
| `Compatibility` | `CompatibilityState` | `compatibility` | "последнее состояние совместимости" | via both |
| `Domain` | `string` | `domain` | `Installation` domain from the last answer | via both |
| `ClientId` | `string` | `client_id` | `Installation` client ID from the last answer | via both |
| `CreatedAt` | `DateTimeOffset` | `created_at` | PC-6 | no |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | PC-6 | interceptor |

`singleton` is a shadow property in configuration (default `true`), not part of
the domain type. `private set`; private parameterless constructor. No EF Core
attribute or dependency in `Domain`.

| Member | Contract |
|---|---|
| `static LegitimacyState FromSuccess(DateTimeOffset checkedAt, InstallationStatus status, CompatibilityState compatibility, string domain, string clientId)` | new row, `LastSuccessfulCheckAt = checkedAt` |
| `static LegitimacyState FromUpgradeRequired(InstallationStatus status, string domain, string clientId)` | new row, `LastSuccessfulCheckAt = null`, `Compatibility = UpgradeRequired` |
| `void RecordSuccess(DateTimeOffset checkedAt, InstallationStatus status, CompatibilityState compatibility, string domain, string clientId)` | sets all five |
| `void RecordUpgradeRequired(InstallationStatus status, string domain, string clientId)` | sets status, domain, client ID; `Compatibility = UpgradeRequired`; `LastSuccessfulCheckAt` untouched |

`RecordSuccess` / `FromSuccess` never receive `UpgradeRequired` (the use case
routes it to the other members); passing it is a programming error
(`ArgumentException`), not an expected outcome.

### 3.2 Enums (`Domain.Enums`)

- `InstallationStatus` — `Active`, `Suspended` (stored `active` / `suspended`).
- `CompatibilityState` — `Supported`, `UpgradeRecommended`, `UpgradeRequired`
  (stored as the wire strings).

Distinct from the Control Plane's types of the same names (no shared project).

### 3.3 `LegitimacyStateConfiguration` (`Infrastructure.Persistence.Configurations`)

- `ToTable("legitimacy_state")` with `ck_legitimacy_state_singleton`,
  `…_status`, `…_compatibility`, `…_domain_length`, `…_client_id_format`.
- `HasKey(Id).HasName("pk_legitimacy_state")`, `UseIdentityByDefaultColumn()`.
- Shadow `bool` property `Singleton` → column `singleton`, required,
  `HasDefaultValue(true)`; unique index `uq_legitimacy_state_singleton`.
- `LastSuccessfulCheckAt` — optional.
- `Status` — required, `HasMaxLength(16)`, string converter.
- `Compatibility` — required, `HasMaxLength(24)`, string converter.
- `Domain` — required, `HasMaxLength(253)`.
- `ClientId` — required, `HasMaxLength(32)`.
- `CreatedAt`, `UpdatedAt` — required.

### 3.4 Persistence types (`Infrastructure.Persistence`)

- `ClassroomAgentDbContext` — `DbSet<LegitimacyState> LegitimacyStates`;
  `UseSnakeCaseNamingConvention()` (PC-5); interceptor registered.
- `TimestampInterceptor` — stamps `LegitimacyState` (and later Stories' entities);
  `TimeProvider` injected.
- `DesignTimeClassroomAgentDbContextFactory` — design-time only (db-design §6.2).
- `Repositories.LegitimacyStateRepository` implementing an `Application` port
  (name the implementor's, e.g. `ILegitimacyStateRepository`):
  `GetAsync(ct)` tracked, `GetForReadAsync(ct)` untracked, `Add(LegitimacyState)`;
  no save. The use case owns `SaveChangesAsync` through a unit-of-work port.

### 3.5 Application

| Type | Role |
|---|---|
| `IControlPlaneClient` (Ports) | `CheckAsync(Guid installationId, string applicationVersion, int contractVersion, ct)` → `ControlPlaneCheckReply` (API design §11); no `Contracts` or HTTP type in its signature |
| `ControlPlaneCheckReply` (Models) | `Answer(InstallationStatus, CompatibilityState, string domain, string clientId)` / `Failure(CheckFailureCategory)` |
| `CheckFailureCategory` (Models) | `Unreachable`, `Timeout`, `ErrorAnswer`, `UnparseableAnswer`, `UnknownInstallation` |
| `CheckLegitimacyUseCase` (UseCases) | calls the client, records per db-design §4.2, returns `CheckOutcome` = `Succeeded` / `Failed(category)` where category additionally includes `UpgradeRequired` and `SaveFailed` |
| `GetLegitimacyModeQuery` (UseCases) | reads the row + clock → `LegitimacyMode(IsReadOnly, Reason?, LastSuccessfulCheckAt?)`, order of spec FR-008, strict `> 7 days` |
| `LegitimacyModeReason` (Models) | `NotYetConfirmed`, `SuspendedByOwner`, `GracePeriodExpired` |

The 7-day rule is a pure function over (`LegitimacyState?`, now) and may live in
`Domain.Rules`; the query is its only caller in this Story.

## 4. Mapping to API DTOs and wire types

| DTO / wire type | Source |
|---|---|
| `LegitimacyCheckResponse.status` | `Installation.Status` → `active` / `suspended` |
| `LegitimacyCheckResponse.compatibility` | `CompatibilityPolicy.Decide` → wire string |
| `LegitimacyCheckResponse.domain`, `.clientId` | `Installation.Domain`, `.ClientId` |
| `InstallationDetailDto.LastCheck` | `InstanceLicenseCheck` of the installation: `AnsweredAt`, `ApplicationVersion`, `ContractVersion`, `AnsweredStatus`, `AnsweredCompatibility`; null if none |
| `ControlPlaneCheckReply.Answer` (installation) | `LegitimacyCheckResponse` parsed and validated (VR-003) by `Infrastructure/ControlPlane` |
| readiness body | `LegitimacyMode.IsReadOnly`, the in-memory last check outcome, database reachability |

`InstanceLicenseCheck.Id`, `InstallationId` and `LegitimacyState.Id` never reach
a DTO (AD-8). `LegitimacyState.Domain` and `ClientId` reach no DTO in this Story.
