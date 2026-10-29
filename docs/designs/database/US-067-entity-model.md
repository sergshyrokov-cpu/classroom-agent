---
artifact_type: entity_model
story: US-067
version: 1
status: DRAFT
created_at: 2026-10-29T08:49:54Z
updated_at: 2026-10-29T08:49:54Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-067-spec.md
    version: 1
  - path: docs/designs/api/US-067-api-design.md
    version: 1
  - path: docs/designs/api/US-067-openapi.yaml
    version: 1
  - path: docs/decisions/US-067-open-decisions.md
    version: 1
supersedes: null
---

# US-067 Entity Model — An Admin changes a Dean's login

Companion of `docs/designs/database/US-067-db-design.md`. No new entity, no new property.

## 1. Business concepts → entities

| Concept (glossary / spec) | Entity / member |
|---|---|
| Dean account, its login (= work email) | `AppUser` (`Role = Dean`), `Email`, `NormalizedEmail` |
| "Open session ends" | `AppUser.SecurityStamp` rotated |
| "Dean login changed" audit event | `AuditEvent` with `AuditAction.DeanLoginChanged` |

## 2. `AppUser` — new domain method (Domain)

```csharp
/// <summary>
/// An Admin changes a Dean's login to the renamed work email (US-067 spec FR-003; BR-014, SC-2 v104). Only the
/// login and the stamps change: password, temporary mark, counter, lockout, disabled state, language, last
/// sign-in and creation time stay as they were, and the rotated stamp ends open sessions at their next request.
/// </summary>
public void ChangeLogin(string email, DateTimeOffset at)
```

Behaviour:

- `ArgumentException.ThrowIfNullOrWhiteSpace(email)`.
- `if (Role != AppRole.Dean) throw new InvalidOperationException(...)` — an Admin's login is their
  Google account and `AllowedAdmin` entry and is never changed here (BR-010; spec §7). The use case
  already refuses non-Deans with `NoSuchDeanAccount`; this is the entity's own invariant.
- `var normalized = Normalize(email);` `Email = normalized; NormalizedEmail = normalized;`
- `RotateSecurityStamp();` (rotates `SecurityStamp` and `ConcurrencyStamp`).
- No domain validation of format or school domain: those rules need the school domain from
  `LegitimacyState` and live in `Application` with creation's (spec FR-002 "same code"), as for
  `CreateDean`.
- `at` is accepted for symmetry with `Disable`, `ReEnable`, `ResetPassword`; it is not stored
  (`UpdatedAt` is set by the interceptor).
- The "same address" case is decided by the use case **before** calling the method (spec FR-006:
  nothing is written, no rotation). The method itself does not check it.

## 3. `AuditAction` — new member (Domain)

```csharp
/// <summary>US-067 spec FR-007 (SC-11 v104): an Admin changed a Dean account's login. Neither address is recorded.</summary>
DeanLoginChanged,
```

Appended after `ErasureExclusionEntered`. Code `dean_login_changed` (db-design §3).

## 4. `AuditEvent` factories (Domain)

- `DeanAccountManaged(long adminId, AuditAction action, long deanId, DateTimeOffset occurredAt, string? requestId)`
  — signature unchanged; the guard accepts `DeanLoginChanged` as the fifth action.
- `DeanAccountManagementRefused(...)` — unchanged; already accepts any action.

## 5. Application types (for the DTO mapping)

- `DeanAccountRefusal` gains `LoginUnchanged` (spec FR-006; API D-3 → `409` re-render).
- The use case returns the existing `DeanAccountActionOutcome` (refusal or success, no account id
  needed by the controller). Use-case name and file are IMPLEMENTATION's
  (`ChangeDeanLoginUseCase` suggested, beside `SetDeanAccountStateUseCase`).
- No new DTO: the screen keeps `DeanAccountsPageModel` / `DeanRow` (`email` shows the new login).

## 6. Mapping to API

| API element (US-067 openapi) | Entity / type |
|---|---|
| path `deanId` | `AppUser.Id` (`Role = Dean`) |
| form `newEmail` | → `AppUser.ChangeLogin(email)` after Application checks |
| `302` success | `AppUser` updated + `AuditEvent(DeanLoginChanged, succeeded)` |
| `409` read-only | `AuditEvent(DeanLoginChanged, refused, read_only_mode)` only |
| `409` unchanged | `DeanAccountRefusal.LoginUnchanged`, nothing written |
| `400` refusals | `DeanAccountRefusal.NotAnEmailAddress` / `OutsideSchoolDomain` / `EmailAlreadyUsed` |
| `DeanRow.email` | `AppUser.Email` |
