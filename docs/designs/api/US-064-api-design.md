---
artifact_type: api_design
story: US-064
version: 1
status: DRAFT
created_at: 2026-10-29T07:11:27Z
updated_at: 2026-10-29T07:11:27Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-064-spec.md
    version: 1
  - path: docs/decisions/US-064-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-064 API Design — A disabled Dean's open session is refused on the next request

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-064-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact (as for US-052 … US-063). Downstream stages that
list `openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly; the routes, policies and the
`401` behaviour it relies on were already checked against the code at SPEC_REVIEW.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification states that the Story
does not change public API behaviour. The approved Specification (v1) states it:

- **§1:** the Story "is expected to change tests only".
- **§6:** "No new input. Existing validation applies unchanged."
- **§7:** "this Story adds no endpoint and changes no policy."
- **§9:** "No new NuGet package, no migration … API_DESIGN and DB_DESIGN are expected
  `NOT_APPLICABLE`."

## 2. Existing operations the tests exercise (unchanged)

| Operation | Contract owner | Behaviour asserted by US-064 |
|---|---|---|
| `GET /workspace/journal` (Razor page, `ViewJournal`) | US-020 lineage | `200` before disabling; `302` → `/sign-in` after |
| `POST /api/v1/exports/journal-xlsx` (`UseReportTemplates`) | US-028 contract | `200` `.xlsx` before; `401` API-6 body (`error: "unauthorized"`) after |
| `POST /settings/deans/{deanId}/state` (Razor form, `ManageDeanAccounts`) | US-012 | used by the Admin to disable / re-enable |

The `401` for an unauthenticated `/api/v1` request is already part of the US-028 contract
(api-design §2.5, API-6); US-064 asserts it, it does not define it.

FR-008 (a defect fix, if a test fails) would restore this existing behaviour, not change a contract.
Should such a fix need a contract change, the implementation stops and the change goes back through
this stage.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001, AC-003, AC-004 | `GET /workspace/journal` (existing) |
| AC-002 | `POST /api/v1/exports/journal-xlsx` (existing) |
| all | `POST /settings/deans/{deanId}/state` (existing, test setup) |

## 4. Open findings

US-064 has no OPEN findings (SPEC_REVIEW m-1, m-2 closed FIXED at HUMAN_SPEC_APPROVAL).
