---
artifact_type: database_design
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T17:40:00Z
updated_at: 2026-10-25T17:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/designs/api/US-056-api-design.md
    version: 1
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 98
supersedes: null
---

# US-056 Database Design — Members seen when a course is first loaded count from the course's creation

**Verdict: PASS — derived start, no schema change, no migration.**

There is no `openapi` input: API_DESIGN is NOT_APPLICABLE
(`docs/designs/api/US-056-api-design.md`).

## 1. Decision: the start is derived, not stored (PC-8)

PC-8 leaves to this design whether a membership's start is stored or derived.
It is **derived** from three values already stored and never rewritten:

| Value | Column | Written by | Rewritten? |
|---|---|---|---|
| first seen | `course_membership.first_seen_at` | synchronization, once per membership | never (BR-051, FR-004) |
| course first loaded | `course.created_at` | `TimestampInterceptor` on insert, inside the course's transaction of its first committed load | never (PC-6) |
| course creation in Google | `course.creation_time` (nullable) | course synchronization (mirror, v83) | follows Google (I-2) |

**Rule (Spec FR-001):**

```
seen_in_first_load := first_seen_at <= course.created_at
start := seen_in_first_load ? COALESCE(course.creation_time, first_seen_at) : first_seen_at
```

- (a) seen in the first load, `creation_time` present → `creation_time`.
- (b) seen in the first load, no `creation_time` → `first_seen_at`, which for
  such a membership *is* the first-loading run's instant (`observedAt`, Spec §1),
  i.e. "the run that first loaded the course" (FR-001 b).
- (c) otherwise → `first_seen_at`.

**Why the comparison is exact (Spec §1, FR-005).** A run takes `observedAt` at
its start and stamps every membership it creates with it; the course row it
inserts is stamped `created_at` at save time, later in the same run, so
first-load memberships have `first_seen_at ≤ created_at`. A later run starts
after that run finished (`SyncRunCoordinator`) on a clock that does not step
back, so its `observedAt > created_at`. Neither input is ever rewritten, so the
result is fixed once the first load commits (FR-002): later runs, the leaver
purge, the retention purge of other rows and erasure (US-038) of other people
change nothing (AC-011). A course whose first attempt was discarded (US-053)
has no row until the run that commits it (I-4).

**Pre-upgrade data (FR-005, AC-009).** The rule reads only columns that have
existed since migration `AddCoursesAndRosters`, so every membership stored
before the upgrade gets its start the moment the new code runs — no backfill,
no data rewrite, no migration. A course whose first load found nobody, or whose
first-load memberships were all deleted, still has its `created_at`, so a later
arrival there is never counted from `creation_time` (AC-009 second paragraph).

### 1.1 Rejected alternative — a stored flag `seen_in_first_load`

A `boolean NOT NULL` column set by the first-loading run would make the
property explicit, but:

- pre-upgrade rows would still need the same `first_seen_at <= created_at`
  comparison as a backfill `UPDATE` over every membership (millions of row
  rewrites at deployment, NFR §9) — the stored flag adds no exactness;
- synchronization would gain a new write path on both the roster and the
  submissions-only branch, plus the US-053 discard path, each a new place to get
  wrong;
- queries would still join `course` for `creation_time` (I-2: the current
  stored value is authoritative, so the start date itself cannot be stored
  either).

Derived costs one join to `course` by primary key and nothing else.

### 1.2 Rejected alternative — a stored start column

Ruled out by Spec I-2: the start must follow the course's current stored
`creation_time`, so a copied date would go stale.

## 2. Schema

**No change.** No table, column, constraint, index or migration is added or
altered. `ck_course_membership_seen_order` and all existing indexes stay.

**Index check (PC-7, NFR §9).** Every consumer already narrows memberships
without the start:

| Reader | Narrowed by | Index |
|---|---|---|
| `JournalSource` Q3, `JournalFieldSource` F3, `MeetReportSource` | `course_id` (+ role) | `ix_course_membership_course_seen` (course prefix) |
| `MeetCodeScoringSource` | participant email | `ix_classroom_participant_email_lower` → `ix_course_membership_participant_id` |
| `MeetCodesReadSource.GetUnassignedPageAsync` | organizer email, `role = 'teacher'` | as today |

The period / date tests run in Application over the loaded rows (journal,
reports, scoring) or in the `EXISTS` of the raw SQL; the added join to
`course` is by `pk_course`. No new index is needed; the existing
`(course_id, first_seen_at, last_seen_at)` index keeps serving the course
lookup.

## 3. Where the rule lives

One C# definition, one SQL mirror:

- **Domain** — `ClassroomAgent.Domain.Rules.MembershipStart.Of(firstSeenAt,
  courseFirstLoadedAt, courseCreationTime)` (entity model §2). Pure, no
  dependency, the single business definition.
- **Infrastructure readers** (`JournalSource`, `JournalFieldSource`,
  `MeetReportSource`, `MeetCodeScoringSource`) project the three values
  (`m.FirstSeenAt`, `m.Course.CreatedAt`, `m.Course.CreationTime`) in the LINQ
  query, then map each row in memory through `MembershipStart.Of` into the
  record's `StartsAt` (entity model §3). EF translates the navigation to a join
  on `fk_course_membership_course`.
- **Raw SQL** `MeetCodesReadSource.GetUnassignedPageAsync` (FR-003.5) cannot call
  C#; its `has_candidate` predicate joins `course` and uses the SQL form of §1:

  ```sql
  JOIN course c ON c.id = cm.course_id
  ...
  AND ((CASE WHEN cm.first_seen_at <= c.created_at
             THEN COALESCE(c.creation_time, cm.first_seen_at)
             ELSE cm.first_seen_at END) AT TIME ZONE {zoneId})::date
      <= (m.started_at AT TIME ZONE {zoneId})::date
  ```

  The rest of the predicate (`on_roster OR last_seen ≥ date`) and the
  `ORDER BY has_candidate DESC, …` are unchanged. Per SPEC_REVIEW v3 m-1 the
  agreement required with scoring is this start and the teacher-on-date rule,
  not the score. A test pins the SQL against `MembershipStart.Of` on the same
  data (AC-004 second paragraph).

Application rules (`JournalCellRule.IsOfPeriod`, `RosterOnDate.Covers`) keep
their logic and receive the start instead of the first-seen instant (entity
model §4). No other reader of `first_seen_at` exists outside `Migrations`
(ripgrep, Spec §1).

## 4. Sensitive data, audit, read-only

No personal data is added or copied; the start is computed from dates already
stored (SC-8). No audit or log event changes (SC-11). Nothing writes, so
read-only mode (BR-026) is unaffected; viewing uses the new start.

## 5. Schema initialization and deployment

Nothing to deploy for the schema: the upgrade is a code change only. The
"before the upgrade" database of AC-009 has the same schema as after it;
TEST_WRITING builds it by seeding rows directly (course `created_at` and
membership `first_seen_at` set explicitly), not through a migration.

## 6. Notes for TEST_WRITING and IMPLEMENTATION

- **Advance the clock between runs** (Spec AC preamble). A fake clock left
  unchanged makes a later run's `observedAt` equal to `course.created_at`, which
  the rule reads as a first-load membership.
- **Existing fixtures may change meaning.** A test that seeds a course with a
  `creation_time` and memberships whose `first_seen_at` is ≤ the course's
  `created_at` (for example a membership seeded "in September" under a clock
  set to October) now gets the `creation_time` start. Tests asserting exclusion
  before `first_seen_at` with such data need their fixture to say which case
  they mean (later arrival: `first_seen_at` after `created_at`). This is the
  Story's intended behaviour change, not a regression; the implementation report
  lists each adjusted test.
- Three existing reader tests assert the record's `FirstSeenAt`
  (`JournalSourceTests.cs:138`, `JournalFieldSourceTests.cs:181`,
  `MeetReportSourceTests.cs:184`); they move to `StartsAt` (entity model §3).
- A `Course` row reloaded after the retention purge deleted it is a new row with
  a new `created_at`; its memberships then count from `creation_time` again. That
  follows FR-002 ("first committed to the installation's database"); no test is
  required.

## 7. Delegation note

Nothing was delegated. Entity, configuration, migration, reader and rule files
were read directly: `CourseMembership.cs`, `Course.cs:76,99`,
`CourseMembershipConfiguration.cs`, `CourseConfiguration.cs:35,49`,
`20260927201534_AddCoursesAndRosters.cs`, `TimestampInterceptor.cs`,
`JournalSource.cs:44-54`, `JournalFieldSource.cs:48-60`,
`MeetReportSource.cs:86-97`, `MeetCodeScoringSource.cs:67-77`,
`MeetCodesReadSource.cs:38-67`, `JournalCellRule.cs:84-86`, `RosterOnDate.cs`,
`RosterMembership.cs`, the three `*MemberRecord.cs`.
