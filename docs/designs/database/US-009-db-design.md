---
artifact_type: database_design
story: US-009
version: 1
status: DRAFT
created_at: 2026-09-20T13:40:00Z
updated_at: 2026-09-20T13:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-009-spec.md
    version: 1
  - path: docs/designs/api/US-009-api-design.md
    version: 1
  - path: docs/designs/api/US-009-openapi.yaml
    version: 1
  - path: docs/decisions/US-009-open-decisions.md
    version: 1
  - path: docs/designs/database/US-008-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-009 Database Design — Configure WorkspaceConnection

## 1. Scope

One new table in the **installation** database and one amendment to an existing
one, in a single EF Core migration:

- `workspace_connection` — the school's Google Workspace domain and its
  technical account (`trebovaniya.md` §3; spec FR-001, FR-012);
- `audit_event` — no new column, but three check constraints change, because
  this Story adds an audited action, a target type and four refusal categories
  (spec FR-009). The codes an enum may hold are enforced in the database, so
  extending an enum **is** a schema change here.

**The Control Plane database is not changed.** A school's connection is its own
data and is never reported to the Owner (spec S-11); a migration there would be a
defect of this Story.

`app_user` and `legitimacy_state` keep their shape. `legitimacy_state` is read
for the `Installation` domain and never written by this Story (spec FR-003).

## 2. Conventions applied

PC-2 (migrations only, no `EnsureCreated()`), PC-3 (`bigint` identity surrogate
key named `id`), PC-4 (every column explicitly mapped — length, nullability,
uniqueness), PC-5 (`snake_case` singular table; `pk_` / `uq_` / `ix_` / `ck_`
names), PC-6 (`created_at` / `updated_at` as UTC `timestamptz`, stamped by the
existing `TimestampInterceptor`), PC-7 (an index only where a query uses the
column as a lookup key), PC-8 (explicit cardinality), PC-9 (sensitive data).

The singleton pattern is the one US-005 established for `legitimacy_state`: a
shadow `singleton` boolean, always true, with a unique index and a check
constraint. It is reused rather than re-invented (AD-10).

## 3. Installation database — table `workspace_connection`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate key, `pk_workspace_connection` (PC-3) |
| `domain` | `varchar(253)` | no | the Workspace domain the connection is bound to, lower-cased |
| `impersonation_user_email` | `varchar(254)` | no | the school's technical account (BR-015), lower-cased |
| `singleton` | `boolean` | no | shadow property, default `true` |
| `created_at` | `timestamptz` | no | PC-6 |
| `updated_at` | `timestamptz` | no | PC-6 |

Three columns of business data and nothing else. **No key, no secret, no
reference to either, no client id, no password, no token** — the service-account
key lives in the configured secret store and its reference in the installation's
configuration, never here (PC-9, SC-7, `trebovaniya.md` §3 v33). A migration or
entity adding such a column is a Critical finding.

`domain` is stored although it can only ever equal the `Installation` domain
(spec I-6): without it the OD-002 mismatch is undetectable, and §3 describes the
record as holding both values.

### 3.1 Constraints

| Name | Rule | Why |
|---|---|---|
| `pk_workspace_connection` | primary key on `id` | PC-3 |
| `ck_workspace_connection_singleton` | `singleton` | with the unique index below, the table holds zero or one row |
| `ck_workspace_connection_domain_length` | `char_length(domain) BETWEEN 3 AND 253` | VR-002 |
| `ck_workspace_connection_domain_lowercase` | `domain = lower(domain)` | a row that bypassed the application cannot hold a mixed-case domain (spec FR-012) |
| `ck_workspace_connection_domain_format` | `domain ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$'` | VR-002: at least two labels, hyphens not at a label edge, no trailing dot (it is stripped before the write), ASCII only (spec I-4) |
| `ck_workspace_connection_email_length` | `char_length(impersonation_user_email) BETWEEN 3 AND 254` | VR-001 |
| `ck_workspace_connection_email_lowercase` | `impersonation_user_email = lower(impersonation_user_email)` | VR-001 |
| `ck_workspace_connection_email_format` | `impersonation_user_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'` | VR-001: exactly one `@`, non-empty parts, no whitespace |
| `ck_workspace_connection_email_domain` | `split_part(impersonation_user_email, '@', 2) = domain` | the stored row is internally consistent: the technical account always belongs to the domain the row is bound to |

The last constraint deserves its limit stated plainly: it enforces that the row
agrees **with itself**, not BR-020. BR-020 compares both values against the
`Installation` domain in `legitimacy_state`, which lives in another table and
changes independently, so it stays where the Specification puts it — in
`Application`, on every save (spec FR-006, FR-007, S-02). The constraint removes
one way for a hand-edited or half-written row to be wrong; it is not the control.

### 3.2 Indexes

| Name | Columns | Why |
|---|---|---|
| `uq_workspace_connection_singleton` | `singleton`, unique | enforces at most one row |

No other index. Neither `domain` nor `impersonation_user_email` is a lookup key:
every read of this table fetches the single row (PC-7). An index on a column of a
one-row table would be cost without a query.

### 3.3 Write

Two paths, both from `SaveWorkspaceConnectionUseCase` (spec FR-006):

- **first save** — insert the row, then insert the audit row, both inside one
  transaction through `IUnitOfWork.ExecuteInTransactionAsync`, so a saved
  connection without its audit row is impossible and the audit row can carry the
  identity the insert generated (the US-008 §4.4 pattern);
- **change** — update `impersonation_user_email` (and `domain`, which the use
  case always writes from `legitimacy_state`, so an OD-002 mismatch is cleared by
  saving), then insert the audit row, in the same one transaction.

A save of identical values still updates `updated_at` and still writes the audit
row (spec I-7). A refused save writes the audit row **only** — the connection is
never touched (spec FR-009, AC-008).

The unique `singleton` index makes a concurrent double first-save a `23505`
rather than a second row. The use case treats it as US-008 treats the concurrent
first sign-in: re-read the row and apply the change to it, never surface an
error (US-008 I-10 as the precedent; spec AC-007).

### 3.4 Read

One query: fetch the single row, or nothing. It is combined with the
`legitimacy_state` read of `GetLegitimacyModeQuery` / the domain read of spec
FR-003 in `GetWorkspaceConnectionQuery`, which returns the DTO of the API
contract. The entity never leaves `Application` (AD-8).

The read is untracked: nothing in the read path writes, and a tracked entity
flushed by a later `SaveChangesAsync` in the same request is exactly the defect
the US-008 security review raised as F-4.

## 4. Installation database — table `audit_event`

No column is added, removed or retyped. Three check constraints change because
the code sets they enumerate grow (spec FR-009):

| Constraint | Before (US-008) | After (US-009) |
|---|---|---|
| `ck_audit_event_action` | `action IN ('admin_sign_in')` | `action IN ('admin_sign_in', 'workspace_connection_saved')` |
| `ck_audit_event_refusal_category_value` | five sign-in categories | the same five plus `domain_mismatch`, `impersonation_domain_mismatch`, `domain_not_confirmed`, `read_only_mode` |
| `ck_audit_event_target_type_value` | did not exist — the enum was empty | `target_type IS NULL OR target_type IN ('workspace_connection')` |

### 4.1 The `target` pairing constraint changes shape

US-008 wrote `ck_audit_event_target` as
`(target_type IS NULL) = (target_id IS NULL)`, which was exactly right while no
action had a target at all. This Story needs a row that names **what** was
refused without naming a row that does not exist: a refused save has
`target_type = 'workspace_connection'` and `target_id = NULL`, because no
connection row was created (spec FR-009).

The constraint therefore becomes:

```
ck_audit_event_target:  target_id IS NULL OR target_type IS NOT NULL
```

- what it still forbids — a `target_id` with no `target_type`, an identifier
  belonging to nothing;
- what it now allows — a `target_type` with no `target_id`, which is the shape of
  every refused action: the kind of object is known, the object was never
  created.

This is a deliberate relaxation of a US-008 invariant, recorded here rather than
made silently, and it is the only change this Story makes to the audit table's
rules. The alternative — writing both columns `NULL` on a refusal — would keep
the old constraint but lose the target type from exactly the rows an
investigation cares about, and the Specification fixes the row shape in FR-009.

### 4.2 Rows this Story writes

| Case | `action` | `target_type` | `target_id` | `outcome` | `refusal_category` |
|---|---|---|---|---|---|
| first save | `workspace_connection_saved` | `workspace_connection` | the new row's id | `succeeded` | null |
| change | `workspace_connection_saved` | `workspace_connection` | the existing row's id | `succeeded` | null |
| refused: impersonation domain | `workspace_connection_saved` | `workspace_connection` | null | `refused` | `impersonation_domain_mismatch` |
| refused: domain to be written | `workspace_connection_saved` | `workspace_connection` | null | `refused` | `domain_mismatch` |
| refused: no domain known | `workspace_connection_saved` | `workspace_connection` | null | `refused` | `domain_not_confirmed` |
| refused: read-only mode | `workspace_connection_saved` | `workspace_connection` | null | `refused` | `read_only_mode` |

The actor is always the Admin's `app_user` id and role `admin` — the request
reached the use case, so it was authenticated and authorised (spec FR-009,
FR-010). `occurred_at` comes from the injectable clock; `request_id` ties the row
to its log line (SC-11).

**A first save and a change are one action**, distinguished by whether
`target_id` names a row that already existed (spec I-2). A test asserts the
distinction without reading any typed value, by comparing the `target_id` of the
two rows: both name the same connection id, and only the first is preceded by no
row at all.

No row carries a domain, an address or any value the Admin typed (SC-11, PC-9).
Immutability is unchanged: `ck_audit_event_immutable` (`updated_at = created_at`)
and the private setters stay as US-008 left them.

## 5. Relationships

None. `workspace_connection` has no foreign key and no navigation property, and
nothing references it (PC-8).

`audit_event.target_id` names a connection row by **bare identifier**, with no
foreign key — the same rule as `actor_id` (PC-9): audit rows outlive what they
name, and the retention purge (PC-11) deletes a connection's evidence on the
row's own schedule, not the connection's. A foreign key would also make the
refused-save rows impossible, since they name no row at all.

## 6. Sensitive data

| Datum | Handling |
|---|---|
| service-account key, or a reference to it | **never stored** — secret store and configuration only (PC-9, SC-7). No column exists to hold one |
| `impersonation_user_email` | an account of the school, stored in clear because it is the value the program must present to Google when reading data (BR-015). It is never written to an audit row and never to a log (SC-10, SC-11) |
| `domain` | the school's domain; not personal data, and already held in `legitimacy_state` |
| audit rows | internal identifiers, codes and timestamps only |

The technical account has no person behind it (BR-015), so this table holds no
personal datum in the sense of §5 — but the address is still school data: it is
returned only to an Admin (spec FR-010), never to the Control Plane (S-11) and
never to any log line (spec FR-014).

Retention: `workspace_connection` is configuration, not personal data, and the
purge of PC-11 does not touch it. Its audit rows are purged on their own
timestamps like every other row.

## 7. Migrations

### 7.1 Installation — `AddWorkspaceConnection`

One migration in `ClassroomAgent.Infrastructure`, created with the documented
command and applied by an explicit deployment step — never by
`Database.Migrate()` at start-up (PC-2, DC-4).

It:

- creates `workspace_connection` with its primary key, its unique singleton index
  and its nine check constraints (§3.1, §3.2);
- drops and recreates `ck_audit_event_action` and
  `ck_audit_event_refusal_category_value` with the enlarged code sets (§4);
- drops `ck_audit_event_target` and recreates it in its new shape (§4.1);
- adds `ck_audit_event_target_type_value` (§4).

It seeds nothing: a seeded connection would be a domain nobody entered and
nobody approved.

`ClassroomAgentDbContext` gains one `DbSet` and one configuration class; the
existing `TimestampInterceptor` covers the new entity without change (PC-6).

The migration is reversible: `Down` drops the table and restores the three audit
constraints to their US-008 text. A downgrade with a `workspace_connection_saved`
row already written would fail on the restored `ck_audit_event_action`, which is
correct — the data no longer fits the old schema, and silently deleting audit
rows to make a downgrade succeed is forbidden (SC-11).

### 7.2 Control Plane — none

Deliberately empty. The Control Plane's schema and model snapshot must be
identical before and after this Story (§1).

## 8. Test-relevant facts

Against real PostgreSQL via Testcontainers; the EF Core InMemory provider is
forbidden (TC-2).

- `uq_workspace_connection_singleton` rejects a second row, and the use case
  turns the `23505` into a re-read (§3.3, AC-007).
- `ck_workspace_connection_email_domain` rejects a row whose address is outside
  its own domain — the last line of defence behind the `Application` check
  (§3.1).
- `ck_workspace_connection_domain_lowercase` and
  `ck_workspace_connection_email_lowercase` reject mixed case, so normalisation
  cannot be skipped by writing through another path.
- `ck_workspace_connection_domain_format` rejects a single-label host, a label
  starting or ending with a hyphen, and a trailing dot.
- The new audit constraints accept the six rows of §4.2 and reject an action or
  refusal category that no enum member codes.
- `ck_audit_event_target` still rejects a `target_id` without a `target_type`
  (§4.1).
- A refused save leaves `workspace_connection` byte-for-byte unchanged while
  `audit_event` gains exactly one row — including in read-only mode, where the
  connection write never happens (spec FR-008, I-8).
- The Control Plane's model snapshot is unchanged: a test that applies the
  Control Plane migrations and compares the model against the snapshot passes
  untouched.
