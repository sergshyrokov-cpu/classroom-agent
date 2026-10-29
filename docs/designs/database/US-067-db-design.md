---
artifact_type: database_design
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

# US-067 Database Design — An Admin changes a Dean's login

**Verdict: PASS.** No table, column, index or foreign key changes. One value is added to the
closed list of audit actions, so the CHECK constraint `ck_audit_event_action` is rebuilt by one
migration (spec FR-007, PC-2). `app_user` keeps its schema; its rows gain one new kind of update
(the login), made through a new domain method (entity model §2).

Delegation: none. The designer read `AppUser.cs`, `AppUserConfiguration.cs`,
`AuditEventConfiguration.cs` (CHECK at :28-38, code maps at :358-418), `AuditEvent.cs`
(`DeanAccountManaged` :544, `DeanAccountManagementRefused` :579) and the latest migration
`20261028123007_AddErasureExclusionInAdvance.cs` directly.

## 1. Tables touched

| Table | Change |
|---|---|
| `app_user` | none in schema; rows may now have `email`, `normalized_email`, `security_stamp`, `concurrency_stamp`, `updated_at` updated by a login change |
| `audit_event` | none in columns; CHECK `ck_audit_event_action` accepts one more code |

## 2. `app_user` — the login change as an update

Existing columns used (unchanged definitions, `AppUserConfiguration.cs`):

| Column | Type | Rule relevant here |
|---|---|---|
| `email` | `varchar(254)` NOT NULL | set to the normalized new address (BR-079: stored lower-cased) |
| `normalized_email` | `varchar(254)` NOT NULL, unique index `uq_app_user_normalized_email` | set to the same value; the index is the final guard of "not taken by another account" |
| `security_stamp` | `varchar(64)` NOT NULL | rotated — ends open sessions (SC-2 v104) |
| `concurrency_stamp` | `varchar(64)` NOT NULL, concurrency token | rotated by `RotateSecurityStamp` |
| `updated_at` | `timestamptz` NOT NULL | moved by `TimestampInterceptor`, as on every save |

Every other column — `id`, `role`, `sign_in_method`, `password_hash`, `password_is_temporary`,
`access_failed_count`, `lockout_end`, `ui_language`, `is_disabled`, `last_successful_sign_in_at`,
`created_at` — is **not written** (spec FR-003, AC-006). The retention clock
(`last_successful_sign_in_at ?? created_at`, PC-11) is therefore unchanged.

**Length.** The validated address is at most 254 characters (spec §6), equal to the column length;
no truncation path exists.

**Uniqueness race (spec FR-004).** A second account taking the address between the pre-check and the
save makes the unique index refuse the UPDATE; `UnitOfWork` already maps that violation to
`UniqueEmailViolationException` (`UnitOfWork.cs:28-31`). The transaction rolls back, so neither the
account change nor the audit row is written.

**Concurrent edit of the same row.** `concurrency_stamp` is a concurrency token: if another Admin
action changed the same account between load and save, the save raises the existing
`DbUpdateConcurrencyException`, exactly as for disable / reset today. Nothing is written; the
existing host handling applies. No new behaviour is designed here.

**No history of old logins is stored.** The old address is not kept anywhere — not on the row, not in
audit (SC-11 v104: "neither address"). Earlier audit rows reference the account by `target_id` /
`actor_id`, which do not change, so the history stays joined to the account (AC-001).

**Report templates** reference the author by `author_id` (no FK, US-042); the list joins `app_user`
by id and shows the current `email`. No template row is touched (spec I-6).

## 3. `audit_event` — new action code

| Enum value | Code | Written by |
|---|---|---|
| `AuditAction.DeanLoginChanged` | `dean_login_changed` | success: `AuditEvent.DeanAccountManaged`; read-only refusal: `AuditEvent.DeanAccountManagementRefused` |

- `ActionCode` / `ActionFromCode` (`AuditEventConfiguration.cs`) gain the pair.
- `ck_audit_event_action` gains `'dean_login_changed'` (appended at the end of the list).
- **Success row** (spec FR-007): actor `app_user` / Admin id / role `admin`; target `app_user` /
  Dean id; outcome `succeeded`; `refusal_category` NULL; `request_id`. No other column — neither
  address appears in any column (there is no free-text column on `audit_event` the factory fills).
- **Read-only refusal row** (spec FR-008; closes the concern of SPEC_REVIEW m-1): action
  `dean_login_changed`, target the Dean id, outcome `refused`, category `read_only_mode`.
  `DeanAccountManagementRefused` does not restrict the action (it validates only the category), so
  it already accepts the new value; the CHECK above is what must accept it at the database. The
  existing `ck_audit_event_refusal_category` holds (refused ⇔ category present).
- `DeanAccountManaged`'s guard (`AuditEvent.cs:551`) is widened from four to five actions; its
  exception message ("one of the four actions") is updated with it.
- Audit rows are never updated or deleted by this Story (SC-11).

## 4. Migration

One EF Core migration, name `AddDeanLoginChangedAuditAction` (PC-2), generated by
`dotnet ef migrations add` so the model snapshot follows the configuration:

- **Up:** `DropCheckConstraint("ck_audit_event_action", "audit_event")`, then
  `AddCheckConstraint` with the list of `AuditEventConfiguration` including `'dean_login_changed'`.
- **Down:** first refuse when rows with the new code exist — a `DO $$ … RAISE EXCEPTION … $$` block,
  as `AddErasureExclusionInAdvance` does for its own irreversible case — because audit rows are
  never deleted (SC-11) and the old CHECK cannot be re-added over them. Then drop and re-add the
  CHECK without the code. Message: `'dean_login_changed audit rows exist; Down is refused'`.
- No data migration: no existing row changes.
- No `EnsureCreated()`; the migration is applied by the existing startup path.

## 5. Sensitive data

- No password, hash or stamp is read into or written by the audit row.
- The new address is personal data of a staff member; it is stored only where the login already
  lived (`email`, `normalized_email`) and nowhere else. Logs: SC-10 (spec §7).

## 6. Indexes

None added. Lookups use the existing `uq_app_user_normalized_email` (uniqueness pre-check, sign-in)
and the primary key (`FindDeanByIdAsync`).

## 7. Findings

None.
