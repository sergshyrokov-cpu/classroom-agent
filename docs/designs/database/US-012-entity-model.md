---
artifact_type: entity_model
story: US-012
version: 1
status: DRAFT
created_at: 2026-09-26T17:31:35Z
updated_at: 2026-09-26T17:31:35Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-012-spec.md
    version: 1
  - path: docs/designs/api/US-012-api-design.md
    version: 1
  - path: docs/designs/api/US-012-openapi.yaml
    version: 1
  - path: docs/designs/database/US-012-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-012 Entity Model — Create and manage Dean accounts

Companion to `docs/designs/database/US-012-db-design.md`. It states what changes
in the domain model, what stays transient in `Application`, and how each piece
maps to a business concept and to the DTOs of the contract.

## 1. Entities

### 1.1 `AppUser` (changed)

The only entity this Story changes. It gains one property and the behaviour that
maintains it; every other property is US-008's and unchanged.

| Member | Kind | Purpose |
|---|---|---|
| `PasswordIsTemporary` | new property, `bool`, private setter | The stored hash is of a password an Admin typed; the next successful authentication forces a change (spec FR-006, I-2) |
| `CreateDean(email, passwordHash, uiLanguage, createdAt)` | new static factory | The Dean counterpart of `CreateAdmin`: role `Dean`, sign-in method `Password`, `PasswordHash` non-null, `PasswordIsTemporary = true`, `IsDisabled = false`, `LastSuccessfulSignInAt = null` (spec FR-002, FR-003) |
| `Disable(at)` | new method | Sets `IsDisabled`, rotates the security stamp, touches nothing else (spec FR-007, I-4) |
| `ReEnable(at)` | new method | Clears `IsDisabled` and nothing else — no temporary mark, no lockout cleared (spec FR-008) |
| `ResetPassword(hash, at)` | new method | Replaces the hash, sets the temporary mark, zeroes `AccessFailedCount`, clears `LockoutEnd`, rotates the stamp; **leaves `IsDisabled` as it is** (spec FR-009, S-08) |
| `SetOwnPassword(hash, at)` | new method | Replaces the hash, clears the temporary mark, rotates the stamp; used by both the forced and the voluntary change (spec FR-006, FR-014) |
| `RecordFailedSignIn(at, maxAttempts, lockoutFor)` | new method | Increments the counter and, at the fifth consecutive failure, sets `LockoutEnd` (spec FR-013) |
| `IsLockedOut(now)` | new query method | `LockoutEnd` is in the future — step 2 of FR-012 |
| `RecordSuccessfulSignIn(at)` | **existing**, reused | Already zeroes the counter and records the time; steps 5 and 6 of FR-012 |
| `RotateSecurityStamp()` | **existing**, reused | Called by the four methods above that must end sessions (spec FR-019) |

Rules the entity keeps enforcing by construction, as US-008 built it: the email
is stored lower-cased, `PasswordHash` is null for every Admin, and only the
factories create an instance.

**The entity holds no policy numbers.** The 5 attempts and 15 minutes arrive as
arguments from `Application`, which reads them from the policy of spec FR-005
and FR-013, and the clock arrives as a `DateTimeOffset` the caller took from
`TimeProvider`. A domain entity that knew the lockout duration would be a second
place to change it.

**The entity never hashes or verifies a password.** `IPasswordHasher<AppUser>`
lives in `Infrastructure` behind a port; `Domain` receives a hash and stores it
(AD-3, AD-4, OD-002). That is why every method above takes a hash, not a
password.

### 1.2 `AuditEvent` (new factories only)

No property changes. The entity gains factories for the seven rows of db-design
§4.4, each fixing its own action, target type and permitted outcome:

| Factory | Row |
|---|---|
| `DeanAccountCreated(adminId, deanId, at, requestId)` | succeeded, target the new account |
| `DeanAccountDisabled(adminId, deanId, …)` | succeeded |
| `DeanAccountReEnabled(adminId, deanId, …)` | succeeded |
| `DeanAccountPasswordReset(adminId, deanId, …)` | succeeded |
| `DeanAccountActionRefused(adminId, action, deanId?, category, …)` | refused; accepts only `ReadOnlyMode` |
| `DeanPasswordChanged(deanId, …)` | succeeded, actor and target the same account |
| `DeanSignInSucceeded(deanId, …)` | succeeded |
| `DeanSignInRefused(deanId?, category, …)` | refused; accepts only `UnknownLogin`, `WrongPassword`, `AccountDisabled`, `LockedOut`; with no account it writes the `anonymous` actor and no target |

Restricting each factory to the categories its case allows is the US-011
pattern (`AccessCheckRefused` accepts two), and it is what keeps an impossible
row — "sign-in refused because read-only" — from being writable at all.

### 1.3 Enums

| Enum | Members added |
|---|---|
| `AuditAction` | `DeanAccountCreated`, `DeanAccountDisabled`, `DeanAccountReEnabled`, `DeanAccountPasswordReset`, `DeanPasswordChanged`, `DeanSignIn` |
| `AuditTargetType` | `AppUser` |
| `AuditRefusalCategory` | `UnknownLogin`, `WrongPassword`, `LockedOut` (`AccountDisabled` and `ReadOnlyMode` already exist and are reused) |
| `AppRole`, `SignInMethod`, `UiLanguage` | **unchanged** |

Their database codes are the snake_case strings of db-design §4, mapped by the
existing `HasConversion` pattern in the configurations.

## 2. Transient models in `Application`

None of these is an entity and none is stored (AD-8):

| Model | Purpose |
|---|---|
| `DeanAccountRow` | One list row — id, email, state, `PasswordIsTemporary`, `LastSuccessfulSignInAt` — mapped to the contract's `DeanRow` |
| `DeanAccountsView` | The list plus the read-only state, mapped to `DeanAccountsPageModel` |
| `SignInOutcome` | Which of the six steps of FR-012 ended the attempt, and what the caller must do next (redirect target + message key). It is the sequence's result, not a stored value |
| `PasswordPolicyResult` | Which rule of FR-005 a submitted password broke, as a translation key |
| `CreateDeanRequest`, `SetDeanStateRequest`, `ResetDeanPasswordRequest`, `DeanSignInRequest`, `ChangePasswordRequest` | Validated request models (`Application/Models/Requests`), carrying the DataAnnotations of spec §6 |

`SignInOutcome` lives in `Application`, not `Domain`, for the same reason
US-011's `AccessCheckStepOutcome` did: it describes what a request produced,
including presentation consequences, not a fact about the account.

## 3. Mapping: business concept → entity → DTO

| Business concept (`trebovaniya.md` §2, §3) | Entity member | Contract schema |
|---|---|---|
| Учётная запись Декана | `AppUser` with `Role = Dean` | `DeanRow` |
| Логин — рабочий email в домене школы | `Email` / `NormalizedEmail` | `DeanRow.email`, `CreateDeanForm.email` |
| Временный пароль | `PasswordIsTemporary` + `PasswordHash` | `DeanRow.passwordIsTemporary`; never a response field carrying the password |
| Отключение и включение | `IsDisabled` via `Disable` / `ReEnable` | `DeanRow.state`, `SetDeanStateForm.desiredState` |
| Сброс пароля Админом | `ResetPassword` | `ResetDeanPasswordForm` |
| Смена собственного пароля | `SetOwnPassword` | `ForcedPasswordChangeForm`, `ChangeOwnPasswordForm` |
| Блокировка после 5 неудач | `AccessFailedCount`, `LockoutEnd` | nothing — the caller is told only the common refusal (S-05) |
| Дата последнего успешного входа | `LastSuccessfulSignInAt` | `DeanRow.lastSuccessfulSignInAt` |
| Аудит действия | `AuditEvent` | nothing — audit is never returned by an operation of this Story |

Three entity properties are deliberately absent from every DTO:
`PasswordHash`, `SecurityStamp` and `AccessFailedCount` / `LockoutEnd`. A view
model that carried any of them would be a finding (spec S-10, S-05).

## 4. Repository surface

`IAppUserRepository` (existing, extended) gains only what the use cases need:

- `GetByNormalizedEmailAsync` — **exists**, reused by sign-in and by the
  uniqueness check of VR-001;
- `GetDeanByIdAsync(id)` — returns an account only when its role is Dean, so
  the `404` of the contract is a repository outcome and not a check every
  handler repeats (spec VR-005, I-8);
- `ListDeansAsync()` — every Dean, ordered by email; unpaginated by OD-003.

**No delete method is added to any repository** (spec FR-010, AC-008). The
absence is part of the design, not an omission.

## 5. What the model does not gain

- No `Dean` entity. A Dean is an `AppUser` with a role, exactly as an Admin is;
  a separate entity would split the unique-email rule across two tables.
- No password-history entity (db-design §3.3).
- No session entity: the cookie and the security stamp already carry session
  state (SC-2).
- No link to `ClassroomParticipant` and no "own courses" — neither role has them
  (`trebovaniya.md` §3).
