---
id: US-021
epic: EPIC-2
title: Course detail with roster
slug: course-detail
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v88.
# Scope decided with the Owner: OD-001 … OD-012 in US-021 discovery (2026-10-11).
---

# User Story

As a **Dean** or an **Admin**

I want to open a course and see what it is and who is in it — its teachers, its
students and those who have left

So that I know whom the course's journal and Meet reports are about.

---

# Business Value

`trebovaniya.md` §4, Epic 2: "Просмотр студентов курса". §2 matrix, "Просмотр
курсов и участников": both roles. US-020 lists the courses but shows only the
number of students; this Story adds the course card with its roster. The leavers
matter because the journal for a past period still counts them (§4 Epic 3, "Кто в
журнале за период"), and without the card a Dean cannot see why they are there.

---

# Scope

## In scope

- **Course card page** — one synchronized course of the installation, addressed by
  its internal id (any course: no "own courses" in v1, §3 `AppUser`).
- **Course details** (OD-001) — name, section, status (one of the five in §3),
  room, description heading and description, creation time and last update time
  in Google in the school's time zone. A value Google did not give is simply not
  shown. The teacher folder and calendar id are not shown.
- **No link to Classroom** (OD-002) — the course's Google link is not shown, as in
  US-020 OD-008.
- **Owner mark** (OD-003) — when the course owner (§3 `Course.owner`) is among the
  current teachers, that teacher is marked "owner". An owner who is not in the
  roster is not shown and not looked up.
- **Roster in sections** (OD-004) — "Teachers" and "Students": the people currently
  in the roster with that role, each section with its count.
- **Leavers** (OD-005) — a third section "Left the course": participations no
  longer in the roster (§3 `CourseMembership`, "сейчас в ростере" not set), each
  with its role and the date synchronization last saw the person in the roster, in
  the school's time zone. This includes a student created from submissions who was
  never seen in the roster (§3, v56) — the date is then that synchronization's day.
  A leaver removed by the retention purge (§5) is no longer shown.
- **Person** (OD-006) — the name by the US-020 OD-004 rule ("Прізвище Ім'я" from
  the Google profile, as Google holds it; the email when Google gives no name) and
  the email. Never corrected or translated.
- **Order and paging** (OD-007) — each section sorted by the shown name; the whole
  roster on one page, no paging.
- **No roster search** (OD-008).
- **Links** (OD-009) — in the US-020 course list the course name becomes a link to
  the card. The card links to the journal and to the Meet reports with this course
  already chosen (as the list row does, US-020 OD-007), and to the course list
  with its default filters; the browser's Back keeps the list's filters.
- **Unknown course** (OD-010) — an id that is malformed or names no course gets
  the ordinary "not found" page (404); the requested value is neither logged nor
  echoed.
- **Address** — the course is the internal id in the path; no email, name or
  Google id in the address. The language switcher keeps the user on the card.
- **Empty sections** — a course with no teachers, no students or no leavers shows
  a short message in that section instead of an empty table.
- **Read-only mode** — the card works (viewing, §2). It writes nothing.
- **Roles** — Dean and Admin (§2 matrix). Allowed-role and forbidden-role tests
  (TC-5); both v1 roles are allowed, so the forbidden case is the unauthenticated
  request.
- **Indexes** — chosen in DB design against this Story's queries, with their
  migration if any is needed (PC-2).
- **Translations** (NFR-073) for the page, field labels, the five statuses,
  section titles, the owner mark, the roles, empty-section messages and links.
  Course data and people's names appear as Google holds them.

## Out of scope

- Export of the roster (OD-011) — no requirement asks for it.
- The time of the last synchronization on the card (OD-012).
- Search or filters inside the roster, and any other sort order.
- Returning from the card to the list with its previous filters (OD-009).
- Teachers and their courses as a separate view — US-022.
- Any change to synchronization or to what is stored about a course or a person.

---

# Acceptance Criteria

## AC-001 Course details

**Given** a synchronized course

**When** a Dean or Admin opens its card

**Then** it shows the name, section, status, room, description heading and
description, and the creation and last update times in Google in the school's time
zone; values Google did not give are omitted, and neither the Classroom link, the
teacher folder nor the calendar id is shown.

## AC-002 Roster sections

**Then** the current teachers and the current students are shown in two sections,
each with its count.

## AC-003 Owner mark

**Given** a course whose owner is a current teacher, and a course whose owner is
not in the roster

**Then** the first course marks that teacher "owner", and the second marks no one.

## AC-004 Leavers

**Given** a teacher and a student who have left the roster, and a student created
from submissions who was never in the roster

**Then** all three appear under "Left the course" with their role and the date they
were last seen in the roster, and none of them appears among the current teachers
or students.

## AC-005 Person

**Then** each person appears as "Прізвище Ім'я" from the Google profile, or as the
email when Google gives no name, together with the email, unchanged.

## AC-006 Order

**Then** each section is sorted by the shown name and the whole roster is on one
page.

## AC-007 Links

**Then** the course name in the course list links to the card, and the card links
to the journal and the Meet reports with that course chosen and to the course
list.

## AC-008 Unknown course

**Given** a malformed id or the id of no course

**Then** the "not found" page is shown, and the value is neither logged nor
echoed.

## AC-009 Empty sections

**Given** a course with no teachers, no students or no leavers

**Then** that section shows its own message.

## AC-010 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the card works and nothing is written.

## AC-011 Roles

**Then** a Dean and an Admin can open the card, and an unauthenticated request is
refused.

## AC-012 Bilingual

**Then** the page, labels, statuses, section titles, the owner mark, roles and
messages are in the user's UI language, while course data and names appear
unchanged.

## AC-013 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner before activation (US-021 discovery, 2026-10-11),
all (a):

- **OD-001** Course details — name, section, status, room, description heading and
  description, creation and update times; no teacher folder or calendar id.
- **OD-002** No "Open in Classroom" link, as in US-020 OD-008.
- **OD-003** Owner — marked "owner" when a current teacher; otherwise not shown.
- **OD-004** Two sections, "Teachers" and "Students", each with a count.
- **OD-005** Leavers in a separate section with the date last seen in the roster.
- **OD-006** Person — name by the US-020 OD-004 rule, and the email.
- **OD-007** Sorted by name; no paging.
- **OD-008** No roster search.
- **OD-009** The course name in the list links to the card; the card links to the
  journal, the Meet reports and the list (default filters; Back keeps them).
- **OD-010** Unknown or malformed course id — 404.
- **OD-011** No roster export.
- **OD-012** No last-synchronization time on the card.

---

# Notes

- The leavers section shows former teachers as well as former students, with the
  role on each row: OD-005 is about everyone no longer in the roster. Discovery
  question 4 split only the current roster by role.
- The owner is matched by Google id: `Course.OwnerGoogleId` against the
  participant's Google user id (US-014 OD-004 keeps the owner as a value without
  a foreign key).
- The description is Google's plain text: shown with its line breaks, encoded,
  never rendered as HTML.
- The journal and the Meet reports already accept `courseId` in their query
  (US-020 Notes), so their links need no change to those pages.
