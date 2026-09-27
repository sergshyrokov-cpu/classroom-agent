---
artifact_type: database_design
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T17:30:24Z
updated_at: 2026-09-27T17:30:24Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-014 Database Design — Sync courses and rosters

## 1. Summary of the schema change

**Three new tables, one migration** (`AddCoursesAndRosters`), no change to any
existing table.

| | Before | After |
|---|---|---|
| Installation migrations | 6 | **7** |
| Installation table set (as `AppUserMigrationTests` counts it, `__EFMigrationsHistory` included) | 6 | **9** |
| Control Plane | untouched | untouched |

New tables: `course`, `classroom_participant`, `course_membership`.

`sync_state` gains **no column**: the run counter it already has now means "courses
processed" (Specification FR-013, OD-005), which is a change of meaning, not of
schema. `audit_event` needs no amendment, because a scheduled run writes no audit
row (FR-019) — the same position US-013 held, and unlike US-009, US-011 and US-012.

## 2. Why this is the Story that makes the database hold teaching data

Everything before it stored configuration and state: one connection, one legitimacy
record, accounts, audit rows, one sync-run row. This is the first Story whose tables
hold **personal data of students, potentially minors** (§6), and the first whose
rows the retention purge will delete (PC-11). Two consequences run through the whole
design:

- the purge's unit is the **course**, and a leaver's expiry is their membership's
  own `last_seen_at` (PC-11) — so those columns exist and are indexed here even
  though US-037 writes the purge;
- nothing exposes these rows yet. There is no endpoint, page or export until EPIC-2
  (Specification FR-018, S-08), so the design is judged on storage correctness, not
  on query shapes that do not exist.

## 3. `course` — the new table

### 3.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate PK, `pk_course` (PC-3) |
| `google_id` | `varchar(64)` | no | Classroom course id; **the upsert key**, `uq_course_google_id` (PC-3, FR-008) |
| `name` | `varchar(750)` | no | §3; the only course string the Specification calls required (VR-002). 750 is Classroom's own documented bound |
| `section` | `varchar(2800)` | yes | §3; Classroom's bound |
| `description_heading` | `varchar(3600)` | yes | Classroom's bound. §3's field list is not exhaustive and the prototype stored heading and description separately (`db.py:87`), so both are kept |
| `description` | `varchar(30000)` | yes | Classroom's bound |
| `room` | `varchar(650)` | yes | §3; Classroom's bound |
| `owner_google_id` | `varchar(64)` | **yes** | §3's `owner`, as a plain value with **no foreign key** (OD-004). Nullable — see §3.3 |
| `creation_time` | `timestamptz` | yes | Google's, stored as given in UTC (I-1) |
| `update_time` | `timestamptz` | yes | Google's. The future age rule reads this (§3.5) |
| `course_state` | `varchar(16)` | no | closed vocabulary, `ck_course_course_state` (§3.2) |
| `alternate_link` | `varchar(2048)` | yes | §3's "ссылка" |
| `teacher_folder_id` | `varchar(128)` | yes | §3's "папка преподавателя" |
| `teacher_folder_title` | `varchar(750)` | yes | idem |
| `calendar_id` | `varchar(256)` | yes | §3 |
| `created_at` | `timestamptz` | no | PC-6, interceptor-stamped |
| `updated_at` | `timestamptz` | no | PC-6 |

String bounds follow Classroom's own documented maxima wherever it publishes one, so
that VR-002's truncation is a safety net rather than routine data loss. Truncation
happens in the entity before the write, never as a database error (VR-002; the
`SyncState.LastError` precedent).

### 3.2 Check constraints

- **`ck_course_course_state`** — `course_state IN ('active', 'archived',
  'provisioned', 'declined', 'suspended')`. The five §3 values, stored as lower-case
  codes exactly as `sync_state.status` stores its three (the
  `SyncStateConfiguration` precedent: a value converter between the enum and the
  code, and a check constraint that keeps the column honest).

  This constraint is the database half of OD-010. The Application half is that a
  course whose state Classroom reports outside the five is **skipped before the
  write**, with one `Warning` line, so the constraint should never be the thing that
  rejects it (FR-003). A violation reaching the database would mean the skip was not
  implemented.

- **No other check constraint.** Unlike `sync_state`, this table has no internal
  agreement to enforce: its columns are independent facts from Google, and there is
  no status-plus-nullable-fields combination that could contradict itself.

### 3.3 Why `owner_google_id` is nullable

Classroom returns `ownerId` on every course in practice, and §3 names the owner
among a course's fields. It is nullable regardless, for the reason OD-011 made the
participant's email non-unique: a column that no requirement reads must not be able
to fail a school's import. OD-004 already established that **US-022 reads
memberships, not this column** — the owner's own teacher membership arrives from the
roster like anyone else's — so a missing value costs nothing, while `NOT NULL` would
turn an unexpected Google response into a course that cannot be stored.

The same reasoning makes `creation_time` and `update_time` nullable. See §3.5 for
what that obliges the age rule to do.

### 3.4 What is deliberately not added

- **No "gone from Google" column.** §3 defines five states and nothing else, and
  FR-011 leaves such a course untouched (I-4). A sixth state or a `present` flag
  would be a business rule no artifact defines.
- **No index besides `uq_course_google_id`.** PC-7 asks for an index on every column
  a repository query uses as a lookup key; this Story's only lookup is by
  `google_id`, which the unique index covers. Epic 2's filters — status, teacher,
  name search (§4 Epic 2) — are US-020's queries, and the indexes that serve them
  should be chosen against those queries rather than guessed now. The prototype's
  `idx_courses_state` / `idx_courses_owner` / `idx_courses_update` (`db.py`) are
  recorded as a hint for US-020, not copied.
- **No foreign key to `classroom_participant` for the owner** (OD-004, §3.3).
- **No retention or age column.** OD-001 keeps the age rule out of this Story, and
  PC-11's "last activity" is computed from several tables rather than stored.
- **No `MeetingCodeLink` and no `MeetSession`** — Epic 4 (PC-8 defines their shape
  when US-031 arrives).

### 3.5 What the future age rule inherits

OD-001 deferred the "do not import a course older than N years" rule. Because
`update_time` is nullable (§3.3), the Story that implements the rule must treat a
**missing** `update_time` as "age unknown" and **import** the course, not refuse it:
refusing on absent evidence is the failure mode OD-001 rejected. PC-11's last
activity is also the latest of four dates, three of which live in tables that do not
exist yet (`course_work`, `submission`, `meet_session`), so the rule cannot be
implemented until at least US-015.

## 4. `classroom_participant` — the new table

### 4.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate PK, `pk_classroom_participant` (PC-3) |
| `google_user_id` | `varchar(64)` | no | Google `userId`; **the upsert key** and the person's only identity, `uq_classroom_participant_google_user_id` (PC-3, OD-011) |
| `email` | `varchar(320)` | **yes** | §3's "личный email"; **personal data**. Optional (OD-006), **non-unique** (OD-011). 320 = 64 local + `@` + 255 domain |
| `full_name` | `varchar(750)` | yes | §3's "имя"; **personal data**. One string, not split (I-2) |
| `created_at` | `timestamptz` | no | PC-6 |
| `updated_at` | `timestamptz` | no | PC-6 |

Stored lower-cased and trimmed when present, the way `WorkspaceConnection`
normalises an address, so Epic 4's matching is not defeated by case (VR-003).

### 4.2 Indexes

- **`uq_classroom_participant_google_user_id`** — unique. Identity, and the upsert
  key (PC-3).
- **`ix_classroom_participant_email`** — **not unique** (OD-011). It exists because
  PC-7 requires an index for "participant email + date range" — Epic 4 resolves a
  Meet participant by email against the roster on the meeting's date (PC-12,
  BR-051). The date half of that composite lives on `course_membership` (§5.3), so
  the two indexes are read together.

  **The absence of uniqueness here is a decision, not an omission.** PC-3's sentence
  "natural keys such as email get a unique index" is written for keys the program
  owns; this address is Google's, it may be absent, and Google permits a deleted
  account's address to be reused by a new `userId`. A unique index would turn that
  legitimate reuse into a failed import for the whole school. A future migration
  adding `IsUnique()` to this index would reintroduce exactly the defect OD-011
  avoided.

### 4.3 No role column

The person carries no Classroom role — it lives on the membership (BR-050, §3,
FR-006). A migration adding a role column to this table is a modelling defect, and
the entity model states the same (entity-model §2).

### 4.4 Personal data

`email` and `full_name` are personal data of students, potentially minors (PC-9,
S-08). Handling rules:

- stored in plain form, because the reports must show a person by name and address
  (§4 Epic 3, Epic 4) — this is not a credential and hashing it would make the
  product useless;
- never written to a log: SC-10 permits an **internal** id in a log line and nothing
  more (FR-016);
- never sent anywhere but this database: SC-13's destinations are Google and the
  Control Plane, and the Control Plane receives none of this (S-05);
- deleted only by the retention purge (PC-11): a participant is deleted when no
  membership references them, and a leaver's membership expires on its own
  `last_seen_at`. US-037 owns that;
- no read path exists until EPIC-2 (S-08).

## 5. `course_membership` — the new table

### 5.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate PK, `pk_course_membership` (PC-3) |
| `course_id` | `bigint` | no | FK → `course.id`, `fk_course_membership_course` |
| `participant_id` | `bigint` | no | FK → `classroom_participant.id`, `fk_course_membership_classroom_participant` |
| `role` | `varchar(16)` | no | closed vocabulary, `ck_course_membership_role` (§5.2) |
| `first_seen_at` | `timestamptz` | no | when synchronization **first** saw the person on this roster (BR-051, FR-009) |
| `last_seen_at` | `timestamptz` | no | when it **last** saw them (BR-051) |
| `on_roster` | `boolean` | no | whether they are on it **now** (BR-051) |
| `created_at` | `timestamptz` | no | PC-6 |
| `updated_at` | `timestamptz` | no | PC-6 |

All three observation columns are `NOT NULL`: unlike a Google field, each is the
program's own record of when it looked (I-3), so there is no case in which one is
unknown.

### 5.2 Check constraints

- **`ck_course_membership_role`** — `role IN ('teacher', 'student')`. Exactly the two
  §3 values (FR-007, VR-004); the same enum-to-code conversion as `course_state` and
  `sync_state.status`.
- **`ck_course_membership_seen_order`** — `last_seen_at >= first_seen_at`. The pair
  cannot be written out of order; FR-009 never moves `first_seen_at`, and a run that
  re-sees a person only advances `last_seen_at`.

Deliberately **not** constrained: `on_roster` against the dates. It is tempting to
require that `on_roster = false` implies a `last_seen_at` in the past, but "in the
past" needs a clock and a row-level check has none — and the legitimate shape of a
row is "seen in this very run and still on the roster", where the two are equal. The
entity owns that invariant instead (entity-model §4.2), the same division US-013 made
for `SyncState.LastSuccessfulRunAt`.

### 5.3 Indexes

- **`uq_course_membership_course_participant`** — unique on `(course_id,
  participant_id)`. **This is PC-8's rule and the correction Specification v2 made**:
  one participation per person per course, the role a field on it, not part of its
  identity (FR-007, VR-004, I-9). The Application resolves a person appearing on both
  of a course's rosters to a single `teacher` membership **before** the write, so this
  index is a guard against a defect, not a path the import is expected to take.
- **`ix_course_membership_course_seen`** — `(course_id, first_seen_at,
  last_seen_at)`. PC-7 requires roster-on-a-date lookups on "course +
  `first_seen_at` / `last_seen_at`", which is how BR-051 answers "who was on this
  roster on the date of that meeting". A composite in this order serves both the
  equality on the course and the range on the dates; the unique index above cannot,
  because it has no date column.
- **`ix_course_membership_participant_id`** — the second foreign key needs its own
  index (PC-7: index every foreign key column). The first, `course_id`, leads both
  indexes above and needs no third.

`course_id` leading `uq_course_membership_course_participant` is also what the purge
will use to delete a course's memberships (PC-11).

### 5.4 Cascade behaviour

Both foreign keys are **`DeleteBehavior.Restrict`**, PC-8's default.

`Cascade` from `course` would be convenient for the purge, which deletes a course
with everything under it — but PC-8's reason for the default applies exactly here:
"Student grades must not disappear because a parent row was removed." When US-015
hangs submissions off these rows, a cascade from `course` would silently take grades
with it. The purge deletes in an explicit order inside one transaction instead
(US-037), which is a visible decision rather than a database side effect.

### 5.5 What is deliberately not added

- **No `MeetParticipation` foreign key to `classroom_participant`.** PC-12 calls such
  a migration a finding; the person is resolved by email against the roster on the
  meeting's date (BR-051). That table does not exist yet, and when Epic 4 creates it,
  this prohibition still stands.
- **No index for the purge's own scan** — `(on_roster, last_seen_at)` would serve
  PC-11's leaver expiry, but that query is US-037's and does not exist. Recorded here
  so US-037 adds it against its real query rather than inheriting a guess.
- **No off-roster timestamp.** BR-051 records "when we last saw them", and the moment
  we noticed their absence is not a fact §3 asks for; it would also be
  indistinguishable from the run instant.
- **No membership history.** §3 stores the current observation, not a log of
  arrivals and departures, exactly as OD-004 of US-013 kept one `SyncState` row.

## 6. The migration

One migration, `AddCoursesAndRosters`, under
`src/ClassroomAgent.Infrastructure/Persistence/Migrations/` (PC-2):

- creates `course`, `classroom_participant`, `course_membership` with every column,
  check constraint, unique index and foreign key above;
- creation order is `course`, `classroom_participant`, then `course_membership`,
  which depends on both;
- `Down` drops them in the reverse order;
- **touches no existing table** — no column added to `sync_state`, no constraint
  amended on `audit_event`;
- installation migrations become **seven**, with `_AddCoursesAndRosters` last;
- applied by the explicit deployment step, never at start-up (PC-2, DC-2). No
  `EnsureCreated()`, no `ExecuteSqlRaw` schema change.

## 7. Sensitive data

| Column | Classification | Rule |
|---|---|---|
| `classroom_participant.email` | personal data, possibly of a minor | §4.4; plain storage, never logged, never exported until EPIC-2 designs it, deleted only by the purge |
| `classroom_participant.full_name` | personal data, possibly of a minor | idem |
| `course.name`, `course.section`, `course.description*`, `course.room` | school data, not personal | not a person's data, but SC-10 still keeps a course **name** out of logs — a log line carries the internal id (FR-016) |
| `course_membership.*` | reveals who studies or teaches where | the role and the dates are personal in combination; the same rules apply |

No credential, token, key or secret reference appears in any of the three tables. A
migration adding one would be a Critical finding (PC-9, SC-7). Nothing here is
hashed, because none of it is a secret — the product exists to show these facts to
the Dean.

## 8. Constraints checklist (PC conventions)

| Convention | How this design satisfies it |
|---|---|
| PC-1 PostgreSQL, Testcontainers | three ordinary tables; the InMemory provider cannot enforce any constraint below, so §9 requires Testcontainers |
| PC-2 migrations only | one migration, `AddCoursesAndRosters`; no `EnsureCreated()` |
| PC-3 identifiers | `bigint` identity `id` on all three; Google ids in their own columns with unique indexes and never as a primary key |
| PC-4 explicit mapping | every column's nullability and every string's `HasMaxLength` stated above; one configuration class per entity |
| PC-5 naming | `snake_case` singular tables; `pk_`/`uq_`/`fk_`/`ix_`/`ck_` prefixes as listed |
| PC-6 timestamps | `created_at` / `updated_at` on all three, `timestamptz`, interceptor-stamped; all Google times stored in UTC as given (I-1) |
| PC-7 indexes | unique indexes on both Google ids; both foreign keys indexed; the roster-on-a-date composite; email indexed non-uniquely for Epic 4 |
| PC-8 relationships | `CourseMembership` is the explicit entity between the two, **unique on (course, participant)**, carrying the role and the three observations; `Restrict` on both foreign keys |
| PC-9 sensitive data | §7; no key, credential or secret reference; personal columns marked with their handling rules |
| PC-10 idempotent sync | upsert on `google_id` / `google_user_id`; the unique indexes are what make repeated runs safe (FR-008) |
| PC-11 retention | the purge's unit is the course, and a leaver expires on `last_seen_at`; both columns exist and `course_id` leads the index the purge will use. The age rule itself is deferred (OD-001, §3.5) |
| PC-12 Meet data | no foreign key from `MeetParticipation` to `ClassroomParticipant`, now or later (§5.5) |

## 9. For TEST_WRITING

Database-level guards, all against real PostgreSQL through Testcontainers — the
InMemory provider enforces none of them (TC-2):

1. **A duplicate Google course id is rejected** (`uq_course_google_id`), and a second
   import of the same course **updates** the row rather than adding one (FR-008).
2. **A duplicate Google `userId` is rejected** (`uq_classroom_participant_google_user_id`).
3. **Two participants may share an email address** — this must **succeed**. It is the
   positive assertion of OD-011, and it fails the moment someone adds `IsUnique()`.
4. **A second membership of the same person in the same course is rejected**
   (`uq_course_membership_course_participant`) — the guard behind Specification v2's
   correction (FR-007, I-9).
5. **A role outside `teacher` / `student` is rejected** (`ck_course_membership_role`).
6. **A `course_state` outside the five codes is rejected** (`ck_course_course_state`),
   *and* at the Application level a course Classroom reports with an unrecognised
   state is **skipped** while the run completes (OD-010, FR-003) — the constraint
   must never be what stops it.
7. **`last_seen_at` earlier than `first_seen_at` is rejected**
   (`ck_course_membership_seen_order`).
8. **A course with no `owner_google_id`, `creation_time` or `update_time` is
   storable** (§3.3) — the nullability is deliberate and a later `NOT NULL` would
   break imports.
9. **Deleting a course with memberships is refused** (`Restrict`, §5.4), so the purge
   must delete explicitly and in order.
10. **The migration set and table set grew as expected**: `AppUserMigrationTests`
    goes from six to **seven** migrations with `_AddCoursesAndRosters` last, and its
    table set gains `course`, `classroom_participant`, `course_membership` — an
    **expected change to an existing test**, as US-013 made the same one.
    `TheControlPlaneSchema_IsUnchangedByThisStory` must keep passing untouched.
11. **`sync_state` is unchanged** — no column added; the model has no pending changes
    against the migrations (`TheModel_HasNoPendingChangesAgainstTheMigrations`).
12. **`audit_event` gains no row** from a scheduled run (FR-019) and no member.

The BR-051 sequence — seen, gone, seen again — and the both-rosters tie-break are
behaviour, not schema; they belong to the Application tests the Specification's
testing notes already require.

## 10. Open questions

None raised by this stage at attempt 2. The contradiction that blocked attempt 1 —
the Specification fixing `CourseMembership` uniqueness as `(course, person, role)`
against PC-8's `(course, participant)` — was resolved by the Owner in PC-8's favour
and corrected in Specification v2; this design implements v2.

All eleven Open Decisions are resolved (open-decisions v1). Two are load-bearing for
this schema and are cited where they act: OD-010 at `ck_course_course_state` (§3.2)
and OD-011 at `ix_classroom_participant_email` (§4.2).
