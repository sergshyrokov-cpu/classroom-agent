---
artifact_type: specification
story: US-061
version: 1
status: APPROVED
created_at: 2026-10-31T14:13:00Z
updated_at: 2026-10-31T14:30:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
supersedes: null
---

# US-061 Specification — Meet reports of a marked course, and a marked course is not a Meet link candidate

Delegation: one read-only lookup collected the code facts in §1.1 (scoring, candidate list, manual
choice, reports, exports, the US-060 course notice). The spec writer read `trebovaniya.md` §4
Epic 4 (lines 1379–1467), the v102…v104 changelog, §7, BR-062 and BR-065 in
`docs/product/business-rules.md`, and the approved US-060 Specification and Open Decisions, whose
wording this Story aligns with (Story OD-001).

## 1. Overview

US-059 marks a course "not in Classroom" (BR-048) or "not visible to the technical account"
(BR-049); such a course is no longer read, so its roster stays as it was on its last successful
read. US-060 shows the marks on the course screens, journal and report. This Story covers the Meet
side (v102, Epic 4):

1. Meet reports 1–4 of a marked course, on screen and in their export, show the course's mark with
   its date and "course membership as of <last successful read>" (BR-062).
2. A marked course is neither linked automatically nor offered as a candidate for an unlinked code;
   a Dean or Admin may still pick it by hand, and existing links to it stay (BR-065).

Nothing is read from Google, no schema changes, no endpoint is added.

### 1.1 Code facts the Specification must not contradict

- `Course.AbsenceMark` (`CourseAbsenceMark?`: `NotInClassroom`, `NotVisibleToTechnicalAccount`) and
  `AbsentSince` (`DateTimeOffset?`) are null together; `LastCompleteReadAt` is `DateTimeOffset?`
  (null for a course not read completely since US-058). No Meet code reads `AbsenceMark` today.
- **Scoring.** `MeetCodeScoringSource.GetUnassignedCodesAsync` returns only codes with no
  `meeting_code_link` row (no automatic link, no person's link, no "not a course" mark).
  `GetScoringInputsAsync` loads the memberships of every course, with no course filter.
  `MeetCodeScorer.Score` makes a course a candidate when an organizer was its teacher on the
  meeting's date and computes each candidate's share (numerator / denominator, organizers excluded);
  `MeetCodeScorer.Decide` links when the best share ≥ `MinSharePercent` (60) and best − next ≥
  `MinGapPoints` (30) points, `next` = 0 with a single candidate. `LinkMeetCodesStep` (batch 200)
  writes `MeetingCodeLink.LinkAutomatically` and the audit event `meet_code_auto_linked`; it runs
  inside synchronization and so under its read-only guard.
- **Candidate list.** `GetMeetCodesQuery` reuses the scoring source and `MeetCodeScorer.Score`, then
  `Candidates(score, courses)` keeps candidates present in `GetAllCoursesAsync` (all courses,
  unfiltered) and maps them to `CandidateCourse(CourseOption, SharePercent)`. View
  `Views/MeetCodes/Index.cshtml`.
- **Manual choice.** `GetMeetCodeCourseChoiceQuery` returns `Candidates` (the list above minus the
  current course) and `OtherCourses` (all other stored courses, ordered by name then id), plus
  `CurrentCourse`. View `Views/MeetCodes/CourseChoice.cshtml`. `SetMeetCodeCourseUseCase` runs the
  read-only guard first, accepts any existing course, and records a person's link
  (`LinkedAutomatically = false`) with audit `meet_code_course_picked` (unassigned code),
  `meet_code_relinked` (linked code) or `meet_code_mark_removed` (code marked "not a course").
- **Reports.** `GetMeetReportsQuery` (reports 1–3, page `Views/MeetReports/Index.cshtml`) already
  loads the course list as `JournalCourseRecord`s (which carry `AbsenceMark`, `AbsentSince`,
  `LastCompleteReadAt`, `Skipped`) and uses only Id, Name, Section; its `MeetReports` DTO has the
  `MeetDelayNotice` and no course notice. `GetMeetReportMeetingQuery` (report 4, page
  `Views/MeetReports/Meeting.cshtml`) returns `MeetingDetail` with `CourseName` only. Reports read
  meetings of codes linked to the course (`l.CourseId == courseId`).
- **Exports.** `ExportMeetReportsCommand` → `MeetReportsWorkbookMapper`: on each of the three sheets
  the header rows are Course | name; Period | from | to; Exported; Delay row; empty; table at row
  index 5. `ExportMeetMeetingCommand` → `MeetMeetingWorkbookMapper`: Course | name; Meeting; Duration;
  Meeting code; Organizer; Exported; Delay row; empty; table at row index 8. Texts come through
  `IReportTexts`. Neither command has a read-only guard; the only write is its audit event under
  `PermittedServiceWrite.AuditEvent`.
- **US-060 notice.** `DataMarkRules.Notice(JournalCourseRecord, zone)` builds the journal/report
  header (`CourseDataNotice`: `NotInClassroom(date)` or `NotUpdated(date?)` — the latter for
  "not visible" **and** for a skipped course). Course-list mark texts are
  `DataMarks.Course.NotInClassroomSince` «немає в Classroom з {0}» and
  `DataMarks.Course.NotVisibleSince` «не видно технічному акаунту з {0}»; the header text
  `DataMarks.Header.NotInClassroomSince` is «Курсу немає в Classroom з {0}».
- Time zone: `SchoolTimeZone`; dates are converted with `TimeZoneInfo.ConvertTime`.

## 2. Business Goal

A marked course's roster is frozen at its last read: students who joined later count as "not on the
course list", students who left still count as its students. The Dean must not read such a Meet
report as current (BR-062). A course re-created in place of a deleted one has the same teacher and
students as the marked old one; both would score the same share and the new code would never link
itself — every meeting would wait in "unlinked meetings" (BR-065).

## 3. Business Flow

1. Synchronization (unchanged) marks courses (US-059) and, in its Meet part, scores and links
   unlinked codes — now with marked courses left out of the candidates.
2. The Dean or Admin opens "unlinked meetings": marked courses are not offered as candidates. On the
   course-choice page a marked course can still be chosen, and its mark is shown next to its name.
3. The Dean or Admin opens or exports the Meet reports of a course; a marked course's reports carry
   the mark and the roster date.

Admin and Dean see the same (BR-002). Read-only mode: viewing and export work; linking is blocked
as today (BR-026, BR-065).

## 4. Functional Requirements

Dates in the texts below are calendar dates in the school's time zone (BR-053), formatted with the
user's culture short date pattern, as dates elsewhere on the same page or sheet. A course is
**marked** when its `AbsenceMark` is set (either value); the mark is read as it is stored at the
moment of the query or the Meet load.

### FR-001 A marked course is not a candidate for automatic linking

1. When scoring an unlinked code, marked courses are removed from the candidates **before** the
   decision. The shares of the remaining candidates are computed exactly as before (same numerator
   and denominator — BR-065); only the set of candidates changes.
2. The decision rule and thresholds are unchanged (60 % / 30 points, configuration keys unchanged).
   With one remaining candidate, `next` is 0, as today.
3. A code that becomes unambiguous without the marked course is linked automatically at the next
   Meet load, as any unlinked code is (actor `system`, audit `meet_code_auto_linked`, unchanged).
4. Only unlinked codes are scored, as today: a code already linked (automatically or by a person)
   or marked "not a course" is not re-examined when its course gets a mark (FR-004).
5. A course whose mark is cleared by synchronization (it reappeared) is a candidate again from the
   next scoring on (I-3).

### FR-002 A marked course is not offered as a candidate in "unlinked meetings"

1. The candidate list of each unlinked code (`Views/MeetCodes/Index.cshtml`) shows only unmarked
   candidates, with their shares as before and in the same order.
2. A code whose only candidates are marked courses stays in the list with no candidate; the Dean or
   Admin picks the course by hand (FR-003).
3. The "Candidates" section of the course-choice page shows the same candidates (FR-002.1) minus the
   current course, as today.

### FR-003 A marked course can still be chosen by hand, and shows its mark there (OD-002 a)

1. On the course-choice page a marked course appears among the other courses (it is never a
   candidate, FR-002.3) and can be chosen like any course.
2. Next to the name of a marked course — among the other courses, and as the current course when the
   code is linked to it — the page shows its mark with its date: «немає в Classroom з <AbsentSince>»
   or «не видно технічному акаунту з <AbsentSince>» (the course-list texts, FR-006). Nothing else on
   the page changes: order, sections, form and messages stay as they are.
3. Choosing a marked course records a person's link exactly as choosing any course: same use case,
   same read-only guard, same audit event by the code's state (`meet_code_course_picked`,
   `meet_code_relinked`, `meet_code_mark_removed`).

### FR-004 Existing links to a marked course stay

1. A link to a course that later becomes marked — automatic or by a person — is not changed, removed
   or reconsidered by the system; its meetings stay in that course's reports (BR-065, v102).
2. Confirming such an automatic link, re-linking the code or marking it "not a course" work as
   today.

### FR-005 Meet reports of a marked course carry the mark and the roster date

1. For a marked course, the reports page (reports 1–3) and the meeting page (report 4) show one
   notice line made of two parts:
   - the mark: `NotInClassroom` → «Курсу немає в Classroom з <AbsentSince>»;
     `NotVisibleToTechnicalAccount` → «Курс не видно технічному акаунту з <AbsentSince>»;
   - the roster: «склад курсу — на <LastCompleteReadAt>»; when `LastCompleteReadAt` is null —
     «склад курсу — на дату останнього читання» with no date (OD-001 a).

   The two parts are joined by «; » into one line.
2. On the reports page the line is shown with the course's reports, next to the existing delay
   notice; on the meeting page, under the course name. Neither page has the line for an unmarked
   course.
3. **Export.** In the reports workbook, the line is written as text in the third cell of the Course
   header row on each of the three sheets; in the meeting workbook, the third cell of its Course
   header row. No row is added: the tables stay at row index 5 and 8. An unmarked course leaves the
   cell empty.
4. Figures, rows, pupils' roles ("course student", "not on the course list", "course teacher") and
   the delay notice are computed and shown exactly as before. The line warns; it changes nothing.
5. A course that is only skipped by synchronization (a `SyncSkippedCourse` record, no mark) gets no
   Meet line: BR-062 names only the two marks (I-1).
6. The line is built in `Application` and travels in the reports' DTOs (AD-8); the Razor views and
   workbook mappers only render it.

### FR-006 Texts

1. Every text comes from `SharedResource.uk.resx` and `.en.resx` (NFR-073); exports through
   `IReportTexts`:

   | Meaning | uk (proposed) | en |
   |---|---|---|
   | choice page, not in Classroom (existing key) | «немає в Classroom з {0}» | "not in Classroom since {0}" |
   | choice page, not visible (existing key) | «не видно технічному акаунту з {0}» | "not visible to the technical account since {0}" |
   | Meet line, not in Classroom (existing header text) | «Курсу немає в Classroom з {0}» | "Course not in Classroom since {0}" |
   | Meet line, not visible | «Курс не видно технічному акаунту з {0}» | "Course not visible to the technical account since {0}" |
   | Meet line, roster with date | «склад курсу — на {0}» | "course membership as of {0}" |
   | Meet line, roster without date | «склад курсу — на дату останнього читання» | "course membership as of the last read" |
   | separator | «; » | "; " |

   The existing keys are reused where the meaning is the same (I-4). The requirements word the texts
   in Russian; the Ukrainian texts above render the same meaning and are the ones the tests pin.
2. Course and person names are never translated.
3. The language is the current user's on screen and `CultureInfo.CurrentUICulture` in the export,
   as today.

## 5. Acceptance Criteria

From the Story, unchanged in meaning; derived criteria are marked.

| AC | Given / When / Then (summary) | FR |
|---|---|---|
| AC-001 | A course `NotInClassroom` since D1, last read R1, with linked meetings. Reports 1–4 on screen and both exports show «Курсу немає в Classroom з D1» and «склад курсу — на R1»; figures as before. | FR-005, FR-006 |
| AC-002 | A course `NotVisibleToTechnicalAccount` since D2, last read R2. Reports and exports show «Курс не видно технічному акаунту з D2» and «склад курсу — на R2». | FR-005, FR-006 |
| AC-003 | An ordinary course with meetings. Neither text on screen or in the exports (the export cell empty). | FR-005.2, FR-005.3 |
| AC-004 | An old course marked `NotInClassroom`, a new course with the same teacher and students, an unlinked code whose meetings that teacher organized with those students. Meet load links the code automatically to the new course (audit `meet_code_auto_linked`). | FR-001 |
| AC-005 | An unlinked code whose candidates by share are a marked course and two unmarked courses with close shares. "Unlinked meetings" lists the code with the two unmarked candidates and without the marked one. | FR-002 |
| AC-006 | An unlinked code and a marked course. The Dean chooses the marked course: the code is linked to it as a person's link, audit `meet_code_course_picked`. | FR-003.1, FR-003.3 |
| AC-007 | A code linked automatically to a course that later becomes marked. After another Meet load the code stays linked to it. | FR-001.4, FR-004 |
| AC-008 | Data of AC-001, read-only mode, a user with English. Reports and exports work and show "Course not in Classroom since D1" and "course membership as of R1". | FR-006, §7 |
| AC-009 *(derived, OD-001 a)* | A marked course with no `LastCompleteReadAt`. The line shows the mark and «склад курсу — на дату останнього читання» with no date. | FR-005.1 |
| AC-010 *(derived, OD-002 a)* | The course-choice page of an unlinked code; a marked and an unmarked course among the other courses. The marked one shows «немає в Classroom з D» next to its name; the unmarked one shows nothing; both can be chosen. | FR-003.2 |
| AC-011 *(derived)* | A course only skipped (a `SyncSkippedCourse` record, no mark) with meetings. No Meet line. | FR-005.5 |

## 6. Validation Rules

No new input: no endpoint, query parameter, form field or upload changes. The existing validation of
the course-choice form and the report period is unchanged.

- **VR-001 Marked course:** `AbsenceMark` is not null (either value). The skip record and the
  course's `State` (active, archived…) do not make a course marked.
- **VR-002 Candidate set:** candidates = courses that pass the existing teacher-on-date rule
  (BR-065) **and** are not marked; shares are computed over the same denominator as before.
- **VR-003 Dates:** UTC instants (`AbsentSince`, `LastCompleteReadAt`) converted to the school's
  time zone, then shown as dates.
- **VR-004 Line composition:** exactly one line or none; for a marked course both parts are always
  present (the roster part dated or undated, OD-001 a).

## 7. Security Requirements

- No endpoint, page, role, policy or permission changes; Admin and Dean see and do the same
  (BR-002, matrix §2).
- **Read-only mode:** reports and exports keep working and show the line (BR-026). The automatic
  link runs only inside synchronization, which does not run in read-only mode; the manual choice
  keeps its existing guard and refusal audit. This Story adds no write and no guard call.
- **Google:** no call; everything is built from stored data (SC-7; TC-4 for tests).
- **Personal data:** the line adds two dates about a course; no email, name, Google id or new field
  leaves the system. No log line gains a name or date (SC-10).
- **Audit:** no new event; the auto-link, manual-link and export events are unchanged (SC-11).
- **Outbound:** no new destination (SC-13).

## 8. Error Handling

- No new error. A marked course, a missing `LastCompleteReadAt`, or a code left with no candidate is
  a normal result (AD-9).
- Choosing a marked course goes through the existing checks and error messages of the choice form
  unchanged.
- A text missing from a translation file is a defect caught by tests, not a runtime fallback.
- The export's error contract is unchanged.

## 9. Non-Functional Requirements

- **Persistence:** no schema change; DB_DESIGN expected NOT_APPLICABLE.
- **API:** no endpoint change; API_DESIGN expected NOT_APPLICABLE.
- **Cost:** the marked-course set is read once per scoring batch / per page, not per code or per
  course (no N+1); the reports already load the course record that carries the mark.
- **Tone:** restrained — a text line, no icon, colour emphasis, pop-up or banner (BR-047, Owner
  2026-10-23).
- **Architecture:** the candidate filter and the line are computed in `Application`; no Google SDK
  type and no `DbContext` in `Web` (AD-3, AD-4, AD-8).
- **Tests:** synthetic data, PostgreSQL via Testcontainers, no Google call (TC-2, TC-4).

## 10. Out of Scope

- Marks in the course list, card, journal, report and journal export (US-060).
- The banner, «Стан даних» and the Admin's confirmation of deleted courses (US-062).
- Re-examining existing links when a course gets a mark (v102: they stay).
- Any change to synchronization, the stored marks, or the share thresholds.
- A mark in the Meet reports' course selector, in the linked-codes list or in the "not a course"
  list (I-2).
- A Meet line for a course that is only skipped by synchronization (FR-005.5, I-1).

## 11. Open Decisions and Interpretations

### Open Decisions

- **OD-001** (from the Story) — roster date when the last successful read is unknown: **resolved a)**
  2026-10-27. FR-005.1, AC-009.
- **OD-002** (from the Story) — the mark on the manual course-choice page: **resolved a)**
  2026-10-27. FR-003.2, AC-010.

`trebovaniya.md` §7 holds no open item this Story depends on (details in
`docs/decisions/US-061-open-decisions.md`). No new Open Decision.

### Interpretations (confirmed or corrected at HUMAN_SPEC_APPROVAL)

- **I-1 Skipped-only course has no Meet line.** BR-062 names the two marks only; a course skipped by
  one synchronization run is still read next time and its roster is not frozen in the same way. The
  journal's "Data not updated" line for it (US-060) is unchanged (FR-005.5).
- **I-2 Where the mark appears on Meet screens.** Only where the Story and OD-002 put it: the reports
  of the course and the course-choice page. The reports' course selector and the linked-codes and
  "not a course" lists are unchanged (§10).
- **I-3 The mark is current state.** Scoring and the line use the mark as stored at that moment: a
  course whose mark synchronization clears is a candidate again and its reports lose the line
  (FR-001.5).
- **I-4 Reused wording.** The choice page uses the course-list texts of US-060; the "not in
  Classroom" Meet line uses the journal header text of US-060, so the same fact reads the same
  everywhere. "Not visible" gets a new capitalised header text, because the journal says "Data not
  updated" for it and BR-062 asks for the mark itself (FR-006).
- **I-5 Export placement.** The line goes into the third cell of the Course header row, as US-060
  did for the journal export, so no table moves (FR-005.3).
- **I-6 Candidates are removed before the decision.** "Not offered by the automatic link" is read as
  "not a candidate at all": the gap of 30 points is measured to the next **unmarked** candidate, which
  is what lets a re-created course link itself (AC-004).

## 12. Traceability

| AC | FR | VR | Source |
|---|---|---|---|
| AC-001 | FR-005, FR-006 | VR-003, VR-004 | BR-062, Epic 4 v102 |
| AC-002 | FR-005, FR-006 | VR-003, VR-004 | BR-062, BR-049 |
| AC-003 | FR-005.2, FR-005.3 | VR-001 | BR-062 |
| AC-004 | FR-001 | VR-002 | BR-065 v102, I-6 |
| AC-005 | FR-002 | VR-002 | BR-065 v102 |
| AC-006 | FR-003.1, FR-003.3 | — | BR-065 v102 |
| AC-007 | FR-001.4, FR-004 | — | BR-065 v41, v102 |
| AC-008 | FR-006, §7 | — | NFR-073, BR-026 |
| AC-009 | FR-005.1 | VR-004 | OD-001 a |
| AC-010 | FR-003.2 | VR-001 | OD-002 a |
| AC-011 | FR-005.5 | VR-001 | BR-062, I-1 |
