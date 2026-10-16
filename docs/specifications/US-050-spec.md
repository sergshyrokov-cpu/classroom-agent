---
artifact_type: specification
story: US-050
version: 1
status: APPROVED
created_at: 2026-10-16T07:50:00Z
updated_at: 2026-10-16T07:55:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-050-browser-security-headers.md
    version: null
  - path: trebovaniya.md
    version: 96
  - path: docs/decisions/US-050-open-decisions.md
    version: 1
supersedes: null
---

# US-050 Specification — Every response carries the browser security headers

## 1. Overview

Both hosts — the installation (public and private port) and the Control Plane —
gain one host-wide rule that adds four browser security headers to every
response: `X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy`
and `Referrer-Policy`. The rule and its values come from `trebovaniya.md` §8
"Защитные заголовки браузера" (v95) and SC-15:

> «Каждый ответ обоих хостов — на всех портах, включая статические файлы, страницу
> ошибки, выгружаемые файлы и ответы REST API — несёт заголовки: `X-Frame-Options:
> DENY` и в CSP `frame-ancestors 'none'` …; `X-Content-Type-Options: nosniff` …;
> `Content-Security-Policy` — скрипты, стили, картинки и шрифты загружаются только с
> этого же хоста; встроенный в страницу код и обработчики событий в разметке не
> выполняются; формы отправляются только на этот же хост, а в инсталляции ещё и на
> адрес входа Google, куда ведёт кнопка «Войти через Google»; подключаемых объектов
> нет; `Referrer-Policy: same-origin` … Как и `no-store`, заголовки ставит одно
> правило на хост, а не пометки на отдельных страницах.»

Origin: pre-pilot security audit 2026-10-15, finding F-3 (neither host sends
`X-Content-Type-Options`, a CSP or a `Referrer-Policy`; `X-Frame-Options:
SAMEORIGIN` appears only on responses that generated an antiforgery token).

The Story changes no endpoint, route, status code, response body, entity, table,
translation entry, audit event or log event. Nothing a user sees changes.

Pattern: US-040's `NoStoreMiddleware` (SC-14) — one middleware per host,
registered first in the pipeline, deciding its header when the response starts,
with an endpoint-enumerating test per host.

What was looked up while writing (read only): `trebovaniya.md` §8 v95 and §7
(no open item concerns headers, CSP or framing); SC-14, SC-15; the two
`NoStoreMiddleware` classes and both `Program.cs` pipelines; the Google handler
configuration in `InstallationSecurityServices.ConfigureGoogle`; every `.cshtml`
of both hosts and every file under both `wwwroot` directories.

## 2. Business Goal

A mistake in a future page, or a hostile site, must not be able to turn a
signed-in Admin's, Dean's or Owner's browser against the school's data: no page
can be framed for clickjacking, the browser does not guess file types, code runs
only from the host's own static files, and page addresses (which can carry
query parameters) do not leak to other sites. This is a second layer: the audit
found no exploitable hole today.

## 3. Business Flow

Not a user flow. For every HTTP request either host answers, whatever its
outcome — page, redirect, refusal, error page, 404, export file, `/api/v1` JSON,
static file, health check, status-push reply — the response leaves the host
carrying the four headers with the values of FR-003 / FR-004.

The one flow that the policy could break is the Admin's Google sign-in:
the "Sign in with Google" button is a POST form to the installation's own sign-in
start endpoint, which answers with a redirect to Google's authorization endpoint.
Browsers check a form's CSP `form-action` against every redirect that follows the
form submission, so the installation's `form-action` names the Google sign-in
origin (FR-004, I-2). The return from Google to the callback is a navigation
started by Google's page and is not governed by the installation's policy.

## 4. Functional Requirements

### FR-001 One host-wide rule per host
Each host has exactly one rule — a middleware in its `Security` namespace
(`ClassroomAgent.Web.Security`, `ClassroomAgent.ControlPlane.Security`) — that
sets the four headers. No controller, action, page model or view sets, removes or
overrides any of the four headers (SC-15 "one host-wide rule").

### FR-002 Every response, every port
The rule covers every response of the host, with no exception — unlike SC-14,
static files are **not** exempt (§8 v95 «включая статические файлы»):

- installation public port: signed-in pages, anonymous pages (sign-in, Dean
  sign-in, error page), redirects (including the HTTP→HTTPS redirection and the
  redirect to Google), refusals (401/403/404/409 …), the re-executed error page
  (`/error/{code}`, `/error/500`), downloaded files, `/api/v1` responses, static
  files, an unknown address;
- installation private port: health checks, the status-push receiver, and the
  404 of every other address;
- Control Plane: the same kinds of response, both before the Owner account exists
  (setup gate) and after.

To achieve this the rule is registered at the very start of each host's
pipeline (beside `NoStoreMiddleware`, before `PublicPortMiddleware`, the
public-port branch, the exception handler and the static-file middleware) and
writes its headers when the response starts (`Response.OnStarting`), so responses
produced further down — including after the exception handler clears the
response — are covered.

### FR-003 Header values common to both hosts

| Header | Value (exact) |
|---|---|
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `same-origin` |

### FR-004 The Content-Security-Policy of each host

Control Plane (exact value):

```
default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'
```

Installation (exact value; identical on both ports):

```
default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self' https://accounts.google.com; frame-ancestors 'none'
```

- `default-src 'self'` is the catch-all for every fetch directive §8 does not
  name (connections from `fetch`, frames, media, workers, manifests): without it
  those would be unrestricted, and the export scripts' `fetch` to `/api/v1`
  needs only the same host (I-1).
- `https://accounts.google.com` is the origin (scheme and host) of the
  authorization endpoint the installation's Google handler redirects to. The
  handler does not override `AuthorizationEndpoint`
  (`InstallationSecurityServices.ConfigureGoogle`), so it is the
  ASP.NET Core default `GoogleDefaults.AuthorizationEndpoint`
  (`https://accounts.google.com/o/oauth2/v2/auth`); `OnRedirectToAuthorizationEndpoint`
  only appends the `hd` hint to that address. The origin, not the full path, is
  listed (I-2). Whether the implementation derives it from the configured
  `GoogleOptions.AuthorizationEndpoint` or holds it as a constant is a technical
  choice; either way a test asserts that the origin in the CSP equals the origin
  of the endpoint the handler actually redirects to.
- No other origin, no scheme source (`data:`, `blob:`, `https:`), no
  `'unsafe-inline'`, no `'unsafe-eval'`, no nonce or hash.

### FR-005 Exactly one value per header
Each of the four headers appears exactly once on a response, with the value of
FR-003 / FR-004. A value written further down the pipeline is replaced, not
kept beside the rule's value. Today this concerns antiforgery, which writes
`X-Frame-Options: SAMEORIGIN` on responses that generate a token; after the
Story those responses carry `X-Frame-Options: DENY` only. (Whether the rule
overwrites the value or antiforgery's header is suppressed via
`AntiforgeryOptions.SuppressXFrameOptionsHeader` is a technical choice; the
observable result is fixed.)

### FR-006 No inline code in the interface
No Razor view (`.cshtml`) of either host contains:

- a `<script>` element with a body (a `<script src="…">` element with an empty
  body is allowed);
- an event-handler attribute (`on` followed by letters, e.g. `onclick=`,
  `onsubmit=`) — `data-*` attributes such as `data-copy-target` are not event
  handlers;
- a `style` attribute.

At the time of writing every view already complies (scan of all `.cshtml`): the
scripts are the five files `wwwroot/js/copy-instruction.js`, `meet-export.js`,
`report-export.js`, `report-template-scale.js` (installation) and
`copy-identifier.js` (Control Plane), included with `src`; the stylesheet is
`wwwroot/css/site.css` per host, with no `url()` or `@import`. No resource is
loaded from another site. The Story therefore adds the guard test; it changes no
view unless the test finds a violation.

### FR-007 Existing interface keeps working
Under the policy, every existing page keeps its behaviour: the Google sign-in
round trip (AC-004), the journal/report export and the Meet export (script
`fetch` to the same host, then a `blob:` download link), copying the connection
instruction and the installation identifier, the report template form's scale
script, the language switcher, sign-out, and every POST form with its
antiforgery token. Setting a style property from a script file (CSSOM) is not
inline style and stays allowed.

### FR-008 Unchanged neighbours
`Cache-Control: no-store` and its static-file exemption (SC-14, US-040), HSTS and
HTTPS redirection on the public port and their absence on the private port
(SC-2), cookies, antiforgery validation and the authorization policies are
unchanged.

## 5. Acceptance Criteria

Story ids are kept.

- **AC-001 Installation: every response carries the headers.** On the public and
  the private port, any endpoint — signed-in page, sign-in page, error page,
  redirect, export, `/api/v1` response, static file, unknown address — returns
  `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: same-origin` and the installation CSP of FR-004 (with
  `frame-ancestors 'none'`), each exactly once. → FR-001, FR-002, FR-003, FR-004,
  FR-005.
- **AC-002 Control Plane: the same.** With the Owner account existing and before
  it exists, any endpoint returns the same three headers and the Control Plane CSP
  of FR-004, whose `form-action` is `'self'` only. → FR-001, FR-002, FR-003,
  FR-004, FR-005.
- **AC-003 The CSP is strict.** Each host's CSP allows scripts, styles, images and
  fonts from `'self'` only, contains neither `'unsafe-inline'` nor
  `'unsafe-eval'`, sets `object-src 'none'` and `base-uri 'self'`, and names no
  origin other than, in the installation's `form-action`, the Google sign-in
  origin. → FR-004.
- **AC-004 The Google sign-in still works.** On the installation's sign-in page,
  pressing "Sign in with Google" lets the browser follow the form to Google, and
  the return through the callback signs the Admin in as today. Automated part:
  the sign-in start response (the redirect to Google) carries the installation
  CSP, and the origin of its `Location` is the origin listed in `form-action`.
  Manual part (a header test cannot prove the browser obeys): one real-browser
  sign-in on a running installation, recorded in the implementation report as
  US-047 recorded its manual check. → FR-004, FR-007.
- **AC-005 No inline code in the interface.** A test scans every `.cshtml` of both
  hosts and finds no `<script>` block with a body, no `on…=` event-handler
  attribute and no `style` attribute. → FR-006.
- **AC-006 Nothing else changes.** The existing test suite stays green. → FR-007,
  FR-008.

## 6. Validation Rules

No external input is added or changed: the rule reads nothing from the request.

- VR-001 Header names and values are compared exactly as in FR-003 / FR-004
  (case of values included; directive order as written).
- VR-002 Each header occurs exactly once per response (FR-005).
- VR-003 The view scan (FR-006) treats as violations: `<script` whose element is
  followed by non-whitespace content before `</script>`; an attribute whose name
  is `on` + one or more letters, preceded by whitespace; an attribute named
  `style`. Matching is case-insensitive.
- VR-004 The endpoint enumeration covers every routed endpoint of the host and
  every HTTP method it accepts, plus a static file and an unknown address, as the
  US-040 `NoStoreResponseTests` enumeration does.

## 7. Security Requirements

- SC-15 in full: values of FR-003 / FR-004, one host-wide rule, no inline code,
  no resource from another site.
- SC-13: the only origin outside the host in either policy is Google's sign-in
  origin, a destination SC-13 already lists; no CSP report endpoint is added
  (no new outbound destination).
- SC-14, SC-2: unchanged (FR-008).
- SC-4: no anonymous access changes — the rule adds headers, never content or
  routes.
- Static files remain free of data and of anything user-dependent (§8 v68).

## 8. Error Handling

No new error path. A response the host produces for an error — exception handler
(`/error/500`), re-executed status page (`/error/{code}`), framework 404/405,
the Control Plane setup gate, the public-port refusal of private-port paths —
carries the headers like any other (FR-002). The rule itself cannot fail on
input; it writes constant values.

## 9. Non-Functional Requirements

- No new NuGet package (Story notes; `AGENTS.md` Technology Stack).
- No measurable cost: four constant header writes per response.
- No translation entries, no audit events (SC-11 unaffected), no log events.

## 10. Out of Scope

- `Cache-Control: no-store` (SC-14, US-040) — unchanged.
- HSTS and HTTPS redirection (SC-2) — unchanged; no `upgrade-insecure-requests`
  (the private port is plain HTTP by design).
- Headers §8 does not name (`Permissions-Policy`, `Cross-Origin-Opener-Policy`,
  `Cross-Origin-Resource-Policy` and the like) and CSP reporting
  (`report-uri` / `report-to`).
- Any change to what pages show.

## 11. Open Decisions

None. The Story carries none, and no gap was found: the rule and its values are
fixed by `trebovaniya.md` §8 v95 and SC-15. `trebovaniya.md` §7 (v96) has no open
item this Story depends on. See `docs/decisions/US-050-open-decisions.md`.

### Interpretations made (for the reviewer and the Owner)

- **I-1 `default-src 'self'`.** §8 names scripts, styles, images, fonts, forms,
  objects and framing. The Specification adds `default-src 'self'` so that every
  directive §8 does not name (notably `connect-src`, `frame-src`) is limited to
  the same host instead of being unrestricted. This is stricter, never looser,
  than §8, and no existing page needs anything else (FR-006 scan).
- **I-2 Google origin, not the full address.** "The Google sign-in address" is
  listed as the origin `https://accounts.google.com`. Browsers ignore the path of a
  source once a redirect has happened and Google moves between paths on that
  origin during sign-in, so a full path would add nothing. If the manual check of
  AC-004 shows the browser blocked on another origin in the redirect chain, that
  origin is **not** added during implementation: it goes back to the
  Specification (and, if needed, to `trebovaniya.md`).
- **I-3 Static files carry the headers.** Unlike `no-store`, §8 v95 explicitly
  includes static files, so the SC-14 exemption is not reused.
- **I-4 One value per header.** Antiforgery's `X-Frame-Options: SAMEORIGIN` is
  replaced by `DENY` (FR-005), since two different values would leave the browser
  to choose.

## 12. Traceability

| AC | Functional requirements | Validation rules | Source |
|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-003, FR-004, FR-005 | VR-001, VR-002, VR-004 | §8 v95, SC-15 |
| AC-002 | FR-001, FR-002, FR-003, FR-004, FR-005 | VR-001, VR-002, VR-004 | §8 v95, SC-15 |
| AC-003 | FR-004 | VR-001 | §8 v95, SC-15, SC-13 |
| AC-004 | FR-004, FR-007 | VR-001 | §8 v95 (Google form address), SC-15 |
| AC-005 | FR-006 | VR-003 | §8 v95, SC-15 |
| AC-006 | FR-007, FR-008 | — | Story, SC-14, SC-2 |

Expected downstream stages (Story notes): API_DESIGN NOT_APPLICABLE (no endpoint,
route, status code or body changes; headers only); DB_DESIGN NOT_APPLICABLE (no
schema change).
