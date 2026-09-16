---
artifact_type: database_design
story: US-002
version: 1
status: DRAFT
created_at: 2026-09-16T13:53:54Z
updated_at: 2026-09-16T13:53:54Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-002-spec.md
    version: 2
  - path: docs/designs/api/US-002-api-design.md
    version: 1
  - path: docs/designs/api/US-002-openapi.yaml
    version: 1
  - path: docs/decisions/US-002-open-decisions.md
    version: 2
  - path: docs/designs/database/US-001-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 69
supersedes: null
---

# US-002 Database Design — Control Plane: Installation

Companion: `docs/designs/database/US-002-entity-model.md`.

## 1. Scope

- **Database:** the Control Plane database only (AD-1, PC-1).
- **Table introduced:** `installation`.
- **Table extended without schema change:** `audit_event` (US-001) — new
  `action` and `target_type` codes only; neither column has a check constraint
  (US-001 db-design §4), so no DDL is needed for them.
- **Not introduced:** `allowed_admin` (US-003), `instance_license_check`
  (US-005). No column for suspension history, deletion or a service-account key.

## 2. Conventions applied

| Rule | Application |
|---|---|
| PC-2 | One new EF Core migration `AddInstallation` in `src/ClassroomAgent.ControlPlane/Persistence/Migrations`; applied by deployment; no `EnsureCreated()` |
| PC-3 | Surrogate `id bigint` identity. The UUID `identifier` is a separate natural-key-like column with a unique index, not the primary key |
| PC-4 | Every property configured in `InstallationConfiguration`; every string has a max length |
| PC-5 | `snake_case`, singular; `pk_`, `uq_`, `ck_` names |
| PC-6 | `created_at`, `updated_at` `timestamptz` NOT NULL, stamped by `TimestampInterceptor` from the injected `TimeProvider` |
| PC-7 | Unique indexes on every lookup key: `identifier` (routes), `domain`, `client_id` (uniqueness checks) |
| PC-8 | No relationship in this Story (`allowed_admin` will reference `installation` in US-003) |
| PC-9 | No key, key reference or secret column; audit rows carry internal ids only |

### 2.1 Why the UUID is not the primary key

PC-3 fixes `id bigint` as every table's key. The UUID is the identifier the Owner
sees, puts into configuration and the service channel carries; it is an
external-facing identifier, like the Google ids of PC-3, so it gets its own
column and unique index. `audit_event.target_id` is `bigint` (US-001), so the
audit target is `installation.id` without changing `audit_event`. US-003's
foreign key from `allowed_admin` will also reference `installation.id`.

## 3. Table `installation`

The school as the Owner's unit of account (`trebovaniya.md` §3 "Installation",
v43, v69; spec FR-004, FR-008, FR-012).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_installation`; internal key; audit target id; never shown or routed |
| `identifier` | `uuid` | NOT NULL | — | `uq_installation_identifier`; random UUIDv4 set by the service at creation (`Guid.NewGuid()`, CSPRNG-backed); never updated |
| `name` | `varchar(200)` | NOT NULL | — | `ck_installation_name_length`; not unique (VR-001) |
| `domain` | `varchar(253)` | NOT NULL | — | `uq_installation_domain`; `ck_installation_domain_lower`; `ck_installation_domain_format`; never updated (BR-021) |
| `client_id` | `varchar(32)` | NOT NULL | — | `uq_installation_client_id`; `ck_installation_client_id_format` |
| `status` | `varchar(16)` | NOT NULL | — | `ck_installation_status`; `'active'` at creation |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6; **the installation's creation time** shown to the Owner (FR-001, FR-005, OD-003) |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6 |

PostgreSQL `varchar(n)` counts characters (code points), matching VR-001's
code-point length; `char_length` in the check below counts the same way.

### 3.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_installation` | `PRIMARY KEY (id)` | PC-3 |
| `uq_installation_identifier` | `UNIQUE (identifier)` | identifier unique (spec FR-004); route lookup |
| `uq_installation_domain` | `UNIQUE (domain)` | one installation per domain, any status (FR-008, S-04); concurrency guarantee |
| `uq_installation_client_id` | `UNIQUE (client_id)` | one installation per client ID (FR-008) |
| `ck_installation_name_length` | `char_length(name) BETWEEN 1 AND 200` | VR-001 lower bound (varchar only caps the upper) |
| `ck_installation_domain_lower` | `domain = lower(domain)` | stored lower case, so the plain unique index is case-insensitive in effect (VR-002, FR-008) |
| `ck_installation_domain_format` | `domain ~ '^[a-z0-9.-]{3,253}$' AND position('.' in domain) > 0` | last line of defence for the stored shape; the full label rules (OD-001) are enforced by request validation |
| `ck_installation_client_id_format` | `client_id ~ '^[0-9]{10,32}$'` | VR-003 |
| `ck_installation_status` | `status IN ('active', 'suspended')` | closed set (`trebovaniya.md` §3) |

The checks duplicate request validation on purpose: the unique indexes only
guarantee uniqueness if every stored domain is lower case, so that invariant is
held by the database. Name edge-whitespace and `Cc`/`Cf` rules and the domain
label rules stay in validation only — expressing Unicode categories and label
structure as SQL checks would be fragile and add nothing to the uniqueness
guarantee.

### 3.2 Indexes

Only the three unique indexes above (plus the primary key):

| Query | Index |
|---|---|
| detail/edit page by `{id}` UUID | `uq_installation_identifier` |
| "domain already registered?" | `uq_installation_domain` |
| "client ID already registered?" | `uq_installation_client_id` |
| list, ordered by name then domain | none — full scan and sort of ~10 rows (I-2); an index would not be used |

### 3.3 Immutability of identifier and domain

`identifier` and `domain` never change after insert (spec S-03, S-05).

1. **Entity** — no setter or method changes them after construction (entity
   model §2.1).
2. **Database** — trigger `trg_installation_immutable_columns`
   `BEFORE UPDATE ON installation FOR EACH ROW`, function
   `fn_installation_immutable_columns()`: raises an exception when
   `NEW.identifier IS DISTINCT FROM OLD.identifier` or
   `NEW.domain IS DISTINCT FROM OLD.domain`. Created and dropped with
   `migrationBuilder.Sql(...)` in `Up` / `Down`, like US-001's audit trigger.

**No delete.** No use case deletes an installation (`trebovaniya.md` §3, v69).
Enforced by the absence of any delete path in code and by trigger
`trg_installation_no_delete` `BEFORE DELETE ON installation FOR EACH ROW`,
function `fn_installation_no_delete()`, raising an exception. Decommissioning a
school is an operational procedure (DC) performed by the Owner outside the
application, which would drop the trigger deliberately if ever needed.

## 4. `audit_event` — values added by this Story

Unchanged schema (US-001 db-design §4). New codes:

| Event (spec FR-009) | `actor_type` | `actor_id` | `action` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|---|
| Installation created | `owner` | Owner id | `installation_created` | `installation` / `installation.id` | `succeeded` | NULL |
| Installation renamed | `owner` | Owner id | `installation_renamed` | `installation` / `installation.id` | `succeeded` | NULL |
| Installation client ID changed | `owner` | Owner id | `installation_client_id_changed` | `installation` / `installation.id` | `succeeded` | NULL |

No row for refusals (spec FR-009). No name, domain, client ID or UUID in any
column; `audit_event` has no column that could hold them (PC-9).

## 5. Transactions and concurrency

- **Create** — in one explicit transaction: insert `installation`, save (the
  identity `id` is generated), add the `installation_created` audit row with
  that id, save, commit. On `23505 unique_violation` from
  `uq_installation_domain` or `uq_installation_client_id`, roll back — neither
  row remains — and return the conflict result (spec FR-008).
- **Rename** / **client ID change** — in one explicit transaction: load the row,
  change the one column, add the audit row, save, commit. The client ID change
  maps `23505` on `uq_installation_client_id` to its conflict result the same way.
- **Which field conflicted.** PostgreSQL reports only the first violated unique
  constraint (`PostgresException.ConstraintName`). For a registration, after
  rollback the service re-runs the domain and client ID existence checks in a
  new query to report every conflicting field (API design §4 POST
  `/installations`). If that re-check finds neither (the other transaction rolled
  back meanwhile), it reports the field named by `ConstraintName`.
- **Isolation** — `READ COMMITTED` (PostgreSQL default); the unique indexes, not
  isolation, provide the guarantee. No explicit lock.
- **No optimistic concurrency token.** A rename and a client ID change touch
  different columns and EF Core updates only modified columns, so they cannot
  overwrite each other. Two renames of the same installation are last-write-wins,
  each audited; with one Owner this is acceptable and is not required otherwise
  by the Specification.

## 6. Sensitive data

| Data | Stored? | Rule |
|---|---|---|
| School name, domain | `installation.name`, `.domain` | not personal data; never in `audit_event` or logs (SC-10, spec FR-014) |
| Service-account client ID | `installation.client_id` | public identifier (glossary), not a secret; never in audit or logs |
| Service-account key or its reference | **no** | never in any Control Plane or installation table (PC-9, SC-7) — a column for it is a Critical finding |
| Installation UUID | `installation.identifier` | not a secret (network isolation protects the channel, SC-9); not written to audit rows (internal id used instead) |

## 7. Migration

- **Name:** `AddInstallation`.
- **Command:**
  `dotnet ef migrations add AddInstallation --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations`.
- **Up:** create `installation` with §3 columns, constraints and indexes; create
  `fn_installation_immutable_columns` + `trg_installation_immutable_columns`
  and `fn_installation_no_delete` + `trg_installation_no_delete`.
- **Down:** drop both triggers and functions, then the table.
- **Effect on an existing Control Plane database** (US-001 applied): one new empty
  table; `owner` and `audit_event` untouched.
- **Backward compatibility:** additive; no existing column changes. Existing
  audit rows keep their codes.

## 8. Test-relevant facts

- Integration tests apply both migrations to a Testcontainers PostgreSQL
  database (TC-2).
- Two concurrent registrations with the same domain (in different letter case —
  the service lower-cases before insert) or the same client ID: one
  `installation` row, one `installation_created` row, the other request `409`
  (AC-004).
- Direct inserts must fail for: duplicate `domain`, duplicate `client_id`,
  duplicate `identifier`, upper-case `domain`, `client_id` with a non-digit or
  9/33 digits, `status` outside the set, empty `name`.
- Direct `UPDATE` of `identifier` or `domain`, and any `DELETE`, must fail
  (triggers).
- `created_at` is controlled through the fake `TimeProvider`, so the displayed
  `dd.MM.yyyy HH:mm UTC` value is assertable.

## 9. Rationale for choices not fixed upstream

- **`created_at` doubles as the business creation time.** `trebovaniya.md` §3
  names "дата создания"; it is set once, in UTC, by the interceptor from the
  injectable clock and never updated — exactly PC-6's `created_at`. A second
  column would always hold the same value. (Contrast `audit_event.occurred_at`,
  which exists because the event time is set by the service.)
- **Triggers for identifier/domain/no-delete** mirror US-001's audit
  immutability: the rule "never changes" is a stated requirement, and the
  database is the only layer that also stops `ExecuteUpdate`/`ExecuteDelete` or
  hand-written SQL.
- **No check constraint on `audit_event.action` / `target_type`** — kept as in
  US-001, so this Story needs no DDL on `audit_event`.
