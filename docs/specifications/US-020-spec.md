---
artifact_type: specification
story: US-020
version: 1
status: APPROVED
created_at: 2026-10-11T10:23:09Z
updated_at: 2026-10-11T10:58:55Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-020-course-list.md
    version: null
  - path: trebovaniya.md
    version: 88
  - path: docs/decisions/US-020-open-decisions.md
    version: 1
supersedes: null
---

# US-020 Specification — Course list with filters and search

## 1. Overview

This Story gives Admin and Dean the list of every Classroom course the
installation has synchronized, with a status filter, a teacher filter and a
search by name (`trebovaniya.md` §4 Epic 2 "Декан видит список всех курсов с
фильтрами (статус, преподаватель, поиск по названию)"; §2 "Просмотр курсов и
участников" ✔ Admin ✔ Dean). Each row links into the journal (US-025) and the
Meet reports (US-033), which already accept a course in their query.

Four properties shape everything below.

- **A pure read.** The list writes nothing, calls no Google API and reads no
  secret. It works unchanged in read-only mode (§2, BR-026) and needs no
  read-only guard.
- **Only what US-014 stored.** Courses, participants and memberships are read
  as Google returned them (§3, v83 "a mirror nobody edits"); nothing is
  corrected, translated or added to what is stored.
- **"Now" means the current roster.** A teacher of a course and the student
  count are memberships with `OnRoster = true` (OD-003, Story Notes); a person
  who has left the roster no longer counts.
- **The address carries the state.** The page is a `GET`; status, teacher,
  search and page are query parameters, so the list can be bookmarked and
  reloaded (OD-009). The teacher travels as an internal id, never as a name or
  an email.

Delegated lookups (read only, facts only): the relevant sections of
`trebovaniya.md` v88 (§2, §3, §4 Epic 2, §7, §8, time zone) and the facts of the
existing code (entities `Course`, `CourseMembership`, `ClassroomParticipant` and
their EF configuration and indexes; the journal and Meet reports pages, their
routes, `courseId` parameter, policies and language-switcher override; the home
page sections; `SchoolTimeZone`; `IJournalSource`; `IReadOnlyModeGuard`;
`SharedResource.*.resx`).

## 2. Business Goal

Today a course is chosen only from the plain drop-downs of the journal and the
Meet reports (US-025 OD-001). A Dean looking for "the 9-B physics course taught
by this teacher" scrolls an unfiltered list of every course, archived ones
included. The course list lets them narrow by status and teacher, search by a
fragment of the name or section, and go straight to the journal or the Meet
reports of the course they found. It is also the entry point of Epic 2: the
course card (US-021) and the teacher view (US-022) build on it.

## 3. Business Flow

### 3.1 Opening the list

From the home page the Dean (or Admin) follows "Courses", the first link of the
Google Workspace section (OD-010). The page shows the filter form — status
(pre-set to ACTIVE, OD-002), teacher (pre-set to "all teachers"), search (empty)
— and below it the first page of ACTIVE courses sorted by name.

### 3.2 Filtering and searching

The user picks a status or "all", a teacher, types part of a name or section,
and submits. The form is a `GET`: the parameters travel in the address and the
list restarts at page 1. Status, teacher and search apply together (AC-007).

### 3.3 Paging

When more than 50 courses match, page links move between pages keeping the
filters (OD-006).

### 3.4 Opening a course

Each row has two links: "Journal" opens the journal with this course chosen;
"Meet reports" opens the Meet reports with this course chosen (OD-007). What
those pages show with no period chosen is their existing behaviour.

### 3.5 Invalid input

An unknown status, a malformed or unknown teacher id, a malformed page number,
or a search text that breaks VR-003: the page shows the filter form with the
default value in place of the rejected one and a message naming the problem.
No list is shown (AC-013).

### 3.6 Read-only mode

The installation is suspended, past its grace period or never legitimated. The
list opens and behaves exactly as above (AC-014).

## 4. Functional Requirements

### FR-001 The course list page and its query

One server-rendered page (`GET`), reachable by Admin and Dean, under the
existing `/workspace/` area beside the journal and the Meet reports (route
indicative, `/workspace/courses`; API_DESIGN fixes it), with these query
parameters (names indicative; API_DESIGN fixes them):

| Parameter | Meaning | Absent or empty |
|---|---|---|
| `status` | one of the five course states, or `all` | ACTIVE (OD-002) |
| `teacherId` | internal id of a `ClassroomParticipant` from the teacher choice | no teacher filter |
| `q` | search text | no search |
| `page` | page number, starting at 1 (I-5) | 1 |

The page always shows the filter form with the effective values. The query is
answered by a use case (query) in `Application/UseCases` — `GetCourseListQuery`,
name indicative — returning a DTO (AD-8); the host only binds, validates shape
and renders. No JSON endpoint is added (I-6). The page is a `GET` and changes
nothing (API-4, §8).

### FR-002 Which courses are listed

Every stored `Course` of the installation is a candidate — there are no "own
courses" in v1 (§3 `AppUser`). No per-Dean restriction, no roster scoping (Hard
Stop). A course is listed when it satisfies every active filter (FR-004 …
FR-006); filters not given do not restrict.

### FR-003 The row

One row per course (OD-001), showing:

| Column | Content |
|---|---|
| Name and section | `Course.Name` as Google gave it; `Course.Section` beneath or beside it when present. Never translated. |
| Status | the translated name of `Course.State` — one of Active, Archived, Provisioned, Declined, Suspended (§3). |
| Teachers | every participant with a `CourseMembership` of this course where `Role = Teacher` and `OnRoster = true` (OD-003), each shown by the name rule of FR-007, ordered by that label in the order of FR-008, ties by internal participant id. None — the cell is empty. |
| Students | the number of memberships of this course with `Role = Student` and `OnRoster = true`; `0` when there are none. |
| Last update in Google | `Course.UpdateTime` converted to the school's time zone (`SchoolTimeZone`, NFR-074), date and time to the minute, formatted for the UI language; an empty cell when Google gave none (I-8). |
| Links | "Journal" and "Meet reports" (FR-010). |

The course's `AlternateLink` is not shown (OD-008). The course owner
(`OwnerGoogleId`) is a teacher in the row only when they have a teacher
membership on the roster (Story Notes; US-014 OD-004).

### FR-004 Status filter

- The choices are the five states of §3 and "all", in the order Active,
  Archived, Provisioned, Declined, Suspended, All, each with a translated label.
- Absent or empty — ACTIVE (OD-002). "All" lists every state.
- A chosen state lists only the courses with that `State`.

### FR-005 Teacher filter

- **The choice** lists every participant who has at least one
  `CourseMembership` with `Role = Teacher` and `OnRoster = true`, in **any**
  course whatever its state — owner and co-teachers alike (OD-003) — each once,
  labelled by the name rule of FR-007, ordered as in FR-008, ties by internal
  participant id. The first choice is "all teachers" (no filter). When two
  choices would carry the same label, the participant's email follows the label
  in parentheses for each of them (I-3); when one of them has no email, that one
  is distinguished by nothing further.
- **Filtering** by a participant lists only the courses where that participant
  has a membership with `Role = Teacher` and `OnRoster = true`. A course they
  have left as teacher, or in which they are a student, is not listed (AC-004).
- The parameter is the participant's internal id (`ClassroomParticipant.Id`),
  never an email, Google id or name (OD-009).
- A well-formed id that is not in the choice is refused (VR-002).

### FR-006 Search

- The search text, after trimming, matches a course when it is a substring of
  `Course.Name` **or** of `Course.Section`, ignoring letter case (OD-005) —
  for Latin and for Ukrainian Cyrillic letters alike (`Ї`/`ї`, `Є`/`є`,
  `І`/`і`, `Ґ`/`ґ`).
- The text is matched literally: `%`, `_`, `\` and any other character have no
  special meaning (I-4).
- No other folding: `е` and `є`, `и` and `і`, an apostrophe and a typographic
  apostrophe are different characters.

### FR-007 The teacher's name

The name rule of OD-004, for the row (FR-003) and the choice (FR-005):

1. **Profile name** — "Прізвище Ім'я": `Surname` and `GivenName` joined by one
   space, as Google holds them (§3, v86); when only one of the two is present,
   that one alone.
2. **Email** — when neither part is present, the participant's `Email` as
   stored, in full (I-2).
3. **No name** — when there is neither, the translated label "teacher without a
   name".

Names and emails are never translated, corrected or re-cased (NFR-073; §3 v83).
`FullName` is not used (I-2).

### FR-008 Order and paging

- Courses are sorted by `Name` in alphabetical order that places Ukrainian
  letters in their Ukrainian positions (`ґ` after `г`, `є` after `е`, `і` and `ї`
  after `и`), Latin letters in Latin order; then by `Section` (a course without a
  section first), then by internal course id — so the order is total and stable
  between pages (OD-006, I-7). The same order is used whatever the UI language
  (I-7). Teacher labels (FR-003, FR-005) use the same alphabetical order.
- 50 courses per page (OD-006). No choice of page size, no other sort order
  (Story Out of scope).
- The page shows the total number of matching courses and, when there is more
  than one page, links to the previous and next page and to each page number (or
  a compact range of them — UI detail for implementation), each keeping status,
  teacher and search.
- A page number past the last page shows the last page; when nothing matches,
  page 1 with the FR-011 message (I-5).

### FR-009 The address and the language switcher

- The form submits with `GET`; the page and every paging link carry only the
  effective, validated parameters: `status`, `teacherId`, `q`, `page`. Defaults
  may be omitted from links. A submitted filter change restarts at page 1.
- The teacher is the internal participant id (OD-009); no name, email or
  Google id appears in any URL the page produces.
- The page sets the language switcher's return-path override
  (`UiLanguageController.ReturnPathViewDataKey`, as the journal and the Meet
  reports do) to the course-list URL rebuilt from the **effective, validated**
  values — never from the raw query (US-025 security review I-2, US-039 INFO-1).
  After a validation failure the rejected parameter is not in the return path.
- Any other query parameter is ignored and never echoed (VR-005).

### FR-010 Links into the journal and the Meet reports

- "Journal" links to the journal page with `courseId` set to the course's
  internal id (`/workspace/journal?courseId={id}` with today's routes).
- "Meet reports" links to the Meet reports page with `courseId` set likewise
  (`/workspace/meet-reports?courseId={id}`).
- Neither page is changed by this Story; their course drop-downs stay (OD-007).
- The links are built from route constants, not hard-coded strings.

### FR-011 Empty states

- **No course synchronized yet** — when the installation stores no `Course` at
  all, the filter form is still shown and a translated message says no course
  has been synchronized yet; no table is drawn.
- **Nothing matches** — when courses are stored but none satisfies the filters
  (including the default ACTIVE filter when every course is archived), a
  translated message says no course matches the filters; no table is drawn.
- **No teacher to choose** — when no participant qualifies for FR-005, the
  teacher choice holds "all teachers" only.

None is an error.

### FR-012 Validation and its answer

The query is validated before any course is read for the list (§8, SC-10); the
rules are §6. On a failure:

- the page is re-rendered — **not** the host's error page — with the filter
  form and a translated message naming the problem (AC-013);
- the rejected field shows its default (status ACTIVE, teacher "all teachers",
  empty search, page 1); the rejected value is not put back into the form, the
  message, the links or the switcher return path;
- no course list is computed;
- the status code is chosen by API_DESIGN within API-5 (`400` for malformed
  input, `404` for a well-formed teacher id not in the choice is the expected
  choice, as the journal does for an unknown course);
- the rejected value is never written to a log (SC-10): the log line names the
  parameter and the rule, never the value.

When several parameters are invalid, every message is shown.

### FR-013 Navigation

- The home page's Google Workspace section gains "Courses" as its **first**
  link, before "Journal" (OD-010), for Admin and Dean, visible in read-only mode
  too (viewing is allowed, §2). Hiding it is never the enforcement — FR-014 is.
- The program has no menu outside the home page sections (the layout carries
  only the language switcher); this Google Workspace section is the "menu" of
  OD-010, and no new layout-wide menu is added (I-1).

### FR-014 Authorization

`InstallationPolicies` gains a policy for the §2 matrix row "Просмотр курсов и
участников" — ✔ Admin, ✔ Dean — `ViewCourses` (name indicative), declared by the
course-list page. It is its own policy (one policy per matrix row, US-019 I-5);
the course card (US-021) and the teacher view (US-022) reuse it.

- An anonymous request is sent to sign in (SC-4); the SC-4 anonymous closed list
  gains nothing; the endpoint enumeration test classifies the page as
  protected.
- A Dean held on the forced password change (US-012) is refused as on every
  other page.
- No per-course restriction for a Dean in v1 (§2, §3 `AppUser`). Teacher and
  Student have no account (Hard Stop), and visibility is never scoped by roster.

### FR-015 Read-only mode

Viewing is permitted in read-only mode (§2, BR-026). The list query does not
call `IReadOnlyModeGuard`, writes nothing — not even a permitted service write —
and opens no Google port. Proven in `Application` for all three BR-025 causes:
the list is returned, no entity is added or modified, and no Google port is
invoked (TC-5, AC-014).

### FR-016 Persistence and performance

- **No new table, no new column, no new audit action.** Viewing is not audited
  (SC-11 lists no viewing action).
- The data is read through a read port in `Application/Ports` (name indicative,
  `ICourseListSource`) implemented in `Infrastructure` with EF Core, without
  change tracking. `DbContext` appears in neither `Application` nor `Web`
  (AD-3). No entity crosses into a view model (AD-8). The existing
  `IJournalSource` is not changed.
- **Filtering, ordering and paging happen in the database** — the number of
  courses read is at most one page, not every course — and the number of
  database round trips per request is fixed, independent of the number of
  courses, teachers and students: the total count, the page of courses, the
  current teachers of the courses on the page, the student counts of the courses
  on the page, and the teacher choice. No per-row query.
- **Indexes** (Story; US-014 DB design §3.4; PC-7, NFR-003): DB_DESIGN chooses
  them against these queries — the state filter, the current teacher
  memberships of a participant, the current memberships of a course by role, and
  the case-insensitive substring search — confirming which existing indexes
  (`ix_course_membership_course_seen`, `ix_course_membership_participant_id`,
  `uq_course_membership_course_participant`) already serve them, and adds any
  that are missing by an EF Core migration in this Story (PC-2). The prototype's
  `idx_courses_state` / `idx_courses_owner` / `idx_courses_update` are a hint
  only. A database extension (for example trigram search) is a DB_DESIGN choice
  that must name its migration; no NuGet package is added.

### FR-017 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): page title, the navigation entry, the column
headers, the five status names and "all", the filter labels and "all teachers",
the search label and submit button, the paging labels and the total, the two
link texts, "teacher without a name", the empty states of FR-011, and every
validation message of §6. Dates follow the UI language. Google data — course
names, sections, teacher names and emails — is never translated. The existing
test that fails on a key missing from one file covers the new keys.

### FR-018 Logging

- A listed page logs one line at `Information`: the actor's account id, the
  effective status, the teacher id or none, whether a search text was given (yes
  or no — never the text, I-4), the page number, and the number of matching
  courses.
- A validation failure logs one line at `Warning` naming the parameter and the
  rule, never the value.
- No log line carries a course name, section, teacher name, email or search
  text (SC-10, NFR-023, DC-10).

## 5. Acceptance Criteria

The Story's seventeen criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | The row: name and section, status, current teachers, current student count, last update in Google in the school's time zone | FR-001, FR-002, FR-003 |
| AC-002 | No status chosen — only ACTIVE courses | FR-004 |
| AC-003 | A status or "all" — only that status, or all courses | FR-004 |
| AC-004 | Teacher filter — owner and co-teacher both offered and both find the course; a former teacher does not | FR-005 |
| AC-005 | Teacher name — "Прізвище Ім'я" from the profile, email when no name, unchanged | FR-007 |
| AC-006 | Search — name or section, any letter case | FR-006 |
| AC-007 | Status, teacher and search combine | FR-002, FR-004, FR-005, FR-006 |
| AC-008 | Sorted by name, 50 per page, paging keeps the filters | FR-008, FR-009 |
| AC-009 | Links to the journal and the Meet reports with the course chosen | FR-010 |
| AC-010 | Filters and page in the address; teacher as internal id; the switcher does not repeat the raw query | FR-009 |
| AC-011 | Two empty states, each with its own message | FR-011 |
| AC-012 | "Courses" first in the menu and first in the home page's Google Workspace section | FR-013 |
| AC-013 | Invalid status, teacher id or page refused with a message; value neither logged nor echoed | FR-012, FR-018, §6 |
| AC-014 | Read-only mode — the list works and nothing is written | FR-015 |
| AC-015 | Dean and Admin open the list; an unauthenticated request is refused | FR-014 |
| AC-016 | Bilingual; Google data unchanged | FR-003, FR-007, FR-017 |
| AC-017 | Tests never reach Google; synthetic data | FR-015, FR-016, TC-2, TC-4 |

Three criteria are derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-018 | Time zone: with the school's time zone `Europe/Kyiv`, a course whose `UpdateTime` is 22:30 UTC on 31 March 2026 shows 1 April 2026, 01:30 (TC-8, NFR-074) | FR-003 |
| AC-019 | Ukrainian order and case: courses named "Історія", "Інформатика", "Географія", "Ґрунтознавство", "Алгебра" are listed as Алгебра, Географія, Ґрунтознавство, Інформатика, Історія; a search for "ІСТ" finds "Історія" and a search for "%" finds only a course whose name or section contains "%" | FR-006, FR-008 |
| AC-020 | Bounded reading: with 120 matching courses, page 2 shows courses 51–100 in order, page 3 shows 101–120, and the number of database queries per request is the same for page 1 and page 3 and does not grow with the number of courses or memberships | FR-008, FR-016 |

## 6. Validation Rules

### VR-001 `status`

- Absent or empty — ACTIVE.
- Otherwise exactly one value, one of `active`, `archived`, `provisioned`,
  `declined`, `suspended`, `all`, case-insensitive (API_DESIGN fixes the exact
  tokens). Anything else — another word, a number, spaces inside, a repeated
  parameter — is **malformed**: message "unknown course status".

### VR-002 `teacherId`

- Absent or empty — no teacher filter.
- Otherwise exactly one value, decimal digits only, parsing to a positive 64-bit
  integer. Anything else — sign, spaces, letters, overflow, zero, a repeated
  parameter — is **malformed**: message "choose a teacher from the list".
- A well-formed id of a participant who is not in the FR-005 choice (no such
  participant, or not a current teacher of any course) is **unknown**: message
  "this teacher is not in the list — choose one from the list". No course list
  is read.

### VR-003 `q`

- Absent, empty, or only white space after trimming — no search.
- Otherwise exactly one value; after trimming, at most **100** characters
  (I-4). Longer, or a repeated parameter, is **malformed**: message "the search
  text is too long" (or "enter one search text" for a repeated parameter —
  wording at implementation).
- A control character (Unicode category Cc) anywhere in the text is malformed:
  message "the search text contains characters that cannot be searched".
- Every other character is allowed and matched literally (FR-006).

### VR-004 `page`

- Absent or empty — 1.
- Otherwise exactly one value, decimal digits only, parsing to an integer from
  1 to 100 000 inclusive (I-5). Anything else — zero, sign, spaces, letters,
  overflow, a repeated parameter — is **malformed**: message "invalid page
  number".
- A valid number past the last page is not an error (FR-008).

### VR-005 Unknown parameters and data from the database

- Any other query parameter is ignored; it is never echoed into the page.
- Values read from the database were validated on import (US-014) and are
  rendered with HTML encoding; a course name, section, teacher name or email is
  never interpreted as markup (SC-10).

### VR-006 The actor

The actor of the log line is the signed-in account from the session, never a
request value (VR-003 of US-019).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page declares its own policy granted to Admin and Dean (`ViewCourses`); deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for the endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access, no roster-based scoping, no per-course restriction for a Dean. | Hard Stop, §2, §3 `AppUser` |
| S-04 | The list writes nothing, calls no Google API and reads no secret, in any mode; proven in `Application` for all three BR-025 causes. | SC-5, SC-8, BR-026, AD-6 |
| S-05 | URLs produced by the page carry status, the internal teacher id, the search text and the page only — no name, email or Google id. | OD-009, US-039 INFO-1 |
| S-06 | The language switcher's return path is rebuilt from validated values, never from the raw query. | US-025 security review I-2, US-039 INFO-1 |
| S-07 | Invalid input is rejected before any course is read; the rejected value never reaches a log, the form, a link or an error page detail. | §8, SC-10 |
| S-08 | No log line carries a course name, section, teacher name, email or the search text; internal ids only. | SC-10, NFR-023 |
| S-09 | Google data is HTML-encoded on output; nothing from the database is rendered as markup. The search text is matched as data, never as a pattern or SQL. | SC-10, VR-005, FR-006 |
| S-10 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-11 | The course's Google link is not exposed (OD-008). No new outbound destination. | OD-008, SC-13 (Hard Stop) |
| S-12 | Viewing writes no audit row. | SC-11 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Signed-in user without the policy (a Dean on the forced password change) | Refused as on every other page (US-012). |
| Malformed status, teacher id, search text or page | The course-list page with the form and the VR message; no list; status per API_DESIGN within API-5 (expected `400`). |
| Unknown teacher id | The course-list page with the form and the VR-002 message; no list; status per API_DESIGN (expected `404`). |
| No course stored yet | Not an error: FR-011 message. |
| No course matches | Not an error: FR-011 message. |
| Page past the last | Not an error: the last page (FR-008). |
| Read-only mode | Not an error: the list is shown (FR-015). |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

## 9. Non-Functional Requirements

- **NFR-003 / PC-7** — explicit indexes back the list queries (FR-016).
- **Bounded queries** — the number of database round trips and of courses read
  does not grow with the number of courses (FR-016, AC-020). A school with a few
  thousand courses accumulated over several years and a few hundred teachers is
  the sizing case DB_DESIGN reasons about.
- **NFR-070** — usable on a phone: the table scrolls horizontally inside the page
  rather than widening it; the filter form is usable at phone width.
- **NFR-073 / NFR-074** — bilingual; times in the school's time zone; tests of
  displayed times use `Europe/Kyiv` (TC-8).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- Export of the course list (OD-011).
- The course card with its roster — US-021; teachers and their courses as a
  separate view — US-022.
- Removing or changing the course drop-downs of the journal and the Meet reports
  (OD-007); any change to those pages.
- A link to the course in Classroom (OD-008).
- Any sort order other than by name; a choice of page size.
- Any change to synchronization or to what is stored about a course.
- A layout-wide menu (I-1).
- A JSON API for the list (I-6).

## 11. Open Decisions

Full text and resolutions in `docs/decisions/US-020-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Row contents | **RESOLVED** 2026-10-11 (a) | none — FR-003 |
| OD-002 Status filter, ACTIVE by default | **RESOLVED** 2026-10-11 (a) | none — FR-004 |
| OD-003 Teacher = current teacher in the roster | **RESOLVED** 2026-10-11 (a) | none — FR-003, FR-005 |
| OD-004 Teacher name | **RESOLVED** 2026-10-11 (a) | none — FR-007 |
| OD-005 Search in name and section | **RESOLVED** 2026-10-11 (a) | none — FR-006 |
| OD-006 50 per page, by name | **RESOLVED** 2026-10-11 (a) | none — FR-008 |
| OD-007 Links to the journal and the Meet reports | **RESOLVED** 2026-10-11 (a) | none — FR-010 |
| OD-008 No Classroom link | **RESOLVED** 2026-10-11 (a) | none — FR-003 |
| OD-009 Filters in the address | **RESOLVED** 2026-10-11 (a) | none — FR-009 |
| OD-010 "Courses" first | **RESOLVED** 2026-10-11 (a) | none — FR-013 |
| OD-011 No export | **RESOLVED** 2026-10-11 (a) | none — §10 |

No new Open Decision was raised. No item of `trebovaniya.md` §7 blocks the
Story (see the Open Decisions artifact).

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 The home page section is the menu.** OD-010 and AC-012 speak of "the
  first menu entry" and "the first link in the home page's Google Workspace
  section". The program has no menu outside the home page: the layout carries
  only the language switcher, and every screen is reached from the home page
  sections. Both phrases are therefore read as one requirement — "Courses" first
  in the Google Workspace section. Adding a layout-wide menu would change every
  page and is not asked for.
- **I-2 The full email, and no `FullName`.** OD-004: "Прізвище Ім'я" from the
  profile, "email when there is no name". The report rule of US-042
  (`ReportPersonNameRule`) shows the email's local part and depends on a
  template's name source; the course list has no template, and the Story says
  "the email", so the full stored address is shown. `FullName` is Google's
  composed name, often "Ім'я Прізвище"; it is not used, so every teacher is shown
  surname first. A participant with neither name nor email — possible in stored
  data — gets the translated "teacher without a name".
- **I-3 Same-name teachers in the choice.** Two teachers with the same surname
  and given name would be indistinguishable in the teacher choice; the email is
  appended to both in that case only. Emails are already visible to Admin and
  Dean as roster data. Rows are not changed: a row lists names only.
- **I-4 Search length and literal matching.** A course name is at most 750
  characters and a section 2800 (US-014), but a search fragment longer than 100
  characters has no practical use; 100 bounds the input (§8). The text is
  matched literally so `%` or `_` typed by a user is not a wildcard. The search
  text is not logged — it is free user input.
- **I-5 Pages are numbered from 1; a page past the end shows the last.** API-8
  numbers pages from 0 for `/api/v1` JSON collections; this is a server-rendered
  page whose address a person reads and bookmarks (OD-009), so pages count from
  1. A bookmarked page number can outlive the courses it pointed to; showing the
  last page is kinder than an error. The upper bound of 100 000 only keeps the
  number in range (50 × 100 000 courses is far beyond any school).
- **I-6 Paginated page, no JSON endpoint.** API-8's "collections are paginated
  from day one" is met by the 50-per-page paging; its JSON body shape applies to
  `/api/v1` endpoints, and this Story adds none.
- **I-7 One alphabetical order, Ukrainian-aware.** OD-006 says "sorted by name".
  Paging must happen in the database (FR-016), so the order cannot depend on the
  UI language per request without a second sort; course names are mostly
  Ukrainian, so the order is Ukrainian-aware for every user. Ties are broken by
  section, then id, so pages never overlap or skip a course. DB_DESIGN chooses
  the mechanism (for example an ICU collation in the query) and proves it by
  AC-019.
- **I-8 A course without `updateTime`.** Google normally sends it; when it is
  absent the cell is empty rather than showing a made-up date.
- **I-9 An unknown teacher id is refused, not an empty list.** A teacher id not
  in the choice comes from an outdated bookmark or a hand-edited address. Saying
  so (VR-002) is clearer than "no course matches", and mirrors the journal's
  unknown course (US-025 VR-001).

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-007 | VR-005 | S-03, S-09, S-10, S-11 |
| AC-002 | FR-004 | VR-001 | — |
| AC-003 | FR-004 | VR-001 | — |
| AC-004 | FR-005 | VR-002 | S-05 |
| AC-005 | FR-007, FR-017 | — | S-09 |
| AC-006 | FR-006 | VR-003 | S-09 |
| AC-007 | FR-002, FR-004, FR-005, FR-006 | VR-001, VR-002, VR-003 | — |
| AC-008 | FR-008, FR-009 | VR-004 | — |
| AC-009 | FR-010 | — | — |
| AC-010 | FR-009 | VR-002, VR-005 | S-05, S-06 |
| AC-011 | FR-011 | — | — |
| AC-012 | FR-013 | — | — |
| AC-013 | FR-012, FR-018 | VR-001 … VR-005 | S-07, S-08 |
| AC-014 | FR-015 | — | S-04, S-12 |
| AC-015 | FR-014 | VR-006 | S-01, S-02, S-03 |
| AC-016 | FR-003, FR-007, FR-017 | — | S-09 |
| AC-017 | FR-015, FR-016 | — | S-04, S-11 |
| AC-018 (derived) | FR-003 | — | — |
| AC-019 (derived) | FR-006, FR-008 | VR-003 | S-09 |
| AC-020 (derived) | FR-008, FR-016 | VR-004 | — |
