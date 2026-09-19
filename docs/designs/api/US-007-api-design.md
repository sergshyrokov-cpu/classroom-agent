---
artifact_type: api_design
story: US-007
version: 1
status: DRAFT
created_at: 2026-09-19T12:25:08Z
updated_at: 2026-09-19T12:25:08Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-007 API Design — Read-only mode enforcement

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-007-openapi.yaml`.** Its absence is
this stage's recorded decision, not a missing artifact: a contract file describing
zero operations would assert a surface this Story does not have. Downstream stages
that list `openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when "the approved Specification
explicitly states the Story does not change public API behavior". The approved
Specification (v2) states exactly that, in four places:

- **Section 1** — "This Story is a mechanism Story. It adds no endpoint, no screen,
  no entity and no migration."
- **Section 7, S-08** — the refusal "is an application exception carrying no HTTP
  concept" (AD-9): no status code, no problem-details body.
- **Section 8** — "A refusal reaches HTTP | out of scope: no installation endpoint
  exists."
- **Section 10** — the HTTP `409` mapping, the API-6 error body, the error page and
  the translated message are out of scope and belong to US-008.

The cause is OD-001, resolved by the Owner on 2026-09-19 (option 1): the refusal
stops at `Application`. There is nothing for a contract to describe — the whole
Story lives below the presentation boundary.

## 2. What the Story does deliver, and why none of it is a contract

| Specification | Delivered artefact | Why it is not an API surface |
|---|---|---|
| FR-002 | `IReadOnlyModeGuard` / `ReadOnlyModeGuard` | an in-process seam in `Application`, not a port and not an endpoint (spec I-1) |
| FR-003 | `ReadOnlyModeException` | an application exception with no HTTP concept (AD-9, S-08) |
| FR-004, FR-005 | `PermittedServiceWrite`, `PermittedServiceWrites`, `ServiceWriteScope` | internal declarations of the BR-026 closed list |
| FR-006 | `ReadOnlyModeUnitOfWork` | a decorator of an internal port (AD-7) |
| FR-007 | `IGoogleDataPort` marker | an empty marker on outbound ports; carries no Google SDK type (AD-4) |
| FR-009 | the `Warning` refusal line | a log event, governed by DC-10 and SC-10, never a response body |
| FR-010, FR-011 | structural tests, DI wiring | solution shape, not a wire format |

## 3. Existing contracts this Story must not change

Confirmed against the Specification; each is an explicit no-change:

- **The installation's private port** — liveness and readiness (US-005 FR-013) and
  the push receiver (US-006 FR-009). Specification FR-008 keeps readiness reporting
  "degraded" in read-only mode and keeps it answering HTTP `200`: read-only is a
  normal mode, not an outage. No status, body or route changes.
- **The legitimacy-check service channel** (US-005) and the **status-change push**
  (US-006). `CheckLegitimacyUseCase` is registered as a permitted service write and
  keeps its behaviour, its outcomes and its wire contract unchanged
  (Specification FR-005). `ClassroomAgent.Contracts` gains nothing.
- **The Control Plane's pages and endpoints** (US-001 … US-006) are untouched: this
  Story is entirely on the installation side.
- **The installation's public port** still maps nothing and answers `404`
  (US-005 I-13). This Story adds no route there.

## 4. What US-008 inherits

Recorded here so the next API design does not have to re-derive it:

- `ReadOnlyModeException` (reason as `LegitimacyModeReason`, optional
  `LastSuccessfulCheckAt`, `Operation`) is what the installation's single
  `IExceptionHandler` must map to **HTTP `409`** with the API-6 error body, per
  API-5 ("including any action blocked in read-only mode") and AD-9.
- The user-visible message is chosen and translated at that point, from the reason
  **carried as data** — never by parsing the exception's fixed English `Message`
  (NFR-073, Specification FR-003, AC-002).
- Until then **API-5 is not satisfied end to end**, which the Specification records
  openly in sections 1, 8 and 10.

## 5. Acceptance Criterion → operation map

None. No Acceptance Criterion of US-007 (AC-001 … AC-011) is satisfied by an HTTP
operation; each is satisfied in `Application` or by a structural test, as the
Specification's traceability table (section 12) shows.

## 6. Auth model

Unchanged. This Story adds no endpoint and therefore no authorization policy. The
deny-by-default fallback and the SC-4 anonymous list of the existing hosts are
untouched; read-only mode is orthogonal to authorization — the caller has the
right, the installation is refusing the action (API-5).

## 7. Error model

Unchanged at the HTTP boundary. Inside the process, the Specification's section 8
is the authority for what each refusal produces. No new error code, no new error
body, no change to the API-6 shape.

## 8. Compatibility

No contract changes, so nothing to version and nothing for a client to adapt to.
`ContractVersion.Current` stays as US-005 set it.

## 9. Open questions

None raised by this stage. OD-001 and OD-002 are both resolved (open-decisions
artifact v2); neither leaves an API question open.
