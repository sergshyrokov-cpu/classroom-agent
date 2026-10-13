---
artifact_type: api_design
story: US-046
version: 1
status: DRAFT
created_at: 2026-10-13T17:46:26Z
updated_at: 2026-10-13T17:46:26Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-046-spec.md
    version: 1
  - path: docs/decisions/US-046-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 91
supersedes: null
---

# US-046 API Design — Page-search hint on teaching-data pages

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-046-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-007,
US-013, US-014, US-018 and US-045. Downstream stages that list `openapi` among
their inputs read this document instead.

This document also fixes the **markup hook** of the hint (§3), so that tests
(TEST_WRITING) and code (IMPLEMENTATION) agree on one element to find. The hook
is page markup, not an API surface.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "The change is presentation only: no endpoint, request parameter,
  status code, use case, port, entity, table or audit event is added or
  changed."
- **FR-009** — no route, query parameter, status code or authorization policy
  changes.
- **§6 VR-001** — no input is added.
- **§7 S-01** — every page keeps its current policy; no new endpoint, no
  anonymous access.

The Story Notes expect the same ("API_DESIGN is NOT_APPLICABLE or records only
the view change").

## 2. What the Story delivers, and why none of it is a contract

| Specification | Delivered change | Why it is not an API surface |
|---|---|---|
| FR-001 … FR-003, FR-006 | one hint line in nine existing Razor views | same routes, methods, status codes and policies; HTML content only |
| FR-004 | course-list hint quotes the search field's label | translation text |
| FR-007 | nothing stored, no cookie, no script | absence of change |
| FR-008 | three translation entries per language | `Application.Localization` resources |
| FR-005, FR-009 | every other page unchanged | — |

Every route, method, status code, error body (API-6), header and policy of the
installation stays as it is; the Control Plane is untouched.

## 3. Markup hook (binding for TEST_WRITING and IMPLEMENTATION)

On each FR-001 page the hint is exactly one element:

```html
<p id="page-search-hint" class="page-search-hint" data-variant="{variant}">{text}</p>
```

- `data-variant` is one of `ordinary`, `course-list`, `paged-list` (spec FR-003
  a, b, c), assigned per page by spec FR-001:

  | Page (view) | `data-variant` |
  |---|---|
  | `Courses/Index` | `course-list` |
  | `Courses/Card`, `Journal/Index`, `Report/Index`, `MeetReports/Index`, `MeetReports/Meeting`, `Database/Index` | `ordinary` |
  | `Teachers/Index`, `MeetCodes/Index` | `paged-list` |

- `{text}` is the translated text of spec FR-003, HTML-encoded by the view
  engine (S-03) — no `Html.Raw`, no child elements, no close control, no script
  (FR-006, FR-007).
- **Placement (FR-002, AC-009).** The element is the first element of the page
  body, i.e. the first thing after the layout's `<h1>`. On `MeetCodes/Index` it
  is the first element after the view's own `<h1>` (OD-002 a). Nothing —
  message, form, table, heading — precedes it otherwise.
- **Absence (FR-005, AC-005).** No other page of the installation, and no
  Control Plane page, contains an element with `id="page-search-hint"` or class
  `page-search-hint`.
- `class="page-search-hint"` carries the small, unobtrusive style (I-3) in the
  installation stylesheet `src/ClassroomAgent.Web/wwwroot/css/site.css`.

How the element is produced (a shared partial rendered by each view, a layout
section, or a view-data flag read by the layout) is an implementation choice,
provided the resulting markup and placement are as above and no view holds hint
text as a literal (FR-008).

## 4. Acceptance Criterion → operation map

| AC | Operation | Note |
|---|---|---|
| AC-001 | existing `GET` of course card, journal, report, Meet reports, meeting, database | `data-variant="ordinary"` |
| AC-002 | existing `GET /workspace/courses` (course list) | `data-variant="course-list"`; quoted name = `Courses.Form.Search` text |
| AC-003 | existing `GET` teacher list, Meet codes | `data-variant="paged-list"`; Meet codes placement per OD-002 a |
| AC-004 | the same operations, all states, both roles, read-only | status codes unchanged; no `Set-Cookie` added by the hint |
| AC-005 | every other installation and Control Plane page | hook absent |
| AC-006 | — | translation resources |
| AC-007 | all existing operations | unchanged |
| AC-008 | FR-001 pages | exactly one hook element |
| AC-009 | FR-001 pages | hook is the first body element (§3) |

## 5. Auth and error model

Unchanged. The hint is rendered only inside a page whose existing policy already
admitted the user; an error response renders the error page, which has no hint
(spec §8).

## 6. Open questions

None. OD-001 and OD-002 are resolved (open-decisions v2).
