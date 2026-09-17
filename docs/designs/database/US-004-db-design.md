---
artifact_type: database_design
story: US-004
version: 1
status: DRAFT
created_at: 2026-09-17T10:59:26Z
updated_at: 2026-09-17T10:59:26Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-004-spec.md
    version: 1
  - path: docs/designs/api/US-004-api-design.md
    version: 1
  - path: docs/designs/api/US-004-openapi.yaml
    version: 1
  - path: docs/decisions/US-004-open-decisions.md
    version: 1
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-003-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 71
supersedes: null
---

# US-004 Database Design — Control Plane: Installation status change

Control Plane database only (AD-1, PC-1). Companion: `US-004-entity-model.md`.

## 1. Scope

- Changing `installation.status` between `active` and `suspended` (spec FR-003).
- Two new audit action codes (spec FR-006).
- **No schema change: no new table, column, constraint, index or trigger, and no
  migration.** The verdict is PASS, not `NOT_APPLICABLE`: the Story changes
  persistence behaviour (a new write path with concurrency and audit rules) even
  though the schema stays as US-002 and US-003 left it.

## 2. Conventions applied

| Rule | Application |
|---|---|
| PC-1 | PostgreSQL via Npgsql, Control Plane database only |
| PC-2 | Schema only through migrations; none is needed (§6) |
| PC-6 | `updated_at` set on every update of `installation`, including the set-based update of §4 |
| PC-9 | Audit rows inserted only, in the transaction of the change they record |
| SC-11 | Audit row carries internal ids and codes only — no name or domain |

## 3. Existing schema used

### 3.1 Table `installation` (US-002 db-design §3)

| Column | Use in this Story |
|---|---|
| `id` (`bigint`) | conditional update key; audit `target_id` |
| `identifier` (`uuid`) | route lookup `{id}` |
| `name`, `domain` | read for the confirmation pages; never written |
| `client_id`, `created_at` | never written |
| `status` (`varchar(16)`, `ck_installation_status IN ('active','suspended')`) | **the only business column written** |
| `updated_at` (`timestamptz`) | set to the current UTC instant on a change |

- `trg_installation_immutable_columns` rejects changes to `identifier` and
  `domain` only; updating `status` and `updated_at` passes it.
- `trg_installation_no_delete` is not touched: nothing is deleted.
- No reason and no status-change time column is added (`trebovaniya.md` §3, v71).
  `updated_at` is the PC-6 technical timestamp of *any* update (a rename also sets
  it), so it is not a status-change time and is never displayed (spec I-6).

### 3.2 Table `allowed_admin` (US-003)

Not read or written by a status change. Its FK `fk_allowed_admin_installation`
(Restrict) and domain trigger are unaffected because `installation.id` and
`installation.domain` never change.

### 3.3 Table `audit_event` (US-001 db-design §4)

`action` is `varchar(64)` with **no check constraint listing codes** (only
`actor_type`, actor/target pairing, `outcome` and refusal category are
constrained). New codes therefore need no migration.

## 4. Values added by this Story

### 4.1 `audit_event.action`

| Code | Written when | `actor_type` / `actor_id` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|
| `installation_suspended` | status changed `active` → `suspended` | `owner` / Owner account `id` | `installation` / `installation.id` | `succeeded` | NULL |
| `installation_resumed` | status changed `suspended` → `active` | `owner` / Owner account `id` | `installation` / `installation.id` | `succeeded` | NULL |

- `target_id` is the internal `bigint` key, as for `installation_renamed`
  (US-002 decision); the UUID identifier is not stored in audit.
- `request_id` is the request identifier, as for every Control Plane row.
- No `refused` row exists for these actions: "unchanged", `404` and antiforgery
  refusals are not audited (spec FR-006).

### 4.2 No new status value

`ck_installation_status` already allows exactly the two values used.

## 5. Transactions and concurrency

### 5.1 The status change

One explicit transaction per POST (default isolation `READ COMMITTED`):

1. Look up the installation by `identifier` → `id` (no row → service result
   `NotFound`, nothing else happens; the lookup may run before the transaction).
2. Conditional set-based update:

   ```sql
   UPDATE installation
      SET status = @target, updated_at = @now
    WHERE id = @id AND status = @expected;
   ```

   suspend: `@expected = 'active'`, `@target = 'suspended'`;
   resume: `@expected = 'suspended'`, `@target = 'active'`.
   In EF Core: `ExecuteUpdateAsync` with both `SetProperty` calls — the
   `TimestampInterceptor` does not see set-based updates, so `updated_at` is set
   explicitly in the same statement (PC-6).
3. Affected rows = 1 → insert the audit row (§4.1) and commit → result `Changed`.
   Affected rows = 0 → insert nothing, commit (or roll back; nothing was written)
   → result `Unchanged`.

The installation must exist at step 2 because rows are never deleted
(`trg_installation_no_delete`), so 0 affected rows can only mean "status already
the target".

### 5.2 Why this is race-free (spec FR-005, S-04)

- PostgreSQL takes a row lock for the `UPDATE`. A concurrent `UPDATE` of the same
  row waits; under `READ COMMITTED` it then re-evaluates its `WHERE` against the
  committed row version.
- **Two suspends:** the first changes 1 row and writes its audit row; the second
  re-evaluates `status = 'active'` as false, changes 0 rows, writes nothing.
- **Suspend and resume:** each applies only if the committed status is its
  precondition when it gets the lock; every change writes exactly one audit row
  in its own transaction.
- The audit insert happens after the update in the same transaction while the row
  lock is held, so an audit row can never exist without its change, or a change
  without its row.
- No unique-constraint or serialization error is expected, so no retry and no
  `409`/`500` mapping is needed.

### 5.3 Not used

- A read-then-`SaveChanges` with a tracked entity would need an optimistic
  concurrency token; `installation` has none and adding `xmin` mapping would be a
  model change for no benefit over §5.1.
- `SERIALIZABLE` isolation is not needed and would add retry handling.

## 6. Migration

**None.** Checked against the model:

- no entity, property, column type, constraint, index or trigger changes;
- the new `AuditAction` members are mapped to strings by the existing value
  converter in `AuditEventConfiguration`; converter lambdas are not part of the
  EF Core model snapshot, so `dotnet ef migrations add` would produce an empty
  migration — it must not be added.
- The migration list stays `InitialOwnerAndAudit`, `AddInstallation`,
  `AddAllowedAdmin`; existing migration tests keep their expectation.

## 7. Sensitive data

- No personal data is involved. The installation's name and domain are read for
  display only and are never written to `audit_event` or logs (SC-10, SC-11).
- Nothing is stored about the reason for suspending (v71).

## 8. Test-relevant facts

- After a suspend: `status = 'suspended'`; `identifier`, `name`, `domain`,
  `client_id`, `created_at` equal to before; `updated_at` ≥ before; the
  installation's `allowed_admin` rows unchanged (count and content).
- After a resume: `status = 'active'`, the same invariants.
- Audit: exactly one row per change with the §4.1 values; zero rows after GET,
  cancel, `400`, `404`, "unchanged".
- Concurrency tests run against real PostgreSQL (TC-2): N parallel suspends of
  one active installation → final `suspended`, exactly one
  `installation_suspended` row; parallel suspend + resume → number of rows equals
  the number of changes, each row's action consistent with a valid alternation.
- The suspended-installation setup of US-003 tests (status set directly in the
  test database) remains valid.
- A direct `UPDATE installation SET domain = …` still fails (unchanged trigger) —
  no new test needed; a status update must not fail.

## 9. Rationale for choices not fixed upstream

- **Set-based conditional update over tracked entity:** the decision "changed or
  unchanged" is atomic in one statement, which is exactly what spec FR-003 step 3
  and FR-005 ask for, and it needs no schema change.
- **No database constraint on action codes:** consistent with US-001 … US-003;
  the closed set is enforced by the C# enum and converter.
- **No status history table:** v71 keeps the audit row as the only trace.
