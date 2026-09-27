---
artifact_type: database_design
story: US-013
version: 1
status: DRAFT
created_at: 2026-09-27T12:10:19Z
updated_at: 2026-09-27T12:10:19Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-013-spec.md
    version: 1
  - path: docs/designs/api/US-013-api-design.md
    version: 1
  - path: docs/decisions/US-013-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-013 Database Design — Background synchronization service

**Verdict: PASS.** One new table, one new migration, nothing else touched.

## 1. Summary of the schema change

| Change | Object |
|---|---|
| New table | `sync_state` — one row, the state of synchronization (`trebovaniya.md` §3) |
| New check constraints | `ck_sync_state_singleton`, `ck_sync_state_status`, `ck_sync_state_counter`, `ck_sync_state_finished_after_started`, `ck_sync_state_terminal_fields`, `ck_sync_state_error_length` |
| New unique index | `uq_sync_state_singleton` |
| New migration | `AddSyncState` — installation migrations **5 → 6**, tables **5 → 6** |
| Altered tables | **none** |
| New relationship | **none** — `sync_state` references nothing and nothing references it |
| Control Plane | **untouched** |

No `audit_event` constraint is amended, because a scheduled run writes no audit
row (spec FR-017). No index is added to any existing table.

## 2. Why the Story needs so little

The Story is the **host** of synchronization, not its data (OD-001): the pipeline
it runs is empty until US-014. Everything the Story stores is the answer to one
question — "what is synchronization doing, and how did it last go" — which
`trebovaniya.md` §3 answers with a single entity: "**SyncState** — статус фоновой
синхронизации с Classroom API (running/completed/failed, счётчик, последняя
ошибка, время последней синхронизации)".

Everything else the Story needs already exists: `LegitimacyState` (US-005) decides
read-only mode, `WorkspaceConnection` (US-009) says whether there is anything to
synchronize through, and `AuditEvent` (US-008) is not written at all here.

## 3. `sync_state` — the new table

### 3.1 Columns

| Column | Type | Null | Rule |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate key, `pk_sync_state` (PC-3) |
| `singleton` | `boolean` | no | shadow property, default `true`, unique — keeps the table at zero or one row (the `legitimacy_state` pattern) |
| `status` | `varchar(16)` | no | `running` \| `completed` \| `failed`, stored as a code through a value converter (spec FR-002) |
| `run_id` | `uuid` | no | the identifier of the current or last run (spec FR-001, VR-003) |
| `started_at` | `timestamptz` | no | when that run started |
| `finished_at` | `timestamptz` | **yes** | when it ended; null exactly while `status = 'running'` |
| `processed_count` | `integer` | no | the one counter of `trebovaniya.md` §3 (spec I-8), `>= 0` |
| `last_error` | `varchar(512)` | **yes** | category and short message of the last failure; null unless `status = 'failed'` |
| `last_successful_run_at` | `timestamptz` | **yes** | end of the last **completed** run; null until one completes |
| `created_at` | `timestamptz` | no | PC-6, stamped by the interceptor |
| `updated_at` | `timestamptz` | no | PC-6, stamped by the interceptor |

Every column is mapped explicitly through the entity configuration — no EF Core
convention default, and `HasMaxLength` on every string (PC-4). Names are
snake_case and the table is singular (PC-5).

`run_id` is a `uuid`, not a string: it is generated inside the process (spec
VR-003), never an identifier from an external system, so PC-3's natural-key rule
does not apply to it. It is **not** unique — one row is overwritten by every run,
so the same column holds a different value over time (OD-004).

### 3.2 Check constraints

| Constraint | Expression | Why |
|---|---|---|
| `ck_sync_state_singleton` | `singleton` | zero or one row (OD-004), the `legitimacy_state` mechanism |
| `ck_sync_state_status` | `status IN ('running', 'completed', 'failed')` | the three values `trebovaniya.md` §3 names and no fourth (spec FR-002, I-3) |
| `ck_sync_state_counter` | `processed_count >= 0` | spec VR-002 |
| `ck_sync_state_finished_after_started` | `finished_at IS NULL OR finished_at >= started_at` | spec VR-002 |
| `ck_sync_state_terminal_fields` | `(status = 'running' AND finished_at IS NULL AND last_error IS NULL) OR (status = 'completed' AND finished_at IS NOT NULL AND last_error IS NULL) OR (status = 'failed' AND finished_at IS NOT NULL AND last_error IS NOT NULL)` | the status and the three nullable columns can never disagree |
| `ck_sync_state_error_length` | `last_error IS NULL OR char_length(last_error) BETWEEN 1 AND 512` | a blank message is not a diagnosis; the bound is spec VR-002's, fixed here |

`ck_sync_state_terminal_fields` is the constraint that earns its place: it makes
three of the Specification's rules impossible to break at the database level — a
run in progress cannot carry an end instant or an error, a completed run cannot
carry an error (spec FR-008), and a failed run cannot be stored without one. It is
the same defence in depth as the two US-008 constraints that make an Admin
password hash impossible.

**`last_successful_run_at` is deliberately not in that constraint.** A failed run
leaves the column as it was (spec FR-008), so a row with `status = 'failed'` and a
non-null `last_successful_run_at` is the normal, informative case — the school
succeeded on Tuesday and has been failing since. The only invariant is that the
column is never written by a failing run, which is the entity's job, not a
constraint's: the value's provenance cannot be expressed in a row-level check.

### 3.3 The 512-character bound on `last_error`

`varchar(512)` is long enough for a category plus one sentence and short enough
that nothing resembling a Google error object, a stack trace or a payload fits. The
Specification forbids all three (spec FR-008, S-06, SC-10); the length limit makes
an accidental violation fail loudly at the database instead of quietly filling the
column with a stack trace. The Application layer truncates before storing so that a
long message becomes a short one rather than an exception at commit.

### 3.4 What is deliberately not added

| Not added | Why |
|---|---|
| A run-history table | OD-004: one row updated in place. A history would need a retention rule, and PC-11 provides none. |
| A seeded row in the migration | Spec I-2: the absence of the row **is** the "never synchronized" state; the first run creates it. A seeded row would need a status value the vocabulary does not have. |
| An `interrupted` or `cancelled` status | Spec I-3: a crashed process cannot write anything, so a stale `running` row must be tolerated in any design; a fourth value `trebovaniya.md` §3 does not define would add a vocabulary without removing the case. |
| Any index besides the singleton one | The table holds one row; every read is a full scan of one row. PC-7 asks for indexes on lookup keys and foreign keys — there are none. |
| A foreign key to `workspace_connection` | Spec FR-002: `SyncState` is the state of the *service*, not of a connection. Deleting a connection must not cascade into the run record. |
| Per-entity counters | Spec I-8: `trebovaniya.md` §3 says "счётчик", singular. US-014 and US-015 decide whether they need more, and a migration is how they would add it. |
| A "next run due at" column | The schedule lives in the process (spec FR-003) and is derived from the previous run's completion plus the configured interval. Persisting it would create a second source of truth for timing and would have to be kept correct across restarts for no benefit. |
| A retention rule for the row | PC-11 deletes teaching data, accounts and audit rows by age. One row of service state has no age to purge; it is overwritten, not accumulated. |

### 3.5 Concurrency

One writer exists by construction: the coordinator guarantees a single run at a
time in a single process (spec FR-004), and an installation is one process. The row
therefore needs no optimistic-concurrency token, and none is added. Two writes
happen per run (spec FR-006) — `running` at the start, the terminal state at the
end — each its own commit, each staging the `sync_state` row and nothing else (the
US-009 review finding F-2).

## 4. `audit_event` — no change

A **scheduled** run writes no audit row (spec FR-017): `trebovaniya.md` §5 audits
the *manual* start of a synchronization, which is US-019. Therefore:

- `AuditAction` gains no member and `ck_audit_event_action` is **not** amended;
- `AuditTargetType` and `AuditRefusalCategory` gain nothing;
- no constraint-amending migration is needed for the audit table — unlike US-009,
  US-011 and US-012, each of which needed one.

US-019 adds the action, its constraint amendment and the row, with `system` or the
requesting user as actor per `trebovaniya.md` §5.

## 5. The migration

One migration, `AddSyncState` (PC-2), shipped in this Story:

1. `CREATE TABLE sync_state` with the columns of §3.1, `pk_sync_state`, the six
   check constraints of §3.2 and the unique index `uq_sync_state_singleton`.
2. Nothing else. No `ALTER TABLE`, no data migration, no seed.

After it, the installation database holds **six** migrations and **six** tables:
`__EFMigrationsHistory`, `app_user`, `audit_event`, `legitimacy_state`,
`sync_state`, `workspace_connection`. The Control Plane keeps its five migrations
and six tables.

The down migration drops the table. Nothing references it, so the drop is clean.

## 6. Sensitive data

`sync_state` holds **no personal data at all** (PC-9, SC-10):

- no email, no name, no grade, no Google identifier of a person or a course;
- `last_error` carries a category and a short message, never a Google error object,
  never a payload, never an identifier of a person (spec S-06, §3.3);
- `run_id` is an internal value with no meaning outside the log it correlates;
- no key, token, connection string or secret ever reaches the table (SC-7).

Access is through the repository port only; the table is never exposed by an
endpoint in this Story (api-design §1). US-024 will show it to Admin and Dean, both
of whom may already see teaching data (BR-002).

## 7. Constraints checklist (PC conventions)

| Convention | How this design satisfies it |
|---|---|
| PC-2 schema by migration only | one `AddSyncState` migration; no `EnsureCreated()` |
| PC-3 identifiers | `bigint` identity `id`; `run_id` is process-generated, not an external natural key, so no unique index is required for it |
| PC-4 explicit mapping | every column typed, lengths on every string, nullability matching the entity's annotations |
| PC-5 naming | `sync_state`, snake_case columns, `pk_`/`uq_`/`ck_` prefixes |
| PC-6 audit timestamps | `created_at` / `updated_at`, UTC `timestamptz`, stamped by the existing interceptor |
| PC-7 indexes | only `uq_sync_state_singleton`; no foreign key and no lookup column exists |
| PC-8 relationships | none, by design (§3.4) |
| PC-9 sensitive data | none stored (§6) |
| PC-10 synchronization | the upsert, incremental and course-age rules belong to US-014, US-015 and US-018 and are **not** modelled here; `SyncState` remains the only source of run progress |
| PC-11 retention purge | not extended; one row of service state has nothing to purge (§3.4) |

## 8. For TEST_WRITING

- `AppUserMigrationTests` asserts the installation's migration and table counts.
  Both change here: **five to six** migrations with `_AddSyncState` last, and
  `sync_state` added to the table set. This is an **expected** change to an existing
  test, traced here as US-011 and US-012 traced their own. The same file's
  `TheControlPlaneSchema_IsUnchangedByThisStory` must keep passing untouched.
- Three guards deserve a database-level test, because the domain alone could be
  bypassed by a future repository:
  - a second row is rejected by PostgreSQL (`uq_sync_state_singleton` /
    `ck_sync_state_singleton`);
  - a status outside the three values is rejected;
  - each of the three disagreements `ck_sync_state_terminal_fields` forbids is
    rejected — `running` with an end instant, `completed` with an error, `failed`
    without one.
- The absence of the row must be a tested state: a reader on a fresh database sees
  "never synchronized", not an exception (spec I-2).
- A failed run must be shown to leave `last_successful_run_at` untouched — assert
  it across a completed run followed by a failed one, which is the only way the
  rule is visible (spec FR-008).
- Each write must commit exactly one row and nothing else (carried US-009 F-2).
- Tests run against real PostgreSQL through Testcontainers; the EF Core InMemory
  provider would enforce none of the six constraints (TC-2).

## 9. Open questions

None raised by this stage. All seven Open Decisions are resolved and none leaves a
persistence question open. The two values the Specification left to this stage are
fixed here: the bound on `last_error` (512 characters, §3.3) and the singleton
mechanism (a shadow `singleton` column with a unique index, §3.2).
