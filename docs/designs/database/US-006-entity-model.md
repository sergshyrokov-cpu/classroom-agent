---
artifact_type: entity_model
story: US-006
version: 1
status: DRAFT
created_at: 2026-09-17T15:44:28Z
updated_at: 2026-09-17T15:44:28Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: docs/designs/database/US-005-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-006 Entity Model — Control Plane: push address

Companion: `US-006-db-design.md`. Control Plane types live in
`ClassroomAgent.ControlPlane` (AD-3): entities in `Persistence`, services and DTOs
in `Services`, the push sender in `Push` (`package-map.md`). Installation types
follow US-005: `Domain`, `Application`, `Infrastructure`, `Web`.

## 1. Business concepts → entities

| Business concept (`trebovaniya.md` v76, v77) | Entity / member | Change |
|---|---|---|
| Push address of a school (§3) | `Installation.PushAddress` (`string?`) | new property + column |
| Who and when changed it (§5) | `AuditEvent` with `AuditAction.InstallationPushAddressChanged` | new enum member |
| Push о смене статуса (§9) | no entity — an in-memory job | `Push` namespace types (§4) |
| Отложенная проверка (§9 v77) | no entity — in-process state | `PushCheckCoordinator` (§5) |
| Проверка легитимности, `LegitimacyState` (§9) | unchanged from US-005 | — |

Nothing about push delivery is persisted (db-design §1).

## 2. Control Plane entities

### 2.1 `Installation` — one new property

| Member | Type | Rules |
|---|---|---|
| `PushAddress` | `string?` | canonical `http://host:port` (api-design §7) or `null`; ≤ 255 chars; private setter |

- Factory `Register(name, domain, clientId, pushAddress)` — the existing factory
  gains the nullable parameter; every other rule unchanged (status `Active`,
  domain lower-cased, identifier generated).
- New method `ChangePushAddress(string? pushAddress)` — sets the property, like
  `Rename` and `ChangeClientId`. No validation inside the entity: the value is
  already validated and canonicalized by the request rules (the US-002 pattern).
- No other property changes; `Identifier` and `Domain` stay immutable in the
  database too (db-design §3.2).

### 2.2 `AuditAction` — one new member

| Member | Code |
|---|---|
| `InstallationPushAddressChanged` | `installation_push_address_changed` |

Added to the enum and to both switch expressions of `AuditEventConfiguration`
(`ActionCode`, `ActionFromCode`), plus a factory
`AuditEvent.InstallationPushAddressChanged(ownerId, installationId, now, requestId)`
mirroring `InstallationRenamed`. `AuditTargetType.Installation`,
`AuditActorType.Owner` and `AuditOutcome.Succeeded` already exist.

### 2.3 `InstallationConfiguration` — mapping

```
builder.Property(i => i.PushAddress).HasMaxLength(255);   // nullable: no IsRequired()
table.HasCheckConstraint("ck_installation_push_address_format", "...");  // db-design §3.1
```

No index. Everything else in the configuration is unchanged.

### 2.4 `TimestampInterceptor`, `ControlPlaneDbContext` — unchanged

No new `DbSet`. The address change is a tracked update, so `updated_at` is stamped
by the interceptor (PC-6).

## 3. Control Plane services and result types

| Type | Role |
|---|---|
| `ChangePushAddressResult` | enum `NotFound`, `Unchanged`, `Changed` (AD-9) |
| `InstallationRegistry.ChangePushAddressAsync(Guid identifier, string? canonicalAddress, long ownerId, string? requestId, CancellationToken)` | db-design §4.2 |
| `InstallationRegistry.RegisterAsync(…)` | gains the canonical address parameter (nullable) |
| `InstallationDetailDto` | gains `PushAddress` (`string?`) |
| `InstallationStatusService.ChangeStatusAsync(…)` | unchanged signature and result; on `Changed` reads `push_address` in the transaction (db-design §4.3) and hands the push off after commit |

Canonicalization (trim, lower-case host, drop a trailing `/`) belongs to the
request-model validation in `Controllers` (api-design §7), so the service and the
entity receive a value already in canonical form or `null`.

## 4. Control Plane push types (`ControlPlane.Push`)

In-memory only; no entity, no table.

| Type | Role |
|---|---|
| `StatusPushDispatcher` (singleton) | `Enqueue(long installationId, Guid identifier, string address)`; keeps at most one in-flight push per installation and cancels the previous one (spec FR-007) |
| `StatusPushSender` (hosted service or worker of the dispatcher) | attempts and pauses: 10 s timeout, 5 s / 30 s / 120 s, max 4 attempts; logs 5011 … 5017 |
| `IStatusPushClient` + `StatusPushClient` | one HTTP attempt; returns `StatusPushAttemptResult` (`Delivered`, `Refused`, `UnexpectedStatus`, `ConnectionFailed`, `Timeout`); no redirects, no cookies, body never read |
| `StatusPushRequest` (`ClassroomAgent.Contracts`) | wire payload: `installationId` only |

The client is the substitution seam for Control Plane tests (TC-4 style); the real
client is tested against a local test HTTP server. Time comes from the injected
`TimeProvider`.

## 5. Installation types

No entity or configuration change; `LegitimacyState`, its repository and the
US-005 use cases stay as they are.

| Type | Role |
|---|---|
| `PushCheckCoordinator` (singleton, `Web.BackgroundServices`) | `Request()` → `PushCheckRequestResult` (`Started`, `Deferred`); holds "a check is running", "start of the last push-triggered check" and the single pending flag (api-design §5) |
| `LegitimacyCheckBackgroundService` | gains the push trigger: runs a check on demand, starts the pending check when allowed, resets the schedule after a push-triggered check; logs 5111 … 5114 |
| `StatusPushEndpoint` (`Web.Security` private route group) | validates the body and the installation id, calls the coordinator, answers `202` / `400` / `404` / `413` |
| `InstallationSettings` / `InstallationSettingsReader` | gain `PrivateAddress` (`Hosting:PrivateAddress`, IP literal or `*`) used for the private Kestrel endpoint |

All state in §5 is process state: nothing is persisted, and a restart runs the
startup check anyway (US-005 FR-003).

## 6. Mapping to API DTOs

| API schema (`US-006-openapi.yaml`) | Source |
|---|---|
| `RegisterInstallationRequest.pushAddress` | bound, validated, canonicalized → `InstallationRegistry.RegisterAsync` → `Installation.PushAddress` |
| `ChangeInstallationPushAddressRequest.pushAddress` | bound, validated, canonicalized (or `null`) → `ChangePushAddressAsync` |
| `InstallationDetailPushAddress.pushAddress` | `InstallationDetailDto.PushAddress` ← `Installation.PushAddress` |
| `StatusPushRequest.installationId` | Control Plane: `Installation.Identifier`; installation: compared with `Installation:Id` from configuration |
| `ServiceResults.ChangePushAddressResult` | `ChangePushAddressResult` |
| `ServiceResults.StatusPushAttemptResult` | `StatusPushAttemptResult` |
| `ServiceResults.PushCheckRequestResult` | `PushCheckRequestResult` |

Never exposed: `Installation.Id` (internal key), `UpdatedAt`, audit rows, push
attempt state.
