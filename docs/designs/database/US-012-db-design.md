---
artifact_type: database_design
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
  - path: docs/decisions/US-012-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-012 Database Design — Create and manage Dean accounts

## 1. Summary of the schema change

| Table | Change |
|---|---|
| `app_user` | **one new column** `password_is_temporary`, plus one new check constraint tying it to a password account |
| `audit_event` | **three check constraints amended**: the action list, the target-type list and the refusal-category list |
| everything else | untouched |

No new table. No new index. No new relationship. The Control Plane database is
not touched at all.

**One migration** is required: `AddDeanAccounts`. It carries the column, its
constraint and the three amended audit constraints, which is the same shape
US-009 (`AddWorkspaceConnection`) and US-011 (`AddAccessCheckAudit`) used (PC-2).
Installation migrations go from **four to five**; the installation still has five
tables.

## 2. Why the Story needs so little

Everything the Specification asks of an account except one fact already exists
on `app_user`, because US-008 designed the table for both roles and left the
Dean's fields unused:

| Specification needs | Column that already holds it |
|---|---|
| the login, which is the work email (FR-002, FR-004) | `email` / `normalized_email`, lower-cased by `ck_app_user_email_lowercase`, unique by `uq_app_user_normalized_email` |
| the password, as a hash only (FR-018) | `password_hash`, nullable, non-null exactly when `sign_in_method = 'password'` |
| the disabled state (FR-007, FR-008) | `is_disabled` |
| the failed-attempt counter and the lockout (FR-013) | `access_failed_count`, `lockout_end` |
| session invalidation on a state change (FR-019) | `security_stamp` |
| the retention clock and the list column (FR-011, PC-11) | `last_successful_sign_in_at` |
| the account's language (I-9) | `ui_language` |
| role and sign-in method (FR-002) | `role`, `sign_in_method`, tied by `ck_app_user_role_sign_in_method` |

The one fact with nowhere to live is whether the stored password is still the
temporary one an Admin typed (FR-006, I-2).

## 3. `app_user` — the change

### 3.1 The new column

| Column | Type | Null | Default | Meaning |
|---|---|---|---|---|
| `password_is_temporary` | `boolean` | NOT NULL | `false` | The stored hash is of a password an Admin set at creation or at a reset; the next successful authentication leads to the forced change (FR-006). Cleared when the Dean sets their own password. |

- **A boolean, not a timestamp or a second hash.** The Specification's I-2 fixes
  this: the temporary password is a *state* of the one stored hash, never a
  second stored secret. A `temporary_password_hash` column would be a second
  password to leak.
- **`NOT NULL` with a `false` default**, so the migration backfills every
  existing row without a data step: the only rows today are Admins, and an Admin
  has no password at all.
- No timestamp is added. Nothing in `trebovaniya.md` or the Specification gives a
  temporary password an expiry; adding `temporary_password_expires_at` would be
  a rule nobody wrote (BR-014 knows only "must be changed at the next sign-in").

### 3.2 The new check constraint

```sql
ck_app_user_password_temporary:
    password_is_temporary = false OR sign_in_method = 'password'
```

An Admin row can never carry a temporary password. This is the same defence in
depth as the two US-008 constraints it joins: `ck_app_user_role_sign_in_method`
and `ck_app_user_password_hash` already make an Admin with a password hash
impossible **in the database**, and SC-2 calls an Admin password a Critical
finding. Without this constraint, a bug that set the flag on an Admin row would
leave an account that Identity's sign-in path would never repair.

No constraint requires a Dean's password to be temporary or not — both states
are legitimate for a Dean at all times.

### 3.3 What is deliberately not added

- **No index.** The only lookups are by `normalized_email` (the unique index
  already there, used by sign-in and by the uniqueness check of VR-001) and the
  full list of Deans, which OD-003 leaves unpaginated because a school has a
  handful of them. An index on `role` would cost writes to serve a scan of a
  few rows (PC-7). The index on `last_successful_sign_in_at` still belongs to
  the retention purge (EPIC-10), with its query.
- **No `disabled_at`, `disabled_by`, `password_changed_at`.** Who disabled an
  account and when is what the audit table answers (SC-11); duplicating it on
  the row would create a second, weaker history that nothing keeps consistent.
- **No password-history table.** No rule forbids reusing an old password; only
  the new-password-equals-temporary rule exists (VR-003), and it is checked
  against the hash still stored.
- **No lockout audit columns.** The counter and `lockout_end` already carry the
  state FR-013 needs.

### 3.4 Concurrency

`concurrency_stamp` is already the entity's concurrency token. It covers the
race the API design's `409` describes: two Admins acting on the same account
from two stale screens. The second write fails the token check and is turned
into the re-rendered page with a message, never a `500`.

## 4. `audit_event` — the change

Nothing structural. Three closed lists grow, so three check constraints are
replaced by the migration — exactly what US-009 and US-011 each did.

### 4.1 `ck_audit_event_action`

Today: `admin_sign_in`, `workspace_connection_saved`, `access_check_run`.

Gains, one per action SC-11 names for Dean accounts (spec FR-017):

| Value | Written by |
|---|---|
| `dean_account_created` | `POST /settings/deans` |
| `dean_account_disabled` | `POST /settings/deans/{id}/state`, `desiredState=Disabled` |
| `dean_account_reenabled` | `POST /settings/deans/{id}/state`, `desiredState=Active` |
| `dean_account_password_reset` | `POST /settings/deans/{id}/password` |
| `dean_password_changed` | both password-change operations of the Dean |
| `dean_sign_in` | `POST /sign-in`, succeeded or refused |

**Disabling and re-enabling stay two actions**, although the API design made
them one operation: SC-11 lists "создание, отключение, включение и сброс
пароля" as four things, and an audit row that said only "state changed" would
force a reader to reconstruct the direction from neighbouring rows.

**`dean_password_changed` covers both the forced and the voluntary change.** It
is one thing SC-11 names once — "смена пароля самим Деканом" — and the row's
actor is the Dean in both cases. Which one it was follows from the account's
state at that moment, and nothing in the requirements asks the question.

**`dean_sign_in` mirrors `admin_sign_in`**: one action, `outcome` telling
succeeded from refused, the category carrying why. A separate
`dean_sign_in_refused` action would duplicate what `outcome` already says.

### 4.2 `ck_audit_event_target_type_value`

Today: `workspace_connection` only. Gains `app_user` — the target of all seven
actions above.

`ck_audit_event_target` is unchanged and keeps working: a target type without an
id stays legal (US-009 made it so), which is what a refused sign-in with no
matching account needs — it names no target at all, and a refused management
action names the account when one exists.

### 4.3 `ck_audit_event_refusal_category_value`

Today ten values. Two of them are reused unchanged:

- `account_disabled` — step 4 of FR-012 (the value US-008 added for exactly
  this, its comment already naming US-012);
- `read_only_mode` — the four management actions refused in read-only mode.

Three are new, one per remaining refusal of FR-012:

| Value | Step |
|---|---|
| `unknown_login` | 1 — no account with that normalized email |
| `locked_out` | 2 — a sign-in lockout is in force |
| `wrong_password` | 3 — the password does not match |

These are the four categories `trebovaniya.md` §5 names for a refused sign-in
(неизвестный логин, неверный пароль, учётная запись отключена, вход временно
заблокирован), now complete for the password path.

### 4.4 Row shapes

| Case | `actor_type` / `actor_id` / `actor_role` | `action` | `target_type` / `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|
| account created | `app_user` / the Admin / `admin` | `dean_account_created` | `app_user` / the new account | `succeeded` | null |
| disabled | `app_user` / the Admin / `admin` | `dean_account_disabled` | `app_user` / the account | `succeeded` | null |
| re-enabled | `app_user` / the Admin / `admin` | `dean_account_reenabled` | `app_user` / the account | `succeeded` | null |
| password reset | `app_user` / the Admin / `admin` | `dean_account_password_reset` | `app_user` / the account | `succeeded` | null |
| management refused in read-only | `app_user` / the Admin / `admin` | the action attempted | `app_user` / the account, or null on a create | `refused` | `read_only_mode` |
| Dean changed own password | `app_user` / the Dean / `dean` | `dean_password_changed` | `app_user` / themselves | `succeeded` | null |
| sign-in succeeded | `app_user` / the account / `dean` | `dean_sign_in` | `app_user` / the account | `succeeded` | null |
| sign-in refused, account known | `app_user` / the account / `dean` | `dean_sign_in` | `app_user` / the account | `refused` | `wrong_password`, `account_disabled` or `locked_out` |
| sign-in refused, unknown login | `anonymous` / null / null | `dean_sign_in` | null / null | `refused` | `unknown_login` |

The unknown-login row is the one that carries no identifier at all: SC-11
forbids recording the login that was typed, so the row states only that a
sign-in was refused because the login is unknown (spec FR-017, S-11).

`ck_audit_event_actor_id` and `ck_audit_event_actor_role` already force the
`anonymous` row to carry neither id nor role, and the `app_user` rows to carry
both. Nothing in this Story bends them.

### 4.5 Immutability and retention, unchanged

`ck_audit_event_immutable` keeps `updated_at = created_at`; no row this Story
writes is ever updated. Installation audit rows are still purged by their own
timestamp (PC-11), and the rows naming an account may outlive it — which is why
`target_id` is an identifier without a foreign key and always has been.

## 5. The migration

**Name:** `AddDeanAccounts`. One migration, `Up` doing five things:

1. `ALTER TABLE app_user ADD COLUMN password_is_temporary boolean NOT NULL DEFAULT false`;
2. add `ck_app_user_password_temporary`;
3. drop and re-add `ck_audit_event_action` with the six new values;
4. drop and re-add `ck_audit_event_target_type_value` with `app_user`;
5. drop and re-add `ck_audit_event_refusal_category_value` with the three new
   values.

`Down` reverses all five. No data migration: the default fills existing rows,
and no existing audit row carries a value the narrower constraints would reject
(the new values cannot exist before the code that writes them).

Schema is created only by migrations — no `EnsureCreated()`, no `ddl-auto`
(PC-2). The Control Plane's own migrations are untouched.

## 6. Sensitive data

- `password_hash` continues to hold **only** a hash produced by
  `IPasswordHasher<AppUser>` (OD-002). No plaintext column is added anywhere,
  and the new boolean says only *that* the password is temporary, never what it
  is.
- No audit row, log line or view model carries the hash, the stamp, the counter
  or the lockout end (spec S-10, S-11).
- `email` is personal data and stays where it is; it appears on the Admin's
  screen, never in an audit row (SC-10).
- The Control Plane learns nothing about Dean accounts: they are an
  installation's own data, and no push or legitimacy field mentions them
  (SC-13).

## 7. Constraints checklist (PC conventions)

| Rule | How this design satisfies it |
|---|---|
| PC-2 migrations only, one per Story's schema change | one `AddDeanAccounts` |
| explicit lengths, nullability, defaults — no EF convention defaults | the column is declared `boolean NOT NULL DEFAULT false`; every other column is unchanged and already explicit |
| surrogate generated keys | `app_user.id` unchanged (`UseIdentityByDefaultColumn`) |
| audit timestamp columns | `created_at` / `updated_at` already on both tables, stamped by the existing interceptor |
| indexes where queried (PC-7) | the existing unique index serves every lookup; none added, with the reason in §3.3 |
| no cascade to audit | unchanged: `target_id` is an identifier, not a foreign key |
| sensitive data never in plaintext | §6 |

## 8. For TEST_WRITING

- **`AppUserMigrationTests` goes from four to five installation migrations**,
  with `_AddDeanAccounts` last; the table count stays five. This is an
  **expected** change to an existing test, traced here — the same kind US-011
  recorded in its §8, and the kind US-010 did not need.
- The Control Plane migration test is unchanged.
- Each refusal must commit **exactly one** `audit_event` row and nothing else
  (carried US-009 F-2) — worth a test per refusing path, since five of the nine
  row shapes in §4.4 are refusals.
- Three guards that pass today and must stay green: no new table; no new index
  on `app_user`; no column added to `audit_event`.
- The `ck_app_user_password_temporary` constraint deserves its own test at the
  database level — an Admin row with the flag set must be rejected by
  PostgreSQL, not only by the domain.

## 9. Open questions

None. Every value this design fixes comes from the Specification, SC-11 or an
existing constraint; no Open Decision was raised and none is needed.
