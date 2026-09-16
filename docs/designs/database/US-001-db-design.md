---
artifact_type: database_design
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

# US-001 Database Design — Control Plane: Owner and AuditEvent

Companion: `docs/designs/database/US-001-entity-model.md`.

## 1. Scope

- **Database:** the Control Plane database only (AD-1, PC-1). Nothing in any
  installation database.
- **Tables introduced:** `owner`, `audit_event`.
- **Not persisted** (by requirement): the one-time setup code (process memory,
  FR-002); Data Protection keys (filesystem directory, FR-017); session state
  (encrypted cookie, FR-010); the sign-in time for the 8-hour limit (a claim in
  the cookie).
- **Not introduced:** ASP.NET Core Identity's role, claim, login and token
  tables. The Owner has one fixed role, no external login, no stored claims and
  no tokens; tables with no use would be unreviewed schema.
- `Installation`, `AllowedAdmin`, `InstanceLicenseCheck` are later Stories.

## 2. Conventions applied

| Rule | Application |
|---|---|
| PC-2 | One EF Core migration in `ClassroomAgent.ControlPlane`, folder `Persistence/Migrations`; applied by the deployment step, never `Migrate()` at startup; no `EnsureCreated()` |
| PC-3 | Surrogate `id bigint` identity (`ValueGeneratedOnAdd`) |
| PC-4 | Every property configured by Fluent API in one configuration class per entity; every string has a max length |
| PC-5 | `snake_case`, singular tables; `UseSnakeCaseNamingConvention()`; constraint names `pk_`, `uq_`, `ix_`, `ck_`, `fk_` |
| PC-6 | `created_at`, `updated_at` `timestamptz` NOT NULL, stamped by an `ISaveChangesInterceptor`; UTC |
| PC-8 | No foreign keys are needed; explicit `Restrict` would apply to any |
| PC-9 | Password only as an Identity hash; `audit_event` carries internal ids only, no FK, never updated or deleted |

DbContext: `ControlPlaneDbContext` in `ClassroomAgent.ControlPlane.Persistence`,
used only from `ControlPlane.Services` (AD-3).

## 3. Table `owner`

The single Owner account of the whole service (`trebovaniya.md` §3 "Owner", §9;
FR-005, FR-008, FR-009, FR-010).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_owner` |
| `user_name` | `varchar(64)` | NOT NULL | — | the login as entered at setup (VR-001) |
| `normalized_user_name` | `varchar(64)` | NOT NULL | — | `uq_owner_normalized_user_name`; upper-invariant of `user_name` — the lookup key for case-insensitive sign-in (FR-008 step 1) |
| `password_hash` | `varchar(256)` | NOT NULL | — | ASP.NET Core Identity password hash (PBKDF2, format V3; ~84 characters today, headroom for a future format); never plaintext (SC-2, PC-9) |
| `security_stamp` | `varchar(64)` | NOT NULL | — | rotated at sign-out, so an old session cookie stops authenticating (api-design §4 POST /sign-out, AC-011) |
| `concurrency_stamp` | `varchar(64)` | NOT NULL | — | optimistic concurrency token (`IsConcurrencyToken`) for lockout-counter and stamp updates |
| `access_failed_count` | `integer` | NOT NULL | `0` | `ck_owner_access_failed_count`: `>= 0` — consecutive failed attempts (FR-009) |
| `lockout_end` | `timestamptz` | NULL | — | end of the current sign-in lockout; NULL or past = no lockout (FR-009) |
| `ui_language` | `varchar(8)` | NOT NULL | — | `ck_owner_ui_language`: `IN ('uk','en')`; set to `uk` at creation (FR-005, NFR-073) |
| `singleton` | `boolean` | NOT NULL | `true` | `ck_owner_singleton`: `= true`; `uq_owner_singleton` — at most one row can ever exist (FR-006) |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6 |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6 |

**Indexes:** `uq_owner_normalized_user_name` (unique; lookup at sign-in),
`uq_owner_singleton` (unique; the one-account guarantee). No other lookup
exists; "does an Owner exist" is `EXISTS (SELECT 1 FROM owner)` on a table of at
most one row.

**Lockout enabled** is not a column: lockout is always on for the Owner (SC-2);
the entity reports it as a constant, it is never switchable.

**Omitted Identity columns:** `email`, `normalized_email`, `email_confirmed`,
`phone_number`, `phone_number_confirmed`, `two_factor_enabled`,
`lockout_enabled`. None is required by `trebovaniya.md` §3; email and phone
would be personal data with no purpose.

### 3.1 Why a singleton column

AC-004 and FR-006 require exactly one account under true concurrency, enforced so
that two transactions cannot both commit. An application check ("no Owner
exists") runs before the insert in both racing transactions and passes in both.
`uq_owner_normalized_user_name` would only stop two submissions with the *same*
login. The constant `singleton = true` column with a unique index makes the
second `INSERT` fail with PostgreSQL `23505 unique_violation` on
`uq_owner_singleton`, whatever the logins, at `READ COMMITTED`. The service maps
that specific constraint violation to the conflict result (`409`, OD-003) and the
whole transaction — the success audit row included — rolls back. No explicit
lock and no `SERIALIZABLE` isolation are needed.

The column is never read or set by application code other than its default.

## 4. Table `audit_event`

The Control Plane audit log (`trebovaniya.md` §3 "AuditEvent", §5 v45/v53/v67;
SC-11; FR-012). This Story creates the table and writes four event kinds; later
Control Plane Stories add actions and categories without schema change.

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_audit_event` |
| `occurred_at` | `timestamptz` | NOT NULL | — | UTC time of the action, from the injectable clock (§5 "время UTC") |
| `actor_type` | `varchar(16)` | NOT NULL | — | `ck_audit_event_actor_type`: `IN ('owner','anonymous')` |
| `actor_id` | `bigint` | NULL | — | the Owner account id; **no FK** (PC-9) |
| `action` | `varchar(64)` | NOT NULL | — | action code, see §4.1 |
| `target_type` | `varchar(32)` | NULL | — | entity type of the object, e.g. `owner` |
| `target_id` | `bigint` | NULL | — | internal id of the object; **no FK** |
| `outcome` | `varchar(16)` | NOT NULL | — | `ck_audit_event_outcome`: `IN ('succeeded','refused')` |
| `refusal_category` | `varchar(32)` | NULL | — | category code, see §4.1 |
| `request_id` | `varchar(128)` | NULL | — | the request identifier also written on the request's log lines; NOT NULL for every event of this Story (all are in a request); nullable for future background events |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6 |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6; always equals `created_at` |

**Check constraints:**

- `ck_audit_event_actor_type` — `actor_type IN ('owner','anonymous')`.
- `ck_audit_event_actor` — `(actor_type = 'anonymous') = (actor_id IS NULL)`:
  an anonymous actor has no id, an Owner actor always has one (§5 v45).
- `ck_audit_event_target` — `(target_type IS NULL) = (target_id IS NULL)`.
- `ck_audit_event_outcome` — `outcome IN ('succeeded','refused')`.
- `ck_audit_event_refusal_category` —
  `(outcome = 'refused') = (refusal_category IS NOT NULL)`.

`action` and `refusal_category` have no check constraint: later Stories add
values (Installation, AllowedAdmin actions; "not in AllowedAdmin", "Control Plane
unavailable" belong to the installation), and each addition would otherwise be a
migration. Their allowed values are closed enums in code (entity model §3).

**Indexes:** only `pk_audit_event`. This Story has no query reading audit rows;
viewing is EPIC-9, which adds the indexes its queries need.

**No personal data:** there is no column for a login, email, name, password,
setup code, IP address or user agent — adding one is a Critical finding (PC-9,
SC-11). The actor and target are internal ids only.

### 4.1 Values written by this Story

| Event (FR-012) | `actor_type` | `actor_id` | `action` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|---|
| Owner sign-in | `owner` | Owner id | `owner_sign_in` | `owner` / Owner id | `succeeded` | NULL |
| sign-in refused, wrong password | `owner` | Owner id | `owner_sign_in` | `owner` / Owner id | `refused` | `wrong_password` |
| sign-in refused, lockout | `owner` | Owner id | `owner_sign_in` | `owner` / Owner id | `refused` | `locked_out` |
| sign-in refused, unknown login | `anonymous` | NULL | `owner_sign_in` | NULL / NULL | `refused` | `unknown_login` |
| Owner account created at first run | `owner` | new Owner id | `owner_first_run_setup` | `owner` / new Owner id | `succeeded` | NULL |
| first-run setup refused, missing/wrong code | `anonymous` | NULL | `owner_first_run_setup` | NULL / NULL | `refused` | `wrong_setup_code` |

The role required by §5 ("актор — id и роль") is `actor_type`: `owner` is the
role `Owner`; `anonymous` has none.

### 4.2 Immutability

Rows are never updated and never deleted in the Control Plane (SC-11, PC-9, v45).
Enforced at three levels:

1. **Entity** — no public setters and no method that changes a row after
   construction (entity model §2.2).
2. **DbContext** — the `SaveChanges` interceptor throws if an `AuditEvent` entry
   is in state `Modified` or `Deleted`; nothing is saved.
3. **Database** — the migration creates
   `fn_audit_event_immutable()` and trigger `trg_audit_event_immutable`
   `BEFORE UPDATE OR DELETE ON audit_event FOR EACH ROW`, raising an exception.
   This also stops `ExecuteUpdate` / `ExecuteDelete` and hand-written SQL through
   the application's connection. (It does not affect `TRUNCATE`, which the
   application never issues; test databases are per test class, PC-1.)

The trigger is created and dropped in the migration's `Up` / `Down` with
`migrationBuilder.Sql(...)` — a schema change inside a migration, as PC-2 allows.

### 4.3 Transactions

- **Account created** — the `owner` insert and its `audit_event` row are saved in
  one transaction (FR-005, AD-7). If the insert loses the race (§3.1), neither
  row remains.
- **Refusals and sign-in** — the audit row is saved in its own transaction. For a
  wrong password, the `access_failed_count` / `lockout_end` update and the audit
  row are saved together in one transaction, so a counted failure is always
  audited and vice versa.
- **Successful sign-in** — the counter reset (`access_failed_count = 0`,
  `lockout_end` cleared) and the audit row in one transaction.

## 5. Sensitive data

| Data | Stored? | Rule |
|---|---|---|
| Owner password | hash only (`owner.password_hash`) | Identity `PasswordHasher`; never plaintext, never on a DTO, never logged (SC-2, AD-8) |
| Password confirmation | no | used only for VR-007 |
| Setup code | no | process memory only (FR-002, OD-005) |
| Owner login | `owner.user_name` | the Owner's own account identifier; never in `audit_event` or logs (§5, v68) |
| Security stamp | `owner.security_stamp` | secret-like; never on a DTO, never logged |
| Lockout state | `owner.access_failed_count`, `lockout_end` | never exposed in any response (FR-008: responses identical) |
| Audit rows | `audit_event` | internal ids and codes only; kept indefinitely |

## 6. Migration

- **Name:** `InitialOwnerAndAudit`.
- **Project / folder:** `src/ClassroomAgent.ControlPlane/Persistence/Migrations/`
  (PC-2: Control Plane migrations live under its own project).
- **Command** (deployment/dev):
  `dotnet ef migrations add InitialOwnerAndAudit --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations`.
- **Up:** create `owner` with all columns, constraints and indexes of §3; create
  `audit_event` with §4; create `fn_audit_event_immutable` and
  `trg_audit_event_immutable`.
- **Down:** drop the trigger and function, then both tables.
- **Expected effect on an empty Control Plane database:** two tables, no rows.
  The setup gate (FR-001) then finds no Owner.
- **Backward compatibility:** first migration; nothing to keep compatible.

## 7. Test-relevant facts

- Integration tests run against PostgreSQL via Testcontainers, one database per
  test class, schema created by applying the migrations — not `EnsureCreated()`
  (TC-2, PC-2).
- The one-account guarantee is proven by two concurrent setup transactions
  against real PostgreSQL: one `owner` row, one `owner_first_run_setup`
  `succeeded` row, the loser mapped to conflict (AC-004).
- `ck_audit_event_*` and `uq_owner_singleton` are asserted by direct inserts that
  must fail.
- An `UPDATE` and a `DELETE` on `audit_event` through the application's
  connection must fail (trigger) — AC-008 "cannot update or delete".
- Lockout timing tests set `lockout_end` via the injectable clock rather than
  waiting 15 minutes.

## 8. Rationale for choices not fixed upstream

- **No Identity roles/claims/logins/tokens tables.** The role `Owner` is added to
  the principal at sign-in, not stored. Using Identity's full schema would add
  four empty tables and an email column (personal data) nobody needs. The
  entity model records the store interfaces this implies.
- **Security stamp for sign-out invalidation** instead of a server-side session
  table: no new table, and the cookie validation compares the stamp on every
  request (validation interval zero), so a stolen cookie stops working at
  sign-out.
- **`occurred_at` separate from `created_at`.** The event time comes from the
  injectable clock (testable, identical to the time used for lockout), while
  `created_at` stays the PC-6 interceptor's column.
- **Codes as `varchar` with check constraints only where the set is stable.**
