---
artifact_type: api_design
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T11:26:36Z
updated_at: 2026-10-05T11:26:36Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/decisions/US-042-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 86
  - path: docs/designs/api/US-027-openapi.yaml
    version: 1
supersedes: null
---

# US-042 API Design — Names of students and teachers in a template report

Companion to `docs/designs/api/US-042-openapi.yaml`, which extends
`docs/designs/api/US-027-openapi.yaml`.

## 1. Scope of the contract

No operation is added or removed. Existing operations change additively:

| Operation | Change | Policy | Writes |
|---|---|---|---|
| `GET /reports` | optional query `names`; `nameSwitch`; names by FR-003; `report.nameSource` | `UseReportTemplates` | nothing |
| `GET /reports/templates/new` | `values.names` = `profile` | `EditReportTemplates` | nothing |
| `GET /reports/templates/{templateRef}/copy` | `values.names` from the source (`profile` for the built-in) | `EditReportTemplates` | nothing |
| `GET /reports/templates/{templateRef}/edit` | `values.names` from the template | `EditReportTemplates` | nothing |
| `POST /reports/templates` | required field `names` | `EditReportTemplates` | template + audit (unchanged shape) |
| `POST /reports/templates/{templateRef}` | required field `names` | `EditReportTemplates` | template + audit (unchanged shape) |

Unchanged: the template list, the delete pair, the home page, the US-025 journal
and the Meet pages. No `/api/v1` path, no Control Plane or private-port change;
`ContractVersion.Current` stays **1**. No operation calls Google. The
synchronization (US-014) is not an HTTP operation; FR-001 changes only its port
and persistence (DB_DESIGN).

## 2. Decisions this stage made

### 2.1 Parameter and field name `names`, values `profile` / `email`

The spec's indicative name is kept. The same token is the form field and the
query parameter, and the same two lower-case values serve both, so one parser in
`Application` (`NameSource`) validates both (`x-host-wide-rules.name-source`).
Exact, case-sensitive, untrimmed, one occurrence (VR-002). The stored
representation is DB_DESIGN's.

### 2.2 The switch is two links

Spec I-8 left the markup here. Two `GET` links to `/reports` with the current
template reference, course id, dates and `names=<source>`; the current option is
marked (`aria-current`) and is not a link. Links, not a `GET` form, because a
link needs no script, keeps the address bookmarkable, and works at phone width.
The paths are built in `Application` from validated values only (the
`ReturnPath` builder of US-027 gains a `names` argument), so a malformed value
can never reach the page. The switch is shown only with a report (`200` with a
course); on `400`/`404` `nameSwitch` is null.

### 2.3 A bad `names` in the template form is a field error, not a structural `400`

US-027 treats a tampered enumerated field (`view`, `scaleMode` …) as
*structural*: the error page `Error.PageExpired`, nothing echoed. US-042 VR-001
and §8 instead say "refused with the form again and a translated message …
the form again with the values". The approved Specification outranks the US-027
precedent, so `names` is checked at the **field-validation** step: the form is
re-rendered (`400`) with the other values as entered, `values.names` empty (no
option checked — the rejected value is not echoed) and `fieldErrors`
`{field: names, key: NameSourceInvalid}`. Missing, empty, repeated and unknown
values are all this one case. Nothing saved or audited.

### 2.4 The language switcher keeps `names` only when the address had it

FR-005: "the page includes the effective parameter in its own return path".
The return path carries `names` when the request carried a valid `names`, and
omits it when absent. Including the template-derived value instead would pin
the source into the address after a plain form submit and turn a later change of
the template's setting into a silent no-op for that bookmarked address, contrary
to I-4. Either way the language change shows the same source (AC-014).

### 2.5 `names` is validated even without a course

With no `courseId` the page shows the form only; a malformed `names` is still
reported (`400`, `NameSourceMalformed`), consistent with US-027, which reports a
malformed `from`/`to` without a course. Its message key comes last in
`messageKeys` (after the period pair) — `names` is the newest parameter, and the
existing order is not disturbed.

### 2.6 A report-only name kind

`PersonName.nameKind` is today the US-025 `JournalNameKind`
(`FullName`/`Email`/`Unnamed`). The report no longer shows a full name (I-6), and
the journal must not change (spec FR-006), so the report's `PersonName` gets its
own `ReportNameKind` — `Profile`, `EmailLocalPart`, `Unnamed`. `JournalNameKind`
and the journal row stay as they are. `displayName` is the computed text; the
view renders it or the existing "without a name" key.

### 2.7 The effective source in the report DTO

`Report` gains `nameSource` (`profile`/`email`) and `nameSourceOrigin`
(`Page`/`Template`). `nameSource` is what US-028 needs to reproduce the screen;
`nameSourceOrigin` feeds the FR-011 log line, which records both without names.

## 3. Operation notes

### `GET /reports`

- Evaluation order unchanged; `names` joins the query-shape step.
- Effective source: valid `names`, else the selected template's setting (the
  built-in: `profile`).
- Every person (Grading rows, header teachers, "Lesson topics" teachers) gets
  `PersonName` from the one FR-003 function; sorted in `Application` before
  mapping (named by `displayName` in UI-language collation, ties by internal
  id, then unnamed by id).
- Database round trips unchanged: surname and given name arrive with the
  participant rows already read.
- Read-only mode: identical in all three causes; no guard call; no write; no
  audit.
- Logging: the existing `ReportBuilt` line gains the effective source and its
  origin; the existing `ReportQueryRefused` `Warning` names `NameSourceMalformed`
  among the rules, never the value.

### Template form GETs

Only `values.names` is new: `profile` for new; the source's for copy (`profile`
for the built-in); stored value for edit.

### Template saves

- Evaluation order unchanged: authentication → restricted session → policy →
  antiforgery → read-only guard (`409`, audited `Refused`/`ReadOnlyMode`) →
  reference checks (change) → structural shape → field validation (now
  including `names`) → write + audit → `302`.
- The built-in is refused at the reference step as before, so its `profile`
  cannot change (AC-005).
- Audit rows unchanged in shape: template id only, no setting value (FR-007,
  AC-008).

## 4. Acceptance Criterion → operation

| AC | Operation(s) | Contract element |
|---|---|---|
| AC-001 | — (synchronization, not HTTP) | DB_DESIGN / port; out of this contract |
| AC-002 | `GET /reports` | `PersonName`, `ReportNameKind.Profile`/`EmailLocalPart` with `names=profile` |
| AC-003 | `GET /reports` | `ReportNameKind.EmailLocalPart` with `names=email` |
| AC-004 | `GET /reports` | `PersonName` ordering rule |
| AC-005 | form GETs, both saves | `values.names` defaults; built-in refused on change |
| AC-006 | `GET /reports` | `names`, `nameSwitch`, `report.nameSource`; `x-writes: nothing` |
| AC-007 | `GET /reports`; both saves | read-only: report allowed; saves `409` |
| AC-008 | `POST /reports/templates/{templateRef}` | one `ReportTemplateChanged`, template id only |
| AC-009 | all six | allowed Admin, Dean; forbidden anonymous (`302 /sign-in`), restricted Dean (`302 /sign-in/change-password`) |
| AC-010 | `GET /reports`; both saves | `NameSourceMalformed` (`400` page); `NameSourceInvalid` (`400` form); value not logged |
| AC-011 | `GET /reports`; form | translated labels; `displayName` never translated |
| AC-012 | — | test infrastructure (TC-2, TC-4) |
| AC-013 | `GET /reports` | `Profile` with one part; `Unnamed` |
| AC-014 | `GET /reports` | `returnPath` carries valid `names` (§2.4) |

## 5. Authentication and authorization

Unchanged from US-027: installation session cookie; `UseReportTemplates` for the
report, `EditReportTemplates` for the form and saves; Admin ✔ Dean ✔; deny by
default; SC-4 list unchanged; every `POST` keeps the antiforgery token; the
switch is a `GET` and needs none.

## 6. Error model

HTML only, as US-027 (no API-6 JSON body).

| Case | Status | Page |
|---|---|---|
| `names` malformed in the address | `400` | report page, form, `NameSourceMalformed`, no report, no switch |
| `names` missing/invalid in the form | `400` | template form, values as entered, `names` empty, `NameSourceInvalid` |
| Save in read-only mode | `409` | translated error page naming the reason (unchanged) |
| Change of the built-in | `400` | template list, `BuiltInNotChangeable` (unchanged) |
| Not signed in / restricted Dean | `302` | `/sign-in` / `/sign-in/change-password` |

New translation keys (both resource files): `ReportTemplate.Validation.NameSourceInvalid`,
`Report.Validation.NameSourceMalformed`, plus the setting label, the two value
labels and the switch caption (key names left to implementation within the
existing `ReportTemplate.*` / `Report.*` families).

## 7. Compatibility

Additive for every existing bookmark: an address without `names` behaves as
before except that names follow the template's setting (all existing templates
get `profile` in the migration, FR-009). The template form now requires `names`;
the only client is the page itself, which always sends it. `PersonName.nameKind`
changes its enum — an internal DTO with no external consumer yet (US-028 is not
implemented).

## 8. Open questions

None blocking. For the human's attention: §2.3 (form-level message instead of
the US-027 structural error page — follows the spec, departs from precedent) and
§2.4 (`names` kept in the language-switcher path only when the address had it).
