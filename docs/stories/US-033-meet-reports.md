---
id: US-033
epic: EPIC-4
title: Built-in Meet reports for a course and period
slug: meet-reports
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v88.
# Scope decided with the Owner on 2026-10-10 in discovery (OD-001 … OD-010 below).
---

# User Story

As a **Dean** or an **Admin**

I want to pick a Classroom course and a period and see how its lessons in Meet
actually went — a summary, the list of meetings, each student's participation,
and any one meeting in detail

So that I can monitor the teaching process in Meet without collecting the
numbers by hand.

---

# Business Value

`trebovaniya.md` §4 Epic 4, BR-062…BR-064: the purpose of Epic 4 is the Dean's
control of how lessons really go in Meet. US-031 stores the meetings and US-032
links them to courses; this Story is the first screen that shows them. It shows
facts only — no lesson plan, lesson length, norms or comparison of teachers
(BR-062, Epic 11). It is not an attendance register: a lesson may take place in
a classroom or offline with materials in Classroom, and the absence of a Meet
meeting does not mean the absence of a student.

---

# Scope

## In scope

- **A "Meet reports" page**, a separate menu item, open to Dean and Admin:
  - **course** — the same drop-down as the journal (US-025 OD-001): every
    synchronized course by name, archived ones included; search arrives with
    US-020;
  - **period** — from and to dates; on first opening, the **current month** in
    the school's time zone;
  - for the chosen course and period, reports 1–3 on one page.
- **Which meetings belong to the course** — every meeting reached through any
  code linked to the course (PC-12), whether the link is automatic or confirmed
  (v55); meetings of a code marked "not a course" belong to no course (BR-083).
  An unconfirmed automatic link is not marked in the reports — they show facts
  only.
- **Which meetings belong to the period** — a meeting belongs to the day of its
  **start** in the school's time zone (NFR-074); a meeting that crosses midnight
  or the period's end belongs wholly to its start day.
- **Report 1 — Summary** (§4 Epic 4): number of meetings, total and average
  duration, average number of course students per meeting.
- **Report 2 — Meeting list**, ordered by start time (v58): date, start, end,
  duration, organizer, number of other course teachers, number of course
  students, number "not on the course list", number of other participants.
  Each row opens report 4.
- **Report 3 — Student participation** (Ukrainian label «Участь студентів»): for
  every student who was on the course roster for at least part of the period
  (v31) — on how many of the period's meetings they were and how many minutes in
  total. Students with no meeting are listed with zeros.
- **Report 4 — One meeting in detail**, on its own page: everyone who connected,
  each marked organizer, course teacher, course student, not on the course list
  or other participant (v58), with "N of M min" — their minutes out of the
  meeting's duration, no percentage. Other participants appear as one line with
  their count, never with an address or name (§3 `MeetParticipation`).
- **Counting rules** — BR-064 as written: only personal domain accounts; the
  organizer is named in their own column and never counted; other teachers of
  the course on the meeting's date have their own column; a domain account not
  on the roster on that date is "not on the course list"; external guests,
  connections without an account and non-personal domain accounts are "other
  participants". Roster membership is taken **on the meeting's date** (§3).
  An organizer who is not a teacher of the course on that date is marked "not a
  teacher of this course". Each Google meeting is its own row; reconnects are
  not merged (BR-063).
- **A person's minutes in one meeting** = the sum of the durations of all their
  connections to it, **capped at the meeting's duration** (two devices at once
  or overlapping reconnects never give more than the meeting lasted).
- **How a person is shown** in reports 2, 3 and 4 — the name from their Google
  profile with the email beside it; email only when the installation knows no
  name for the account.
- **Data delay notice** on the page: Meet data arrive with a delay of up to
  24 hours (v47), with the time up to which Meet meetings are loaded
  (`SyncState`, v87). No time yet (Meet never loaded) is shown as such.
- **Read-only mode** (AD-6, BR-026): the reports keep working; this Story writes
  nothing.
- **Roles**: Dean and Admin both (§2 matrix, "Просмотр и экспорт статистики
  Meet-занятий"). Every endpoint has an allowed-role and a forbidden-role test
  (TC-5).
- **Validation** (§8, SC-10): course id, meeting id and dates are validated; an
  inverted or unparsable period, an unknown course or a meeting not in the course
  gives a validation error or 404, never an exception page.
- **Translations** (NFR-073) for the page, column headers, marks, the delay
  notice and errors; dates and numbers follow the user's language; course names,
  person names, emails and meeting codes appear as Google holds them.

## Out of scope

- **Export of the reports** — a separate Story, US-044 (OD-001).
- Lesson plan, lesson length, school norms, merging reconnects, comparing
  teachers, configuring the reports, views wider than one course — Epic 11.
- Course list filters and search — US-020.
- Any change to linking — US-032.
- An audit row for viewing: viewing is not in SC-11's list of audited actions.

---

# Acceptance Criteria

## AC-001 Choosing a course and a period

**When** a Dean or Admin opens the page for the first time

**Then** the course drop-down lists every synchronized course, archived ones
included, and the period is the current month in the school's time zone; after
a course is chosen, reports 1–3 are shown for it and the period.

## AC-002 Meetings of the course

**Given** a course with two codes, one linked automatically and unconfirmed, one
confirmed, and a third code marked "not a course" whose meetings had the same
organizer

**Then** the reports include the meetings of both linked codes, with no mark
about how they were linked, and none of the marked code.

## AC-003 Meetings of the period

**Given** a meeting that starts at 23:30 on the last day of the period in the
school's time zone and ends after midnight, and one that starts at 00:10 on the
day after the period

**Then** the first is in the period, wholly, and the second is not.

## AC-004 Summary

**Then** report 1 shows the number of meetings, total and average duration and
the average number of course students per meeting, computed from the same
meetings as report 2.

## AC-005 Meeting list and counting rules

**Given** a meeting with its organizer, another teacher of the course, two
course students, a personal domain account (on another course's roster) not
on this course's roster on that date, a class account on no roster, and an
external guest

**Then** its row shows the organizer by name and email, 1 course teacher,
2 course students, 1 "not on the course list" and 2 other participants; the
organizer is counted nowhere.

## AC-006 Roster on the meeting's date

**Given** a student who joined the course after meeting X and was on the roster
at meeting Y

**Then** at X they count as "not on the course list", at Y as a course student.

## AC-007 Organizer not a teacher of the course

**Given** a meeting organized by an account that was not a teacher of the
course on its date

**Then** the organizer is marked "not a teacher of this course".

## AC-008 Student participation

**Given** a period in which one student attended two meetings, one attended
none, and one left the course mid-period

**Then** report 3 lists all three — the second with zeros, the third with the
meetings they attended while on the roster — each with meetings and total
minutes.

## AC-009 Minutes are capped at the meeting's duration

**Given** a 45-minute meeting where a student was connected from two devices for
40 minutes each

**Then** their minutes for that meeting are 45, shown as "45 of 45 min", and
report 3 adds 45, not 80.

## AC-010 One meeting in detail

**When** a row of report 2 is opened

**Then** report 4 lists everyone who connected with their mark and "N of M min";
other participants appear as one line with their count and no address or name.

## AC-011 Data delay notice

**Then** the page states that Meet data arrive with a delay of up to 24 hours
and shows the time up to which Meet meetings are loaded, or that they have not
been loaded yet.

## AC-012 No meetings

**Given** a course with no meetings in the period

**Then** report 1 shows zeros, report 2 is empty with an explanatory line, and
report 3 lists the students with zeros.

## AC-013 Invalid input

**Given** an inverted or unparsable period, an unknown course, or a meeting that
does not belong to the chosen course

**Then** the request is refused with a validation error or 404, and the rejected
values are not logged.

## AC-014 Roles

**Then** a Dean and an Admin can open both pages; an unauthenticated request is
refused. Both v1 roles are allowed here, so the forbidden case of TC-5 is the
unauthenticated request.

## AC-015 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** both pages work as usual.

## AC-016 Bilingual

**Then** the pages, headers, marks, the delay notice and errors are in the UI
language, dates and numbers follow it; course names, person names, emails and
meeting codes appear as Google holds them.

## AC-017 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner on 2026-10-10, before activation:

- **OD-001** Scope — (a) on-screen reports only; export moves to a new Story,
  US-044.
- **OD-002** Export format — (a) Excel only; Word later with US-029. Carried to
  US-044.
- **OD-003** Export file — (a) one Excel workbook per course and period with
  sheets "Summary", "Meetings", "Student participation" («Участь студентів»);
  report 4 exported separately from the meeting's page; a built-in fixed
  layout, not a school template (configuration is Epic 11). Carried to US-044.
- **OD-004** Which period a meeting belongs to — (a) the day of its start in the
  school's time zone, wholly.
- **OD-005** A person's minutes in one meeting — (a) the sum of their
  connections, capped at the meeting's duration; shown as "N of M min", no
  percentage.
- **OD-006** How a person is shown — (a) the Google profile name with the email
  beside it.
- **OD-007** Which courses can be chosen — (a) every synchronized course,
  archived ones included, through the journal's drop-down; search comes with
  US-020.
- **OD-008** Data delay notice — (a) the notice plus the time up to which Meet
  meetings are loaded, shown to Dean and Admin. The §2 matrix keeps sync
  *diagnostics* Admin-only; a data-freshness time is part of viewing Meet
  statistics, as the last synchronization time already is in Epic 5 for both
  roles, so no change to `trebovaniya.md` is needed.
- **OD-009** Unconfirmed automatic links — (a) not marked in the reports.
- **OD-010** Navigation and default period — (a) a "Meet reports" menu item,
  reports 1–3 on one page, report 4 from a row of report 2; the current month on
  first opening.

---

# Notes

- Known limitation (v58): a shared account enrolled in a course counts as a
  person; staff without Classroom courses fall into "other participants".
- Known limitation (v41): meetings of a non-Classroom link reused in several
  courses all count in the one course the code is linked to.
- The field names of the `call_ended` event are still to be checked on a live
  domain at first deployment (§7 item 28); the reports read only what US-031
  stored.
- "Student" is the English term of the glossary; the Owner asked for
  «студенти» rather than «учні» in the Ukrainian label of report 3. Whether the
  other Ukrainian labels follow is a translation choice for the Specification.
