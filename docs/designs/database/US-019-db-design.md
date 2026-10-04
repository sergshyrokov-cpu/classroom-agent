---
artifact_type: database_design
story: US-019
version: 1
status: DRAFT
created_at: 2026-10-04T06:35:40Z
updated_at: 2026-10-04T06:35:40Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-019-spec.md
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-api-design.md
    version: 1
  - path: docs/decisions/US-019-open-decisions.md
    version: 1
supersedes: null
---

# US-019 Database Design — Trigger a synchronization from the UI

## 1. Summary

**No new table, no new column, no new index.** The only persistence change is on
`audit_event`: one new action value, and the check constraints that enumerate
and shape it. One migration (PC-2).

Not `NOT_APPLICABLE`: a new value is written to an existing constrained column.

The request itself is **not persisted** — it is process state in
`SyncRunCoordinator` (US-013 OD-007; a restart starts from the schedule again).
`sync_state` is untouched: the press writes nothing to it (spec BR-044; the run
it requests writes `sync_state` exactly as a scheduled run does, US-013).

## 2. `audit_event` — one new action

### 2.1 `AuditAction.SynchronizationRequested`

Name confirmed (spec FR-005 proposal). Database code
`'synchronization_requested'`, in the explicit code mapping of
`AuditEventConfiguration` (`ActionCode` / `ActionFromCode`), as every member
before it.

Meaning: a manual start of synchronization (`trebovaniya.md` §5 "ручной запуск
синхронизации", SC-11) — accepted or refused. `succeeded` means **the request
was accepted**, not that a run happened (spec I-2).

### 2.2 The rows

| Column | Accepted press | Refused press |
|---|---|---|
| `occurred_at` | time of the press (`TimeProvider`) | same |
| `actor_type` | `app_user` | `app_user` |
| `actor_id` | the signed-in account's `AppUser` id | same |
| `actor_role` | `admin` or `dean` — **from the session**, unlike the existing Admin-only factories | same |
| `action` | `synchronization_requested` | same |
| `target_type` / `target_id` | `NULL` / `NULL` (spec I-3) | `NULL` / `NULL` |
| `outcome` | `succeeded` | `refused` |
| `refusal_category` | `NULL` | `read_only_mode` or `connection_not_usable` (existing values) |
| `request_id` | the request's trace id | same |
| purge counts | `NULL` (existing `ck_audit_event_purge_counts_absent`) | `NULL` |
| `created_at` = `updated_at` | as every audit row (`ck_audit_event_immutable`) | same |

No email, no domain, nothing from Google (SC-11, PC-9). `AuditTargetType` and
`AuditRefusalCategory` gain nothing.

### 2.3 Constraints

| Constraint | Change |
|---|---|
| `ck_audit_event_action` | **amended**: the list gains `'synchronization_requested'` (11 values) |
| `ck_audit_event_sync_request_shape` | **new**: `action <> 'synchronization_requested' OR (actor_type = 'app_user' AND target_type IS NULL AND target_id IS NULL AND (refusal_category IS NULL OR refusal_category IN ('read_only_mode', 'connection_not_usable')))` |
| every other `audit_event` constraint | unchanged |

The new shape constraint follows `ck_audit_event_purge_actor` (US-037 db-design
§2.4): it states in the schema what the domain factories guarantee — a manual
start is always a user's, never targets a row, and is refused only for the two
reasons the use case has. It costs nothing and catches a factory mistake in an
integration test. The existing `ck_audit_event_actor_id` / `ck_audit_event_actor_role`
already force `actor_id` and `actor_role` for `app_user`.

### 2.4 Retention

Unchanged: these rows are purged by the existing retention purge with every
other audit row of the installation (PC-11, US-037). They are never updated.

## 3. The migration

`AddSynchronizationRequestAudit` — in
`src/ClassroomAgent.Infrastructure/Persistence/Migrations/`, generated with the
command in `AGENTS.md`, after the configuration change:

- `Up`: drop and re-create `ck_audit_event_action` with the 11 values; add
  `ck_audit_event_sync_request_shape`.
- `Down`: drop `ck_audit_event_sync_request_shape`; re-create
  `ck_audit_event_action` with the 10 values of `AddRetentionPurge`.

No data migration: no existing row has the new value. No `EnsureCreated()`
(PC-2). The model snapshot is updated by the generator.

`Down` would fail if rows with the new action exist; that is the expected
behaviour of a constraint-narrowing rollback, as in US-011 and US-037, and is
not mitigated.

## 4. Sensitive data

Nothing new is stored. The audit row carries ids, a role, a category and a trace
id — no personal data beyond the actor's internal id (SC-11). Nothing about the
key, the connection or Google is written (SC-7, PC-9).

## 5. Transactions and read-only mode

- The accepted row is written in the use case's own unit of work after the
  enqueue (spec FR-001 step 4). If the commit fails after the request was
  enqueued, the run still happens and the press is not audited; the failure
  surfaces through the single exception handler (`500`). Order "enqueue, then
  audit" is what the spec fixes; an in-process request cannot be enlisted in the
  database transaction.
- The read-only refusal row is written around its commit alone, declaring
  `PermittedServiceWrite.AuditEvent` (US-009 FR-008, US-011 FR-008); audit rows
  are on the BR-026 list. `PermittedServiceWrite` is unchanged.
- The unusable-connection refusal is not in read-only mode, so it is an ordinary
  write.

## 6. What this Story does not touch

`sync_state`, `workspace_connection`, `app_user`, every teaching-data table and
every Control Plane table. `DbContext` stays out of `Application` and `Web`
(AD-3).
