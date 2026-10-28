---
artifact_type: database_design
story: US-065
version: 1
status: DRAFT
created_at: 2026-10-28T11:49:36Z
updated_at: 2026-10-28T11:49:36Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-065-spec.md
    version: 2
  - path: docs/designs/api/US-065-api-design.md
    version: 1
  - path: docs/designs/api/US-065-openapi.yaml
    version: 1
  - path: docs/decisions/US-065-open-decisions.md
    version: 2
  - path: docs/designs/database/US-038-db-design.md
    version: null
supersedes: null
---

# US-065 Database Design — An exclusion entered by email before the person is loaded

Delegation: none. Read directly: `ErasedParticipant`,
`ErasedParticipantConfiguration`, `AuditEventConfiguration` (action and shape
checks), `AuditEvent` erasure factories, `TimestampInterceptor`,
`IErasedParticipantRepository`, `IErasureFence`, `IErasureSaltStore`,
`WorkspaceConnection.NormalizeEmail`, `ClassroomParticipantConfiguration`
(`ix_classroom_participant_email_lower`), US-038 db-design §2, §7, §9.

## 1. Summary

| Change | Where |
|---|---|
| `google_user_id` becomes nullable | `erased_participant` |
| `ck_erased_participant_values` → id or fingerprint required | `erased_participant` |
| `ck_erased_participant_immutable` replaced by a `BEFORE UPDATE` trigger allowing only the one fill | `erased_participant` |
| New audit action `erasure_exclusion_entered` + shape check | `audit_event` |
| No new table, no new column, no new index | — |

One migration, `AddErasureExclusionInAdvance` (PC-2). The salt table, the fence
and the Meet load are unchanged.

## 2. `erased_participant`

### 2.1 Columns after the change

| Column | Type | Null | Change |
|---|---|---|---|
| `id` | `bigint` identity | no | — |
| `google_user_id` | `varchar(MaxGoogleUserIdLength)` | **yes** | was `NOT NULL` |
| `email_fingerprint` | `bytea` (32 bytes) | yes | — |
| `erased_at` | `timestamptz` | no | — (holds the entry date for an exclusion entered in advance, spec I-3; not renamed — no screen shows it, and a rename would touch US-038 code for no behaviour) |
| `created_at`, `updated_at` | `timestamptz` | no | — |

Personal data: none readable. The fingerprint is a keyed hash of an address
(US-038 db-design §3); the Google user id is an opaque Google identifier, held
only after the match (spec S-05).

### 2.2 Checks

- `ck_erased_participant_values` (replaced):
  `(google_user_id IS NULL OR char_length(google_user_id) >= 1)
   AND (email_fingerprint IS NULL OR octet_length(email_fingerprint) = 32)
   AND (google_user_id IS NOT NULL OR email_fingerprint IS NOT NULL)`.
  The last clause is spec FR-003: an exclusion with neither could never match.
- `ck_erased_participant_immutable` (`updated_at = created_at`) is **dropped**:
  a filled row has `updated_at > created_at`. Its guarantee moves to the trigger
  (§2.3), which states it exactly.

### 2.3 Trigger `tg_erased_participant_fill_only`

`BEFORE UPDATE ON erased_participant FOR EACH ROW EXECUTE FUNCTION
erased_participant_fill_only()`, the function raising
`check_violation` unless **all** hold:

- `OLD.google_user_id IS NULL AND NEW.google_user_id IS NOT NULL`;
- `NEW.id = OLD.id`, `NEW.email_fingerprint IS NOT DISTINCT FROM OLD.email_fingerprint`
  (and therefore not null — §2.2), `NEW.erased_at = OLD.erased_at`,
  `NEW.created_at = OLD.created_at`.

`updated_at` may change (it records the fill). So: an exclusion created by an
erasure (id set at insert) can never be updated; an exclusion entered in
advance can be updated exactly once, only by gaining a Google user id, and is
unchangeable afterwards (spec FR-007, AC-015).

Why a trigger, not a check: a check sees one row version and cannot tell "the
first fill" from "a later change of the id"; only `OLD`/`NEW` can. Why this is
safe to introduce: plain PL/pgSQL, no extension (US-038 db-design §7.2 rejected
a trigger only because a fingerprint check would need `pgcrypto`). EF Core with
Npgsql needs no model annotation for a trigger. Created and dropped by
`migrationBuilder.Sql` in the migration, as the expression index of US-032 was;
the configuration class carries a comment naming it (the model snapshot cannot).

### 2.4 Indexes

- `uq_erased_participant_google_user_id` stays. On a nullable column PostgreSQL
  allows any number of `NULL`s, so many unmatched exclusions coexist; at most
  one exclusion per Google id still holds (spec I-2).
- `ix_erased_participant_email_fingerprint` stays and serves both the entry's
  "already excluded" check and the sync match (§4). The table holds one row per
  erasure request — tens, not thousands; a partial index on unmatched rows
  would buy nothing (NFR-003 is about bounded round trips, which §4 keeps).

## 3. The entry transaction (spec FR-002, FR-011)

Inside one READ COMMITTED transaction:

1. `pg_advisory_xact_lock(ErasureFenceKey)` — the **exclusive** side of the
   US-038 fence (`IErasureFence.EnterAsErasureAsync`). It waits for in-flight
   synchronization and Meet transactions and holds new ones off until commit
   (FR-011, AC-012).
2. Loaded check: participants whose `lower(email) = @normalized` (served by
   `ix_classroom_participant_email_lower`), every participant, on a roster or
   not, returned in the US-038 search shape (§5 of the entity model). Any →
   rollback, outcome "already loaded".
3. `IErasureSaltStore.GetOrCreateAsync` — the US-038 insert-on-conflict; then
   the fingerprint of `NormalizeEmail(email)` in the application.
4. `SELECT 1 FROM erased_participant WHERE email_fingerprint = @fp LIMIT 1`. Any
   → rollback, outcome "already excluded". The salt created in step 3 rolls
   back with it — and cannot have been created there, since with no salt no
   fingerprinted exclusion exists (API contract `x-writes`).
5. Insert one `erased_participant` (fingerprint, `google_user_id NULL`,
   `erased_at` = now) and one `audit_event` (§6); commit.

Two Admins entering the same address at once: the exclusive lock serializes
them; the second sees the first row in step 4 and answers "already excluded".

Normalization of the stored side: step 2 compares `lower(email)`; the entered
side is `NormalizeEmail` (trim, lower case, domain trailing dot removed). A
stored email differing only by surrounding spaces or a trailing dot in the
domain would not be found; Google returns neither, and US-032's lookup by email
makes the same comparison.

## 4. Synchronization (spec FR-006 … FR-008)

In each course transaction, after `EnterAsWriterAsync` (shared fence) and
where `GetExcludedGoogleUserIdsAsync` runs today:

1. **Salt**: `IErasureSaltStore.FindAsync` once per run (it never changes once
   created; created only under the exclusive fence, which a running course
   transaction excludes). No salt → no exclusion entered in advance can exist;
   steps 2–4 skipped.
2. **Candidates**: the course's roster entries (teachers and students) that
   have an email in Google's answer; fingerprint of each normalized email,
   computed in the application.
3. **Match** — one query:
   `SELECT id, email_fingerprint FROM erased_participant
    WHERE google_user_id IS NULL AND email_fingerprint = ANY(@fps)`.
4. **Stored-id guard** (FR-008, OD-003 a) — one query over the matched entries'
   Google ids: `SELECT google_user_id FROM classroom_participant WHERE
   google_user_id = ANY(@ids)`. A matched entry whose id is stored is **not**
   treated as excluded and fills nothing.
5. The remaining matched entries' Google ids join today's excluded-id set for
   this course; every existing skip (participant, membership, submission,
   submitter, owner) then applies unchanged (FR-006).
6. **Fill** — per matched exclusion, the Google id chosen by FR-007 (ordinally
   first among that fingerprint's remaining matched entries), skipped when
   that id is in the set `GetExcludedGoogleUserIdsAsync` returned — i.e. already
   held by another exclusion (I-2); step 5's additions do not count here:
   `UPDATE erased_participant SET google_user_id = @gid, updated_at = @now
    WHERE id = @id AND google_user_id IS NULL`
   by `ExecuteUpdateAsync`. `@now` comes from the injected `TimeProvider`
   because `ExecuteUpdate` bypasses `TimestampInterceptor` (PC-6).

Round trips per course: at most three added (match, stored-id guard, one
`UPDATE` per filled exclusion — normally zero, at most a handful once per
person in a whole installation's life). Bounded (NFR-003).

Concurrency with **lifting**, which takes no fence (US-038 db-design §7.3): a
lifting that commits between steps 3 and 6 deletes the row; the `UPDATE`
affects zero rows, which is not an error (that is why it is a guarded
set-based `UPDATE`, not a tracked entity save that would throw a concurrency
exception and fail the course). The person is still skipped in this course;
the next run loads them as new (US-038 I-11 applies).

Two **unmatched** exclusions with one fingerprint cannot arise: an erasure
always sets the Google id, and the entry refuses a fingerprint that already has
an exclusion (§3 step 4, under the exclusive fence). The fill loop is still
written defensively: exclusions in `id` order, each filled id added to the
excluded-id set before the next, so the unique index is never hit.

## 5. Meet load

Unchanged: `GetExcludedFingerprintsAsync` already matches every exclusion with
a fingerprint, filled or not (spec FR-009, AC-005).

## 6. `audit_event`

### 6.1 New value

- `ck_audit_event_action` gains `'erasure_exclusion_entered'`
  (`AuditAction.ErasureExclusionEntered`; `ActionCode()` / `ActionFromCode()`
  extended). `target_type` reuses `'erased_participant'`.

### 6.2 New check

- `ck_audit_event_exclusion_entry_shape` —
  `action <> 'erasure_exclusion_entered' OR (actor_type = 'app_user' AND
   actor_role = 'admin' AND (outcome = 'refused' OR (target_type =
   'erased_participant' AND target_id IS NOT NULL)))`
  — the same shape as `ck_audit_event_exclusion_lift_shape`. The literal codes
  are copied from `AuditEventConfiguration`, not from this text.
- The US-038 erasure count checks already require counts to be absent on every
  row other than a succeeded `participant_erased`; nothing to add.

### 6.3 Refusal

A read-only refusal: `outcome = refused`, `refusal_category = read_only_mode`,
`target_type` / `target_id` null — as a refused lifting (US-038 db-design §9.4,
spec FR-013). No row carries an email, a fingerprint or a Google user id
(SC-11). The fill by synchronization writes no audit row (spec FR-007).

## 7. Migration `AddErasureExclusionInAdvance`

Up:

1. `ALTER COLUMN google_user_id DROP NOT NULL`;
2. drop and re-create `ck_erased_participant_values` (§2.2);
3. drop `ck_erased_participant_immutable`;
4. `migrationBuilder.Sql`: create function `erased_participant_fill_only()` and
   trigger `tg_erased_participant_fill_only` (§2.3);
5. drop and re-create `ck_audit_event_action` with the new value; add
   `ck_audit_event_exclusion_entry_shape`.

Down — the exact reverse: drop the trigger and function; delete nothing but
fail if a row with `google_user_id IS NULL` exists (re-adding `NOT NULL` would
fail anyway — the migration states it rather than deleting exclusions, which
would undo erasures on request); restore the two checks; restore the action
check without the value (fails if an `erasure_exclusion_entered` audit row
exists — audit rows are never deleted outside the purge, PC-9).

Existing rows satisfy every new rule (all have a Google id; none was updated).

## 8. Rationale for choices not forced by the Specification

| Choice | Alternative rejected |
|---|---|
| Trigger for the one fill | A `matched_at` column + check: still cannot forbid a second update |
| Keep `erased_at` name | Rename to `excluded_at`: touches US-038 code and tests, no visible gain |
| Guarded `ExecuteUpdate` for the fill | Tracked entity update: a concurrent lifting would throw and fail the course |
| No new index | Partial index on unmatched rows: table too small to matter |
| Stored-id guard as a separate query | Join in the match query: the guard needs the roster's ids, not the exclusion's |
