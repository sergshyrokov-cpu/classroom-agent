---
artifact_type: api_design
story: US-055
version: 1
status: DRAFT
created_at: 2026-10-25T11:20:00Z
updated_at: 2026-10-25T11:20:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/decisions/US-055-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 97
supersedes: null
---

# US-055 API Design — Points and grades of any size are stored as Google returned them

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-055-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052,
US-053, US-054 and earlier Stories without API changes. Downstream stages that
list `openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "It changes the type of the three columns, ships the migration, and
  changes nothing else: no requirement, no endpoint, no user-visible text, no
  rule of the run (OD-001 a)."
- **§7 S-01** — "No endpoint, page, policy, route or role check is added or
  changed."
- **§8** — the only error-handling change is that PostgreSQL error 22003 no
  longer occurs for a value that fits `decimal`; no new error path.

## 2. What the Story touches, and why it is not a contract change

- **Storage only.** Three column types (`course_work.max_points`,
  `submission.assigned_grade`, `submission.draft_grade`) and one migration —
  DB_DESIGN's work.
- **Journal and report pages** (Razor, US-020/US-027/US-028): they already carry
  `decimal?` points in their DTOs (`JournalCell`, `JournalColumn`,
  `ReportGrade`); the CLR type, the formatting and the routes are unchanged
  (FR-005, I-3). A larger value is a different value of the same field, not a
  schema change.
- **Report export** (`JournalExportController`, under `/api/v1`, US-022/US-043):
  request body, validation, status codes and error bodies are unchanged; the
  produced `.xlsx` carries the stored value through the existing
  `ReportWorkbookMapper` formatting (AC-006a). The file content is not part of
  the JSON contract.
- No Google call, scope, policy or anonymous route changes (S-01, S-02).

## 3. Acceptance Criterion → operation map

| AC | Operation | Contract change |
|---|---|---|
| AC-001 … AC-005 | none (synchronization run, migration) | — |
| AC-006 | existing journal and report Razor `GET` pages | none |
| AC-006a | existing report export endpoint (US-022 / US-043 contract) | none |

## 4. Open questions

None.

## 5. Delegation note

No lookups were delegated; a direct grep of `src/ClassroomAgent.Web` confirmed
that no controller handles grades or points beyond passing existing DTOs.
