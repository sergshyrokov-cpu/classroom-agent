---
artifact_type: api_design
story: US-040
version: 1
status: DRAFT
created_at: 2026-10-04T21:13:33Z
updated_at: 2026-10-04T21:13:33Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-040-spec.md
    version: 2
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 85
supersedes: null
---

# US-040 API Design — Responses are not cached

**Verdict: PASS.** The Story changes public HTTP behaviour — a response header
on every response of both hosts — but adds no operation. The contract
`docs/designs/api/US-040-openapi.yaml` therefore has empty `paths` and states
the change as one host-wide rule (`x-host-wide-rules.no-store`) plus the header
component `CacheControlNoStore`, as US-001 and US-008 state their host-wide
rules.

## 1. Scope of the contract

| Host | What changes | What does not |
|---|---|---|
| Installation, public port | every response gets `Cache-Control: no-store` | every operation of US-008 … US-039 |
| Installation, private port | the same: health checks, status-push receiver, refusals | port filter, plain HTTP (DC-6) |
| Control Plane | the same: Owner pages, setup gate, `/service/v1`, `/api/v1` | every operation of US-001 … US-019 |
| Both | static files of `wwwroot` keep their caching | — |

## 2. Decisions this stage made

- **D-1 Final value.** The rule writes exactly `no-store`. Where the pipeline
  already wrote a `Cache-Control` value containing the `no-store` directive
  (antiforgery: `no-cache, no-store`; exception handler: `no-cache,no-store`),
  that value is kept (spec FR-006). Any other value (none today) is replaced by
  `no-store`. Reason: the spec requires the directive, not an exact string, and
  overwriting framework values buys nothing.
- **D-2 No `Pragma` / `Expires`.** Not added. The requirement names only
  `Cache-Control`; FR-005 forbids other header changes. Existing framework
  writes of `Pragma: no-cache` stay as they are.
- **D-3 Applied when the response starts.** The rule must see the response as
  the last middleware left it — redirects, error re-execution and refusals are
  written by middleware and filters that run after the rule's registration
  point — so the header is decided at the moment the response starts, not on
  the way in. A response whose headers were sent before that point does not
  exist inside the pipeline.
- **D-4 Static-file exception by what was served, not by path.** The exception
  is "the static-file middleware served an existing file", not "the path starts
  with `/css` or `/js`". A missing file under those paths gets `no-store`
  (spec FR-004, VR-003).
- **D-5 Both ports of the installation.** The rule is registered once, before
  the public-port branch, so the private port is covered without a second
  registration (spec FR-001, FR-003).

## 3. Operation notes

No operation is added or changed. Every operation of every earlier contract of
both hosts now carries the response header `CacheControlNoStore`; the earlier
contracts are not edited — `x-host-wide-rules` applies to them, as US-008's
rules apply to later Stories.

**Implementation guidance (non-normative).** One middleware per host,
registered first in the pipeline, which registers a response-starting callback
that applies D-1; the static-file middleware marks a served file (its
"prepare response" hook) so the callback skips it. The design does not bind
the class names or the marking mechanism.

## 4. Authentication and authorization model

Unchanged. The rule runs for every caller and every authentication state,
before authentication and authorization decide; it adds no anonymous access
(SC-4) and no policy (API-9).

## 5. Error model

Unchanged bodies and status codes (API-5, API-6). Every error response —
`400`, `401`, `403`, `404`, `405`, `409`, `500`, under `/api/v1` or as the
error page — carries the header. The rule itself raises no error.

## 6. Acceptance Criterion → contract map

| AC | Contract element |
|---|---|
| AC-001 | `x-host-wide-rules.no-store` (installation, both ports); `CacheControlNoStore` |
| AC-002 | `x-host-wide-rules.no-store` (Control Plane); `CacheControlNoStore` |
| AC-003 | `x-host-wide-rules.static-files` |
| AC-004 | `x-host-wide-rules.no-store` — "one registration … no attribute"; the enumeration tests (VR-004) are TEST_WRITING's |
| AC-005 | `x-host-wide-rules.unchanged` |

## 7. Compatibility

Additive. `ContractVersion.Current` stays 1 (DC-12); the service channel and
the status push ignore `Cache-Control`. Browsers will re-request pages on Back
instead of showing a stored copy — the intended effect.

## 8. Open questions for later stages

None. OD-001 is resolved (a) and recorded as
`x-host-wide-rules.pre-pipeline-responses`. DB_DESIGN is expected
`NOT_APPLICABLE`: no persistence change.
