---
id: US-058
epic: EPIC-1
title: Synchronization marks items no longer in Classroom and records the courses it skips
slug: sync-marks-items-gone-and-records-skipped-courses
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from trebovaniya.md v99 (`ad3865e`) and the
# Owner's v99 decisions of 2026-10-26. First of three v99 Stories, all before
# the pilot (Owner 2026-10-26): (1) this one — the data; (2) marks in the
# journal, report and export; (3) the data-health banner and «Стан даних» page.
---

# User Story

As a **Dean**

I want synchronization to keep a record of coursework that Google no longer
returns and of courses it could not read

So that the next two v99 Stories can show me where the journal no longer
matches Classroom, instead of the program silently presenting stale data as
the truth.

---

# Business Value

Deans take what the program shows as the truth and are not set up to check it
(Owner, 2026-10-26). Two things today stay invisible:

1. **An item deleted in Classroom (or returned to draft)** stays in the journal
   unchanged: synchronization only adds and updates items, it never notices that
   one is gone. v99 (§4 Epic 1, BR-045): such an item is kept with its grades and
   marked "not in Classroom" with the date it was noticed.
2. **A course synchronization skips** — for points out of range (v98) or for a
   course state the program does not know — is written only to the run log.
   v99 (§3 `SyncState`, §4 Epic 1, BR-046): the skip is recorded with the
   course's Google id, its name from Google's course list, the reason and since
   when, until a run reads the course successfully.

This Story stores both facts. It shows nothing yet: the marks in the journal,
report and export are Story 2; the banner and the «Стан даних» page are Story 3.
Shipping the data first lets the marks accumulate from the day it is deployed.

---

# Scope

**In scope:**

- in a **full read** of a course (one that is read and committed, not skipped,
  not an unchanged archived course): every stored item of either resource
  (`courseWork`, `material`) that Google did not return gets the mark "not in
  Classroom" with the instant it was first noticed; its submissions and grades
  are untouched;
- the mark keeps its first date while the item stays absent; an item that Google
  returns again loses the mark and is updated as usual;
- the mark is written in the course's own transaction, so a course whose read or
  write fails keeps its marks exactly as before;
- a record of each course the run skips **for out-of-range values** (BR-043,
  v98) or **for an unknown course state**: Google course id, the course name from
  Google's course list as read in that run, the reason, and the instant of the
  first skip in the current streak;
- the record keeps its first instant while the course keeps being skipped (the
  name is refreshed from the latest course list); it is removed when a run reads
  the course successfully, or when the course is no longer in Google's course
  list;
- one run-log line with the numbers of items marked and unmarked in the run,
  and no titles, ids of items or people (SC-10);
- the EF Core migration for whatever the DB design decides (PC-2);
- tests with synthetic data only, the Google port substituted (TC-4).

**Out of scope:**

- any display — journal, report, export (Story 2); banner, «Стан даних», who
  sees what (Story 3);
- a course that vanishes mid-run (`404`): it is not recorded, see Notes;
- courses not imported because they are older than the retention period N
  (§5): that is policy, not a problem;
- a course deleted in Google as a whole: v99 says nothing about it;
- telling a deleted item from one returned to draft — Google's answer cannot
  (v99).

---

# Acceptance Criteria

## AC-001 A coursework item Google no longer returns is marked

**Given** a stored course with two graded items, each with a graded submission,
and Google now returns only the first of them

**When** synchronization runs

**Then** the second item is still stored with the mark "not in Classroom" and the
run's instant, its submission and grade are unchanged, and the first item has no
mark.

## AC-002 A material Google no longer returns is marked the same way

**Given** a stored course with a material that Google no longer returns

**When** synchronization runs

**Then** the material is still stored and carries the mark with the run's instant.

## AC-003 The mark keeps its first date

**Given** an item marked by an earlier run, still absent from Google's answer

**When** synchronization runs again

**Then** the item is still marked and its date is the earlier run's.

## AC-004 An item that comes back loses the mark

**Given** a marked item that Google returns again, with a changed title

**When** synchronization runs

**Then** the item has no mark and carries the new title.

## AC-005 Courses that are not read in full get no marks

**Given** an unchanged archived course whose stored item Google would no longer
return, and a course skipped for an out-of-range value whose stored item Google
no longer returns

**When** synchronization runs

**Then** neither item gains a mark, and items already marked in them keep their
marks.

## AC-006 A course that fails keeps its marks as they were

**Given** a stored course whose read fails after its items were listed, so the
run stops

**When** synchronization runs

**Then** no item of that course gains or loses a mark.

## AC-007 A skipped course is recorded

**Given** a course never loaded before that the run skips for an out-of-range
value, and a course whose state the program does not know

**When** synchronization runs

**Then** each is recorded with its Google id, its name from Google's course list,
its reason, and the run's instant.

## AC-008 The record keeps its first instant

**Given** a course recorded as skipped by an earlier run, skipped again, whose
name in Google's course list has changed

**When** synchronization runs

**Then** the record keeps the earlier instant and carries the new name.

## AC-009 The record goes when the course is read

**Given** a course recorded as skipped, whose value the teacher has corrected

**When** synchronization runs and reads the course successfully

**Then** the record is gone.

## AC-010 The record goes when the course leaves Google's list

**Given** a course recorded as skipped that is no longer in Google's course list

**When** synchronization runs

**Then** the record is gone.

## AC-011 What is not recorded

**Given** a course that vanishes mid-run (`404`) and a new course older than the
retention period N

**When** synchronization runs

**Then** neither is recorded as skipped.

## AC-012 The run log counts marks without naming anything

**Given** the data of AC-001 and AC-004 in one run

**When** synchronization runs

**Then** the run log has one line giving one item marked and one unmarked, and no
log line contains an item's title or id, a person or a grade.

---

# Open Decisions

None. The Owner decided the behaviour on 2026-10-26 (v99 `ad3865e`, derived
rules BR-045, BR-046 in `5f048dd`).

---

# Notes

- **`404` is not recorded.** BR-046 cites BR-043, which also covers a course that
  vanishes mid-run. But v99 gives that case no notice kind (BR-047 lists only
  out-of-range values and an unknown state), and the course leaves Google's list
  by the next run anyway, so a record would only flash. Spec review should
  confirm this reading.
- **A failed run** leaves both the marks and the skip records of the courses it
  did not reach as they were; only a course the run actually handles changes
  them.
- **Read-only mode:** synchronization does not run, so nothing here changes; no
  new service write is needed for BR-026.
- **Retention purge and erasure (Epic 10):** a marked item is an ordinary item
  and is purged with its course; a skip record holds no personal data. The spec
  states what the purge does with a record whose course it deletes.
- Code today: items are upserted in `ImportCourseWorkAsync`
  (`RunSynchronizationUseCase.cs` ~701), which reads the stored items of the
  course first — the natural place to find the absent ones; the course
  transaction commits after `course.RecordCompleteRead()`. The unknown-state skip
  is `progress.SkippedCourses` (~471), the out-of-range skip is next to it
  (~522); both are log-only today. `CourseWork.List` is called without
  `courseWorkStates`, so Google returns published items only — a draft is
  absent, hence v99's wording.
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint); DB_DESIGN applies
  (the mark on `CourseWork`, the skip records).
- No new NuGet package, no new requirements version.
