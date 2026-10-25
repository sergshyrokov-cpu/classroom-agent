---
artifact_type: api_design
story: US-057
version: 1
status: DRAFT
created_at: 2026-10-25T18:19:24Z
updated_at: 2026-10-25T18:19:24Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-057-spec.md
    version: 1
  - path: docs/decisions/US-057-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 98
supersedes: null
---

# US-057 API Design — A course with points beyond the program's range is skipped, and the school scale never fails a report

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-057-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052 …
US-056 and earlier Stories without API changes. Downstream stages that list
`openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§7** — "No endpoint, policy, role or audit event changes (SC-4, SC-11
  unaffected)."
- **§6 VR-002** — "No user input, request body, route or query parameter is
  added or changed."
- **§9** — "no migration; no schema, endpoint or UI change."

## 2. What the Story touches, and why it is not a contract change

- **Synchronization run** (FR-001 … FR-004): the Google reader, the run use case
  and the host's log lines. A background run, not an HTTP operation; the new
  outcome list and log event 5135 are internal.
- **Report on screen and export** (FR-005): `ReportBuilder.Grade` computes the
  same `ReportGrade` (`ScaleLabel` / `RawPoints`) as today for every pair that
  does not overflow. The Razor report page and the export endpoint
  (US-022 / US-043 contract under `/api/v1`) keep their routes, request bodies,
  validation, status codes and error bodies. The only observable difference is
  that an extreme stored pair no longer ends in the generic unexpected-error
  response — that response was never a documented outcome of the contract, so
  removing its cause changes no contract element.
- No Google call, scope, policy or anonymous route changes (§7).

## 3. Acceptance Criterion → operation map

| AC | Operation | Contract change |
|---|---|---|
| AC-001 … AC-005, AC-009, AC-010 | none (synchronization run) | — |
| AC-006, AC-007 | existing report Razor `GET` page; existing report export endpoint | none |
| AC-008 | existing report Razor `GET` page | none |

## 4. Open questions

None.

## 5. Delegation note

No lookups were delegated; the reviewer of the Specification already verified
that `ReportBuilder.Grade` is the single percent computation and that the
reader's port methods have no caller outside `RunSynchronizationUseCase`.
