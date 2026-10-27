---
artifact_type: api_design
story: US-059
version: 1
status: DRAFT
created_at: 2026-10-27T11:00:00Z
updated_at: 2026-10-27T11:00:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 101
supersedes: null
---

# US-059 API Design — Synchronization marks courses no longer in Classroom or hidden from a new technical account, guards against an empty course list, and skips a course with negative values

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-059-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-052 …
US-058 and earlier Stories without API changes. Downstream stages that list
`openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification directly and listed the
`src/ClassroomAgent.Web` files that reference `WorkspaceConnection`.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§7** — "No endpoint, page, role, policy or permission changes."
- **§10** — "API design: no endpoint (API_DESIGN expected NOT_APPLICABLE)."
- **§1 / §10** — nothing is displayed by this Story; the marks, the empty-list
  flag and the "not visible" technical problem are shown by v99 Stories 2 and 3.

The changes are inside synchronization (`RunSynchronizationUseCase`, the
reader's value conversion in `GoogleClassroomReader`) and its persistence: the
course mark and its instant, the course's last sighting on the list, the
empty-list flag in `SyncState`.

The one change reached through an existing operation is FR-002: saving the
connection (`WorkspaceConnectionController` / `WorkspaceConnectionPageModel` →
`SaveWorkspaceConnectionUseCase`) now also records the instant of the technical
account's last change. Its request, response, status codes, validation, error
keys, authorization (Admin only) and read-only refusal stay exactly as they are
(Specification FR-002.4). The new instant is not returned by any operation or
view in this Story.

## 2. Compatibility

No existing operation, DTO, status code or error key changes. Story 2 and
Story 3 of v99 design the operations and views that show the new data.

## 3. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001 … AC-010, AC-012 … AC-018 | none — verified through synchronization tests (Application / host), not HTTP |
| AC-011 | none new — verified through `SaveWorkspaceConnectionUseCase` (Specification §9 Tests); the existing save operation's contract is unchanged |

## 4. Open findings

`US-059/SPEC_REVIEW/m-1` (the account-change race) concerns the run's instants,
not the HTTP contract; left OPEN for DB_DESIGN / IMPLEMENTATION.

## 5. Open questions

None.

```yaml
result:
  verdict: NOT_APPLICABLE
  stage: API_DESIGN
  story: US-059
  artifact_status: DRAFT
  artifacts:
    - docs/designs/api/US-059-api-design.md
  next_stage: DB_DESIGN
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings: []
  closed_findings: []
```
