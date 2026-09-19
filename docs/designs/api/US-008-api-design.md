---
artifact_type: api_design
story: US-008
version: 1
status: DRAFT
created_at: 2026-09-19T18:01:17Z
updated_at: 2026-09-19T18:01:17Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/decisions/US-008-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 78
supersedes: null
---

# US-008 API Design — Admin sign-in via Google OAuth with AllowedAdmin verification

Companion to `docs/designs/api/US-008-openapi.yaml`. The contract is
authoritative; this document carries the reasoning, the traceability and the
decisions this stage made.

## 1. Scope of the contract

Two hosts, two very different surfaces.

**Control Plane** gains exactly one operation:
`POST /service/v1/admin-login-checks`. It is the third request of the
Control Plane ↔ Data Plane service channel, beside the legitimacy check (US-005)
and the status push (US-006). No Control Plane page changes — the Owner sees
nothing about school sign-ins, by design (`trebovaniya.md` §9 keeps the Owner out
of a school's data, and a sign-in trail is the school's audit, EPIC-9).

**Installation** gains its entire public-port surface. US-005 recorded the rule
"every method and path on the public port answers `404`"; this Story replaces it.
Six operations — the sign-in page, the Google start, the OAuth callback, the
landing page, sign-out and the error page — plus nine host-wide rules that every
later installation Story inherits rather than re-decides.

The installation's private port is untouched. `/health/live`, `/health/ready` and
the push receiver keep the route group and port filter of US-005 and US-006.

## 2. Decisions this stage made

### 2.1 One port with two methods, not a second port (spec I-7)

The Specification left this to API_DESIGN. **The Admin login check is a second
method on `IControlPlaneClient`.**

The Control Plane is one external system, and AD-4 asks for a port per external
system, not per call. The two calls share everything that matters: the same base
address from `ControlPlane:Address`, the same `HttpClient` registration with
`AllowAutoRedirect = false` and `UseCookies = false`, the same timeout (spec I-8)
and the same classification of an HTTP outcome into a typed reply. A second port
would duplicate all of it and invite the two copies to drift — which is exactly
the failure the classification table in §2.4 exists to prevent.

The cost is that a test fake implements two methods instead of one. That is
cheaper than two registrations of the same typed client.

### 2.2 The refusal travels in TempData, never in a query parameter

A refused callback redirects to `/sign-in`. The reason has to survive that
redirect.

A query parameter would be the obvious choice and is the wrong one: anyone could
then craft `https://school.example/sign-in?refusal=…` and have the school's own
sign-in page render a message of their choosing. That is a phishing aid built
into the product.

The refusal therefore travels as a **category** in TempData, whose cookie is
protected by the Data Protection key ring that FR-001 makes a required setting.
The page maps the category to a translation key. The visitor never sees a
message the server did not choose.

### 2.3 The contract version stays 1

`ContractVersion.Current` remains **1**. Adding an endpoint and two wire types is
additive under DC-12: no existing request or response changes shape and no
property is removed or repurposed. The installation keeps reporting contract
version 1 on the legitimacy check, so the Control Plane's compatibility rule
(US-005 FR-005) is unchanged and no school is pushed into `upgrade_required` by
this Story.

### 2.4 A `404` is not always "unknown installation"

The classification table in the contract (`ClientClassification`) carries one
rule that is easy to get wrong and expensive if it is: a `404` **without** the
`{"outcome":"unknown_installation"}` body — which is what a Control Plane too old
to route the new path returns — is classified `Unavailable`, never `NotAllowed`.

Security is unaffected either way, because both refuse the sign-in. The audit is
not: "this email is not approved" and "the Control Plane could not be asked" are
different events, and an operator reading the audit after a failed deployment
must not be told the Owner revoked somebody.

### 2.5 No `/api/v1` operation, but the `/api/v1` rules are configured

The installation has no REST resource yet, so this contract defines no `/api/v1`
operation. The prefix, the API-6 body shape and the `409` read-only mapping are
nevertheless host-wide rules from this Story onwards (FR-014), and the `ApiError`
schema is defined so the later Stories inherit one shape rather than invent one.

The mapping is proven by a **test-only** probe endpoint registered in the test
host. It is not part of this contract and ships in no environment. This is stated
explicitly so a reviewer does not read the absence of an `/api/v1` path as the
mapping being untested.

### 2.6 No return-URL on the unauthenticated challenge

An unauthenticated request answers `302 /sign-in` with no return-URL parameter,
following what US-001 decided for the Control Plane (US-001 spec I-6). A return
URL on an anonymous page is an open-redirect surface, and this Story has exactly
one post-sign-in destination anyway.

## 3. Operation notes

### `POST /service/v1/admin-login-checks` — Control Plane

Anonymous and antiforgery-exempt under the existing SC-4 entry "Legitimacy check
and Admin login check", which already names both endpoints, so the closed list
grows by an endpoint but not by an entry. The US-001 endpoint enumeration test is
extended to know it.

The body is read explicitly rather than by model binding, exactly as
`LegitimacyCheckController` does, so every malformed request — wrong content
type, bad JSON, wrong type, broken rule — yields the same `400 invalid_request`
and the body is never logged or echoed.

Three properties of the answer matter and are asserted separately:

- it carries **one** boolean and nothing else — no entry id, no list, no other
  school's data (SC-12);
- a **suspended** `Installation` is answered exactly like an active one. Status
  belongs to the legitimacy check, not here;
- **nothing is written** — no audit row, no `InstanceLicenseCheck`, no last-seen
  stamp. A `POST` that writes nothing is unusual enough to be worth a test of its
  own.

Logs carry the `Installation` id and the outcome, never the email.

### `GET /sign-in` — installation

The SC-4 anonymous entry it maps to is "Dean sign-in page", per spec I-1: one
page serves the host, carrying only the Google form in this Story, and US-012
adds the Dean password form to the same page rather than opening a second
anonymous endpoint.

### `POST /sign-in/google` — installation

`POST`, because starting a sign-in sets the correlation cookie and the `state` —
it changes state, and `GET` never does (`trebovaniya.md` §8). It therefore
carries the antiforgery token, which is why the anonymous list entry is safe.

The authorization request is fully specified in the contract. Two points are
requirements rather than configuration choices: the scope set is stated
**explicitly** rather than inherited from the handler default, so a future
package default cannot silently widen what a school's Admin consents to (OD-003);
and the domain hint is present only when `LegitimacyState` knows the domain
(OD-002).

### `GET /signin-google` — installation

The single `GET` permitted to change state (`trebovaniya.md` §8, v64), and the
only operation in this contract that writes. Its processing order is in the
contract and is the order the tests assert: `state` and correlation first, then
the email's validity, then lower-casing, then the Control Plane, and only then
any write.

It always writes an audit row — success or refusal, never neither.

### `GET /` — installation

Reachable by **Admin and Dean**, from the `trebovaniya.md` §2 permission matrix
row "Просмотр статуса легитимности". Dean accounts do not exist until US-012;
declaring the policy now means US-012 adds accounts, not authorization.

The legitimacy status comes from `GetLegitimacyModeQuery` (US-005) through
`Application`. The view recomputes nothing (AD-3, AD-6) and receives a DTO, never
a domain entity (AD-8).

### `POST /sign-out` — installation

`POST` with the antiforgery token; `GET` answers `405`. It writes **no** audit
row: `trebovaniya.md` §5 lists sign-in and refused sign-in and the list is closed
(spec I-13).

### `GET /error/{statusCode}` — installation

One page for `400`, `403`, `404` and `500`. Translated text only: no exception
message, stack trace, request body or personal datum. The `400` text is
`Error.PageExpired` with a link back to the page the form came from — a local
path, never an absolute URL.

## 4. Authentication and authorization model

| Operation | Anonymous | Policy | Antiforgery |
|---|---|---|---|
| `POST /service/v1/admin-login-checks` | yes (SC-4 service channel, network isolation) | — | exempt |
| `GET /sign-in` | yes (SC-4 "Dean sign-in page") | — | — |
| `POST /sign-in/google` | yes (SC-4 "Google OAuth start and callback") | — | **required** |
| `GET /signin-google` | yes (same entry) | — | not applicable (GET) |
| `GET /` | no | Admin or Dean | — |
| `POST /sign-out` | no | any authenticated | **required** |
| `GET /error/{statusCode}` | yes (SC-4 error page) | — | — |
| catch-all `404` | yes (same entry) | — | — |

Deny by default: a fallback policy requires an authenticated user, so an endpoint
whose author forgets an attribute closes rather than opens. An enumeration test
fails on any anonymous endpoint outside this table (API-9, SC-4).

Cookies are specified in `components.securitySchemes.installationSession`. The
session cookie's `SameSite=Lax` is a requirement, not a preference: `Strict`
would leave the first page after the return from Google without a session.

## 5. Error model

| Status | Where | Body |
|---|---|---|
| `400` | channel | `ServiceOutcome` `invalid_request`; the request body never logged or echoed |
| `400` | installation pages | error page, `Error.PageExpired` |
| `403` | installation pages | error page, `Error.Forbidden` |
| `404` | channel | `ServiceOutcome` `unknown_installation` |
| `404` | installation pages | error page, `Error.NotFound`, from the anonymous catch-all |
| `405` | installation | wrong method on `/sign-in/google` or `/sign-out` |
| `409` | installation, `/api/v1` | `ApiError` with the read-only reason — **host-wide rule, no operation yet** |
| `500` | both | empty on the channel; error page `Error.Unexpected` on pages; no internals anywhere |

A refused sign-in is **not** an error status. It is an expected outcome: `302` to
`/sign-in` with a translated message. Exceptions signal failures, not expected
outcomes (AD-9).

## 6. Acceptance Criterion → operation map

| Story AC | Operation(s) / rule |
|---|---|
| AC-001 | `components.schemas.Configuration` (start-up rules; not an HTTP operation) |
| AC-002 | `x-host-wide-rules.fallback-authorization` + every `x-anonymous` marker; the enumeration test |
| AC-003 | `POST /sign-in/google`, `GET /signin-google` |
| AC-004 | `GET /signin-google` step 4 → `POST /service/v1/admin-login-checks`; `ClientClassification` |
| AC-005 | `POST /service/v1/admin-login-checks` `200` |
| AC-006 | `POST /service/v1/admin-login-checks` `400` and `404` |
| AC-007 | `GET /signin-google` step 5 |
| AC-008 | `GET /signin-google` step 6, category `NotInAllowedAdmin` |
| AC-009 | `ClientClassification` → `Unavailable`; category `ControlPlaneUnavailable` |
| AC-010 | `x-audit` on `/signin-google`; `ServiceResults.SignInRefusalCategory` |
| AC-011 | `GET /signin-google` steps 5–6 (BR-026 members, list not widened) |
| AC-012 | `x-host-wide-rules.read-only-mode`, `ApiError` |
| AC-013 | `securitySchemes.installationSession`, `x-host-wide-rules.session-lifetime`, `.antiforgery` |
| AC-014 | `POST /sign-out` |
| AC-015 | `PageTextKeys`, `x-host-wide-rules.culture` |
| AC-016 | `GET /error/{statusCode}`, `x-host-wide-rules.error-page` |
| AC-017 | `GET /` and `LandingPageModel` |
| AC-018 | `x-audit` / log notes on each operation; SC-10 stated per operation |

## 7. Compatibility

- **Additive only.** No existing operation changes. `ServiceOutcome` is reused
  unchanged; `ContractVersion` stays 1 (§2.3).
- **One US-005 rule is replaced**, deliberately and with its successor named:
  `installation-public-port` no longer answers `404` to everything. US-005 wrote
  that rule expecting US-008 to replace it.
- The Control Plane's SC-4 anonymous list gains an endpoint but no entry: the
  existing entry already reads "Legitimacy check and Admin login check".
- No Control Plane page, DTO or view model changes.

## 8. Open questions for later stages

None blocking. Two notes for DB_DESIGN and TEST_WRITING:

- **DB_DESIGN** owns the `app_user` and `audit_event` tables, the unique index on
  the normalized email that FR-011 relies on for the concurrent-first-sign-in
  rule, and the single migration. This contract fixes only what crosses HTTP.
- **TEST_WRITING** must substitute the Google handler with a synthetic
  authentication scheme (TC-4 — no test calls a live Google endpoint), substitute
  `IControlPlaneClient` in the installation's tests, and drive the Control Plane
  endpoint against real PostgreSQL (TC-2). The `ClientClassification` table is a
  test list, not prose: each row is a case, including the `404`-without-body row
  of §2.4.
