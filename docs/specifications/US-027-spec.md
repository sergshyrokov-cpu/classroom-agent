---
artifact_type: specification
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T18:57:01Z
updated_at: 2026-10-04T18:57:01Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-027-report-templates.md
    version: null
  - path: trebovaniya.md
    version: 84
  - path: docs/decisions/US-027-open-decisions.md
    version: 1
  - path: docs/specifications/US-025-spec.md
    version: 1
supersedes: null
---

# US-027 Specification — Report templates and the on-screen report

## 1. Overview

This Story opens the program's second section, **"Reports and templates"**
(`trebovaniya.md` v84 §4 "Направление развития", Epic 3), next to "Google
Workspace", where the journal of US-025 stays unchanged. In it an Admin or a
Dean keeps the school's report templates and sees, for a course and a period,
the report a template produces (§2: "Использование шаблонов отчётов" ✔ ✔,
"Создание и редактирование шаблонов отчётов" ✔ ✔).

Six properties shape everything below.

- **A template is a set of settings, not a layout** (§3 `ReportTemplate`, v84):
  view, what each cell state is shown as, whether materials are hidden, the
  grading scale and the hours per lesson. The Excel layout is US-028.
- **Templates read journal fields, not mirror tables** (§3 v84). Report building
  in `Application` consumes a set of named journal fields (FR-004); one port
  fills them from the Google mirror today. Epic 17 will fill the same fields from
  two streams without any template changing.
- **The report is a structure, not HTML.** A use case returns a report DTO that
  the on-screen page renders; US-028 (Excel), US-029 (Word) and US-030 build
  files from the same DTO (Story Notes).
- **One built-in template, defined by the program.** "Academic journal" lives in
  code, not in a row an installation could change; it is listed in every
  installation, can be copied and can be neither changed nor deleted (§3 v84).
- **Templates created in the school are school-own data** (v83 second layer):
  shared by the whole school, each with its author, referencing nothing in the
  mirror and referenced by nothing there.
- **The lesson date is the publication date** (OD-001 a) — and it decides period
  membership too. Today the program does not store `scheduledTime`; how that is
  closed is **OD-004, open**.

## 2. Business Goal

The journal of US-025 shows Classroom as it is: raw points, every state. A
school keeps a paper academic journal per group and semester with grades in its
own scale and a sheet of lesson topics. The built-in "Academic journal" gives
nearly every school that journal without set-up; a school with another scale or
other marks copies it and changes the copy, without anyone editing layouts in
code. Export and the printed journal (US-028 … US-030) then build files from the
report defined here.

## 3. Business Flow

### 3.1 The section

From the home page the Dean (or Admin) follows "Reports and templates". The
section lists the templates: the built-in "Academic journal" first, then the
templates created in the school by name, each with its author and the date of
its last change (FR-002). From the list the user can open a report, copy any
template, create a template, and change or delete a created one.

### 3.2 Creating, copying, changing, deleting

- **Create** opens the template form with the defaults of FR-007; **copy** opens
  it pre-filled with the copied template's settings and the name of FR-008. The
  user edits and saves; only a successful save creates the template.
- **Change** opens the same form with the template's settings. Saving replaces
  them.
- **Delete** asks for confirmation on the page and deletes the template.
- An invalid form is shown again with the values entered and a message for each
  problem; nothing is saved (AC-009).
- In read-only mode every one of these is refused by `Application` with the
  read-only reason (FR-013); the list and reports keep working.

### 3.3 Viewing a report

The user chooses a template, a course and a period on the report page and
submits. The form is a `GET`: template, course and dates travel in the query
string, which carries no personal data (Story Notes, US-039 INFO-1). The page
shows the form with the chosen values and, below it, the two parts of the report
(FR-005). Viewing is not exporting and is not audited.

## 4. Functional Requirements

### FR-001 The section and its pages

A new section "Reports and templates" with server-rendered pages (names
indicative; API_DESIGN fixes routes and status codes):

| Page | Method | Purpose |
|---|---|---|
| Template list | `GET` | FR-002 |
| New template form / save | `GET` / `POST` | FR-007, FR-009 |
| Copy form / save | `GET` / `POST` | FR-008, FR-009 |
| Change form / save | `GET` / `POST` | FR-009 |
| Delete | `POST` | FR-010 |
| Report | `GET` | FR-005, FR-011 |

Every `POST` carries the antiforgery token (§8, global rule). Each action is a
use case in `Application/UseCases` returning DTOs (AD-8); the host binds,
validates shape and renders. No JSON endpoint is added (I-12).

The home page gains a "Reports and templates" entry for Admin and Dean, visible
in read-only mode as well. The journal entry of US-025 stays where it is.
Hiding an entry is never the enforcement — FR-012 and FR-013 are.

### FR-002 The template list

- The built-in "Academic journal" first, under its translated name, marked as
  built-in, with no author and no date.
- Then every template created in the school, ordered by name in the collation of
  the UI language, ties broken by internal id. Each shows its name as written
  (never translated), its author (FR-014) and the date and time of its last
  change in the school's time zone.
- Actions: report and copy for every template; change and delete for created
  templates only. The built-in template offers no change or delete action, and
  the use cases refuse them anyway (FR-006).

### FR-003 Template settings

A template has exactly these settings (§3 v84):

| Setting | Values |
|---|---|
| Name | text, VR-001 |
| View | `full` or `short` (BR-057) |
| Hide materials | yes / no |
| Grading scale | "no conversion", or a table of ranges (FR-015) |
| Hours per lesson | whole number, VR-005; default 2 |
| Mark of each cell state | for each state of the table below: the program's text, a text of the school's own, or empty (VR-003) |
| Late mark | the program's text, a text of the school's own, or not shown |

The **cell states** a template maps (BR-056, US-025 FR-006):

| State | Applies to |
|---|---|
| turned in, not graded | graded work |
| returned without a grade | graded work |
| turned in | ungraded work |
| returned | ungraded work |
| not turned in (due has passed) | all coursework |
| not due yet | all coursework |
| not turned in, no due date | all coursework |
| not assigned | all coursework |
| unrecognised state | all coursework — the program's choice shows Google's raw value (US-015 OD-005) |

The grade itself is not a mark: it is shown through the scale (FR-015). The
"draft" label and the last turn-in date are fixed by the view (FR-005.4). A
material's cell is always empty (BR-056).

"The program's text" is translated into the viewer's UI language; a text of the
school's own is shown as written (NFR-073).

### FR-004 Journal fields

Report building in `Application` reads only these fields, supplied through one
read port in `Application/Ports` (name indicative, `IJournalFieldSource`)
implemented in `Infrastructure` from the Google mirror (§3 v84; AD-4). No
template, report builder or view reads a mirror table or entity directly.

| Field | Content | Filled today from |
|---|---|---|
| Course | internal id, name, section | `Course` |
| Teachers | display name of each teacher of the course (FR-005.5) | `CourseMembership` role teacher, `ClassroomParticipant` |
| Student | internal participant id, display name (US-025 FR-005) | `CourseMembership`, `ClassroomParticipant` |
| Lesson | internal item id, kind (graded / ungraded / material), title, lesson date, maximum points, due instant | `CourseWork` |
| Cell | the cell state of US-025 FR-006 computed at instant B, assigned grade points, draft grade points, late flag, last turn-in instant, raw state | `Submission` + the US-025 cell computation |

- **Lesson date** — the publication date: `scheduledTime` if set, otherwise
  `creationTime` (OD-001 a). **OD-004 (open)** decides where `scheduledTime`
  comes from; the Specification states the rule, not the storage. When neither
  value is known for an item (`creationTime` is nullable in storage), its
  `ItemDate` is used (I-9).
- **Cell** is computed by the same function as the journal of US-025 (one
  computation for every cell, BR-056 v57), with B read once per request. The
  journal page of US-025 does not change.
- The port reads with a **fixed number of queries per report**, independent of
  rows and columns, without change tracking (US-025 FR-014). `DbContext` appears
  in neither `Application` nor `Web` (AD-3).

### FR-005 The report

Input: a template, a course and a period `[from, to]`. The period is converted
to a UTC interval `[start, end)` exactly as US-025 FR-003 (school time zone,
inclusive dates, DST rule). Output: a report DTO with a header and two parts,
independent of HTML.

**FR-005.1 Items of the report.** The non-deleted and deleted items alike
(US-015 OD-004, as in the journal) of the chosen course whose **lesson date**
lies in `[start, end)` (OD-001 a, v84: "и на обоих листах, и в датах колонок").
An item outside the period by lesson date is in neither part, even if its
`ItemDate` is in the period — the report and the journal for one period may
differ (v84).

**FR-005.2 Header.** Template name (translated for the built-in, as written
otherwise), course name and section (Google data), the period, and the teachers
(FR-005.5).

**FR-005.3 Part "Grading"** ("Оцінювання").

- **Columns:** the report's items; materials excluded when the template hides
  them. Order: lesson date ascending, then title in the collation of the UI
  language, then internal id. Header: the lesson date in the school's time zone,
  formatted for the UI language, and the item's title (I-8); a material's column
  carries the translated "material" marker.
- **Rows:** the students of the period by BR-051 exactly as US-025 FR-005, with
  "a submission in the period" meaning a submission to one of the report's
  columns; same display and order rules.
- **Cells:** from the journal field "Cell" (FR-004), rendered by FR-005.4.

**FR-005.4 Cell content.**

| Journal cell | Report cell |
|---|---|
| material | empty |
| assigned grade | the grade through the scale (FR-015) |
| any other state | the template's mark for that state (FR-003); empty if the mark is empty |
| unrecognised state with the program's choice | the raw value, never translated, never a grade (US-025 FR-006) |

Added to a cell that has a submission:

- **late** — the template's late mark, when the late flag is set and the late
  mark is shown;
- **full view only:** the **draft grade** of graded work, when the cell shows a
  state rather than an assigned grade (US-025 I-2), through the same scale and
  labelled with the translated "draft"; and the **last turn-in date** in the
  school's time zone (BR-057, BR-058).

The short view shows the grade or the state mark and the late mark only.

**FR-005.5 Teachers.** Every `CourseMembership` of the course with role teacher
that was on the roster during part of the period, by the BR-051 rule of US-025
FR-005 condition 1 (I-6). Display: full name, else email, else the translated
"teacher without a name"; order as student rows. Joined with ", ".

**FR-005.6 Part "Lesson topics"** ("Теми занять").

One row per report item that is **not a material** — ungraded work included;
materials are excluded whatever the template's materials setting (v84; I-7).
Order as the Grading columns. Columns:

| Column | Content |
|---|---|
| Date | the lesson date, school time zone |
| Topic | the item's title, never translated |
| Hours | the template's hours per lesson |
| Teacher | the teachers of FR-005.5 |
| Independent work | empty (Epics 14–16) |
| Teacher's signature | empty |

Column headings are program text, translated.

**FR-005.7 Empty reports.** No item in the period: both parts are replaced by a
translated "nothing was published in this period". Items but no student: the
Grading table is not drawn and "no students in this period" is shown; Lesson
topics is still shown. Neither is an error.

### FR-006 The built-in template "Academic journal"

Defined in code in `Application` (or `Domain`), never stored as a row, never
written by any use case (Story Notes; I-1). Its settings (OD-003 a, v84):

| Setting | Value |
|---|---|
| Name | translated: "Академічний журнал" / "Academic journal" |
| View | short |
| Hide materials | yes |
| Scale | the 12-point preset of OD-002 (FR-015) |
| Hours per lesson | 2 |
| "not assigned" | a text of its own: "—" (dash; the same in both languages) |
| every other state, the unrecognised state included | empty |
| Late mark | not shown |

- It is identified in requests by a fixed key distinct from every created
  template's id (API_DESIGN fixes the form).
- Change and delete of it are refused by `Application` with a translated
  message, whatever the role and mode (AC-001); the attempt is not audited — it
  changes nothing and is not an SC-11 action (I-15).
- A later program version may change its definition; copies are not touched
  (§3 v84), because a copy stores its own settings (FR-008).

### FR-007 Creating a template

The new-template form opens with these defaults (I-2): name empty; view full;
materials shown; scale "no conversion"; hours 2; every state mark and the late
mark "the program's text". A template with these defaults reproduces the
journal of US-025 for the report's items. Saving validates (§6) and creates the
template with the actor as author (FR-014).

### FR-008 Copying a template

Any template — built-in or created — can be copied. The copy form is pre-filled
with all settings of the source; its name is the source name followed by the
translated " (copy)" in the actor's UI language, at the moment of copying (I-3).
On save the copy is an ordinary created template: its own row, its own settings,
author = the actor, no link to the source. Copying the built-in template stores
its settings as they are in the running program version. A copy is a create for
audit (FR-016).

### FR-009 Changing a template

Any created template can be changed by any Admin or Dean (§2 v84). Saving
validates (§6) and replaces all settings; the author does not change; the
last-change time is updated. If the template was deleted meanwhile, the save is
refused as "template not found" (FR-011). Two users saving the same template:
the later save wins whole; no merge (I-11).

### FR-010 Deleting a template

Any created template can be deleted by any Admin or Dean. Delete is permanent;
nothing references a template (v83 school-own data; US-028 adds its own
reference rules). Deleting a template already deleted is "template not found".

### FR-011 The report page and its query

Query parameters (names indicative):

| Parameter | Meaning | Absent |
|---|---|---|
| `template` | the built-in key or the internal id of a created template | the built-in template is pre-selected; no report is shown until a course is chosen (I-4) |
| `courseId` | internal id of a `Course` | the form only |
| `from`, `to` | the period, school time zone | the default of US-025 FR-007 — the current calendar month |

- The template drop-down lists the built-in template first, then created ones
  in the order of FR-002. The course drop-down is that of US-025 FR-002
  (every stored course, name and section, never translated; a translated
  message when none is stored).
- The view is the template's setting; there is no view parameter.
- Validation (§6) happens before anything is read for the report. On a failure
  the page is re-rendered with the form and a translated message naming the
  problem — not the host's error page; no report data is read; status within
  API-5 per API_DESIGN (expected `400` malformed / inverted, `404` unknown
  template or course).
- The page sets its own return path for the language switcher from validated
  values (US-025 IMPL D-1; S-05).

### FR-012 Authorization

`InstallationPolicies` gains one policy per matrix row (§2; US-019 I-5), names
indicative:

- `UseReportTemplates` — "Использование шаблонов отчётов", ✔ Admin, ✔ Dean:
  the template list and the report page;
- `EditReportTemplates` — "Создание и редактирование шаблонов отчётов",
  ✔ Admin, ✔ Dean: the new, copy, change and delete forms and saves.

Anonymous requests are sent to sign in; the SC-4 anonymous list gains nothing;
the endpoint enumeration test classifies every new page as protected. A Dean
held on the forced password change is refused as everywhere (US-012). No
per-template ownership check: any Admin or Dean changes and deletes any created
template (§2 v84). Teacher and Student have no account (Hard Stop).

### FR-013 Read-only mode

- Listing templates, opening forms and viewing reports work in read-only mode
  (§2, BR-026); they write nothing and call no Google API.
- Creating, copying (saving the copy), changing and deleting a template call
  `IReadOnlyModeGuard` **first** and are refused in `Application` with
  `ReadOnlyModeException` and the read-only reason, for all three BR-025 causes
  (AD-6). Nothing is changed. The refusal writes the audit row of FR-016 with
  outcome `Refused`, category `ReadOnlyMode` (precedent US-009, US-011, US-019).
- No Google port is opened by any use case of this Story, in any mode (S-04).

### FR-014 Author

- The author is the `AppUser` of the session who created the template (a copy's
  author is whoever copied it); never a request value.
- The list shows the author's email (already visible to Admins; I-10).
- When the retention purge deletes that account (US-037), the template stays;
  its author becomes unknown and the list shows a translated "account deleted"
  (Story Notes). The template stores no copy of the author's email or name.
  DB_DESIGN chooses the mechanism (for example a nullable reference cleared by
  the purge) so that the purge is never blocked by a template.

### FR-015 The grading scale

- **"No conversion"**: the grade is shown as raw points out of the maximum,
  exactly as the journal shows it (US-025 FR-006: trailing zeros dropped, number
  format of the UI language, `0 / 10` not empty).
- **Ranges**: an ordered table of rows `from % – to % → label`. The percent of
  a grade is `points / maximum × 100`, **rounded to a whole number, 0.5 up**
  (v84; applied to every scale — I-5), then clamped to 0 … 100 (extra credit
  above the maximum counts as 100 %; I-5). The cell shows the label of the row
  whose `[from, to]` contains it. The label is the school's text, never
  translated.
- An item whose maximum is not positive cannot be converted: the cell shows raw
  points as under "no conversion" (I-5).
- **The 12-point preset** (OD-002): 1 — 0–8, 2 — 9–16, 3 — 17–25, 4 — 26–33,
  5 — 34–41, 6 — 42–50, 7 — 51–58, 8 — 59–66, 9 — 67–75, 10 — 76–83,
  11 — 84–91, 12 — 92–100. The form offers it as a button that fills the table;
  the rows can then be edited. No other preset ships in v1 (OD-002).
- Validation: VR-004.

### FR-016 Audit

Per SC-11 (§5 v84), one `AuditEvent` row, target type report template, target
id = the template's internal id, actor = the session account and role:

| Action (names indicative) | When | Outcome |
|---|---|---|
| `ReportTemplateCreated` | a template is created — from scratch or as a copy | `Succeeded` |
| `ReportTemplateChanged` | a template's settings are saved | `Succeeded` |
| `ReportTemplateDeleted` | a template is deleted | `Succeeded` |
| the same three | refused by read-only mode (FR-013) | `Refused` / `ReadOnlyMode`; target id null for a create, the template id otherwise |

- The row carries no template content — no name, mark, label or setting — and no
  personal data (SC-11, SC-10).
- The audit row is written in the same transaction as the change (PC conventions
  as in earlier Stories).
- Viewing a report, opening a form, a validation failure and a "not found"
  write no audit row.

### FR-017 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073): section and page titles, navigation entry,
form labels and buttons, the built-in template's name, "(copy)", "built-in",
"account deleted", the program text of every cell state (graded and ungraded
wording separately, as US-025), "material", "draft", the column headings of both
parts, "teacher without a name", the empty states of FR-005.7 and FR-011, the
confirmation of delete, every validation and refusal message of §6 and §8.
Reused keys of US-025 stay as they are. Data from Google — course names,
sections, titles, names, emails, raw states — and every text written into a
template — name, marks, scale labels — are never translated. Dates and numbers
follow the UI language. The existing missing-key test covers the new keys.

### FR-018 Persistence

- **One new table** for created templates (DB_DESIGN shapes it; `ReportTemplate`
  is already named in `package-map.md`), with its scale rows and state marks,
  the author reference of FR-014, and created / changed timestamps (PC
  conventions). The built-in template has no row.
- New audit action values, if `AuditAction` / `AuditTargetType` need them.
- Every change ships with its EF Core migration (PC-2).
- **The mirror is not changed by this Story**, except as OD-004 resolves.
- Indexes (NFR-003, PC-7) support: the report's items of a course by lesson date
  (depends on OD-004), memberships of a course, submissions of an item; DB_DESIGN
  confirms which exist (US-025) and adds what is missing.

### FR-019 Logging

- A built report: one `Information` line with the actor's account id, the
  template id or built-in key, the course id, the period, and the row, column
  and topic counts.
- A template created, changed or deleted: one `Information` line with the actor
  id, the action and the template id.
- A validation failure: one `Warning` line naming the field and the rule, never
  the value. A read-only refusal: as US-007 FR-009.
- No log line carries a template name, mark or label, a student or teacher name,
  an email, a grade, a title or other Google data (SC-10, NFR-023, DC-10).

## 5. Acceptance Criteria

The Story's eleven criteria, unchanged in meaning; ids are the Story's.

| Id | Criterion | Specified by |
|---|---|---|
| AC-001 | The section lists the built-in "Academic journal"; nothing lets anyone change or delete it | FR-001, FR-002, FR-006 |
| AC-002 | Copy, create, change, delete; author recorded; shared by the school | FR-007 … FR-010, FR-012, FR-014 |
| AC-003 | Grading scale: range label; "no conversion" raw points; overlapping or incomplete table refused | FR-015, VR-004 |
| AC-004 | "Academic journal" report: Grading by item and lesson date, no materials, BR-051 rows; Lesson topics rows with date, title, hours, teachers, empty columns | FR-004, FR-005, FR-006 |
| AC-005 | A copy's changed marks, materials, view or hours show in its report; the built-in template's report does not change | FR-003, FR-005.4, FR-008 |
| AC-006 | Read-only mode: view works; create, copy, change, delete refused in `Application`; no Google call | FR-013 |
| AC-007 | Audit: one row per create, change, delete with action, actor, template id only; viewing writes none | FR-016 |
| AC-008 | Only Admin and Dean; allowed- and forbidden-role test per endpoint | FR-012 |
| AC-009 | Invalid input: message naming the problem, nothing saved, value not logged | §6, FR-011, FR-019 |
| AC-010 | Bilingual; school and Google text as written | FR-017 |
| AC-011 | Tests never reach Google; data synthetic in PostgreSQL | FR-004, FR-013, TC-2, TC-4 |

Derived from the Specification:

| Id | Criterion | Specified by |
|---|---|---|
| AC-012 | Period by lesson date in the school's time zone (`Europe/Kyiv`): an item published 28 September and due 2 October is in the September report with date 28 September and absent from the October report; an item published at 00:30 local time on the period's first day is in it (TC-8) | FR-004, FR-005.1 |
| AC-013 | Rounding at a range boundary: with the 12-point preset, 0.85 of 10 (8.5 %) shows 2 and 0.84 of 10 (8.4 %) shows 1; 10.5 of 10 shows 12 | FR-015 |
| AC-014 | A template whose author account was deleted stays listed and usable, with "account deleted" as author | FR-014 |

## 6. Validation Rules

Every rule applies on save, in `Application` (`Validation`, DataAnnotations
where they fit — §8, `package-map.md`), before anything is written. Text is
trimmed first; "text" below excludes control characters (I-13).

### VR-001 Name

Required; 1–100 characters; unique among created templates, compared trimmed and
case-insensitively (I-14); it may equal the built-in template's name in either
language. Messages: "enter a name", "the name is too long", "a template with
this name already exists".

### VR-002 View, materials

View exactly `full` or `short`; hide materials a boolean. A missing or unknown
value is malformed (a tampered form).

### VR-003 Marks

For each state of FR-003 and for the late mark: exactly one of "the program's
text", "own text", "empty" (late: "not shown"). Own text is required when
chosen; 1–30 characters (I-13). The set of states is closed: an unknown state
in the request is malformed; a missing one is malformed.

### VR-004 Scale

- Mode exactly "no conversion" or "ranges". With "no conversion" any rows sent
  are ignored and not stored.
- With "ranges": 1 to 101 rows; each `from` and `to` a whole number 0–100 with
  `from ≤ to`; each label required, 1–10 characters (I-13).
- Sorted by `from`, the rows must **cover 0–100 with no gap and no overlap**: the
  first starts at 0, the last ends at 100, each starts at the previous `to` + 1.
  A table that fails is refused with a message naming the first offending row
  and whether it overlaps or leaves a gap (AC-003). Labels may repeat.

### VR-005 Hours per lesson

Required; a whole number 1–10 (I-16).

### VR-006 Template reference

The built-in key, or one decimal value parsing to a positive 64-bit integer.
Anything else is malformed. A well-formed id of no created template is "template
not found". Change or delete with the built-in key is refused (FR-006).

### VR-007 Report query

- `courseId`, `from`, `to`: the rules VR-001 … VR-003 of US-025, with the same
  messages.
- `template`: VR-006.
- Any other parameter is ignored and never echoed into the page.

### VR-008 The actor

The author and the audit actor are the signed-in account from the session,
never a request value.

### VR-009 Rendering

Google data and every template text are rendered with HTML encoding; nothing is
interpreted as markup (SC-10).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | Every page declares `UseReportTemplates` or `EditReportTemplates`; deny by default; the SC-4 anonymous list gains nothing. | SC-4, §2, API-9 |
| S-02 | Allowed-role tests (Admin, Dean) and forbidden tests (anonymous; a Dean on the forced password change) for every endpoint. | SC-1, TC-5 |
| S-03 | No Teacher or Student access; no roster scoping; no per-course restriction. | Hard Stop, §2 |
| S-04 | No use case of this Story calls a Google port or reads a secret, in any mode. | SC-5, SC-8, BR-026 |
| S-05 | The report query string carries the template reference, course id and dates only; the page sets its own switcher return path from validated values. | Story Notes, US-039 INFO-1, US-025 IMPL D-1 |
| S-06 | Create, copy, change, delete refused in read-only mode in `Application`, guard first; proven for all three BR-025 causes. | AD-6, BR-025, BR-026, TC-5 |
| S-07 | Every `POST` carries the antiforgery token. | §8 |
| S-08 | Input validated before business logic; the rejected value never reaches a log, an audit row or an error page detail. | §8, SC-10 |
| S-09 | Audit rows per FR-016, template id only; no content, no personal data; never updated. | SC-11 |
| S-10 | No log line carries template text or Google data; internal ids only. | SC-10, NFR-023 |
| S-11 | Output HTML-encoded (VR-009). | SC-10 |
| S-12 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-13 | No new outbound destination. | SC-13 (Hard Stop) |
| S-14 | A template stores no personal data — no copy of the author's name or email. | SC-10, PC retention |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in (SC-4). |
| Dean on the forced password change | Refused as on every other page (US-012). |
| Invalid template form | The form again with the entered values and a message per problem; nothing saved; status per API_DESIGN (expected `400`). |
| Malformed report query or `from` after `to` | The report page with the form and the message; no report; expected `400`. |
| Unknown template or course | The page with the form and "not found" message; expected `404`. |
| Change or delete of the built-in template | Refused with a translated message; nothing changed; expected `400` or `403` per API_DESIGN within API-5. |
| Change or delete of a template deleted meanwhile | "Template not found"; expected `404`. |
| Read-only mode on a save or delete | `ReadOnlyModeException` → the US-008 mapping (error page with the reason); audit `Refused` / `ReadOnlyMode`. |
| Missing or wrong antiforgery token | `400` with the §8 error page. |
| No course stored; empty report | Not an error: FR-011, FR-005.7 messages. |
| An unexpected failure | The single exception handler answers the error page with no detail (AD-9, API-10). |

## 9. Non-Functional Requirements

- **NFR-003 / PC-7** — explicit indexes back the report queries (FR-018).
- **Bounded queries** — a fixed number of round trips per report (FR-004).
- **NFR-070** — usable on a phone: both tables scroll horizontally inside the
  page; the template form, including the scale table, is usable at phone width.
- **NFR-073 / NFR-074** — bilingual; dates in the school's time zone; tests of
  period boundaries use `Europe/Kyiv` (TC-8).
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.

## 10. Out of Scope

- Excel layout and export (US-028), Word (US-029), assembling several templates
  (US-030), and the export audit row.
- Filling "independent work", "teacher's signature" or real hours from school
  data (Epics 14–16); integration of two streams (Epic 17).
- Any change to the US-025 journal page or to synchronization and the mirror —
  **unless OD-004 (a) is chosen**, which adds storing `scheduledTime`.
- Uploading a template file; per-Dean private templates; binding a template to
  courses.
- Presets other than the 12-point scale (OD-002).
- A JSON API for templates or reports (I-12).

## 11. Open Decisions

Full text, options and resolutions in `docs/decisions/US-027-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 Lesson date and period membership | **RESOLVED** 2026-10-04 (a), v84 | none — FR-004, FR-005.1; implementation depends on OD-004 |
| OD-002 Preset scale ranges | **RESOLVED** 2026-10-04, v84 | none — FR-015 |
| OD-003 The built-in template's marks and scale | **RESOLVED** 2026-10-04 (a) | none — FR-006 |
| OD-004 `scheduledTime` is not stored | **OPEN — blocking** | The lesson date of FR-004 cannot be computed as v84 defines it; FR-005.1, FR-018 indexes, AC-004, AC-012 and DB_DESIGN wait for it. Recommended: (a) store `scheduledTime` on `CourseWork` in this Story. |

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`.

- **I-1 The built-in template is code.** A row would be something an
  installation could change; a program-defined value cannot be, and a program
  update replaces it naturally (Story Notes, §3 v84).
- **I-2 A new template starts as the journal.** Defaults (full, materials shown,
  no conversion, program marks) reproduce what US-025 shows, so a Dean changes
  only what differs; hours default to 2 (v84).
- **I-3 A copy's name.** "<source> (copy)" in the copier's language, editable
  before saving; once saved it is school text and never translated.
- **I-4 The built-in template is pre-selected** on the report page — it is the
  template nearly every school uses (v84 business value).
- **I-5 Rounding, extra credit and zero maximum.** v84 states "rounded to a
  whole number, 0.5 up" for the preset; it is applied to every range scale so
  that ranges are always whole numbers. A grade above the maximum (extra credit)
  counts as 100 %; a non-positive maximum cannot give a percent and the raw
  points are shown.
- **I-6 Teachers of the period.** "Преподаватели курса" is read as the teachers
  on the roster during part of the period — the BR-051 rule for students — so a
  past semester's report names who taught it, not today's teachers.
- **I-7 Lesson topics never lists materials.** v84 defines the sheet as "кроме
  материалов" in v1; the "hide materials" setting affects the Grading part only.
- **I-8 Column header carries the title too.** v84 puts the lesson date in the
  header; on screen two items of one day are told apart only by their titles.
  The paper layout is US-028's business.
- **I-9 Missing `creationTime`.** Google always sends it; the column is nullable
  only because the import tolerates its absence. Should it be absent (and
  `scheduledTime` too), the item's `ItemDate` is its lesson date rather than
  dropping it silently.
- **I-10 The author is shown by email.** `AppUser` has no display name; the email
  is already visible to Admins and is the account's identity.
- **I-11 Last save wins.** No convention defines optimistic concurrency; templates
  are small and edited rarely. A save after a delete is "not found".
- **I-12 No JSON API.** As US-025 I-9: server-rendered pages only; the report
  DTO is what later Stories reuse, not an HTTP endpoint.
- **I-13 Text limits.** Name 100, mark 30, scale label 10 characters, no control
  characters: a mark and a label fit in a journal cell; the limits are the
  Specification's choice, not a business rule, and can be changed here.
- **I-14 Unique names.** Two templates with one name cannot be told apart in the
  drop-down; the built-in name is not reserved because it is shown marked
  "built-in".
- **I-15 A refused built-in change is not audited.** SC-11 lists creating,
  changing and deleting a template; a refused attempt on the built-in template
  changes nothing and is not a read-only refusal.
- **I-16 Hours 1–10.** A lesson is a whole number of teaching hours; 10 is a
  generous bound to catch typing errors, not a business rule.

### Non-blocking findings

- **F-1** `AGENTS.md` Domain Essentials is marked verified against v80;
  `trebovaniya.md` is at v84 (carried from US-025).
- **F-2** The report page shows personal data like the journal; no convention
  requires `Cache-Control: no-store` (US-025 SEC I-1). Owner / SECURITY_REVIEW
  decide, preferably before the export Stories.
- **F-3** API-8 does not name the report view; as for the journal (US-025 I-9),
  it is treated like an export — whole period, not paginated.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-006 | VR-006 | S-01 |
| AC-002 | FR-007, FR-008, FR-009, FR-010, FR-012, FR-014 | VR-001 … VR-005, VR-008 | S-01, S-07, S-14 |
| AC-003 | FR-015 | VR-004 | — |
| AC-004 | FR-004, FR-005, FR-006 | VR-007 | S-05, S-12 |
| AC-005 | FR-003, FR-005.4, FR-008 | — | — |
| AC-006 | FR-013 | — | S-04, S-06 |
| AC-007 | FR-016 | VR-008 | S-09 |
| AC-008 | FR-012 | — | S-01, S-02, S-03 |
| AC-009 | FR-011, FR-019, §6 | VR-001 … VR-007 | S-08, S-10 |
| AC-010 | FR-017 | VR-009 | S-11 |
| AC-011 | FR-004, FR-013 | — | S-04, S-13 |
| AC-012 (derived) | FR-004, FR-005.1 | VR-007 | — |
| AC-013 (derived) | FR-015 | VR-004 | — |
| AC-014 (derived) | FR-014 | — | S-14 |
