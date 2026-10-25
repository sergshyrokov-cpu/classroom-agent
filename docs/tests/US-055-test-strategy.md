---
artifact_type: test_strategy
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T12:30:00Z
updated_at: 2026-10-25T12:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/designs/api/US-055-api-design.md
    version: 1
  - path: docs/designs/database/US-055-db-design.md
    version: 1
  - path: docs/designs/database/US-055-entity-model.md
    version: 1
supersedes: null
---

# US-055 Test Strategy — Points and grades of any size are stored as Google returned them

## 1. Scope

The Story changes the type of three columns (`course_work.max_points`,
`submission.assigned_grade`, `submission.draft_grade`) from `numeric(10,4)` to
unconstrained `numeric` and ships migration `WidenGradeColumns` (db-design §3).
No endpoint, page, policy, text, entity signature or run rule changes
(spec S-01, API design NOT_APPLICABLE, entity model: no signature change).

The tests therefore prove three things:

1. **Storage** — the columns are unconstrained `numeric`, hold values above the
   old ceiling and with more than four decimal places exactly, keep their CHECK
   constraints, and the migration keeps every stored value (AC-001 … AC-003,
   AC-005).
2. **The run** — a synchronization through the real host stores such values
   exactly and goes on past them to the next courses and the Meet step
   (AC-001 … AC-004).
3. **Display** — the journal, the report and the report export show such values
   as stored and do not fail (AC-006, AC-006a).

## 2. Test levels

| Level | Why | Class |
|---|---|---|
| Integration (persistence, real PostgreSQL, TC-2) | column type, migration, exact storage and EF Core round trip, constraints | `Infrastructure/Persistence/GradeColumnsPersistenceTests` |
| Integration (host + background service, Google ports substituted, TC-4) | the run end to end | `Web/BackgroundServices/LargeGradesSyncTests` |
| Integration (HTTP, signed-in Dean) | journal, report, export | `Web/Pages/LargeGradesPageTests` |

No unit level: no business rule changes; a use-case test with substituted
persistence could not see a column limit. No contract level: no API change.

## 3. Scenarios

### Positive
- 12 345 678.5 maximum points stored and read back (AC-001) — run and EF Core.
- 2 500 000 assigned, 1 000 000.25 draft stored (AC-002) — run and EF Core.
- 10.123456 / 7.654321 stored with six decimals, also as PostgreSQL text (AC-003).
- Three courses, the second with 1 000 000 maximum points: all stored, Meet port
  called, `meet_loaded_up_to` set, run `completed`, 3 processed (AC-004).
- Journal full view shows `2500000 / 12345678,5` and the draft `1000000,25`;
  raw-points report shows `2500000 / 12345678,5`; the export's cell carries it
  (AC-006, AC-006a).

### Boundary
- Old ceiling 999 999.9999, the smallest four-decimal step 0.0001, zero and null
  survive the migration as the same text (AC-005).
- A 26-significant-digit value (`99999999999999999999.123456`) and nine decimal
  places (`123456789012.987654321`) round-trip — within `decimal`'s range
  (FR-001).
- AC-004 uses 1 000 000, the first whole value above the old ceiling.

### Negative / regression
- Negative maximum and negative assigned or draft grade still refused by
  `ck_course_work_max_points_non_negative` and `ck_submission_grades_non_negative`
  (db-design §7). Green before the change by design.

### Validation
None added or removed (spec VR-001). A value outside `decimal` is out of scope.

### Security
- S-03: every value is synthetic; no real grade is read.
- S-04 / SC-10: a completed run with the large values writes none of them to the
  log, in invariant or Ukrainian format; the completion assertion runs first so
  the absence is not vacuous.
- S-01: no endpoint changes, so no new allowed/forbidden-role test; the pages
  are exercised as the Dean, the role that does the day-to-day work. The existing
  role tests of the journal, report and export cover access.

### Persistence
- Column shape via `information_schema` (precision and scale both `NULL`).
- Migration name and order (`WidenGradeColumns` after
  `20261015203416_AddNotAnAdminAccountRefusal`).
- Model/snapshot agreement stays covered by the existing
  `HasPendingModelChanges()` checks (db-design §7); not duplicated.

## 4. Fixtures

- `PostgreSqlFixture`, `InstallationTestHost` (per-test database, migrations).
- `SyncHostExtensions.StartAsync` with `FakeClassroomReader` (synthetic course,
  task and submission) and the default `FakeMeetReportsReader`.
- `JournalHostExtensions.StartAsync(Actor.Dean)` + `SeedJournalAsync`, plus one
  extra September task with the AC-001/AC-002 values inserted by
  `CourseWorkRows`; `ReportHostExtensions.InsertTemplateAsync` with no scale
  (scale mode `none`, spec I-4).
- `JournalExportHostExtensions.ExportAsync` and ClosedXML to read the workbook.

## 5. Decisions taken in this stage

- **SPEC_REVIEW m-1** — added `TheBuiltInScaleReport_DoesNotFail_OnTheLargeValues`:
  the built-in template converts by its scale; 2 500 000 / 12 345 678.5 ≈ 20 %,
  no overflow. The extreme pair (huge points over a tiny maximum) stays outside
  the ACs, as the review noted.
- **SPEC_REVIEW m-2** — AC-006a uses the **table** layout, the one the export
  endpoint (`/api/v1/exports/journal-xlsx`) builds; the shipped blank form
  renders grades through the same `ReportWorkbookMapper.GradingCell`.
- AC-006 "builds a report" is tested on the page with a raw-points template
  (spec I-4); the export is AC-006a.

## 6. Excluded scenarios

- A Google value that is NaN, infinity or beyond `decimal` (spec VR-001, out of
  scope).
- The reader's `double` → `decimal` cast (spec I-1, unchanged, and no test calls
  a live Google API — TC-4).
- `Down` migration: lossy by design, never run by deployment (db-design §3.2).
- Lock duration on a large table: measured at DB_DESIGN (db-design §4), not a
  repeatable test.

## 7. Known limitations

- The page and export tests seed the values by SQL, not by a run; the run is
  proven separately by `LargeGradesSyncTests`.
- The Ukrainian number format is asserted (the default school language);
  the English format uses the same formatter.

## 8. Open Decisions

None. OD-001 (resolved, a) does not affect testing.
