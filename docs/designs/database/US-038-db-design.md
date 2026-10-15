---
artifact_type: database_design
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T06:05:00Z
updated_at: 2026-10-15T06:05:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 2
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: trebovaniya.md
    version: 93
supersedes: null
---

# US-038 Database Design — Erase all data of one participant on request

Installation database only (AD-1, PC-1). The Control Plane database is untouched.

## 1. Tables touched

| Table | Change |
|---|---|
| `erased_participant` | **new** — the exclusion (spec FR-008) |
| `erasure_salt` | **new** — the installation's salt, one row (spec FR-010) |
| `meet_session` | `organizer_email` becomes nullable; check relaxed; expression index (spec FR-007, I-9) |
| `meet_participation` | expression index on `lower(email)` (spec FR-006) |
| `course` | index on `owner_google_id` (spec FR-007) |
| `audit_event` | two actions, two target types, five count columns, shape checks (spec FR-013) |
| `classroom_participant`, `course_membership`, `submission` | no schema change; rows deleted by the erasure |

## 2. `erased_participant`

### 2.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | bigint identity | no | `pk_erased_participant`; the id the lifting audit records |
| `google_user_id` | varchar(64) | no | as `classroom_participant.google_user_id` (same length) |
| `email_fingerprint` | bytea | yes | HMAC-SHA-256 of the normalized email with the salt, 32 bytes; null when the participant had no email (OD-001 a) |
| `erased_at` | timestamptz | no | the erasure date (UTC), set by the use case from the clock |
| `created_at`, `updated_at` | timestamptz | no | PC-6 |

No name, no email, no reference to the deleted participant (spec FR-008, S-05).
No foreign key: nothing it could point to survives the erasure.

### 2.2 Constraints

- `uq_erased_participant_google_user_id` — unique on `google_user_id`. One
  exclusion per Google user id: a second erasure of the same id is impossible
  while the first exclusion stands, because synchronization never recreates
  the participant (FR-012). Also the last line of defence against a double
  insert by two concurrent erasures (§7).
- `ck_erased_participant_values` —
  `char_length(google_user_id) >= 1 AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = 32)`.
- `ck_erased_participant_immutable` — `updated_at = created_at`: an exclusion is
  created and deleted, never changed.

### 2.3 Indexes

- `uq_erased_participant_google_user_id` (above) — the synchronization lookup
  "which of these Google user ids are excluded" (`= ANY(@ids)`).
- `ix_erased_participant_email_fingerprint` — **not** unique (spec I-10: two
  exclusions may share an email); the Meet-load lookup and the lifting.

### 2.4 Retention

The purge never touches this table (spec FR-017, §3 v93). PC-11's list of purged
tables does not gain it.

## 3. `erasure_salt`

### 3.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | bigint identity | no | `pk_erasure_salt` |
| `salt` | bytea | no | 32 bytes from `RandomNumberGenerator` |
| `singleton` | boolean | no | shadow property, default `true` (pattern of `sync_state`, `legitimacy_state`) |
| `created_at`, `updated_at` | timestamptz | no | PC-6 |

### 3.2 Constraints

- `ck_erasure_salt_singleton` — `singleton`.
- `uq_erasure_salt_singleton` — unique on `singleton`: at most one row.
- `ck_erasure_salt_values` — `octet_length(salt) = 32`.
- `ck_erasure_salt_immutable` — `updated_at = created_at`: never changed (spec
  FR-010 — a new salt would make every exclusion unmatchable).

### 3.3 Generation

- Not seeded by the migration (the database has no `pgcrypto`, and the salt
  must come from the installation's own code — Story Notes).
- Created **lazily by the first erasure**, inside its transaction:
  `INSERT … ON CONFLICT (singleton) DO NOTHING`, then read the row. Two
  concurrent first erasures therefore agree on one salt.
- Lifting and the Meet load only **read** it. No row → no fingerprinted
  exclusion can exist → lifting answers "no exclusion", the Meet load skips the
  check.
- No read-only path writes it: the only writer is the erasure, refused in
  read-only mode.
- Never shown, logged, exported or sent to the Control Plane (spec S-05). It is
  in database backups, which is consistent: a restore brings back the salt
  together with the exclusions made with it.

## 4. `meet_session`

- `organizer_email` — varchar(254), **nullable** (was `NOT NULL`). Null means the
  organizer was erased or excluded (spec FR-007, FR-012).
- `ck_meet_session_values` — the term `char_length(organizer_email) >= 3`
  becomes `organizer_email IS NULL OR char_length(organizer_email) >= 3`; the
  constraint's other terms are unchanged.
- `ix_meet_session_organizer_email_lower` — expression index on
  `lower(organizer_email)`, created by `migrationBuilder.Sql` (EF Core cannot
  express it; same approach as `ix_classroom_participant_email_lower`, US-032).
  Serves the erasure's count and clearing.

## 5. `meet_participation`

- `ix_meet_participation_email_lower` — expression index on `lower(email)`,
  created by `migrationBuilder.Sql`. Serves the erasure's count and deletion. The
  existing `ix_meet_participation_email_joined_at` stays.

## 6. `course`

- `ix_course_owner_google_id` — on `owner_google_id` (PC-7: a lookup key of the
  erasure's count and clearing). No other change.

## 7. How the erasure writes — and the write fence (spec FR-011)

### 7.1 The transaction

One `ExecuteInTransactionAsync` (default READ COMMITTED), in this order:

1. **Exclusive write fence** — `SELECT pg_advisory_xact_lock(@key)` (§7.3).
2. Delete the participant row's dependants and the row, set-based
   (`ExecuteDeleteAsync` / `ExecuteUpdateAsync`, as `RetentionPurgeStore`), each
   returning its row count:
   - `meet_participation` where `lower(email) = @normalizedEmail` (skipped when
     the participant has no email);
   - `submission` where `participant_id = @id`;
   - `course_membership` where `participant_id = @id`;
   - `meet_session` set `organizer_email = NULL` where
     `lower(organizer_email) = @normalizedEmail` (skipped with no email);
   - `course` set `owner_google_id = NULL` where `owner_google_id = @googleUserId`;
   - `classroom_participant` where `id = @id`. **0 rows → the participant was
     erased meanwhile: roll back, `404`** (spec FR-016).
3. Get or create the salt (§3.3); insert `erased_participant`.
4. Add the audit row; `SaveChanges`; commit.
5. After commit: the `Information` log line.

The participant row is read (for its Google user id and email) **after** the
fence, inside the transaction. `@normalizedEmail` is the participant's email
normalized as `WorkspaceConnection.NormalizeEmail` (spec VR-004).

### 7.2 Why a fence

Synchronization and the Meet load commit per course / per page, each in its own
READ COMMITTED transaction. Without coordination, a run could check "not
excluded", the erasure could commit, and the run could then insert the person
again (spec FR-011, AC-010). Serializable isolation would need retries in every
writer; a database trigger cannot check a fingerprint without `pgcrypto`.

### 7.3 The fence

One PostgreSQL transaction-scoped advisory lock with a fixed key
(`ErasureFenceKey`, a `long` constant in Infrastructure, value `0x4552415345`):

| Who | Lock | Taken |
|---|---|---|
| Erasure | `pg_advisory_xact_lock(key)` — exclusive | first statement of its transaction |
| Synchronization, per course transaction | `pg_advisory_xact_lock_shared(key)` | first statement of the transaction, before reading exclusions |
| Meet load, per page transaction | `pg_advisory_xact_lock_shared(key)` | likewise |

Shared locks never block each other, so synchronization and the Meet load run as
today. An erasure waits for in-flight writer transactions to commit (so it sees
and deletes what they stored) and writers wait for an erasure to commit (so
their exclusion read, which happens after the lock, sees the new row). Locks
are released at commit or rollback; nothing can leak.

Lifting does **not** take the fence: removing an exclusion only permits more,
and a run that still sees it skips the person until the next run.

The SQL is issued through a port (§9 of the entity model) by `ExecuteSqlRawAsync`
— a query, not a schema change (PC-2 unaffected).

### 7.4 What the writers read after the fence

- **Synchronization**, per course: the set of excluded Google user ids among the
  course's roster and submitters —
  `SELECT google_user_id FROM erased_participant WHERE google_user_id = ANY(@ids)`.
- **Meet load**, per page: the salt (once per run is enough — it never changes),
  the fingerprints of the page's distinct normalized emails (participants and
  organizers) computed in the application, then
  `SELECT email_fingerprint FROM erased_participant WHERE email_fingerprint = ANY(@fps)`.

## 8. How the lifting writes

One transaction: compute the fingerprint (no salt row → "no exclusion", nothing
written); `DELETE FROM erased_participant WHERE email_fingerprint = @fp RETURNING id`
(through EF: load the matching rows, remove them); one audit row per removed id;
commit. Zero rows → "no exclusion", no audit row.

## 9. `audit_event`

### 9.1 New values

- `ck_audit_event_action` gains `'participant_erased'` and
  `'erasure_exclusion_lifted'` (`AuditAction.ParticipantErased`,
  `AuditAction.ErasureExclusionLifted`; `ActionCode()` / `ActionFromCode()`
  extended).
- `ck_audit_event_target_type_value` gains `'classroom_participant'` and
  `'erased_participant'` (`AuditTargetType.ClassroomParticipant`,
  `AuditTargetType.ErasedParticipant`). `target_id` is a bare id; the row it
  named no longer exists, by design.

### 9.2 New columns

All `integer`, nullable, set only on a succeeded `participant_erased` row:

| Column | Counts (spec FR-013) |
|---|---|
| `erased_memberships` | `course_membership` rows deleted |
| `erased_submissions` | `submission` rows deleted |
| `erased_meet_participations` | `meet_participation` rows deleted |
| `cleared_meet_organizers` | `meet_session` rows whose organizer email was cleared |
| `cleared_course_owners` | `course` rows whose owner was cleared |

### 9.3 New checks

Two checks, mirroring `ck_audit_event_purge_counts` / `_absent`:

- `ck_audit_event_erasure_counts` — a succeeded erasure has all five:
  `NOT (action = 'participant_erased' AND outcome = 'succeeded') OR (erased_memberships IS NOT NULL AND erased_submissions IS NOT NULL AND erased_meet_participations IS NOT NULL AND cleared_meet_organizers IS NOT NULL AND cleared_course_owners IS NOT NULL)`.
- `ck_audit_event_erasure_counts_absent` — every other row has none:
  `(action = 'participant_erased' AND outcome = 'succeeded') OR (erased_memberships IS NULL AND erased_submissions IS NULL AND erased_meet_participations IS NULL AND cleared_meet_organizers IS NULL AND cleared_course_owners IS NULL)`.
- `ck_audit_event_erasure_counts_non_negative` — each of the five `IS NULL OR >= 0`.
- `ck_audit_event_erasure_shape` —
  `action <> 'participant_erased' OR (actor_type = 'app_user' AND actor_role = 'admin' AND (outcome = 'refused' OR (target_type = 'classroom_participant' AND target_id IS NOT NULL)))`.
- `ck_audit_event_exclusion_lift_shape` —
  `action <> 'erasure_exclusion_lifted' OR (actor_type = 'app_user' AND actor_role = 'admin' AND (outcome = 'refused' OR (target_type = 'erased_participant' AND target_id IS NOT NULL)))`.

The literal codes for `actor_type`, `actor_role` and `outcome` are those the
existing checks already use; the implementation copies them from
`AuditEventConfiguration`, not from this text.

### 9.4 Refusals

A read-only refusal writes one row with `outcome = refused`,
`refusal_category = read_only_mode`, no counts; for an erasure `target_type` /
`target_id` name the requested participant id (as the Dean refusal names the
dean id), for a lifting both are null (spec I-12). No row ever carries a name,
an email, a Google user id or a fingerprint (spec S-06).

## 10. How the pages read

- **Search** (spec FR-002): one query over `classroom_participant` with
  `lower(coalesce(email,'')) LIKE @p OR lower(coalesce(full_name,'')) LIKE @p OR …`
  (`@p` = `'%' || escaped lower(term) || '%'`, `ESCAPE '\'`), ordered, `LIMIT 51`
  (the 51st row sets `truncated`), joined to a grouped count of
  `course_membership` by role. A contains-search cannot use a B-tree index; the
  table holds one row per person of the school (thousands), so a sequential scan
  is bounded. No trigram extension is added.
- **Confirmation** (spec FR-003): the participant by id; its memberships joined
  to `course` for names; five counts — `submission` (and with a grade),
  `meet_participation` by `lower(email)`, `meet_session` by
  `lower(organizer_email)`, `course` by `owner_google_id`. Each count is backed by
  an index (`ix_submission_participant_id`, the two expression indexes,
  `ix_course_owner_google_id`). A fixed number of round trips.

## 11. Migration `AddParticipantErasure`

One migration, in this order: create `erased_participant` and `erasure_salt`
with their constraints and indexes; alter `meet_session.organizer_email` to
nullable and replace `ck_meet_session_values`; create
`ix_meet_session_organizer_email_lower`, `ix_meet_participation_email_lower`
(`migrationBuilder.Sql`, with `DROP INDEX` in `Down`), `ix_course_owner_google_id`;
add the five audit columns; replace `ck_audit_event_action` and
`ck_audit_event_target_type_value`; add the new audit checks. No data changes.
`Down` restores `NOT NULL` on `organizer_email` (it fails if a null exists — an
accepted one-way step once erasures happened, stated in the migration comment).

## 12. Sensitive data

| Data | Handling |
|---|---|
| Name, email | deleted with the participant; never copied into the exclusion, audit or log |
| Google user id | kept in `erased_participant` only (accepted trade-off, §3 v93); never in audit or log |
| Email fingerprint | bytea, not reversible without the salt and a guess; never shown, logged or audited |
| Salt | one row, never changed, never shown, logged, exported or sent |

## 13. Conventions checked

PC-2 (one migration, no `EnsureCreated`), PC-3 (bigint identity), PC-4 (explicit
lengths and nullability; `bytea` lengths by check), PC-5 (snake_case,
`uq_`/`ix_`/`pk_` names; `ck_` as the existing tables), PC-6 (timestamps via the
interceptor; `erased_at` is a business date, not a stamp), PC-7 (every lookup
column indexed), PC-8 (no new foreign key; deletions explicit and child-first),
PC-11 (purge unchanged).

## 14. Tests the design implies

- Migration up on a database with data: `meet_session` rows keep their emails.
- `uq_erased_participant_google_user_id` and the `erasure_salt` singleton reject
  a second row; the immutable checks reject an update.
- Audit checks reject an erasure row without counts and a non-erasure row with
  counts.
- Fence: an erasure committed between a synchronization's read from Google and
  its course transaction leaves no participant row (spec AC-010) — driven by
  holding the exclusive lock in a test transaction while the writer starts.
- Two concurrent erasures of one participant: one exclusion, one `404`.

## 15. Delegation

Read only, `quick-look`: `persistence-conventions.md`, `artifact-schema.md`, the
US-032 and US-018 database designs (format), `UnitOfWork.ExecuteInTransactionAsync`,
grep for locks and isolation levels; `AuditEventConfiguration`, the `AuditEvent`
factories and enums, `MeetSessionConfiguration` and every reader of
`OrganizerEmail`, `CourseConfiguration` and `OwnerGoogleId` readers,
`ClassroomParticipantConfiguration`, `MeetParticipationConfiguration`, the
singleton pattern of `SyncState` / `LegitimacyState`, the latest migration and
the DbContext, collations.
