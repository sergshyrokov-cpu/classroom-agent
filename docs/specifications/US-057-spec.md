---
artifact_type: specification
story: US-057
version: 2
status: APPROVED
created_at: 2026-10-25T18:05:00Z
updated_at: 2026-10-25T18:40:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
  - path: docs/designs/database/US-057-db-design.md
    version: 1
supersedes: docs/specifications/US-057-spec.md@v1
---

# US-057 Specification — A course with points beyond the program's range is skipped, and the school scale never fails a report

Delegation: none. The `trebovaniya.md` passages (§4 Epic 1 "Когда повторы не помогли",
"Инкрементальная синхронизация"; §4 Epic 3 "Перевод в шкалу школы"; v98 changelog) and the
code facts below were read directly by the spec writer.

**Version 2** (loop-back from DB_DESIGN, BLOCKED B-1/B-2): the stored-value framing of AC-007
and the negative half of AC-010 cannot be met under the existing non-negative constraints. The
Owner kept the schema (OD-001). Changed: §1 code facts, FR-005 note, AC-007, AC-010, VR-002,
§8, §10, §11 (OD-001, I-6, L-1), §12. Everything else is v1 unchanged.

## 1. Overview

`trebovaniya.md` v98 settles two extreme cases left open by the US-055 security review
(INFO-1, INFO-2):

1. **Synchronization.** Google returns points as a `double`; the program holds them as
   `decimal` (range about ±7.9·10²⁸). A course in which Google returned points, a grade or a
   maximum beyond that range "пропускается" — the run continues, the stored copy of the course
   stays as it was, the run log names the course without the values, and the next run reads the
   course again (§4 Epic 1, *v98*).
2. **School scale.** "Процент ограничен пределами 0–100 %: баллы не меньше максимума дают
   100 %, отрицательные — 0 %; отчёт строится при любых сохранённых баллах" (§4 Epic 3, *v98*).

Code facts this Specification must not contradict (read 2026-10-25 at `bb67ffb`):

- `GoogleClassroomReader.Snapshot(GoogleCourseWork)` casts `MaxPoints` with `(decimal)double`
  (`GoogleClassroomReader.cs:352`); `Snapshot(GoogleSubmission)` casts `AssignedGrade` and
  `DraftGrade` the same way (`:384–385`). The cast throws `OverflowException` for a value beyond
  the `decimal` range. The snapshots are built **outside** `GuardAsync` (`:201`, `:230`), so the
  exception leaves the reader unclassified.
- `RunSynchronizationUseCase` reads roster, coursework and submissions of one course before its
  transaction and catches `GoogleReadFailedException` of kind `CourseGone` there
  (`RunSynchronizationUseCase.cs:495–517`); any other exception reaches the run's
  `catch (Exception)` and stops the run as `Unexpected` (`:126–157`).
- The host writes one Warning per gone course, event 5130 `SyncCourseGone`
  (`SynchronizationBackgroundService.cs:281–284`, `:408–413`). Event ids 5130–5134 are used;
  5135 is free.
- The database refuses negative points: `ck_course_work_max_points_non_negative`
  (`max_points IS NULL OR max_points >= 0`) and `ck_submission_grades_non_negative` (both grades
  `IS NULL OR >= 0`), in force since US-015 and re-pinned by US-055. The Domain entities do not
  check the sign; the constraint is the only guard, so a negative value fails the course's
  transaction (DB design v1 §1). This Story keeps both constraints (OD-001).
- `ReportBuilder` reads its cells from the `IJournalSource` / `IJournalFieldSource` ports
  (`Application/Ports`), so a report can be built from a pair that no table holds.
- The school-scale percent is computed in one place, `ReportBuilder.Grade`
  (`ReportBuilder.cs:256–270`), as `points / max * 100`, rounded and then clamped. The on-screen
  report (`GetReportQuery`) and the export (`ExportJournalCommand`) both use `ReportBuilder`.

## 2. Business Goal

One unusual value Google returned never stops the school's whole synchronization, and no stored
value can make a report page or an export fail.

## 3. Business Flow

**Synchronization.** For every listed course that passes the existing checks (state, blank name,
unchanged archive), the run reads the roster, coursework and submissions. If any coursework
maximum, assigned grade or draft grade of that course is beyond the range the program holds, the
course is skipped before its transaction: nothing of it is written, the run goes on with the next
course, then the Meet step and Meet code linking. The host writes one Warning line naming the run
and the course. The next run treats the course like any other course not read in full.

**Report.** When a Dean or Admin opens a report or exports it with a template in the school
scale, every grade is converted to a percent bounded to 0–100 % and then to the scale label.
Points not less than the maximum give 100 % and points not greater than 0 give 0 % without a
division, so no stored pair can overflow.

## 4. Functional Requirements

### FR-001 The reader recognises a value beyond the range

When `GoogleClassroomReader` turns a Google `double` into a `decimal` for a coursework
`maxPoints`, a submission `assignedGrade` or a submission `draftGrade`, and the value lies beyond
the `decimal` range in either direction (greater than `decimal.MaxValue` or less than
`decimal.MinValue`), the reader does not let `OverflowException` escape. It throws
`GoogleReadFailedException` with a new kind of `GoogleReadFailureKind`,
`ValueOutOfRange`, scoped to the course being read. The exception carries no value, no
coursework id, no submission id and no user id (SC-10). No Google SDK type crosses into
Application (AD-4).

- A value within the range keeps today's conversion exactly (US-055 scope is unchanged).
- Values Google cannot send as a JSON number (`NaN`, infinity) are out of scope (§9 below).
- The kind is a course-scoped failure like `CourseGone`; it never stops the run by itself.

### FR-002 The course is skipped, the run continues

`RunSynchronizationUseCase` catches `GoogleReadFailedException` of kind `ValueOutOfRange` at the
same place as `CourseGone` (around its roster/coursework/submission reads, before the course's
transaction). For that course:

1. nothing is written — no course row, item, membership, submission, person or complete-read
   `updateTime` — and a copy already stored stays exactly as the earlier run left it;
2. the course is recorded in a new outcome list of skipped-for-out-of-range course Google ids,
   kept separate from `CoursesGone`, and is reported in the outcome whether the run completes or
   is later stopped by another failure (the same two `SynchronizationRunOutcome` paths that carry
   `CoursesGone`);
3. it is not counted in `ProcessedCount` (the counter never claims data the installation does
   not hold — the same rule as US-017 I-5);
4. the loop continues with the next course; the Meet step and Meet code linking run as after any
   other skip; `SyncState` ends `completed` if nothing else fails.

The whole course is skipped even if only one value of one item or one submission is out of range
(§4: "пропускается курс").

### FR-003 One run-log line per skipped course

For each course in the FR-002 list the host (`SynchronizationBackgroundService`) writes one log
line:

- level `Warning`, event id `5135`, event name `SyncCourseValueOutOfRange`;
- message parameters: the run id and the course Google id passed through
  `GoogleLogValue.Bounded`, as `SyncCourseGone` does;
- the message names the reason in words (points or grade beyond the range the program holds)
  and contains no value, no coursework id or title, no student, no submission id (SC-10).

No other log line of the run carries the value or those identifiers either: the
`OverflowException` and its message are never logged.

### FR-004 The next run reads the course again

Because FR-002 writes nothing for the course — in particular not the `updateTime` remembered at
its last complete read — the incremental-sync rule (§4 Epic 1 "Инкрементальная синхронизация",
US-018 `IncrementalSyncRule.IsUnchangedArchive`) never treats a skipped course as unchanged on
account of that run: an archived course whose Google `updateTime` equals the one listed in the
skipping run is read in full by the next run, unless an earlier complete read of the same course
recorded that very `updateTime`. No new state is stored for this.

Consequence stated for the reviewer: a course stored completely at `updateTime` T, archived, and
never changed since cannot gain an out-of-range value without its `updateTime` changing (Google
forbids changes to an archived course, §4 v90); so the exception case does not arise in practice.

### FR-005 School-scale percent bounded before dividing

`ReportBuilder.Grade`, when the template is in ranges mode and the maximum is positive:

1. points `>=` maximum → percent `100`, without dividing;
2. points `<=` 0 → percent `0`, without dividing;
3. otherwise → today's computation: `points / max * 100`, rounded to a whole number with 0.5 away
   from zero, clamped to 0 … 100.

The label is chosen from the template's scale rows exactly as today. With no conversion mode, or a
maximum that is absent or not positive, the cell keeps today's raw points out of the maximum. This
holds for assigned and draft points alike (both go through `Grade`) and therefore for the
on-screen report and every export built by `ReportBuilder`.

For every pair that does not overflow today, rules 1–3 give the same percent as today's code
(today's clamp maps `>= 100` to 100 and `<= 0` to 0), so no existing report changes.

Rule 2 is stated for any `decimal` the function receives. A negative stored value cannot exist
(§1, OD-001), so in a report read from the database rule 2 is reached only by points 0; the
negative half is proved where the report is built from a substituted journal port (AC-007b).

## 5. Acceptance Criteria

| AC | Story AC | Statement |
|---|---|---|
| AC-001 | AC-001 | Three courses; the second holds a coursework item with `maxPoints` 1·10³⁰ (synthetic). After a run: courses 1 and 3 and their items are stored, nothing of course 2 is written, the Meet step ran, `SyncState` is `completed`. |
| AC-002 | AC-002 | One course with a submission whose `assignedGrade` is 1·10³⁰, another with a submission whose `draftGrade` is −1·10³⁰ (synthetic). Both courses are skipped as in AC-001; the run is `completed`. |
| AC-003 | AC-003 | A course stored by an earlier run, in which Google now returns an item with `maxPoints` 1·10³⁰: after the run its course row, items, memberships and submissions are exactly as the earlier run stored them. |
| AC-004 | AC-004 | With AC-001's data the run log has exactly one `SyncCourseValueOutOfRange` Warning (event 5135) naming the run id and course 2's Google id; no log line of the run contains the value (in any formatting the test checks, at least `1E+30` and `1000000000000000000000000000000`), the item's id or title, a student's name, email or id, or a submission id. |
| AC-005 | AC-005 | An archived course skipped by the rule of AC-001 on its first appearance; the next (not weekly-full) run lists it with the **same** `updateTime` and in-range values: the course is read in full and stored. |
| AC-006 | AC-006 | A school-scale template, a stored grade of 7·10²⁸ points against a maximum of 0.0001 (synthetic): the on-screen report and the export both show the scale label for 100 % and neither fails. Same for a draft grade with the same values. |
| AC-007 | AC-007 (restated, OD-001) | **a)** A school-scale template, a stored grade of 0 points against a maximum of 0.0001: the on-screen report and the export both show the scale label for 0 % and neither fails. **b)** A school-scale template and a grade of −7·10²⁸ against a maximum of 0.0001 delivered by a substituted journal port (no stored row): the report built by `ReportBuilder` — through the on-screen report and the export use cases — shows the scale label for 0 % and does not fail. Same for a draft grade with the same values in both parts. |
| AC-008 | AC-008 | A school-scale template (12-point preset), grades 6 of 12, 12 of 12 and 0 of 12: the report shows the labels for 50 %, 100 % and 0 % (`6`, `12`, `1`), as before this Story. |
| AC-009 | derived (FR-002.2) | A run that skips a course for an out-of-range value and is then stopped by another failure (e.g. a transient failure on a later course) still reports the skipped course: its `SyncCourseValueOutOfRange` line is written, and the run is `failed` with that other failure's diagnosis, not `Unexpected` because of the skip. |
| AC-010 | derived (FR-001; restated v2, OD-001) | A coursework `maxPoints`, an `assignedGrade` and a `draftGrade` just inside the upper bound (7.9·10²⁸, synthetic) are stored as today's conversion gives them, and the course is not skipped. The lower bound is not tested by storing: a negative value is refused by the database (L-1). |

AC-009 and AC-010 are derived: AC-009 pins the "reported whether the run completes or stops"
behaviour that `CoursesGone` already has; AC-010 pins the boundary so the check does not skip
valid values. AC-007 and AC-010 are restated in v2 by OD-001: the Story's AC-007 Given (a
**stored** −7·10²⁸) cannot be set up under the kept constraint.

## 6. Validation Rules

- VR-001 Range check: a `double` value `v` is out of range when it cannot be converted to
  `decimal` — `v > (double)decimal.MaxValue` or `v < (double)decimal.MinValue`, or the
  conversion otherwise reports overflow. The check is applied to coursework `maxPoints`,
  submission `assignedGrade` and submission `draftGrade` — the only `double` → `decimal`
  conversions in the reader. Absent values stay absent.
- VR-002 Report: `points` and `max` are `decimal` values; read from the database they are any
  value the column holds — unconstrained `numeric` since US-055, but never negative
  (`ck_*_non_negative`, kept by OD-001). FR-005 accepts any `decimal`, negatives included; no
  further validation is added. `max` ≤ 0 or absent keeps the raw-points path.
- No user input, request body, route or query parameter is added or changed.

## 7. Security Requirements

- SC-10: the out-of-range value, the coursework id and title, student identity and submission id
  are never written to a log, to `SyncState` or to any exception message that reaches a log. The
  only identifiers logged are the run id and the course Google id (already logged by
  `SyncCourseGone`; bounded by `GoogleLogValue.Bounded`).
- SC-13 / Hard Stops: reads only; no new Google request, no new scope, no new destination.
- No endpoint, policy, role or audit event changes (SC-4, SC-11 unaffected). Read-only mode is
  untouched: a skipped course is a synchronization outcome, and synchronization is already
  blocked in read-only mode.
- AD-4: the reader converts the overflow into the port-level `GoogleReadFailedException`; no
  Google SDK type or `OverflowException` crosses into Application.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Out-of-range value in a course | Course skipped (FR-002), one Warning (FR-003), run continues |
| Same course out of range in every run | Skipped in every run until the value is corrected in Google; the rest of the school syncs |
| Another failure later in the same run | Handled as today; the skipped course is still reported (AC-009) |
| Extreme stored pair in a school-scale report | 100 % or 0 % without dividing (FR-005); page and export are built |
| Any other exception | Unchanged: the run stops with `Unexpected` |
| In-range negative value from Google (e.g. −1) | Unchanged: the database refuses it and the run stops with `Unexpected` (L-1, out of scope) |

## 9. Non-Functional Requirements

- NFR-062 stack unchanged; no NuGet package; no migration; no schema, endpoint or UI change.
- TC-4: every test uses synthetic values and substitutes the Google port; the reader-level range
  check is tested without a live Google call.
- TC-2: integration tests that assert stored rows run on PostgreSQL via Testcontainers.
- No user-visible string is added (the log line is not user-visible; no translation entry).

## 10. Out of Scope

- Showing skipped courses in the interface (v98 asks only for the run log).
- `NaN` and infinity — Google cannot send them in JSON.
- Any change to how values within range are stored or shown (US-055).
- The "no conversion" mode — it already shows raw points out of the maximum.
- Precision loss of in-range values (e.g. a tiny `double` below `decimal` resolution becoming 0) —
  that is today's conversion, unchanged.
- Any schema change: the non-negative constraints stay (OD-001); no migration.
- An in-range negative value from Google (L-1) — still stops the run; a possible new
  `trebovaniya.md` §7 item for the Owner, not resolved here.

## 11. Open Decisions

None open. The Story has none; §7 of `trebovaniya.md` holds no open item this Story depends on.
One decision is recorded as resolved in `docs/decisions/US-057-open-decisions.md` v2:

- **OD-001 Keep the non-negative constraints, or store negatives? — RESOLVED (a), the Owner,
  2026-10-25.** The schema is kept. Impact: AC-007 restated (a stored 0, plus a negative pair
  through a substituted journal port); AC-010 restated to the upper bound; VR-002 and FR-005
  note; no migration (DB_DESIGN expected NOT_APPLICABLE).

Interpretations made here (visible for SPEC_REVIEW and the Owner):

- I-1 "больше, чем программа может хранить" is read in both directions (beyond `decimal.MinValue`
  too) — Story note; §4 gives no other rule for a large negative value. (FR-001, AC-002)
- I-2 One out-of-range value skips the whole course, not just the item or submission — §4 says the
  course is skipped. (FR-002)
- I-3 The skipped course is not counted as processed and gets its own log event 5135, separate
  from `SyncCourseGone`, so the log tells the two reasons apart. (FR-002.3, FR-003)
- I-4 "Следующий прогон читает курс снова" is met by writing nothing for the course, including
  its complete-read `updateTime`; no new stored marker. (FR-004)
- I-5 "Отрицательные — 0 %" is applied as "points ≤ 0 → 0 %", which equals today's result for 0.
  (FR-005)
- I-6 "Отчёт строится при любых сохранённых баллах" is met for every value the database can
  hold (non-negative); the negative rule of §4 v98 is met by FR-005 for any value it receives and
  proved without a stored row (AC-007b). (FR-005, AC-007, OD-001)

Known limitation (for the Owner, not resolved here):

- L-1 An **in-range** negative maximum or grade from Google (e.g. −1) is not skipped by FR-001
  — it is within the `decimal` range — and the database refuses it, so the course's transaction
  fails and the whole run stops as `Unexpected`, the pattern v98 removes for out-of-range values.
  Whether Classroom can send a negative value at all is not established in the requirements. If
  it matters, it is a new `trebovaniya.md` §7 item (DB design v1 §4).

## 12. Traceability

| AC | FR / VR | `trebovaniya.md` |
|---|---|---|
| AC-001 | FR-001, FR-002, VR-001 | §4 Epic 1 "Когда повторы не помогли" (v98) |
| AC-002 | FR-001, FR-002, VR-001 | same; I-1 |
| AC-003 | FR-002.1 | same ("уже загруженные данные этого курса остаются как были") |
| AC-004 | FR-003, §7 | same ("пишется номер курса без самих значений"); SC-10 |
| AC-005 | FR-004 | same ("следующий прогон читает курс снова"); §4 "Инкрементальная синхронизация" (v90) |
| AC-006 | FR-005.1 | §4 Epic 3 "Перевод в шкалу школы" (v98) |
| AC-007 | FR-005.2, VR-002 | same; OD-001 |
| AC-008 | FR-005.3 | same (v84 preset) |
| AC-009 | FR-002.2 | §4 Epic 1 (skip ≠ run failure) |
| AC-010 | FR-001, VR-001 | §4 Epic 1 ("больше примерно 7,9·10²⁸"); OD-001 |
