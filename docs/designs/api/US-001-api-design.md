---
artifact_type: api_design
story: US-001
version: 1
status: DRAFT
created_at: 2026-09-16T08:13:25Z
updated_at: 2026-09-16T08:13:25Z
produced_by: openapi-designer
inputs:
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/decisions/US-001-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 68
supersedes: null
---

# US-001 API Design — Owner first-run setup and sign-in

Contract: `docs/designs/api/US-001-openapi.yaml` (`info.version: "1"`).

## 1. Rationale

- **No `/api/v1` endpoint.** Every screen of this Story is a server-rendered
  Razor page and every state change is an HTML form post (`trebovaniya.md` §8:
  anonymous forms with a hidden antiforgery field; AC-011: the Owner sees the
  error page). Nothing calls the pages by script, so no REST resource is needed
  and none is added. Consequently no response of this Story uses the API-6 JSON
  body: errors are the error page or the form shown again (AD-9, SC-4 v66).
- **Why a contract anyway.** The Story is the Control Plane's security baseline.
  Test-writer needs fixed routes, form field names, status codes, redirect
  targets, cookie names and message keys to write the TC-3/TC-5 tests before the
  code exists. The OpenAPI file describes the pages as operations with
  `application/x-www-form-urlencoded` bodies and `text/html` responses; `x-`
  keys carry the security and traceability metadata.
- **Not `NOT_APPLICABLE`.** The approved Specification does not state that the
  Story has no HTTP behaviour; it defines pages, forms and status codes.

## 2. Routes

| Method | Path | Anonymous | Policy | Purpose | Spec |
|---|---|---|---|---|---|
| GET | `/setup` | yes (SC-4 "First-run setup") | — | setup form | FR-001, FR-003 |
| POST | `/setup` | yes (same) | — | create the Owner account | FR-004 … FR-006 |
| GET | `/sign-in` | yes (SC-4 "Owner sign-in") | — | sign-in form | FR-007 |
| POST | `/sign-in` | yes (same) | — | sign in | FR-008 … FR-010 |
| POST | `/sign-out` | no | `Owner` | end the session | FR-011 |
| GET | `/` | no | `Owner` | home page | FR-013 |
| GET | `/error/{statusCode}` | yes (SC-4 "Error page") | — | error page for 400/403/404/500 | FR-016 |
| any | unmatched | yes (SC-4 "Error page", catch-all) | — | `404` | FR-016 |
| GET | static files (`/css/…`, `/js/…`, `/images/…`) | yes (SC-4 "Static files", v68) | — | CSS, JS, images | — |

Kebab-case paths, no verbs beyond the unavoidable `sign-in` / `sign-out`
(API-3 applies to `/api/v1`; the same style is kept for pages).

The service-channel endpoints of the Control Plane (legitimacy check, Admin login
check) are not part of this Story.

## 3. Host-wide behaviour

Applied in this order to every request:

1. **HTTPS only** — the host has no HTTP listener; no HSTS (SC-2, DC-6).
2. **Static files** — served before the gate; only files that exist in the
   static files directory; anything else continues.
3. **Setup gate** (FR-001) — while no Owner account exists, a request to `/`,
   `/sign-in` (GET or POST) or `/sign-out` answers `302 Location: /setup`.
   Exempt: `/setup`, `/error/{statusCode}`, static files, the catch-all. The gate
   decides by path/endpoint, not by listing the exempt pages in each page. It
   must not cache "no Owner" beyond the moment the account is created.
4. **Authentication / authorization** — cookie authentication; fallback policy
   = authenticated Owner; `LoginPath = /sign-in`, `AccessDeniedPath = /error/403`,
   and no return-URL parameter (spec I-6).
5. **Antiforgery** — global for every POST; a refusal is turned by a result
   filter into `400` + error page `Error.PageExpired` with a link back to the
   page (API-7, SC-4). No endpoint is exempt in this Story.
6. **Exceptions** — the single `IExceptionHandler` re-executes `/error/500`
   (AD-9); status codes without a body (`403`, `404`) are shown through
   `UseStatusCodePagesWithReExecute("/error/{0}")`.

Consequence for anonymous POSTs to `/sign-out`: the challenge (`302 /sign-in`)
comes before antiforgery. The TC-5 antiforgery test for `/sign-out` therefore
sends the request as a signed-in Owner and expects `400`.

## 4. Operations

### GET /setup

- No account: `200`, form with `setupCode`, `login`, `password`,
  `passwordConfirmation`, token; secrets never pre-filled.
- Account exists: anonymous `302 /sign-in`; signed-in Owner `302 /` (I-1). The
  two responses carry nothing that identifies the account.

### POST /setup

Processing order (OD-004), each step only if the previous passed:

| Step | Check | Failure response | Audit |
|---|---|---|---|
| 1 | antiforgery token | `400`, error page `Error.PageExpired` | — |
| 2 | binding: VR-001 login, VR-002 password, VR-003 password vs login, VR-007 confirmation | `400`, setup form with one message per failed field; `login` refilled, secrets empty | — |
| 3 | Owner account exists | `409`, setup page with `Setup.AlreadyCreated` and a link to `/sign-in` (OD-003) | — |
| 4 | setup code (VR-004, OD-005) — empty = wrong | `400`, setup form, `Setup.SetupCode.Invalid` on `setupCode` | refused, anonymous, "wrong setup code" |
| 5 | create account in one transaction with the success audit row | concurrent loser: `409` as step 3, transaction rolled back (FR-006) | succeeded, the new Owner id |

Success: `302 /`, `Set-Cookie` session, code voided.

Step 2 reports **all** failed fields at once, not the first only. When VR-002
fails, VR-003 is still evaluated if the password is non-empty. VR-007 is
reported independently of VR-002.

The step-5 conflict is detected from the database outcome (the uniqueness
mechanism is `db-designer`'s) and mapped in `ControlPlane.Services` to the same
conflict result as step 3 — never `500`.

### GET /sign-in

- Account exists, no session: `200`, form with `login`, `password`, token.
- Signed-in Owner: `302 /` (I-1). No account: `302 /setup` (gate).

### POST /sign-in

| Case | Response | Audit | Counter |
|---|---|---|---|
| antiforgery refusal | `400`, error page | — | — |
| `login` or `password` empty | `400`, form naming the field (I-2) | — | — |
| unknown login | `401`, form, `SignIn.Refused` | refused, anonymous, "unknown login" | — |
| lockout in force (password not checked) | `401`, identical | refused, Owner, "locked out" | unchanged |
| wrong password | `401`, identical | refused, Owner, "wrong password" | +1; the 5th starts a 15-minute lockout |
| success | `302 /`, session cookie | succeeded, Owner | reset |

"Identical" is tested: status, body (apart from the fresh antiforgery token)
and headers of the three `401` cases must not differ. The login lookup is
case-insensitive. The sequence is not built on
`SignInManager.PasswordSignInAsync` / `CheckPasswordSignInAsync` (SC-2 v66).

### POST /sign-out

- Signed-in Owner with token: `302 /sign-in`, session cookie deleted; the old
  cookie no longer authenticates (the cookie is invalidated server-side, e.g. by
  a security-stamp check, not only deleted client-side).
- Signed-in Owner without token: `400`, error page; the session stays.
- No session: `302 /sign-in`. No account: `302 /setup`.
- `GET /sign-out`: no GET endpoint — the catch-all answers `404`; the session is
  unaffected (API-4).

### GET /

- Signed-in Owner: `200`, signed-in state and a sign-out form. No session:
  `302 /sign-in`. No account: `302 /setup`.

### GET /error/{statusCode}

- `statusCode` ∈ {400, 403, 404, 500}: that status with its translated text only.
- Any other value: `404`, `Error.NotFound`.
- Anonymous: Ukrainian. Signed-in Owner: account language.

### Catch-all

- `MapFallback(...).AllowAnonymous()`: any method, any unmatched path, any time —
  `404`, `Error.NotFound`; never a redirect.

### Static files

- Anonymous, read-only, only existing files of the static files directory;
  missing → `404`. Whatever the mechanism (`UseStaticFiles` middleware or
  `MapStaticAssets` endpoints), the TC-5 anonymous-endpoint enumeration counts
  them under the SC-4 "Static files" entry and fails on any other anonymous
  endpoint.

## 5. Request models

Form models live in the Control Plane (it cannot reference `Application`,
AD-3); they are DTO-style request types, never persistence entities (AD-8).

| Model | Field | Rules |
|---|---|---|
| `SetupRequest` | `setupCode` | no binding rule (OD-004); normalized (hyphens, spaces removed, case-insensitive) and compared in constant time in the service (OD-005) |
| | `login` | required; 4–64; `^[A-Za-z0-9._-]{4,64}$` (VR-001) |
| | `password` | required; 15–128 **code points**; no trimming; not equal to / not containing `login`, case-insensitive (VR-002, VR-003) |
| | `passwordConfirmation` | required; ordinal equality with `password` (VR-007) |
| `SignInRequest` | `login` | required (non-empty) only (VR-005) |
| | `password` | required (non-empty) only |

Length in code points needs a custom `ValidationAttribute`: `StringLength` counts
UTF-16 units. Whitespace-only `login` is invalid (it fails the pattern anyway).

`__RequestVerificationToken` is posted with each form and consumed by
antiforgery validation, not bound to the model.

No response model carries a password, hash, setup code or lockout detail
(AD-8).

## 6. Auth model

- **Scheme:** ASP.NET Core Identity cookie authentication for the Owner (API-7).
- **Principal:** Owner account id, role `Owner`, account language, sign-in time.
- **Policy `Owner`:** authenticated and role `Owner`. Fallback policy: authenticated
  (SC-4). With a single account and role, no `403` case exists in this Story; the
  `AccessDenied` path is still wired to `/error/403` and tested with a principal
  lacking the role.
- **Session cookie:** `__Host-cp-session`; `Path=/`, `Secure`, `HttpOnly`,
  `SameSite=Strict`, no `Domain`, no `Expires`/`Max-Age` (OD-002, v68). The
  `__Host-` prefix makes the browser refuse the cookie unless it is `Secure`,
  host-only and path `/`.
- **Expiry:** sliding 30 minutes; absolute 8 hours from the sign-in time in the
  principal, checked on every request (cookie validation event); an expired
  session behaves as no session (`302 /sign-in`). No "remember me".
- **Antiforgery cookie:** `__Host-cp-antiforgery`; `Path=/`, `Secure`,
  `HttpOnly`, `SameSite=Strict`.
- **Every other cookie** the host sets is `Secure` (SC-2).
- **Data Protection:** key ring in the configured directory (FR-017); a restart
  keeps sessions and open forms valid.

## 7. Error model

No JSON error body. Two presentation forms:

| Form | Used for | Content |
|---|---|---|
| Error page `/error/{code}` | `400` antiforgery, `403`, `404`, `500` | one translated text, no detail; `400` adds a link back to the page |
| Form shown again | `400` validation / wrong code, `401` refused sign-in, `409` setup conflict | the same page with field-level or form-level translated messages |

Message keys (both `uk` and `en` in `ControlPlane.Localization`, TC-8):
`Setup.Login.Required`, `Setup.Login.Length`, `Setup.Login.Characters`,
`Setup.Password.Required`, `Setup.Password.Length`, `Setup.Password.ContainsLogin`,
`Setup.PasswordConfirmation.Required`, `Setup.PasswordConfirmation.Mismatch`,
`Setup.SetupCode.Invalid`, `Setup.AlreadyCreated`, `SignIn.Login.Required`,
`SignIn.Password.Required`, `SignIn.Refused`, `Error.PageExpired`,
`Error.Forbidden`, `Error.NotFound`, `Error.Internal`. Page titles and labels get
their own keys at the implementor's choice; every one exists in both languages.

`Setup.Password.ContainsLogin` covers both "equals" and "contains". No message
repeats a submitted password, confirmation or code. `SignIn.Refused` is the §9
text: wrong login or password, or sign-in temporarily locked after several
failed attempts — try again later.

## 8. Acceptance Criterion → operation map

| AC | Operations | Key assertions |
|---|---|---|
| AC-001 | GET `/`, GET/POST `/sign-in`, POST `/sign-out`, GET `/setup`, GET `/error/*`, catch-all | before setup every page → `302 /setup`; error page and unknown paths not redirected; setup form has 4 fields |
| AC-002 | POST `/setup` | `302 /`; session cookie; one account; hash only; language `uk`; no installation write |
| AC-003 | GET/POST `/setup` | after setup: GET anonymous `302 /sign-in`, signed-in `302 /`; POST `409`; no second account |
| AC-004 | POST `/setup` ×2 concurrently | one account; loser `409`, not `500`; no partial rows |
| AC-005 | POST `/sign-in` | `302 /` + cookie; case-insensitive login; three `401` cases identical; lockout after 5; 15-minute release; reset on success |
| AC-006 | POST `/setup` | `400` per VR-001/002/003/007 boundary; field messages; 15-char plain and 128-char passwords accepted; secrets not in response |
| AC-007 | POST `/setup` | missing/wrong code `400`; expected code absent from response; code void after setup; new code after restart |
| AC-008 | POST `/sign-in` | audit rows per case; no typed login/password |
| AC-009 | all pages | Ukrainian by default; keys exist in `uk` and `en` |
| AC-010 | all | anonymous only: `/setup`, `/sign-in`, `/error/{statusCode}`, catch-all, static files |
| AC-011 | POST `/setup`, `/sign-in`, `/sign-out` | no token → `400` error page; `GET /sign-out` → `404`, still signed in; cookie attributes |
| AC-012 | POST `/setup` | success row with new Owner id; wrong/missing code row anonymous "wrong setup code"; field-validation failures not audited |

## 9. Compatibility

First contract of the Control Plane; no existing contract changes. Later Stories
add pages under the same host rules; the setup gate and the fallback policy cover
them without per-page code.

## 10. Open questions

None blocking. Notes for later stages:

- **Spec input version.** The Specification records `trebovaniya.md` v67; this
  design reads v68. The v68 additions (session lifetime, static files in SC-4,
  Control Plane logging) do not contradict the Specification and are applied
  here (§4 static files, §6 expiry).
- **Control Plane logging (v68, DC-10)** is outside the HTTP contract; it binds
  `dotnet-implementor` and `security-reviewer`: the setup code, typed login and
  password never reach a log line.
- **Setup code in tests.** The contract fixes behaviour, not how tests obtain the
  code; the Specification (§9) leaves a substitutable source to the implementor.
