---
artifact_type: database_design
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
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 70
supersedes: null
---

# US-003 Database Design — Control Plane: AllowedAdmin

Companion: `docs/designs/database/US-003-entity-model.md`.

## 1. Scope

- **Database:** the Control Plane database only (AD-1, PC-1).
- **Table introduced:** `allowed_admin`.
- **Tables referenced, unchanged:** `installation` (US-002), `owner` (US-001).
- **Table extended without schema change:** `audit_event` — new `action` and
  `target_type` codes only (no check constraint on those columns, US-001
  db-design §4).
- **Not introduced:** anything for the Admin login check (US-008), revocation
  history, soft delete, passwords or secrets.

## 2. Conventions applied

| Rule | Application |
|---|---|
| PC-2 | One new EF Core migration `AddAllowedAdmin`; applied by deployment; no `EnsureCreated()` |
| PC-3 | Surrogate `id bigint` identity; the UUID `identifier` (routes, API design §2) is a separate uniquely-indexed column, as for `installation` |
| PC-4 | Every property configured in `AllowedAdminConfiguration`; every string has a max length |
| PC-5 | `snake_case`, singular; `pk_`, `fk_`, `uq_`, `ix_`, `ck_` names |
| PC-6 | `created_at`, `updated_at` `timestamptz` NOT NULL, stamped by `TimestampInterceptor` from the injected `TimeProvider` |
| PC-7 | Unique index on (`installation_id`, `email`) serves the duplicate check and the per-installation ordered list; unique index on `identifier` serves routes |
| PC-8 | `allowed_admin` → `installation` many-to-one, and → `owner` many-to-one; both FKs `ON DELETE RESTRICT`, declared in Fluent API |
| PC-9 | No password, token or secret column (SC-3); audit rows carry internal ids only, never the email |

## 3. Table `allowed_admin`

An email permitted to be Admin of one installation, who added it and when
(`trebovaniya.md` §3 "AllowedAdmin", v70; spec FR-003, FR-005, FR-011).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_allowed_admin`; internal key; audit target id; never shown or routed |
| `identifier` | `uuid` | NOT NULL | — | `uq_allowed_admin_identifier`; random UUIDv4 set by the service at creation (`Guid.NewGuid()`); route `{adminId}` |
| `installation_id` | `bigint` | NOT NULL | — | `fk_allowed_admin_installation` → `installation.id`, `ON DELETE RESTRICT` |
| `email` | `varchar(254)` | NOT NULL | — | lower case; `ck_allowed_admin_email_lower`, `ck_allowed_admin_email_format`; unique with `installation_id` |
| `added_by_owner_id` | `bigint` | NOT NULL | — | `fk_allowed_admin_added_by_owner` → `owner.id`, `ON DELETE RESTRICT`; **"кем добавлен"** |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6; **"когда добавлен"** — the date added shown to the Owner (FR-001, I-3) |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6; always equals `created_at` (rows are never updated, §3.3) |

### 3.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_allowed_admin` | `PRIMARY KEY (id)` | PC-3 |
| `uq_allowed_admin_identifier` | `UNIQUE (identifier)` | route lookup; identifier unique |
| `uq_allowed_admin_installation_email` | `UNIQUE (installation_id, email)` | one entry per email per installation (FR-003, S-04); concurrency guarantee; conflict mapping by this name |
| `fk_allowed_admin_installation` | `FOREIGN KEY (installation_id) REFERENCES installation (id) ON DELETE RESTRICT` | ownership (FR-008); an installation is never deleted anyway (US-002 trigger) |
| `fk_allowed_admin_added_by_owner` | `FOREIGN KEY (added_by_owner_id) REFERENCES owner (id) ON DELETE RESTRICT` | "added by" is a real Owner account; no use case deletes an Owner |
| `ck_allowed_admin_email_lower` | `email = lower(email)` | stored lower case, so the plain unique index is case-insensitive in effect (VR-001, AC-004) |
| `ck_allowed_admin_email_format` | `char_length(email) BETWEEN 3 AND 254 AND email ~ '^[a-z0-9._''-]{1,64}@[a-z0-9.-]+$'` | last line of defence for the stored shape; dot placement in the name part is enforced by request validation |

The unique index is on the lower-cased column; `ck_allowed_admin_email_lower`
makes that equivalent to a case-insensitive uniqueness, as for
`installation.domain`.

### 3.2 Domain match — trigger

The rule "email only in the installation's domain" (BR-079, S-03) involves two
tables, so a check constraint cannot hold it. Trigger
`trg_allowed_admin_domain_match` `BEFORE INSERT ON allowed_admin FOR EACH ROW`,
function `fn_allowed_admin_domain_match()`: raises an exception unless
`split_part(NEW.email, '@', 2) = (SELECT domain FROM installation WHERE id =
NEW.installation_id)`. The service checks the same rule first and answers `400`
(spec FR-003 step 5); the trigger only guarantees no path stores a foreign-domain
entry. Since `installation.domain` never changes (US-002 trigger), the invariant
holds for the row's lifetime.

### 3.3 No update

An entry is never edited (`trebovaniya.md` §3, v70): email, installation, added-by
and added-at are fixed. Trigger `trg_allowed_admin_no_update`
`BEFORE UPDATE ON allowed_admin FOR EACH ROW`, function
`fn_allowed_admin_no_update()`, raises an exception for any update. The entity has
no mutating member (entity model §2.1).

**Delete is allowed** — revocation is a physical delete (spec FR-005,
`trebovaniya.md` §5: the email is deleted with the entry). No trigger restricts
it.

### 3.4 Indexes

| Query | Index |
|---|---|
| revocation page / POST by `{adminId}` | `uq_allowed_admin_identifier` |
| entries of an installation, ordered by email (detail page) | `uq_allowed_admin_installation_email` (leading `installation_id`, then `email`) |
| entry count of an installation (warning, I-10) | same index |
| "email already an entry of this installation?" | same index |
| FK `added_by_owner_id` | `ix_allowed_admin_added_by_owner_id` — keeps the `owner` RESTRICT check from scanning; one Owner, but PC-7 indexes every FK column |

The FK on `installation_id` is covered by the unique index's leading column.

## 4. `audit_event` — values added by this Story

Unchanged schema. New codes:

| Event (spec FR-007) | `actor_type` | `actor_id` | `action` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|---|
| AllowedAdmin added | `owner` | Owner id | `allowed_admin_added` | `allowed_admin` / `allowed_admin.id` | `succeeded` | NULL |
| AllowedAdmin revoked | `owner` | Owner id | `allowed_admin_revoked` | `allowed_admin` / `allowed_admin.id` (of the deleted row) | `succeeded` | NULL |

`audit_event.target_id` has no FK (US-001), so a revoked entry's id stays in its
rows after the delete (spec FR-007). No email, installation name, domain or UUID
in any column.

## 5. Transactions and concurrency

- **Add** — one explicit transaction: insert `allowed_admin`, save (identity `id`
  generated), add the `allowed_admin_added` audit row with that id, save, commit.
  On `23505 unique_violation` with `ConstraintName =
  uq_allowed_admin_installation_email`: roll back — neither row remains — and
  return the "taken" result (spec FR-003, AC-004). The pre-check (step 6) runs
  before the transaction; the constraint decides a race. A `P0001` from
  `trg_allowed_admin_domain_match` cannot occur after the service check passed
  and is not mapped — it would surface as `500` and indicates a defect.
- **Revoke** — one explicit transaction:
  1. select `id` of the row where `identifier = @adminId AND installation_id =
     @installationId`; none → roll back, not found;
  2. `DELETE FROM allowed_admin WHERE id = @id` (EF Core `ExecuteDeleteAsync`),
     read the affected row count; `0` (deleted meanwhile by a concurrent request)
     → roll back, not found, no audit (spec FR-005 step 3);
  3. add the `allowed_admin_revoked` audit row with that id, save, commit.
  Two concurrent revocations of one entry: the second `DELETE` waits on the row
  lock, then affects 0 rows → `404`; exactly one audit row.
- **Revoke vs add of the same email** — independent: a re-added email is a new
  row with a new `id` and `identifier`.
- **Isolation** — `READ COMMITTED`; the unique index and the affected-row count,
  not isolation, give the guarantees. No explicit lock; no concurrency token (rows
  are never updated).

## 6. Sensitive data

| Data | Stored? | Rule |
|---|---|---|
| Admin email | `allowed_admin.email` | personal data of a school employee; stored only here; deleted with the entry on revocation (`trebovaniya.md` §5); never in `audit_event`, logs or any other table (SC-10, SC-11) |
| Admin password / OAuth token | **no** | Admins have no password (SC-3); a column for one is a Critical finding |
| Entry UUID | `allowed_admin.identifier` | not a secret; appears in Owner-only URLs; not in audit rows |
| Added-by Owner id | `allowed_admin.added_by_owner_id` | internal id |

## 7. Migration

- **Name:** `AddAllowedAdmin`.
- **Command:**
  `dotnet ef migrations add AddAllowedAdmin --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations`.
- **Up:** create `allowed_admin` with §3 columns, constraints and indexes; create
  `fn_allowed_admin_domain_match` + `trg_allowed_admin_domain_match` and
  `fn_allowed_admin_no_update` + `trg_allowed_admin_no_update` via
  `migrationBuilder.Sql`.
- **Down:** drop both triggers and functions, then the table.
- **Effect on an existing Control Plane database** (US-001, US-002 applied): one new
  empty table; `owner`, `installation`, `audit_event` untouched.
- **Backward compatibility:** additive.

## 8. Test-relevant facts

- Integration tests apply all three migrations to Testcontainers PostgreSQL
  (TC-2). US-001/US-002 migration tests that enumerate tables or migrations must
  account for `allowed_admin` / `AddAllowedAdmin`.
- Two concurrent additions of one email (different letter case) to one
  installation: one `allowed_admin` row, one `allowed_admin_added` row, the other
  request `409` (AC-004).
- The same email may be inserted for two installations only if their domains are
  equal, which `uq_installation_domain` forbids — tests need not cover it.
- Direct inserts must fail for: duplicate (`installation_id`, `email`), duplicate
  `identifier`, upper-case `email`, email with `+` or a space, an email whose
  domain differs from the installation's (trigger), unknown `installation_id` or
  `added_by_owner_id` (FK).
- Direct `UPDATE` of any column must fail (trigger); `DELETE` succeeds.
- Two concurrent revocations: one row deleted, one audit row, the other `404`.
- A suspended installation for AC-006 is prepared by a direct
  `UPDATE installation SET status = 'suspended'` — allowed: the US-002 trigger
  protects only `identifier` and `domain`.
- `created_at` is controlled through the fake `TimeProvider`, so the displayed
  `dd.MM.yyyy HH:mm UTC` value is assertable.

## 9. Rationale for choices not fixed upstream

- **`created_at` doubles as "added at"** — set once, UTC, from the injectable
  clock, never updated — the same reasoning as `installation.created_at`
  (US-002 db-design §9).
- **FK to `owner` for "added by"** (spec FR-011 left it to this stage): unlike
  audit rows, which must outlive the accounts they name (`trebovaniya.md` §5
  v53), an entry is meaningful only while the Owner account exists, and the
  Control Plane has no Owner deletion. The FK keeps the value honest.
- **Domain-match trigger** — the rule is the point of v70 (a personal address can
  never become an Admin); as with US-002's immutability triggers, the database is
  the only layer that also stops hand-written SQL.
- **No-update trigger rather than per-column** — every column is immutable, so
  forbidding `UPDATE` entirely is simpler and exact.
- **Physical delete, no history table** — `trebovaniya.md` §5 requires the email to
  disappear with the entry; the audit row keeps the fact without the email.
