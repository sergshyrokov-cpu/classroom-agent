---
artifact_type: api_design
story: US-058
version: 1
status: DRAFT
created_at: 2026-10-26T19:50:52Z
updated_at: 2026-10-26T19:50:52Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 99
supersedes: null
---

# US-058 API Design — Synchronization marks items no longer in Classroom and records the courses it skips

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-058-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052 …
US-057 and earlier Stories without API changes. Downstream stages that list
`openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly and checked that
no controller in `src/ClassroomAgent.Web/Controllers` exposes the synchronization
state (none references `SyncState`).

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§7** — "No endpoint, page, role, policy or permission changes."
- **§10** — "API design: no endpoint (API_DESIGN expected NOT_APPLICABLE)."
- **§1 / §10** — nothing is displayed by this Story; the journal, report,
  export, banner and «Стан даних» page belong to the next two v99 Stories.

All changes are inside synchronization (`RunSynchronizationUseCase`) and its
persistence: the "not in Classroom" mark on `CourseWork`, the skip records in
the synchronization state, and the last-successful-read instant on `Course`.
None of them is read by any existing operation or view in this Story.

## 2. Compatibility

No existing operation, DTO, status code or error key changes. Story 2 and
Story 3 of v99 will design the operations and views that show the new data.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-015 | none — verified through synchronization tests (Application / host), not HTTP |

## 4. Open questions

None.

```yaml
result:
  verdict: NOT_APPLICABLE
  stage: API_DESIGN
  story: US-058
  artifact_status: DRAFT
  artifacts:
    - docs/designs/api/US-058-api-design.md
  next_stage: DB_DESIGN
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
```
