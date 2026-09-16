---
artifact_type: entity_model
story: US-001
version: 1
status: DRAFT
created_at: 2026-09-16T08:27:43Z
updated_at: 2026-09-16T08:27:43Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/designs/api/US-001-api-design.md
    version: 1
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
  - path: docs/decisions/US-001-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 68
supersedes: null
---

# US-001 Entity Model — Control Plane: Owner and AuditEvent

Schema: `docs/designs/database/US-001-db-design.md`.

The Control Plane is one project with internal namespaces (AD-3); it does not
reference `ClassroomAgent.Domain`. Persistence entities live in
`ClassroomAgent.ControlPlane.Persistence`, configurations in
`ClassroomAgent.ControlPlane.Persistence.Configurations`, one class per file.

## 1. Business concepts → entities

| Business concept (glossary / §3) | Entity | Table |
|---|---|---|
| **Owner** — the single account of the service Owner, claimed at first run with the setup code | `Owner` | `owner` |
| **AuditEvent** — who did what, when, with what outcome; internal ids only | `AuditEvent` | `audit_event` |
| **Setup code** | — (in-memory service state, not an entity) | — |

## 2. Entities

### 2.1 `Owner`

A plain class — it does not derive from `IdentityUser<long>`, whose email,
phone and two-factor members this schema deliberately omits; the custom store of
§4 works with it. The mapped members are exactly these:

| Property | C# type | Column | Business meaning |
|---|---|---|---|
| `Id` | `long` | `id` | internal id; the audit actor/target id |
| `UserName` | `string` | `user_name` | the login (VR-001) |
| `NormalizedUserName` | `string` | `normalized_user_name` | case-insensitive lookup key |
| `PasswordHash` | `string` | `password_hash` | Identity hash |
| `SecurityStamp` | `string` | `security_stamp` | invalidates sessions at sign-out |
| `ConcurrencyStamp` | `string` | `concurrency_stamp` | concurrency token |
| `AccessFailedCount` | `int` | `access_failed_count` | consecutive failed sign-ins |
| `LockoutEnd` | `DateTimeOffset?` | `lockout_end` | lockout end |
| `UiLanguage` | `UiLanguage` (enum `Uk`, `En`) | `ui_language` (`'uk'`/`'en'` via value converter) | account language |
| `CreatedAt` | `DateTimeOffset` | `created_at` | PC-6 |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` | PC-6 |

Shadow property `Singleton` (`bool`, default `true`) → `singleton`; not on the
CLR type, never set by code.

Nullability: every property is required except `LockoutEnd`. There is no
`LockoutEnabled` property: the store reports lockout as always enabled.

Invariants:

- created only by the first-run setup service, with `UiLanguage = Uk`;
- `UserName` satisfies VR-001 at creation; there is no rename in this Story;
- never deleted by the application.

### 2.2 `AuditEvent`

Immutable after construction: `private set` / `init` only, created through a
static factory per event kind, no update methods.

| Property | C# type | Column |
|---|---|---|
| `Id` | `long` | `id` |
| `OccurredAt` | `DateTimeOffset` (UTC) | `occurred_at` |
| `ActorType` | `AuditActorType` (`Owner`, `Anonymous`) | `actor_type` (`'owner'`, `'anonymous'`) |
| `ActorId` | `long?` | `actor_id` |
| `Action` | `AuditAction` (`OwnerSignIn`, `OwnerFirstRunSetup`) | `action` (`'owner_sign_in'`, `'owner_first_run_setup'`) |
| `TargetType` | `AuditTargetType?` (`Owner`) | `target_type` (`'owner'`) |
| `TargetId` | `long?` | `target_id` |
| `Outcome` | `AuditOutcome` (`Succeeded`, `Refused`) | `outcome` (`'succeeded'`, `'refused'`) |
| `RefusalCategory` | `AuditRefusalCategory?` (`UnknownLogin`, `WrongPassword`, `LockedOut`, `WrongSetupCode`) | `refusal_category` (`'unknown_login'`, `'wrong_password'`, `'locked_out'`, `'wrong_setup_code'`) |
| `RequestId` | `string?` | `request_id` |
| `CreatedAt` | `DateTimeOffset` | `created_at` |
| `UpdatedAt` | `DateTimeOffset` | `updated_at` |

Enums are stored as the snake_case strings above through explicit value
converters — never as integers (a reordered enum would silently rewrite history)
and never by `ToString()` of the member name.

Factories (the only constructors the services use), matching db-design §4.1:

| Factory | Sets |
|---|---|
| `OwnerSignInSucceeded(ownerId, occurredAt, requestId)` | actor owner/id, target owner/id, succeeded |
| `OwnerSignInRefused(ownerId, category, occurredAt, requestId)` | actor owner/id, target owner/id, refused, category ∈ {`WrongPassword`, `LockedOut`} |
| `OwnerSignInRefusedUnknownLogin(occurredAt, requestId)` | actor anonymous, no target, refused, `UnknownLogin` |
| `OwnerFirstRunSetupSucceeded(ownerId, occurredAt, requestId)` | actor owner/new id, target owner/new id, succeeded |
| `OwnerFirstRunSetupRefusedWrongCode(occurredAt, requestId)` | actor anonymous, no target, refused, `WrongSetupCode` |

A factory accepts no string that could carry a login, password or code, so the
type makes the "no personal data" rule structural.

`OwnerFirstRunSetupSucceeded` needs the Owner id before commit: the service adds
the `Owner`, saves (id generated) and adds the audit event in the same explicit
transaction, then commits (db-design §4.3).

## 3. Enums

In `ClassroomAgent.ControlPlane.Persistence` (persistence codes), one per file:
`UiLanguage`, `AuditActorType`, `AuditAction`, `AuditTargetType`,
`AuditOutcome`, `AuditRefusalCategory`. Adding a member is a code change with its
value converter mapping; no migration for `AuditAction` /
`AuditRefusalCategory` (no check constraint), a migration for the others.

## 4. Identity integration

- Only the Identity features this Story uses: password hashing and verification
  (`IPasswordHasher<Owner>`), normalization (`ILookupNormalizer`), the security
  stamp, and the failed-count / lockout fields.
- The user store implements `IUserStore<Owner>`, `IUserPasswordStore<Owner>`,
  `IUserSecurityStampStore<Owner>` and `IUserLockoutStore<Owner>` against
  `ControlPlaneDbContext`, and **nothing else** — not `IUserClaimStore`,
  `IUserLoginStore`, `IUserAuthenticationTokenStore`, `IUserRoleStore`,
  `IUserEmailStore`, `IUserPhoneNumberStore`, `IUserTwoFactorStore`. So
  `UserManager` reports those features unsupported and never queries tables that
  do not exist (db-design §1). The claims principal (id, role `Owner`, language,
  sign-in time, security stamp) is built by the sign-in service, not read from a
  claims table.
- `IdentityOptions`: `Lockout.MaxFailedAccessAttempts = 5`,
  `Lockout.DefaultLockoutTimeSpan = 15 min`, `Lockout.AllowedForNewUsers = true`;
  `Password.*` all disabled / zero (VR-002 … VR-003 are enforced by the request
  validation, not by Identity's `PasswordValidator`, which counts UTF-16 units);
  `User.RequireUniqueEmail = false`; `User.AllowedUserNameCharacters` covers
  VR-001's set.
- The sign-in sequence (FR-008) uses `UserManager.FindByNameAsync`,
  `IsLockedOutAsync`, `CheckPasswordAsync`, `AccessFailedAsync`,
  `ResetAccessFailedCountAsync`, and sets `LockoutEnd = null` on success — never
  `SignInManager.PasswordSignInAsync` / `CheckPasswordSignInAsync` (SC-2 v66).
  `AccessFailedAsync` resets the counter to 0 when it starts a lockout, which is
  the spec's I-4.
- `ControlPlaneDbContext` derives from `DbContext`, not `IdentityDbContext`: it
  maps `Owner` and `AuditEvent` only.

## 5. Mapping to API DTOs

No entity appears in a page model, form model or view model (AD-8). The services
return result types; page models bind the request forms of the API design.

| API element (openapi) | Direction | Entity / service type | Fields that cross |
|---|---|---|---|
| `SetupRequest` (`setupCode`, `login`, `password`, `passwordConfirmation`) | in | → `FirstRunSetupService.CreateOwnerAsync(login, password, setupCode, requestId, ct)` → `Owner` | `login` → `UserName`, `NormalizedUserName`; `password` → `PasswordHash` (hashed); `setupCode` compared, not stored; `passwordConfirmation` checked at binding, not passed on |
| `SignInRequest` (`login`, `password`) | in | → `OwnerSignInService.SignInAsync(login, password, requestId, ct)` | `login` → lookup by `NormalizedUserName`; `password` verified against `PasswordHash` |
| `FirstRunSetupResult` | out | `Created(OwnerSessionDto)` / `WrongSetupCode` / `AlreadyExists` | no entity data except the session DTO |
| `OwnerSignInResult` | out | `SignedIn(OwnerSessionDto)` / `Refused` | `Refused` carries **no** reason: the three refusals must be indistinguishable to presentation |
| `OwnerSessionDto` | out | from `Owner` | `OwnerId` (`Id`), `UiLanguage`, `SecurityStamp` for the cookie principal only; never `PasswordHash`, `UserName`, lockout fields |

`SecurityStamp` crosses only into the authentication cookie (encrypted by Data
Protection), never into a rendered page.

## 6. Namespaces and files

| Type | Namespace | File |
|---|---|---|
| `ControlPlaneDbContext` | `ClassroomAgent.ControlPlane.Persistence` | `Persistence/ControlPlaneDbContext.cs` |
| `Owner`, `AuditEvent`, enums | `ClassroomAgent.ControlPlane.Persistence` | `Persistence/<Type>.cs` |
| `OwnerConfiguration`, `AuditEventConfiguration` | `ClassroomAgent.ControlPlane.Persistence.Configurations` | one file each |
| `TimestampInterceptor` (PC-6, plus the AuditEvent Modified/Deleted guard) | `ClassroomAgent.ControlPlane.Persistence` | one file |
| `OwnerUserStore` | `ClassroomAgent.ControlPlane.Persistence` | one file |
| migration `InitialOwnerAndAudit` | `ClassroomAgent.ControlPlane.Persistence.Migrations` | generated |
| service result types, `OwnerSessionDto` | `ClassroomAgent.ControlPlane.Services` | one file each |

`Persistence.Configurations` and `Persistence.Migrations` are sub-namespaces of
the `Persistence` namespace in `package-map.md`, not new top-level namespaces.
