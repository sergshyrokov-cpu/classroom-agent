---
id: US-057
epic: EPIC-1
title: A course with points beyond the program's range is skipped, and the school scale never fails a report
slug: out-of-range-grades-do-not-stop-sync
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from the US-055 security review (INFO-1, INFO-2,
# 2026-10-25) and trebovaniya.md v98; the Owner decided on 2026-10-25 to raise
# both cases in the requirements first. Aligned with trebovaniya.md v98.
---

# User Story

As an **Admin**

I want a course whose points Google returned beyond what the program can hold to
be skipped instead of stopping the run, and the school scale in a report to work
with any stored points

So that one unusual value never stops the school's whole synchronization or
breaks a report page.

---

# Business Value

US-055 widened the three points columns, so the database now holds any value the
program can read. Two extreme cases remained (US-055 security review, INFO-1 and
INFO-2); v98 settled both in `trebovaniya.md`:

1. **Synchronization (§4, Epic 1, "Когда повторы не помогли").** Google returns
   points as a `double`; the program keeps them as `decimal`, which ends at about
   7.9·10²⁸. Today a larger value throws when it is read, the run stops as an
   unexpected error, and every next run stops at the same course until the
   teacher changes the value in Google — the pattern that blocked the pilot in
   P-9. v98: such a course **is skipped, the run continues**; the data already
   stored for that course stays as it was; the run log names the course without
   the values; the next run reads the course again.
2. **Report in the school scale (§4, Epic 3, "Перевод в шкалу школы").** The
   percent is computed as points ÷ maximum × 100 before it is clamped, so an
   extreme pair (very large points over a very small maximum) can overflow and
   the page shows the generic "unexpected error, try later" — retrying never
   helps. v98: the percent is bounded to 0–100 %; points not less than the
   maximum give 100 %, negative points give 0 %; **the report is built whatever
   points are stored**.

Both cases are practically unreachable today; the Story closes them before the
pilot rather than after a school meets one.

---

# Scope

**In scope:**

- synchronization recognises a maximum points, assigned grade or draft grade
  that the program cannot represent (beyond the `decimal` range, in either
  direction) and skips that whole course, the way a course that vanished
  mid-run (`404`) is skipped: nothing of it is written, a copy already stored
  stays unchanged, the remaining courses, the Meet step and Meet code linking
  run, and the run ends `completed` if nothing else fails;
- one run-log line per skipped course with the run and the course's Google id
  only — never the value, the task, the student or the submission (SC-10);
- the next run reads the skipped course again, archived or not (it was not fully
  read, so incremental sync must not treat it as unchanged);
- the school-scale percent in the on-screen report and in every export built
  from it: points ≥ maximum → 100 % and points ≤ 0 → 0 % without dividing;
  otherwise as today;
- tests with synthetic values only (TC-4); the Google port substituted.

**Out of scope:**

- showing skipped courses in the interface — v98 asks only for the run log
  (like `404` courses and the incremental-sync counter);
- values Google cannot send in JSON (not a number, infinity);
- any change to how values within range are stored or shown (US-055);
- the "no conversion" mode — it already shows raw points out of the maximum.

---

# Acceptance Criteria

## AC-001 A course with too-large maximum points is skipped

**Given** three courses, the second of which holds a task whose maximum points
in Google is 1·10³⁰ (synthetic)

**When** synchronization runs

**Then** the first and third courses and their items are stored, nothing of the
second course is written, the Meet step runs, and `SyncState` is `completed`.

## AC-002 A too-large grade skips the course the same way

**Given** a course with a submission whose assigned grade is 1·10³⁰, and another
course with a submission whose draft grade is −1·10³⁰ (synthetic)

**When** synchronization runs

**Then** both courses are skipped as in AC-001 and the run is `completed`.

## AC-003 The stored copy of a skipped course stays as it was

**Given** a course already stored by an earlier run, in which Google now returns
a task with maximum points 1·10³⁰

**When** synchronization runs

**Then** the course, its items, its memberships and its submissions stay exactly
as the earlier run stored them.

## AC-004 The skip is logged without the value

**Given** the data of AC-001

**When** synchronization runs

**Then** the run log has one warning naming the run and the second course's
Google id, and no log line contains the value, the task's id or title, a
student, or a submission id.

## AC-005 The next run reads the course again

**Given** an archived course that was skipped by AC-001's rule, and whose
`updateTime` in Google has not changed since

**When** the next synchronization runs after the teacher corrected the value

**Then** the course is read in full and stored.

## AC-006 Points not less than the maximum give 100 %

**Given** a template in the school scale and a stored grade whose points are
7·10²⁸ against a maximum of 0.0001 (synthetic)

**When** a Dean opens the report on screen and exports it

**Then** both show the scale label for 100 %, and neither fails.

## AC-007 Negative points give 0 %

**Given** a template in the school scale and a stored grade of −7·10²⁸ against a
maximum of 0.0001 (synthetic)

**When** a Dean opens the report on screen and exports it

**Then** both show the scale label for 0 %, and neither fails.

## AC-008 Ordinary points are converted as before

**Given** a template in the school scale and grades of 6 of 12, 12 of 12 and 0 of
12

**When** a Dean opens the report

**Then** they show the labels for 50 %, 100 % and 0 %, as before this Story.

---

# Open Decisions

None. The Owner decided on 2026-10-25 to settle both cases in the requirements
(v98, `88183e2`); this Story implements them.

---

# Notes

- Origin: US-055 security review INFO-1 and INFO-2 (2026-10-25).
- "Больше, чем программа может хранить" in §4 is read as beyond the range in
  either direction: a value below about −7.9·10²⁸ fails the same way, and §4
  gives no other rule for it.
- Code today: the cast `(decimal)double` in `GoogleClassroomReader.cs:352`,
  `384–385` throws `OverflowException`; the reads run before the course
  transaction (`RunSynchronizationUseCase.cs` ~495–518), next to the existing
  `CourseGone` skip and its host log line `SyncCourseGone`
  (`SynchronizationBackgroundService.cs:410`). The report percent is
  `ReportBuilder.Grade` (`ReportBuilder.cs:256`).
- No Google SDK exception or type may reach Application (AD-4): the reader turns
  the overflow into a port-level failure.
- Expected stages: API_DESIGN NOT_APPLICABLE, DB_DESIGN NOT_APPLICABLE (no
  endpoint or schema change).
- No new NuGet package, no new requirements version.
