---
artifact_type: specification
story: US-028
version: 1
status: APPROVED
created_at: 2026-10-05T13:36:00Z
updated_at: 2026-10-05T13:55:58Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-028-excel-export.md
    version: null
  - path: trebovaniya.md
    version: 86
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/specifications/US-042-spec.md
    version: 1
supersedes: null
---

# US-028 Specification — Export a journal to Excel using a school template

## 1. Overview

US-027 built the report of a template for a course and a period as a DTO
independent of HTML; US-042 made the names in it selectable. This Story turns
that same report into an `.xlsx` file the user downloads (`trebovaniya.md` v86,
§4 Epic 3: "экспорт строит файл из того же отчёта"; §2 "Просмотр и экспорт
журнала успеваемости" — Admin ✔, Dean ✔).

This Story:

- adds an **export action** to the report page of US-027 FR-011: same template,
  course, period and name source as the report on screen;
- builds the report with the **existing** report query (US-027 FR-005, US-042
  FR-006) — nothing is computed a second time — and maps it in `Application`
  into a **workbook model** (sheets, rows, typed cells, program text already
  translated);
- writes that model to `.xlsx` with **ClosedXML** (OD-001 a) behind the
  `IReportRenderer` port in `Infrastructure/Export` (`architecture.md`,
  `package-map.md`);
- lays the file out itself — one worksheet per part of the report (OD-002 a);
- writes one **audit row** per export (§5, SC-11, BR-073), works in **read-only
  mode** (BR-026), and is a **`POST`** with the antiforgery token (§8 v64,
  API-3, API-4).

It adds no page, no template setting, no table other than what the export audit
row needs, and no Google call.

## 2. Business Goal

A school keeps a paper academic journal. Today it has the report only on screen
and must copy it by hand. The file closes that gap: printed, the built-in
"Academic journal" gives the school the paper version of the electronic journal
(§4 Epic 3 v84); kept, it is the school's own copy — also the copy it takes
before an installation is decommissioned (§5, DC-8: "выгрузку журналов делает
сама школа обычным экспортом").

## 3. Business Flow

1. A Dean or Admin opens "Reports and templates", chooses a template, a course
   and a period, and sees the report (US-027); optionally switches the name
   source (US-042).
2. Next to the report they press **"Export to Excel"** ("Експорт в Excel").
3. The browser sends a `POST` carrying the template reference, course id,
   period and effective name source of the report on screen, with the
   antiforgery token.
4. The installation validates the input exactly as the report page does,
   builds the same report, maps it to the workbook model, renders the file in
   memory, writes the audit row, and returns the file as a download.
5. On invalid input the user sees a translated message naming the problem; no
   file, no audit row.

## 4. Functional Requirements

### FR-001 The export request

- **Inputs** — exactly those of the report page (US-027 FR-011, US-042 FR-005):
  the template reference (built-in key or created template id), the course's
  internal id, the period `from`, `to`, and the name source (optional; absent →
  the template's setting, US-042 FR-004) — plus one input of its own, the
  **page orientation** of the printout (`portrait` / `landscape`; optional,
  absent → `portrait`; OD-004 a).
- **Method: `POST`** with the antiforgery token (`trebovaniya.md` §8 v64:
  "Выгрузка журнала или отчёта — POST с токеном: она пишет аудит"; API-3, API-4,
  API-7; SC-4: a state-changing action reachable by `GET` is a finding).
  The exact route and request shape — the `/api/v1/exports/{kind}` shape of
  API-3 or a form post of the report page — are fixed by API_DESIGN within API-2,
  API-3 and API-6.
- The inputs are internal ids, dates and a source value; **no personal data**
  is sent (Story Notes; US-039 INFO-1).
- The Story Notes describe the export as a `GET`. `trebovaniya.md` §8 outranks
  the Story and requires `POST`; this Specification follows §8 (finding F-1).

### FR-002 The entry point

- The report page shows the action **"Export to Excel"** whenever it shows a
  report — including an empty report (US-027 FR-005.7) — and only then: not on
  the bare form, not after a validation failure.
- The action carries the validated values the page was built from: template,
  course, period and the **effective** name source of the switch (US-042 FR-004,
  FR-005; §4 v86: "Экспорт со страницы отчёта берёт выбранный на ней вариант —
  файл не расходится с экраном").
- Next to it, a choice of page orientation — "Книжкова" / "Portrait"
  (pre-selected) and "Альбомна" / "Landscape" (OD-004 a). The choice is sent
  with the export and **stored nowhere** — not in the template, not on the
  account; the next export starts from portrait again.
- It is shown in read-only mode (BR-026). Showing it is not enforcement: the
  policy and validation of FR-008 and FR-005 decide (AD-6, SC-4).
- Usable at phone width (NFR-070).

### FR-003 One source of content

- The export runs the report of US-027 FR-005 with the US-042 changes, through
  the same `Application` query and the same rules — cells (US-027 FR-005.4,
  FR-015), rows and order (US-042 FR-006), teachers, items and empty states.
  It never recomputes a grade, a mark, a name or an order of its own.
- If the file and the screen ever differ for the same inputs, the screen is
  right and the export is the defect (Story Notes).
- The report is built at one instant B, as on screen (US-027 FR-004); the file
  reflects the data at the moment of export.

### FR-004 The workbook

`Application` maps the report DTO into a **workbook model** that holds
everything the file shows: sheet names, rows, and for every cell its **type**
(text, number, date, empty) and its value with every program text already
translated (FR-007). The renderer writes that model as it is and holds no rule
(AD-3: no business rule outside `Application`). API_DESIGN / DB_DESIGN shape the
model; `IReportRenderer` takes it and returns the file bytes.

**FR-004.1 Sheets.** Two worksheets, in this order, for every template (the
report always has these two parts — US-027 FR-005):

| Sheet | Name (translated) | Content |
|---|---|---|
| 1 | "Оцінювання" / "Grading" | US-027 FR-005.3 |
| 2 | "Теми занять" / "Lesson topics" | US-027 FR-005.6 |

**FR-004.2 Header block.** Each sheet begins with the report header of US-027
FR-005.2 — one row each: template name (translated for the built-in, as written
otherwise), course name and section, the period, the teachers (US-042 FR-006) —
each preceded by a translated label; then one empty row; then the table (I-2).

**FR-004.3 "Grading" table.**

- Header row: a first cell with the translated student column heading, then one
  cell per report column: the lesson date (school time zone, UI-language
  format) and the item's title, and for a material the translated "material"
  marker — the same parts as on screen, in one text cell (I-3).
- One row per student of the report, in the report's order; first cell the
  displayed name (US-042 FR-003); then one cell per column, written by FR-004.5.

**FR-004.4 "Lesson topics" table.** Header row of the six translated headings of
US-027 FR-005.6 (Date, Topic, Hours, Teacher, Independent work, Teacher's
signature), then one row per topic in the report's order:

| Column | Cell |
|---|---|
| Date | the lesson date as an Excel **date** with the UI language's short date format (I-4) |
| Topic | text, the item's title |
| Hours | **number**, the template's hours per lesson |
| Teacher | text, the teachers joined with ", " |
| Independent work, Teacher's signature | empty |

**FR-004.5 Cell content of "Grading".** Each cell holds what the screen shows
for it (US-027 FR-005.4), as one cell:

| Report cell | Excel cell |
|---|---|
| empty (a material, an empty mark) | empty |
| a grade alone — no late mark, draft grade or turn-in date — whose text is a whole number (a scale label such as `12`) | **number** (OD-003 a) |
| anything else — a mark, a raw state, a non-numeric scale label, raw points `8 / 10`, a grade with a late mark, draft grade or turn-in date | **text**, composed exactly as the screen composes it (OD-003 a) |

So the file never differs from the screen; the built-in "Academic journal" and
every scale with numeric labels are calculable; "no conversion" and the full
view stay text, as they are on screen.

**FR-004.6 Empty reports** (US-027 FR-005.7). No item in the period: both
sheets carry the header block and, in place of the table, the translated
"nothing was published in this period". Items but no student: "Grading" carries
the header block and "no students in this period"; "Lesson topics" is filled.
An empty report is exported and audited like any other (row count 0) — it is
not an error.

**FR-004.7 Text values are never formulas.** Every text cell is written as a
string value, never through a formula API, so opening the file executes
nothing. A text that begins with `=`, `+`, `-`, `@`, a tab or a carriage return
— a template name or mark, a scale label, a course name, a title, a name — is
additionally given Excel's "quote prefix" cell style, so that editing the cell
does not turn it into a formula; the value itself is written unchanged, equal
to the screen (Story Notes; SC-10; I-5).

**FR-004.8 Readable printout** (Story: "print settings beyond what the
Specification decides is needed"). Both sheets: the orientation chosen at
export (FR-002; portrait by default — the form of a paper academic journal,
OD-004 a), fit to one page wide,
the table's header row repeated on every printed page; "Grading" also repeats
the student-name column and freezes it with the header row on screen. Column
widths follow the content within a fixed maximum, text wraps in header cells.
No other print setting is offered and none is stored per school or per
template (Story Out of scope; I-6).

**FR-004.9 Excel limits.** A text longer than Excel's cell limit (32 767
characters) is cut to the limit (I-7). Sheet names are the fixed translated
names above, within Excel's 31-character limit.

### FR-005 Validation and not-found

- The inputs are validated **before anything is read for the report**, by the
  same rules and with the same messages as the report page: US-027 VR-007 /
  VR-006 (template), US-025 VR-001 … VR-003 (course, period), US-042 VR-002
  (name source). See §6.
- A malformed value or `from` after `to` → `400`; an unknown template or course
  → `404` (API-5). The user sees a translated message naming the problem; the
  mechanism (the report page re-rendered with the message, or a JSON error per
  API-6 shown by the page) is fixed by API_DESIGN.
- On any failure: no file, no audit row, nothing logged but the field and the
  rule — never the value (SC-10).

### FR-006 The file and the response

- `200` with the file; content type
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`;
  `Content-Disposition: attachment` with the file name of FR-006.1 (API-2).
- `Cache-Control: no-store` by the host-wide rule of US-040 (SC-14); the export
  adds no attribute of its own and the SC-14 enumeration test covers the new
  endpoint.
- The file is built **in memory** and streamed; it is never written to disk,
  never stored in the database, never kept by the installation (Story Notes).

**FR-006.1 File name.** `<course name> <from>–<to>.xlsx`, the dates in
`yyyy-MM-dd` (I-8). Example: `Алгебра 7-А 2026-09-01–2026-09-30.xlsx`.

- Built from the course name and the period only — never a student's or
  teacher's name or email, never the template name.
- In the course name, every character a Windows file name cannot hold
  (`\ / : * ? " < > |` and control characters) is replaced by `_`; runs of
  white space become one space; leading and trailing spaces and dots are
  removed; the course part is cut to 100 characters. If nothing remains, the
  translated word "course" is used.
- Non-ASCII names are sent per RFC 6266 (`filename*` UTF-8, with an ASCII
  fallback `filename`), so a Cyrillic name downloads correctly (AC-005).

### FR-007 Language of the file

- Program text in the file — sheet names, header-block labels, column headings,
  the "material" and "draft" markers, program marks of cell states (including
  the built-in template's), the "without a name" labels, the empty-state
  messages, the file-name fallback — comes from the translation files in the
  **UI language of the exporting user** at the moment of export (NFR-073,
  `package-map.md` `Localization`).
- Dates and numbers inside text cells follow that language exactly as on screen.
- Course names, sections, titles, names, emails and email parts (Google data),
  and every text written into a template — its name, own marks, scale labels —
  are never translated (§4 v86, NFR-073).

### FR-008 Authorization

- The export is covered by **`UseReportTemplates`** (US-027 FR-012: the row
  "Использование шаблонов отчётов" and §2 "Просмотр и экспорт журнала
  успеваемости" — Admin ✔, Dean ✔). No new policy, no new matrix cell.
- Deny by default; anonymous requests are refused (redirect to sign-in for a
  page, `401` for `/api/v1`, per API_DESIGN); the SC-4 anonymous list gains
  nothing; the endpoint enumeration test classifies the export as protected.
- A Dean held on the forced password change is refused as everywhere (US-012).
- No per-course or roster scoping; Teacher and Student have no account (Hard
  Stop).
- A request without a valid antiforgery token → `400` (API-7).

### FR-009 Read-only mode

- The export works for all three BR-025 causes (BR-026: "viewing and exporting
  already-synced data keep working"). It does **not** call
  `IReadOnlyModeGuard`.
- Its only write is the audit row of FR-010 — on the BR-026 closed list ("audit
  rows"); TC-5 requires a test that it is still written in read-only mode.
- No use case of this Story opens a Google port, in any mode (SC-8, BR-026).

### FR-010 Audit

Per §5, SC-11 and BR-073, every **successful** export writes one `AuditEvent`:

| Field | Value |
|---|---|
| Action | new value, name indicative `JournalExported` |
| Actor | the session's account id and role (never a request value) |
| Target | the course (type course, the course's internal id) |
| Outcome | `Succeeded` |
| Details | the period (`from`, `to`), the template (created template id, or a marker of the built-in template — DB_DESIGN chooses), the number of rows, the format (Excel) |

- **The number of rows** is the number of student rows of "Grading" (0 when
  there is none) (I-9).
- The row carries no file content and no personal data — no name, email, grade,
  title, template name or mark (SC-11, SC-10).
- `AuditEvent` has no fields for the details today; DB_DESIGN adds them (PC-2,
  one migration).
- **Order**: the file is rendered first; then the audit row is written and
  committed; only then is the file returned. If the audit row cannot be
  written, no file is returned and the single exception handler answers (AD-9,
  API-10) — an export is never delivered unaudited (§5: "кто выгрузил журнал
  такого-то числа").
- Viewing a report, using the name switch, a validation failure and a
  "not found" write no audit row (AC-007, AC-009).

### FR-011 The renderer port and the package

- `IReportRenderer` (`architecture.md` ports table) is declared in
  `Application/Ports` and implemented in `Infrastructure/Export`. No ClosedXML
  type crosses into `Application` or `Domain` (AD-4).
- **ClosedXML 0.105.1** (latest stable on NuGet on 2026-10-05; OD-001 a) is
  referenced by `ClassroomAgent.Infrastructure.csproj` only. Its transitive
  packages and their licences are listed in the implementation report and
  checked at SECURITY_REVIEW (SC dependency rules).
- The renderer receives the workbook model only — no entity, no `DbContext`,
  no localizer call: every string is already in the model.

### FR-012 Logging

- A successful export: one `Information` line with the actor's account id, the
  template id or built-in key, the course id, the period, the name source and
  its origin (page or template), the row and topic counts, and the file size in
  bytes.
- A validation failure: one `Warning` naming the field and the rule, never the
  value. A not-found: as on the report page.
- No log line carries a name, email, grade, title, template text or any file
  content (SC-10, NFR-023, DC-10).

### FR-013 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073):
the "Export to Excel" action, the orientation choice and its two values, the
orientation validation message, the two sheet names, the header-block labels, the
student column heading of "Grading", the file-name fallback "course", and any
message of §8 that has no key yet. Keys of US-025 / US-027 / US-042 — column
headings of "Lesson topics", markers, program marks, "without a name", empty
states, validation messages — are reused, not duplicated. The existing
missing-key test covers the new keys.

## 5. Acceptance Criteria

| Id | Criterion (Story) | Covered by |
|---|---|---|
| AC-001 | "Academic journal" → `.xlsx` with "Grading" and "Lesson topics" holding exactly what the screen shows | FR-003, FR-004.1–FR-004.6 |
| AC-002 | Any created template, full or short view, changed marks, hidden materials, other scale or hours — reflected exactly | FR-003, FR-004.3–FR-004.5 |
| AC-003 | Converted grades, raw points, empty cells, the "not assigned" dash hold the screen's value; a grade is a number Excel can calculate with | FR-004.5, OD-003 |
| AC-004 | Program text in the UI language; Google data and Dean text as written | FR-007, FR-013 |
| AC-005 | File name from course name and period, no student name or email; invalid characters still give a valid download | FR-006.1 |
| AC-006 | Read-only mode: export works, no Google call, audit row written | FR-009, FR-010 |
| AC-007 | One audit row: action, actor, course id, period, template id, row count; no content, no personal data; viewing writes none | FR-010 |
| AC-008 | Anonymous refused; allowed-role and forbidden-role test for every endpoint | FR-008 |
| AC-009 | Unknown template or course, bad period → no file, message naming the problem, no audit row, value not logged | FR-005, FR-012 |
| AC-010 | `Cache-Control: no-store` on the file response | FR-006 |
| AC-011 | Tests synthetic in PostgreSQL, no Google; file checked by reading the workbook, not by bytes | §9 |

## 6. Validation Rules

### VR-001 Export inputs

The rules of the report page, unchanged, with the same messages:

- `template`: US-027 VR-006 (the built-in key or the id of an existing created
  template; malformed → `400`, unknown → `404`).
- `courseId`, `from`, `to`: US-025 VR-001 … VR-003 (required for an export —
  there is no "form only" state; absent → `400`).
- name source: US-042 VR-002 (absent → the template's setting; any other value
  → `400`).
- orientation: absent → `portrait`; `portrait` or `landscape` exactly; any other
  value → `400` with a translated message naming the field (OD-004 a).
- A repeated parameter is malformed, as on the report page. Any other input is
  ignored and never echoed.

### VR-002 The actor

The audit actor and the UI language are taken from the session, never from the
request (US-027 VR-008).

### VR-003 Google and template values in the file

Written as values only, never as formulas (FR-004.7); cut to Excel's limit
(FR-004.9); never translated (FR-007).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The export declares `UseReportTemplates`; deny by default; SC-4 anonymous list unchanged. | SC-4, §2, API-9 |
| S-02 | Allowed-role (Admin, Dean) and forbidden-role (anonymous; Dean on forced password change) tests. | SC-1, TC-5 |
| S-03 | `POST` with antiforgery; never reachable by `GET`. | §8 v64, API-4, API-7, SC-4 |
| S-04 | No Google port opened, no secret read, in any mode; works in read-only mode with the audit row written. | SC-5, SC-8, BR-026, TC-5 |
| S-05 | Input validated before any report read; rejected value never in a log, audit row or error detail. | §8, SC-10 |
| S-06 | One audit row per export: ids, period, row count, format — no content, no personal data; written before the file is returned. | §5, SC-11, BR-073 |
| S-07 | The file is built in memory; never written to disk or stored. | Story Notes, SC-10 |
| S-08 | No text cell is a formula; dangerous leading characters get the quote prefix. | Story Notes, SC-10 |
| S-09 | File name carries no personal data; no personal data in the request. | Story, US-039 INFO-1 |
| S-10 | `Cache-Control: no-store` by the host-wide rule. | SC-14, §8 v85 |
| S-11 | No entity in a request or response; the renderer gets the workbook model only. | AD-8, AD-4 |
| S-12 | ClosedXML only in `Infrastructure`; transitive packages and licences reviewed. | OD-001, AGENTS.md |
| S-13 | No new outbound destination. | SC-13 (Hard Stop) |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Refused per FR-008 (sign-in redirect or `401`). |
| Dean on the forced password change | Refused as everywhere (US-012). |
| Missing or wrong antiforgery token | `400` (API-7). |
| Malformed input, `from` after `to` | `400`; translated message naming the problem; no file; no audit row. |
| Unknown template or course | `404`; translated "not found" message; no file; no audit row. |
| Empty report | Not an error: the file of FR-004.6, audited. |
| Read-only mode | Not an error: the export works (FR-009). |
| Rendering or audit write fails | No file; the single exception handler answers with no detail (AD-9, API-10); nothing audited as succeeded. |

## 9. Non-Functional Requirements

- **Same queries as the report** — a fixed number of round trips (US-027
  FR-004); exports are not paginated (API-8).
- **In memory** — one course and one period per file keeps the workbook small.
- **NFR-073 / NFR-074** — bilingual program text; dates in the school's time
  zone; period-boundary tests use `Europe/Kyiv` (TC-8).
- **NFR-070** — the action is usable at phone width.
- **Tests (AC-011)** — data built synthetically in PostgreSQL via Testcontainers
  (TC-2), Google ports substituted (TC-4); the workbook is opened from the
  response bytes in memory and its sheets and cells are asserted; no file is
  written into the repository (Hard Stop: no generated `.xlsx` committed).
- **NFR-062** — .NET 10, nullable enabled, warnings as errors.

## 10. Out of Scope

- Uploading and filling a school's own Excel layout (OD-002 → US-043).
- Word (US-029), several templates or courses in one file (US-030), export of
  the US-025 journal page or of Meet statistics.
- Any change to the report, its templates, names or synchronization.
- A screen to view audit rows (EPIC-9).
- Per-school print configuration.

## 11. Open Decisions

See `docs/decisions/US-028-open-decisions.md`.

| Id | Subject | State | Impact |
|---|---|---|---|
| OD-001 | Excel library | Resolved: ClosedXML | FR-011 |
| OD-002 | Who supplies the layout | Resolved: the program | FR-004, §10 |
| OD-003 | Which "Grading" cells are numbers | Resolved: (a) a whole-number grade alone is a number, everything else text as on screen | FR-004.5, AC-003 |
| OD-004 | Page orientation of the printout | Resolved: (a) chosen at export, portrait by default, stored nowhere | FR-001, FR-002, FR-004.8, VR-001 |

### Interpretations

Stated choices the wording leaves open; any may be turned into an Open
Decision at `HUMAN_SPEC_APPROVAL`.

- **I-1** The export accepts the same inputs as the report page plus the
  orientation of OD-004 and nothing more; one course, one period, one template
  per file (Story).
- **I-2** The header block is repeated on both sheets, so each prints on its
  own.
- **I-3** A "Grading" column header is one text cell (date and title together,
  as on screen), not two header rows.
- **I-4** "Lesson topics" dates are real Excel dates (sortable), shown in the
  UI language's short date format; the screen shows the same date.
- **I-5** Quote prefix for text starting with `=`, `+`, `-`, `@`, tab, CR —
  defence in depth; the value is not altered.
- **I-6** Print settings of FR-004.8 other than the orientation are fixed by
  the program.
- **I-7** Cutting text at 32 767 characters; Google titles never reach it in
  practice.
- **I-8** File name form and character rules of FR-006.1; the template name is
  not part of it (the Story names course and period only).
- **I-9** "Number of rows" in the audit row = student rows of "Grading".

### Findings

- **F-1** The Story Notes call the export a `GET`; `trebovaniya.md` §8 v64 and
  API-3 / API-4 / SC-4 require `POST` with the token. §8 wins; the Story's
  concern — no personal data in the address — is met because the request
  carries only ids, dates and the source value.

## 12. Traceability

| AC | FR | VR / S |
|---|---|---|
| AC-001 | FR-003, FR-004.1–4.6 | — |
| AC-002 | FR-003, FR-004.3–4.5 | — |
| AC-003 | FR-004.5 (OD-003) | — |
| AC-004 | FR-007, FR-013 | VR-003 |
| AC-005 | FR-006.1 | S-09 |
| AC-006 | FR-009, FR-010 | S-04 |
| AC-007 | FR-010 | VR-002, S-06 |
| AC-008 | FR-008 | S-01, S-02, S-03 |
| AC-009 | FR-005, FR-012 | VR-001, S-05 |
| AC-010 | FR-006 | S-10 |
| AC-011 | §9 | — |
