---
artifact_type: specification
story: US-043
version: 1
status: DRAFT
created_at: 2026-10-13T18:55:00Z
updated_at: 2026-10-13T18:55:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-043-school-excel-layout.md
    version: null
  - path: trebovaniya.md
    version: 91
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/specifications/US-028-spec.md
    version: 1
supersedes: null
---

# US-043 Specification — Fill a school's own Excel layout file with a template report

## 1. Overview

US-028 exports the report of a template (US-027, names by US-042) into an
`.xlsx` laid out by the program. This Story lets a Dean or Admin attach the
school's **own Excel journal file** to a created template as its **layout**;
the Excel export of that template then fills the layout instead of producing
the program's own (`trebovaniya.md` v91 §3 `ReportTemplate`: "Excel-вёрстка
(файл) подключается к шаблону при экспорте"; §6: "сохранить поддержку Excel (в
т.ч. по существующим шаблонам школы) … шаблоны — конфигурируемый ресурс, а не
хардкод в коде (как было в прототипе)"; §8: "Генерация экспортов Excel/Word по
`ReportTemplate`").

This Story:

- adds **upload / replace** and **remove** of a layout on the page of a created
  template (OD-001 a, OD-003 a), stored in the installation database with the
  template;
- defines the **markers** a school types into cells of its file to say where
  each report field goes (OD-002 a) and the checks a file must pass (OD-004 a);
- makes the existing Excel export fill the layout when the template has one,
  from the **same report** as the screen — nothing computed again;
- adds the installation's first rule for **uploaded files** to
  `security-conventions.md` (OD-005 a).

It adds no Google call, no new role or matrix cell, and no change to the report,
to names, or to the program's own layout.

Delegated during writing (read only): the `trebovaniya.md` lookups (§2, §3, §5,
§6, §7, §8 line ranges) and the code facts below (routes, entity, audit,
renderer, read-only guard) — to `quick-look`. Facts relied on:

- `ReportTemplatesController` serves `/reports/templates…` under
  `EditReportTemplates` / `UseReportTemplates`; the built-in template is the
  key `academic-journal`, not a row.
- `JournalExportController` serves `POST /api/v1/exports/journal-xlsx` under
  `UseReportTemplates`; `ExportJournalCommand` → `IReportRenderer`
  (`ClosedXmlReportRenderer`); the quote-prefix rule lives in
  `Application/Models/Export/WorkbookCell`.
- Audit: `AuditAction.ReportTemplateChanged`, `JournalExported`; read-only
  refusals through `ReportTemplateRefusalAudit` / `IReadOnlyModeGuard`.
- No upload or multipart handling exists anywhere in `src/` yet.

## 2. Business Goal

A school prints its academic journal on its own form — its headers, column
widths, borders, merged title cells, signature lines. The program's own layout
(US-028) does not look like that form, so the school copies the data across by
hand. With a layout attached, the export **is** the school's form, filled. The
prototype did this for one fixed file at hard-coded cells (`exports.py`: row 8,
row 9, column 3, `A2`, `D1`) — the hard-code §6 forbids; here any school supplies
its own file and says itself, with markers, where the data goes.

## 3. Business Flow

### 3.1 Preparing and attaching a layout

1. The Dean takes the school's empty journal form in Excel and types markers
   into it — `{{course}}` where the course name goes, `{{student.name}}` in the
   first student row, `{{lesson.date}}` above the first grade column,
   `{{grade}}` in the first grade cell, and so on (FR-004). The upload page
   lists every marker with its meaning (FR-012).
2. The Dean opens a created template's page in "Reports and templates" and
   uploads the file as its layout.
3. The installation checks the file (FR-003). If it passes, it is stored with
   the template; the template page and the template list show that the template
   has a layout. If not, the page shows a translated message naming the
   problem; nothing is stored.
4. Later the Dean may upload another file (replace) or remove the layout.

### 3.2 Exporting

1. On the report page (US-027 / US-028) the user exports to Excel as today.
2. If the chosen template has a layout, the file returned is the layout with
   the marked places filled from the report on screen (FR-006). Otherwise the
   program's own layout of US-028, unchanged.

## 4. Functional Requirements

### FR-001 A layout belongs to a created template

- A **created** template (US-027 FR-007, FR-008 — a copy of the built-in one
  included) has **zero or one** layout (OD-003 a, Story).
- The **built-in** "Academic journal" never has a layout. Upload, replace and
  remove addressed to the built-in key are refused by `Application` with a
  translated message, whatever the role and mode; not audited — they change
  nothing (as US-027 FR-006; I-1).
- A layout consists of: the file's bytes exactly as uploaded, the file's name
  (VR-002), its size in bytes and the instant of upload. The bytes are never
  re-saved or altered on upload.
- **Template page** (the change page of US-027 FR-009): a "Layout" block shows,
  when there is a layout, its file name, size and upload date and time (school
  time zone) with the actions **Replace** and **Remove**; when there is none,
  "no layout — the program's own layout is used" and the action **Upload**. The
  block also carries the help of FR-012.
- **Template list** (US-027 FR-002): a created template with a layout carries a
  translated "Excel layout" mark (I-2).
- **Changing** a template's settings (US-027 FR-009) does not touch its layout.
- **Copying** a template (US-027 FR-008) does **not** copy its layout; the copy
  starts without one (I-3).
- **Deleting** a template deletes its layout with it, in the same transaction
  (OD-001 a, Story Notes).
- The stored layout is **not downloadable** in this Story (I-4).

### FR-002 The actions

| Action | Method | Purpose |
|---|---|---|
| Upload or replace a layout | `POST`, `multipart/form-data`, one file field | FR-003; replaces the existing layout if there is one |
| Remove a layout | `POST` | removes it; removing when there is none is not an error and changes nothing (I-5) |

- Both are under `/reports/templates/{templateRef}/…` next to the existing
  template routes; API_DESIGN fixes the routes and status codes within API-2,
  API-5, API-6. Both carry the antiforgery token (§8, API-7) and are server-
  rendered form posts of the template page; no JSON endpoint (as US-027 I-12).
- After success the user returns to the template page with a translated
  confirmation; after a refusal the template page is re-rendered with the
  translated message naming the problem (FR-003) — not the host's error page.
- An unknown or malformed `templateRef` → "template not found" / malformed, as
  the template pages of US-027 (VR-006 there). A template deleted meanwhile is
  "template not found".
- Each action is a use case in `Application/UseCases` returning DTOs (AD-8); the
  host only binds the request, limits its size (VR-001) and renders.

### FR-003 Upload checks

Checked in this order; the **first** failing check refuses the upload with its
translated message (I-6); nothing is stored, no audit row is written (except
the read-only refusal), and neither the file nor its name is logged (SC-10):

| # | Check | Refused when | Message names |
|---|---|---|---|
| 1 | Read-only mode (FR-009) | installation is read-only | the read-only reason |
| 2 | Template | built-in key (FR-001), malformed, unknown | the template problem |
| 3 | A file is sent | no file, or an empty file (0 bytes) | "choose a file" |
| 4 | Type by name | the file name does not end in `.xlsx` (case-insensitive) — `.xls`, `.xlsm`, `.xltx`, anything else (OD-004 a) | "only .xlsx files" |
| 5 | Size | more than **1 MB = 1 048 576 bytes** (OD-004 a) | "larger than 1 MB" |
| 6 | Opens as a workbook | not a readable Office Open XML spreadsheet — not a ZIP package, password-protected/encrypted, damaged, another format renamed to `.xlsx` | "not an Excel workbook" |
| 7 | No macros | the package carries a macro (VBA) project, whatever its name says (OD-004 a: macros refused) | "contains macros" |
| 8 | Not oversized inside | the package's total uncompressed size exceeds **20 MB** (I-7) | "too large inside" |
| 9 | Markers | the marker rules of VR-003 are broken | the marker and its place (sheet name and cell address, e.g. `Оцінювання!B9`) |

- The file's content type sent by the browser is not trusted; checks 6–8 look
  at the file itself (I-8).
- The check runs entirely in memory; the file is never written to disk
  (Story Notes, FR-014).

### FR-004 The markers

A **marker** is text in a cell of the layout of the form `{{name}}` (OD-002 a).
Marker names are fixed, ASCII, the same in both UI languages; letters are
matched without regard to case, and spaces just inside the braces are ignored
(`{{ Course }}` = `{{course}}`) (I-9).

**FR-004.1 Single-value markers** — may stand anywhere, any number of times,
alone in a cell or **inside** a longer text (`Курс: {{course}}`):

| Marker | Value (from the report, US-027 FR-005.2 / US-042) |
|---|---|
| `{{course}}` | the course name and section as the report header shows them |
| `{{period}}` | the period as the report header shows it (UI-language format) |
| `{{teachers}}` | the teachers of US-027 FR-005.5 / US-042, joined with ", " |

**FR-004.2 Grading table markers** ("Оцінювання", US-027 FR-005.3) — each
**at most once** in the workbook and **alone** in its cell:

| Marker | Fills | Direction |
|---|---|---|
| `{{student.no}}` | row number 1, 2, 3 … (number) | down, one row per student |
| `{{student.name}}` | the displayed name (US-042 FR-003) | down |
| `{{lesson.date}}` | the lesson date of each report column (Excel date) | right, one column per report column |
| `{{lesson.title}}` | the item's title; for a material, followed by the translated "material" marker as on screen | right |
| `{{grade}}` | the report cell of each student × column (FR-006.3) | down and right |

**FR-004.3 Lesson topics markers** ("Теми занять", US-027 FR-005.6) — each at
most once and alone in its cell:

| Marker | Fills (down, one row per topic) |
|---|---|
| `{{topic.no}}` | row number 1, 2, 3 … (number) |
| `{{topic.date}}` | the lesson date (Excel date) |
| `{{topic.hours}}` | the template's hours per lesson (number) |
| `{{topic.title}}` | the item's title |
| `{{topic.teacher}}` | the teachers joined with ", " |

The "Independent work" and "Teacher's signature" columns of the report are
always empty (US-027 FR-005.6); the school's form keeps them as it made them —
no marker is needed (I-10).

This is the exact field list the Story asked the Specification to fix (Story
"Fields the layout can take"); `{{lesson.title}}` is added because the report's
column header shows the title with the date (I-11). The template name is not a
field (Story list).

**FR-004.4 Where markers are read.** Only from cell values that are text, on
every worksheet. Not from formulas, comments, headers/footers, names, charts or
other parts of the file (I-12). Text that contains `{{`…`}}` but is not a known
marker is an **unknown marker** (VR-003).

### FR-005 Marker placement

Rows and columns must line up so that every student, lesson and grade lands in
the right place (VR-003 enforces it):

- **Grading table.** All grading-table markers are on **one** worksheet.
  `{{student.no}}`, `{{student.name}}` and `{{grade}}` are in **one row** (the
  *student row*); `{{lesson.date}}`, `{{lesson.title}}` and `{{grade}}` are in
  **one column** (the *lesson column*). The lesson markers stand **above** the
  student row; the student markers stand **left** of the lesson column. Any
  subset is allowed (for example names only, no grades).
- **Lesson topics table.** All topic markers are on one worksheet and in **one
  row** (the *topic row*).
- The two tables may be on the same worksheet or on different ones. On the same
  worksheet the topic row must not be the student row, and no topic marker may
  stand in the lesson column (I-13).
- At least **one** marker of any kind must exist in the workbook (OD-004 a).

### FR-006 Filling the layout at export

**FR-006.1 One source of content.** The values come from the report built by
the existing report query exactly as for the screen and for US-028 (US-027
FR-005, US-042 FR-006; US-028 FR-003) — same template, course, period and name
source. Nothing is computed a second time; if the file and the screen differ,
the screen is right (Story Notes). `Application` maps the report into a
**layout fill model** — for every marker kind its typed, translated values
(text, number, date, empty) — and the renderer places them (AD-3: no business
rule in the renderer; AD-4: no ClosedXML type in `Application`).

**FR-006.2 Placement.** Each single-value marker is replaced by its value; when
it stands inside a longer text, the cell becomes that text with the marker
replaced. Each table marker cell receives the first value; further values go
down (students, topics) or right (lessons), **by the rule of OD-006** (open —
how rows and columns are added, and what happens to the school's prepared
rows). Whatever OD-006 decides, every student, lesson and topic reaches the file,
and nothing the school placed below or right of the filled area is overwritten
(AC-003).

**FR-006.3 Cells.**

- Grade cells: exactly the typing of US-028 FR-004.5 (OD-003 a there) — a whole-
  number grade alone is a **number**; anything else is **text** composed as on
  screen; an empty report cell is an empty cell.
- `{{lesson.date}}`, `{{topic.date}}`: Excel **dates**. If the school gave the
  marker cell a date number format, it is kept; otherwise the UI language's
  short date format is applied (as US-028 I-4) (I-14).
- `{{student.no}}`, `{{topic.no}}`, `{{topic.hours}}`: numbers.
- Every text the program writes follows US-028 FR-004.7 (string value, never a
  formula; quote prefix for a leading `=`, `+`, `-`, `@`, tab, CR) and FR-004.9
  (cut at 32 767 characters) (AC-009).
- The cell's own style (font, borders, fill, alignment) is kept; only its value
  and, for dates without a date format, its number format change.

**FR-006.4 Empty states** (US-027 FR-005.7). No column (nothing published, or
all hidden materials): the lesson-marker and grade cells are left empty. No
student: the student-marker and grade cells are left empty. No topic: the
topic-marker cells are left empty. Single-value markers are still filled. The
program writes no "nothing was published" text into a layout — the school's
form has no place for it (I-15). An empty report is exported and audited like
any other (US-028 FR-004.6).

**FR-006.5 No marker survives.** After filling, no marker text remains in the
file.

**FR-006.6 Everything else is the school's.** Every cell without a marker,
every format, column width, row height, merge, formula, data validation,
conditional format, sheet name and order, and every print setting (orientation,
margins, print area, titles, scaling) is kept as in the layout — moved, not
changed, where rows or columns are added (OD-006). Formulas the school wrote stay
formulas; the program writes values only (Story Out of scope).

**FR-006.7 US-028 print settings and orientation.** With a layout, the program
applies **none** of US-028 FR-004.8: the school's own print settings rule. The
orientation input of US-028 FR-001 is still validated (US-028 VR-001) but not
applied, and the report page does not offer the orientation choice when the
chosen template has a layout (I-16).

**FR-006.8 Excel limits.** If filling would need more rows or columns than an
Excel worksheet holds (1 048 576 rows, 16 384 columns), the export is refused
with a translated message; no file, no audit row (I-17).

**FR-006.9 A layout that no longer fits.** The export opens the stored layout
and reads its markers by the same rules as FR-003 checks 6–9. If that fails (a
later program version changed a rule), the export is refused with a translated
message saying the layout must be replaced; no file, no audit row; the stored
layout is not changed (I-18).

### FR-007 The export request and response

- The export endpoint, its inputs, validation, file name, content type,
  `Content-Disposition` and status codes are those of US-028 (FR-001, FR-005,
  FR-006) — unchanged. Whether a layout is used is decided by the template, never
  by a request value.
- The file is built in memory from the stored layout and streamed; never written
  to disk, never stored (US-028 FR-006, Story Notes).
- `Cache-Control: no-store` by the host-wide rule (SC-14, US-040) — on the
  export, the template page and the upload and remove responses alike; nothing
  of its own is added.
- A template without a layout exports **exactly** as US-028 (AC-004).

### FR-008 Authorization

- Upload, replace and remove: **`EditReportTemplates`** (§2 "Создание и
  редактирование шаблонов отчётов" — Admin ✔, Dean ✔; attaching a layout is a
  change of the template, Story). The template page and list stay under their
  current policies; the export under `UseReportTemplates` (US-028 FR-008).
- No new policy, no new matrix cell. Any Admin or Dean may change the layout of
  any created template (§2 v84: templates are shared by the school).
- Deny by default; anonymous requests are sent to sign in; the SC-4 anonymous
  list gains nothing; the endpoint enumeration test classifies the new actions as
  protected. A Dean on the forced password change is refused as everywhere
  (US-012). Teacher and Student have no account (Hard Stop).
- No antiforgery token → `400` (API-7).

### FR-009 Read-only mode

- Upload, replace and remove call `IReadOnlyModeGuard` **first** — before the
  template is looked up or the file is read — and are refused in `Application`
  with `ReadOnlyModeException` and the reason, for all three BR-025 causes
  (AD-6, SC-5). The refusal writes the US-027 FR-013 audit row: action
  `ReportTemplateChanged`, outcome `Refused`, category `ReadOnlyMode`, target the
  template id (null when the reference is the built-in key or malformed) (I-19).
- The export with a layout works in read-only mode; its only write is the
  export audit row (BR-026, US-028 FR-009).
- No use case of this Story opens a Google port, in any mode (SC-8).

### FR-010 Audit

- Upload, replace and remove are **changes of the template** (Story; §5 v84:
  "создание, изменение и удаление шаблона отчёта (фиксируется только id
  шаблона)"): one `AuditEvent` with action `ReportTemplateChanged`, target the
  template id, outcome `Succeeded`, actor and role from the session — nothing
  else (I-20). It is written in the same transaction as the change.
- The row never carries the file, its name, size, markers or any content
  (SC-11, Story Notes).
- A refused upload (FR-003 checks 2–9), a refused built-in, a "not found", and
  removing when there is no layout write no row. The read-only refusal writes
  the row of FR-009.
- The template's last-change time (US-027 FR-002) is updated by each successful
  upload, replace and remove (I-21).
- The export audit row is **unchanged** from US-028 FR-010 (Story).

### FR-011 Persistence

- The layout is stored in the **installation database** (OD-001 a), one per
  created template, deleted with it (cascade). DB_DESIGN chooses its shape
  (for example its own table referencing the template, holding the bytes in a
  `bytea` column, the file name, size and upload instant) under PC conventions,
  with one migration (PC-2).
- The bytes are loaded **only** by the export and by nothing that lists or shows
  templates (the list, the change page) (NFR, I-22).
- The layout is covered by the database backups (DC-13); it is not a mirror
  table and nothing from Google is stored in it.
- No new audit action value is needed (FR-010).

### FR-012 Localization and the help text

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073):
the "Layout" block title, "no layout — the program's own layout is used", the
"Excel layout" list mark, Upload / Replace / Remove, the confirmations, every
refusal message of FR-003, FR-006.8 and FR-006.9, and the **help text**:

- the list of markers of FR-004 with the meaning of each, in the UI language
  (the marker names themselves are never translated);
- the placement rules of FR-005 in plain words;
- the rule about rows and columns decided by OD-006;
- that the file must be an **empty form** — no student names, grades or other
  personal data (Story Notes);
- `.xlsx` only, at most 1 MB, no macros.

Marker values follow US-028 FR-007: program text (the "material" marker, the
program's marks of the built-in-derived template) in the exporting user's UI
language; Google data and text the Dean wrote — in the template or in the
layout — never translated. The existing missing-key test covers the new keys.

### FR-013 Logging

- Successful upload / replace / remove: one `Information` line with the actor's
  account id, the action, the template id and — for an upload — the file size
  in bytes.
- Refused upload: one `Warning` naming the check of FR-003 that failed (and, for
  VR-003, the rule) — never the file name, a marker text, a cell value or any
  content.
- Export with a layout: the US-028 FR-012 line plus "layout used".
- No log line carries the file, its name, or report content (SC-10, NFR-023,
  DC-10).

### FR-014 The upload rule in `security-conventions.md`

Per OD-005 (a) this Story adds a section **"SC-15 Uploaded files"** to
`docs/architecture/security-conventions.md`, derived from this Specification:

- a file is accepted only by an endpoint whose approved Specification defines
  it, under that endpoint's authorization policy and read-only rule;
- the request body is limited at the host before it is read in full; the type
  is checked from the file's content, never from the browser's content type; a
  size limit after decompression applies to packages;
- a file carrying executable content (macros) is refused;
- an uploaded file is never written to the file system, never logged, and its
  name never reaches a log or audit row; it is stored only where its
  Specification says (here: the installation database);
- it is shown or returned only to the roles of its policy;
- the service-account key is never accepted this way (SC-7 unchanged: an
  upload form for a key is still a Critical finding).

## 5. Acceptance Criteria

Story AC-001 … AC-010 keep their ids; AC-011 … AC-014 are derived.

| Id | Criterion | Covered by |
|---|---|---|
| AC-001 | Dean or Admin uploads a valid layout to a created template → the template page and list show it; one audit row `ReportTemplateChanged` with the template id only | FR-001, FR-002, FR-003, FR-010 |
| AC-002 | Export of a template with a layout: the school's file with marked places filled; every value equals the screen for the same inputs; a whole-number grade is a number; unmarked cells, formats, merges, print settings unchanged | FR-006.1–FR-006.7 |
| AC-003 | More students and lessons than the layout prepared → all in the file; what the school placed below and right is kept | FR-006.2 (OD-006), FR-006.6 |
| AC-004 | Template without a layout → exactly the US-028 file | FR-007 |
| AC-005 | Replace → next export uses the new file; remove → the program's own layout; each writes its audit row | FR-002, FR-010 |
| AC-006 | Wrong type, over 1 MB, not a workbook, macros, too large inside, no marker, unknown or misplaced marker → refused naming the problem; nothing stored; no audit row; file and name not logged | FR-003, VR-001, VR-003, FR-013 |
| AC-007 | Read-only (each BR-025 cause): upload, replace, remove refused (audit `Refused`/`ReadOnlyMode`); export with a layout works and writes its audit row | FR-009 |
| AC-008 | Anonymous refused; allowed-role and forbidden-role test for every endpoint | FR-008 |
| AC-009 | Values Excel would read as a formula are written as text with the quote prefix, as US-028 | FR-006.3 |
| AC-010 | Tests synthetic in PostgreSQL, no Google; layouts made by the tests; output checked by reading the workbook | §9 |
| AC-011 | Upload / remove on the built-in template refused with a message; nothing stored; no audit row | FR-001 |
| AC-012 | Copy of a template with a layout has none; delete of a template removes its layout | FR-001, FR-011 |
| AC-013 | Every single-value marker filled wherever it stands (also inside text, several times); no marker text left in the file; empty report fills nothing but single values | FR-004.1, FR-006.2, FR-006.4, FR-006.5 |
| AC-014 | `Cache-Control: no-store` on the new responses (enumeration test) | FR-007 |

## 6. Validation Rules

### VR-001 The upload request

- Exactly one file field; a request with no file or more than one file is
  refused (FR-003 check 3) (I-23).
- The host limits the request body to 1 MB plus a small allowance for the form
  envelope; a larger body is refused before it is read in full, with the "larger
  than 1 MB" message (API_DESIGN fixes the status within API-5 and how the page
  shows it). Check 5 is then applied to the file itself.
- `templateRef`: US-027 VR-006 (the built-in key → FR-001 refusal; malformed;
  unknown → not found).
- Any other form field is ignored and never echoed.

### VR-002 The file name

Used for the type check (FR-003 check 4) and stored for display only. Any path
part a browser sends is dropped (the part after the last `/` or `\`); control
characters are removed; the result is cut to 255 characters; if nothing remains
the upload is refused as "choose a file". It is shown HTML-encoded, is never
used to build a file-system path, a response header or a log line (I-24).

### VR-003 Marker rules (FR-003 check 9)

Refused, naming the marker and the cell (`Sheet!A1`) — the first in sheet
order, then row, then column:

1. no marker at all in the workbook (message without a place);
2. an unknown marker (`{{…}}` that is not one of FR-004);
3. a table marker (FR-004.2, FR-004.3) that is not alone in its cell;
4. a table marker that occurs more than once;
5. grading-table markers on more than one sheet; student markers and `{{grade}}`
   not in one row; lesson markers and `{{grade}}` not in one column; a lesson
   marker not above the student row; a student marker not left of the lesson
   column (FR-005);
6. topic markers on more than one sheet or not in one row; the topic row equal
   to the student row, or a topic marker in the lesson column (FR-005);
7. any further rule OD-006 adds (for option c: an end marker without its start
   marker, or not below / right of it).

The rejected value — the cell text — is shown back to the user only as the
marker it contains, HTML-encoded; never logged (SC-10).

### VR-004 The actor

The actor of the audit row and the UI language come from the session, never
from the request (US-027 VR-008).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | Upload / replace / remove declare `EditReportTemplates`; deny by default; SC-4 anonymous list unchanged | §2, SC-4 |
| S-02 | Allowed-role (Admin, Dean) and forbidden-role (anonymous; Dean on forced password change) tests per endpoint | SC-1, TC-5 |
| S-03 | `POST` with antiforgery; nothing reachable by `GET` changes a layout | §8, API-4, API-7 |
| S-04 | Read-only guard first in `Application`; refusal audited; export still works; no Google port in any mode | AD-6, SC-5, SC-8, BR-026 |
| S-05 | Request body limited at the host; type from content; `.xlsx` only, ≤ 1 MB, ≤ 20 MB unpacked, no macros, opens as a workbook | OD-004 a, FR-003, FR-014 |
| S-06 | The file is processed in memory; never written to disk; stored only in the installation database | OD-001 a, Story Notes, FR-014 |
| S-07 | The file, its name and its content never reach a log or audit row; audit row = template id only | SC-10, SC-11, Story Notes |
| S-08 | The stored layout is read only by the export; not downloadable; shown (name, size, date) only to Admin and Dean | FR-001, Story Notes |
| S-09 | The program writes values only — no formula, quote prefix on dangerous leading characters; the school's formulas kept, not evaluated by the program | US-028 FR-004.7, AC-009 |
| S-10 | Reading the file resolves no external reference (linked file, URL, external entity) and executes nothing; the program does not evaluate formulas | FR-014 (I-25) |
| S-11 | The service-account key is never accepted by an upload (SC-7) | Hard Stop |
| S-12 | No entity in a request, response or view model; the renderer gets the fill model and the layout bytes only | AD-8, AD-4 |
| S-13 | ClosedXML only in `Infrastructure`; no new package (Story Notes; a new one would be a new Open Decision) | AGENTS.md |
| S-14 | `Cache-Control: no-store` by the host-wide rule | SC-14 |
| S-15 | No new outbound destination | SC-13 (Hard Stop) |
| S-16 | `security-conventions.md` gains SC-15 "Uploaded files" | OD-005 a, FR-014 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous / forced password change | Refused per FR-008 |
| Missing or wrong antiforgery token | `400` (API-7) |
| Read-only mode on upload / replace / remove | Refused (`409`, AD-6) with the reason on the template page; audited `Refused` |
| Built-in template | Refused with a translated message; not audited |
| Malformed / unknown / deleted template | Malformed / "template not found" as US-027 |
| FR-003 checks 3–9 | Template page re-rendered with the message naming the problem; status per API_DESIGN within API-5 (expected `400`); nothing stored; no audit row |
| Request body over the limit | Refused as "larger than 1 MB" before the body is read in full |
| Remove when there is no layout | Not an error; nothing changes; no audit row |
| Export: layout cannot be read or its markers break a rule (FR-006.9) | Refused with "replace the layout" message; no file; no audit row |
| Export: Excel row/column limit (FR-006.8) | Refused with a message; no file; no audit row |
| Export: rendering or audit write fails | As US-028: no file; single exception handler (AD-9, API-10) |

## 9. Non-Functional Requirements

- **Memory and time** — a layout ≤ 1 MB (≤ 20 MB unpacked) is opened, filled
  and saved in memory per export; the report query is unchanged (US-027 FR-004:
  fixed number of round trips). The template list and page never load layout
  bytes (FR-011).
- **NFR-073 / NFR-074** — bilingual program text and help; dates in the school
  time zone; period tests in `Europe/Kyiv` (TC-8).
- **NFR-070** — the layout block and its actions usable at phone width.
- **Tests (AC-010)** — data built synthetically in PostgreSQL via
  Testcontainers (TC-2); Google ports substituted (TC-4). Layout files are
  built by the tests in memory (with ClosedXML or as raw packages for the
  malformed, encrypted, macro and oversized cases); the exported workbook is
  opened from the response bytes and its cells, formats, merges and print
  settings are asserted — never compared by bytes. No `.xlsx` is written into
  the repository (Hard Stop). `school-journal-full.xlsx` may be read by a test
  as a realistic layout only after markers are added to an in-memory copy; the
  versioned file is never changed (I-26).
- **NFR-062** — .NET 10, nullable enabled, warnings as errors.

## 10. Out of Scope

- Word layouts (US-029); several templates or courses in one file (US-030).
- AI help mapping fields of an uploaded file (§4 Epic 13).
- Any change to the report (US-027), to names (US-042), to the program's own
  layout (US-028), to the export audit row.
- Layouts for the Meet reports (US-044).
- A layout on the built-in template (OD-003 a); downloading a stored layout
  (I-4); markers in headers/footers, comments or formulas (I-12); a template
  name marker.
- Program-written formulas.

## 11. Open Decisions

See `docs/decisions/US-043-open-decisions.md`.

| Id | Subject | State | Impact |
|---|---|---|---|
| OD-001 | Where the layout lives | Resolved: (a) with the template in the installation DB | FR-001, FR-011 |
| OD-002 | How fields are placed | Resolved: (a) markers in cells | FR-004, FR-005 |
| OD-003 | Which templates | Resolved: (a) created templates only | FR-001, AC-011 |
| OD-004 | Accepted files | Resolved: (a) `.xlsx`, ≤ 1 MB, no macros | FR-003, VR-001 |
| OD-005 | New requirements version first | Resolved: (a) no; SC-15 added in this Story | FR-014 |
| OD-006 | More students / lessons than the layout has rows / columns | **Open — blocks API_DESIGN** | FR-006.2, FR-012, VR-003 rule 7, AC-003 |

### Interpretations

Stated choices the wording leaves open; any may become an Open Decision at
`HUMAN_SPEC_APPROVAL`.

- **I-1** Layout actions on the built-in key are refused and not audited, like
  change/delete of the built-in (US-027 FR-006, I-15 there).
- **I-2** The template list shows an "Excel layout" mark — the Story asks that
  "the template shows that it has a layout".
- **I-3** A copy does not carry the layout: the copy form cannot pre-fill a
  file, and a copy "has no link to the source" (US-027 FR-008). The Dean uploads
  the file again if wanted.
- **I-4** No download of the stored layout in this Story (the Story allows one
  "if the Specification adds one"). The Dean keeps the file they uploaded; one
  fewer path that hands out a stored file.
- **I-5** Remove with no layout is a no-op success, not an error, not audited.
- **I-6** One message per refusal — the first failing check — so the Dean fixes
  one thing at a time with an exact place.
- **I-7** 20 MB unpacked limit: guards against a small file that expands
  enormously in memory; a real journal form unpacks to well under 1 MB.
- **I-8** The browser's content type is ignored; the name and the content decide.
- **I-9** Markers are matched case-insensitively with inner spaces ignored, so
  `{{ Course }}` works; marker names are ASCII and untranslated.
- **I-10** No markers for "Independent work" and "Teacher's signature" — always
  empty in v1 (US-027 FR-005.6).
- **I-11** `{{lesson.title}}` added beside `{{lesson.date}}`; a school that
  wants only dates simply does not use it.
- **I-12** Markers are read from text cell values only.
- **I-13** Both tables may share a worksheet if their rows and columns do not
  collide.
- **I-14** Date cells keep the school's date format, else the UI language's short
  date.
- **I-15** Empty states write no message into the school's form.
- **I-16** With a layout the orientation choice is hidden and not applied; the
  school's print settings rule.
- **I-17** Excel sheet limits refuse the export rather than cut data.
- **I-18** A stored layout that no longer passes the rules refuses the export
  with "replace the layout"; it is never silently exported half-filled.
- **I-19** The read-only refusal uses `ReportTemplateChanged` / `Refused` /
  `ReadOnlyMode`, as US-027 FR-013.
- **I-20** Attach, replace and remove are recorded as `ReportTemplateChanged` —
  "a change of the template" (Story), the §5 list of v84; no new action value.
- **I-21** A layout change updates the template's last-change time.
- **I-22** Layout bytes are not loaded by list or page queries.
- **I-23** A multi-file request is refused, not partly accepted.
- **I-24** File-name sanitising rules of VR-002.
- **I-25** External references in the school's file (links to other workbooks,
  web queries) are kept in the file as the school made them but never followed
  by the program; the program does not recalculate formulas.
- **I-26** Tests may reuse the versioned blank form only as an in-memory source.

### Findings

- **F-1** The real school form (`school-journal-full.xlsx`) draws a fixed
  table — 30 student rows × 24 lesson columns on "Оцінювання", 23 topic rows on
  "Теми занять" — so a course can be larger than the form; a marker gives only
  the table's start. Hence OD-006.
- **F-2** The Story says the template "shows that it has a layout"; the US-027
  change page is the template's page, so the layout block goes there (FR-001).

## 12. Traceability

| AC | FR | VR / S |
|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-010 | VR-001, VR-002, S-01, S-07 |
| AC-002 | FR-006.1–FR-006.7 | S-09, S-12 |
| AC-003 | FR-006.2, FR-006.6 (OD-006) | VR-003 |
| AC-004 | FR-007 | — |
| AC-005 | FR-002, FR-010 | S-07 |
| AC-006 | FR-003, FR-013 | VR-001, VR-002, VR-003, S-05, S-07 |
| AC-007 | FR-009 | S-04 |
| AC-008 | FR-008 | S-01, S-02, S-03 |
| AC-009 | FR-006.3 | S-09 |
| AC-010 | §9 | — |
| AC-011 | FR-001 | — |
| AC-012 | FR-001, FR-011 | S-08 |
| AC-013 | FR-004.1, FR-006.2, FR-006.4, FR-006.5 | — |
| AC-014 | FR-007 | S-14 |
