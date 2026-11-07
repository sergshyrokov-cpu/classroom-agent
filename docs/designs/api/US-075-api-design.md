---
artifact_type: api_design
story: US-075
version: 1
status: DRAFT
created_at: 2026-11-07T18:47:29Z
updated_at: 2026-11-07T18:47:29Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 113
supersedes: null
---

# US-075 API Design — A short hint under each kind of data problem, and banner links to each section

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so this stage produces no OpenAPI
contract.

**There is deliberately no `docs/designs/api/US-075-openapi.yaml`.** The missing file is this stage's recorded
decision, as in earlier Stories with no API change (US-068, US-069, US-073, US-076). Downstream stages that list
`openapi` among their inputs read this document instead.

Delegation: none. The designer read the Specification, `SignInRoutes.cs`, `DataHealthController.cs`,
`Views/DataHealth/Index.cshtml` and the `DataHealth.*` keys of `SharedResource.uk.resx` directly.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification states the Story does not change
public API behaviour. Specification v2 (approved at `HUMAN_SPEC_APPROVAL` 2026-11-07) states it:

- **Overview:** "API design and database design are expected to be empty deltas — no endpoint, schema, DTO field
  or audit event is added."
- **VR-001:** "The page and the banner take no new input: no request field, query parameter or route value is
  added. The existing confirmation form keeps its validation (US-061)."
- **SEC-001:** "Authorization is unchanged … No new endpoint."
- **Out of Scope:** "Any new data, right, endpoint, DTO field, schema change or audit event."

No route, method, status code, response body, policy or form field changes. The page keeps
`GET /workspace/data-health` (`SignInRoutes.DataHealth`) and the Admin's
`POST /workspace/data-health/course-deletion-confirmation` (`SignInRoutes.DataHealthCourseDeletionConfirmation`,
US-061) as they are, with their policies.

## 2. What changes, and why none of it is API contract

- **Banner links (FR-001):** plain `<a href>` links to fragments of the existing page —
  `/workspace/data-health#data-health-technical` and `#data-health-teaching`. A fragment is never sent to the
  server; the section ids already exist in `Index.cshtml`. No new request shape.
- **Hints (FR-002…FR-006):** fixed texts from translation files rendered inside the existing page response,
  chosen by kind, the viewer's role, the section and the read-only flag the page model already carries
  (`IsAdmin`, `ReadOnly`; VR-002). No new DTO field is needed: the role and read-only state are already on the
  page's view model.
- **Styling (FR-001.4, FR-009):** classes in the host's own stylesheet (SC-15).

This is presentation, not an HTTP contract.

## 3. Translation keys

The Specification's key names (FR-008) are kept: they follow the existing `DataHealth.Hint.*` and
`DataHealth.Technical.*` pattern and collide with no existing key. Checked against the current resx:

| New key | Neighbour already present | Collision |
|---|---|---|
| `DataHealth.Hint.AboveMaximum` | `DataHealth.Teaching.AboveMaximum` (heading) | no |
| `DataHealth.Hint.ItemsNotInClassroom` | `DataHealth.Hint.NotInClassroom` (T4, stays), `DataHealth.Teaching.ItemsNotInClassroom` (heading) | no |
| `DataHealth.Hint.NoRecentSuccess` | `DataHealth.Technical.NoRecentSuccess` (title) | no |
| `DataHealth.Hint.NoSuccessYet` | `DataHealth.Technical.NoSuccessYet` (title) | no |
| `DataHealth.Hint.CourseListEmpty` | `DataHealth.Technical.CourseListEmpty` (Admin's title, stays) | no |
| `DataHealth.Hint.CoursesNotVisible` | `DataHealth.Technical.CoursesNotVisible` (title) | no |
| `DataHealth.Hint.UnknownCourseState` | `DataHealth.Technical.UnknownCourseState` (title) | no |
| `DataHealth.Technical.CourseListEmpty.Dean` | `DataHealth.Technical.CourseListEmpty` | no |

8 keys, each in both `SharedResource.uk.resx` and `SharedResource.en.resx`, with the values of Specification v2
FR-008 verbatim (the T2 text as agreed; finding `US-075/SPEC_REVIEW/m-1` closed ACCEPTED by the Owner). Code
refers to them through the existing `DataHealthTextKeys` constants class.

## 4. Acceptance Criterion → operation map

| AC | Operation |
|---|---|
| AC-001, AC-002 | none — the banner in every existing installation page response (`_Layout.cshtml`), unchanged routes and policies |
| AC-003 … AC-007 | none — existing `GET /workspace/data-health` response |
| AC-008 | none — confirms no route, policy or audit change |

## 5. Auth and error model

Unchanged. The banner and the page keep their policies (Admin, Dean; SC-4). No new error: a kind with no hint
renders as today (Specification "Error Handling").

## 6. Open findings

The Story has no OPEN finding. No new finding.
