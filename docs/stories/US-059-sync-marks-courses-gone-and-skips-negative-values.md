---
id: US-059
epic: EPIC-1
title: Synchronization marks courses no longer in Classroom, guards against an empty course list, and skips a course with negative values
slug: sync-marks-courses-gone-and-skips-negative-values
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from trebovaniya.md v100 (`a687d76`, derived rules
# in `be29d49`) and the Owner's decisions of 2026-10-26/27. The synchronization
# part of v100; showing the course mark joins v99 Stories 2 (journal, report,
# export) and 3 (banner, «Стан даних»). Before the pilot (Owner 2026-10-26).
---

# User Story

As a **Dean**

I want synchronization to notice a course that Google no longer lists, without
being fooled by an empty list, and to skip a course with negative points instead
of stopping

So that a course deleted in Classroom does not silently pass for a live one, and
one bad value in one course does not stop every course from updating.

---

# Business Value

1. **A course deleted in Classroom** today just stops being read: it keeps its
   last stored status (possibly "active"), its grades count in reports, and
   nothing says it is gone. v100 (§3 `Course`, §4 Epic 1, BR-048): a stored course
   missing from a course list read to its end is kept and marked "not in
   Classroom" with the date it was noticed; the mark goes if the course comes
   back.
2. **An empty course list.** If the technical account loses its super-admin role,
   Google returns an empty list without an error (v97) — the rule above would
   mark every course. v100: an empty list while courses are stored marks nothing,
   and the run records it as a technical problem (BR-047, BR-048).
3. **A negative grade or maximum** passes the reader and hits the database check
   constraints (`ck_submission_grades_non_negative`,
   `ck_course_work_max_points_non_negative`), which stops the whole run
   (US-057 spec review m-4, L-1). v100 (§4 Epic 1, BR-043): such a course is
   skipped like one with values above the range.

This Story stores the facts. Showing them is v99 Stories 2 and 3.

---

# Scope

**In scope:**

- when the course list is read **to its end** and is **not empty**: every stored
  course whose Google id the list did not yield gets the mark "not in Classroom"
  with the run's instant; its roster, items, submissions and grades, and its
  stored status are untouched; its items gain no mark of their own;
- the mark keeps its first date while the course stays absent; a marked course
  that the list yields again loses the mark and is handled as usual in that run;
- a course listed but not read (skipped, unchanged archived, older than the
  retention period N, unknown state, no name) is listed, so it is not marked;
- **empty-list guard**: a list read to its end that yields no course while at
  least one course is stored marks nothing, removes no skip record (US-058
  FR-007.3) and stores in `SyncState` that the last course list came back empty;
  the next run that reads a non-empty list clears it;
- a course in which Google returns a **negative** grade, draft grade or maximum
  points is skipped exactly like one above the range (BR-043, v98): its loaded
  data stays, it is recorded as skipped (US-058, reason out-of-range values), the
  run's log has its course id without the values, the run goes on;
- one run-log line with the numbers of courses marked and unmarked in the run,
  no names or ids (SC-10); the empty-list case logged without personal data;
- the EF Core migration for whatever the DB design decides (PC-2);
- tests with synthetic data only, the Google port substituted (TC-4).

**Out of scope:**

- any display: the mark in the course list, course card, journal, report and
  export (Story 2); the notices, banner, «Стан даних», the Dean's hint to contact
  the Admin (Story 3);
- a course that vanishes mid-run (`404`): skipped as today, marked only if the
  next list lacks it (BR-048);
- any threshold for a partial loss of courses (v100: none);
- telling a deleted course from one that left the technical account's view —
  Google's answer cannot.

---

# Acceptance Criteria

## AC-001 A course Google no longer lists is marked

**Given** two stored courses, each with an item and a graded submission, and
Google's course list now yields only the first

**When** synchronization runs

**Then** the second course is still stored with its status, item, submission and
grade unchanged, and carries the mark "not in Classroom" with the run's instant;
its item has no mark; the first course has no mark.

## AC-002 The mark keeps its first date

**Given** a course marked by an earlier run and still absent from the list

**When** synchronization runs again

**Then** the course is still marked and its date is the earlier run's.

## AC-003 A course that comes back loses the mark

**Given** a marked course that the list yields again, with a changed name

**When** synchronization runs

**Then** the course has no mark and carries the new name.

## AC-004 A listed course that is not read is not marked

**Given** stored courses that the list yields but the run does not read: an
unchanged archived course, a course skipped for out-of-range values, and a course
in a state the program does not know

**When** synchronization runs

**Then** none of them is marked.

## AC-005 A run that stops marks nothing

**Given** a stored course absent from the list, and a run that fails before the
list is read to its end

**When** synchronization runs

**Then** the course gains no mark and a marked course keeps its mark.

## AC-006 An empty list marks nothing

**Given** stored courses, one of them marked, a skip record, and Google's course
list read to its end with no course in it

**When** synchronization runs

**Then** no course gains or loses a mark, the skip record stays, and `SyncState`
holds that the last course list came back empty.

## AC-007 A non-empty list clears the empty-list flag

**Given** `SyncState` holds that the last course list came back empty

**When** a run reads a list with courses in it

**Then** the flag is cleared and the courses are marked by the ordinary rule.

## AC-008 An empty list on a new installation is no problem

**Given** no course is stored

**When** a run reads an empty course list

**Then** the run completes and the empty-list flag is not set.

## AC-009 A negative value skips the course instead of stopping the run

**Given** three courses: one with a negative assigned grade, one with a negative
draft grade, one with a negative maximum points, and a fourth course with
ordinary values

**When** synchronization runs

**Then** the run completes, the fourth course is loaded, each of the three is
recorded as skipped for out-of-range values with its already loaded data
unchanged, and the run's log has their course ids and no values.

## AC-010 The run log counts course marks without naming anything

**Given** the data of AC-001 and AC-003 in one run

**When** synchronization runs

**Then** the run log has one line giving one course marked and one unmarked, and
no log line contains a course's name or id, a person or a grade.

---

# Open Decisions

None. The Owner decided the behaviour on 2026-10-26/27 (v100 `a687d76`, derived
rules BR-043, BR-047, BR-048 in `be29d49`).

---

# Notes

- **Code today.** The run already collects the listed course ids (`listed`,
  `RunSynchronizationUseCase.cs` ~478) and, only after the list is read to its
  end, removes the skip records of unlisted courses (~588-598, US-058 FR-007.3).
  The course mark belongs at the same point, and the empty-list guard must also
  hold back that removal. `ReadCoursesAsync` has no `courseStates` or per-user
  filter (`GoogleClassroomReader.cs` ~126), so absence means Google really did
  not return the course.
- **Negative values.** The reader turns values beyond `decimal` into
  `GoogleReadFailureKind.ValueOutOfRange` (`GoogleClassroomReader.cs` ~392-409);
  negatives pass and fail on the check constraints at save, which ends the run.
  Where the negative check lives (reader or use case) is the spec's and design's
  call; the skip reason stays `CourseSkipReason.ValueOutOfRange`, since the
  notice text (v98) already says "недопустимое значение баллов".
- **Read-only mode:** synchronization does not run, nothing here changes; no new
  service write for BR-026.
- **Retention purge:** a marked course is purged by the ordinary rule (BR-072);
  its last activity no longer grows. Nothing new to purge: the mark is on the
  course row, the flag is in `SyncState`.
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint); DB_DESIGN applies
  (the course mark, the empty-list flag).
- No new NuGet package, no new requirements version.
