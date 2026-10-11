---
artifact_type: ac_test_matrix
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T20:15:00Z
updated_at: 2026-10-10T20:15:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/designs/database/US-033-db-design.md
    version: 1
  - path: docs/tests/US-033-test-strategy.md
    version: 1
supersedes: null
---

# US-033 Acceptance Criteria → Test Matrix

Unit classes live in `tests/ClassroomAgent.Tests/Application/UseCases/`. Host
classes live in `Web/…`, and the port class in `Infrastructure/Persistence/`.
"Q" means `GetMeetReportsQuery` over `FakeMeetReportSource`, Kyiv,
`ManualTimeProvider(MeetReportsTestData.Now)` and the October period. "Q4"
means `GetMeetReportMeetingQuery`. Status is the red-phase state; the
generation report gives the evidence.

| AC | Scenario | Level | Test class | Expected result |
|---|---|---|---|---|
| AC-001 | no `courseId` → form only, default period 2026-10-01…2026-10-31 from `Now` in Kyiv, `Reports` null, courses listed (archived ones too — the fake has no archive filter, the port test proves R-none filters nothing) | Unit | `MeetReportsRequestValidationTests` | `Shown`, `From`/`To` = October |
| AC-001 | one absent date filled independently (only `from` given → `to` = last day of current month) | Unit | `MeetReportsRequestValidationTests` | as stated |
| AC-001 | no course stored → `NoCoursesStored` true | Unit | `MeetReportsRequestValidationTests` | true, `Reports` null |
| AC-001 | page with a course renders form, notice, reports 1–3 in that order; home page has the entry for Admin and Dean | Host | `MeetReportsPageTests` | `200`, order of sections, link `/workspace/meet-reports` |
| AC-002 | codes A (auto, unconfirmed) and B (confirmed) linked to the course, code C marked "not a course" with the same organizer, code D unlinked → only A and B meetings | Unit | `MeetReportsMeetingsOfCourseTests` | `Meetings` = A+B ids |
| AC-002 | same through PostgreSQL: R1 returns A and B meetings, not C (link with null course) or D | Integration | `MeetReportSourceTests` | as stated |
| AC-003 | start 23:30 Kyiv on 2026-10-31 ending 00:40 next day → in; start 00:10 Kyiv on 2026-11-01 → out | Unit | `MeetReportsMeetingsOfCourseTests` | first in, wholly (its full duration counts in summary); second out |
| AC-018 | start 00:30 Kyiv 2026-11-01 (= 2026-10-31 22:30 UTC) → out; start 00:30 Kyiv 2026-10-01 (= 2026-09-30 21:30 UTC) → in | Unit | `MeetReportsMeetingsOfCourseTests` | as stated; `Bounds` = `[StartUtc, EndUtc)` |
| AC-018 | R1 half-open bounds on PostgreSQL: start = `start` in, start = `endExclusive` out | Integration | `MeetReportSourceTests` | as stated |
| AC-004 | three meetings of 45, 30, 60 min with 2, 0, 3 students → count 3, total 8100 s, average 2700 s, average students 1.7 (rounded to one decimal = 5/3) | Unit | `MeetReportsSummaryTests` | values as stated; summary derives from the same rows as report 2 |
| AC-005 | meeting: organizer (teacher), other teacher, 2 students, personal account not on this roster (member of another course), class account (domain email with no membership), external guest (null email) → teachers 1, students 2, not-on-list 1, other 2; organizer named with full name and email, counted nowhere | Unit | `MeetReportsCountingTests` | as stated |
| AC-005 | a person connecting 3 times counts once; an email differing only in case matches the roster | Unit | `MeetReportsCountingTests` | counts unchanged by repeats/case |
| AC-005 | I-8: one guest connection repeated twice with null email → other = 2; one class account connecting twice → other = 1 | Unit | `MeetReportsCountingTests` | as stated |
| AC-005 | I-4: a person who is both teacher and student on D counts as teacher only; the organizer who is also a student counts nowhere | Unit | `MeetReportsCountingTests` | as stated |
| AC-006 | student joins after meeting X (FirstSeenAt after X's date), on roster at Y → X: not-on-list 1, students 0; Y: students 1 | Unit | `MeetReportsCountingTests` | as stated |
| AC-006 | student left (OnRoster false, LastSeenAt before meeting date) → not on list at that meeting | Unit | `MeetReportsCountingTests` | as stated |
| AC-007 | organizer not a teacher of the course on D (teacher of another course) → `OrganizerNotCourseTeacher` true; a teacher organizer → false | Unit | `MeetReportsCountingTests`, `MeetReportMeetingQueryTests` | as stated in report 2 and report 4 |
| AC-008 | students S1 (2 meetings), S2 (none), S3 (left mid-period, attended one meeting before and one after leaving) → S1: 2; S2: 0, 0; S3: 1 with only the on-roster meeting's seconds | Unit | `MeetReportsStudentsTests` | as stated |
| AC-008 | a student whose membership ended before the period, and a teacher, are not rows | Unit | `MeetReportsStudentsTests` | absent |
| AC-008 | I-10 order: Ukrainian collation (names Ярослав, Іван, Євген, Андрій) and English; tie on name broken by email | Unit | `MeetReportsStudentsTests` | ordered |
| AC-008 | OD-012: a student without an email is a row with `Email` null, zeros; without email and name → `PersonRef(null, null)` and the page shows `MeetReports.Person.Unnamed` | Unit + Host | `MeetReportsStudentsTests`, `MeetReportsPageTests` | as stated |
| AC-009 | 45-min meeting, one student on two devices 40 min each → report 4 `Seconds` 2700; report 3 adds 2700 (not 4800) | Unit | `MeetReportsMinutesTests` | as stated |
| AC-009 | I-5: two meetings where a student has 30 s each → report 3 `TotalSeconds` 60 (rendered "1"); a single 90 s → rendered "2" (half up) | Unit + Host | `MeetReportsMinutesTests`, `MeetReportMeetingPageTests` | seconds in DTO; text on the page |
| AC-009 | page shows "45 of 45 min" (en) for the capped person | Host | `MeetReportMeetingPageTests` | text present |
| AC-010 | report 4 lines: organizer first (if connected), then teachers, students, not-on-list, each by name; marks per FR-005; other participants one line with count and capped total, no email of the class account in the model or HTML | Unit + Host | `MeetReportMeetingQueryTests`, `MeetReportMeetingPageTests` | as stated |
| AC-010 | I-3: organizer with no connection → named in header, no line in `People` | Unit | `MeetReportMeetingQueryTests` | as stated |
| AC-010 | report 4 opens for a meeting of the course outside the given period (I-1); back link keeps valid `from`/`to`, drops invalid ones | Unit | `MeetReportMeetingQueryTests` | `Shown`; `BackLink` as stated |
| AC-010 | each row of report 2 links to `/workspace/meet-reports/meetings/{id}?courseId=…&from=…&to=…` | Host | `MeetReportsPageTests` | link present |
| AC-011 | watermark set → `MeetLoadedUpTo` = local Kyiv date-time; no sync state / null watermark → null; page shows `DelayNotice` text and either the time or `NotLoaded` | Unit + Host | `MeetReportsDelayNoticeTests`, `MeetReportsPageTests` | as stated |
| AC-012 | no meeting → summary all zero, `Meetings` empty, students listed with zeros; page shows `Empty.NoMeetings`; no student → page shows `Empty.NoStudents` | Unit + Host | `MeetReportsSummaryTests`, `MeetReportsPageTests` | as stated, `200` |
| AC-013 | `courseId` malformed (`abc`, `-1`, `0`, `1 `, overflow, repeated) → `Invalid`, `CourseMalformed`, source not called; unknown id → `CourseUnknown`; `from`/`to` malformed (`2026-13-01`, `2026-02-30`, `1999-12-31`, `2101-01-01`, `01.10.2026`, repeated) → `FromMalformed`/`ToMalformed`; inverted → `PeriodInverted`; one-day period valid; year 2000 and 2100 valid | Unit | `MeetReportsRequestValidationTests` | as stated |
| AC-013 | report 4: `courseId` absent / malformed → `Invalid`, `CourseMalformed`; unknown course, unknown meeting, meeting of another course, `meetingId` `0` or overflow → `NotFound`, `MeetingNotFound` | Unit | `MeetReportMeetingQueryTests` | as stated |
| AC-013 | host: `400` / `404` re-render the page (not the error page); non-digit `meetingId` → catch-all `404`; the rejected value is not in the HTML and not in the log | Host | `MeetReportsPageTests`, `MeetReportMeetingPageTests`, `MeetReportsLoggingTests` | as stated |
| AC-014 | Admin `200` and Dean `200` on both paths; anonymous `302 /sign-in`; Dean with temporary password `302 /sign-in/change-password`; enumeration test lists both paths as protected | Security | `MeetReportsAuthorizationTests` | as stated |
| AC-015 | in each BR-025 cause (suspended, grace expired, never legitimated) both pages answer `200` with the same content | Host | `MeetReportsPageTests` | as stated |
| AC-015 | a page request writes nothing (row counts of `audit_event`, `sync_state`, `meeting_code_link` unchanged) | Host | `MeetReportsPageTests` | unchanged |
| AC-016 | every key in `MeetReportsTestData.Keys.All` exists in uk and en, non-empty, different; uk `MeetReports.Students.Title` = «Участь студентів»; course name, person name, email and meeting code shown as stored in both languages; average students `1,7` in uk and `1.7` in en | Localization | `MeetReportsTranslationTests` | as stated |
| AC-017 | no Google port is called by either page (substituted readers record no call) | Host | `MeetReportsPageTests` | zero calls |
| R1–R6 | `MeetReportSource`: R4 returns members of all roles with email/full name; R5 lower-case matching, tie rule (lowest id with a name), personal flag true when any duplicate has a membership; R6 name / null; R3 null for another course's meeting and for a marked code | Integration | `MeetReportSourceTests` | as stated |
| SC-10 | built page logs course id and counts only — no email, name, meeting code | Logging | `MeetReportsLoggingTests` | as stated |
