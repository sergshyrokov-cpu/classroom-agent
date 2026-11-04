---
artifact_type: api_design
story: US-073
version: 1
status: DRAFT
created_at: 2026-11-04T15:19:34Z
updated_at: 2026-11-04T15:19:34Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-073-spec.md
    version: 4
  - path: docs/decisions/US-073-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 110
supersedes: null
---

# US-073 API Design — The eye shows only a password the person entered

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no OpenAPI contract.

**There is deliberately no `docs/designs/api/US-073-openapi.yaml`.** The missing file is this stage's recorded decision, not a missing artifact, as in earlier Stories with no API change (for example US-069 and US-072). Downstream stages that list `openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` as optional when the approved Specification states that the Story does not change public API behaviour. The approved Specification (v4, Overview) states it: "Only the host's script and its source tests change. No endpoint, schema, policy, form field, view text or translation entry changes."

## 2. What changes, and why none of it is API contract

- **The eye's rules** (FR-002 to FR-007): browser-side behaviour of the static file `wwwroot/js/password-toggle.js` in each host.
- **Requests:** the script sends nothing (FR-008.2), and the control contributes nothing to a request (Validation Rules). The 9 password fields keep their names and server-side rules, so the forms that carry them post exactly what they post today.
- **Anonymous access:** the pages keep loading the same static file (SC-4), with no change to a route, policy or status code.
- **REST endpoints** (`/api/v1/…`): untouched.

## 3. Acceptance Criterion → operation map

No Acceptance Criterion maps to a new or changed operation. AC-001 to AC-006 and AC-008 are browser behaviour. AC-007 holds the existing sign-in and password operations unchanged, and existing tests prove it.

## 4. Open findings

US-073 has no OPEN findings.
