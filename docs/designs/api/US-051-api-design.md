---
artifact_type: api_design
story: US-051
version: 1
status: DRAFT
created_at: 2026-10-15T20:35:00Z
updated_at: 2026-10-15T20:35:00Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-051-spec.md
    version: 1
  - path: docs/decisions/US-051-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 96
supersedes: null
---

# US-051 API Design — The Google sign-in admits only Admin accounts

**Verdict: NOT_APPLICABLE.** This Story changes no public API behaviour, so no
OpenAPI contract is produced.

**There is deliberately no `docs/designs/api/US-051-openapi.yaml`.** Its absence
is this stage's recorded decision, not a missing artifact — as for US-007,
US-013, US-014, US-018, US-045, US-046, US-047 and US-048. Downstream stages that
list `openapi` among their inputs read this document instead.

## 1. Why the stage does not apply

`stage-map.yaml` marks `API_DESIGN` optional when the approved Specification
states the Story does not change public API behaviour. The approved
Specification (v1) states it:

- **§1** — "No endpoint, route, status code or page is added."
- **§6 VR-001** — "this Story adds no input and no new validation."
- **§7 SC-4** — "No endpoint, policy or anonymous surface changes; the callback
  keeps its `state` and correlation-cookie protection."
- **§14** — "API_DESIGN not applicable (no endpoint, route, status code or body
  change)."

## 2. The one observable change, and why it is not a contract change

FR-006: the Google OAuth callback answers the new refusal exactly as it answers
every existing one (US-008) — a redirect to the sign-in page with the refusal
text carried in TempData (`GoogleSignInEvents`: `SignInRoutes.RefusalTempDataKey`,
`Response.Redirect(SignInRoutes.SignInPage)`). The only difference is the value
of the translation key, `SignIn.Refused.NotAdminAccount`, chosen by the existing
refusal-to-text mapping. Route, method, status code (the same redirect),
anonymous status and protection of the callback are unchanged; the sign-in page
renders the refusal text it already renders.

The new text is a translation entry (NFR-073), not a contract element. The
`SignInRefusal` member and its `TextKey` case are internal to the Web and
Application layers.

## 3. Delegation

Nothing was delegated; the Specification, the US-048 precedent and
`src/ClassroomAgent.Web/Security/GoogleSignInEvents.cs` were read directly.
