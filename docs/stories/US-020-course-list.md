---
id: US-020
epic: EPIC-2
title: Course list with filters and search
slug: course-list
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v88.
# Scope decided with the Owner: OD-001 … OD-011 in US-020 discovery (2026-10-11).
---

# User Story

As a **Dean** or an **Admin**

I want to see all of the school's Classroom courses in one list, filter it by
status and teacher, and search it by name

So that I can find a course quickly and open its journal or its Meet reports.

---

# Business Value

`trebovaniya.md` §4, Epic 2: "Декан видит список всех курсов с фильтрами (статус,
преподаватель, поиск по названию)". §2 matrix, "Просмотр курсов и участников":
both roles. Today a course is chosen only from a plain drop-down in the journal
and the Meet reports (US-025 OD-001); this Story adds the real list and links from
it into both.

---

# Scope

## In scope

- **Course list page** — every synchronized course of the installation (no
  "own courses" in v1, §3 `AppUser`). One row per course (OD-001):
  - name and section;
  - status (Google's state, one of the five in §3);
  - teachers — everyone with the teacher role currently in the course's roster
    (OD-003), shown by the name rule of OD-004;
  - number of students currently in the roster;
  - last update time in Google (`updateTime`), in the school's time zone.
- **Status filter** (OD-002) — one of the five states or "all". With no choice
  the list shows ACTIVE courses only.
- **Teacher filter** (OD-003) — a choice of the people who are currently a
  teacher in the roster of at least one course: the owner and co-teachers alike.
  The filter keeps the courses where the chosen person is such a teacher.
- **Teacher name** (OD-004) — "Прізвище Ім'я" from the Google profile, as Google
  holds it (§3, v86); when Google gives no name, the email. Never corrected or
  translated.
- **Search** (OD-005) — part of the text, case-insensitive, in the name or the
  section.
- **Filters combine** — status, teacher and search apply together.
- **Paging and order** (OD-006) — sorted by name; 50 courses per page.
- **Links from a course** (OD-007) — "Journal" and "Meet reports" open that page
  with this course already chosen. The drop-downs on those pages stay as they
  are. The course card with its roster is US-021.
- **No link to Classroom** (OD-008) — the course's Google link is not shown.
- **Filters in the address** (OD-009) — the list is a GET page; status, teacher,
  search and page are query parameters, so it can be bookmarked and reloaded. The
  teacher is the participant's internal id, never an email or name. The page
  sets the language switcher's return-path override as the journal does, so the
  switcher does not reflect the raw query (US-025 security review I-2, US-039
  INFO-1).
- **Navigation** (OD-010) — a new first menu entry "Courses", and the first link
  in the home page's Google Workspace section.
- **Empty states** — no synchronized courses yet, and no course matching the
  filters, each with its own message.
- **Read-only mode** — the list works (viewing, §2). It writes nothing.
- **Roles** — Dean and Admin (§2 matrix). Allowed-role and forbidden-role tests
  (TC-5); both v1 roles are allowed, so the forbidden case is the unauthenticated
  request.
- **Validation** (§8, SC-10) — status, teacher id, search text and page number are
  validated. An unknown status or a malformed id or page is refused with the
  page's validation message, and the rejected value is never logged or echoed.
- **Indexes** — chosen in DB design against this Story's queries (US-014 DB design
  §3.4 left them to US-020), with their migration (PC-2).
- **Translations** (NFR-073) for the page, column headers, the five statuses,
  filter labels, empty states and the menu entry. Course names, sections and
  teacher names appear as Google holds them.

## Out of scope

- Export of the course list (OD-011) — no requirement asks for it.
- The course card with its roster — US-021.
- Teachers and their courses as a separate view — US-022.
- Removing the course drop-downs from the journal and the Meet reports (OD-007).
- Any sort order other than by name, and a choice of page size.
- Any change to synchronization or to what is stored about a course.

---

# Acceptance Criteria

## AC-001 The list

**Given** synchronized courses

**When** a Dean or Admin opens "Courses"

**Then** each row shows the name and section, the status, the teachers currently
in the roster, the number of students currently in the roster, and the last
update time in Google in the school's time zone.

## AC-002 Default status

**Given** no status chosen

**Then** only ACTIVE courses are listed.

## AC-003 Status filter

**When** a status or "all" is chosen

**Then** only courses in that status, or all courses, are listed.

## AC-004 Teacher filter

**Given** a course whose owner and co-teacher are both in the roster, and a
person who has left a course's roster as a teacher

**Then** the teacher choice lists the owner and the co-teacher, choosing either
lists that course, and a course is not listed for a teacher no longer in its
roster.

## AC-005 Teacher name

**Then** a teacher appears as "Прізвище Ім'я" from the Google profile, or as the
email when Google gives no name, unchanged.

## AC-006 Search

**Given** a search text

**Then** the courses whose name or section contains it, in any letter case, are
listed.

## AC-007 Filters combine

**Then** status, teacher and search together list only the courses that satisfy
all three.

## AC-008 Order and paging

**Given** more than 50 matching courses

**Then** the list is sorted by name, shows 50 per page, and lets the user move
between pages keeping the filters.

## AC-009 Links

**Then** each course links to the journal and to the Meet reports, which open
with that course chosen.

## AC-010 Address

**Then** the filters and page are in the address, the teacher is an internal id,
not an email or name, and the language switcher does not repeat the query.

## AC-011 Empty states

**Then** an installation with no synchronized courses and a filter that matches
nothing each show their own message.

## AC-012 Navigation

**Then** "Courses" is the first menu entry and the first link in the home page's
Google Workspace section.

## AC-013 Invalid input

**Given** an unknown status, or a malformed teacher id or page number

**Then** the request is refused with a validation message, and the rejected value
is neither logged nor echoed.

## AC-014 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the list works and nothing is written.

## AC-015 Roles

**Then** a Dean and an Admin can open the list, and an unauthenticated request is
refused.

## AC-016 Bilingual

**Then** the page, headers, statuses and messages are in the user's UI language,
while course names, sections and teacher names appear unchanged.

## AC-017 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner before activation (US-020 discovery, 2026-10-11),
all (a):

- **OD-001** Row contents — name and section, status, teachers, number of
  students in the roster, last update time in Google.
- **OD-002** Status filter — ACTIVE by default; any of the five states or "all".
- **OD-003** Teacher — anyone with the teacher role currently in the course's
  roster, owner and co-teachers alike.
- **OD-004** Teacher name — "Прізвище Ім'я" from the Google profile, email when
  there is no name.
- **OD-005** Search — substring, case-insensitive, in the name and the section.
- **OD-006** Paging — 50 per page, sorted by name.
- **OD-007** Links — to the journal and the Meet reports with the course chosen;
  their drop-downs stay.
- **OD-008** No "Open in Classroom" link.
- **OD-009** Filters in the address; the teacher as an internal id; the language
  switcher's return-path override set (US-025 I-2).
- **OD-010** "Courses" is the first menu entry and the first link on the home
  page.
- **OD-011** No export of the list in this Story.

---

# Notes

- The journal and the Meet reports already accept `courseId` in their query, so
  the links need no change to those pages; what they show for a course with no
  period chosen is their existing behaviour.
- A teacher counted in OD-003 is a `CourseMembership` with role teacher and "now
  in roster" set; the course owner who is not in the roster is not listed (US-014
  OD-004 keeps the owner as a Google id without a foreign key).
- The prototype's `idx_courses_state` / `idx_courses_owner` / `idx_courses_update`
  are a hint only (US-014 DB design §3.4).
