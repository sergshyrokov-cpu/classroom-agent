---
artifact_type: database_design
story: US-006
version: 1
status: DRAFT
created_at: 2026-09-17T15:44:28Z
updated_at: 2026-09-17T15:44:28Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-006-spec.md
    version: 2
  - path: docs/designs/api/US-006-api-design.md
    version: 1
  - path: docs/designs/api/US-006-openapi.yaml
    version: 1
  - path: docs/decisions/US-006-open-decisions.md
    version: 2
  - path: docs/designs/database/US-002-db-design.md
    version: 1
  - path: docs/designs/database/US-004-db-design.md
    version: 1
  - path: docs/designs/database/US-005-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-006 Database Design — Control Plane: push address

Companion: `US-006-entity-model.md`.

## 1. Scope

- **Control Plane database:** one new nullable column `installation.push_address`
  with its check constraint, one new audit action code, and the rule that the
  status change reads the address in its own transaction (spec FR-002, FR-003,
  FR-005). One migration: `AddInstallationPushAddress`.
- **Installation database:** **no change.** The push receiver writes nothing; the
  check it triggers writes `LegitimacyState` exactly as US-005 already designed
  (spec FR-009, FR-012). No migration on that side.
- Push attempts, retries and results are **not persisted** on either side
  (`trebovaniya.md` §9 v76, spec FR-006, FR-007) — they live in memory and are
  lost on restart. The pending check of v77 is in-process state in the
  installation, not a row.

## 2. Conventions applied

- PC-1 PostgreSQL, two databases, never shared.
- PC-2 every schema change ships as an EF Core migration in this Story; no
  `EnsureCreated`.
- PC-5 snake_case names via `EFCore.NamingConventions`; explicit index and
  constraint names.
- PC-6 `updated_at` maintained by `TimestampInterceptor` for tracked updates.
- SC-10 / SC-11 the push address never reaches `audit_event` or a log line.
- Explicit lengths, nullability and check constraints — no EF Core convention
  defaults (AGENTS.md, PC-5).

## 3. Control Plane schema change

### 3.1 Column `installation.push_address`

| Property | Value |
|---|---|
| Name | `push_address` |
| Type | `character varying(255)` |
| Nullable | **yes** — NULL means "not set" (spec FR-002, `trebovaniya.md` §3 v76) |
| Default | none (an existing row gets NULL) |
| Unique | **no** — several schools may share an address behind a proxy (v76) |
| Indexed | no — never a query predicate; rows are found by `identifier` or `id` |

Check constraint `ck_installation_push_address_format`:

```sql
push_address IS NULL
OR push_address ~ '^http://(\[[0-9a-f:.]+\]|[a-z0-9.-]{1,253}):[1-9][0-9]{0,4}$'
```

- Enforces the canonical form the application stores (api-design §7): `http://`,
  lower-case host (DNS name, IPv4 literal, or bracketed IPv6), explicit port
  without leading zeros, no trailing slash, no path, query, fragment or user info.
- The finer rules — length ≤ 255 (the column), port ≤ 65535, label lengths, no
  hyphen at a label edge — stay in application validation (VR-001): a regular
  expression for them would be unreadable and the column is written only through
  that validation. The constraint is the backstop against a value in another
  shape, in the spirit of `ck_installation_domain_format`.
- Empty string is impossible: the pattern requires a host and a port; the
  application stores NULL for "not set".

### 3.2 Unchanged parts of `installation`

- `identifier` and `domain` stay immutable
  (`trg_installation_immutable_columns`); the trigger is **not** extended:
  `push_address` is changeable at any status (v76).
- `trg_installation_no_delete` unchanged.
- `uq_installation_identifier`, `uq_installation_domain`,
  `uq_installation_client_id` unchanged.
- `ck_installation_status`, `ck_installation_name_length`,
  `ck_installation_domain_*`, `ck_installation_client_id_format` unchanged.

### 3.3 `audit_event.action` — one new code

| Code | Written when | actor | target | outcome | refusal_category |
|---|---|---|---|---|---|
| `installation_push_address_changed` | the canonical push address changes, including set from NULL and cleared to NULL | `owner` / Owner account `id` | `installation` / `installation.id` | `succeeded` | NULL |

- One code for set, change and clear (spec I-4): the row may carry neither the old
  nor the new value, so separate codes would only reveal which side was empty.
- Registration with an address writes only `installation_created` (spec I-5).
- No `refused` row: validation failures, `404` and antiforgery refusals are not
  audited, as in US-002.
- As in US-001 … US-005 there is no database constraint on action codes; the
  closed set is the C# enum plus the converter.

## 4. Write paths and transactions

### 4.1 Register with a push address (spec FR-002, FR-003)

US-002 step 5 unchanged, with `push_address` set to the canonical value or NULL in
the same `INSERT`, inside the same transaction as the `installation_created` audit
row. Conflicts on domain or client ID behave exactly as US-002; the address takes
part in no uniqueness check.

### 4.2 Change the push address (spec FR-002, FR-003)

One explicit transaction, tracked entity (the pattern of `RenameAsync`, US-002
§5):

1. Load the installation by `identifier` → none: result `NotFound`, nothing
   written.
2. Compare the canonical candidate (NULL for empty input) with the stored value,
   ordinally → equal: result `Unchanged`, nothing written, no transaction needed.
3. Begin transaction → `installation.ChangePushAddress(value)` → add the
   `installation_push_address_changed` audit row → `SaveChangesAsync` → commit →
   result `Changed`. `updated_at` is stamped by `TimestampInterceptor` (PC-6).

Concurrency: two Owner sessions changing the address at once are serialized by the
row lock of the second `UPDATE`; both are real changes relative to what each read,
so two audit rows and the last value win. No optimistic token is introduced (the
table has none, US-004 §5.3) and `409` is not part of this flow — nothing here is
unique.

### 4.3 Status change reads the address (spec FR-005, I-6)

US-004 §5.1 stays exactly as it is — the conditional set-based `UPDATE`, then the
audit row, in one transaction. This Story adds one read **inside that
transaction**, after an affected-row count of 1:

```sql
SELECT push_address FROM installation WHERE id = @id;
```

- It is executed while the transaction still holds the row lock taken by the
  `UPDATE`, so it returns the address as of the moment the status change commits:
  a concurrent address change either committed before (and is seen) or waits for
  the lock (and is not).
- `ExecuteUpdateAsync` returns only a count, so the value cannot come back from
  the `UPDATE` itself; a second statement in the same transaction is the simplest
  correct way and costs one indexed lookup by primary key.
- The value is handed to the in-memory sender **after commit** (api-design §6).
  Nothing about the push is written to the database, so no audit row, no column
  and no table records delivery.
- Results `Unchanged` and `NotFound` read nothing and enqueue nothing.

### 4.4 Installation database

No write path is added. The push receiver writes nothing; the triggered check uses
the US-005 `LegitimacyState` upsert, which is on the BR-026 read-only closed list.

## 5. Migration

**One Control Plane migration: `AddInstallationPushAddress`.**

- `ALTER TABLE installation ADD COLUMN push_address character varying(255) NULL;`
- `ALTER TABLE installation ADD CONSTRAINT ck_installation_push_address_format
  CHECK (...);` (§3.1)
- Down: drop the constraint, drop the column.
- No trigger, index or function changes; no data migration (existing rows keep
  NULL, which is a valid "not set").
- The new `AuditAction` member needs no migration: converter lambdas are not part
  of the model snapshot (US-004 §6).
- Migration list becomes `InitialOwnerAndAudit`, `AddInstallation`,
  `AddAllowedAdmin`, `AddInstanceLicenseCheck`, `AddInstallationPushAddress` —
  `MigrationTests` must expect the fifth entry.
- **No installation-database migration**; that list stays
  `InitialLegitimacyState`.

## 6. Sensitive data

- The push address is not personal data, but it is infrastructure detail: it is
  never written to `audit_event`, never logged (SC-10, SC-11), and appears only on
  the Owner's own pages, HTML-encoded.
- No credential, token or secret is added anywhere by this Story.
- Nothing about push delivery — attempt counts, categories, timestamps — is
  persisted, so no new retention obligation arises (PC-11 untouched).

## 7. Test-relevant facts

- Column exists, is nullable, `varchar(255)`, has no unique index; two
  installations may hold the same value.
- `ck_installation_push_address_format` rejects, by direct SQL:
  `https://h:1`, `http://H:1` (upper case), `http://h` (no port), `http://h:1/p`,
  `http://h:01`, `''`; accepts `http://10.0.0.5:8081`,
  `http://school-a.private:8081`, `http://[fd00::5]:8081`, NULL.
- Registration without an address stores NULL; with one, the canonical value; one
  `installation_created` row either way.
- Set / change / clear: exactly one `installation_push_address_changed` row per
  change, in the same transaction, `target_type = 'installation'`,
  `target_id = installation.id`, `outcome = 'succeeded'`, and the row contains no
  address; `updated_at` advances; `identifier`, `domain`, `status`, `client_id`,
  `name`, `created_at` unchanged; `allowed_admin` rows and
  `instance_license_check` untouched.
- Unchanged submission (same canonical value, or empty while NULL) writes nothing
  and leaves `updated_at` as it was.
- A suspended installation accepts an address change; the status stays
  `suspended`.
- Status change: after `Changed`, the address read in the transaction equals the
  committed value; `Unchanged`/`NotFound` read nothing. The audit rows of US-004
  are unaffected.
- Migration test: five Control Plane migrations, applied from empty and idempotent
  on re-run; the installation database still has one.
- The `trg_installation_immutable_columns` trigger still rejects a domain or
  identifier update, and now accepts a `push_address` update.

## 8. Rationale for choices not fixed upstream

- **Column on `installation`, not a separate table.** It is a single optional
  attribute of the school record (`trebovaniya.md` §3 v76), with no history —
  history would be a persistence feature nobody asked for, and the audit row is
  the trace.
- **No unique constraint.** v76 explicitly allows two schools behind one proxy
  address.
- **NULL, not empty string,** for "not set": it keeps the check constraint simple
  and matches the nullable DTO and the "clear it" flow.
- **Check constraint on shape only.** Deep syntax validation belongs in the
  application (VR-001); the constraint prevents a malformed value entering by any
  other route, exactly as the domain constraint does.
- **Read-after-update instead of `UPDATE … RETURNING`.** EF Core's
  `ExecuteUpdateAsync` cannot return columns; raw SQL for this one value would
  cost more than an indexed re-read inside the same transaction.
- **No persisted push queue.** v76 decided retries live in memory and the periodic
  check is the guarantee; a table would add a retention and cleanup obligation for
  data the Owner never sees.
