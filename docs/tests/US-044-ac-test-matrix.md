---
artifact_type: ac_test_matrix
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T07:40:00Z
updated_at: 2026-10-11T07:40:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/designs/database/US-044-db-design.md
    version: 1
  - path: docs/tests/US-044-test-strategy.md
    version: 1
supersedes: null
---

# US-044 Acceptance Criteria → Test Matrix

Unit classes live in `tests/ClassroomAgent.Tests/Application/UseCases/`, the
renderer class in `Infrastructure/Export/`, the schema class in
`Infrastructure/Persistence/`, host classes in `Web/…`. "W" is
`MeetExportWorld`; "world" is the US-033 October host world (A1, B1). "DTO" is
the page query run from the host's container with the same inputs. Status is the
red-phase state; the generation report gives the evidence.

| AC | Scenario | Level | Test class | Expected result | Status |
|---|---|---|---|---|---|
| AC-001 | reports page with a course: token meta tag, export action text, `portrait`/`landscape` radios with landscape checked, `data-course-id`, `data-from`/`data-to` = the period | Host | `MeetExportActionTests` | present | red |
| AC-001 | reports page without dates: `data-from`/`data-to` = effective default period (September 2026 from the host clock) | Host | `MeetExportActionTests` | 2026-09-01 / 2026-09-30 | red |
| AC-001 | reports page in an empty state (course without meetings) still offers the action | Host | `MeetExportActionTests` | present | red |
| AC-001 | bare form, `400` (malformed course) and `404` (unknown course) offer no action | Host | `MeetExportActionTests` | absent | green guard |
| AC-001 | meeting page offers the action with `data-course-id`, `data-meeting-id`; "meeting not found" page does not | Host | `MeetExportActionTests` | as stated | red / green guard |
| AC-002 | three sheets in order, names `Зведення`, `Зустрічі`, `Участь студентів` (uk) | Host | `MeetReportsExportTests` | as stated | red |
| AC-002 | Summary: Indicator/Value table, four values equal the DTO (count; total and average in minutes; average students) | Host | `MeetReportsExportTests` | numbers equal | red |
| AC-002 | Meetings: eleven headings in order; one row per DTO row in order: date, start, end, duration min, organizer name, email, mark, four counts; A1/B1 only | Host | `MeetReportsExportTests` | equal, 2 rows | red |
| AC-002 | Student participation: four headings; one row per DTO row in order; student with no meeting shows 0, 0 | Host | `MeetReportsExportTests` | equal, 3 rows | red |
| AC-002 | every data text of the Meetings and Students sheets appears on the reports page for the same inputs | Host | `MeetReportsExportTests` | contained | red |
| AC-002 | command: one workbook of three sheets; audit counts from reports 2 and 3 | Unit | `ExportMeetReportsCommandTests` | as stated | red |
| AC-003 | one sheet `Зустріч`; table headings Person, Email, Mark, Minutes, Of meeting; rows equal DTO people in order with mark text, N, M | Host | `MeetMeetingExportTests` | equal | red |
| AC-003 | "Other participants" line after one empty row: label, count 2, minutes 10 for A1; no class-account email anywhere in the file | Host | `MeetMeetingExportTests` | as stated | red |
| AC-003 | nobody identified: header row only, then the line (I-6) | Host + Unit | `MeetMeetingExportTests`, `ExportMeetMeetingCommandTests` | as stated | red |
| AC-004 | course workbook, each sheet: Course, Period (Oct 1 and 31), Exported (= host clock in Kyiv, date-time cell), delay line "not loaded yet"; row 5 empty | Host | `MeetReportsExportTests` | as stated | red |
| AC-004 | delay line with a watermark: notice, "loaded up to" and the local time formatted as on screen | Host | `MeetReportsExportTests` | contained | red |
| AC-004 | meeting workbook: Course, Meeting (date, start, end), Duration 45, Meeting code as stored, Organizer (name, email, mark), Exported, delay line; row 8 empty; B1's organizer mark "not a teacher of this course" | Host | `MeetMeetingExportTests` | as stated | red |
| AC-005 | autofilter Summary `A6:B10`, Meetings `A6:K8`, Students `A6:D9`, meeting `A9:E13` (the line outside); frozen rows 6 / 9 | Host | `MeetReportsExportTests`, `MeetMeetingExportTests` | as stated | red |
| AC-005 | a table replaced by an empty-state line has no autofilter | Host | `MeetReportsExportTests` | none | red |
| AC-005 | renderer: autofilter from header row to `TableLastRowIndex` (or the last row); none when `AutoFilter` is false | Infra | `ClosedXmlReportRendererMeetTests` | ranges | red |
| AC-006 | counts, minutes and averages numeric; average students format `0.0`; dates, times and date-times are date/time cells; total duration in minutes | Host + Infra + Unit | `MeetReportsExportTests`, `MeetMeetingExportTests`, `ClosedXmlReportRendererMeetTests`, `ExportMeetReportsCommandTests` | typed | red |
| AC-006 | renderer: a `Time` or `DateTime` cell without its workbook format throws `InvalidOperationException` | Infra | `ClosedXmlReportRendererMeetTests` | throws | red |
| AC-007 | no orientation → landscape; `landscape` → landscape; `portrait` → portrait; fit one page wide; header row repeated | Host + Unit | both host classes, both command classes | as stated | red |
| AC-007 | `Portrait`, `LANDSCAPE`, `x`, ` portrait` → `400` naming `orientation`, own message; no read, no file, no audit | Host + Unit | both host classes, both command classes | as stated | red |
| AC-008 | `Meet Алгебра 7_А 2026-10-01–2026-10-31.xlsx` and `Meet <course> 2026-10-05 10-00.xlsx` through `filename*`; no person, email or code in `Content-Disposition` | Host | `MeetReportsExportTests`, `MeetMeetingExportTests` | as stated | red |
| AC-008 | file name: both forms, `HH-mm`, the course part identical to the journal rule (parity theory), fallback | Unit | `MeetExportFileNameTests` | as stated | red |
| AC-009 | course and person names starting with `=`, `+`, `-`, `@` are text cells, unchanged, with the quote prefix, no formula | Host | `MeetReportsExportTests`, `MeetMeetingExportTests` | as stated | red |
| AC-010 | one `meet_reports_exported` row: actor, course, period, rows 3, meetings 2, format, no meeting id, no template, no `meet_code`; no sensitive value in the row | Host | `MeetReportsExportTests` | as stated | red |
| AC-010 | one `meet_meeting_exported` row: actor, course, meeting id, rows = DTO people, no period, no meetings count | Host | `MeetMeetingExportTests` | as stated | red |
| AC-010 | factories set exactly the db-design §3.2 columns; guards throw on bad ids, counts, period, role, format | Unit | `MeetExportAuditEventTests` | as stated | red |
| AC-010 | commands stage one row and commit once under `PermittedServiceWrite.AuditEvent`; render failure or commit failure returns no file | Unit | both command classes | as stated | red |
| AC-010 | CHECKs refuse: reports row without `export_meetings`, with a template id or a meeting id; meeting row with a period, with `export_meetings`, without or with `0` meeting id; journal row with a Meet column; non-export row with a Meet column; negative counts; a `meet_code` on an export row; refused outcome. Valid rows of both actions and a journal row are accepted | Infra | `MeetExportAuditSchemaTests` | as stated | red |
| AC-011 | each BR-025 cause: both exports `200`, one audit row, zero Google calls | Host | both host classes | as stated | red |
| AC-012 | course export: missing/malformed `courseId`, malformed or missing `from`/`to`, inverted period → `400` API-6 naming the field with the reports' message; unknown course → `404`; no audit; value not echoed | Host + Unit | `MeetReportsExportTests`, `ExportMeetReportsCommandTests` | as stated | red |
| AC-012 | meeting export: malformed or missing `meetingId` → `400` `Export.Validation.MeetingMalformed`; unknown meeting and meeting of another course → identical `404` `MeetingNotFound`; no audit | Host + Unit | `MeetMeetingExportTests`, `ExportMeetMeetingCommandTests` | as stated | red |
| AC-012 | refused export logs a `Warning` naming the field, never the value | Logging | `MeetExportLoggingTests` | as stated | red |
| AC-013 | per endpoint: protected and POST-only; Admin `200`, Dean `200`; anonymous `401`; Dean on forced change `403`; no token `400`; `GET` `405`; form body `415`; malformed body `400` without field errors — all API-6 | Security | `MeetExportAuthorizationTests` | as stated | red |
| AC-014 | English user: sheet names `Summary`, `Meetings`, `Student participation`, `Meeting`; labels and marks in English; names, emails, course and code as stored | Host | both host classes | as stated | red |
| AC-014 | every new `ReportText` Meet member and `Export.Validation.MeetingMalformed` resolve, non-empty, in uk and en; sheet names exact | Localization | `MeetExportTranslationTests` | as stated | red |
| AC-015 | no test calls Google: substituted readers record zero calls during every export | Host | both host classes | 0 | red |
| AC-016 | `Cache-Control: no-store` on both exports | Host | both host classes | present | red |
| AC-016 | the journal export's workbook has no autofilter (I-7) | Host | `MeetReportsExportTests` | none | green guard |
| AC-017 | Kyiv bounds: meetings at 10-01 00:30 and 10-31 23:30 local are rows, 11-01 00:10 is not; the 10-31 meeting's end is a date-time 11-01 00:40 | Host | `MeetReportsExportTests` | as stated | red |
| FR-014 | success logs one `Information` line with ids, counts, orientation and size; no name, email or meeting code in any line | Logging | `MeetExportLoggingTests` | as stated | red |
| FR-009 | validation happens before any read of report data (`W.Reads` = 0) | Unit | both command classes | 0 | red |
