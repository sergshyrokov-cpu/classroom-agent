---
artifact_type: database_design
story: US-039
version: 1
status: DRAFT
created_at: 2026-10-03T08:54:15Z
updated_at: 2026-10-03T08:54:15Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-039-spec.md
    version: 1
  - path: docs/decisions/US-039-open-decisions.md
    version: 1
  - path: docs/designs/api/US-039-api-design.md
    version: 1
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-039 Database Design — Choose UI language (Admin, Dean and Owner)

**Verdict: PASS, with no schema change.** No table, column, key, index,
constraint, relationship or migration is added or altered (spec FR-001, OD-003).
The stage is *not* `NOT_APPLICABLE`: the Story introduces a **new write** to two
existing columns, and how that write is made — through which entity method, with
which concurrency and timestamp behaviour — is a persistence decision recorded
here. Entity details: `docs/designs/database/US-039-entity-model.md`.

## 1. Columns used (existing, unchanged)

| Database | Table | Column | Type / constraints (as built) | Created by |
|---|---|---|---|---|
| installation | `app_user` | `ui_language` | `varchar(8)`, `NOT NULL`, check `ck_app_user_ui_language` (`IN ('uk','en')`); value converter `uk` ↔ `UiLanguage.Uk`, `en` ↔ `UiLanguage.En` | US-008 |
| Control Plane | `owner` | `ui_language` | `varchar(8)`, `NOT NULL`, check `ck_owner_ui_language` (`IN ('uk','en')`); the same converter on `ControlPlane.Persistence.UiLanguage` | US-001 |

Both columns are already guarded twice: the converter can emit only `uk` or
`en` and rejects any other code on read, and each table's check constraint
rejects any other value at the database. Nothing is added.

## 2. The write

### 2.1 Installation — `AppUser`

- A new domain method `AppUser.ChooseUiLanguage(UiLanguage language)` sets
  `UiLanguage` and renews `ConcurrencyStamp`, the pattern every other mutating
  method of `AppUser` follows.
- It does **not** rotate `SecurityStamp` (spec I-3), and touches no other
  property: not `LastSuccessfulSignInAt`, not the lockout fields, not
  `PasswordIsTemporary`.
- `UpdatedAt` is stamped by the existing `TimestampInterceptor` (PC-6); the
  method does not set it.
- The use case loads the row **tracked** by the session's account id, calls the
  method and commits once under `PermittedServiceWrite.SignInBookkeeping`
  (spec FR-007). The untracked `GetSessionStateAsync` read used by the
  per-request stamp check is not reused for the write.
- Choosing the stored language still calls the method and commits (one row,
  renewed `ConcurrencyStamp` and `UpdatedAt`); this keeps the use case free of a
  branch that tests would otherwise have to cover separately. No user-visible
  difference.

### 2.2 Control Plane — `Owner`

- `Owner` is a plain mutable persistence class (the Control Plane has no Domain
  project). The `Services` method sets `UiLanguage` and renews
  `ConcurrencyStamp` with `OwnerStamps.New()`, as `OwnerSessionService` and
  `OwnerSignInService` already do. `SecurityStamp` is not touched.
- `UpdatedAt` is stamped by the Control Plane's `TimestampInterceptor` (PC-6).

## 3. Concurrency

`ConcurrencyStamp` is a concurrency token on both tables. If another write to the
same row commits between the load and the save — for example, a sign-out in a
second tab rotating the stamp — the save fails with a concurrency exception.
That case is **not retried**: the request ends as the spec's "database failure"
row (`500`, error page, nothing stored, session unchanged — api-design §5), and
the user simply clicks again; a sign-out in the meantime will have ended the
session anyway. No new handling is designed.

## 4. Sensitive data

`ui_language` is not personal data in the sense of SC-10 and holds no
credential. No column with a hash, stamp or token is read into a DTO by this
Story; the re-issued session copies the security stamp from the **current
cookie**, not from the database (api-design §2.3).

## 5. Migrations and schema initialisation

None. `dotnet ef migrations add` must produce an empty migration for both
contexts after this Story; TEST_WRITING / IMPLEMENTATION may assert the model
snapshot is unchanged. No `EnsureCreated()` (PC-2).

## 6. Read-only mode

The write is the BR-026 permitted "user chooses their UI language". It changes
one column of one `app_user` row and nothing else, which is what makes the
declaration safe (spec FR-007, AC-003).

## 7. Traceability

| Requirement | Design element |
|---|---|
| FR-004, AC-002 | §2.1, §2.2 — own row only, by session id |
| FR-005, I-3 | §2 — security stamp not rotated |
| FR-007, AC-003 | §2.1, §6 |
| OD-003 | §1, §5 — no schema change |
| §8 error table "database failure" | §3 |
