---
artifact_type: test_strategy
story: US-050
version: 1
status: DRAFT
created_at: 2026-10-16T08:30:00Z
updated_at: 2026-10-16T08:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: docs/specifications/US-050-spec.md
    version: 1
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
  - path: docs/designs/api/US-050-api-design.md
    version: 1
  - path: docs/designs/database/US-050-db-design.md
    version: 1
  - path: docs/reviews/specifications/US-050-spec-review.md
    version: 1
supersedes: null
---

# US-050 Test Strategy — Every response carries the browser security headers

## 1. Scope

The host-wide rule of both hosts that adds `X-Frame-Options`, `X-Content-Type-Options`,
`Referrer-Policy` and `Content-Security-Policy` to every response (spec FR-001 … FR-005), the
no-inline-code rule for views (FR-006) and the guard that existing scripts run under the policy
(FR-007). No endpoint, entity, translation, audit or log event changes (API_DESIGN and DB_DESIGN
NOT_APPLICABLE), so there are no contract, persistence or Application-layer tests.

Pattern: US-040 `NoStoreResponseTests` (per host) and `NoStoreRuleTests` (architecture).

## 2. Test levels

| Level | What | Classes |
|---|---|---|
| Integration (host over HTTP, real PostgreSQL via Testcontainers, TC-2) | headers on real responses of both hosts, both installation ports | `Web.Security.SecurityHeadersResponseTests`, `ControlPlane.Security.SecurityHeadersResponseTests` |
| Architecture / source scan (no host) | one rule per host; no inline code in views; scripts free of constructs the policy refuses | `Architecture.SecurityHeadersRuleTests`, `Architecture.InlineCodeRuleTests` |
| Manual | AC-004 real-browser Google sign-in | recorded in the implementation report (Story notes, spec AC-004) |

Shared helpers: `TestInfrastructure.SecurityHeaders` (expected values, exactly-once check, endpoint
enumeration reusing `NoStore.MethodsOf`, CSP directive parser) and `TestInfrastructure.InlineCode`
(the VR-003 view scan and the script scan).

## 3. Scenarios

### Positive
- P-1 every routed endpoint × every accepted method (HEAD/OPTIONS skipped, GET+POST for any-method
  endpoints, as US-040 VR-004) on the installation public port, for Admin, Dean and anonymous
  (AC-001).
- P-2 the same on the installation private port (health checks, status-push receiver) and an unknown
  private address (AC-001, FR-002).
- P-3 the same on the Control Plane for Owner, anonymous with the Owner existing, and before setup
  (AC-002).
- P-4 named response kinds per host: 200 pages of each role, the journal, the journal export
  (`/api/v1`, downloaded `.xlsx`), redirects (to sign-in, setup gate, HTTPS redirection), every static
  file of `wwwroot` (spec I-3 — not exempt, unlike SC-14).
- P-5 the CSP each host actually sends is parsed and checked directive by directive (AC-003).
- P-6 the sign-in start (POST `/sign-in/google`) with the real Google handler configuration redirects
  to an origin equal to the non-`'self'` source of `form-action` (AC-004 automated part, spec
  FR-004 "a test asserts that the origin in the CSP equals the origin of the endpoint the handler
  actually redirects to").

### Negative / error paths (spec §8)
- N-1 403 refusal, 404 (page and `/api/v1`), antiforgery 400 (form and `/api/v1` API-6 body), 409
  read-only refusal, exception-handler 500 (real failure: table dropped), missing static file 404,
  the Google callback address without `state` (handled by the auth handler, not enumerated).

### Boundary / exactness (VR-001, VR-002, FR-005)
- B-1 every header compared by exact value and exactly one occurrence; a raw response with two
  values arrives joined and fails the equality.
- B-2 a page that generates an antiforgery token (today `X-Frame-Options: SAMEORIGIN`) carries
  `DENY` and nothing else — both hosts (spec I-4).
- B-3 no CSP directive appears twice.

### Validation
- None: the rule reads no input (spec §6).

### Security
- S-1 framing refused (`DENY` + `frame-ancestors 'none'`), no `'unsafe-inline'`/`'unsafe-eval'`,
  `object-src 'none'`, `base-uri 'self'`; the Control Plane policy names no origin at all (`://`
  absent); the installation names only Google's sign-in origin (SC-13, SC-15).
- S-2 no source file outside the host's `Security` folder names one of the headers; no view uses
  `<meta http-equiv>` (FR-001 "one host-wide rule").
- S-3 view scan (AC-005, VR-003) plus, per SPEC_REVIEW m-1, `<style>` elements with a body.
- S-4 script scan: no `eval`, `new Function`, string timers, `setAttribute` of `style` or `on…`
  in `wwwroot/js` of either host (FR-007 — they would break under the policy). CSSOM stays allowed.
- S-5 the scanners have their own positive and negative examples, so a guard that passes today is
  not vacuous.

### Persistence
- None (DB_DESIGN NOT_APPLICABLE).

## 4. Fixtures

`PostgreSqlFixture` (Testcontainers), `InstallationTestHost` / `JournalHostExtensions` /
`DeanAccountHostExtensions` / `ReadOnlyModeHost` (installation), `ControlPlaneTestHost` (Control
Plane), `HostEndpoint.All`, `NoStore.StaticFilesOf`, `StaticFiles.RepositoryRoot`. The Google
handler is **not** stubbed in P-6 and the callback test: the start only builds the redirect locally
and the callback without `state` refuses before any exchange, so no request leaves the process (TC-4).

## 5. Expected red phase

- Red: every header assertion of both `SecurityHeadersResponseTests` classes (no host sends the
  headers yet; antiforgery pages send `SAMEORIGIN`).
- Green before implementation, by design: `InlineCodeRuleTests` and `SecurityHeadersRuleTests`
  (guards — spec FR-006 states every view already complies and nothing names the headers today);
  their scanner self-tests prove they detect violations.

## 6. Excluded scenarios

- Real-browser enforcement of the policy (the browser blocking framing, inline code or a form
  target) — not observable from a header test; covered by the AC-004 manual check and by the
  static scans.
- A full Google sign-in round trip with the stub asserting the exact CSP: the stub replaces the
  authorization endpoint with `accounts.google.test`, which would make the expected `form-action`
  depend on whether the implementation derives the origin from `GoogleOptions` or holds a constant
  — a choice the spec leaves open (FR-004). The existing US-008/US-051 sign-in tests stay green
  (AC-006) and P-6 checks the real configuration.
- Headers the spec puts out of scope (`Permissions-Policy`, COOP, CORP, CSP reporting) and
  `no-store`/HSTS, which keep their own US-040 / SC-2 tests (FR-008, AC-006).

## 7. Known limitations

- The view scan is textual (VR-003): it does not render Razor, so markup produced by a tag helper or
  an `@Html.Raw` string is not seen. No view uses `Html.Raw` today; the CSP still blocks such code at
  runtime.
- The event-handler pattern (`on` + letters + `=` after whitespace) could match C# inside a Razor
  code block; the spec fixes the pattern, and a false positive would show as a red guard, not a
  hidden defect.

## 8. Open Decisions affecting testing

None (`docs/decisions/US-050-open-decisions.md`: none open).
