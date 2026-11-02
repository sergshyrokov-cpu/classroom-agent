---
artifact_type: api_design
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T11:49:14Z
updated_at: 2026-11-02T11:49:14Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 106
supersedes: null
---

# US-069 API Design — Small interface text fixes and a show-password control

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-069-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact, as in earlier Stories with no API change (for
example US-068). Downstream stages that list `openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the
Story does not change public API behaviour. The approved Specification (v3) states it:

- **Out of Scope:** "this Story **does not change public API behaviour** (no endpoint, parameter,
  status code or response body beyond the view text above)".
- **Out of Scope:** no change of "any route, policy, form field name, export file, audit event or
  business rule".
- **Validation Rules:** "No input is added and no validation changes."

## 2. What changes, and why none of it is API contract

- **Razor views and translation files** (FR-001 … FR-005): server-rendered text and markup. Every
  page keeps its route, method, policy, status codes and form field names.
- **The Meet reports prompt** (FR-004): the first option keeps `value=""`, so a request with no
  course is the same request as today, with the same answer.
- **The control** (FR-005.2): `type="button"` with no `name`, `value` or `form` attribute, so a form
  sends exactly the fields it sends today. AC-008's rendered-form test holds this.
- **New static files** (FR-005.5, FR-008.1): `js/password-toggle.js` and `img/password/*.svg` in each
  host's `wwwroot`, served by the existing static-file pipeline (SC-4, SC-14). Static files are not
  operations of the REST API (`api-conventions.md` covers `/api/v1/…`).
- **REST endpoints** (`/api/v1/…`, the exports): untouched.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001, AC-002, AC-003, AC-004 | existing pages, unchanged routes and policies; view text only |
| AC-005, AC-006, AC-007 | none; browser behaviour on existing pages |
| AC-008 | existing pages and headers; the existing static-file pipeline serves the new files |
| AC-009 | existing sign-in and password operations, unchanged |
| AC-010 | none; translation files |
| AC-011 | none; manual check |

## 4. Open findings

US-069/SPEC_REVIEW/m-4 concerns the wording of the Russian digest, not the HTTP contract; this stage
leaves it OPEN.
