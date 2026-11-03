---
artifact_type: api_design
story: US-072
version: 1
status: DRAFT
created_at: 2026-11-03T20:31:43Z
updated_at: 2026-11-03T20:31:43Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-072-spec.md
    version: 1
  - path: docs/decisions/US-072-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 109
  - path: docs/designs/api/US-039-openapi.yaml
    version: 1
supersedes: null
---

# US-072 API Design — Control Plane error page without the language switcher

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no OpenAPI contract.

**There is deliberately no `docs/designs/api/US-072-openapi.yaml`.** The missing file is this stage's recorded decision, not a missing artifact, as in earlier Stories with no API change (for example US-069). Downstream stages that list `openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification and the US-039 contract directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the Story does not change public API behaviour. The approved Specification (v1, section 13) states it: "this Story does not change public API behaviour: no endpoint, method, status code or response contract is added or changed; only the markup of the error page loses the switcher."

## 2. What changes, and why none of it is API contract

- **The switcher's rule** (FR-001, FR-002): a server-side view decision in the Control Plane. The error page's route, status codes and text do not change.
- **The language action** (`POST /account/language`): its contract, including the `400` with the error page on a refused code, stays as US-039 defines it.
- **Existing contract text:** US-039's `x-ui-elements.LanguageSwitcher.not-rendered-on` already lists "the error page of both hosts". This Story makes the Control Plane conform to it, so that contract needs no amendment.
- **REST endpoints** (`/api/v1/…`): untouched.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 | existing protected `POST` operations and `POST /account/language`, unchanged; `400` with the error page |
| AC-002 | existing `404` by re-execution, unchanged |
| AC-003 | as AC-001; the page's language, unchanged |
| AC-004 | existing Owner pages and `POST /account/language`, unchanged |

## 4. Open findings

US-072 has no OPEN findings.
