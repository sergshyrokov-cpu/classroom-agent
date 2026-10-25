---
id: US-055
epic: EPIC-1
title: Points and grades of any size are stored as Google returned them
slug: grade-columns-fit-any-value
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from deployment rehearsal finding P-9
# (D:\HyperV\rehearsal-findings.md, session 4, 2026-10-23); decisions by the
# Owner on 2026-10-25. Aligned with trebovaniya.md v97.
---

# User Story

As an **Admin**

I want synchronization to store every task's maximum points and every grade
exactly as Google returned them, whatever their size

So that one task with unusually large points does not stop the school's whole
synchronization on every run.

---

# Business Value

`trebovaniya.md` §3 stores a task's maximum points and a submission's assigned
and draft grades "в баллах как есть", and v83 makes Google data a mirror stored as
Google returned it. The database does not allow that today: the three columns
`course_work.max_points`, `submission.assigned_grade` and `submission.draft_grade`
are `numeric(10,4)` (US-015 database design), which holds at most 999 999.9999 and
rounds to four decimal places.

The deployment rehearsal on the college's real domain (session 4, 2026-10-23)
hit the limit: after 4 793 courses the run failed with PostgreSQL error 22003
"numeric field overflow" (`SyncRunFailed`, diagnosis `Unexpected`, step
Classroom). Google holds a larger value somewhere in the college's Classroom (the
value was not read — it is student data). The run stops at the same course every
time (US-017 FR-005, unchanged by US-053 OD-001), so the courses after it and the
Meet step never load. This blocks the pilot: the college's first full
synchronization can never finish.

The code is brought to the requirement; the requirement does not change.

---

# Scope

**In scope:**

- the three columns hold any value that reaches the program from Google —
  the size and decimal places of the stored value are not limited by the
  column — and the stored value equals the value read from Google;
- an EF Core migration that changes the three columns and keeps every stored
  value unchanged;
- the journal, the reports and their exports show such values like any other
  (they already format whatever is stored);
- tests on real PostgreSQL (TC-2) with synthetic values above the old limit and
  with more than four decimal places (TC-4).

**Out of scope:**

- what the run does after a failure: it still stops (US-017 FR-005, US-053
  OD-001) — the Owner decided on 2026-10-25 not to change that rule here;
- a value Google returns that the program cannot represent at all (not a number,
  infinity, or beyond the range of the program's number type when it is read) —
  no such value has been seen; it would still stop the run with `Unexpected`,
  as any other unexpected error;
- the points scale and conversion in report templates (`ReportTemplate`);
- interface fixes found in the same rehearsal (P-10, P-12, P-13) — they go to the
  styling Story.

---

# Acceptance Criteria

## AC-001 Large maximum points are stored as returned

**Given** a course whose task has maximum points of 12 345 678.5 in Google
(synthetic)

**When** synchronization runs

**Then** the run completes, and the stored maximum points of that task equal
12 345 678.5.

## AC-002 Large grades are stored as returned

**Given** a submission whose assigned grade is 2 500 000 and whose draft grade is
1 000 000.25 in Google (synthetic)

**When** synchronization runs

**Then** the run completes, and both grades are stored with exactly those values.

## AC-003 Decimal places are not rounded

**Given** a task with maximum points 10.123456 and a submission graded 7.654321
in Google (synthetic)

**When** synchronization runs

**Then** the stored values are 10.123456 and 7.654321, not rounded to four
decimal places.

## AC-004 The courses after the unusual one still load

**Given** three courses, the second of which holds a task with maximum points
above 999 999.9999

**When** synchronization runs

**Then** all three courses and their items are stored, the Meet step runs, and
`SyncState` is `completed`.

## AC-005 Existing values survive the migration

**Given** an installation database at the current schema with stored maximum
points and grades (synthetic, within the old limit)

**When** the new migration is applied

**Then** every stored value is unchanged.

## AC-006 The journal and reports show such values

**Given** the data of AC-001 and AC-002

**When** a Dean opens the course journal and builds a report for that course

**Then** the cell shows the grade and the maximum points as stored, and neither
page fails.

---

# Open Decisions

None. The Owner decided on 2026-10-25: (a) only make the columns fit any value;
the rule that an unexpected error stops the run stays as it is.

---

# Notes

- Origin: deployment rehearsal finding P-9, `D:\HyperV\rehearsal-findings.md`
  (session 4, item 6, 2026-10-23). Pre-pilot blocker.
- Code today: `CourseWorkConfiguration.cs:47` and `SubmissionConfiguration.cs:47–48`
  (`HasColumnType("numeric(10,4)")`); the domain type is `decimal?`
  (`CourseWork.MaxPoints`, `SubmissionDetails.AssignedGrade/DraftGrade`); the
  Google reader casts the API's `double` to `decimal`
  (`GoogleClassroomReader.cs:352`, `384–385`).
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint changes); DB_DESIGN
  applies (column type change + migration).
- The first full synchronization of the college takes more than 24 hours
  (821 810 submissions in the rehearsal) — plan the pilot start around it.
- No new NuGet package, no new requirements version.
