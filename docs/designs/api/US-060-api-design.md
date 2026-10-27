---
artifact_type: api_design
story: US-060
version: 1
status: DRAFT
created_at: 2026-10-27T13:14:44Z
updated_at: 2026-10-27T13:14:44Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 102
supersedes: null
---

# US-060 API Design — Marks in the course list, course card, journal, report and export

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-060-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact; earlier Stories with no API change (US-052 …
US-059) were handled the same way. Downstream stages that list `openapi` among their inputs read
this document instead.

Delegation: none. The designer read the Specification directly and listed the Web controllers that
serve the journal export.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the
Story does not change public API behaviour. The approved Specification (v1) states it in four places:

- **§6:** "No new input: no endpoint, query parameter, form or upload changes."
- **§7:** "No endpoint, page, role, policy or permission changes."
- **§8:** "No new error … The export's error contract is unchanged."
- **§9:** "API: no endpoint change; API_DESIGN expected NOT_APPLICABLE."

## 2. What changes, and why none of it is API contract

- **Course list, course card, journal, report.** These are server-rendered Razor pages. The marks
  travel in the Application DTOs that the pages already receive (spec §9, AD-8). No JSON endpoint
  serves them; `src/ClassroomAgent.Web` has no `[ApiController]`.
- **Report export** (`JournalExportController`). The route, method, parameters, authorization
  policy, status codes, media type and error body all stay the same. Only the workbook content
  changes: a fill colour, cell notes and mark texts (FR-007, FR-008). The file's content is not
  part of the HTTP contract.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-012 | none new or changed; existing pages and the existing export endpoint |

## 4. Open findings

The OPEN US-060 findings are US-060/SPEC_REVIEW/m-1 and US-060/SPEC_REVIEW/m-3. They concern the
Specification and the digest, not the API, so this stage closes neither.
