---
id: US-056
epic: EPIC-1
title: Members seen when a course is first loaded count from the course's creation
slug: membership-starts-at-course-creation
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from deployment rehearsal finding P-14
# (D:\HyperV\rehearsal-findings.md, 2026-10-23) and trebovaniya.md v98;
# decisions by the Owner on 2026-10-23 (option 1) and 2026-10-25 (1a-5a).
# Aligned with trebovaniya.md v98.
---

# User Story

As a **Dean**

I want the people who were already in a course when the program first loaded it
to count as its members from the day the course was created in Google

So that journals, reports and Meet reports for periods before the first
synchronization show the course's real teachers and students.

---

# Business Value

Until v98 a membership started on the day synchronization first saw the person
(`trebovaniya.md` §3, v31: "всё, что было до первой синхронизации, считается
начавшимся в её день"). On a new installation that leaves every period before the
first synchronization without members. The deployment rehearsal on the college's
real domain (finding P-14, 2026-10-23) showed what that means:

- the report for 01.09–31.12.2025 had an empty «Викладачі:» line on screen and in
  both exports; students appeared only through their submissions;
- in the Meet report for 01.09–21.10.2026 the organizer was "не викладач цього
  курсу" on every one of 24 meetings, with 0 course teachers and 0 course students
  — every meeting predated the first synchronization;
- Meet codes could not link automatically, because nobody was a course member on
  any meeting date.

Every school starts with exactly this situation, so the pilot cannot show a Dean
a meaningful report for the current school year without this Story.

v98 (§3, Epic 3, Epic 4; BR-051, PC-8) sets the rule: whoever is on the roster or
found through submissions in the run that first loads a course is a member from
the course's Google `creationTime` (from that run's date when Google gave none);
whoever appears later is a member from the day synchronization first saw them.
The membership still ends on its last-seen date. The rule covers every course,
including courses created after the first synchronization, and memberships
stored before v98.

---

# Scope

**In scope:**

- the start of a membership, as journals, reports and Meet reports use it, follows
  the v98 rule;
- every place that decides "a member of the course in a period" or "on a date":
  the journal's student list, the report's teacher line and student list, Meet
  reports (course student / course teacher / organizer marks, report 3 student
  list) and automatic Meet code linking;
- memberships already stored in an installation's database follow the rule after
  the upgrade, without a new synchronization;
- tests on real PostgreSQL (TC-2) with synthetic Google data (TC-4).

**Out of scope:**

- the dates "впервые / последний раз замечен" themselves: they keep meaning what
  synchronization observed and are not rewritten;
- the leaver expiry (PC-11) and the retention purge: they keep counting from the
  last-seen date;
- the known limitation of v98: in a course that lives for years with changing
  students, current members count from the course's creation and appear in
  journals and Meet reports for years they did not study; people who left before
  the course was first loaded are known only through their submissions;
- the v98 extreme-value rules from the US-055 security review (a course with
  points too large to store is skipped; the scale percentage is clamped to
  0–100 %) — a separate Story, US-057;
- interface changes: no screen shows the start of a membership.

---

# Acceptance Criteria

## AC-001 Students of the first load count from the course's creation

**Given** a course created in Google on 2025-09-01 with student S on its roster,
loaded by the installation's first synchronization on 2026-10-22, and S has no
submissions

**When** a Dean opens the course journal for 01.09–31.12.2025

**Then** S is listed in the journal.

## AC-002 Teachers of the first load appear in reports for earlier periods

**Given** the course of AC-001 with teacher T on its roster at the first load

**When** a Dean builds a report for that course for 01.09–31.12.2025

**Then** the teacher line names T, on screen and in the exports.

## AC-003 Meet reports know the course's members before the first synchronization

**Given** the course of AC-001 and AC-002 with a Meet code linked to it, and a
meeting on 2025-10-15 organized by T and joined by S

**When** a Dean opens the Meet report for a period containing that meeting

**Then** T is marked as the organizer and a course teacher, S as a course student,
and report 3 lists S among the course's students for the period.

## AC-004 Automatic Meet code linking uses the same start

**Given** the course of AC-001 and AC-002 and an unlinked Meet code whose meetings
all predate the first synchronization and are organized by T and attended by the
course's students

**When** automatic linking runs

**Then** the code is scored and linked exactly as it would be for meetings after
the first synchronization.

## AC-005 Someone found through submissions at the first load counts from creation

**Given** a course created on 2025-09-01 whose first load on 2026-10-22 finds
submissions of student L, who is not on the roster

**When** a Dean opens the course journal for 01.11–30.11.2025, when L has no
submission in that period

**Then** L is listed: L's membership runs from 2025-09-01 to 2026-10-22.

## AC-006 Without a creation date the first load's date is used

**Given** a course for which Google returned no `creationTime`, first loaded on
2026-10-22 with student S on its roster

**When** a Dean opens the course journal for 01.09–31.12.2025 and for
01.10–31.10.2026

**Then** S is not listed for 2025 (no submissions there), and is listed for
October 2026.

## AC-007 Later arrivals count from the day they were seen

**Given** a course created on 2025-09-01 and first loaded on 2026-10-22, and
student N who joined its roster later and was first seen by the run of
2026-11-05

**When** a Dean opens the journal for 01.10–31.10.2026 and the Meet report for a
meeting of 2026-10-30

**Then** N is in neither (N has no submissions there); N is a member from
2026-11-05.

## AC-008 A course created after the first synchronization follows the same rule

**Given** an installation whose first synchronization ran on 2026-10-22, and a
course created in Google on 2026-10-20 that the first synchronization did not
load and that a later run on 2026-11-05 loads for the first time with student S

**When** a Dean opens the course journal for 20.10–31.10.2026

**Then** S is listed: S counts from the course's creation, 2026-10-20.

## AC-009 Memberships stored before the upgrade follow the rule

**Given** an installation database at the schema before this Story, holding a
course with `creationTime` 2025-09-01 whose memberships were stored by the run
that first loaded it on 2026-10-22, and one membership first seen by a later run
on 2026-11-05 (synthetic)

**When** the installation is upgraded and a Dean opens the journal for
01.09–31.12.2025 before any new synchronization

**Then** the members stored by the first load are listed, and the member first
seen on 2026-11-05 is not.

## AC-010 The observed dates and the leaver expiry are unchanged

**Given** the memberships of AC-001…AC-009

**When** synchronization runs again and the leaver expiry is evaluated

**Then** each membership's first-seen and last-seen dates are what synchronization
observed, and a leaver's expiry still counts from the last-seen date.

---

# Open Decisions

None. The Owner chose option 1 on 2026-10-23 and answered the follow-up questions
on 2026-10-25 (1a–5a), all written into `trebovaniya.md` v98:

1. members found through submissions at the first load also count from
   `creationTime` (a);
2. without `creationTime`, the first-load run's date (a);
3. multi-year courses — accepted as a known limitation (a);
4. the rule applies to memberships stored before v98 (a);
5. the US-055 extreme-value rules go into the same version, delivered by a
   separate Story (a).

---

# Notes

- Origin: deployment rehearsal finding P-14, `D:\HyperV\rehearsal-findings.md`
  (items P-14 and 9, 2026-10-23). Pre-pilot blocker.
- Code today: the start is `CourseMembership.FirstSeenAt`
  (`CourseMembership.cs:38`), used in `RosterOnDate.cs:12`,
  `GetJournalQuery.cs:124`, `ReportBuilder.cs:97` and `:156`,
  `GetMeetReportsQuery.cs:156`, `MeetReportRules.cs:148`, and read by
  `JournalSource`, `JournalFieldSource`, `MeetReportSource` and
  `MeetCodeScoringSource`. `Course.CreationTime` already holds Google's value.
- PC-8 leaves the DB design to decide whether the start is stored or derived,
  and how memberships stored before v98 are recognised as seen in the run that
  first loaded their course.
- Expected stages: API_DESIGN NOT_APPLICABLE (no endpoint changes); DB_DESIGN
  applies (stored or derived start, possibly a migration).
- No new NuGet package, no new requirements version.
