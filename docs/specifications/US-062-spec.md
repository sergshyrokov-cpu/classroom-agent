---
artifact_type: specification
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T18:35:09Z
updated_at: 2026-10-27T18:35:09Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: trebovaniya.md
    version: 103
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
supersedes: null
---

# US-062 Specification — Data-health banner, the «Стан даних» page and the Admin's confirmation of deleted courses

Delegation: three read-only lookups collected (a) the `trebovaniya.md` v103 passages — §2 matrix
rows 744, 749, 750 and notes 764–769, §4 Epic 1 lines 1106–1191, Epic 6 lines 1453–1461, §5 audit
lines 1743–1745, §7 lines 2045–2108; (b) domain facts (`SyncState`, `SyncSkippedCourse`, `Course`,
`CourseWork`, `Submission`, `LegitimacyState`, `GracePeriod`, `WorkspaceConnection`); (c) web-layer
facts (layout, routes, policies, read-only refusal, texts, audit). The spec writer read the Story,
BR-025…BR-027, BR-043…BR-049, BR-081 in `docs/product/business-rules.md`, `trebovaniya.md` lines
740–770 and 1100–1194, SC-11, NFR-033, NFR-074, NFR-075, and checked `Course.MarkAbsent` /
`RecordListed`, `SyncState.Begin` / `RecordCourseListRead`, `GetLegitimacyModeQuery` and
`RunSynchronizationUseCase.cs` lines 615–660.

## 1. Overview

US-058…US-060 store and show what synchronization notices, course by course. Nothing yet tells the
Dean that the data has problems at all. This Story adds:

- a list of **problems** computed from stored data (no Google call), of two kinds — *teaching*
  (fixed by a teacher) and *technical* (fixed by the Admin or the Owner);
- a **banner** at the top of every page of a signed-in Admin or Dean while any counted problem
  exists: «N технічних проблем, M навчальних», linking to «Стан даних»;
- the **«Стан даних» page** with every problem's details, hints, and the separate archived list;
- the Admin's **confirmation** that courses "not visible to the technical account" were deleted in
  Classroom;
- the **date** since which course lists have come back empty in a row, stored next to today's flag.

### 1.1 Code facts the Specification must not contradict

- `SyncState` (one row, may be absent before the first run): `Status` (`Running`, `Completed`,
  `Failed`), `StartedAt`, `FinishedAt`, `LastError` (the name of a `SyncDiagnosis`: `ScopeNotAuthorized`,
  `TechnicalAccountUnknown`, `TechnicalAccountCannotRead`, `ApiNotEnabled`, `KeyUnavailable`,
  `KeyRejected`, `GoogleUnavailable`, `Unexpected`), `FailedStep`, `LastSuccessfulRunAt` (written only
  by `CompleteRun`), `LastCourseListEmpty` (bool, set by `RecordCourseListRead`). Starting a run
  (`Begin`) sets `Status = Running` and clears `LastError` and `FailedStep`.
- `SyncSkippedCourse`: `CourseGoogleId` (unique), `CourseName?`, `Reason` (`ValueOutOfRange`,
  `UnknownCourseState`), `SkippedSince`. No foreign key to `Course`.
- `Course`: `State` (`Active`, `Archived`, `Provisioned`, `Declined`, `Suspended`), `AbsenceMark?`
  (`NotInClassroom`, `NotVisibleToTechnicalAccount`) with `AbsentSince`, `LastListedAt`,
  `LastCompleteReadAt`. `MarkAbsent` refuses to change a mark already set; `RecordListed` removes any
  mark. Synchronization leaves an already-marked absent course's mark as it is
  (`RunSynchronizationUseCase.cs:633-636`).
- `CourseWork.MaxPoints` (`decimal?`), `CourseWork.NotInClassroomSince`; `Submission.AssignedGrade`,
  `DraftGrade` (`decimal?`); `DataMarkRules.IsAboveMaximum(points, max)` = both present, max > 0,
  points > max.
- `LegitimacyState.LastSuccessfulCheckAt?`, `Status`, `Compatibility` (`Supported`,
  `UpgradeRecommended`, `UpgradeRequired`); an `upgrade_required` answer records `Compatibility`
  without moving `LastSuccessfulCheckAt`. `GracePeriod.Length` = 7 days, `HasExpired` = strictly more.
  `GetLegitimacyModeQuery` gives `IsReadOnly` and `LegitimacyModeReason` (`NotYetConfirmed`,
  `SuspendedByOwner`, `GracePeriodExpired`).
- `WorkspaceConnection.TechnicalAccountChangedAt`.
- Web: MVC controllers, one layout `Views/Shared/_Layout.cshtml`; the forced password change is
  `/sign-in/change-password`, enforced by `TemporaryPasswordMiddleware`. The journal is
  `/workspace/journal?courseId=<id>`. The Admin's synchronization state ("Last synchronization") is a
  section of `/settings/workspace-connection`, its diagnosis texts are `LastSync.Diagnosis.*` and
  `AccessCheck.Outcome.*`. The read-only message lives on the landing page (`Landing.ReadOnly.*`).
- Read-only refusal: `IReadOnlyModeGuard.EnsureAllowedAsync` throws `ReadOnlyModeException`, mapped to
  409. A signed-in user failing a policy gets 403.
- Texts: `Application/Localization/SharedResource.uk.resx` / `.en.resx`, dotted keys. Audit:
  `AuditAction` enum + `AuditEvent` factories, written through `IAuditEventRepository.Add`.

## 2. Business Goal

Deans trust the data and will not look for its problems themselves (Owner 2026-10-26). The banner
makes them visible on every page; the page says what each problem is and who fixes it — a teacher,
the Admin or the Owner — so nobody acts on data synchronization could not keep current.

## 3. Business Flow

1. Synchronization runs as before and stores marks, skip records and its state; it now also stores
   the empty-list date (FR-008).
2. On every page request of a signed-in Admin or Dean, the system computes the problem counts from
   stored data and shows the banner if any counted problem exists (FR-003).
3. The user follows the banner to «Стан даних» and sees the problems as their role permits (FR-004,
   FR-005).
4. A Dean takes teaching problems to the teacher, technical ones to the Admin. The Admin fixes
   technical problems, tells the Owner about the Control Plane problem, or confirms that courses the
   technical account cannot see were deleted (FR-006).
5. When a cause is gone — a run loads the corrected data, a run succeeds, the connection returns —
   the problem disappears at the next request (FR-001.4).

## 4. Functional Requirements

### FR-001 Problems

FR-001.1 The system derives the current problems from stored data only — `SyncState`,
`SyncSkippedCourse`, `Course`, `CourseWork`, `Submission`, `LegitimacyState`, the legitimacy mode and
the clock. It never calls Google and never writes while doing so.

FR-001.2 **Teaching problems** (one per course and kind):

| Id | Kind | Present when | Details |
|---|---|---|---|
| T1 | Points above the maximum (BR-067) | the course has at least one submission whose `AssignedGrade` or `DraftGrade` is above its item's `MaxPoints` (`DataMarkRules.IsAboveMaximum`) | each such grade: item title, student name, points, maximum, and whether it is a draft |
| T2 | Items "not in Classroom" (BR-045) | the course has at least one `CourseWork` with `NotInClassroomSince` set | each item: title and the date of the mark |
| T3 | Course skipped for out-of-range or negative values (BR-043) | a `SyncSkippedCourse` with `Reason = ValueOutOfRange` | since when the course is not updated (FR-001.6); hint H1 |
| T4 | Course "not in Classroom" (BR-048) | `Course.AbsenceMark = NotInClassroom` | the mark's date; hint H2 |

FR-001.3 **Technical problems**:

| Id | Kind | Present when | Admin's details |
|---|---|---|---|
| X1 | The last run failed | `SyncState.Status = Failed` | the diagnosis text the connection page shows for `LastError` (same keys), the failed step and the finish time; a link to the connection page |
| X2 | No successful synchronization for more than 24 hours, or none yet | not in read-only mode, and `LastSuccessfulRunAt` is absent (or there is no `SyncState`) or more than 24 hours before now | the time of the last successful run, or "none yet" |
| X3 | Course skipped for an unknown state (BR-046) | a `SyncSkippedCourse` with `Reason = UnknownCourseState`, one per course | the course name, since when it is skipped |
| X4 | Google returns no course (BR-048, v102) | the empty-list date (FR-008) is set | «Google не повертає жодного курсу з <date> — перевірте роль технічного акаунта» |
| X5 | Courses not visible to the technical account (BR-049) | at least one course with `AbsenceMark = NotVisibleToTechnicalAccount` — **one** problem for all of them, archived included | the list of those courses (name, state, mark date, last successful read) with the confirmation of FR-006 |
| X6 | No connection with the Control Plane (BR-047, v103) | not in read-only mode, `LastSuccessfulCheckAt` set, and now − `LastSuccessfulCheckAt` more than 24 hours | «Немає зв'язку із сервісом з <C>; з <C + 7 днів> програма перейде в режим «лише для читання», якщо зв'язок не відновиться», hint H5; when `LegitimacyState.Compatibility = UpgradeRequired`, the reason: the installation's version is no longer supported (BR-027) |

X6 is computed from the same instant and grace period as `GetLegitimacyModeQuery`: the read-only date
shown is `LastSuccessfulCheckAt + GracePeriod.Length`. At exactly 24 hours nothing is raised
(strictly more, as in AC-013's "C1 + 23 hours"). Once the installation is read-only for any reason, X2
and X6 are not raised.

FR-001.4 **Current state only.** A problem exists exactly while its condition of FR-001.2/FR-001.3
holds at the moment of the request. No problem is stored, acknowledged or expired.

FR-001.5 **Counting.** Each teaching problem and each per-course technical problem (X3) counts one. X1,
X2, X4, X5 and X6 count one each. A per-course problem of a course whose stored `State = Archived` is
**not counted**; X5 counts once whatever its courses' states. A skipped course with no `Course` row
(never loaded) is not archived and is counted.

FR-001.6 **Course identity for skip records.** A `SyncSkippedCourse` is matched to a `Course` by
Google course id. A matched course is shown by its stored name; a never-loaded one by
`SyncSkippedCourse.CourseName` (BR-046), or by its Google course id when the name is absent. "Not
updated since" for T3 is the course's `LastCompleteReadAt`; for a loaded course without it the text
has no date (as US-060 OD-002 a); for a never-loaded course the text says it has not been loaded yet
and has been skipped since `SkippedSince`.

### FR-002 Who sees what

FR-002.1 Both Admin and Dean see the banner and «Стан даних» (§2 row 749). Every Dean sees every
course's problems (dean scope is a later edition).

FR-002.2 Teaching problems: both roles see their full details.

FR-002.3 Technical problems: the **Dean** sees, for each, its title line (texts §FR-007) and hint H3
«Повідомте Адміністратора», and no details — no diagnosis, course list or course names for X3/X5. The
title of X6 for the Dean carries the two dates (AC-013). The **Admin** sees the title and the details
of FR-001.3; for X6 hint H5 «Повідомте Власника» instead of H3.

### FR-003 Banner

FR-003.1 Shown at the top of every HTML page rendered with the installation layout for a signed-in
Admin or Dean, while at least one counted problem exists (FR-001.5), **except** the forced password
change page (OD-001 a). It is not shown on pages without a signed-in user and not part of any
`/api/v1` response.

FR-003.2 Text: «N технічних проблем, M навчальних» with the language's plural forms (Ukrainian one /
few / many; English one / other), both numbers always shown, zero included; the whole text links to
«Стан даних». Example for 1 and 3: «1 технічна проблема, 3 навчальні» / «1 technical problem,
3 teaching».

FR-003.3 Restrained: a coloured strip — no icon animation, sound, pop-up, blinking or dismiss
control (feedback "restrained UI tone").

FR-003.4 Shown in read-only mode like any view.

### FR-004 «Стан даних» page

FR-004.1 A page of the installation for Admin and Dean (new policy, both roles), under the
installation layout; its exact route is fixed by API_DESIGN. Title «Стан даних» / «Data health».

FR-004.2 Sections, in order: technical problems; teaching problems; archived courses. A section with
nothing to show says so in one line. When no problem exists at all, the page says «Проблем із даними
немає».

FR-004.3 **Teaching problems**: grouped by course (course name in the language's culture order), the
kinds in the order T4, T3, T2, T1. Each course has a link to its journal (`/workspace/journal?courseId=
<id>`) when a `Course` row exists; a never-loaded skipped course has none. T1 rows are ordered by item
then student. Under the section's heading, for the Dean only, hint H4 «Якщо з викладачем вирішити не
вдалося, зверніться до Адміністратора» (v100).

FR-004.4 **Archived courses**: the per-course problems (T1–T4, X3) of courses with `State = Archived`,
in the same layout as FR-004.3, under the heading «Архівні курси (не враховуються)». Archived courses
in X5 stay in X5 and are not repeated here.

FR-004.5 **Read-only mode**: the page works; when the installation is read-only, the page shows the
existing read-only message (the landing page's `Landing.ReadOnly.*` text for the current reason) at its
top, in place of the X6 problem that can no longer exist.

FR-004.6 Dates and times in the school's time zone (NFR-074), formatted as elsewhere in the
installation.

### FR-005 Page-search hint

The page lists teaching data and shows the ordinary one-line page-search hint under its title
(NFR-075, interpretation I-7).

### FR-006 The Admin's confirmation of deleted courses (BR-049)

FR-006.1 In X5's details the Admin sees a checkbox per listed course and a button «Підтвердити:
видалені в Classroom». The Dean sees neither.

FR-006.2 Posting the confirmation (Admin only; a Dean is refused 403 — TC-5):
1. the read-only guard runs first; in read-only mode the request is refused by the existing read-only
   refusal (409) and nothing changes (AD-6, BR-026);
2. the request is validated (VR-001…VR-003);
3. in one transaction, every selected course must still carry `AbsenceMark =
   NotVisibleToTechnicalAccount`; if any does not, nothing changes and the Admin sees «Список змінився
   — оновіть сторінку» (interpretation I-5);
4. each selected course gets `AbsenceMark = NotInClassroom` and `AbsentSince` = the instant of the
   confirmation (a new domain operation: `MarkAbsent` refuses an already-marked course);
5. one audit event (SC-11) is added in the same transaction: actor the Admin, the internal ids of the
   confirmed courses, no names (§5, v101);
6. the Admin is returned to «Стан даних» with a one-line confirmation of how many courses were marked.

FR-006.3 From then on those courses follow BR-048: synchronization keeps the mark while the course is
absent from the list (it already keeps a set mark) and removes it when the course is listed again.
Unless archived, each now counts as a teaching problem T4; X5 disappears when no course is left in it.

FR-006.4 Unselected courses keep `NotVisibleToTechnicalAccount`.

### FR-007 Texts

All texts come from the translation files, in Ukrainian and English (NFR-073); names, titles and
student names are shown as Google returned them, never translated. Keys follow the dotted convention
(e.g. `DataHealth.Banner.Technical.One`). The Ukrainian wording, translated from `trebovaniya.md`:

| Text | Ukrainian | English |
|---|---|---|
| Page title | Стан даних | Data health |
| T1 | Бали вище максимуму | Points above the maximum |
| T2 | Завдання або матеріали, яких немає в Classroom | Items not in Classroom |
| T3 | Курс не оновлюється з <дата> | The course has not been updated since <date> |
| T4 | Курсу немає в Classroom з <дата> | The course is not in Classroom since <date> |
| H1 (T3) | курс не оновлюється: у Classroom введено неприпустиме значення балів, зверніться до викладача | the course is not updated: an invalid points value was entered in Classroom, contact the teacher |
| H2 (T4) | курс видалено або приховано в Classroom, уточніть у викладача | the course was deleted or hidden in Classroom, check with the teacher |
| X1 | Остання синхронізація завершилася помилкою | The last synchronization failed |
| X2 | Успішної синхронізації не було понад 24 години / Успішної синхронізації ще не було | No successful synchronization for more than 24 hours / No successful synchronization yet |
| X3 | Курс пропущено: стан курсу, якого програма не знає | Course skipped: a course state the program does not know |
| X4 | Google не повертає жодного курсу з <дата> — перевірте роль технічного акаунта | Google has returned no course since <date> — check the technical account's role |
| X5 | Технічний акаунт не бачить курсів, які бачив попередній | The technical account does not see courses the previous one saw |
| X6 | Немає зв'язку із сервісом з <дата>; з <дата> програма перейде в режим «лише для читання», якщо зв'язок не відновиться | No connection with the service since <date>; from <date> the program will switch to read-only mode unless the connection is restored |
| H3 | Повідомте Адміністратора | Tell the Admin |
| H4 | Якщо з викладачем вирішити не вдалося, зверніться до Адміністратора | If it could not be solved with the teacher, contact the Admin |
| H5 | Повідомте Власника | Tell the Owner |

The read-only mode's name is the installation's existing one (`Landing.ReadOnly.Title`: «Режим лише
для читання» / «Read-only mode»).

### FR-008 Empty course list date

FR-008.1 `SyncState` stores the instant since which course lists have come back empty in a row
(nullable). A run that reads an empty list while courses are stored sets it to its own `StartedAt` if it
is not set, and keeps it if it is; a run that reads a non-empty list clears it. The existing
`LastCourseListEmpty` flag keeps its meaning and stays (NFR-033 forbids dropping a column in use without
an approved decision); the two are kept consistent (flag true exactly when the date is set).

FR-008.2 The migration (PC-2) sets the date of an existing row with `LastCourseListEmpty = true` to
that row's `StartedAt` — the start of the last run (OD-002 a).

FR-008.3 X4 (FR-001.3) reads the date. A run that reads an empty list counts as successful (BR-048), so
X2 is not raised while such runs keep coming.

## 5. Acceptance Criteria

The Story's AC-001…AC-014 are adopted unchanged, with these readings:

| AC | Reading |
|---|---|
| AC-001 | Problems: T1 and T2 on course A (two), T4 on course B (one) — 3 teaching; X1 — 1 technical; the fixture's last successful run is within 24 hours so X2 is absent. Ukrainian banner «1 технічна проблема, 3 навчальні». |
| AC-002 | No condition of FR-001 holds; the layout has no banner element. |
| AC-003 | Archived course with T1 only: no banner; the page's archived section lists it with the grade's details. |
| AC-004 | A run stores the corrected grade (≤ max): T1 gone from banner and page. |
| AC-005 | T1, T2, T3 (loaded, with `LastCompleteReadAt`), T3 (never loaded, by `CourseName`), T4; journal links for the loaded courses only; H1, H2 and H4 shown to the Dean. |
| AC-006 | X1 with `ScopeNotAuthorized`, X3: the Dean sees two titles with H3 and no diagnosis or course name; the Admin sees the diagnosis text and the course name. |
| AC-007 | X2 at now − 25 h; "none yet" without `LastSuccessfulRunAt`; neither in read-only mode. |
| AC-008 | Three empty runs from E1: X4 with E1, no X2; after a non-empty run X4 is gone. |
| AC-009 | X5 counts once (banner technical = 1 from X5); the Admin's details list the three courses; the Dean sees X5's title with H3. |
| AC-010 | After confirming two: both `NotInClassroom` with the confirmation instant; one audit event with their two ids; the third stays; the two count as T4 unless archived. |
| AC-011 | Dean POST → 403; Admin POST in read-only → 409; marks unchanged. |
| AC-012 | English UI in read-only mode: banner and page in English; course, item and student names as stored. |
| AC-013 | At C1 + 25 h: X6, banner technical 1; dates C1 and C1 + 7 days; Dean H3, Admin H5; at C1 + 23 h no X6. |
| AC-014 | Past the grace period: no X6; the read-only message on the page; with `Compatibility = UpgradeRequired` during the grace period the Admin's X6 details name the reason. |

Added by this Specification:

- **AC-015 (FR-003.1, OD-001 a)** A Dean with a temporary password, on the forced password change page,
  sees no banner while problems exist.
- **AC-016 (FR-008.1, FR-008.2)** The migration gives an existing `LastCourseListEmpty = true` row the
  date of its `StartedAt`; a later empty run keeps the date; a non-empty run clears both.
- **AC-017 (FR-006.2 step 3, VR-002)** A confirmation naming a course no longer marked "not visible"
  (or an unknown id) changes nothing and writes no audit event.

## 6. Validation Rules

- **VR-001** The confirmation carries a list of course ids: at least one, at most 1000 after removing
  duplicates (interpretation I-6); each a positive 64-bit integer. Otherwise 400, nothing changes.
- **VR-002** Every id must name an existing course with `AbsenceMark = NotVisibleToTechnicalAccount` at
  the moment of the transaction; otherwise the whole request is refused (FR-006.2 step 3), nothing
  changes.
- **VR-003** The form carries the antiforgery token (SC-4); a missing or wrong token is refused before
  any processing.
- **VR-004** Values read from storage for display are shown as stored; a `LastError` that names no known
  `SyncDiagnosis` is shown with the `Unexpected` text.
- The rejected payload is never logged (SC-10).

## 7. Security Requirements

- **Authorization (SC-4, TC-5):** the page and the banner — Admin and Dean (§2 row 749); the
  confirmation — Admin only (§2 row 750). Allowed- and forbidden-role tests for both. Anonymous users
  get neither.
- **Detail restriction (§2 notes 764–766):** technical details (diagnosis, course lists of X3/X5, the
  upgrade reason) are rendered for the Admin only; the Dean's page contains none of them. Enforced in
  the Application query result for the Dean, not by hiding markup (AD-6 spirit, AD-8 DTOs).
- **Read-only (SC-5, AD-6):** the confirmation calls `IReadOnlyModeGuard` before any read or write; the
  page and banner are views and work in read-only mode.
- **Audit (SC-11):** the confirmation writes one audit event with the courses' internal ids, no personal
  data; never updated.
- **No Google call, no new outbound flow (SC-8, SC-13).**
- **Personal data:** the page shows student names and grades — teaching data already visible to both
  roles in the journal. Logs carry no names, grades or titles (SC-10, NFR-023). Responses are not
  cached (SC-14, as for every page).

## 8. Error Handling

- Confirmation by a Dean → 403; in read-only mode → the existing read-only refusal (409 page);
  invalid input → 400 with a message on the page; list changed (VR-002) → 409-class refusal with
  «Список змінився — оновіть сторінку», nothing changed. Exact status codes and bodies are fixed by
  API_DESIGN.
- A failure while computing the banner never breaks the page it decorates: the page renders without
  the banner and the failure is logged without personal data (interpretation I-4). The «Стан даних» page
  itself reports a failure through the normal error page.
- A concurrent synchronization changing a course's mark between the page load and the confirmation is
  covered by VR-002 inside the transaction; how the transaction excludes a concurrent change is a
  DB_DESIGN decision.

## 9. Non-Functional Requirements

- The banner is computed on every page request: counts are computed in the database (aggregate /
  existence queries), never by loading submissions into memory; the design fixes the queries and any
  index they need. No cache that would show a problem after its cause is gone, beyond the request.
- UI in Ukrainian and English (NFR-073); school time zone (NFR-074).
- Tests use synthetic data and a substituted clock (TC-4); integration tests on PostgreSQL via
  Testcontainers (TC-2).
- Schema change only through a migration (PC-2), backward compatible (NFR-033).

## 10. Out of Scope

- Marks on the course list, card, journal, report and export (US-060) and in Meet reports (US-061).
- Narrowing problems to a Dean's courses (dean scope, later edition).
- A history of problems (log and audit hold it).
- Notifications outside the installation (e-mail etc.).
- Undoing the Admin's confirmation (the mark is removed only when the course returns to the list).
- Changing synchronization beyond the empty-list date (FR-008).

## 11. Open Decisions and Interpretations

### Open Decisions

| Id | Subject | Status |
|---|---|---|
| OD-001 | Pages with the banner | Resolved a) — all signed-in pages except the forced password change (FR-003.1) |
| OD-002 | Empty-list date of an upgraded installation | Resolved a) — the last run's start (FR-008.2) |

`trebovaniya.md` §7 holds no open item this Story depends on (items 14, 26–29 concern documentation,
re-loading a purged leaver, Meet fields and delegation with 2SV).

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 (FR-001.3 X1)** "The last run failed" is `SyncState.Status = Failed`. Starting a run clears
  the stored error, so while the next run is in progress X1 is not shown; it returns if that run fails.
- **I-2 (FR-002.3)** For the Dean a technical problem is its title line without details; X3's course
  name and X5's course list count as details. X6's title keeps its two dates for the Dean, as AC-013
  says the page shows them, and the read-only date matters to the Dean's work.
- **I-3 (FR-004.5)** "The read-only message replaces it" is read as: «Стан даних» shows the existing
  read-only message at its top; no new read-only banner is added to every page.
- **I-4 (§8)** A failure computing the banner leaves the page without the banner rather than failing
  the page.
- **I-5 (FR-006.2)** The confirmation is all-or-nothing: if any selected course is no longer "not
  visible", nothing is confirmed and the Admin reloads.
- **I-6 (VR-001)** At most 1000 course ids per confirmation — above any school's course count, a bound
  for input validation.
- **I-7 (FR-005)** NFR-075 lists the pages with teaching data that existed then; «Стан даних» shows
  teaching data, so it gets the ordinary page-search hint.
- **I-8 (FR-004.3)** H4 (contact the Admin) is shown to the Dean only — the Admin is the one it points
  to.
- **I-9 (FR-001.3 X6)** `upgrade_required` raises no problem of its own before 24 hours: it is a cause
  of X6 and appears in X6's Admin details (BR-027 says it consumes the grace period).
- **I-10 (FR-001.2 T1)** Draft grades above the maximum count (BR-067 names "a grade or draft grade");
  grades of items "not in Classroom" and of students off the roster count too — they are stored data.
- **I-11 (FR-008.1)** The empty-list flag stays alongside the new date (NFR-033); the date of a streak
  is the start of its first empty run.

## 12. Traceability

| AC | FR | VR | Source |
|---|---|---|---|
| AC-001 | FR-001.2, FR-001.3, FR-001.5, FR-003 | — | BR-047, §4 v99 |
| AC-002 | FR-003.1 | — | BR-047 |
| AC-003 | FR-001.5, FR-004.4 | — | BR-047 archived |
| AC-004 | FR-001.4 | — | BR-047 current state |
| AC-005 | FR-001.2, FR-001.6, FR-004.3 | — | BR-043, BR-045, BR-046, BR-048, v100, I-10 |
| AC-006 | FR-001.3 X1/X3, FR-002.3 | VR-004 | §2 notes 764–766, Epic 6, I-1, I-2 |
| AC-007 | FR-001.3 X2 | — | BR-047 |
| AC-008 | FR-001.3 X4, FR-008 | — | BR-048 v102, I-11 |
| AC-009 | FR-001.3 X5, FR-001.5, FR-002.3 | — | BR-049 v101 |
| AC-010 | FR-006 | VR-001, VR-002 | BR-049, SC-11 |
| AC-011 | FR-006.2 | VR-003 | §2 row 750, BR-026, TC-5 |
| AC-012 | FR-003.4, FR-004, FR-007 | — | NFR-073, BR-047 |
| AC-013 | FR-001.3 X6, FR-002.3 | — | BR-047 v103, BR-081, I-2 |
| AC-014 | FR-001.3 X6, FR-004.5 | — | BR-025, BR-027, I-3, I-9 |
| AC-015 | FR-003.1 | — | OD-001 a |
| AC-016 | FR-008 | — | OD-002 a, PC-2 |
| AC-017 | FR-006.2 | VR-002 | I-5 |
