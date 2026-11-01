---
artifact_type: api_design
story: US-068
version: 1
status: DRAFT
created_at: 2026-11-01T13:36:52Z
updated_at: 2026-11-01T13:36:52Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/decisions/US-068-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 105
supersedes: null
---

# US-068 API Design — Both hosts share one restrained look built on Bootstrap

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no
OpenAPI contract.

**There is deliberately no `docs/designs/api/US-068-openapi.yaml`.** The missing file is this
stage's recorded decision, not a missing artifact; earlier Stories with no API change (US-052 …
US-061, US-063, US-064) were handled the same way. Downstream stages that list `openapi` among their
inputs read this document instead.

Delegation: none. The designer read the Specification directly and checked the static-file and
routing lines of both hosts' `Program.cs` (`UseStaticFiles` with `NoStoreMiddleware.MarkStaticFile`,
`MapControllers`, `MapFallback`).

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the
Story does not change public API behaviour. The approved Specification (v2) states it:

- **§1:** "No endpoint, schema, translation, policy or business rule changes; API and database
  design are expected to be NOT_APPLICABLE."
- **FR-009.2:** "No route, controller, authorization policy, form field, script behaviour, export
  file, audit event or business rule changes."
- **FR-009.3:** "no middleware, no `Program.cs` pipeline line and no content-type mapping changes."
- **§6:** "No user input is added or changed."

## 2. What changes, and why none of it is API contract

- **New static files** in each host's `wwwroot` — `lib/bootstrap/css/bootstrap.min.css`,
  `lib/bootstrap/LICENSE.txt`, `lib/bootstrap/VERSION.txt`, `img/bootstrap/*.svg` (FR-001,
  FR-007). They are served by the existing `UseStaticFiles` pipeline, anonymously (SC-4), with the
  existing security headers and normal caching (SC-14) and the framework's default content types.
  Static files are not operations of the REST API (`api-conventions.md` covers `/api/v1/…`), and
  no route, policy or pipeline line is added.
- **Layouts, views and `site.css`** — server-rendered Razor markup and stylesheets only (FR-002 …
  FR-008). Every page keeps its route, method, policy, status codes, form fields and texts.
- **REST endpoints** (`/api/v1/…`, the exports) — untouched; their request and response bodies,
  status codes, media types and error body stay as they are.
- **Missing static file** (e.g. the browser's request for `bootstrap.min.css.map`) — the existing
  `404` answer, unchanged (spec §8).

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 | existing pages of both hosts (unchanged routes and policies) + `GET` of the two stylesheets through the existing static-file pipeline |
| AC-002, AC-003, AC-004 | existing static-file pipeline serving the new files; existing headers |
| AC-005, AC-006, AC-008 | none — view and stylesheet content |
| AC-007 | none — manual check |

## 4. Open findings

US-068/SPEC_REVIEW/m-2 (which confirmation buttons get the danger colour) concerns presentation, not
the HTTP contract; this stage leaves it OPEN for the stages that build the views.
