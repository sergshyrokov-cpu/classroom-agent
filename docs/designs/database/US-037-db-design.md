---
artifact_type: database_design
story: US-037
version: 1
status: DRAFT
created_at: 2026-10-03T16:58:45Z
updated_at: 2026-10-03T16:58:45Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-037-spec.md
    version: 1
  - path: docs/designs/api/US-037-api-design.md
    version: 1
  - path: docs/decisions/US-037-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-037 Database Design — Retention purge

**Verdict: PASS.** One migration, `AddRetentionPurge`: five count columns and
one action value on `audit_event`, two indexes, and no new table. The rest of
the Story is new **deletes** over existing tables.

`api_design` is NOT_APPLICABLE and there is no `openapi` file by that decision,
so no DTO maps to anything here.

## 1. Tables touched

| Table | Schema change | Behaviour change |
|---|---|---|
| `audit_event` | 5 nullable count columns; new `action` value; constraints; index on `occurred_at` | rows older than the cutoff deleted (FR-008); one purge row per run (FR-010) |
| `course_membership` | partial index for off-roster rows | leavers deleted (FR-005); memberships of expired courses deleted (FR-003) |
| `course` | none | expired courses deleted (FR-003) |
| `course_work` | none | deleted with their course (FR-003) |
| `submission` | none | deleted with their course or leaver (FR-003, FR-005) |
| `classroom_participant` | none | orphans deleted (FR-006) |
| `app_user` | none | unused accounts deleted (FR-007) |

Untouched: `legitimacy_state`, `workspace_connection`, `sync_state`.

No foreign key changes. Every FK stays `Restrict` (PC-8); the purge deletes
child first and never relies on a cascade (PC-11).

## 2. `audit_event` — the counts

### 2.1 Why columns on `audit_event`

§5 v47 and SC-11 put the counts **in the audit event** of the run, and §5's
list of row fields (time, actor, action, target, outcome, request id) does not
hold them. Options considered:

| Option | Verdict |
|---|---|
| **Five nullable `integer` columns on `audit_event`, required for the purge action and forbidden for all others** | **chosen** |
| A JSON / text "details" column | rejected: free text in an audit row is what PC-9 and SC-10 forbid; a check constraint cannot prove it holds no personal datum |
| A separate `retention_purge_run` table linked by id | rejected: two rows for one event, a second retention rule, and the counts would leave the audit trail the requirement puts them in |

The columns hold integers only, so no personal datum can enter them; PC-9's
"no name, email or grade column" holds.

### 2.2 Columns

| Column | Type | Null | Meaning (spec FR-010) |
|---|---|---|---|
| `purged_courses` | `integer` | yes | expired courses deleted |
| `purged_leaver_memberships` | `integer` | yes | leavers' memberships deleted by FR-005 (not those of expired courses) |
| `purged_participants` | `integer` | yes | orphaned participants deleted |
| `purged_accounts` | `integer` | yes | `app_user` rows deleted |
| `purged_audit_rows` | `integer` | yes | `audit_event` rows deleted |

`integer` (to 2 147 483 647) is enough for one run's counts in a school.
Nullable because every other action has no counts; a default of 0 would make a
sign-in row claim it purged nothing.

### 2.3 Action

New `AuditAction.RetentionPurgeRun`, code **`retention_purge_run`** (`varchar(64)`,
existing column). `ck_audit_event_action` is re-created with the value added.

### 2.4 Constraints (new)

| Name | Expression | Why |
|---|---|---|
| `ck_audit_event_purge_counts` | `(action = 'retention_purge_run') = (purged_courses IS NOT NULL AND purged_leaver_memberships IS NOT NULL AND purged_participants IS NOT NULL AND purged_accounts IS NOT NULL AND purged_audit_rows IS NOT NULL)` | all five on a purge row, none half-filled; and a non-purge row carries none… |
| `ck_audit_event_purge_counts_absent` | `action = 'retention_purge_run' OR (purged_courses IS NULL AND purged_leaver_memberships IS NULL AND purged_participants IS NULL AND purged_accounts IS NULL AND purged_audit_rows IS NULL)` | …enforced in both directions: a sign-in row with one stray count fails |
| `ck_audit_event_purge_counts_non_negative` | each column `IS NULL OR >= 0` (one constraint, five conjuncts) | VR-003 |
| `ck_audit_event_purge_actor` | `action <> 'retention_purge_run' OR (actor_type = 'system' AND target_type IS NULL AND target_id IS NULL AND outcome = 'succeeded' AND request_id IS NULL)` | FR-010: actor `system`, no target, succeeded (I-7), no request id |

Existing constraints already force `actor_id` and `actor_role` null for a
`system` actor and `refusal_category` null for `succeeded`, so they are not
repeated. `ck_audit_event_immutable` is unchanged.

### 2.5 Index

`ix_audit_event_occurred_at` on `(occurred_at)`. It serves FR-008's
`DELETE … WHERE occurred_at < @cutoff`. `audit_event` is the one table that
grows with every sign-in and, later, every export, for N years, so a sequential
scan per day would grow without bound. This is the first index on the table;
the US-008 comment that deferred it to the purge is corrected (FR-018).

## 3. `course_membership` — leavers

Query (FR-005), per kept course or over all of them:

```sql
SELECT id, course_id, participant_id FROM course_membership
WHERE on_roster = false AND last_seen_at < @cutoff
```

**Index:** `ix_course_membership_off_roster_last_seen` on `(last_seen_at)`
`WHERE on_roster = false` (partial). Off-roster rows are a minority, so the
partial index is small and leads straight to the expired ones. The existing
`ix_course_membership_course_seen` leads with `course_id` and cannot serve a
search across courses.

The submissions of a leaver in that course (FR-005):

```sql
DELETE FROM submission s USING course_work w
WHERE s.course_work_id = w.id AND w.course_id = @courseId AND s.participant_id = @participantId
```

served by the existing `ix_submission_participant_id` and `course_work`'s PK.

## 4. Expired courses — no new index

FR-002 needs, per course, the latest of its own dates. The store reads them in
**one** grouped query and returns the raw maxima; the "latest of, nulls ignored,
fallback to `created_at`, strictly earlier than the cutoff" rule is applied in
`Application` by the single shared definition (spec FR-002), not in SQL:

```sql
SELECT c.id, c.update_time, c.created_at,
       (SELECT max(w.creation_time) FROM course_work w WHERE w.course_id = c.id),
       (SELECT max(w.update_time)   FROM course_work w WHERE w.course_id = c.id),
       (SELECT max(s.update_time)   FROM submission s JOIN course_work w ON w.id = s.course_work_id
                                    WHERE w.course_id = c.id)
FROM course c
```

Served by `ix_course_work_course_item_date` / `uq_course_work_course_resource_google_id`
(leading `course_id`) and `uq_submission_course_work_google_id` (leading
`course_work_id`). A school has hundreds of courses; once a day this is cheap,
and a stored "last activity" column was rejected: synchronization would have to
maintain it on every write and it would be a second copy of the rule.

Deleting one expired course (FR-003), child first, in one transaction:

```sql
DELETE FROM submission s USING course_work w WHERE s.course_work_id = w.id AND w.course_id = @id;
DELETE FROM course_work WHERE course_id = @id;
DELETE FROM course_membership WHERE course_id = @id;
DELETE FROM course WHERE id = @id;
```

## 5. Orphaned participants, accounts

```sql
DELETE FROM classroom_participant p
WHERE NOT EXISTS (SELECT 1 FROM course_membership m WHERE m.participant_id = p.id);
```

served by `ix_course_membership_participant_id`. If a `submission` still named
such a participant, `Restrict` fails the statement and the step rolls back
(spec FR-006, FR-009) — deliberately not worked around.

```sql
DELETE FROM app_user WHERE coalesce(last_successful_sign_in_at, created_at) < @cutoff;
```

**No index on `app_user`.** The table holds a handful of Admin and Dean
accounts; an index would never be chosen. The US-012 comment that reserved one
for the purge is corrected to say so (FR-018). No other table has a foreign key
to `app_user`, so nothing blocks or cascades (PC-9).

## 6. How the deletes run

- **Set-based**: EF Core `ExecuteDeleteAsync` from the store in
  `Infrastructure`, each returning its affected-row count, which becomes the
  count of spec FR-010. No entity is loaded to be deleted.
- **Transactions** are owned by the use case through
  `IUnitOfWork.ExecuteInTransactionAsync` (AD-7): one per expired course (§4
  four statements), one per kept course with leavers (§3 two statements), one
  each for participants, accounts and audit rows (spec FR-009, I-4). Counts are
  added to the run's totals only after their transaction commits (VR-003).
- **Read-only mode.** Set-based deletes do not pass through
  `IUnitOfWork.SaveChangesAsync`, so the commit backstop (`ReadOnlyModeUnitOfWork`)
  never sees them. That is acceptable only because the deletes exist on **one
  port used by one use case**, which declares
  `PermittedServiceWrite.RetentionPurge` via `ServiceWriteScope` for the whole
  run and is registered in `PermittedServiceWrites.Declarations`. The run's
  audit row is added through `IAuditEventRepository` and committed with
  `SaveChangesAsync` inside that scope, so the backstop admits it in read-only
  mode.
- **Audit deletion is confined.** `IAuditEventRepository` stays add-only; the
  audit-row delete lives on the purge port only (SC-11, PC-9).
- **Ordering inside a run** follows spec FR-009, so the run's audit row is
  inserted after the audit-row delete and can never be caught by it.

## 7. Migration `AddRetentionPurge`

1. `ALTER TABLE audit_event ADD` the five columns (nullable, no default).
2. Drop and re-create `ck_audit_event_action` with `retention_purge_run`.
3. Add the four constraints of §2.4. Existing rows have all five null and a
   non-purge action, so they satisfy them; no data step.
4. `CREATE INDEX ix_audit_event_occurred_at`.
5. `CREATE INDEX ix_course_membership_off_roster_last_seen … WHERE on_roster = false`.

`Down` reverses it. No `EnsureCreated()`, no hand-written schema outside the
migration (PC-2). Migration name follows the existing ones under
`src/ClassroomAgent.Infrastructure/Persistence/Migrations/`.

## 8. Sensitive data

- Deleted rows include personal data of students (submissions with grades,
  participant names and emails) and account credentials (Dean password hash) —
  they are removed physically, which is the purpose (§5 v36).
- The new columns hold integers only. Nothing the purge writes or logs carries a
  name, email, grade or Google id (SC-10).
- The service-account key is not involved (PC-9).

## 9. Tests the design implies

Against real PostgreSQL (TC-2):

- the migration applies on a database holding US-008 … US-017 rows; old rows
  satisfy the new constraints;
- `ck_audit_event_purge_counts` / `_absent` / `_non_negative` / `_purge_actor`
  each reject a violating insert;
- each delete of §3 … §5 removes exactly the expected rows and nothing else,
  with `Restrict` FKs intact (no cascade added);
- a failure injected inside one course's transaction leaves that course whole;
- the boundary (`= cutoff` kept, one tick earlier deleted) for course, leaver,
  account and audit row;
- the audit-row delete removes the rows older than the cutoff and nothing newer
  (PC-11).
