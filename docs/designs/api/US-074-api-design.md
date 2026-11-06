---
artifact_type: api_design
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T19:47:03Z
updated_at: 2026-11-06T19:47:03Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 112
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
supersedes: null
---

# US-074 API Design — Meet code lists show the organizer by name and find the code by that name

Contract: `docs/designs/api/US-074-openapi.yaml` (v1). Verdict **PASS**: the Story changes the view model of an
existing page and the meaning of an existing search parameter, so a delta contract is recorded rather than
`NOT_APPLICABLE`.

Delegation: none. The designer read the Specification, the US-070 and US-071 contracts and the existing DTOs
(`UnassignedCodeItem`, `LinkedCodeItem`, `NotACourseCodeItem`, `PersonRef`) and `_MeetPerson.cshtml` directly.

## 1. Rationale

- No new path, parameter, status code, policy or translation key (spec sections 1, 7, 9).
- The organizer item reuses the existing DTO `PersonRef(FullName, Email)` (US-033 Meet reports) instead of a new
  type. This lets the view reuse `MeetReports/_MeetPerson.cshtml` and makes the "same as the Meet reports"
  rendering (spec FR-002) a single piece of markup. On these lists `Email` is never null, so the partial's
  "unnamed" branch cannot fire (spec FR-002).
- `OrganizerEmails: IReadOnlyList<string>` is **replaced** by `Organizers: IReadOnlyList<PersonRef>` on the three
  DTOs. Keeping both would let the view and the search read different lists, which spec FR-001 forbids. The
  Application row models (`UnassignedCodeRow`, `LinkedCodeRow`, `MarkedCodeRow`) carrying what the read source
  returns are the database design's choice (spec FR-002).

## 2. Operations

| Operation | Change | Spec | AC |
|---|---|---|---|
| `GET /workspace/meet-codes` | organizer item → `PersonRef` list; `q` also matches organizer name | FR-001, FR-002, FR-003, FR-005 | AC-001…AC-005, AC-007…AC-009 |
| `GET /workspace/search` | none of its own; "Meet codes" group gains the name field through the shared read | FR-004 | AC-006, AC-007 |

Row actions (`POST` link, confirm, mark, reset of US-032) and the course-choice page are unchanged (spec section 10);
they carry `returnQ` as in US-070.

## 3. Acceptance Criterion → operation

| AC | Operation | Observable in the contract |
|---|---|---|
| AC-001 | getMeetCodes | `organizers[].fullName` non-null and `email`, rendered name then email, encoded |
| AC-002 | getMeetCodes | `fullName` null → email alone |
| AC-003 | getMeetCodes | distinct by lower-cased email, ordinal order |
| AC-004 | getMeetCodes | erased/excluded organizer absent from `organizers`; `q` does not match through them |
| AC-005 | getMeetCodes | `q` over the name field; counters; carried by links and row actions |
| AC-006 | getSearchResults | "Meet codes" group via shared read |
| AC-007 | both | `x-read-only-mode: allowed`, `x-calls-google: false`, policies unchanged with allowed/forbidden tests, `x-logs-search-text: false`, `x-audit: none` |
| AC-008 | getMeetCodes | non-functional; bounded queries per page — database design |
| AC-009 | getMeetCodes | same `PersonRef` name as the Meet reports for the same email |

## 4. Authentication and authorization

Unchanged: `ViewMeetCodes` (Admin, Dean) on the codes page; `ViewCourses` (Admin, Dean) on the header search;
no anonymous access (SC-4); no Teacher or Student account. Each policy keeps an allowed-role and a forbidden-caller
test (TC-5, AC-007).

## 5. Error model

Unchanged from US-070 and US-071: `400` page with `searchMessage` for a refused `q`; US-032 fallback for invalid
`list`/`page`/`size`; `302` to sign-in or the forced password change; the existing access-denied response. An
organizer with no known name is not an error (spec section 8).

## 6. Compatibility

In-process only: the three DTOs lose `OrganizerEmails` and gain `Organizers`. Callers are the Razor view
`MeetCodes/Index.cshtml`, `GetMeetCodesQuery` mapping and tests. No external client, no Control Plane wire type
(`ContractVersion.Current` stays 1).

## 7. Findings handled

- **US-074/SPEC_REVIEW/m-1** (name rule order) is not in API_DESIGN responsibility: the contract only says
  `fullName` is null when no name is known or the chosen one is blank. Left OPEN for DB_DESIGN and TEST_WRITING.

## 8. Open questions

None.
