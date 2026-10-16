---
artifact_type: api_design
story: US-049
version: 1
status: DRAFT
created_at: 2026-10-16T12:55:00Z
updated_at: 2026-10-16T12:55:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-049-spec.md
    version: 1
  - path: docs/decisions/US-049-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-049 API Design — Session-signing keys are encrypted at rest

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-049-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-050,
US-051 and earlier Stories without API changes. Downstream stages that list
`openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "Users see nothing."
- **§7 S-04** — "No new endpoint, no new anonymous surface, no change to
  cookies, authorization policies, antiforgery or session timeouts."
- **§13** — "API_DESIGN NOT_APPLICABLE (no endpoint, request or response
  changes)."

## 2. The observable effect, and why it is not a contract change

FR-003: when a key cannot be decrypted, a session cookie or antiforgery token
protected with it is no longer accepted. Under `/api/v1` the request is then
unauthenticated and gets the existing `401` error body (spec §8, "as today");
no new status code, error key or body shape appears. The change is in how the
key ring is stored on disk, beneath every operation, not in any operation's
path, method, schema, status code or authorization.

## 3. Delegation

Nothing was delegated; the Specification and the US-050 precedent were read
directly.
