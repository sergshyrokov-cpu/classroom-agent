---
artifact_type: database_design
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T13:10:02Z
updated_at: 2026-09-28T13:10:02Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/decisions/US-015-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-015 Database Design — Sync coursework and submissions

## 1. Summary of the schema change

**Two new tables and one migration** (`AddCourseWorkAndSubmissions`, PC-2):

| Table | Holds |
|---|---|
| `course_work` | both Classroom resources — `courseWork` (assignments) and `courseWorkMaterials` (materials) — in one table, per OD-008 |
| `submission` | one student's submission of one piece of work: its state, grades, last turn-in and `late` flag |

Installation migrations go from **seven to eight**, and the table set as
`AppUserMigrationTests` counts it from **nine to eleven**.

**No existing table is altered.** `course`, `classroom_participant` and
`course_membership` keep their shape, `sync_state` gains no column (FR-014),
`audit_event` needs no amendment (FR-020), and the Control Plane is untouched.

**Nothing in the schema carries the retention period.** FR-012 makes
`Retention:Years` a required *configuration* value read at startup; there is no
settings table and none is added (see §8).

## 2. Why these two tables complete Epic 1's data

US-014 gave the database courses and the people on them. Everything Epic 3 needs
beyond that is here: the journal is `student × (задание/материал с датой) ×
клетка` (§4 Epic 3), so its columns are `course_work` rows and its cells are
computed from `submission`. This is also the Story that finally makes a course's
**last activity** computable — PC-11 defines it as the latest of the course's own
update time, the creation or update of any of its coursework, and the update of
any of its submissions, and the last two arrive here.

## 3. `course_work` — the new table

### 3.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate PK, `pk_course_work` (PC-3) |
| `course_id` | `bigint` | no | FK → `course.id`, `fk_course_work_course` |
| `google_id` | `varchar(64)` | no | Classroom's id for the item; part of the upsert key (FR-010) |
| `resource` | `varchar(24)` | no | which Classroom resource it came from — closed vocabulary, `ck_course_work_resource` (§3.2) |
| `title` | `varchar(3000)` | no | §3's «заголовок»; Classroom's own documented bound. Truncated, not refused, if longer (VR-002) |
| `item_date` | `timestamptz` | no | the one date of §3's cascade `scheduledTime` → `dueDate` → `updateTime` → `creationTime` (FR-008) — see §3.3 |
| `due_at` | `timestamptz` | **yes** | §3's «срок сдачи (если задан)» — see §3.4 |
| `max_points` | `numeric(10,4)` | **yes** | §3's «максимум баллов»; absent means ungraded work (BR-052) |
| `creation_time` | `timestamptz` | yes | Google's, stored as given in UTC (I-1). PC-11 reads it |
| `update_time` | `timestamptz` | yes | Google's. PC-11 reads it |
| `created_at` | `timestamptz` | no | PC-6, interceptor-stamped |
| `updated_at` | `timestamptz` | no | PC-6 |

`numeric`, not `double precision`, for points: a grade is printed in a journal and
compared against a maximum, and binary floating point would surface as
`7.999999` in a school's document. Four decimal places is far beyond anything
Classroom reports and costs nothing.

### 3.2 Check constraints

- **`ck_course_work_resource`** — `resource IN ('course_work',
  'course_work_material')`. The two Classroom resources of §3 and BR-052, stored
  as lower-case codes through a value converter, the `SyncStateConfiguration`
  pattern US-014 also followed.
- **`ck_course_work_material_has_no_grading`** —
  `resource <> 'course_work_material' OR (max_points IS NULL AND due_at IS NULL)`.
  A material has no submissions, no points and no due date: the
  `courseWorkMaterials` resource does not carry those fields at all (§3, BR-052).
  The constraint keeps the single-table decision of OD-008 honest — one table for
  two resources must not let a material acquire grading columns.
- **`ck_course_work_max_points_non_negative`** —
  `max_points IS NULL OR max_points >= 0`. Data sanity on external input (VR-001),
  not a business rule: nothing in the requirements permits a negative maximum, and
  a negative value would corrupt every cell computed from it.

Deliberately **not** constrained: **graded versus ungraded**. It is derived from
`resource` plus `max_points` and is never stored (PC-3, FR-005, §3 v32), so there
is no column to constrain and a teacher adding points later updates the same row.

### 3.3 Why `item_date` is `NOT NULL`

The cascade of FR-008 ends at `creationTime`, which Classroom returns for every
item, so in practice a date always exists. It is `NOT NULL` because the journal
addresses a column *by its date* (§4 Epic 3) — a row without one could never
appear in the artefact this Story exists to make possible.

An item for which **all four** cascade sources are absent is therefore
**unimportable** and is refused with the missing-identifier cases of VR-002. This
is the one place where this design refuses rather than tolerates, and the reason
is that tolerating would store a row no report can use.

### 3.4 Why `due_at` is one column, not a date and a time

Classroom returns `dueDate` and `dueTime` as separate objects, and documents
`dueTime` as required whenever `dueDate` is present — so the pair is either both
or neither, and one `timestamptz` in UTC is faithful to it (BR-053, PC-6).

Recorded as a design decision with its fallback, because Google's behaviour here
is **not verified in this project**: if a response ever carries a date without a
time, the due date is stored as **absent** rather than guessed at midnight or at
end of day. Guessing would silently change BR-056's cell from «срок не задан» to
a wrong «не сдано» or «срок не наступил»; storing it as absent lands on a state
BR-056 already defines.

### 3.5 Indexes

- **`uq_course_work_course_resource_google_id`** — unique on
  `(course_id, resource, google_id)`. **This is Specification v2's correction.**
  The resource is in the key because ids from the two Classroom resources may
  collide (PC-3); the course is in it because Classroom documents `courseWork.id`
  as unique *per course*, so a key without it would let one Google-side
  repetition fail a whole school's import (§1 of the Specification, v2).
- **`ix_course_work_course_item_date`** — `(course_id, item_date)`. Required by
  PC-7, which names "composite indexes supporting the journal query (course +
  period)" — that query is exactly equality on the course and a range on the date.
- **No separate index on `course_id`**: it leads both indexes above, which
  satisfies PC-7's "index every foreign key column". It is also what the purge
  will use to delete a course's items (PC-11).

### 3.6 What is deliberately not added

- **No Classroom `state` column.** OD-004 imports only `PUBLISHED` items and does
  not store the state; a column would invite later Stories to filter on something
  §3 never asked to keep.
- **No «gone from Google» flag**, exactly as US-014 refused one for a course: an
  item Classroom stops returning keeps its row until the purge (AC-005).
- **No description column.** §3's field list for `CourseWork` is «заголовок, тип,
  дата, срок сдачи, максимум баллов» and no requirement reads a description; the
  Story's Scope adds nothing beyond §3. A future Story that needs it adds it with
  its own migration.
- **No purge-scan index** such as `(course_id, update_time)`. PC-11's query is
  US-037's and does not exist yet; US-014 made the same choice for its leaver scan,
  so US-037 indexes against its real query rather than inheriting a guess.

## 4. `submission` — the new table

### 4.1 Columns

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `bigint` identity | no | surrogate PK, `pk_submission` (PC-3) |
| `course_work_id` | `bigint` | no | FK → `course_work.id`, `fk_submission_course_work` |
| `participant_id` | `bigint` | no | FK → `classroom_participant.id`, `fk_submission_classroom_participant`. The person is matched by Google `userId` (I-7), never by email |
| `google_id` | `varchar(64)` | no | Classroom's submission id; part of the upsert key (FR-010) |
| `state` | `varchar(32)` | no | closed vocabulary of **seven** codes — the six of VR-004 plus `unrecognised`, `ck_submission_state` (§4.2) |
| `raw_state` | `varchar(64)` | **yes** | the string Google sent, kept **only** when `state = 'unrecognised'` (OD-005, §4.2) |
| `assigned_grade` | `numeric(10,4)` | yes | raw points as Google gave them (PC-13, VR-007) |
| `draft_grade` | `numeric(10,4)` | yes | idem; the student cannot see it yet (§3) |
| `turned_in_at` | `timestamptz` | yes | the **latest** transition to `TURNED_IN` (FR-009, BR-058); absent when the history carries none |
| `late` | `boolean` | no | Google's flag as Google computed it (§3, I-9). Google omits it when false, so absent reads as `false` |
| `update_time` | `timestamptz` | yes | Google's, per OD-007 — the value PC-11 needs for last activity |
| `created_at` | `timestamptz` | no | PC-6 |
| `updated_at` | `timestamptz` | no | PC-6 |

**Nothing else is stored.** No file, no answer, no attachment, no grade-change
history, no rubric grade (BR-059, PC-13, §3). The submission history is read in
memory by the adapter, reduced to `turned_in_at`, and discarded before the
Application boundary (entity-model §6).

### 4.2 Check constraints

- **`ck_submission_state`** — `state IN ('new', 'created', 'turned_in',
  'returned', 'reclaimed_by_student', 'student_edited_after_turn_in',
  'unrecognised')`. The six recognised values of VR-004 — the five Classroom
  documents plus the one BR-056 names — and the marker OD-005 requires.
- **`ck_submission_raw_state`** —
  `(state = 'unrecognised') = (raw_state IS NOT NULL)`. A biconditional, not a
  one-way implication: an unrecognised row **must** carry the value Google sent,
  and a recognised row **must not** carry a leftover string. Without it the marker
  could exist with nothing to identify, which is the whole point of OD-005.
- **`ck_submission_grades_non_negative`** —
  `(assigned_grade IS NULL OR assigned_grade >= 0) AND (draft_grade IS NULL OR
  draft_grade >= 0)`. Data sanity on external input (VR-001).

Deliberately **not** constrained, each for a stated reason:

- **a grade against the coursework's `max_points`** — it is another row, which a
  row-level check cannot reach, and Classroom permits a teacher to award more than
  the maximum. BR-056 prints «баллы из максимума задания, как есть»; inventing a
  ceiling would discard a real grade.
- **a grade on ungraded work** — I-8 stores it as given rather than discarding it.
  A constraint forbidding it would turn a Google-side oddity into a failed import.
- **`state` against `turned_in_at`** — it is tempting to require that
  `state = 'turned_in'` implies a date, but FR-009 says a submission whose history
  Google omits has **no** date, so that combination is legitimate. The reverse is
  legitimate too: work turned in and later reclaimed keeps its turn-in date while
  the state is `reclaimed_by_student` (BR-058).

### 4.3 Indexes

- **`uq_submission_course_work_google_id`** — unique on
  `(course_work_id, google_id)`, Specification v2's second correction: Classroom
  documents a submission id as unique only among the submissions of its own
  course work.
- **`ix_submission_course_work_participant`** — `(course_work_id,
  participant_id)`, **not unique**. This is the journal's cell lookup: one
  student's submission of one piece of work. It is deliberately **not** a unique
  index, on the same reasoning that produced Specification v2 and US-014's
  OD-011: nothing in this project verifies that Classroom cannot return two
  submissions of one item for one student, and a unique index would turn that
  Google-side possibility into a failed import of the whole school. A duplicate
  would show up as two cells rather than as an outage, which is the cheaper
  failure.
- **`ix_submission_participant_id`** — the second foreign key needs its own index
  (PC-7). `course_work_id` leads the two indexes above and needs no third.

### 4.4 Cascade behaviour

Both foreign keys are **`DeleteBehavior.Restrict`** (PC-8's default), for the
reason PC-8 gives in the very words this table is about: "Student grades must not
disappear because a parent row was removed."

A `Cascade` from `course_work` would be convenient for the purge and is exactly
what PC-8 forbids — deleting a course would then silently take a school's grades
with it. US-037 deletes in an explicit order inside one transaction instead
(PC-11), which is a visible decision rather than a database side effect. US-014
made the same choice one level up.

### 4.5 Personal data

`assigned_grade`, `draft_grade`, `turned_in_at` and `late`, joined to a person
through `participant_id`, are **personal data of a student, potentially a minor**
(§5, SC-1). Handling, all stated in §7 below: plain in the installation's own
database because reports must show them, never in a log line, never beyond that
database, deleted only by the purge, and with **no read path at all** until
Epic 3 (S-08).

## 5. What is deliberately not added anywhere

- **No settings table** for `Retention:Years` — configuration comes from
  `appsettings` and the environment (AD-10, DC-3), read at startup (§8).
- **No `last_activity` column on `course`.** PC-11 defines last activity as a
  computation over four sources, and FR-011 computes it from **Google's** data for
  a course that has no row yet (I-10). A stored column would have to be
  recomputed on every run, could drift, and would still not cover the Meet
  component that arrives with US-031.
- **No table for the submission history** (Epic 12, BR-059) and no rubric table.
- **No `MeetParticipation` foreign key to `classroom_participant`** — PC-12 still
  calls such a migration a finding, and that table still does not exist.

## 6. The migration

One migration, `AddCourseWorkAndSubmissions`, under
`src/ClassroomAgent.Infrastructure/Persistence/Migrations/` (PC-2):

- creates `course_work` and `submission` with every column, check constraint,
  unique index and foreign key above;
- creation order is `course_work`, then `submission`, which depends on it and on
  `classroom_participant`;
- `Down` drops them in reverse order;
- **touches no existing table** — no column added to `sync_state`, no constraint
  amended on `audit_event`, nothing altered on US-014's three tables;
- installation migrations become **eight**, with `_AddCourseWorkAndSubmissions`
  last;
- applied by the explicit deployment step, never at start-up (PC-2, DC-2). No
  `EnsureCreated()`, no `ExecuteSqlRaw` schema change.

## 7. Sensitive data

| Value | Handling |
|---|---|
| `assigned_grade`, `draft_grade` | personal data of a student (§5, SC-1). Stored as raw points because reports must print them; **never** in a log line, an error body or a `SyncState` diagnosis (SC-10, S-06); never sent beyond this database (SC-13, S-07); deleted only by the purge (PC-11) |
| `turned_in_at`, `late` | personal data in the same sense — they describe one child's conduct. Same rules |
| `course_work.title` | not personal data, but kept out of logs with the course name, for the same reason US-014 kept that out (SC-10) |
| `raw_state` | a Classroom enumeration value, not personal data. It is the one value this Story deliberately **does** write to a log, in the single Warning line of OD-005, with the submission id and nothing else |
| credentials | none. No key, token, password or secret reference appears in either table |

Neither table has a column that could hold a name, an email address or a Google
person id: the person is reached only through `participant_id` (I-7).

## 8. `Retention:Years` — why it is not in this design

FR-012 makes the retention period a **required installation setting** read at
startup, and by DC-3 and PC-11 an installation without a valid value refuses to
start. It has no database presence:

- configuration is `appsettings` plus environment variables (AD-10, DC-3), never
  a table;
- the value reaches the Application layer as a narrow settings record, the
  existing `Application/Models/SchoolDefaults` pattern — not as the whole
  settings object, which SC-7 keeps out of the container (the US-013 security
  review's F-1);
- the entity model states the shape (entity-model §8); the reading and the
  refusal to start belong to `InstallationSettingsReader` in `Web.Configuration`,
  where `Sync:IntervalMinutes` already lives.

`deployment-conventions.md` DC-3 gains the key name, as it did for
`Sync:IntervalMinutes` in US-013 (FR-022).

## 9. Constraints checklist (PC conventions)

| Convention | How this design satisfies it |
|---|---|
| PC-1 PostgreSQL, Testcontainers | two ordinary tables; the InMemory provider enforces none of §10's guards |
| PC-2 migrations only | one migration, `AddCourseWorkAndSubmissions`; no `EnsureCreated()` |
| PC-3 identifiers | `bigint` identity `id` on both; Google ids in their own columns, never a primary key, with the **parent-scoped** unique indexes of Specification v2 — and PC-3's own wording gains that scope (FR-022) |
| PC-4 explicit mapping | every nullability and every `HasMaxLength` stated above; one configuration class per entity |
| PC-5 naming | `snake_case` singular tables; `pk_`/`uq_`/`fk_`/`ix_`/`ck_` prefixes as listed |
| PC-6 timestamps | `created_at` / `updated_at` on both, `timestamptz`, interceptor-stamped; Google's times stored in UTC as given (I-1) |
| PC-7 indexes | unique indexes on both natural keys; every foreign key indexed (leading a composite where one exists); the journal's course-plus-date composite and its cell lookup |
| PC-8 relationships | `Restrict` on all three foreign keys, for PC-8's own stated reason about grades |
| PC-9 sensitive data | §7 — grades marked, never logged, no credential anywhere |
| PC-10 idempotent sync | upsert on the two natural keys; the unique indexes are what make repeated runs safe (FR-010) |
| PC-11 retention | `creation_time` / `update_time` on `course_work` and `update_time` on `submission` are the columns the purge's last-activity computation needs; `course_id` and `course_work_id` lead the indexes it will delete by. The purge itself is US-037 |
| PC-12 Meet data | unchanged; no foreign key from `MeetParticipation` to `classroom_participant`, now or later |
| PC-13 coursework and submissions | the stored set is exactly PC-13's list **plus Google's `updateTime`** — the correction FR-022 makes to PC-13 itself (OD-007) |

## 10. For TEST_WRITING

Database-level guards, all against real PostgreSQL through Testcontainers — the
InMemory provider enforces none of them (TC-2):

1. **A duplicate `(course_id, resource, google_id)` is rejected**, and a second
   import of the same item **updates** the row rather than adding one (FR-010).
2. **The same Google id in two different courses is accepted** — the positive
   assertion of Specification v2. It fails the moment someone narrows the key, and
   it is the guard that the whole loop-back existed to install.
3. **The same Google id under the two different resources is accepted** — the
   other half of the key (PC-3).
4. **A duplicate `(course_work_id, google_id)` submission is rejected**, and the
   same submission id under a **different** piece of work is accepted (v2).
5. **Two submissions of one item by one student are accepted** — the positive
   assertion of §4.3. It fails the moment someone adds `IsUnique()` to
   `ix_submission_course_work_participant`.
6. **A `resource` outside the two codes is rejected** (`ck_course_work_resource`).
7. **A material with `max_points` or `due_at` is rejected**
   (`ck_course_work_material_has_no_grading`).
8. **A negative `max_points`, `assigned_grade` or `draft_grade` is rejected**.
9. **A `state` outside the seven codes is rejected** (`ck_submission_state`), *and*
   at the Application level a submission whose state Classroom reports outside the
   six is **stored with the marker** while the run completes (OD-005, VR-004) —
   the opposite of US-014's skip, and the constraint must never be what stops it.
10. **The `raw_state` biconditional holds both ways**: `unrecognised` without a
    raw string is rejected, and a recognised state **with** one is rejected too
    (`ck_submission_raw_state`).
11. **An item with no `due_at`, `max_points`, `creation_time` or `update_time` is
    storable** (§3.1) — the nullability is deliberate and a later `NOT NULL` would
    break imports.
12. **A submission with no `turned_in_at` is storable** (FR-009), and `late`
    defaults to `false` when Google omits it.
13. **Deleting a `course_work` that has submissions is refused**, and deleting a
    `course` that has coursework is refused (`Restrict`, §4.4) — so the purge must
    delete explicitly and in order.
14. **The migration set and table set grew as expected**: `AppUserMigrationTests`
    goes from seven to **eight** migrations with `_AddCourseWorkAndSubmissions`
    last, and its table set gains `course_work` and `submission` — an **expected
    change to an existing test**, as US-013 and US-014 each made in turn.
    `TheControlPlaneSchema_IsUnchangedByThisStory` must keep passing untouched.
15. **`sync_state` is unchanged** — no column added; the model has no pending
    changes against the migrations
    (`TheModel_HasNoPendingChangesAgainstTheMigrations`).
16. **`audit_event` gains no row** from a scheduled run (FR-020) and no member.

Behaviour, not schema, and therefore Application tests rather than these: the
date cascade of FR-008 with a fixture per level; the truncation of a long title;
the age rule of FR-011 including the «already imported course is never
age-checked» case (I-4); the off-roster membership of FR-007; the refusal to
start without `Retention:Years` (VR-008), which is a host test; and the log-content
assertions of FR-017, where the grade absence must be proved at host level with a
control proving the import happened — the pattern US-014's
`CourseImportLoggingTests` established.

## 11. Open questions

None raised by this stage at attempt 2.

The contradiction that blocked attempt 1 — the Specification keying `CourseWork`
and `Submission` globally, against Classroom's documented per-parent
uniqueness — was resolved by the Owner on 2026-09-28 by scoping each key with its
parent, and corrected in Specification v2. This design implements v2, and §10
items 2, 4 and 5 are the tests that keep it implemented.
