---
artifact_type: specification
story: US-022
version: 1
status: APPROVED
created_at: 2026-10-12T08:58:14Z
updated_at: 2026-10-12T09:00:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-022-teachers-and-their-courses.md
    version: null
  - path: trebovaniya.md
    version: 90
  - path: docs/decisions/US-022-open-decisions.md
    version: 1
supersedes: null
---

# US-022 Specification — Teachers and their courses

## 1. Overview

This Story gives Admin and Dean a list of the school's teachers, each with the
number of courses they currently teach and a link to the course list filtered
by that teacher (`trebovaniya.md` §4 Epic 2 "Декан видит преподавателей и
привязанные к ним курсы"; §2 "Просмотр курсов и участников" ✔ Admin ✔ Dean). It
is the last Story of Epic 2 and builds on the course list (US-020).

Four properties shape everything below.

- **A pure read.** The list writes nothing, calls no Google API and reads no
  secret. It works unchanged in read-only mode (§2, BR-026) and needs no
  read-only guard.
- **Only what US-014 stored.** Participants, courses and memberships are read as
  Google returned them (§3, v83 "a mirror nobody edits"); nothing is corrected,
  translated or added to what is stored.
- **One teacher rule, shared with the course list.** A teacher of a course is a
  participant with a `CourseMembership` of that course where `Role = Teacher`
  and `OnRoster = true` (US-020 OD-003, OD-002). The teacher list, its course
  count and the course list's teacher filter use exactly this rule, so the count
  and the linked list always agree (AC-004).
- **No personal data in the address.** The page's address carries only the
  status and the page number; the link to the course list carries the teacher as
  the internal participant id (OD-005, US-020 OD-009).

Delegated lookups: none were delegated; the facts below were read directly
(read only) — `trebovaniya.md` v90 §4 Epic 2 and §7; the US-020 Specification,
DB design §3.4 (label key, collation `course_name_uk`) and Open Decisions; the
existing code of the course list (`CoursesController`, `GetCourseListQuery`,
`CourseListUrl`, `CoursePersonNameRule`, `ICourseListSource`,
`SignInRoutes.Courses`), the home page (`Views/Home/Index.cshtml`) and the
layout (`Views/Shared/_Layout.cshtml`).

## 2. Business Goal

Today a Dean finds a teacher's courses only by picking the teacher from the
course list's drop-down (US-020 FR-005). They cannot see who teaches in the
school at a glance, nor how many courses each person carries. The teacher list
answers both, and one click opens the teacher's courses — the Dean no longer
needs to know a course name to reach it.

## 3. Business Flow

### 3.1 Opening the list

From the home page the Dean (or Admin) follows "Teachers", the second link of
the Google Workspace section, right after "Courses" (OD-007, I-1). The page
shows the status filter (pre-set to ACTIVE, OD-003) and below it the first page
of teachers of ACTIVE courses sorted by name.

### 3.2 Changing the status

The user picks a status or "all" and submits. The form is a `GET`: the status
travels in the address and the list restarts at page 1.

### 3.3 Paging

When more than 50 teachers match, page links move between pages keeping the
status (OD-006).

### 3.4 Opening a teacher's courses

Each row links to the course list filtered by that teacher and by the chosen
status (OD-001). The course list shows exactly the courses counted in the row.

### 3.5 Invalid input

An unknown status or a malformed page number: the page shows the status filter
with the default value in place of the rejected one and a message naming the
problem. No list is shown.

### 3.6 Read-only mode

The installation is suspended, past its grace period or never legitimated. The
list opens and behaves exactly as above (AC-009).

## 4. Functional Requirements

### FR-001 The teacher list page and its query

One server-rendered page (`GET`), reachable by Admin and Dean, under the
existing `/workspace/` area beside the course list (route indicative,
`/workspace/teachers`; API_DESIGN fixes it), with these query parameters (names
indicative; API_DESIGN fixes them, reusing the course list's where the meaning is
the same):

| Parameter | Meaning | Absent or empty |
|---|---|---|
| `status` | one of the five course states, or `all` | ACTIVE (OD-003) |
| `page` | page number, starting at 1 | 1 |

There is no teacher, name or search parameter (OD-005). The page always shows the
status filter with the effective value. The query is answered by a use case
(query) in `Application/UseCases` — `GetTeacherListQuery`, name indicative —
returning a DTO (AD-8); the host only binds, hands over and renders. No JSON
endpoint is added (US-020 I-6). The page is a `GET` and changes nothing (API-4,
§8).

### FR-002 Who is listed

- A participant (`ClassroomParticipant`) is listed when they have at least one
  `CourseMembership` with `Role = Teacher` and `OnRoster = true` in a course
  whose `State` matches the status filter — any course when the filter is "all"
  (OD-002, OD-003).
- Owner and co-teachers alike: the course owner (`OwnerGoogleId`) is listed only
  through a teacher membership on the roster, exactly as in the course list
  (US-020 FR-003; US-014 OD-004).
- A participant who has left the roster of every course as a teacher
  (`OnRoster = false` everywhere), or who is only a student, is not listed.
  A teacher membership in a course of another status does not list the
  participant under the chosen status.
- Each participant appears at most once: rows are participants, not memberships
  (Story Notes).
- Every participant of the installation is a candidate — no per-Dean
  restriction, no roster scoping (Hard Stop, §3 `AppUser`).

### FR-003 The row

| Column | Content |
|---|---|
| Name | the FR-004 name rule |
| Email | `ClassroomParticipant.Email` as stored; an empty cell when there is none |
| Courses | the number of distinct courses of the chosen status (any status for "all") in which the participant has a teacher membership on the roster (FR-002) — always at least 1 |
| Link | "Courses" (translated), the FR-007 link |

No other column (OD-004) — in particular no number of courses owned. Names and
emails are never translated, corrected or re-cased (NFR-073; §3 v83).

### FR-004 The teacher's name

The course pages' name rule (US-020 FR-007, OD-004; `CoursePersonNameRule`),
unchanged:

1. **Profile name** — "Прізвище Ім'я": `Surname` and `GivenName` joined by one
   space, as Google holds them; when only one is present, that one alone.
2. **Email** — when neither part is present, the full `Email` as stored.
3. **No name** — when there is neither, the translated label "teacher without a
   name".

An empty string counts as absent. `FullName` is not used (US-020 I-2). In case 2
the email appears in both the Name and the Email columns (I-3).

### FR-005 Status filter

- The choices are the five states of §3 and "all", in the order Active,
  Archived, Provisioned, Declined, Suspended, All, each with the translated label
  the course list already uses.
- Absent or empty — ACTIVE (OD-003). A chosen state restricts both who is listed
  (FR-002) and what is counted (FR-003); "all" restricts neither.

### FR-006 Order and paging

- Teachers are sorted by the shown name in the course pages' alphabetical order
  (US-020 FR-008, I-7: Ukrainian letters in their Ukrainian positions, Latin in
  Latin order, the same for every UI language) — that is, by the US-020 DB design
  §3.4 **label key** (profile name, else email) under the collation
  `course_name_uk`, participants with neither name nor email last, ties by
  internal participant id. The order is total and stable between pages (OD-006).
- A participant without a Google name therefore sorts by their email (Story
  Notes).
- 50 teachers per page (OD-006). No choice of page size, no other sort order.
- The page shows the total number of listed teachers and, when there is more
  than one page, links to the previous and next page and to each page number (or
  a compact range — UI detail for implementation, as on the course list), each
  keeping the status (I-4).
- A page number past the last page shows the last page; when nobody is listed,
  page 1 with the FR-008 message (US-020 I-5).

### FR-007 The link to the course list

- Each row links to the course list with the teacher filter set to the
  participant's internal id (`ClassroomParticipant.Id`) and the status filter set
  to the effective status of the teacher list, page 1, no search text.
- The link is built by the course list's own URL rule (`CourseListUrl.Build`
  with today's code), so its parameter names, tokens and defaults are the course
  list's; no URL string is assembled by hand.
- No name, email or Google id appears in it (OD-005, US-020 OD-009).
- The course list then shows exactly the courses counted in the row: it filters
  by the same teacher rule (US-020 FR-005) and the same status (US-020 FR-004),
  and every participant the teacher list shows is in the course list's teacher
  choice (a current teacher of some course), so the link is never refused as an
  unknown teacher (AC-004, AC-016). The course list itself is not changed
  (OD-008).

### FR-008 Empty list

When nobody satisfies FR-002 under the chosen status — including an
installation that has synchronized nothing yet — the status filter is still
shown and a translated message says no teacher teaches a course of the chosen
status; no table and no paging are drawn (I-2). Not an error.

### FR-009 The address and the language switcher

- The form submits with `GET`; the page and every paging link carry only the
  effective, validated `status` and `page`. Defaults may be omitted from links.
  A submitted status change restarts at page 1.
- The page sets the language switcher's return-path override
  (`UiLanguageController.ReturnPathViewDataKey`, as the course list does) to the
  teacher-list URL rebuilt from the **effective, validated** values — never from
  the raw query (US-025 security review I-2, US-039 INFO-1). After a validation
  failure the rejected parameter is not in the return path.
- One rule in `Application` builds every teacher-list URL (paging links and the
  return path), as `CourseListUrl` does for the course list.
- Any other query parameter — including a `teacherId` or a `q` typed by hand — is
  ignored and never echoed (VR-003).

### FR-010 Validation and its answer

The query is validated before anything is read for the list (§8, SC-10); the
rules are §6. On a failure:

- the page is re-rendered — **not** the host's error page — with the status
  filter and a translated message naming the problem;
- the rejected field shows its default (status ACTIVE, page 1); the rejected
  value is not put back into the form, the message, the links or the switcher
  return path;
- no list is computed;
- the status code is chosen by API_DESIGN within API-5 (`400` expected, as the
  course list answers a malformed parameter);
- the rejected value is never written to a log (SC-10): the log line names the
  parameter and the rule, never the value.

When both parameters are invalid, both messages are shown. The status and page
messages of the course list may be reused.

### FR-011 Navigation

- The home page's Google Workspace section gains "Teachers" as its **second**
  link, right after "Courses" and before "Journal" (OD-007), for Admin and Dean,
  visible in read-only mode too (viewing is allowed, §2). Hiding it is never the
  enforcement — FR-012 is.
- That section is the program's menu (US-020 I-1); no layout-wide menu is added
  (I-1).

### FR-012 Authorization

The page declares the existing `ViewCourses` policy (US-020 FR-014) — the §2
matrix row "Просмотр курсов и участников", ✔ Admin, ✔ Dean. No new policy.

- An anonymous request is sent to sign in (SC-4); the SC-4 anonymous closed list
  gains nothing; the endpoint enumeration test classifies the page as protected.
- A Dean held on the forced password change (US-012) is refused as on every
  other page.
- No per-teacher or per-course restriction for a Dean in v1 (§2, §3 `AppUser`).
  Teacher and Student have no account (Hard Stop); the list is never scoped by
  roster.

### FR-013 Read-only mode

Viewing is permitted in read-only mode (§2, BR-026). The query does not call
`IReadOnlyModeGuard`, writes nothing — not even a permitted service write — and
opens no Google port. Proven in `Application` for all three BR-025 causes: the
list is returned, no entity is added or modified, and no Google port is invoked
(TC-5, AC-009).

### FR-014 Persistence and performance

- **No new table, no new column, no new audit action.** Viewing is not audited
  (SC-11 lists no viewing action).
- The data is read through a read port in `Application/Ports` implemented in
  `Infrastructure` with EF Core, without change tracking — a new port
  (`ITeacherListSource`, name indicative) or new methods on `ICourseListSource`;
  DB_DESIGN chooses. `DbContext` appears in neither `Application` nor `Web`
  (AD-3). No entity crosses into a view model (AD-8). The existing course-list
  queries are not changed in behaviour.
- **Filtering, grouping, counting, ordering and paging happen in the database** —
  at most one page of teachers is read, not every participant — and the number
  of database round trips per request is fixed, independent of the number of
  teachers, courses and memberships (for example: the total number of listed
  teachers, and the page of teachers with their course counts). No per-row query.
- **Indexes** (Story; PC-7, NFR-003): DB_DESIGN checks these queries against the
  existing indexes (among them `ix_course_membership_participant_id`,
  `ix_course_membership_course_seen`, `uq_course_membership_course_participant`
  and the course-list indexes of US-020) and adds any that is missing by an EF
  Core migration in this Story (PC-2). The collation `course_name_uk` exists
  already (US-020); no database extension and no NuGet package is added.

### FR-015 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): page title, the navigation entry, the column
headers, the status filter label and submit button (the five status names and
"all" are reused from the course list), the paging labels and the total, the
course-list link text, "teacher without a name" (may be reused), the FR-008
message, and every validation message of §6 (may be reused). Google data —
teacher names and emails — is never translated. The existing test that fails on
a key missing from one file covers the new keys.

### FR-016 Logging

- A shown page logs one line at `Information`: the actor's account id, the
  effective status, the page number, and the number of listed teachers.
- A validation failure logs one line at `Warning` naming the parameter and the
  rule, never the value.
- No log line carries a teacher name or email (SC-10, NFR-023, DC-10).

## 5. Acceptance Criteria

The Story's twelve criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Default filter lists only a current teacher of an ACTIVE course — not a teacher of only an ARCHIVED course, not a former teacher, not a student | FR-002, FR-005 |
| AC-002 | ARCHIVED lists only the archived-course teacher; "all" lists both; counts are of the chosen status | FR-002, FR-003, FR-005 |
| AC-003 | Row: "Прізвище Ім'я", or the email when no name; the email unchanged; the number of courses | FR-003, FR-004 |
| AC-004 | Each row links to the course list filtered by the teacher and the status; that list shows exactly the counted courses | FR-007 |
| AC-005 | More than 50 teachers — sorted by the shown name, 50 per page | FR-006 |
| AC-006 | The address holds only status and page; no name, email or Google id in it or in the course-list link | FR-007, FR-009 |
| AC-007 | "Teachers" second in the menu and second on the home page | FR-011 |
| AC-008 | No teacher of the chosen status — the page's own message | FR-008 |
| AC-009 | Read-only mode — the page works and nothing is written | FR-013 |
| AC-010 | Dean and Admin open the page; an unauthenticated request is refused | FR-012 |
| AC-011 | Bilingual; names unchanged | FR-003, FR-004, FR-015 |
| AC-012 | Tests never reach Google; synthetic data | FR-013, FR-014, TC-2, TC-4 |

Four criteria are derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-013 | Invalid status or page refused with a message; no list; the value neither logged nor echoed in the form, the links or the switcher return path; another parameter (`teacherId`, `q`) is ignored and not echoed | FR-009, FR-010, FR-016, §6 |
| AC-014 | Order: teachers "Іваненко Ірина", "Гнатюк Олег", "Ґудзь Анна", "Андрієнко Петро" and one with no name and email `zoe@school.example` are listed as Андрієнко Петро, Гнатюк Олег, Ґудзь Анна, Іваненко Ірина, zoe@school.example; a participant with neither name nor email is last | FR-004, FR-006 |
| AC-015 | Bounded reading: with 120 listed teachers, page 2 shows teachers 51–100 in order and page 3 shows 101–120; the number of database queries per request is the same for page 1 and page 3 and does not grow with the number of teachers, courses or memberships | FR-006, FR-014 |
| AC-016 | Count agrees with the course list: a participant who teaches two ACTIVE courses, has left the roster of a third ACTIVE course as teacher, and is a student of a fourth ACTIVE course shows 2, and the linked course list shows exactly those two courses and is not refused | FR-002, FR-003, FR-007 |

## 6. Validation Rules

### VR-001 `status`

The course list's rule (US-020 VR-001), unchanged and with the same tokens:

- Absent or empty — ACTIVE.
- Otherwise exactly one value, one of the course list's six status tokens
  (`active`, `archived`, `provisioned`, `declined`, `suspended`, `all`),
  case-insensitive. Anything else — another word, a number, spaces inside, a
  repeated parameter — is **malformed**: message "unknown course status".

### VR-002 `page`

The course list's rule (US-020 VR-004), unchanged:

- Absent or empty — 1.
- Otherwise exactly one value, decimal digits only, parsing to an integer from
  1 to 100 000 inclusive. Anything else — zero, sign, spaces, letters, overflow,
  a repeated parameter — is **malformed**: message "invalid page number".
- A valid number past the last page is not an error (FR-006).

### VR-003 Unknown parameters and data from the database

- Any other query parameter is ignored; it is never echoed into the page.
- Values read from the database were validated on import (US-014) and are
  rendered with HTML encoding; a name or email is never interpreted as markup
  (SC-10).

### VR-004 The actor

The actor of the log line is the signed-in account from the session, never a
request value (US-020 VR-006).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page declares the existing `ViewCourses` policy (Admin, Dean); deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for the endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access, no roster-based scoping, no per-teacher or per-course restriction for a Dean. | Hard Stop, §2, §3 `AppUser` |
| S-04 | The list writes nothing, calls no Google API and reads no secret, in any mode; proven in `Application` for all three BR-025 causes. | SC-5, SC-8, BR-026, AD-6 |
| S-05 | URLs produced by the page — its own, paging links, the switcher return path and the course-list links — carry the status, the page and the internal participant id only; no name, email, Google id or search text. | OD-005, US-020 OD-009, US-039 INFO-1 |
| S-06 | The language switcher's return path is rebuilt from validated values, never from the raw query. | US-025 security review I-2, US-039 INFO-1 |
| S-07 | Invalid input is rejected before anything is read; the rejected value never reaches a log, the form, a link or an error page detail. | §8, SC-10 |
| S-08 | No log line carries a teacher name or email; internal ids and counts only. | SC-10, NFR-023 |
| S-09 | Names and emails are HTML-encoded on output; nothing from the database is rendered as markup. | SC-10, VR-003 |
| S-10 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-11 | No Google id is shown or put in a URL. No new outbound destination. | SC-13 (Hard Stop) |
| S-12 | Viewing writes no audit row. | SC-11 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Signed-in user without the policy (a Dean on the forced password change) | Refused as on every other page (US-012). |
| Malformed status or page | The teacher-list page with the status filter and the VR message; no list; status per API_DESIGN within API-5 (expected `400`). |
| Nobody teaches a course of the chosen status, or nothing synchronized yet | Not an error: FR-008 message. |
| Page past the last | Not an error: the last page (FR-006). |
| Read-only mode | Not an error: the list is shown (FR-013). |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

## 9. Non-Functional Requirements

- **NFR-003 / PC-7** — explicit indexes back the list queries (FR-014).
- **Bounded queries** — the number of database round trips and of participants
  read does not grow with the size of the school (FR-014, AC-015). A school with
  a few hundred teachers and a few thousand courses accumulated over several
  years is the sizing case DB_DESIGN reasons about (as in US-020).
- **NFR-070** — usable on a phone: the table scrolls horizontally inside the page
  rather than widening it; the status filter is usable at phone width.
- **NFR-073** — bilingual; no time is shown, so no time-zone rule applies.
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- A teacher card page (OD-001).
- Former teachers who teach no current course (OD-002).
- Any other column, such as the number of courses owned (OD-004).
- Search by name or email (OD-005); any other sort order; a choice of page size.
- Links from teacher names on the course list or the course card (OD-008); any
  change to those pages.
- Export of the teacher list (OD-009).
- Any change to synchronization or to what is stored about a person.
- A layout-wide menu (I-1).
- A JSON API for the list (US-020 I-6).

## 11. Open Decisions

Full text and resolutions in `docs/decisions/US-022-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 One teacher list, link to the course list | **RESOLVED** 2026-10-12 | none — FR-001, FR-007 |
| OD-002 Teacher = current teacher in a roster | **RESOLVED** 2026-10-12 | none — FR-002 |
| OD-003 Status filter, ACTIVE by default | **RESOLVED** 2026-10-12 | none — FR-005 |
| OD-004 Row: name, email, number of courses | **RESOLVED** 2026-10-12 | none — FR-003 |
| OD-005 No name search | **RESOLVED** 2026-10-12 | none — FR-001, FR-009 |
| OD-006 Sorted by name, 50 per page | **RESOLVED** 2026-10-12 | none — FR-006 |
| OD-007 "Teachers" second | **RESOLVED** 2026-10-12 | none — FR-011 |
| OD-008 No links from names elsewhere | **RESOLVED** 2026-10-12 | none — FR-007, §10 |
| OD-009 No export | **RESOLVED** 2026-10-12 | none — §10 |

No new Open Decision was raised. No item of `trebovaniya.md` §7 blocks the
Story (see the Open Decisions artifact).

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 The home page section is the menu.** OD-007 and AC-007 say "the second
  menu entry" and "the second link on the home page". As in US-020 I-1, the
  program has no menu outside the home page: the layout carries only the
  language switcher. Both phrases are read as one requirement — "Teachers"
  second in the Google Workspace section, after "Courses". (Read literally,
  "the second link on the home page" would not hold even for "Courses" today:
  the Dean's synchronization button comes first.)
- **I-2 One empty message.** The Story names one message, "when no teacher
  matches the chosen status". The course list distinguishes "nothing
  synchronized" from "nothing matches" (US-020 FR-011); the teacher list does
  not — before the first synchronization nobody teaches anything, and the one
  message is true. The course list already tells the Dean whether courses exist.
- **I-3 The email may appear twice.** When Google gives no name, the Name column
  shows the email (OD-004) and the Email column shows it again. Both columns stay
  for every row so the table keeps one shape.
- **I-4 The total is shown.** As on the course list (US-020 FR-008), the page
  shows how many teachers are listed. It is a count, not personal data.
- **I-5 The count is of courses, not memberships.** A participant has at most
  one membership per course (`uq_course_membership_course_participant`), so
  "number of courses" is unambiguous; it counts only courses where the
  membership is a current teacher membership (FR-003).
- **I-6 The link always opens page 1 with no search.** The course list's
  search and page are not carried from the teacher list — the teacher list has
  neither.
- **I-7 Under "all" a teacher may be listed for courses of mixed states.** The
  count under "all" adds up courses of every state; a stored course always has
  one of the five states (US-014 OD-010 skips any other before it is stored), so
  "all" equals the sum over the five filters for each teacher.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-005 | VR-001 | S-03 |
| AC-002 | FR-002, FR-003, FR-005 | VR-001 | — |
| AC-003 | FR-003, FR-004 | VR-003 | S-09 |
| AC-004 | FR-007 | — | S-05 |
| AC-005 | FR-006, FR-009 | VR-002 | — |
| AC-006 | FR-007, FR-009 | VR-003 | S-05, S-06, S-11 |
| AC-007 | FR-011 | — | — |
| AC-008 | FR-008 | — | — |
| AC-009 | FR-013 | — | S-04, S-12 |
| AC-010 | FR-012 | VR-004 | S-01, S-02, S-03 |
| AC-011 | FR-003, FR-004, FR-015 | — | S-09 |
| AC-012 | FR-013, FR-014 | — | S-04, S-11 |
| AC-013 (derived) | FR-009, FR-010, FR-016 | VR-001, VR-002, VR-003 | S-06, S-07, S-08 |
| AC-014 (derived) | FR-004, FR-006 | — | — |
| AC-015 (derived) | FR-006, FR-014 | VR-002 | — |
| AC-016 (derived) | FR-002, FR-003, FR-007 | — | S-05 |
