---
artifact_type: specification
story: US-024
version: 1
status: APPROVED
created_at: 2026-10-12T12:42:03Z
updated_at: 2026-10-12T12:51:16Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-024-database-statistics.md
    version: null
  - path: trebovaniya.md
    version: 90
  - path: docs/decisions/US-024-open-decisions.md
    version: 2
supersedes: null
---

# US-024 Specification — Database statistics and last synchronization

## 1. Overview

This Story gives Admin and Dean one page, "Database", that shows how much data
the installation holds, when it was last synchronized with Google and how that
went, and offers the "Synchronize" button (`trebovaniya.md` §4 Epic 5: "Раздел
показывает: общую статистику локальной БД (кол-во курсов, пользователей, встреч
Meet и т.п.), время последней синхронизации, кнопку "Синхронизировать""; §2 rows
"Раздел "Управление БД" (статистика)" ✔ Admin ✔ Dean and "Запуск синхронизации"
✔ Admin ✔ Dean; §4 Epic 6: "Экран статистики БД (Epic 5) — отдельно").

Four properties shape everything below.

- **Viewing is a pure read.** Opening the page writes nothing, calls no Google
  API and reads no secret. It works unchanged in read-only mode (§2: "работают
  просмотр и экспорт") and needs no read-only guard.
- **Counts of what is stored, nothing interpreted.** Each counter is a count of
  rows the synchronization stored (§3, v83 "a mirror nobody edits"); people are
  counted by the one current-roster rule already used by the course and teacher
  lists (US-020 OD-003, US-022 OD-002).
- **The button is the US-019 request.** Pressing it runs the existing
  `RequestSynchronizationUseCase` with its read-only refusal, connection refusal,
  audit row and messages. Only where the user lands afterwards is new — and that
  is settled by OD-011 (§11, resolved (a): a second endpoint), because US-019
  fixes the return page from the role on the server (US-019 VR-002).
- **No diagnosis here.** A failed run reads "failed" without a cause; the
  diagnosis stays on the Admin's connection page (US-017 FR-007/FR-008, OD-005).

Delegated lookups (read only, `quick-look`): `trebovaniya.md` v90 §2, §3
(`SyncState`, `MeetingCodeLink`, `CourseMembership`, course states), §4 Epic 5/6,
§7 and the time-zone rule; the US-017, US-019, US-022 and US-032 Specifications
and the US-019 API design; `security-conventions.md` SC-4/SC-11,
`api-conventions.md`, `non-functional-requirements.md`; the existing code of
`SyncState`, `SynchronizationRequestController`, `RequestSynchronizationUseCase`,
`InstallationPolicies`, `SignInRoutes`, the home page and the counting ports.
Read directly: `GetLastSynchronizationQuery`, `MeetCodesReadSource.GetCountsAsync`,
`Views/Home/Index.cshtml`, `InstallationExceptionHandler`.

## 2. Business Goal

Before relying on a journal or a report, the Dean needs to know whether the data
is current. Today only the Admin sees the state of synchronization, on the
connection page (US-017); the Dean has a button (US-019) but no way to tell how
fresh the data is or whether the last run failed. The Database page answers
"how much is there" and "how fresh is it" for both roles, and lets either of
them ask for a new run from the same place.

## 3. Business Flow

### 3.1 Opening the page

A signed-in Dean or Admin follows the "Database" link in the home page's new
Database section (FR-009). The page shows the counters (FR-002 … FR-004) and the
synchronization block (FR-005, FR-006) as they are at that moment, and the
"Synchronize" button (FR-007).

### 3.2 Requesting a run

The user presses "Synchronize". The request is handled exactly as US-019 handles
it (FR-007). On acceptance the user returns to the Database page with the US-019
confirmation message (OD-011). The page does not refresh by itself
(OD-008); a reload shows the newer state.

### 3.3 A failed run

The synchronization block reads "failed". A Dean sees a hint that the Admin has
the details; an Admin sees a link to the connection page (FR-006).

### 3.4 Before the first run

Every counter is zero and every time and the state read "not yet" (FR-005).

### 3.5 Read-only mode

The page shows everything. The button stays visible; a press is refused with the
reason, exactly as US-019 AC-005 (FR-008).

## 4. Functional Requirements

### FR-001 The Database page and its query

One server-rendered page (`GET`), reachable by Admin and Dean (route indicative,
`/database`; API_DESIGN fixes it). It has **no query parameters**; any parameter
sent is ignored and never echoed. The page is answered by a query in
`Application/UseCases` — `GetDatabaseStatisticsQuery`, name indicative —
returning a DTO (AD-8); the host only hands over and renders. No JSON endpoint
is added. The `GET` changes nothing (API-4, §8).

### FR-002 Course counters

- One count per course state of §3 — ACTIVE, ARCHIVED, PROVISIONED, DECLINED,
  SUSPENDED — over every stored `Course`, and their total (OD-002, AC-002).
- All five states are always shown, a state with no course showing 0.
- The total equals the sum of the five (a stored course always has one of the
  five states; US-014 OD-010 skips any other before it is stored).

### FR-003 People counters

- **Teachers** — the number of distinct `ClassroomParticipant`s with at least one
  `CourseMembership` where `Role = Teacher` and `OnRoster = true`, in a course of
  **any** state (OD-003; the US-022 rule without its status filter — Story Notes).
- **Students** — likewise with `Role = Student`.
- A participant with no membership on the roster (`OnRoster = false` everywhere)
  is counted in neither, though their record may await the retention purge
  (US-037). A participant who is a teacher in one course and a student in
  another is counted once in each (AC-003).
- A participant is counted at most once per counter, however many courses they
  are in.

### FR-004 Teaching and Meet counters

- **Coursework items** — the number of stored `CourseWork` rows.
- **Submissions** — the number of stored `Submission` rows.
- **Meet meetings** — the number of stored `MeetSession` rows.
- **Meet codes awaiting a decision** — the number of distinct meeting codes of
  stored `MeetSession`s that have no `MeetingCodeLink` row, i.e. neither a link to
  a course nor the "not a course" mark (§3 `MeetingCodeLink`, v88). This is the
  rule of the Meet codes page's "Unassigned" list (US-032 FR-001/FR-007,
  `IMeetCodesReadSource.GetCountsAsync`), so the two numbers always agree
  (AC-018).

### FR-005 The synchronization block

Read from the installation's single `SyncState` row (US-013). Shown identically
to both roles (OD-004):

| Line | Source | When absent |
|---|---|---|
| Last successful synchronization | `LastSuccessfulRunAt` | "not yet" |
| Current state | `Status`: Running → "in progress", Completed → "completed", Failed → "failed" | no row → "not yet" |
| Last full run | `LastFullRunAt` (v90) | "not yet" |
| Meet meetings loaded up to | `MeetLoadedUpTo` (v87) | "not yet" |

- No `SyncState` row means no synchronization has ever run: the state and all
  three times read "not yet" (AC-006).
- **Not shown:** `ProcessedCount` (OD-004), `LastError`, `FailedStep`, `RunId`,
  `StartedAt`, `FinishedAt`.
- **Times** are converted from UTC to the school's time zone in `Application`
  (`SchoolTimeZone`; §8 "Часовой пояс школы … даты показываются на экранах") and
  formatted as the other pages of the installation show them (the culture's short
  date and `HH:mm`, as on the course list and the connection page; Story Notes).
  The view does no time-zone arithmetic (AD-3).

### FR-006 A failed run

When the state is "failed":

- both roles see "failed" and no cause — no diagnosis text, failed step or error
  reaches the DTO or the page (OD-005; US-017 FR-008 keeps the diagnosis
  Admin-only);
- a **Dean** sees a hint that the Admin has the details;
- an **Admin** sees a link to the connection page (`SignInRoutes.WorkspaceConnection`),
  where the diagnosis lives (US-017).

The hint and the link are shown only for "failed". Which one is shown follows the
signed-in role; the link is a plain link, not enforcement — the connection page
keeps its own `ConfigureWorkspaceConnection` policy.

### FR-007 The "Synchronize" button

- A form on the Database page, for both roles, with an antiforgery token,
  posting a synchronization request (OD-006).
- The request is handled by the existing `RequestSynchronizationUseCase`
  unchanged: the read-only guard first, then the connection check, then the
  request to `ISynchronizationRequests`, then exactly one audit row
  `AuditAction.SynchronizationRequested` with the US-019 outcome and refusal
  category (US-019 FR-002 … FR-006). No new use case, audit action or message
  code is added for the request itself.
- The messages are US-019's: `Requested`, `RequestedAfterCurrentWork`, and for a
  connection that is not usable the role's variant (`ConnectionNotUsableAdmin`,
  `ConnectionNotUsableDean`). The one-time message travels as US-019 carries it
  (TempData `SynchronizationMessage`), never in the address.
- **After an accepted request the user is on the Database page** with the
  confirmation message (OD-006, AC-008). **After a connection refusal** the
  Database page is shown with the role's refusal message, status `409`, as
  US-019 does on the role's own page. **After a read-only refusal** the host's
  exception handler answers `409` with the reason, as for every blocked action
  (API-5, US-019 AC-005).
- **The press posts to a second endpoint of the Database page** (OD-011
  resolved (a); route fixed by API_DESIGN), policy `StartSynchronization`,
  calling the same `RequestSynchronizationUseCase` and always returning to the
  Database page; the US-019 endpoint is untouched. The page to return to is never taken from a free value in the
  request (no `returnUrl`, no referrer; US-019 VR-002) — the endpoint cannot
  become an open redirect.
- The existing buttons — the Dean's on the home page and the Admin's on the
  connection page — keep their behaviour and their return pages (OD-006).

### FR-008 Read-only mode

- Viewing is permitted (§2, BR-026). The query does not call
  `IReadOnlyModeGuard`, writes nothing — not even a permitted service write — and
  opens no Google port. Proven in `Application` for all three BR-025 causes
  (TC-5, AC-009).
- The button stays visible. A press is refused by the US-019 use case with the
  read-only reason; nothing is enqueued; the refusal audit row is written as
  US-019 writes it (OD-007).

### FR-009 Navigation

The home page gets a new section, "Database", with one link to the Database page
(OD-001). It is shown to Admin and Dean, in read-only mode too, placed after the
"Reports and templates" section and before the Admin-only settings section
(`admin-settings`), so it is the last section both roles see (AC-001). It is a
plain link, not enforcement — the page's policy is. Nothing else on the home page
changes.

### FR-010 Authorization

- The page declares a **new policy** for the §2 row "Раздел "Управление БД"
  (статистика)" — `ViewDatabaseStatistics`, name indicative — granting Admin and
  Dean (Story Scope "Roles"). Deny by default; the SC-4 anonymous list gains
  nothing; the endpoint enumeration test classifies the page as protected.
- The press is authorized by the existing `StartSynchronization` policy (§2
  "Запуск синхронизации", Admin and Dean), on the Database page's own endpoint (OD-011).
- An anonymous request is sent to sign in (SC-4). A Dean held on the forced
  password change (US-012) is refused as on every other page.
- No Teacher or Student access, no roster-based scoping (Hard Stop).

### FR-011 Persistence and performance

- **No new table, no new column.** No new audit action: viewing is not audited
  (SC-11 lists no viewing action); the press writes US-019's row.
- The counters and the synchronization state are read through read ports in
  `Application/Ports` implemented in `Infrastructure` with EF Core, without change
  tracking — a new port (`IDatabaseStatisticsSource`, name indicative), reusing
  `ISyncStateRepository.GetForReadAsync` and/or `IMeetCodesReadSource` where they
  fit; DB_DESIGN chooses. `DbContext` appears in neither `Application` nor `Web`
  (AD-3). No entity crosses into a view model (AD-8).
- **Counting happens in the database**: no row is loaded to be counted in memory,
  and the number of database round trips per request is fixed, independent of
  the number of courses, people, coursework items, submissions and meetings
  (AC-017).
- **Indexes** (PC-7, NFR-003): DB_DESIGN checks the counting queries against the
  existing indexes and adds any that is missing by an EF Core migration in this
  Story (PC-2). No NuGet package, no database extension.
- **Consistency between counters is not required** (I-5): the counters may be
  read while a run is storing data, and each reflects what was committed when it
  was read.

### FR-012 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): the page title, the home-page section heading
and link, every counter label, the "total" label, the four synchronization line
labels, the three states and "not yet", the Dean's hint, the Admin's link text and
the button label. The five course-state names and the US-019 button and message
texts are reused where they exist. The existing test that fails on a key missing
from one file covers the new keys. Nothing from Google is shown on this page.

### FR-013 Logging

- A shown page logs one line at `Information`: the actor's account id. No
  counter values are required in it; none is personal data if logged.
- The press logs as US-019 logs it (`SynchronizationRequestLog`).
- No log line carries a name, email, meeting code or Google id (SC-10, NFR-023,
  DC-10; v89 log-path rule).

## 5. Acceptance Criteria

The Story's twelve criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | Home page has a "Database" section, last among the sections both roles see, linking to the page | FR-009 |
| AC-002 | Courses in each of the five statuses — the count of each and the total | FR-002 |
| AC-003 | Teacher, student, teacher-and-student, and a leaver of every roster: teachers = 1st and 3rd, students = 2nd and 3rd, the 4th not counted | FR-003 |
| AC-004 | Counts of coursework, submissions, Meet meetings and Meet codes with neither link nor mark | FR-004 |
| AC-005 | Last successful, last full and Meet loaded-up-to times and the current state, same to both roles; no processed counter | FR-005 |
| AC-006 | Never run — every time and the state read "not yet"; counters zero | FR-002 … FR-005 |
| AC-007 | Failed — "failed", no cause; Dean hint; Admin link to the connection page | FR-006 |
| AC-008 | Press outside read-only — run requested as by US-019, one US-019 audit row, user back on this page with the US-019 confirmation | FR-007 (OD-011) |
| AC-009 | Read-only mode — counters and block shown, button visible, press refused with the US-019 reason, opening writes nothing | FR-008 |
| AC-010 | Dean and Admin open the page; unauthenticated refused; a press without a valid antiforgery token refused | FR-010, FR-007 |
| AC-011 | Bilingual page, section, labels, statuses, states, hint and messages | FR-012 |
| AC-012 | Tests never reach Google; synthetic data | FR-008, FR-011, TC-2, TC-4 |

Derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-013 | A failed run with a stored diagnosis and failed step: neither the diagnosis text, its key, nor the step appears in the page for either role | FR-005, FR-006 |
| AC-014 | Times shown in the school's time zone: a `LastSuccessfulRunAt` of 2026-03-01 22:30 UTC with zone `Europe/Kyiv` is shown as 2 March 2026 00:30 | FR-005 |
| AC-015 | A participant with two current teacher memberships and one current student membership is counted once as a teacher and once as a student; a membership of an ARCHIVED course counts as any other | FR-003 |
| AC-016 | Connection not usable — the press is refused with the role's US-019 message on the Database page, `409`, nothing enqueued, one refusal audit row | FR-007 |
| AC-017 | Bounded reading: the number of database queries for the page is the same with 1 and with 200 courses (and their people, coursework, submissions and meetings) | FR-011 |
| AC-018 | The Meet-codes-awaiting-decision counter equals the Unassigned count of the Meet codes page for the same data: a code with two meetings counts once; a linked code and a "not a course" code are not counted | FR-004 |
| AC-019 | Any query parameter sent to the page is ignored: the page is shown and the value appears nowhere in it | FR-001, VR-001 |

## 6. Validation Rules

### VR-001 The page request

The page binds no parameter. Unknown query parameters are ignored and never
echoed into the page, a link, the form or the language switcher's return path.

### VR-002 The press

- Antiforgery token required; missing or invalid → refused (API-7), nothing
  requested, no audit row.
- The press carries no user-chosen value that decides behaviour; the return
  page is fixed by the endpoint (OD-011 (a)), never taken from the request.

### VR-003 Data from the database

- A `SyncState.Status` outside the three known values is an unexpected failure
  (as in `GetLastSynchronizationQuery`), answered by the generic error page.
- A course `State` outside the five is not expected (US-014 OD-010); the counting
  query counts the five states by name and the total as their sum, so such a row,
  if it existed, would not be shown.

### VR-004 The actor

The role comes from the authenticated session, never from the request (SC-1).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page declares a new policy for the §2 "Управление БД (статистика)" row, granting Admin and Dean; deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for the page and for the press. | SC-1, TC-5 |
| S-03 | No Teacher or Student access, no roster-based scoping. | Hard Stop, §2 |
| S-04 | Viewing writes nothing, calls no Google API and reads no secret in any mode; proven in `Application` for all three BR-025 causes. | SC-5, SC-8, BR-026, AD-6 |
| S-05 | The press goes through the US-019 use case: read-only guard in `Application`, refusal audit, nothing enqueued when refused. | AD-6, SC-11, US-019 FR-005/FR-006 |
| S-06 | The press requires a valid antiforgery token. | API-7 |
| S-07 | The return page after the press is chosen on the server, never from a free request value; no open redirect. | US-019 VR-002, OD-011 |
| S-08 | No diagnosis text, failed step or error is shown on this page to either role. | OD-005, US-017 FR-008 |
| S-09 | The page address carries no parameter and no personal data; the one-time message travels outside the address. | Story "Address", US-039 INFO-1 |
| S-10 | No log line carries personal data or a meeting code. | SC-10, NFR-023, v89 |
| S-11 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-12 | No new outbound destination. | SC-13 (Hard Stop) |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request (page or press) | Redirected to sign-in (SC-4). |
| Dean on the forced password change | Refused as on every other page (US-012). |
| Never synchronized | Not an error: zeros and "not yet" (FR-005). |
| Last run failed | Not an error: "failed" with the hint or the link (FR-006). |
| Press in read-only mode | `409` with the reason from the host's exception handler (API-5, US-019). |
| Press with no usable connection | `409`, the Database page with the role's US-019 refusal message (FR-007). |
| Press without a valid antiforgery token | Refused per API-7. |
| Unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10, SC-10). |

## 9. Non-Functional Requirements

- **NFR-001** — the press returns immediately; the run happens in the background
  service (unchanged from US-019).
- **NFR-003 / PC-7** — explicit indexes back the counting queries (FR-011).
- **Bounded queries** — a fixed number of round trips, counting in the database
  (FR-011, AC-017). The sizing case is a school with several years of data
  (NFR-003): thousands of courses, tens of thousands of submissions and meetings.
- **NFR-070** — usable on a phone.
- **NFR-073** — bilingual; times in the school's time zone.
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- Any diagnosis of a failed run on this page (OD-005).
- The processed-courses counter (OD-004); `StartedAt`/`FinishedAt` of the run.
- Automatic refresh (OD-008).
- Database size on disk (OD-009).
- Export of the statistics (OD-010).
- Counters of application accounts, report templates or audit rows (OD-002).
- Counting people who left every course (OD-003).
- Any change to synchronization, to `RequestSynchronizationUseCase`, to the
  existing buttons and their return pages, or to the connection page.
- Any history of past runs (US-013 OD-004).
- A "Synchronize" button on the Admin's home page (US-019 I-6 stands).
- A JSON API for the statistics.

## 11. Open Decisions

Full text and resolutions in `docs/decisions/US-024-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Separate "Database" page, last section | **RESOLVED** 2026-10-12 | none — FR-001, FR-009 |
| OD-002 The counters | **RESOLVED** 2026-10-12 | none — FR-002 … FR-004 |
| OD-003 People by current roster | **RESOLVED** 2026-10-12 | none — FR-003 |
| OD-004 The synchronization block | **RESOLVED** 2026-10-12 | none — FR-005 |
| OD-005 Failed without cause; hint / link | **RESOLVED** 2026-10-12 | none — FR-006 |
| OD-006 Button for both roles; existing buttons unchanged | **RESOLVED** 2026-10-12 | none — FR-007 |
| OD-007 Read-only: page works, button as US-019 | **RESOLVED** 2026-10-12 | none — FR-008 |
| OD-008 No auto-refresh | **RESOLVED** 2026-10-12 | none — §10 |
| OD-009 No disk size | **RESOLVED** 2026-10-12 | none — §10 |
| OD-010 No export | **RESOLVED** 2026-10-12 | none — §10 |
| OD-011 How the press returns to the Database page | **RESOLVED** 2026-10-12 — option (a), a second endpoint | none — FR-007, VR-002, AC-008, AC-016, S-07 |

**OD-011** — the Story asks for "the same request as US-019: the same entry
point" *and* "after the press the user returns to this page", and puts "any
change … to the US-019 request" out of scope. US-019 chooses the return page from
the role on the server (Dean → home, Admin → connection page; US-019 VR-002), so
both cannot hold literally. **Resolved by the Owner on 2026-10-12 at
`HUMAN_SPEC_APPROVAL`: option (a).** The options were (details in the Open
Decisions artifact):

- **(a) A second endpoint for this page** (recommended) — a new `POST` on the
  Database page, policy `StartSynchronization`, calling the same
  `RequestSynchronizationUseCase` and always returning to the Database page.
  "The same entry point" is read as the same Application use case. The US-019
  endpoint is untouched; the return page is still fixed by the server.
- **(b) An origin field on the US-019 endpoint** — the forms send a value from a
  closed list (`database` or none); anything else falls back to the role page.
  One endpoint, but the US-019 endpoint, its VR-002 and its tests change.
- **(c) Keep the US-019 return** — the button on the Database page lands the Dean
  on the home page and the Admin on the connection page. No change anywhere, but
  OD-006 and AC-008 ("back on this page") are not met.

No item of `trebovaniya.md` §7 (v90) blocks the Story (see the Open Decisions
artifact).

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 "Users" are teachers and students.** §4 says "пользователей"; per the
  Story Notes and §2 (Epic 7), these are synced participants, not accounts.
- **I-2 The state before the first run reads "not yet".** AC-006 says "each time
  reads not yet"; the state line is not a time, but with no `SyncState` row there
  is no state either, so it reads "not yet" too (US-017 calls it "never run").
- **I-3 The total is the sum of the five states.** No "other" bucket is shown.
- **I-4 Stored rows are counted as stored.** Coursework, submissions and meetings
  are counted whatever the state of their course, including rows of a course or a
  person awaiting the retention purge — they are in the database until the purge
  removes them. Only people are filtered (by OD-003).
- **I-5 No snapshot across counters.** The counters are not required to be read
  in one transaction; during a run they may disagree with one another by the
  rows stored in between. A reload shows the newer numbers (OD-008).
- **I-6 Times are in the school's time zone.** The Story says "the way the other
  pages show them"; the course list, the course card and the connection page use
  the school's zone, while the home page's legitimacy line still shows UTC (US-008
  I-12). This page follows the majority and §8.
- **I-7 The hint and the link appear only for "failed".** In progress, completed
  and "not yet" show neither.
- **I-8 The page title doubles as the section name.** The home-page section is
  headed "Database" and holds one link, also "Database" (or a short "Statistics
  and synchronization"); the exact wording is chosen with the translations.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-009 | — | S-01 |
| AC-002 | FR-002 | VR-003 | — |
| AC-003 | FR-003 | — | S-03 |
| AC-004 | FR-004 | — | — |
| AC-005 | FR-005 | VR-003 | S-08 |
| AC-006 | FR-002 … FR-005 | — | — |
| AC-007 | FR-006 | — | S-08 |
| AC-008 | FR-007 | VR-002 | S-05, S-06, S-07 |
| AC-009 | FR-008 | — | S-04, S-05 |
| AC-010 | FR-007, FR-010 | VR-002, VR-004 | S-01, S-02, S-06 |
| AC-011 | FR-012 | — | — |
| AC-012 | FR-008, FR-011 | — | S-04, S-12 |
| AC-013 (derived) | FR-005, FR-006 | — | S-08 |
| AC-014 (derived) | FR-005 | — | — |
| AC-015 (derived) | FR-003 | — | — |
| AC-016 (derived) | FR-007 | VR-002 | S-05 |
| AC-017 (derived) | FR-011 | — | — |
| AC-018 (derived) | FR-004 | — | — |
| AC-019 (derived) | FR-001 | VR-001 | S-09 |
