---
artifact_type: specification
story: US-041
version: 2
status: APPROVED
created_at: 2026-10-05T07:16:54Z
updated_at: 2026-10-05T07:26:24Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-041-unknown-file-path-404.md
    version: null
  - path: trebovaniya.md
    version: 85
  - path: docs/architecture/security-conventions.md
    version: null
  - path: docs/decisions/US-041-open-decisions.md
    version: 2
supersedes: null
---

# US-041 Specification — An unknown file-like address answers 404 to anyone

## 1. Overview

`trebovaniya.md` v66 (changelog; §8 "Страница ошибки") and SC-4 fix that an
unknown address answers `404` to anyone, an anonymous visitor included: each
host maps an anonymous catch-all that returns `404` (the error page, or the
API-6 body under `/api/v1`, which stays out of scope here — §10).

US-040 (test-generation report, finding F-2) found that this holds only for
paths that do not look like a file. The catch-all is registered with
`MapFallback(...)` without a pattern, which ASP.NET Core registers as
`{*path:nonfile}`: a path whose last segment contains a dot (`/robots.txt`,
`/css/no-such-file.css`, `/favicon.ico`) matches no endpoint, falls under the
fallback authorization policy and is challenged — `302` to sign-in.

This Story makes the catch-all of both hosts match **every** unmatched path,
file-like or not. Nothing is disclosed today; this is a conformance fix. No new
endpoint, page, text, status code for an existing address, cookie or header is
introduced.

## 2. Business Goal

Both hosts behave exactly as the requirements state: an address that nothing
serves answers `404` to anyone, and an anonymous visitor is never sent to
sign-in for a page that does not exist. A later security review then finds the
code and SC-4 in agreement.

## 3. Business Flow

1. A caller (anonymous, Admin or Dean on the installation; anonymous or the
   Owner on the Control Plane) requests a path.
2. If the path names a file that exists in the host's static files directory,
   the file is served (unchanged, SC-4 "Static files", v68).
3. Otherwise, if the path matches an endpoint, that endpoint runs (unchanged).
4. Otherwise — whatever the last segment looks like — the anonymous catch-all
   answers `404`, which the host's status-code re-execution turns into the
   error page.

## 4. Functional Requirements

**FR-001 Installation catch-all covers file-like paths.** On the installation's
public port, a request that matches no endpoint and no existing static file
answers `404` with the error page for an anonymous caller, an Admin and a Dean alike, including when
the last path segment contains a dot. It is never a redirect to sign-in.

**FR-002 Control Plane catch-all covers file-like paths.** On the Control
Plane, the same request answers `404` with the error page for an anonymous caller and for the Owner — both after the
Owner account exists and before it exists. Before setup it is `404`, not the
setup gate's `302 /setup` (OD-001): the catch-all stays exempt from the setup
gate, as it is today.

**FR-003 One catch-all per host, matching every path.** Each host keeps exactly
one anonymous catch-all (SC-4 "Error page" entry of the anonymous list); its
route pattern matches any path, without the `nonfile` constraint. It keeps the
lowest routing precedence, so it never wins over a real endpoint, and it reads
and writes nothing.

**FR-004 Existing static files are unchanged.** A file that exists in a host's
static files directory is still served anonymously with `200` (and with the
caching behaviour US-040 gave static files). Static files are served before
routing, so the catch-all never sees such a request.

**FR-005 Private port.** The installation's private port keeps its
behaviour (DC-6, SC-9): a path outside the private paths answers `404` from
`PublicPortMiddleware` without an error page. A request under a private path
prefix that matches no private endpoint (e.g. `/health/x.txt`) answers `404`
too — never a redirect to sign-in — and gets no error page, because the private
port has no status-code re-execution.

**FR-006 Nothing else changes.** Real protected pages still redirect an
anonymous caller to sign-in; the Control Plane setup gate still redirects
requests to non-exempt endpoints to `/setup` before setup; the Google OAuth
callback `/signin-google` is still handled by the authentication handler
(US-008); the error page, its texts and `Cache-Control` (US-040) are unchanged.

## 5. Acceptance Criteria

- **AC-001 Installation: a missing file-like path answers 404 to anyone.** On
  the public port, an anonymous caller, an Admin or a Dean requesting a path
  that nothing serves and whose last segment contains a dot (e.g.
  `/robots.txt`, `/css/no-such-file.css`) gets `404` with the error page — never
  a redirect to sign-in. (Story AC-001)
- **AC-002 Control Plane: the same.** With the Owner account existing, and also
  before it exists, an anonymous caller or the Owner requesting such a path gets
  `404` with the error page; before setup it is not `302 /setup`. (Story
  AC-002, OD-001)
- **AC-003 Existing static files are unchanged.** On either host, a file that
  exists in `wwwroot`, requested anonymously, is served with `200`. (Story
  AC-003)
- **AC-004 Nothing else changes.** The existing test suite stays green: the
  setup gate of the Control Plane, the sign-in redirect for real protected
  pages and the OAuth callback behave as before. (Story AC-004)

## 6. Validation Rules

No request body, query or form field is introduced. The only input is the
request path, and it is not interpreted: any path that matches no endpoint and
no existing static file is answered `404`.

- A path is "file-like" when its last segment contains a dot. The behaviour is
  the same for file-like and non-file-like paths; the distinction matters only
  for the tests, which must cover file-like paths.
- Test cases must include at least: a root-level file name (`/robots.txt`), a
  file name under an existing static directory that does not exist there
  (`/css/no-such-file.css`).
- The path is never echoed into the response or a log message beyond what the
  framework's request logging already records (SC-10).

## 7. Security Requirements

- The catch-all is the existing SC-4 anonymous entry "Error page, and the
  fallback catch-all that answers `404` for any unmatched request". Widening its
  pattern does not add an anonymous endpoint; no change to the anonymous list is
  needed.
- It returns only the status `404`; the body is the error page with translated
  text only (SC-4 "One error page per host"). It discloses
  nothing about whether a protected page, file or record exists.
- Deny by default is kept: every real endpoint keeps its authorization policy,
  and the fallback authorization policy still protects any matched endpoint
  without one. The catch-all never matches a path that a real endpoint matches
  (lowest precedence, FR-003).
- Static files remain limited to the application's static files directory,
  read-only (SC-4 v68).
- The private port's isolation (SC-9, DC-6) is unchanged (FR-005).
- No audit event: answering `404` to an unknown address is not an SC-11 action.

## 8. Error Handling

| Situation | Host | Response |
|---|---|---|
| Unmatched path (file-like or not) | installation public port, Control Plane | `404`, error page with the "not found" text, in the caller's language (signed in) or the default language (anonymous) |
| Unmatched path, Control Plane before setup | Control Plane | `404` as above, not `302 /setup` |
| Path outside private paths | installation private port | `404`, no body (unchanged) |
| Unmatched path under a private path prefix | installation private port | `404`, no error page |
| Existing static file | both (public port) | `200`, the file (unchanged) |

An exception while producing the `404` follows the existing `500` handling.

## 9. Non-Functional Requirements

- NFR-073: the error page is shown in the language rules already in force; no
  new translation entry.
- No new NuGet package; no database change (DB_DESIGN is expected
  NOT_APPLICABLE); no configuration change.
- Tests follow `testing-conventions.md`: per host, an anonymous and a signed-in
  caller (TC-5 spirit — the catch-all is anonymous by design, so "forbidden
  role" is replaced by "every role gets the same `404`").

## 10. Out of Scope

- Any change to the error page, its texts, sign-in or `Cache-Control` (US-040).
- The OAuth callback `/signin-google` (US-008) beyond confirming it still works.
- Responses Kestrel writes before the application pipeline (see US-040 OD-001).
- Any change to which static files exist or how they are served.
- **The API-6 body for an unmatched path under `/api/v1`** (OD-002). SC-4 v66
  says the catch-all answers with the API-6 body under `/api/v1`; neither host
  does that today for any unmatched path — the status-code re-execution renders
  the error page — and neither host has an `/api/v1` resource yet. Under
  `/api/v1` an unmatched path, file-like or not, keeps answering `404` with the
  error page. The first Story that adds an `/api/v1` resource to a host makes
  that host's unmatched `/api/v1` paths answer with the API-6 body.

## 11. Open Decisions

- **OD-001 The Control Plane before setup** — resolved by the Owner
  (2026-10-05): `404`, no first-run exception. Impact: FR-002, AC-002. See
  `docs/decisions/US-041-open-decisions.md`.
- **OD-002 The API-6 body under `/api/v1`** — raised at API_DESIGN (finding
  API F-1), resolved by the Owner (2026-10-05): out of scope for US-041; see
  §10. Impact: FR-001, FR-002, §6, §8.

No unresolved Open Decision. `trebovaniya.md` §7 holds no open question this
Story depends on.

## 12. Traceability

| Acceptance Criterion | Functional requirements | Validation / other |
|---|---|---|
| AC-001 | FR-001, FR-003 | §6 file-like test cases; §7 |
| AC-002 | FR-002, FR-003 | OD-001; §6 |
| AC-003 | FR-004 | §7 static files |
| AC-004 | FR-005, FR-006 | §8 |
