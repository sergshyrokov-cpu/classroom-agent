---
artifact_type: database_design
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T13:48:55Z
updated_at: 2026-10-10T13:48:55Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/designs/api/US-032-api-design.md
    version: 1
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-032 Database Design — Link meeting codes to courses

**Verdict: PASS.**

Delegation: none at this stage. The configurations of `audit_event`,
`sync_state`, `course`, `course_membership`, `classroom_participant`, the latest
migration (`20261010064711_AddMeetPull`), `RetentionPurgeStore`,
`IRetentionPurgeStore` and `CourseActivityDates` were read directly; the code
facts gathered by `quick-look` at SPECIFICATION were reused.

## 1. Tables touched

| Table | Change | Spec |
|---|---|---|
| `meeting_code_link` | **new** | FR-001, FR-006, FR-008 … FR-013, FR-016 |
| `audit_event` | + `meet_code`, + `meet_previous_course_id`, + `purged_meet_code_links`; six new action codes | FR-014, FR-016 |
| `sync_state` | `failed_step` may be `linking` | FR-006 |

No change to `meet_session`, `meet_participation`, `course`,
`course_membership`, `classroom_participant`. Conventions as before: snake_case,
singular tables, `pk_`/`uq_`/`fk_`/`ix_`/`ck_` names (PC-5), explicit lengths and
nullability (PC-4), `created_at`/`updated_at` by the interceptor (PC-6).

**No table stores shares or candidates.** They are computed from current data
when needed (§5), as API design §3 left to this stage: a stored score would go
stale with every roster change between runs (spec I-8), would need its own purge
rule, and nothing reads it often enough to need caching.

## 2. `meeting_code_link`

One row per meeting code that is **linked or marked**; an unassigned code has no
row (spec FR-001). The "not a course" mark is **a row with no course** (Story
Notes left the choice here): it keeps the "a code is in at most one state" rule
in one unique index, and moving between linked and marked is an update of one
row, never a delete-and-insert that a concurrent writer could interleave.

### 2.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | bigint identity | no | `pk_meeting_code_link` |
| `meeting_code` | varchar(64) | no | as on `meet_session` (US-031 VR-001) |
| `course_id` | bigint | yes | FK → `course.id`; null = marked "not a course" |
| `linked_automatically` | boolean | yes | set exactly when linked |
| `linked_by_app_user_id` | bigint | yes | the person who made the link; null for an automatic one; bare id, no FK (PC-11) |
| `linked_at` | timestamptz | yes | when the current link was made |
| `confirmed_by_app_user_id` | bigint | yes | bare id, no FK |
| `confirmed_at` | timestamptz | yes | |
| `marked_by_app_user_id` | bigint | yes | who set the mark; bare id, no FK |
| `marked_at` | timestamptz | yes | |
| `concurrency_stamp` | varchar(64) | no | new value on every change; EF Core concurrency token (as `app_user`) — spec FR-013 |
| `created_at`, `updated_at` | timestamptz | no | PC-6 |

### 2.2 Constraints

- `uq_meeting_code_link_meeting_code` — unique on `meeting_code`: a code
  belongs to at most one course and has at most one state (§3, PC-8). It also
  makes two concurrent first decisions on one code impossible (spec FR-013).
- `fk_meeting_code_link_course_id` — `ON DELETE RESTRICT` (PC-8); the purge
  deletes links before the course (§6).
- `ck_meeting_code_link_state` — exactly one of the two shapes:
  ```sql
  (course_id IS NOT NULL AND linked_automatically IS NOT NULL AND linked_at IS NOT NULL
     AND marked_by_app_user_id IS NULL AND marked_at IS NULL)
  OR
  (course_id IS NULL AND linked_automatically IS NULL AND linked_at IS NULL
     AND linked_by_app_user_id IS NULL AND confirmed_by_app_user_id IS NULL
     AND confirmed_at IS NULL
     AND marked_by_app_user_id IS NOT NULL AND marked_at IS NOT NULL)
  ```
- `ck_meeting_code_link_maker` — `linked_automatically IS NULL OR
  linked_automatically = (linked_by_app_user_id IS NULL)`: an automatic link
  has no person, a person's link always does.
- `ck_meeting_code_link_confirmation` — `(confirmed_by_app_user_id IS NULL) =
  (confirmed_at IS NULL) AND (confirmed_at IS NULL OR linked_automatically)`:
  only an automatic link is ever confirmed (spec I-9).
- `ck_meeting_code_link_values` — `char_length(meeting_code) >= 1 AND
  char_length(concurrency_stamp) >= 1`.

### 2.3 Indexes

- `uq_meeting_code_link_meeting_code` (above) — the anti-join "codes with no
  link" (§5) and every lookup by code.
- `ix_meeting_code_link_course_id` — the foreign key's index (PC-7); the purge
  and last-activity query go from course to its codes.

`meet_session` already has `ix_meet_session_meeting_code_started_at` (US-031 §2.3),
which serves code → meetings and the per-code first/last date and count.

## 3. `audit_event`

### 3.1 New action codes

`meet_code_auto_linked`, `meet_code_course_picked`, `meet_code_link_confirmed`,
`meet_code_relinked`, `meet_code_marked_not_a_course`, `meet_code_mark_removed`
— added to `ck_audit_event_action` (dropped and recreated with the full list).

### 3.2 New columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `meet_code` | varchar(64) | yes | the meeting code (spec FR-014, OD-008) |
| `meet_previous_course_id` | bigint | yes | the course before the change: re-link's old course, marking's previous course; bare id, no FK |
| `purged_meet_code_links` | integer | yes | purge count of links removed (spec FR-016 rule 5, I-13) |

The course **after** the change is the row's target: `target_type = 'course'`,
`target_id` = the course (existing columns, as the export row does). Marking has
no course after it, so its target is null.

| Action | Actor | target (course) | `meet_previous_course_id` |
|---|---|---|---|
| `meet_code_auto_linked` | `system` | linked course | null |
| `meet_code_course_picked` | person | chosen course | null |
| `meet_code_link_confirmed` | person | the link's course | null |
| `meet_code_relinked` | person | new course | old course |
| `meet_code_marked_not_a_course` | person | null | previous course, or null for an unassigned code |
| `meet_code_mark_removed` | person | chosen course | null |

A **refused** row (read-only, spec FR-014, API design §2.5) carries the action,
the person, `refused` / `read_only_mode`, the code when it passed its shape, and
**no course ids** (they are unverified input).

### 3.3 New constraints

- `ck_audit_event_meet_code_absent` — `action IN (<six>) OR (meet_code IS NULL
  AND meet_previous_course_id IS NULL)`.
- `ck_audit_event_meet_code_shape` —
  ```sql
  action NOT IN (<six>) OR (
    (actor_type = 'system') = (action = 'meet_code_auto_linked')
    AND (action <> 'meet_code_auto_linked' OR (outcome = 'succeeded' AND request_id IS NULL))
    AND (refusal_category IS NULL OR refusal_category = 'read_only_mode')
    AND (target_type IS NULL OR target_type = 'course')
    AND (target_type IS NULL) = (target_id IS NULL)
    AND (outcome = 'succeeded' OR (target_id IS NULL AND meet_previous_course_id IS NULL))
    AND (outcome <> 'succeeded' OR (meet_code IS NOT NULL
         AND (target_id IS NULL) = (action = 'meet_code_marked_not_a_course')))
    AND (meet_previous_course_id IS NULL
         OR action IN ('meet_code_relinked', 'meet_code_marked_not_a_course'))
    AND (action <> 'meet_code_relinked' OR outcome <> 'succeeded'
         OR meet_previous_course_id IS NOT NULL)
  )
  ```
- `ck_audit_event_meet_code_values` — `(meet_code IS NULL OR
  char_length(meet_code) >= 1) AND (meet_previous_course_id IS NULL OR
  meet_previous_course_id > 0)`.
- `ck_audit_event_purge_meet_code_links` — `(purged_meet_code_links IS NULL OR
  (action = 'retention_purge_run' AND purged_meet_code_links >= 0))`.

As in US-031 §5.2, purge rows written before this Story cannot gain the new
count (`ck_audit_event_immutable`), so the column is nullable and the existing
purge constraints are **not** changed; every purge row written after this Story
carries it — the domain factory takes a non-nullable count.

## 4. `sync_state`

`ck_sync_state_failed_step` is dropped and recreated as `failed_step IS NULL OR
(status = 'failed' AND failed_step IN ('classroom', 'meet', 'linking'))` (spec
FR-006). The column (`varchar(16)`) is unchanged.

## 5. How the data is read

### 5.1 Unassigned codes

`meet_session` codes with no `meeting_code_link` row (anti-join on
`meeting_code`), grouped by code: first and last `started_at`, meeting count,
distinct organizers. Counts for the list switcher are the same query counted.

### 5.2 Candidates and shares (spec FR-002, FR-003)

For a set of codes, the scoring source loads in bounded queries:

1. the codes' meetings: id, code, organizer email, `started_at`;
2. their participations with an email: meeting id, email;
3. the memberships whose participant email equals, **case-insensitively**, one
   of those emails (organizers and participants): course id, role,
   `first_seen_at`, `last_seen_at`, `on_roster`, participant email.

Matching and the roster-on-a-date rule run in memory in `Application` (a pure
function, entity model §4), with dates in the school's time zone. The query on
`classroom_participant.email` compares `lower(email)`; the existing
`ix_classroom_participant_email` is on the raw value, so the design adds
**`ix_classroom_participant_email_lower`** — an expression index on
`lower(email)` — so the lookup by a set of emails stays an index scan as the
participant table grows. (Raw-case matching would miss a participant whose email
Google returns in a different case in Classroom and in Meet; PC-12 matching is
case-insensitive.)

**Unassigned list ordering** (codes with candidates first) needs only
*whether* a code has a candidate: an `EXISTS` over organizer-teacher memberships
covering the meeting date, evaluated in SQL for all unassigned codes. Exact date
comparison in the school's zone is done in SQL through
`(started_at AT TIME ZONE <zone>)::date` against the membership's
`first_seen_at` / `last_seen_at` converted the same way — the same rule as the
in-memory function, which tests prove equal on boundary dates (§9). Shares are
then computed only for the codes on the requested page.

**The linking step** (spec FR-006) processes unassigned codes in batches of
**200 codes** ordered by code, loading items 1–3 per batch, so a run never holds
the whole Meet history in memory (spec §9).

### 5.3 Linked and marked lists

From `meeting_code_link` joined to `course` (linked) and aggregated
`meet_session` per code (count, last start; a code with no meeting left shows 0
and no date). Persons' emails come from `app_user` by bare id; a missing row is
the "deleted account" (spec I-7).

## 6. How the purge changes (spec FR-016)

Within `RetentionPurgeStore` (entity model §6):

1. **Last activity**: `CourseActivityDates` gains `LatestLinkedMeetingStart` —
   `max(meet_session.started_at)` over sessions whose `meeting_code` has a
   `meeting_code_link` with this `course_id`.
2. **Course deletion** (one transaction per course, AD-7), child-first:
   `meet_participation` of sessions reached through the course's links →
   those `meet_session` rows → the course's `meeting_code_link` rows → then the
   existing deletes (submissions, course work, memberships, course). Returns the
   counts of meetings, participations and links.
3. **Leaver expiry**: in the existing per-course transaction, before the
   memberships are deleted, delete `meet_participation` rows whose
   `lower(email)` equals the lower email of an expiring leaver of that course and
   whose session's code is linked to that course. The count adds to
   `purged_meet_participations`.
4. **Orphaned marks**: after the meeting-date rule (US-031), delete
   `meeting_code_link` rows with `course_id IS NULL` and no `meet_session` of
   their code. Set-based, one statement. The count adds to
   `purged_meet_code_links`.

Course links with no meeting left are not touched (spec FR-016 rule 4).
Meetings deleted by rule 2 add to `purged_meet_sessions` /
`purged_meet_participations`.

## 7. How writes are made

- **Linking step**: per code, one transaction: insert the link and its audit row
  (spec FR-006, AD-7). A unique violation on `uq_meeting_code_link_meeting_code`
  means a person decided the code since the batch was read: the step **skips that
  code** (no audit row) and continues — it is not a run failure.
- **Person's actions**: load the row by code (or its absence), compare with the
  expected state, change it, add the audit row, save — one transaction. A
  concurrent change of the same row fails the save on `concurrency_stamp`
  (`DbUpdateConcurrencyException`); a concurrent first insert fails on the unique
  index. Both are answered as the stale-state `409` (spec FR-013), not `500`. A
  concurrent purge that removed the chosen course fails the FK: answered as
  `404` course not found; one that removed the link row: a concurrency failure →
  `409`.

## 8. Migration `AddMeetingCodeLinks`

One migration (PC-2): creates `meeting_code_link` with its constraints and
indexes; adds `ix_classroom_participant_email_lower`; adds the three
`audit_event` columns and four constraints and recreates
`ck_audit_event_action`; recreates `ck_sync_state_failed_step`. All new columns
on existing tables are nullable with no default; existing rows stay valid with no
data change. `Down` reverses exactly these. Applied at deployment, never at
startup (PC-2).

## 9. Sensitive data

- `meeting_code_link` holds no personal data: a Google meeting code, a course id
  and internal account ids. The accounts' emails are read from `app_user` only to
  render the page for Dean and Admin.
- `audit_event` gains a code and course ids only — no email or name (SC-11).
- The scoring reads emails of participants, potentially minors (PC-9), in memory
  only for the computation; nothing derived from them is stored; nothing is
  logged (SC-10, spec §9).
- No key, credential or secret reference is added.

## 10. Tests the design implies

- Migration applies on an empty database and on one with pre-Story `audit_event`
  purge rows and failed `sync_state` rows; both stay valid.
- Each new check constraint rejects a violating row (Testcontainers, TC-2):
  both state shapes, maker, confirmation on a person's link, every audit shape
  rule, the purge count on a non-purge row, `linking` on a non-failed state.
- Unique code rejects a second row; `RESTRICT` blocks deleting a course with
  links.
- Concurrency stamp: two updates of one row — the second fails.
- The SQL "has a candidate" agrees with the in-memory rule on dates at the
  school-zone day boundary (first/last seen on the meeting's day, the day
  before, the day after).
- Purge: last activity counts a recent linked meeting; course deletion removes
  links, linked meetings and participations; leaver expiry removes only the
  leaver's participations in linked meetings (case-insensitively); orphaned marks
  go, course links with no meetings stay; counts match.
