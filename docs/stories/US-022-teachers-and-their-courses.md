---
id: US-022
epic: EPIC-2
title: Teachers and their courses
slug: teachers-and-their-courses
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v90.
# Scope decided with the Owner: OD-001 … OD-009 in US-022 discovery (2026-10-12).
---

# User Story

As a **Dean** or an **Admin**

I want to see the school's teachers and how many courses each of them teaches,
and go from a teacher to their courses

So that I can find a teacher's courses without knowing the course names.

---

# Business Value

`trebovaniya.md` §4, Epic 2: "Декан видит преподавателей и привязанные к ним
курсы". §2 matrix, "Просмотр курсов и участников": both roles. US-020 already
filters the course list by teacher (US-020 OD-003, OD-009), but only for a teacher
the Dean picks from a drop-down; this Story adds the teachers as a list of their
own, each linked to the course list filtered by that teacher. It is the last
Story of Epic 2.

---

# Scope

## In scope

- **Teacher list page** (OD-001) — one new page listing teachers; each row links to
  the US-020 course list filtered by that teacher. No teacher card page.
- **Who is a teacher** (OD-002) — anyone currently in the roster of at least one
  course with the teacher role (§3 `CourseMembership`, "сейчас в ростере" set),
  owner and co-teachers alike, as in US-020 OD-003. A person who has left the
  roster of every course is not listed.
- **Status filter** (OD-003) — the course status, ACTIVE by default; any of the
  five statuses in §3 or "all". A teacher is listed when they currently teach at
  least one course of the chosen status; the course count is of that status; the
  link to the course list carries the same status.
- **Row** (OD-004) — the name by the US-020 OD-004 rule ("Прізвище Ім'я" from the
  Google profile, as Google holds it; the email when Google gives no name), the
  email, and the number of courses. Never corrected or translated.
- **No name search** (OD-005) — a name fragment in the address would put personal
  data into the URL, the logs and the language switcher's return path.
- **Order and paging** (OD-006) — sorted by the shown name, 50 per page, as in
  US-020 OD-006; page number handled as the course list handles it.
- **Address** — only the status and the page number in the query; no name, email
  or Google id. The link to the course list passes the teacher as the internal id
  (US-020 OD-009). The language switcher keeps the user on the page with its
  filter.
- **Menu** (OD-007) — "Teachers" is the second menu entry, after "Courses", and
  the second link on the home page.
- **Empty list** — when no teacher matches the chosen status, a short message
  instead of an empty table.
- **Read-only mode** — the page works (viewing, §2). It writes nothing.
- **Roles** — Dean and Admin (§2 matrix), the existing `ViewCourses` policy
  (US-020). Allowed-role and forbidden-role tests (TC-5); both v1 roles are
  allowed, so the forbidden case is the unauthenticated request.
- **Indexes** — chosen in DB design against this Story's queries, with their
  migration if any is needed (PC-2).
- **Translations** (NFR-073) for the page, column headers, the status filter, the
  empty-list message, the menu entry, the home-page link and the course link.
  People's names appear as Google holds them.

## Out of scope

- A teacher card page (OD-001).
- Former teachers who teach no current course (OD-002).
- Any other column, such as the number of courses owned (OD-004).
- Search by name or email (OD-005), and any other sort order.
- Turning teacher names in the course list or the course card into links
  (OD-008); any change to those pages beyond the menu.
- Export of the teacher list (OD-009) — no requirement asks for it.
- Any change to synchronization or to what is stored about a person.

---

# Acceptance Criteria

## AC-001 Who is listed

**Given** a person teaching an ACTIVE course, a person teaching only an ARCHIVED
course, a person who has left the roster of every course, and a person who is only
a student

**When** a Dean or Admin opens the teacher list with its default filter

**Then** only the first person is listed.

## AC-002 Status filter

**Given** the same data

**When** the status filter is ARCHIVED, or "all"

**Then** ARCHIVED lists only the second person, and "all" lists the first two;
each teacher's course count is of the chosen status.

## AC-003 Row

**Then** each teacher appears as "Прізвище Ім'я" from the Google profile, or as the
email when Google gives no name, together with the email, unchanged, and the
number of courses.

## AC-004 Link to the courses

**Then** each row links to the course list filtered by that teacher and by the
chosen status, and that list shows exactly the counted courses.

## AC-005 Order and paging

**Given** more than 50 teachers

**Then** the list is sorted by the shown name, 50 per page.

## AC-006 Address

**Then** the address holds only the status and the page number; no name, email or
Google id appears in it or in the link to the course list.

## AC-007 Menu

**Then** "Teachers" is the second menu entry and the second link on the home page.

## AC-008 Empty list

**Given** no teacher of the chosen status

**Then** the page shows its own message.

## AC-009 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the page works and nothing is written.

## AC-010 Roles

**Then** a Dean and an Admin can open the page, and an unauthenticated request is
refused.

## AC-011 Bilingual

**Then** the page, headers, statuses, messages and links are in the user's UI
language, while names appear unchanged.

## AC-012 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner before activation (US-022 discovery, 2026-10-12),
all as recommended:

- **OD-001** One teacher list page; each row links to the US-020 course list
  filtered by the teacher. No teacher card.
- **OD-002** A teacher is anyone currently in a course roster with the teacher
  role; people who left every course are not listed.
- **OD-003** Course status filter, ACTIVE by default; listing, count and link
  follow the chosen status.
- **OD-004** Row — name, email, number of courses.
- **OD-005** No name search: it would put personal data in the address.
- **OD-006** Sorted by name, 50 per page.
- **OD-007** "Teachers" is the second menu entry and the second home-page link.
- **OD-008** No links from teacher names on the course list or the course card.
- **OD-009** No export.

---

# Notes

- A person teaching several courses is one row: rows are participants
  (`ClassroomParticipant`), not memberships.
- The count and the course list's teacher filter must agree (AC-004): both use
  the US-020 OD-003 rule, current teacher memberships.
- Ordering by the shown name means a person with no Google name sorts by their
  email, as in US-020 and US-021.
