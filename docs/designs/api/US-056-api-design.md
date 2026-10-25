---
artifact_type: api_design
story: US-056
version: 1
status: DRAFT
created_at: 2026-10-25T17:20:00Z
updated_at: 2026-10-25T17:20:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 98
supersedes: null
---

# US-056 API Design — Members seen when a course is first loaded count from the course's creation

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-056-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-055 and
earlier Stories without API changes. Downstream stages that list `openapi` among
their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v3) states it:

- **§1** — "It changes no endpoint, no screen markup, no text and no observed
  date; what the pages list, and the order of the unassigned Meet codes list,
  follow the new start (FR-006)."
- **FR-006** — "No endpoint, user-visible text, translation entry, audit event,
  log event or Google call is added or changed, and no page's markup changes."
- **§7** — no new endpoint, page, role, permission or policy.
- **§8** — no new failure.

## 2. What the Story touches, and why it is not a contract change

- **Which memberships count** for a period or a date (FR-001, FR-003). That
  changes *which rows* the existing pages and exports contain, not the shape of
  any request, response, route or status code.
- **Journal, template report, Meet report pages** (Razor `GET`s,
  `MeetReportsController`, journal and report controllers): routes, query
  parameters, view-model DTOs and authorization unchanged.
- **Exports** (`JournalExportController`, `MeetExportController` under
  `/api/v1`): request bodies, validation, status codes and error bodies
  unchanged; the produced files list members per the new start.
- **Unassigned Meet codes list** (`MeetCodesController` `GET`, via
  `GetMeetCodesQuery` → `IMeetCodesReadSource.GetUnassignedPageAsync`): the order
  rule "codes with a candidate first" and the paging contract stay; only which
  codes have a candidate follows the new start (FR-003.5).
- **Automatic Meet code linking** runs inside synchronization — no endpoint.
- **Upgrade migration** (if DB_DESIGN needs one) — deployment, no endpoint.

## 3. Acceptance Criterion → operation map

| AC | Operation | Contract change |
|---|---|---|
| AC-001, AC-005, AC-006, AC-007, AC-008, AC-009, AC-011 | existing course journal Razor `GET` | none |
| AC-002 | existing template report page and export endpoints | none |
| AC-003, AC-007 | existing Meet report page (`MeetReportsController`) | none |
| AC-004 (1st para) | none (scoring inside synchronization) | — |
| AC-004 (2nd para) | existing unassigned Meet codes page (`MeetCodesController` `GET`) | none (row order only) |
| AC-010 | none (synchronization, leaver expiry) | — |

## 4. Open questions

None.

## 5. Delegation note

No lookups were delegated; a direct ripgrep of `src/ClassroomAgent.Web`
controllers and of `GetUnassignedPageAsync` callers confirmed that the affected
data reaches users only through existing routes and DTOs.
