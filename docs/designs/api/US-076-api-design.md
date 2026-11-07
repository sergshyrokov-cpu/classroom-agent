---
artifact_type: api_design
story: US-076
version: 1
status: DRAFT
created_at: 2026-11-07T07:06:13Z
updated_at: 2026-11-07T07:06:13Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-076-spec.md
    version: 2
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 112
supersedes: null
---

# US-076 API Design — A «?» beside the title opens short help on five pages

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-076-openapi.yaml`.** The missing file is this stage's
recorded decision, as in earlier Stories with no API change (US-068, US-069, US-073). Downstream stages
that list `openapi` among their inputs read this document instead.

This document still fixes what the Specification left to API design: the translation key names and their
final values (Specification FR-006, section 4.1 label check).

Delegation: none. The designer read the Specification, both `SharedResource` resx files and
`Web/Security/PageSearchHintTextKeys.cs` directly; the UI labels were compared with the resx values in
this stage.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification states the Story does not
change public API behaviour. Specification v2 states it:

- **FR-008:** the help "adds no endpoint, no form, no link, no request when opened, no audit event, no log
  line, and no write"; the five pages' "forms, links, policies, read-only behaviour and exports stay as
  they are".
- **Section 6:** "The Story adds no input: no form, no query or route parameter, no request body."
- **Section 7:** "it adds no endpoint and changes no policy".

No route, status code, response body, policy, form field or export changes. The five pages keep their
current routes (`SignInRoutes.Journal`, `MeetCodes`, `MeetReports`, `MeetReportMeeting`,
`ReportTemplates`) and policies.

## 2. What changes, and why none of it is API contract

The change is rendered HTML inside five existing Razor page responses: a `<details>`/`<summary>` block in
the layout's title area (FR-002…FR-004), text from translation files (FR-005, FR-006) and styling in
`wwwroot/css/site.css` (FR-007). The page id is a view-data value set in code by the view (VR-001), never
request input. This is presentation, not an HTTP contract.

## 3. Translation keys (fixed here, FR-006)

Key names follow the existing dotted pattern (`PageSearchHint.Ordinary`, `DataMarks.Left.On`). The page
segment is the PascalCase of the Specification's page id.

| Page id (Spec §1) | Key prefix | Lines |
|---|---|---|
| `journal` | `PageHelp.Journal.Line1` … `Line4` | 4 |
| `meet-codes` | `PageHelp.MeetCodes.Line1` … `Line4` | 4 |
| `meet-reports` | `PageHelp.MeetReports.Line1` … `Line3` | 3 |
| `meeting` | `PageHelp.Meeting.Line1` … `Line3` | 3 |
| `report-templates` | `PageHelp.ReportTemplates.Line1` … `Line3` | 3 |
| control | `PageHelp.Control.Name` | 1 |

18 keys, each present in both `SharedResource.uk.resx` and `SharedResource.en.resx`. Key names are
referenced from code through a constants class beside `PageSearchHintTextKeys` (one public type per file);
its name and the view-data key name are implementation choices.

### 3.1 Values

The values are exactly the lines of Specification v2 section 4.1 as approved at `HUMAN_SPEC_APPROVAL`,
with one change the Owner decided at that gate (finding `US-076/SPEC_REVIEW/m-4`): journal line 2 begins
with the word «Позначка» / "The mark" so the quoted label keeps its lowercase UI value.

`PageHelp.Journal.Line2`:

- **uk:** Позначка «вибув(-ла)» з датою біля імені означає, що учня зараз немає у списку курсу в
  Classroom; дата — коли синхронізація бачила його востаннє. Позначка «немає в Classroom» під назвою
  завдання стосується лише цього завдання, а рядок над таблицею «Курсу немає в Classroom з …» або «Дані
  не оновлювалися з …» — усього курсу.
- **en:** The mark "left" with a date beside a name means the student is not on the course's list in
  Classroom now; the date is when synchronization last saw them. The mark "not in Classroom" under a
  coursework title concerns only that item, while a line above the table, "Course not in Classroom since
  …" or "Data not updated since …", concerns the whole course.

All other 17 values: verbatim from Specification v2 section 4.1 (lines) and «Довідка про сторінку» /
"Help about this page" (control).

### 3.2 Label check (Specification section 4.1)

Every quoted UI label in the help lines was compared with the current resx values:

| Label in help (uk / en) | Key | Match |
|---|---|---|
| «вибув(-ла)» / "left" | `DataMarks.Left.On`, `DataMarks.Left` | yes (after m-4) |
| «немає в Classroom» / "not in Classroom" | `DataMarks.Item.NotInClassroom` | yes |
| «Курсу немає в Classroom з …» / "Course not in Classroom since …" | `DataMarks.Header.NotInClassroomSince` | yes |
| «Дані не оновлювалися з …» / "Data not updated since …" | `DataMarks.Header.NotUpdatedSince` | yes |
| «Непризначені» / "Unassigned" | `MeetCodes.List.Unassigned` | yes |
| «Прив'язати до іншого курсу» / "Link to another course" | `MeetCodes.Action.Relink` | yes |
| «не курс» / "not a course" | `MeetCodes.Action.MarkNotACourse` (quoted part) | yes |
| «Не в списку курсу» / "Not on the course list" | `MeetReports.Meetings.NotOnCourseList` | yes |
| «не в списку курсу» / "not on the course list" | `MeetReports.Mark.NotOnCourseList` | yes |
| «Інші учасники» / "Other participants" | `MeetReports.Meetings.OtherParticipants` | yes |
| «Шаблони звітів» / "Report templates" | `ReportTemplate.List.Title`, `ReportTemplate.NavigationEntry` | yes |
| «Звіт» / "Report" | `ReportTemplate.Action.Report` | yes |
| «Копіювати» / "Copy" | `ReportTemplate.Action.Copy` | yes |
| «Експорт в Excel» / "Export to Excel" | `Report.Export.Action` | yes |
| «Академічний журнал» / "Academic journal" | `ReportTemplate.BuiltIn.AcademicJournal` | yes |
| «Стан даних» / "Data health" | `DataHealth.Title` | yes |
| «Зустрічі Meet» / "Meet meetings" | `MeetCodes.Title` | yes |

No difference remains to report. The English lines keep the straight double quotes of the Specification;
the resx files already mix straight and typographic quotes, so neither is introduced here.

## 4. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-007 | none — existing GET page responses of the five pages, unchanged routes and policies |

## 5. Auth and error model

Unchanged. The help is part of five already-protected pages (SC-3, SC-4); it has no error of its own
(Specification section 8).

## 6. Open findings

- `US-076/SPEC_REVIEW/m-4` — **fixed** here (section 3.1).
- `US-076/SPECIFICATION/m-2` — not this stage's; the Owner deferred it to `trebovaniya.md` v113.

No new finding.
