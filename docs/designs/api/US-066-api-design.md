---
artifact_type: api_design
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T18:32:13Z
updated_at: 2026-10-28T18:32:13Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-066 API Design — Students with the same name are shown with their email

**Verdict: PASS.** No operation, parameter, policy, status code or error body changes. The approved
Specification (FR-003.4) leaves the shape of the student row's optional email to this stage; the
earlier contracts of the journal and report pages (US-025, US-027, US-042) define their view models,
so the additive field is recorded here. Contract: `docs/designs/api/US-066-openapi.yaml`.

Delegation: none. The designer read the Specification, the view-model schemas of
`US-025-openapi.yaml`, `US-027-openapi.yaml` and `US-042-openapi.yaml`, the US-060 api design, and the
current records `JournalRow`, `GradingRow`, `PersonName` and the two Razor views directly.

## 1. Decisions

- **D-1 One optional field `namesakeEmail` on the student row** — `JournalRow` (journal page) and
  `GradingRow` (report page, export, blank). Null on every row that is not a namesake, so a course
  without namesakes renders exactly as today (AC-006).
- **D-2 On the row, not on `PersonName`.** `PersonName` is shared with the report's teachers
  (header and "Lesson topics"). Putting the field on `GradingRow` makes "teachers never carry an
  email" (FR-001, OD-002 a) true by type rather than by a rule someone must remember.
- **D-3 The field carries the email, not the composed text.** `Application` decides *whether* a row
  is a namesake (FR-003.4, AD-3); the composition `<name> (<email>)` (FR-004.1) is a fixed format
  applied where the name is rendered — the Razor row header and `ReportWorkbookMapper.StudentText`,
  which the blank mapper already reuses. Keeping name and email apart lets Razor encode both parts
  (VR-002) and keeps `displayName` unchanged for ordering (FR-005). To keep the format in one place,
  the implementation should expose one composition helper in `Application` used by the mapper and
  the two views; this is a recommendation, not a contract element.
- **D-4 No new translation key** — the format is identical in both languages (FR-004.2, spec I-5).

## 2. Operation-by-operation

| Operation | Source contract | Change |
|---|---|---|
| `GET /workspace/journal` | US-025 | `JournalRow.namesakeEmail`; row header renders the composition |
| `GET /reports` | US-027, US-042 | `GradingRow.namesakeEmail`; row header renders the composition |
| Report export (both layouts) | US-043 | student name cell text = composition when set; note, layout, file name, audit unchanged |

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 | `GET /workspace/journal` |
| AC-002, AC-005, AC-007 | `GET /reports` |
| AC-003 | report export, layout by template |
| AC-004 | report export, group-lessons blank |
| AC-006 | all three |
| AC-008 | `GET /workspace/journal`, `GET /reports` (null field for the namesake without email) |
| AC-009 | `GET /reports` and report export ("left" mark beside the composed name / in the cell note) |

## 4. Auth model

Unchanged: `ViewJournal`, `UseReportTemplates` and the export policy (Admin ✔, Dean ✔); deny by
default; nothing anonymous. The email reaches no new role: the same roles already see it on the course
card and in the report's email variant (spec §7).

## 5. Error model

Unchanged. A missing email is not an error (FR-003.1, spec §8).

## 6. Compatibility

Additive and server-rendered only; `src/ClassroomAgent.Web` has no `[ApiController]`, so no external
client is affected. Positional records gain a trailing optional parameter defaulting to null, so
existing constructions compile unchanged. `ContractVersion.Current` stays 1.

## 7. Open questions

None. No OPEN finding of US-066 in `docs/workflow/findings.jsonl`.
