---
artifact_type: database_design
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
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 73
supersedes: null
---

# US-005 Database Design — InstanceLicenseCheck and LegitimacyState

Companion: `docs/designs/database/US-005-entity-model.md`.

## 1. Scope

Two databases, never one (AD-1, PC-1):

| Database | Change |
|---|---|
| **Control Plane** | new table `instance_license_check`; `installation` referenced, unchanged; `audit_event` untouched (checks are not audited, S-14) |
| **Installation** (new, one per school) | first migration; one table `legitimacy_state` |

**Not introduced:** a history of checks; a "last check result" or "previous mode"
column (spec I-3 — process memory only); `audit_event`, `app_user` or any other
installation table (their Stories create them); any key, secret or configuration
value (PC-9, spec FR-001).

## 2. Conventions applied

| Rule | Application |
|---|---|
| PC-1 | Installation database is its own PostgreSQL database; connection string `ConnectionStrings:Installation` (API design §10) |
| PC-2 | Control Plane migration `AddInstanceLicenseCheck`; installation migration `InitialLegitimacyState` under `src/ClassroomAgent.Infrastructure/Persistence/Migrations/`; both applied by deployment, never at startup; no `EnsureCreated()` |
| PC-3 | Surrogate `id bigint` identity on both tables |
| PC-4 | Every property configured in an entity configuration class; every string has a max length |
| PC-5 | `snake_case`, singular; `pk_`, `fk_`, `uq_`, `ck_` names |
| PC-6 | `created_at`, `updated_at` `timestamptz` NOT NULL, stamped by a `TimestampInterceptor` from the injected `TimeProvider` — the Control Plane's existing one, and a new one in the installation's `Infrastructure/Persistence` |
| PC-7 | `uq_instance_license_check_installation_id` serves the FK and the lookup; `legitimacy_state` has one row |
| PC-8 | `instance_license_check` → `installation` one-to-zero-or-one, `ON DELETE RESTRICT` |
| PC-9 | No personal data, no secrets. Domain and client ID are not personal data (spec Notes) but are kept out of logs (SC-10) |

## 3. Control Plane — table `instance_license_check`

The last legitimacy check of one `Installation` (`trebovaniya.md` §3, §9 v73;
spec FR-006; API design §5 step 4).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_instance_license_check`; internal; never shown |
| `installation_id` | `bigint` | NOT NULL | — | `uq_instance_license_check_installation_id`; `fk_instance_license_check_installation` → `installation.id`, `ON DELETE RESTRICT` |
| `answered_at` | `timestamptz` | NOT NULL | — | UTC instant the Control Plane answered; shown on the detail page (FR-011) |
| `application_version` | `varchar(20)` | NOT NULL | — | as reported; `ck_instance_license_check_application_version` |
| `contract_version` | `integer` | NOT NULL | — | as reported; `ck_instance_license_check_contract_version` |
| `answered_status` | `varchar(16)` | NOT NULL | — | `ck_instance_license_check_answered_status` |
| `answered_compatibility` | `varchar(24)` | NOT NULL | — | `ck_instance_license_check_answered_compatibility` |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6; first call |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6; last replacement |

### 3.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_instance_license_check` | `PRIMARY KEY (id)` | PC-3 |
| `uq_instance_license_check_installation_id` | `UNIQUE (installation_id)` | one record per installation (FR-006); concurrency guarantee; conflict mapping by this name |
| `fk_instance_license_check_installation` | `FOREIGN KEY (installation_id) REFERENCES installation (id) ON DELETE RESTRICT` | a check belongs to a registered installation; installations are never deleted (US-002) |
| `ck_instance_license_check_application_version` | `application_version ~ '^(0\|[1-9][0-9]{0,5})\.(0\|[1-9][0-9]{0,5})\.(0\|[1-9][0-9]{0,5})$'` | stored shape = VR-002 (written in SQL as `'^(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})$'`) |
| `ck_instance_license_check_contract_version` | `contract_version BETWEEN 1 AND 999999` | VR-002 |
| `ck_instance_license_check_answered_status` | `answered_status IN ('active', 'suspended')` | same vocabulary as `ck_installation_status` |
| `ck_instance_license_check_answered_compatibility` | `answered_compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')` | FR-005 |

No trigger: the row is replaced in place by design; delete is not used by any
use case, and the FK `RESTRICT` plus the no-delete trigger on `installation`
keep it from being orphaned.

### 3.2 Write — replace the last check

In `ControlPlane.Services`, after the installation was found and compatibility
computed (API design §5 steps 2–4). One `SaveChangesAsync` per attempt, so each
attempt is atomic; no explicit transaction is needed because no other row changes.

1. Load the `instance_license_check` row where `installation_id = @id` (tracked).
2. **None** → add a new row with the call's values; save.
   - On `23505 unique_violation` with `ConstraintName =
     uq_instance_license_check_installation_id` (a concurrent first call inserted
     it): detach the added entity, go to step 3 once.
3. **Exists** → set `answered_at`, `application_version`, `contract_version`,
   `answered_status`, `answered_compatibility`; save. `updated_at` is stamped by
   the interceptor.
4. Any other exception, or a second unique violation, propagates (`500`).

Concurrency: two calls both updating the same row — `READ COMMITTED`, no
concurrency token; each `UPDATE` sets all five columns, so the row ends with one
call's complete values (last committed wins, spec I-14). Two first calls — one
insert wins, the other hits the unique index and updates. Neither is `500`.

`answered_at` is taken from `TimeProvider.GetUtcNow()` by the service (a business
value), separately from the interceptor's `updated_at`; in practice they are equal.

`answered_status` is the installation's `status` read in step 2 of the endpoint;
if US-004 changes it concurrently, the answer and the record both carry the value
read — consistent with each other.

### 3.3 Read

`InstallationRegistry.GetAsync` (detail page) left-joins or separately loads the
row by `installation_id`; none → `LastCheck = null` (API design §9).

## 4. Installation database — table `legitimacy_state`

The installation's own record of its legitimacy (`trebovaniya.md` §3 v46, v53,
v54, v73; spec FR-007, FR-009).

| Column | Type | Null | Default | Constraint / note |
|---|---|---|---|---|
| `id` | `bigint` | NOT NULL | identity | `pk_legitimacy_state` |
| `singleton` | `boolean` | NOT NULL | `true` | `uq_legitimacy_state_singleton`, `ck_legitimacy_state_singleton` — at most one row |
| `last_successful_check_at` | `timestamptz` | NULL | — | NULL until a check succeeds (a row may exist after an `upgrade_required` answer only, spec FR-008 last bullet) |
| `status` | `varchar(16)` | NOT NULL | — | last known `Installation` status; `ck_legitimacy_state_status` |
| `compatibility` | `varchar(24)` | NOT NULL | — | last compatibility state; `ck_legitimacy_state_compatibility` |
| `domain` | `varchar(253)` | NOT NULL | — | `Installation` domain from the last answer; `ck_legitimacy_state_domain_length` |
| `client_id` | `varchar(32)` | NOT NULL | — | `Installation` client ID from the last answer; `ck_legitimacy_state_client_id_format` |
| `created_at` | `timestamptz` | NOT NULL | — | PC-6 |
| `updated_at` | `timestamptz` | NOT NULL | — | PC-6 |

No column for the last check result, the failure category or the previous mode
(spec I-3); no installation id (it is configuration, FR-001).

### 4.1 Constraints

| Name | Definition | Why |
|---|---|---|
| `pk_legitimacy_state` | `PRIMARY KEY (id)` | PC-3 |
| `uq_legitimacy_state_singleton` | `UNIQUE (singleton)` | with the check below: exactly zero or one row (FR-009, AC-011) |
| `ck_legitimacy_state_singleton` | `singleton` (i.e. `singleton = true`) | the only allowed value |
| `ck_legitimacy_state_status` | `status IN ('active', 'suspended')` | VR-003 |
| `ck_legitimacy_state_compatibility` | `compatibility IN ('supported', 'upgrade_recommended', 'upgrade_required')` | VR-003 |
| `ck_legitimacy_state_domain_length` | `char_length(domain) BETWEEN 3 AND 253` | VR-003 |
| `ck_legitimacy_state_client_id_format` | `client_id ~ '^[0-9]{10,32}$'` | VR-003 |

The answer is validated in the client first (VR-003 → "unparseable answer"); the
constraints are the last line of defence. A constraint failure on save is the
"save fails" case of spec FR-007: unsuccessful, `Error` log, state as before.

No trigger: the row is updated in place on every recording check; no use case
deletes it.

### 4.2 Write — record a check result

In the check use case (`Application`), through a repository in
`Infrastructure/Persistence/Repositories` that stages changes; the use case saves
(package-map: repositories never call `SaveChangesAsync`).

| Result | Change |
|---|---|
| successful | `last_successful_check_at` = completion time from `TimeProvider`; `status`, `compatibility`, `domain`, `client_id` = answer |
| `upgrade_required` answer | `status`, `compatibility`, `domain`, `client_id` = answer; `last_successful_check_at` unchanged (NULL on a new row) |
| any other unsuccessful category | no write |

1. Load the single row (tracked).
2. None → add one; exists → set the fields above.
3. Save. On `23505` with `ConstraintName = uq_legitimacy_state_singleton` (cannot
   happen with one check at a time, FR-003; defensive): detach, reload, apply
   step 2 as an update, save once more.
4. Any other failure → the use case treats the check as unsuccessful (FR-007).

Writing this table is on the BR-026 closed list: no read-only guard applies to it
(AC-011).

### 4.3 Read

The read-only query (spec FR-008) and readiness (FR-013) read the single row
untracked (`AsNoTracking`); no row = "never confirmed". Readiness also uses a
successful read as its database probe: a connection failure → `Unhealthy`.

## 5. Sensitive data

| Data | Where | Rule |
|---|---|---|
| Installation domain | `installation.domain` (existing), `legitimacy_state.domain` | not personal data; never in logs (SC-10) |
| Service-account client ID | `installation.client_id` (existing), `legitimacy_state.client_id` | not a secret (it is shown to the school's super-admin, US-010); never in logs |
| Versions, status, compatibility | both new tables | operational data |
| Service-account key, key reference, connection string, Control Plane address | **nowhere in any database** | PC-9, DC-3; a column for any of them is a Critical finding |

Neither table holds personal data; neither is subject to the retention purge
(PC-11 covers teaching data, accounts and audit rows).

## 6. Migrations

### 6.1 Control Plane — `AddInstanceLicenseCheck`

- **Command:**
  `dotnet ef migrations add AddInstanceLicenseCheck --project src/ClassroomAgent.ControlPlane --startup-project src/ClassroomAgent.ControlPlane --output-dir Persistence/Migrations`.
- **Up:** create `instance_license_check` with §3 columns and constraints.
- **Down:** drop the table.
- **Effect:** one new empty table; `owner`, `installation`, `allowed_admin`,
  `audit_event` untouched. Additive.

### 6.2 Installation — `InitialLegitimacyState`

- **Command** (AGENTS.md command table):
  `dotnet ef migrations add InitialLegitimacyState --project src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web --output-dir Persistence/Migrations`.
- **Design-time context:** the Web host refuses to start without its required
  settings (FR-001), so `Infrastructure/Persistence` provides an
  `IDesignTimeDbContextFactory` for the installation `DbContext` reading only a
  connection string — as the Control Plane's
  `DesignTimeControlPlaneDbContextFactory` does. It is never used at runtime.
- **Up:** create `legitimacy_state` with §4 columns and constraints.
- **Down:** drop the table.
- **Effect:** the first schema of every installation database. Creates no row.

## 7. Test-relevant facts

- Control Plane integration tests apply four migrations; migration tests of
  US-001 … US-003 that enumerate tables or migrations must account for
  `instance_license_check` / `AddInstanceLicenseCheck`.
- Installation integration tests run on their own Testcontainers database with
  `InitialLegitimacyState` applied (TC-2); a migration test asserts the table and
  its constraints exist and no row is created.
- Direct inserts must fail:
  - `instance_license_check`: a second row for one `installation_id`; unknown
    `installation_id` (FK); `application_version` `1.0`, `01.0.0`, `1.0.0-rc1`;
    `contract_version` 0; status or compatibility outside the sets;
  - `legitimacy_state`: a second row; `singleton = false`; status or compatibility
    outside the sets; domain of 2 characters; client ID with a letter or 9 digits.
- Two concurrent first checks of one installation in the Control Plane: one
  `instance_license_check` row, both answered `200` (AC-005, I-14).
- `answered_at`, `last_successful_check_at`, `created_at`, `updated_at` are driven
  by the fake `TimeProvider`; the grace-period tests (2 days, exactly 7 days,
  8 days — AC-009, AC-011) set `last_successful_check_at` through a recorded check
  or a direct insert.
- AC-011 restart: a new host on the same database reads the row; nothing in the
  table depends on process state.
- Readiness `Unhealthy`: the test stops or points away from the database; the
  table itself is not involved.
