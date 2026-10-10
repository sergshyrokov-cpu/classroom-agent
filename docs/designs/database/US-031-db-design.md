---
artifact_type: database_design
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T05:54:15Z
updated_at: 2026-10-10T05:54:15Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/designs/api/US-031-api-design.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 87
supersedes: null
---

# US-031 Database Design — Pull Meet events and keep history beyond 180 days

**Verdict: PASS.**

Delegation: a read-only `quick-look` agent reported the current
`ClassroomAgentDbContext`, the configurations of `sync_state`, `audit_event`,
`course` and `classroom_participant`, the latest migrations, `RetentionPurgeCounts`
and `RetentionPurgeStore`; the two constraints this design changes were then read
directly.

## 1. Tables touched

| Table | Change | Spec |
|---|---|---|
| `meet_session` | **new** | FR-005, FR-008, FR-009, FR-013 |
| `meet_participation` | **new** | FR-008, FR-009, FR-013 |
| `sync_state` | + `meet_loaded_up_to`, + `failed_step` | FR-007, FR-010 |
| `audit_event` | + `purged_meet_sessions`, + `purged_meet_participations` | FR-013 |

No change to `course`, `classroom_participant`, `course_membership` or any other
table. `meeting_code_link` is **not** created here — it is US-032's (OD-001).
Conventions: snake_case, singular tables, `pk_`/`uq_`/`fk_`/`ix_`/`ck_` names
(PC-5), every string with a maximum length, every nullability explicit (PC-4),
`created_at`/`updated_at` timestamptz stamped by the existing interceptor (PC-6).

## 2. `meet_session`

One row per Google conference (PC-3, PC-12, BR-063).

### 2.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | bigint identity | no | `pk_meet_session` |
| `conference_id` | varchar(128) | no | natural key (VR-001) |
| `meeting_code` | varchar(64) | no | as Google returned it, trimmed (VR-001, AC-014) |
| `organizer_email` | varchar(254) | no | a domain account's email (FR-005); never empty — a meeting without one is not stored |
| `started_at` | timestamptz | no | earliest `joined_at` of its participations (FR-008) |
| `ended_at` | timestamptz | no | latest `joined_at + duration` of its participations (FR-008, I-2) |
| `created_at`, `updated_at` | timestamptz | no | PC-6 |

No course column and no foreign key to `course` or `classroom_participant` (PC-8,
PC-12): a meeting reaches its course only through `meeting_code_link` (US-032).

### 2.2 Constraints

- `uq_meet_session_conference_id` — unique index on `conference_id` (PC-3; the
  upsert key, AC-003).
- `ck_meet_session_ended_after_started` — `ended_at >= started_at`.
- `ck_meet_session_values` — `char_length(conference_id) >= 1 AND
  char_length(meeting_code) >= 1 AND char_length(organizer_email) >= 3`
  (blank values never reach the table; VR-001).

### 2.3 Indexes

- `ix_meet_session_started_at` — the purge deletes by meeting date once a day
  over a table that grows for N years (FR-013), and US-033's period filters start
  from it.
- `ix_meet_session_meeting_code_started_at` — (`meeting_code`, `started_at`),
  the "code + date" index PC-7 names for Meet statistics. Added now, with the
  table, so US-032/US-033 add no migration of this table for it.

## 3. `meet_participation`

One row per connection, keyed by (conference, endpoint) (PC-3).

### 3.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | bigint identity | no | `pk_meet_participation` |
| `meet_session_id` | bigint | no | FK → `meet_session.id` |
| `endpoint_id` | varchar(128) | no | (VR-001) |
| `email` | varchar(254) | yes | a domain account's email; **null = "other participant"** (FR-008, AC-005) |
| `joined_at` | timestamptz | no | event time − duration (FR-008, I-3) |
| `duration_seconds` | integer | no | 0 … 86 400 (VR-001) |
| `created_at`, `updated_at` | timestamptz | no | PC-6 |

**The "other participant" mark is `email IS NULL`.** PC-12 speaks of a flag; a
separate boolean would carry the same fact twice and need a constraint to keep
the two in step. The entity exposes it as a computed `IsOtherParticipant`
(entity model §2). There is no name column, no display name, no `is_external`,
no device, location or telemetry column (AC-013), and no foreign key to
`classroom_participant` (PC-12, BR-051).

The (conference, endpoint) natural key of the Story is held as
(`meet_session_id`, `endpoint_id`): the session row is the conference, one to one.

### 3.2 Constraints

- `fk_meet_participation_meet_session_id` — `ON DELETE RESTRICT` (PC-8); the
  purge deletes children first (§6).
- `uq_meet_participation_meet_session_id_endpoint_id` — unique on
  (`meet_session_id`, `endpoint_id`) (PC-3, AC-003); it also serves as the
  foreign key's index (PC-7).
- `ck_meet_participation_duration` — `duration_seconds BETWEEN 0 AND 86400`.
- `ck_meet_participation_values` — `char_length(endpoint_id) >= 1 AND (email IS
  NULL OR char_length(email) >= 3)`.

### 3.3 Indexes

- `ix_meet_participation_email_joined_at` — (`email`, `joined_at`), the
  "email + date" index PC-7 names for Meet statistics and the leaver expiry of
  US-032. Rows with a null email are indexed too; no partial index (the
  provider's filter syntax gains nothing here).

## 4. `sync_state` — watermark and failed step

### 4.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `meet_loaded_up_to` | timestamptz | yes | the end of the last successful Meet pull (FR-007); null = never |
| `failed_step` | varchar(16) | yes | `classroom` / `meet` (FR-010, I-5) |

`meet_loaded_up_to` lives on the singleton row because it is synchronization
state read with the rest of the block (FR-011) and written in the same save as
`CompleteRun`. It is outside `ck_sync_state_terminal_fields` like
`last_successful_run_at`: any status may carry an earlier watermark.

### 4.2 Constraints (new)

- `ck_sync_state_failed_step` — `failed_step IS NULL OR (status = 'failed' AND
  failed_step IN ('classroom', 'meet'))`. A failed row written before this Story
  keeps a null step (I-5); `ck_sync_state_terminal_fields` is **not** changed, so
  no existing row can violate anything.
- `ck_sync_state_meet_loaded_up_to` — `meet_loaded_up_to IS NULL OR
  last_successful_run_at IS NOT NULL`: a watermark is only ever written by a
  completed run (FR-007).

## 5. `audit_event` — two purge counts

### 5.1 Columns

| Column | Type | Null |
|---|---|---|
| `purged_meet_sessions` | integer | yes |
| `purged_meet_participations` | integer | yes |

### 5.2 Constraints

Audit rows are never updated (SC-11, `ck_audit_event_immutable`), so purge rows
written before this Story cannot be given the new counts. The existing
`ck_audit_event_purge_counts` (the five US-037 counts) is therefore **left
unchanged**, and the new pair has its own rules:

- `ck_audit_event_purge_meet_counts` — `(purged_meet_sessions IS NULL) =
  (purged_meet_participations IS NULL)` — both or neither.
- `ck_audit_event_purge_meet_counts_absent` — `action = 'retention_purge_run' OR
  (purged_meet_sessions IS NULL AND purged_meet_participations IS NULL)`.
- `ck_audit_event_purge_meet_counts_non_negative` — each null or `>= 0`.

Every purge row written after this Story carries both counts — enforced by the
domain factory (entity model §4), which takes non-nullable counts. A purge row
with both null is a pre-Story row. No migration backfills or touches existing
audit rows.

## 6. How the purge deletes meetings

Added to `IRetentionPurgeStore` / `RetentionPurgeStore` (entity model §5),
set-based with `ExecuteDeleteAsync` as the existing deletes:

1. select up to **500** `meet_session.id` with `started_at < cutoff`, ordered by
   `id` (uses `ix_meet_session_started_at`);
2. in **one transaction** (AD-7, FR-013): delete the `meet_participation` rows of
   those ids, then those `meet_session` rows; return both counts;
3. repeat until a batch returns no id.

A meeting and its participations always go in the same transaction, so a meeting
is never left half-deleted; the batch bounds the transaction on the first purge
after a long period. The cutoff is the purge's existing one (now − N years); the
"meeting's own date" is `started_at` (FR-013). The purge runs in read-only mode
as before (BR-026).

## 7. How the pull writes

Per page of events read (entity model §6):

1. load the `meet_session` rows of the page's conference ids with their
   `meet_participation` rows (one query, `uq_meet_session_conference_id`);
2. apply FR-005/FR-009 in memory — new sessions, new or updated participations,
   then recompute `started_at`/`ended_at` over **all** participations of each
   touched session;
3. save in one transaction per page (AD-7).

A session's participations are all loaded, so start/end are computed over old and
new connections together (AC-002). A page is bounded by Google's page size (≤
1 000 events), so a transaction stays bounded. On a unique-index violation from a
concurrent writer — impossible today, since runs never overlap (US-013) — the
run fails as `Unexpected`; no special handling.

## 8. Migration `AddMeetPull`

One migration (PC-2): creates `meet_session`, `meet_participation` with their
constraints and indexes; adds the two `sync_state` columns and two constraints;
adds the two `audit_event` columns and three constraints. All new columns on
existing tables are nullable with no default, so existing rows stay valid with no
data change. `Down` drops exactly these. Applied at the deployment step, never at
startup (PC-2).

## 9. Sensitive data

- `meet_participation.email` and `meet_session.organizer_email` are **personal
  data** of staff and students, potentially minors (PC-9). Only domain accounts'
  emails are stored (§3 v87); guests and accountless connections leave no
  identity. Never logged (SC-10, spec FR-012). Deleted only by the purge, N years
  after the meeting (BR-066).
- `conference_id`, `meeting_code`, `endpoint_id` are Google identifiers, not
  personal on their own; not logged either (spec I-6).
- No name, display name, IP, device or location is stored anywhere (AC-013).
- No key, credential or secret reference is added (PC-9).

## 10. Tests the design implies

- Migration applies to an empty database and to one holding pre-Story
  `sync_state` (failed, null step) and `audit_event` purge rows (null Meet
  counts) — both stay valid.
- Each new check constraint rejects a violating insert (Testcontainers, TC-2).
- Unique (conference) and unique (session, endpoint) reject duplicates.
- `RESTRICT` blocks deleting a session that still has participations.
- The purge deletes exactly the sessions with `started_at < cutoff` and their
  participations, across more than one batch, and returns the counts.
