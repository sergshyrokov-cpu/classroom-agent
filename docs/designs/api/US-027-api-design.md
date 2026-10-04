---
artifact_type: api_design
story: US-027
version: 1
status: DRAFT
created_at: 2026-10-04T19:20:00Z
updated_at: 2026-10-04T19:20:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/decisions/US-027-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 84
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/api/US-019-openapi.yaml
    version: 1
supersedes: null
---

# US-027 API Design — Report templates and the on-screen report

Companion to `docs/designs/api/US-027-openapi.yaml`.

## 1. Scope of the contract

Eight new operations on the installation's public port and one existing page
changed additively:

| Operation | Purpose | Policy | Writes |
|---|---|---|---|
| `GET /reports/templates` | template list, section entry | `UseReportTemplates` | nothing |
| `GET /reports/templates/new` | new-template form | `EditReportTemplates` | nothing |
| `GET /reports/templates/{templateRef}/copy` | copy form | `EditReportTemplates` | nothing |
| `POST /reports/templates` | save new or copy | `EditReportTemplates` | template + audit |
| `GET /reports/templates/{templateRef}/edit` | change form | `EditReportTemplates` | nothing |
| `POST /reports/templates/{templateRef}` | save change | `EditReportTemplates` | template + audit |
| `GET`/`POST /reports/templates/{templateRef}/deletion` | confirm / delete | `EditReportTemplates` | delete + audit |
| `GET /reports` | the report | `UseReportTemplates` | nothing |
| `GET /` (US-008) | gains "Reports and templates" entry | unchanged | nothing |

No `/api/v1` path, no Control Plane change, no private-port change;
`ClassroomAgent.Contracts` gains nothing, `ContractVersion.Current` stays **1**.
No operation calls Google or reads a secret in any mode.

## 2. Decisions this stage made

### 2.1 The `/reports` section prefix

`trebovaniya.md` v84 §4 makes "Reports and templates" a section beside "Google
Workspace", whose screens live under `/workspace` (US-025 api-design §2.1). The
new section gets its own prefix the same way: `/reports`. The section's entry
page — the home-page link — is the template list `/reports/templates`, because
spec §3.1 says the section lists the templates. The report itself is
`/reports`, a short bookmarkable URL:
`/reports?template=academic-journal&courseId=42&from=2026-09-01&to=2026-09-30`.

### 2.2 The built-in key and one reference type

The built-in template is referenced by the fixed key **`academic-journal`**
(spec FR-006 left the form to this stage). It cannot collide with a created
template's id, which is decimal digits only. Path segment `{templateRef}` and
query parameter `template` share one rule (spec VR-006), bound as a string and
parsed in `Application` — not by a route constraint — so that change and delete
of the built-in reach `Application` and are refused there with the translated
message (AC-001), rather than falling into the anonymous catch-all `404`.

### 2.3 Copy saves through create

The copy form (`GET …/{templateRef}/copy`) posts to `POST /reports/templates`.
A saved copy is an ordinary created template with no link to its source and is a
create for audit (spec FR-008, FR-016), so a second save endpoint would be the
same use case under another path. The copy form differs from the new form only
in its pre-filled values.

### 2.4 Delete confirmation is a page

Spec §3.2: "asks for confirmation on the page". `GET …/deletion` renders a
confirmation page naming the template, with a button that posts to the same path.
No script dialog, so it works without JavaScript and is testable as two plain
requests. HTML forms cannot send `DELETE`; the noun `deletion` follows the
US-019 practice of naming what the `POST` creates.

### 2.5 Status codes

| Situation | Status | Body |
|---|---|---|
| Saved, changed, deleted | `302` → `/reports/templates` | list with one-time confirmation (TempData `ReportTemplateMessage`: `Created` / `Changed` / `Deleted`) — Post-Redirect-Get as US-009 … US-019 |
| Field validation failure | `400` | the form re-rendered with entered values and `fieldErrors` |
| Tampered form (unknown enum value, missing state key, bad row index) | `400` | the translated error page `Error.PageExpired` — the legitimate form never sends these; same reasoning and same text as US-039 api-design §2.4, no new string |
| Missing/invalid antiforgery token | `400` | the error page (host-wide) |
| `TemplateMalformed` | `400` | the template list with the message |
| Built-in key on edit/change/delete | `400` `BuiltInNotChangeable` | the template list with the message |
| `TemplateNotFound` | `404` | the template list with the message |
| Read-only mode on a save or delete | `409` | host-wide translated error page naming the BR-025 reason (API-5) |
| Report query malformed / inverted | `400` | the report page with the form and messages (as US-025) |
| Report template or course unknown | `404` | the report page with the form and messages |

**Why `400` for the built-in template, not `403` or `409`.** `403` is reserved
for the role matrix; every forbidden-role test in this project means "this role
may not", and the Admin and Dean *may* edit templates. `409` is the read-only
status in this codebase; reusing it would blur the read-only tests. The request
names a resource that by definition cannot be changed, which is a request
problem — `400` within API-5.

### 2.6 Evaluation order and the read-only guard

For the three writing `POST`s the guard runs **first** inside the use case
(spec FR-013, US-012 api-design §2.8), right after the host's authentication,
authorization and antiforgery:

> guard (`409`) → reference shape (`400`) → built-in (`400`) → existence (`404`)
> → form shape (`400` error page) → field validation (`400` form) → write.

Consequences, stated so TEST_WRITING meets decisions, not surprises:

- A read-only school is told "read-only" even for a malformed reference, the
  built-in key or a deleted template.
- The `Refused` / `ReadOnlyMode` audit row's target id is the reference when it
  parses as a created-template id (whether or not that template exists), and null
  otherwise — for a create, for the built-in key and for a malformed value
  (spec FR-016: "target id null for a create, the template id otherwise").
- A refused change or delete of the built-in template outside read-only mode is
  not audited (spec I-15).

The three `GET` forms, the confirmation page, the list and the report do not call
the guard: they work in read-only mode (spec FR-013).

### 2.7 Form fields bound as strings

`ReportTemplateForm` lives in `Application/Models/Requests`; every field is a
string and is validated in `Application` (US-039 api-design §2.5: MVC enum
binding accepts numbers and ignores case). Field names:

- `name`, `view` (`full`/`short`), `hideMaterials` (`true`/`false`),
  `hoursPerLesson`;
- `scaleMode` (`none`/`ranges`) and `scale[i].from`, `scale[i].to`,
  `scale[i].label`;
- `marks[<State>].kind` (`program`/`own`/`empty`) and `marks[<State>].text` for
  every member of `ReportCellState` — the nine states of spec FR-003;
- `lateMark.kind` (`program`/`own`/`hidden`) and `lateMark.text`.

Own text sent with another `kind`, and scale rows sent with `none`, are ignored
and not stored (spec VR-003, VR-004).

The **field error keys** (`ReportTemplateFieldErrorKey`) are the messages of
spec §6. Two are named by this stage because §6 implies them without wording:
`TextInvalidCharacters` (spec I-13 excludes control characters from every text)
and `ScaleNoRows` / `ScaleTooManyRows` (VR-004's 1–101 rows; the row script lets
a user reach both). A scale error names the row number as submitted and its
bounds, so the user can find it in the table they see.

### 2.8 The scale table needs a small script

Adding and removing scale rows and the 12-point preset button (spec FR-015) are
done by one same-origin script in `wwwroot/js` (precedent:
`copy-instruction.js`). The preset values come from `Application` into the page
(`twelvePointPreset`), never duplicated in the script. Without the script the
existing rows can still be edited and saved.

### 2.9 The report DTO carries keys, not translated text

`Report` (contract components) is the HTML-independent structure of spec FR-005
that US-028 … US-030 reuse. Program texts are carried as keys
(`ReportMark.kind = Program`, `programKey`) and translated at rendering time;
school texts are carried as written (`Own`, scale labels, the template name).
Cells are already resolved through the template (grade through the scale, mark
per state, late, draft, turn-in date), so no renderer re-implements spec FR-005.4.
Program text reuses the US-025 keys (`Journal.Cell.<State>`, `Journal.Mark.Late`,
`Journal.Mark.Draft`, `Journal.Column.Material`, `Journal.Row.Unnamed`).
Rows and columns carry no internal id of a person or item (as US-025 §2.5).

### 2.10 Policies

`UseReportTemplates` and `EditReportTemplates`, one per matrix row (spec FR-012),
added to `InstallationPolicies` beside `ViewJournal`, both granted to Admin and
Dean. Opening a form is under `EditReportTemplates`: the forms belong to the
"create and edit" row.

### 2.11 Language switcher

The report page builds its return path from validated values only (spec FR-011,
S-05). The template pages use their own `GET` path (no query); a `400` form
re-render returns to the form's `GET` path, so switching language there drops
the unsaved values — acceptable, as no convention keeps unsaved form state.

## 3. Operation notes

### `GET /reports`

- Order: authentication → restricted session → `UseReportTemplates` → shape
  (`400`) → existence of template and course (`404`) → build (`200`).
- Absent `template` → built-in; absent `courseId` → form only; absent dates → the
  current month (US-025 FR-007). B and the default month come from one
  `TimeProvider` read.
- Shape errors are collected in parameter order (`template`, `courseId`,
  `from`, `to`, pair); existence is checked only when the shape is valid; both
  unknown template and unknown course give both keys under `404`. A malformed
  value is not echoed.
- Read-only mode: identical; the guard is not called.

### `POST /reports/templates`, `POST /reports/templates/{templateRef}`, `POST …/deletion`

- Order of §2.6. Template, child rows and the audit row in one transaction.
- Uniqueness of the name is checked on save, trimmed, case-insensitive, among
  created templates, excluding the template being changed.
- Two concurrent saves: last wins whole; a save after delete is `404`.

### `GET …/copy`

- The name " (copy)" suffix may exceed 100 characters or collide with an existing
  name; both are reported on save (VR-001), not when the form opens.

## 4. Authentication and authorization model

| Operation | Anonymous | Dean (temporary password) | Dean | Admin | Antiforgery |
|---|---|---|---|---|---|
| `GET /reports/templates` | `302 /sign-in` | `302 /sign-in/change-password` | `200` | `200` | — |
| `GET …/new`, `…/copy`, `…/edit`, `…/deletion` | `302 /sign-in` | `302 /sign-in/change-password` | `200`/`400`/`404` | same | — |
| `POST /reports/templates` | `302 /sign-in` | `302 /sign-in/change-password` | `302`/`400`/`409` | same | required |
| `POST …/{templateRef}`, `POST …/deletion` | `302 /sign-in` | `302 /sign-in/change-password` | `302`/`400`/`404`/`409` | same | required |
| `GET /reports` | `302 /sign-in` | `302 /sign-in/change-password` | `200`/`400`/`404` | same | — |

There is no forbidden signed-in role in v1; the forbidden-role tests are the
anonymous visitor and the restricted session (spec S-02, as US-025). No
per-template ownership check. The SC-4 anonymous list and the antiforgery
exemption list are unchanged.

## 5. Error model

All responses are HTML; there is no API-6 JSON body in this Story. See §2.5.
`500` is the single error page with no detail (API-10).

## 6. Acceptance Criterion → operation map

| AC | Operation(s) | Evidence in the contract |
|---|---|---|
| AC-001 | list, `…/edit`, `POST …/{ref}`, `…/deletion` | `isBuiltIn`, `canChange: false`; `BuiltInNotChangeable` `400` |
| AC-002 | all template operations | create/copy/change/delete `302`; `x-author`; `authorEmail`; no ownership check |
| AC-003 | `POST` create / change; `GET /reports` | `ScaleOverlap`, `ScaleGap`, …; `ReportGrade` `ScaleLabel` / `RawPoints` |
| AC-004 | `GET /reports` | `Report.grading`, `lessonTopics`, `header.teachers` |
| AC-005 | copy, change, `GET /reports` | `ReportCell` resolved by template; built-in defined in code |
| AC-006 | writing `POST`s; all `GET`s | `409` `ReadOnlyRefused`; `x-read-only-mode`; `x-calls-google: false` |
| AC-007 | writing `POST`s; `GET /reports` | `x-writes`; target id rule §2.6; report writes nothing |
| AC-008 | every operation | `x-policy`; `302` anonymous and restricted |
| AC-009 | writing `POST`s; `GET /reports` | `400` with `fieldErrors` / `messageKeys`; value never logged |
| AC-010 | every page | keys for program text; Google and template text as written |
| AC-011 | every operation | `x-calls-google: false`; data from PostgreSQL |
| AC-012 | `GET /reports` | `from`/`to` school time zone; `lessonDate` |
| AC-013 | `GET /reports` | `ReportGrade.ScaleLabel` |
| AC-014 | list, `GET /reports` | `authorState: AccountDeleted` |

## 7. Compatibility

- Additive only: new paths; the home page gains a link; the US-025 journal page
  and its contract are unchanged.
- The US-008 endpoint enumeration test must classify every new path as protected.

## 8. Open questions for later stages

None blocking. Notes:

- **DB_DESIGN** shapes `ReportTemplate` and its child rows, the nullable author
  reference cleared by the purge (spec FR-014), `CourseWork.ScheduledTime`
  (FR-020), the three `AuditAction` members and the `AuditTargetType` value, the
  name-uniqueness mechanism (trimmed, case-insensitive), and the journal-field
  port `IJournalFieldSource` with its fixed query count.
- **TEST_WRITING** asserts the evaluation order of §2.6 including the audit
  target id rule; `400` vs error page for tampered forms; that the report `400` /
  `404` bodies are the report page; that malformed values are not echoed; and
  read-only behaviour for all three BR-025 causes.
- **Non-blocking — caching (spec F-2).** The report page carries students'
  personal data. No convention sets `Cache-Control: no-store`; the Owner /
  SECURITY_REVIEW decide.
- **For the human's attention.** §2.5 (`400` for the built-in), §2.6 (guard
  before the reference check, and the audit target id), §2.7 (the two new field
  error keys) and §2.8 (script for scale rows) are this stage's choices within
  the conventions.
