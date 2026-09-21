---
artifact_type: database_design
story: US-011
version: 1
status: DRAFT
created_at: 2026-09-21T08:02:19Z
updated_at: 2026-09-21T08:02:19Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-011-spec.md
    version: 1
  - path: docs/designs/api/US-011-api-design.md
    version: 1
  - path: docs/designs/api/US-011-openapi.yaml
    version: 1
  - path: docs/decisions/US-011-open-decisions.md
    version: 1
supersedes: null
---

# US-011 Database Design — Check access diagnostic

## 1. Scope

This Story stores **nothing about a check** (OD-003, spec FR-013): no table, no
column, no row per step, no "last result". Its only persistence change is that the
installation's `audit_event` table learns **one new action** and **one new refusal
category** — and because that table enforces its closed lists with check
constraints (US-008 db-design §4, amended by US-009), the two new codes need a
constraint-amending migration. Verified against
`AuditEventConfiguration` and migration `20260920181834_AddWorkspaceConnection`,
not taken from the Specification.

| Change | Where |
|---|---|
| `ck_audit_event_action` gains `access_check_run` | installation `audit_event` |
| `ck_audit_event_refusal_category_value` gains `connection_not_usable` | installation `audit_event` |
| migration `AddAccessCheckAudit` | installation |
| anything | Control Plane — **none** (AD-1) |

The names the API design proposed are **confirmed**: `AuditAction.AccessCheckRun`
(code `access_check_run`) and `AuditRefusalCategory.ConnectionNotUsable` (code
`connection_not_usable`).

## 2. Conventions applied

- PC-2 — every schema change ships as an EF Core migration in the same Story; no
  `EnsureCreated()`.
- PC-4/PC-5 — snake_case codes, the existing explicit value conversions extended,
  no EF Core default for an enum.
- PC-9 — no column that could hold the key, its reference, a token or a Google
  response (spec S-07, S-08).
- PC-11 — audit rows are deleted only by the retention purge; this Story adds no
  delete or update path.
- PC-7 — no index: the new rows are written, not queried (there is no audit screen
  until EPIC-9).

## 3. Installation database — table `audit_event`

No column is added, removed or retyped. Lengths still fit: `access_check_run` is 16
characters in `action varchar(64)`; `connection_not_usable` is 21 in
`refusal_category varchar(32)`.

### 3.1 Amended constraints

```sql
-- ck_audit_event_action
action IN ('admin_sign_in', 'workspace_connection_saved', 'access_check_run')

-- ck_audit_event_refusal_category_value
refusal_category IS NULL OR refusal_category IN (
  'not_in_allowed_admin', 'control_plane_unavailable', 'unknown_installation',
  'callback_failed', 'account_disabled', 'domain_mismatch',
  'impersonation_domain_mismatch', 'domain_not_confirmed', 'read_only_mode',
  'connection_not_usable')
```

Every other constraint is unchanged — in particular `ck_audit_event_target`
(`target_id IS NULL OR target_type IS NOT NULL`), `ck_audit_event_target_type_value`
(still only `workspace_connection`), `ck_audit_event_refusal_category`
(`(outcome = 'refused') = (refusal_category IS NOT NULL)`) and
`ck_audit_event_immutable`.

### 3.2 Rows this Story writes

| Situation (spec FR-008) | action | actor | outcome | refusal_category | target_type | target_id |
|---|---|---|---|---|---|---|
| check carried out (any verdict) | `access_check_run` | `app_user`, the Admin's id, `admin` | `succeeded` | null | `workspace_connection` | the connection's id |
| refused: read-only mode | `access_check_run` | as above | `refused` | `read_only_mode` | `workspace_connection` | the connection's id if a row exists, else null |
| refused: no usable connection | `access_check_run` | as above | `refused` | `connection_not_usable` | `workspace_connection` | the connection's id for `DomainMismatch`; null for `NotConfigured` |

- A `succeeded` row always has a target id: the check runs only on a usable
  connection, which is a stored row.
- `read_only_mode` is **reused**, not duplicated: it already means "refused because
  the installation is in read-only mode" for the save of US-009, and it means the
  same here. The action column tells the two apart.
- `connection_not_usable` covers both `NotConfigured` and `DomainMismatch`
  deliberately: the audit answers "who ran what, and was it refused"; *why the
  connection was unusable* is the connection state, visible on US-009's page, and
  the target id already distinguishes the two (null vs. present).
- `request_id` is set as on every row (SC-11). No row carries the technical
  account, the domain, a scope, a step outcome or the verdict (spec I-4, S-11).

### 3.3 The read-only refusal commit

As US-009 FR-008 and its db-design §4.2 established: the guard throws, and the
refusal row is committed alone under a declared `PermittedServiceWrite.AuditEvent`.
**Nothing else may be staged** in the `DbContext` at that moment — the carried
US-009 finding F-2 (a declared service write commits the whole context). This
Story's use case stages nothing before the guard, so the assumption holds; a test
proves the committed change set is exactly one `audit_event` row.

### 3.4 Reads

The check reads `workspace_connection` (through the US-009 query) and
`legitimacy_state` (through the read-only guard and the US-009 query). Both are
untracked singleton reads. No read is added to any other table.

## 4. What is deliberately not stored

| Not stored | Why |
|---|---|
| the result, the verdict, any step outcome | OD-003; a stored result would also be a new service write for BR-026's closed list |
| the time of the last check | same; OD-005 set no cooldown, so nothing needs it |
| any access token | SC-7; a token outlives nothing beyond the check (spec FR-002) |
| the service-account key or its reference | SC-7 Hard Stop, PC-9 — configuration and the secret store only (DC-3, DC-5) |
| anything from a Google response | spec S-08, SC-12 |

The startup self-check writes **nothing** to the database (spec FR-010): it logs.

## 5. Relationships

None added. `audit_event.target_id` stays a bare identifier with no foreign key
(US-008 db-design §4: audit rows outlive what they name).

## 6. Sensitive data

The new codes are fixed identifiers, not data. The table still has no column able
to hold an email, a domain, a name or free text beyond the request id. The Google
packages this Story adds (OD-001) live in `Infrastructure` and touch no persistence
type.

## 7. Migrations

### 7.1 Installation — `AddAccessCheckAudit`

Generated with `dotnet ef migrations add AddAccessCheckAudit --project
src/ClassroomAgent.Infrastructure --startup-project src/ClassroomAgent.Web` after
the configuration change, and reviewed so that it contains **only**:

- `DropCheckConstraint` + `AddCheckConstraint` for `ck_audit_event_action`;
- `DropCheckConstraint` + `AddCheckConstraint` for
  `ck_audit_event_refusal_category_value`;
- the exact inverse in `Down`.

No `CreateTable`, no `AddColumn`, no index. `Down` would fail on a database that
already holds an `access_check_run` row — correct and expected: rolling back past a
Story whose audit rows exist must not silently discard or orphan them (PC-11), and
DC-4 applies migrations forward.

### 7.2 Control Plane — none

The Control Plane's schema and its migrations are untouched (AD-1).

## 8. Test-relevant facts

- `AppUserMigrationTests` asserts **3** installation migrations; it becomes **4**,
  with `_AddAccessCheckAudit` last, and the table list stays at **five** tables. This
  change to an existing test is expected and traced to this design — unlike US-010,
  which had to leave the test untouched.
- The Control Plane migration test (5 migrations) stays unchanged.
- A PostgreSQL integration test (TC-2) proves each new code is accepted, and that
  an unknown action code is still rejected by `ck_audit_event_action`.
- For every row of §3.2 a test reads the row back and asserts `action`, `outcome`,
  `refusal_category`, `target_type`, `target_id` and that no other row was written
  by the same request.
