---
id: US-061
epic: EPIC-4
title: Meet reports warn about a marked course, and a marked course is not a Meet link candidate
slug: meet-reports-and-linking-for-marked-courses
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-27 from trebovaniya.md v102 (`33174fe`,
# derived rules BR-062, BR-065 in `236832a`). The Meet part of the course marks
# (US-059): v102 items 1 (Meet report mark) and 2 (not a link candidate). Before
# the pilot.
---

# User Story

As a **Dean**

I want Meet reports of a course that is no longer read to say so, and a
recreated course to get its Meet code without the old one getting in the way

So that I do not read a frozen roster as today's, and meetings of a course
created anew land in it by themselves.

---

# Business Value

1. A course marked "not in Classroom" or "not visible to the technical account"
   (US-059) is no longer read, so its roster stays as it was on the last read:
   students who joined later count as "not on the course list", those who left
   still count as its students. Without a warning the Dean reads the report as
   current (v102, Epic 4).
2. A course recreated in Classroom instead of a deleted one has the same teacher
   and students as the old, marked course. The Meet code scoring gives both the
   same share, so the new code is never linked automatically and every meeting
   waits in "unlinked meetings" (v102, Epic 4, BR-065).

---

# Scope

**In scope:**

- Meet reports 1–4 of a course with either mark, on screen and in their export,
  show the course's mark with its date and «состав курса — на <дату последнего
  успешного чтения курса>» (BR-062, v102); the texts come from the translation
  files (NFR-073);
- a course with either mark is not a candidate: the automatic link never chooses
  it and the candidate list of an unlinked code does not offer it (BR-065, v102);
- the share of the remaining candidates is computed as before; an unlinked code
  that becomes unambiguous without the marked course is linked automatically at
  the next Meet load, as any unlinked code is;
- the Dean or Admin can still choose a marked course for a code by hand; links
  already made to a marked course stay;
- tests with synthetic data, the Google ports substituted (TC-4).

**Out of scope:**

- marks in the course list, card, journal, report and export (US-060);
- banner and «Стан даних» (US-062);
- re-examining existing links when a course gets a mark (v102: they stay);
- any change to synchronization or to the shares' thresholds.

---

# Acceptance Criteria

## AC-001 A Meet report of a marked course warns about its roster

**Given** a course marked "not in Classroom" since D1, last read successfully at
R1, with linked meetings

**When** the Dean opens Meet reports 1–4 for it and exports them

**Then** each, on screen and in the file, shows «Курса нет в Classroom с D1» and
«состав курса — на R1»; the figures are computed as before.

## AC-002 The same for a course "not visible to the technical account"

**Given** a course marked "not visible to the technical account" since D2, last
read successfully at R2

**When** the Dean opens and exports its Meet reports

**Then** they show «не виден техническому аккаунту с D2» and «состав курса — на
R2».

## AC-003 An unmarked course's reports have no warning

**Given** an ordinary course with meetings

**When** the Dean opens and exports its Meet reports

**Then** neither text appears.

## AC-004 A recreated course gets its code automatically

**Given** an old course marked "not in Classroom" and a new course with the same
teacher and students, and an unlinked code whose meetings that teacher organized
with those students

**When** Meet data is loaded

**Then** the code is linked automatically to the new course.

## AC-005 A marked course is not offered as a candidate

**Given** an unlinked code whose candidates by share are a marked course and two
unmarked courses with close shares

**When** the Dean opens "unlinked meetings"

**Then** the code is listed with the two unmarked courses as candidates and without
the marked one.

## AC-006 A marked course can still be chosen by hand

**Given** an unlinked code and a marked course

**When** the Dean chooses the marked course for the code

**Then** the code is linked to it, as a human's link, with the audit event of
any manual link.

## AC-007 Existing links to a marked course stay

**Given** a code linked automatically to a course that later becomes marked

**When** Meet data is loaded again

**Then** the code stays linked to that course.

## AC-008 Reports follow the user's language and work in read-only mode

**Given** the data of AC-001 and the installation in read-only mode

**When** a user with English chosen opens and exports the reports

**Then** both texts are in English and the export works.

---

# Open Decisions

All resolved by the Owner on 2026-10-27 before activation (option a) each).

## OD-001 Roster date when the last successful read is not known

As US-060 OD-002: `Course.LastCompleteReadAt` exists only since US-058.

- **a) (recommended)** — show the course mark and the roster line without a date
  («состав курса — на дату последнего чтения»), worded together with US-060 OD-002
  so both read the same.
- b) — omit the roster line when there is no date.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-002 Is the mark shown on the manual course-choice page?

v102 keeps the manual pick of a marked course allowed but does not say whether the
choice page shows the mark.

- **a) (recommended)** — yes, the course's mark with its date next to its name, so
  the Dean does not pick a deleted course by mistake; nothing else on the page
  changes.
- b) — no; the page stays as it is.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

---

# Notes

- **Code today:** scoring `MeetCodeScoringSource` / `LinkMeetCodesStep` (no mark
  filter), candidate list `GetMeetCodesQuery`, manual choice
  `GetMeetCodeCourseChoiceQuery` / `SetMeetCodeCourseUseCase`, reports
  `GetMeetReportsQuery` / `GetMeetReportMeetingQuery` / `MeetReportSource`, export
  `ExportMeetReportsCommand`, `ExportMeetMeetingCommand` with
  `MeetReportsWorkbookMapper`, `MeetMeetingWorkbookMapper`.
- **Read-only mode:** the automatic link runs inside synchronization, which does not
  run; the reports are viewing and work (BR-026).
- No schema change expected (DB_DESIGN likely NOT_APPLICABLE); no new endpoint.
- No new NuGet package, no new requirements version.
