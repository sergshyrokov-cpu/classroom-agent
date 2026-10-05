---
artifact_type: api_design
story: US-041
version: 1
status: DRAFT
created_at: 2026-10-05T07:27:37Z
updated_at: 2026-10-05T07:27:37Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-041-spec.md
    version: 2
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 85
supersedes: null
---

# US-041 API Design — An unknown file-like address answers 404

**Verdict: PASS.** The Story changes observable HTTP behaviour — a file-like
unmatched path answers `404` instead of `302 /sign-in` — but adds no operation
and changes no earlier contract: US-001 already defined the catch-all for "any
path". The contract `docs/designs/api/US-041-openapi.yaml` restates the
catch-all for both hosts with file-like paths explicit, plus host-wide rules.

## 1. Scope of the contract

| Host / port | What changes | What does not |
|---|---|---|
| Installation, public port | unmatched file-like path: `302 /sign-in` → `404` error page, for anonymous, Admin, Dean | real endpoints, existing static files, `/signin-google`, error page, `Cache-Control` |
| Installation, private port | unmatched path under a private prefix with a dot (e.g. `/health/x.txt`): → `404`, no body | paths outside private paths (`404` from the port filter), `/health`, status push |
| Control Plane | unmatched file-like path: → `404` error page, for anonymous and the Owner, before and after setup | setup gate for real endpoints, sign-in redirect, existing static files |

## 2. Decisions this stage made

- **D-1 One catch-all per host, pattern without `nonfile`.** The catch-all's
  route pattern matches any path (in ASP.NET Core terms a catch-all parameter
  without the `nonfile` constraint, e.g. `{*path}`), keeping fallback (lowest)
  precedence. No second catch-all is added for file-like paths (spec FR-003).
- **D-2 The response is produced the way it is today.** The catch-all sets
  `404` and writes no body; the existing status-code re-execution renders
  `Error.NotFound` on the public port and the Control Plane. No new view, text
  or translation key.
- **D-3 Control Plane catch-all keeps its setup-gate exemption** (OD-001).
- **D-4 `/api/v1` unchanged** (OD-002): an unmatched `/api/v1` path keeps the
  error page.

## 3. Operation notes

`/{unmatchedPath}` (any method) — anonymous (`security: []`), the SC-4 entry
"Error page, and the fallback catch-all". Path parameter is not interpreted or
echoed. Response `404`: error page (public port, Control Plane) or no body
(private port), with `Cache-Control: no-store` (US-040).

## 4. Authentication and authorization model

The catch-all is anonymous by design and on the SC-4 closed list already; widening
its pattern adds no anonymous endpoint. Every real endpoint keeps its policy;
the fallback authorization policy still applies to any matched endpoint
without one. Because the catch-all has the lowest precedence, it never
shadows a protected endpoint. A signed-in caller gets the same `404` as an
anonymous one (in their own language).

## 5. Error model

| Status | Where | Body |
|---|---|---|
| `404` | installation public port, Control Plane | error page `Error.NotFound` |
| `404` | installation private port | none |
| `500` | if producing the `404` throws | existing exception handling |

No API-6 body is produced by this Story (OD-002).

## 6. Acceptance Criterion → contract map

| AC | Contract element |
|---|---|
| AC-001 | `/{unmatchedPath}` on the installation; `catch-all-matches-every-path` |
| AC-002 | `/{unmatchedPath}` on the Control Plane; `setup-gate` |
| AC-003 | `request-order` (static files before routing) |
| AC-004 | `unchanged`, `private-port` |

## 7. Compatibility

Contract version stays 1. The only observable change is `302 /sign-in` →
`404` for unmatched file-like paths (and for unmatched paths under a private
prefix on the private port). No client depends on the old redirect: it led to
a sign-in page for a page that does not exist.

## 8. Open questions for later stages

None. DB_DESIGN is expected NOT_APPLICABLE (no entity, no migration).
