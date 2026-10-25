---
artifact_type: test_strategy
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T19:30:00Z
updated_at: 2026-10-25T19:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: docs/designs/api/US-056-api-design.md
    version: 1
  - path: docs/designs/database/US-056-db-design.md
    version: 1
  - path: docs/designs/database/US-056-entity-model.md
    version: 1
supersedes: null
---

# US-056 Test Strategy — Members seen when a course is first loaded count from the course's creation

## 1. Scope

The start of a membership (spec FR-001) and every member-in-period / member-on-date
decision that uses it (FR-003.1 … FR-003.5): the journal, the template report (screen
and both exports), Meet reports (marks and report 3), automatic Meet code linking, the
order of the unassigned Meet codes list. Also: memberships stored before the upgrade
(FR-005), the fixed "seen in the first load" property (FR-002), and the dates and rules
that must not change (FR-004).

Not in scope: endpoints, authorization, texts, audit, logging (FR-006 — nothing changes;
existing suites cover them), a schema change (none — db-design §2).

## 2. Test levels

| Level | What | Class |
|---|---|---|
| Unit (Domain rule) | `MembershipStart.Of` cases a / b / c and the `≤` boundary | `Application/UseCases/MembershipStartTests` |
| Integration, reader (TC-2) | the four readers carry `StartsAt`; the raw-SQL candidate order agrees with `MembershipStart` + `RosterOnDate` | `Infrastructure/Persistence/MembershipStartReadersTests` |
| Host end-to-end (TC-2, TC-4) | real synchronization runs (`FakeClassroomReader`, manual clock on the criteria's days), then a Dean opens the pages | `Web/Pages/MembershipStartJournalTests`, `MembershipStartReportTests`, `MembershipStartMeetTests` |
| Host, seeded pre-upgrade DB | AC-009: rows as runs stored them, no connection, journal | `Web/Pages/MembershipStartPreUpgradeTests` |

The host tests are the evidence for the Acceptance Criteria: the courses and memberships
come from real runs, so the relation the design rests on — `first_seen_at` = the run's
`observedAt` ≤ `course.created_at` stamped at save time, and a later run's `observedAt`
after it — is produced by the system, not assumed by a fixture. Meetings and manual links
are seeded in SQL, as the existing US-032/US-033 page tests do.

A later run is a restarted host over the same database with its clock on the later day
(`MembershipStartHost.RunAgainAtAsync`): the start is compared by calendar date, so a
scheduled run minutes after the first would land on the first run's day (db-design §6:
advance the clock between runs).

## 3. Scenarios

Dates (Kyiv, TC-8 non-UTC zone with daylight saving): course created 2025-09-01; first run
2026-10-22; later run 2026-11-05.

**Positive.** First-load roster student in the 2025 journal (AC-001); first-load teacher in
the report teacher line on screen, table export, blank form (AC-002); Meet marks and report
3 before the first run (AC-003); automatic link of a code of pre-run meetings (AC-004); a
submissions-only first-load member listed for a month without a submission (AC-005); a
course first loaded by a later run counts from its creation (AC-008); stored pre-upgrade
first-load members (AC-009).

**Negative.** Later arrival excluded before the day they were seen, in the journal and the
Meet report (AC-007); no creationTime → not listed for 2025 (AC-006); later arrival in a
pre-upgrade course with no stored first-load member (AC-009 ¶2 — guards against the
rejected "earliest surviving first-seen" recognition, spec FR-005); erased member stays
erased and the later arrival keeps its start (AC-011). Every negative assertion has a
positive control in the same response (a listed member or a shown column), so an empty page
cannot pass it.

**Boundary.** `first_seen_at` = `created_at` counts as the first load; one tick later does
not (unit); instants compared regardless of offset (unit); the AC-008 period starts on the
course's creation day.

**Persistence.** Observed `first_seen_at` / `last_seen_at` / `on_roster` after another run
(AC-010); leaver expiry counts from last seen even when the start is more than the
retention period ago (AC-010); readers project the start (reader class).

**Validation / security.** No new input, endpoint, role or audit (spec §7, FR-006). Pages
are opened by a signed-in Dean; authorization of those pages is covered by the existing
allowed/forbidden-role suites and is unchanged.

## 4. Fixtures

- `MembershipStartHost` (new): the criteria's instants, `StartFirstRunAsync`,
  `RunAgainAtAsync`, `MembershipAsync`, `CourseIdAsync`.
- `FakeClassroomReader.WithCourse(..., creationTime)` and `WithRoster` (new parameters).
- `CourseRows.InsertCourseAsync(..., createdAt)` (new parameter).
- `SyncHostExtensions.StartAsync(..., now)` (new parameter).
- All names, addresses and ids invented (TC-4).

## 5. Excluded scenarios

- Spec I-1 (`creationTime` after the first load): impossible, the spec says not tested.
- A course reloaded after the retention purge deleted it (db-design §6): no test required.
- Overlapping runs / a clock stepping backwards: outside the rule's conditions (FR-005).
- Pre-upgrade course whose first-load members were all deleted: same stored rows as "found
  nobody", covered by the one AC-009 ¶2 test.

## 6. Known limitations

- AC-006, AC-007 (journal and Meet), AC-009 ¶2 and AC-010 pass before implementation by
  design: today's start (first seen) already gives their expected result. They guard against
  an implementation that over-applies the creation date or touches observed dates.
- Existing fixtures that seed `first_seen_at` ≤ the course's `created_at` with a
  `creation_time` change meaning under the new rule (db-design §6); IMPLEMENTATION lists
  each test it adjusts.

## 7. Open Decisions

None affecting testing.
