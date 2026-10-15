---
id: US-052
epic: EPIC-6
title: Every Admin-only and Dean-only write has a forbidden-role test
slug: forbidden-role-tests
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from pre-pilot audit 2, finding F-1 and
# informational note I-1 (docs/reviews/audits/2026-10-pre-pilot-cross-cutting-audit.md);
# to be reviewed and edited by the Owner before activation. Aligned with
# trebovaniya.md v96. Test-only: no production behaviour changes.
---

# User Story

As the **Owner** of the service

I want every endpoint that only one role may use to have a test proving the
other role is refused, and one test that holds the whole permission matrix

So that a moved or deleted authorization attribute fails the test suite instead
of silently opening an Admin action to a Dean, or a Dean action to an Admin.

---

# Business Value

Pre-pilot audit 2 (finding F-1) found four state-changing endpoints whose
forbidden-role refusal is not tested:

- `POST /settings/deans` — create a Dean account;
- `POST /settings/deans/{deanId}/state` — disable or re-enable it;
- `POST /settings/deans/{deanId}/password` — reset its password;
- `POST /account/password` — a Dean changing their own password.

The code refuses the wrong role today: the three Dean-account endpoints are
Admin-only through the class-level attribute of `DeanAccountsController`, and
`/account/password` is Dean-only through its own method attribute. But only the
GET pages are tested with the wrong role. TC-5 says an endpoint with no
authorization test is treated as an endpoint with no policy: if the attribute
moved or was lost, the fallback policy would admit any signed-in user and no
test would fail. Creating or resetting a Dean account is the most sensitive
write a Dean must not reach.

The audit also noted (I-1) that no single test compares the routes with the §2
matrix: each Story pins only its own endpoints, so an endpoint added later under
the policy of the wrong row passes if its own Story forgets the check.

---

# Scope

**In scope:**

- HTTP tests with a real Dean session (signed in by password, temporary password
  already changed) sending each of the three Dean-account POSTs with a valid
  antiforgery token, and a real Admin session sending `POST /account/password`;
- a permission-matrix test: a table of every endpoint of the installation host
  (method and route pattern) with the policy its `trebovaniya.md` §2 row
  requires, compared with the endpoints the host actually exposes.

**Out of scope:**

- any change to production code, policies or the matrix itself;
- the Control Plane — it has one role (Owner), and its endpoints are already
  enumerated by `InstallationAuthorizationTests` and its siblings;
- the open US-037 test items (audit 2 I-4, I-5).

---

# Acceptance Criteria

## AC-001 A Dean cannot create a Dean account

**Given** a signed-in Dean with a valid antiforgery token

**When** the Dean posts a complete, valid create form to `/settings/deans`

**Then** the answer is `403` with the translated error page, no account is
created and no audit row is written.

## AC-002 A Dean cannot disable, re-enable or reset another Dean

**Given** a signed-in Dean and a second, existing Dean account

**When** the first Dean posts a valid state change, and a valid password reset,
for the second account

**Then** each answer is `403` with the translated error page, and the second
account's state, password hash, lockout and security stamp are unchanged; no
audit row is written.

## AC-003 An Admin cannot post the own-password form

**Given** a signed-in Admin with a valid antiforgery token

**When** the Admin posts a complete own-password form to `/account/password`

**Then** the answer is `403` with the translated error page, and the Admin
account is unchanged; no audit row is written.

## AC-004 The allowed role still succeeds

**Given** the same requests

**When** they are sent by the allowed role — an Admin for the three Dean-account
POSTs, a Dean for `/account/password`

**Then** they succeed, so the refusals above prove a role boundary and not a
broken request.

## AC-005 The permission matrix is one test

**Given** the installation host

**When** the matrix test lists every endpoint with its declared authorization

**Then** each endpoint is in the test's table with the policy of its §2 row (or
in the SC-4 anonymous list); an endpoint missing from the table, a table row
with no endpoint, or a different policy fails the test.

## AC-006 Nothing else changes

**Given** the existing test suite

**When** it runs with the new tests

**Then** it stays green, and no file under `src/` is changed.

---

# Open Decisions

None. The required behaviour is the existing `trebovaniya.md` §2 matrix, SC-4
and TC-5.

---

# Notes

- Origin: pre-pilot audit 2 2026-10-15, finding F-1 and note I-1.
- Patterns to reuse: `DeanSessionBoundaryTests` (real Dean session over HTTP),
  `DeanAccountHostExtensions` (the three POST helpers), `HostEndpoint.All` (the
  endpoint enumeration other authorization tests already use).
- For AC-001…AC-003 the token must be valid, so the refusal under test is the
  authorization one, not the antiforgery one (as `ErasureAuthorizationTests` does).
- If US-051 is delivered first, nothing here changes: it adds no endpoint.
- Red phase: these tests pass against the current code by design — the behaviour
  exists and only its proof is missing. The implementation report records that,
  and shows each new test failing once against a temporarily removed attribute
  (not committed), so no test is vacuous.
- Expected stages: API_DESIGN NOT_APPLICABLE, DB_DESIGN NOT_APPLICABLE;
  IMPLEMENTATION adds no production code.
- No new NuGet package.
