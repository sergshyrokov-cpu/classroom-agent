---
artifact_type: api_design
story: US-063
version: 1
status: DRAFT
created_at: 2026-10-28T17:03:27Z
updated_at: 2026-10-28T17:03:27Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-063 API Design — «Сформовано» and «Дані з Google на» in the journal, the report and the Excel export

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-063-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact; earlier Stories with no API change (US-052 …
US-060) were handled the same way. Downstream stages that list `openapi` among their inputs read
this document instead.

Delegation: none. The designer read the Specification directly and checked the routes of the Web
controllers that serve the journal, the report and the export.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the
Story does not change public API behaviour. The approved Specification (v1) states it in four places:

- **§6:** "No new input: no endpoint, query parameter, form or upload changes."
- **§7:** "No endpoint, page, role, policy or permission changes."
- **§8:** "No new error … The export's error contract is unchanged."
- **§9:** "API: no endpoint change; the page view models gain fields as US-060's `Notice` did;
  API_DESIGN expected NOT_APPLICABLE."

## 2. What changes, and why none of it is API contract

- **Journal and report pages** (`JournalController` `GET` `SignInRoutes.Journal`,
  `ReportController` `GET` `SignInRoutes.Report`). Server-rendered Razor pages; the two new lines
  travel in the Application DTOs the pages already receive (`JournalPageModel`, `ReportHeader`;
  spec §1.1, §9, AD-8). No JSON endpoint serves them.
- **Report export** (`JournalExportController`, `POST /api/v1/exports/journal-xlsx`, US-028
  contract). Route, method, parameters, authorization policy, status codes, media type, file name
  and error body stay the same. Only the workbook content changes: a note on the course-name cell
  (FR-004, FR-005). The file's content is not part of the HTTP contract.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-009 | none new or changed; existing pages and the existing export endpoint |

## 4. Open findings

The OPEN US-063 findings are US-063/SPEC_REVIEW/m-1 (attribution of «невідомо») and
US-063/SPEC_REVIEW/m-2 (the same date shown twice next to US-060's notice). They concern the
Specification and the digest, not the API, so this stage closes neither.
