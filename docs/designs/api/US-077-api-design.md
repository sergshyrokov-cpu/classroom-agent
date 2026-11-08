---
artifact_type: api_design
story: US-077
version: 1
status: DRAFT
created_at: 2026-11-08T12:32:23Z
updated_at: 2026-11-08T12:32:23Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-077-spec.md
    version: 2
  - path: docs/decisions/US-077-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 113
supersedes: null
---

# US-077 API Design — Fewer Google token requests, and logs that explain failures, before the pilot

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no OpenAPI contract.

**There is deliberately no `docs/designs/api/US-077-openapi.yaml`.** The missing file is this stage's recorded
decision, as in earlier Stories with no API change (US-073, US-075, US-076). Downstream stages that list `openapi`
among their inputs read this document instead.

Delegation: none. The designer read the Specification and `GoogleSignInEvents.cs` directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification states the Story does not change public
API behaviour. Specification v2 (approved at `HUMAN_SPEC_APPROVAL` 2026-11-08) states it:

- **Validation Rules:** "No request body, query parameter or endpoint is added."
- **Non-Functional Requirements:** "No new package, setting, schema, migration, endpoint, translation key, page or
  audit event."
- **Out of Scope:** "Any change to a user-visible text, screen, right, endpoint, schema or audit event."
- **S-07:** "No authorization, session or audit behaviour changes."

## 2. What changes, and why none of it is API contract

- **Token reuse (FR-001…FR-005):** inside `Infrastructure`. `IClassroomReader` is unchanged, and the calls to Google
  keep their endpoint and scopes.
- **Sign-in failure kind (FR-006):** one more value in an application log line. The Google callback keeps its redirect,
  its `TempData` refusal key and its audit row.
- **Event id and EF ordering (FR-007, FR-008):** internal.
- **Versioned static files (FR-009):** the rendered address of the host's own CSS and JavaScript gains `?v=`. These
  files are not API operations, and their paths, method and caching stay as they are.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-006 | none — internal to the Google reader |
| AC-007 | none — the existing Google callback, unchanged for the caller |
| AC-008, AC-009 | none — internal |
| AC-010 | none — existing static files, new query value only |
| AC-011 | none — confirms no route, policy or audit change |

## 4. Auth and error model

Unchanged. No new error body, status code or policy.

## 5. Open findings

`US-077/SPEC_REVIEW/m-1` and `m-2` concern token behaviour inside the reader, not the HTTP contract. They stay OPEN for
test writing and implementation, as the Owner decided at approval. No new finding.
