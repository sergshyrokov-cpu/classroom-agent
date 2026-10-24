---
artifact_type: api_design
story: US-054
version: 1
status: DRAFT
created_at: 2026-10-24T20:25:00Z
updated_at: 2026-10-24T20:25:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 97
supersedes: null
---

# US-054 API Design — The program's texts describe the technical account as a super-admin with blocked sign-in

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-054-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052,
US-053 and earlier Stories without API changes. Downstream stages that list
`openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "This Story changes **texts only** … No endpoint, schema, scope,
  access-check step, failure classification or authorization rule changes."
- **§7** — "No authorization change: the instruction stays Admin-only
  (`ViewConnectionInstruction`, US-010 FR-011), the connection page and access
  check keep their policies (SC-4, deny by default)."
- **§8** — "No new error paths. FR-004 changes only the advice text of an
  existing outcome."
- **§10** — "No new requirements version, NuGet package, endpoint, schema or
  migration."

## 2. What the Story touches, and why it is not a contract change

- **Translation values** (`SharedResource.uk.resx` / `.en.resx`): new keys for
  the technical-account statements (FR-001), two keys removed (FR-002), three
  values reworded under kept keys (FR-001 #2, FR-003, FR-004). Rendered text is
  not part of any HTTP contract; no DTO field, route, status code or error body
  carries a translation key.
- **Connection-instruction page** (Razor `GET`, Admin-only, US-010): the
  `<pre id="connection-instruction">` gains a nested list for the measures
  (I-3). Markup only; the page model DTO and the route are unchanged. The markup
  hook `#connection-instruction` that `copy-instruction.js` reads stays (FR-005).
- **Connection page and access check** (Razor, Admin-only, US-009/US-011/US-017):
  the hint and the `TechnicalAccountCannotRead` outcome text change; the outcome
  enum, `SyncDiagnosis` mapping and every request/response shape are unchanged.
- Nothing is under `/api/v1`.

## 3. Delegation

Nothing was delegated; the approved Specification and the US-053 precedent were
read directly.
