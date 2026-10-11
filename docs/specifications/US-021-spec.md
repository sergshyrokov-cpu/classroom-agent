---
artifact_type: specification
story: US-021
version: 1
status: APPROVED
created_at: 2026-10-11T16:02:42Z
updated_at: 2026-10-11T16:07:25Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-021-course-detail.md
    version: null
  - path: trebovaniya.md
    version: 88
  - path: docs/decisions/US-021-open-decisions.md
    version: 1
supersedes: null
---

# US-021 Specification — Course detail with roster

## 1. Overview

This Story gives Admin and Dean the card of one synchronized Classroom course:
what the course is, who teaches it, who studies in it now, and who has left it
(`trebovaniya.md` §4 Epic 2 "Просмотр студентов курса"; §2 "Просмотр курсов и
участников" ✔ Admin ✔ Dean). The course list of US-020 links to it, and it links
on to the journal (US-025) and the Meet reports (US-033).

Four properties shape everything below.

- **A pure read.** The card writes nothing, calls no Google API and reads no
  secret. It works unchanged in read-only mode (§2, BR-026) and needs no
  read-only guard.
- **Only what US-014 stored.** The course, its participants and their
  memberships are shown as Google returned them (§3, v83 "a mirror nobody
  edits"); nothing is corrected, translated or added to what is stored.
- **The roster is `OnRoster`.** A current teacher or student is a membership
  with `OnRoster = true`; a leaver is a membership of the course with
  `OnRoster = false` (§3 `CourseMembership` "сейчас в ростере"; OD-005). Every
  stored membership of the course appears exactly once, in exactly one section.
- **The course is an internal id in the path.** No name, email or Google id is
  in any address the card produces or accepts.

Delegated lookups: none — the facts below were read directly (read only) from
the Story, `trebovaniya.md` v88 (§2, §3, §4 Epic 2, §5, §7), the US-020
Specification and Open Decisions, and the existing code: entities `Course`,
`CourseMembership`, `ClassroomParticipant`, enum `ClassroomRole`;
`RunSynchronizationUseCase.ResolveSubmitterAsync` (the off-roster student created
from submissions); `CoursesController`, `SignInRoutes` (`/workspace/courses`,
`/workspace/journal`, `/workspace/meet-reports`,
`/workspace/meet-reports/meetings/{meetingId}`), `CourseListPersonName`,
`InstallationPolicies.ViewCourses`, `UiLanguageController.ReturnPathViewDataKey`,
`ErrorController` and the status-code re-execution in `Program.cs`, the logging
level overrides in `InstallationLogging`.

## 2. Business Goal

US-020 shows a course's current teachers and only the **number** of its
students. A Dean who opens the journal of a course for a past period finds
students there who are not in the class today — the journal still counts
leavers (§4 Epic 3 "Кто в журнале за период") — and has nowhere to see who they
are. The card answers "whom is this course's journal and its Meet reports
about": the course's details, its current teachers (with the owner marked), its
current students, and those who have left with the day they were last seen.

## 3. Business Flow

### 3.1 Opening the card

In the course list (US-020) the Dean (or Admin) clicks a course's name. The
card opens: the course's details, then three sections — "Teachers",
"Students", "Left the course".

### 3.2 Moving on

From the card the user follows "Journal" or "Meet reports" (both open with this
course chosen), or "All courses" (the course list with its default filters). The
browser's Back returns to the list exactly as it was, because the list keeps its
filters in its address (US-020 FR-009).

### 3.3 A bad address

A course id that is malformed, out of range, or names no stored course gets the
installation's ordinary "not found" page (404). Nothing about the requested
value is shown or logged.

### 3.4 Read-only mode

The installation is suspended, past its grace period or never legitimated. The
card opens and behaves exactly as above (AC-010).

## 4. Functional Requirements

### FR-001 The card page

One server-rendered page (`GET`), reachable by Admin and Dean, addressed by the
course's internal id (`Course.Id`) in the path, under the course list's route
(indicative: `/workspace/courses/{courseId}`; API_DESIGN fixes it). No query
parameter is read; any query parameter is ignored and never echoed.

The data is produced by a use case (query) in `Application/UseCases` —
`GetCourseCardQuery`, name indicative — returning a DTO (AD-8); the host binds
the path value, renders, and maps "not found" to 404. No JSON endpoint is added
(I-8). The page is a `GET` and changes nothing (API-4, §8).

Every stored course of the installation can be opened — there are no "own
courses" in v1 (§3 `AppUser`), no per-Dean restriction and no roster scoping
(Hard Stop).

### FR-002 Course details

The card shows, in this order (OD-001):

| Field | Source | Shown when |
|---|---|---|
| Name | `Course.Name` as Google gave it; the page heading | always (required on import) |
| Section | `Course.Section` | present |
| Status | the translated name of `Course.State` — Active, Archived, Provisioned, Declined or Suspended (§3); the same labels as the US-020 list | always |
| Room | `Course.Room` | present |
| Description heading | `Course.DescriptionHeading` | present |
| Description | `Course.Description` — plain text: HTML-encoded, line breaks kept, never rendered as markup, links not made clickable (Story Notes) | present |
| Created in Google | `Course.CreationTime` in the school's time zone (`SchoolTimeZone`, NFR-074), date and time to the minute, formatted for the UI language | present |
| Last update in Google | `Course.UpdateTime`, as above | present |

- "Present" means a non-null value that is not empty or white space only. A
  field that is not present is **omitted** — neither its label nor a
  placeholder is shown (Story: "simply not shown").
- Never shown: `AlternateLink` (OD-002), `TeacherFolderId`,
  `TeacherFolderTitle`, `CalendarId` (OD-001), `GoogleId`, `OwnerGoogleId`,
  `CreatedAt`, `UpdatedAt`.
- Course data is never translated, corrected or re-cased (NFR-073; §3 v83).

### FR-003 Roster sections

The card shows three sections, in this order:

| Section | Memberships of this course | Row |
|---|---|---|
| **Teachers** | `Role = Teacher`, `OnRoster = true` | name (FR-005), email, owner mark (FR-004) |
| **Students** | `Role = Student`, `OnRoster = true` | name (FR-005), email |
| **Left the course** | `OnRoster = false`, either role | name (FR-005), email, role, last seen (FR-006) |

- "Teachers" and "Students" each show their count — the number of rows in the
  section (OD-004). "Left the course" shows its count too (I-4).
- A membership belongs to exactly one section; a leaver never appears among the
  current teachers or students (AC-004). The schema allows one membership per
  person per course (`uq_course_membership_course_participant`), so a person
  appears at most once on the card.
- The role shown in "Left the course" is the stored `CourseMembership.Role`,
  translated ("Teacher" / "Student").
- A leaver removed by the retention purge (§5, PC-11) has no membership any
  more and is therefore not shown; no other filter applies to leavers.

### FR-004 Owner mark

- A row in "Teachers" is marked "owner" (translated) when the participant's
  `GoogleUserId` equals the course's `OwnerGoogleId`, compared ordinally
  (OD-003; Story Notes; US-014 OD-004).
- At most one row can match, since `GoogleUserId` is unique per participant.
- An owner who is not a current teacher — absent from the roster, a leaver, or
  on the roster as a student — is not marked anywhere, not shown separately and
  not looked up in Google (OD-003). A course with no `OwnerGoogleId` marks no
  one.

### FR-005 The person

The name rule of US-020 FR-007 / OD-004, applied to every row of every section
(OD-006):

1. **Profile name** — "Прізвище Ім'я": `Surname` and `GivenName` joined by one
   space, as Google holds them (§3, v86); when only one of the two is present,
   that one alone.
2. **Email** — when neither part is present, the participant's `Email` as
   stored, in full.
3. **No name** — when there is neither, a translated label "no name" (I-2). A
   student created from submissions (§3, v56) has neither name nor email
   (`ResolveSubmitterAsync` imports the Google user id alone), so this is the
   expected label for such a leaver.

Beside the name, the **email** column shows `Email` as stored; it is empty when
the participant has no email (it is not repeated as a placeholder). `FullName`
and `GoogleUserId` are not shown (I-2). Names and emails are never translated,
corrected or re-cased.

### FR-006 Last seen in the roster

For a row in "Left the course", the date is `CourseMembership.LastSeenAt`
converted to the school's time zone, **date only**, formatted for the UI language
(OD-005). For a student created from submissions and never seen on the roster,
`LastSeenAt` is the instant of the synchronization that created the membership
(`FirstSeen` then `NotOnRoster`), so the date shown is that synchronization's day
(Story; AC-004). No other date is computed or shown for a leaver.

### FR-007 Order and no paging

- Each section is sorted by the shown name (FR-005) in the same
  Ukrainian-aware alphabetical order as the US-020 course list (US-020 FR-008,
  I-7): Ukrainian letters in their Ukrainian positions, Latin letters in Latin
  order, the same order whatever the UI language. Ties — equal names — are
  broken by the email (a row without email first), then by the internal
  participant id, so the order is total and stable.
- Rows labelled "no name" come after every named row of their section, ordered
  by internal participant id (I-3).
- The whole roster is on one page; no paging, no other sort order, no search or
  filter inside the roster (OD-007, OD-008).

### FR-008 Empty sections

- A section with no rows shows its own translated message instead of a table —
  "no teachers in the roster", "no students in the roster", "no one has left the
  course" (wording at implementation). Its count is shown as `0` (AC-009).
- None is an error.

### FR-009 Links

- **Course list → card.** In the US-020 course list, the course **name** in
  each row becomes a link to that course's card (OD-009). Nothing else in the
  list changes; its "Journal" and "Meet reports" links stay.
- **Card → journal** — the journal page with `courseId` set to the course's
  internal id (`/workspace/journal?courseId={id}` with today's routes).
- **Card → Meet reports** — likewise (`/workspace/meet-reports?courseId={id}`).
  Neither page is changed by this Story; they already accept `courseId` (Story
  Notes).
- **Card → course list** — the course list with its default filters
  (`/workspace/courses`, no query). Returning to the list's previous filters is
  out of scope; the browser's Back provides it (OD-009).
- All links are built from route constants, not hard-coded strings.

### FR-010 The address and the language switcher

- The card's address carries only the internal course id; no name, email or
  Google id appears in any URL the card produces.
- The card sets the language switcher's return-path override
  (`UiLanguageController.ReturnPathViewDataKey`, as the course list does) to the
  card URL rebuilt from the **parsed** course id — never from the raw request
  path — so switching the language keeps the user on the card (Story "Address").

### FR-011 Unknown course

The path value is validated before any course is read (§8, SC-10; VR-001). The
card answers **404** with the installation's ordinary not-found page (the
host's `/error/404` re-execution) when:

- the value is malformed (VR-001) — including a value the route does not match
  at all; or
- the value is well formed but no stored `Course` has that id — including a
  course removed by the retention purge.

Malformed and unknown are answered identically (OD-010): same status, same
page, no detail. The requested value is never put into the page, a link, the
switcher return path or a log line (FR-013).

### FR-012 Authorization

The card declares the existing policy `InstallationPolicies.ViewCourses` — the
§2 matrix row "Просмотр курсов и участников", ✔ Admin, ✔ Dean (US-020 FR-014
foresaw its reuse here). No new policy.

- An anonymous request is sent to sign in (SC-4); the SC-4 anonymous closed list
  gains nothing; the endpoint enumeration test classifies the card as
  protected.
- A Dean held on the forced password change (US-012) is refused as on every
  other page.
- No per-course restriction for a Dean in v1 (§2, §3 `AppUser`). Teacher and
  Student have no account (Hard Stop); visibility is never scoped by roster.

### FR-013 Logging

- A shown card logs one line at `Information`: the actor's account id, the
  internal course id, and the three section counts.
- A 404 logs one line at `Warning` naming the parameter and whether it was
  malformed or unknown — never the value (AC-008; SC-10). A value the route
  does not match never reaches the controller and logs nothing of its own
  (`Microsoft.*` is held at `Warning` and routing writes no such line).
- No log line carries a course name, section, description, room, a person's
  name, email or Google id (SC-10, NFR-023, DC-10).

### FR-014 Read-only mode

Viewing is permitted in read-only mode (§2, BR-026). The card query does not
call `IReadOnlyModeGuard`, writes nothing — not even a permitted service write —
and opens no Google port. Proven in `Application` for all three BR-025 causes:
the card is returned, no entity is added or modified, and no Google port is
invoked (TC-5, AC-010).

### FR-015 Persistence and performance

- **No new table, no new column, no new audit action.** Viewing is not audited
  (SC-11 lists no viewing action).
- The data is read through a read port in `Application/Ports` (name indicative,
  `ICourseCardSource`) implemented in `Infrastructure` with EF Core, without
  change tracking. `DbContext` appears in neither `Application` nor `Web`
  (AD-3); no entity crosses into a view model (AD-8). `ICourseListSource` and
  `IJournalSource` are not changed beyond what the course-name link needs (the
  list row already carries the course id).
- The number of database round trips per request is fixed — the course, and its
  memberships with their participants — independent of the roster size. No
  per-row query.
- **Indexes** (Story; PC-7, NFR-003): DB_DESIGN confirms whether the existing
  indexes on `course_membership` (`uq_course_membership_course_participant`,
  `ix_course_membership_course_seen`, `ix_course_membership_participant_id`,
  `ix_course_membership_current_teacher`) serve "all memberships of one course",
  and adds any missing one by an EF Core migration in this Story (PC-2). The
  ordering mechanism (the US-020 ICU collation in the query, or ordering in
  `Application`) is a DB_DESIGN / implementation choice proved by AC-015.

### FR-016 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): page title, the field labels of FR-002, the
three section titles and the count label, the column headers (name, email,
role, last seen), "owner", the two role names, "no name", the three empty-section
messages, and the three link texts. The five status names are the existing
US-020 keys. Dates follow the UI language. Course data, people's names and
emails are never translated (AC-012). The existing test that fails on a key
missing from one file covers the new keys.

## 5. Acceptance Criteria

The Story's thirteen criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Details: name, section, status, room, description heading and description, creation and update times in the school's time zone; absent values omitted; no Classroom link, teacher folder or calendar id | FR-001, FR-002 |
| AC-002 | Current teachers and current students in two sections, each with its count | FR-003 |
| AC-003 | Owner who is a current teacher is marked "owner"; an owner not in the roster — no one marked | FR-004 |
| AC-004 | A former teacher, a former student and a never-rostered submitter all under "Left the course" with role and last-seen date, none among current teachers or students | FR-003, FR-005, FR-006 |
| AC-005 | "Прізвище Ім'я" from the profile, email when no name, with the email, unchanged | FR-005 |
| AC-006 | Each section sorted by the shown name; whole roster on one page | FR-007 |
| AC-007 | Course name in the list links to the card; card links to the journal and Meet reports with the course chosen and to the course list | FR-009 |
| AC-008 | Malformed id or id of no course — "not found"; the value neither logged nor echoed | FR-011, FR-013, VR-001 |
| AC-009 | A section with no rows shows its own message | FR-008 |
| AC-010 | Read-only mode (any BR-025 cause) — the card works, nothing written | FR-014 |
| AC-011 | Dean and Admin open the card; an unauthenticated request is refused | FR-012 |
| AC-012 | UI strings in the user's language; course data and names unchanged | FR-002, FR-005, FR-016 |
| AC-013 | Tests never reach Google; synthetic data | FR-014, FR-015, TC-2, TC-4 |

Four criteria are derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-014 | Time zone: with the school's time zone `Europe/Kyiv`, a course whose `UpdateTime` is 22:30 UTC on 31 March 2026 shows 1 April 2026, 01:30; a leaver whose `LastSeenAt` is 22:30 UTC on 31 March 2026 shows the date 1 April 2026 (TC-8, NFR-074) | FR-002, FR-006 |
| AC-015 | Ukrainian order: students "Іваненко Олег", "Ігнатенко Анна", "Гнатюк Ірина", "Ґудзь Петро", "Андрієнко Марія" are listed as Андрієнко, Гнатюк, Ґудзь, Іваненко, Ігнатенко; a student with no name and no email is listed after all of them as "no name" | FR-005, FR-007 |
| AC-016 | Description as text: a description containing `<b>x</b>` and a line break is shown as the literal characters with the line break kept, never as markup | FR-002, VR-002 |
| AC-017 | Bounded reading: the number of database queries for a course with 3 members equals that for a course with 300 members and 50 leavers | FR-015 |

## 6. Validation Rules

### VR-001 The course id in the path

- Exactly the path segment: decimal digits only (`0`–`9`), parsing to a
  positive 64-bit integer (1 … 9 223 372 036 854 775 807).
- Anything else — empty, a sign, spaces, letters, a decimal point, leading `+`,
  zero, overflow — is **malformed** → 404 (FR-011). A non-digit value is
  expected to be rejected by the route constraint already (as
  `SignInRoutes.MeetReportMeeting` does); an all-digit value that overflows or
  is zero matches the route and is rejected by the parse. API_DESIGN fixes the
  route constraint.
- A well-formed id with no stored course is **unknown** → 404.
- Leading zeros (`007`) — I-6.

### VR-002 Data from the database

Values read from the database were validated on import (US-014, US-042) and are
rendered with HTML encoding; a course name, section, room, description heading,
description, person's name or email is never interpreted as markup (SC-10). The
description's line breaks are preserved by rendering, not by inserting markup
into the stored text.

### VR-003 The actor

The actor of the log line is the signed-in account from the session, never a
request value (US-019 VR-003).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The card declares `ViewCourses` (Admin, Dean); deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for the endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access, no roster-based scoping, no per-course restriction for a Dean. | Hard Stop, §2, §3 `AppUser` |
| S-04 | The card writes nothing, calls no Google API and reads no secret, in any mode; proven in `Application` for all three BR-025 causes. | SC-5, SC-8, BR-026, AD-6 |
| S-05 | The only request value is the course id; it is validated before any read; malformed and unknown answer the same 404 with no detail; the value never reaches the page, a link, the switcher return path or a log line. | §8, SC-10, OD-010 |
| S-06 | URLs produced by the card carry internal course ids only — no name, email or Google id. | Story "Address", US-039 INFO-1 |
| S-07 | No log line carries course text, a person's name, email or Google id; internal ids and counts only. | SC-10, NFR-023 |
| S-08 | All Google data, the description included, is HTML-encoded on output; nothing from the database is rendered as markup. | SC-10, VR-002 |
| S-09 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-10 | The course's Google link, teacher folder and calendar id are not exposed. No new outbound destination. | OD-001, OD-002, SC-13 (Hard Stop) |
| S-11 | Viewing writes no audit row. | SC-11 |
| S-12 | Emails of teachers and students are shown to Admin and Dean only, who already see roster data (§2 "Просмотр курсов и участников"). | §2, §5 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Signed-in user without the policy (a Dean on the forced password change) | Refused as on every other page (US-012). |
| Malformed course id | 404, the ordinary not-found page, no detail (FR-011). |
| Unknown course id (never existed or purged) | 404, the same page (FR-011). |
| A section with no rows | Not an error: FR-008 message. |
| A course with no optional details | Not an error: those fields are omitted (FR-002). |
| Read-only mode | Not an error: the card is shown (FR-014). |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

## 9. Non-Functional Requirements

- **NFR-003 / PC-7** — the card's queries are backed by explicit indexes
  (FR-015).
- **Bounded queries** — the number of database round trips does not grow with
  the roster (FR-015, AC-017). Sizing case: a course with a few hundred students
  and, after several years, a few hundred leavers until the retention purge
  removes them.
- **NFR-070** — usable on a phone: each section's table scrolls horizontally
  inside the page rather than widening it; a long description wraps.
- **NFR-073 / NFR-074** — bilingual; times and dates in the school's time zone;
  tests of displayed times use `Europe/Kyiv` (TC-8).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- Export of the roster (OD-011).
- The time of the last synchronization on the card (OD-012).
- Search or filters inside the roster, and any other sort order; paging
  (OD-007, OD-008).
- Returning from the card to the list with its previous filters (OD-009) — the
  browser's Back does it.
- Teachers and their courses as a separate view — US-022.
- Any change to synchronization or to what is stored about a course or a
  person; any change to the journal or Meet reports pages.
- A link to the course in Classroom (OD-002); showing or looking up an owner who
  is not a current teacher (OD-003).
- A JSON API for the card (I-8).

## 11. Open Decisions

Full text and resolutions in `docs/decisions/US-021-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Course details | **RESOLVED** 2026-10-11 (a) | none — FR-002 |
| OD-002 No Classroom link | **RESOLVED** 2026-10-11 (a) | none — FR-002 |
| OD-003 Owner mark | **RESOLVED** 2026-10-11 (a) | none — FR-004 |
| OD-004 Two sections with counts | **RESOLVED** 2026-10-11 (a) | none — FR-003 |
| OD-005 Leavers with last-seen date | **RESOLVED** 2026-10-11 (a) | none — FR-003, FR-006 |
| OD-006 Person | **RESOLVED** 2026-10-11 (a) | none — FR-005 |
| OD-007 Sorted by name, no paging | **RESOLVED** 2026-10-11 (a) | none — FR-007 |
| OD-008 No roster search | **RESOLVED** 2026-10-11 (a) | none — FR-007 |
| OD-009 Links | **RESOLVED** 2026-10-11 (a) | none — FR-009 |
| OD-010 Unknown course — 404 | **RESOLVED** 2026-10-11 (a) | none — FR-011 |
| OD-011 No export | **RESOLVED** 2026-10-11 (a) | none — §10 |
| OD-012 No last-sync time | **RESOLVED** 2026-10-11 (a) | none — §10 |

No new Open Decision was raised. No item of `trebovaniya.md` §7 blocks the
Story; items 14 and 27 affect which leaver memberships exist, not how the card
shows them (see the Open Decisions artifact).

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 "Not given" means null or blank.** The Story omits "a value Google did
  not give". Google may send an empty string as well as no field; both are
  treated as not given, so the card never shows a label with nothing after it.
- **I-2 "No name" is a neutral label.** US-020 has "teacher without a name"
  for a teacher row. The card shows teachers, students and leavers, and the
  typical nameless person here is a submitter-only student (no name, no email by
  design of US-015 OD-006); a role-neutral "no name" label fits every section.
  `FullName` is not used, as in US-020 I-2, so every person is shown surname
  first.
- **I-3 Nameless rows last.** Sorting a translated label among Google names
  would move those rows when the UI language changes; putting them after the
  named rows keeps one order for every user (as US-020 I-7 keeps one order).
- **I-4 The leavers section also shows its count.** OD-004 asks counts for
  "Teachers" and "Students"; showing the number of leavers too costs nothing,
  answers "how many have left" at a glance and keeps the three sections alike.
  If the Owner prefers no count there, it is dropped at the gate.
- **I-5 Leavers show the date, not the time.** OD-005 says "the date"; the
  synchronization instant to the minute adds nothing for a person who left and
  would suggest a precision the roster snapshot does not have.
- **I-6 Leading zeros.** `/workspace/courses/007` parses to 7. Either it opens
  course 7 (the parse is the rule) or it is malformed (only the canonical form
  is accepted). The Specification takes the first — the route constraint and
  the parse already accept it, and it reveals nothing — and the switcher return
  path and every link use the canonical form. API_DESIGN may tighten it.
- **I-7 The owner as a current student is not marked.** OD-003 marks the owner
  "when a current teacher". Google keeps the owner a teacher, so this case
  should not occur; if the stored data says otherwise, the card shows what is
  stored and marks no one, rather than inventing a role.
- **I-8 No JSON endpoint.** The card is a server-rendered page like the course
  list (US-020 I-6); `/api/v1` gains nothing.
- **I-9 404 for a purged course.** A course removed by the retention purge
  (§5) is indistinguishable from one that never existed; the card answers 404
  for both, revealing nothing about what was once stored.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-002 | VR-002 | S-03, S-08, S-09, S-10 |
| AC-002 | FR-003 | — | S-12 |
| AC-003 | FR-004 | — | — |
| AC-004 | FR-003, FR-005, FR-006 | — | — |
| AC-005 | FR-005, FR-016 | VR-002 | S-08, S-12 |
| AC-006 | FR-007 | — | — |
| AC-007 | FR-009, FR-010 | — | S-06 |
| AC-008 | FR-011, FR-013 | VR-001 | S-05, S-07 |
| AC-009 | FR-008 | — | — |
| AC-010 | FR-014 | — | S-04, S-11 |
| AC-011 | FR-012 | VR-003 | S-01, S-02, S-03 |
| AC-012 | FR-002, FR-005, FR-016 | — | S-08 |
| AC-013 | FR-014, FR-015 | — | S-04, S-10 |
| AC-014 (derived) | FR-002, FR-006 | — | — |
| AC-015 (derived) | FR-005, FR-007 | — | — |
| AC-016 (derived) | FR-002 | VR-002 | S-08 |
| AC-017 (derived) | FR-015 | — | — |
