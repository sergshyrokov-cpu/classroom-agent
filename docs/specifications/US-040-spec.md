---
artifact_type: specification
story: US-040
version: 2
status: APPROVED
created_at: 2026-10-04T21:09:54Z
updated_at: 2026-10-04T21:11:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-040-no-store-responses.md
    version: null
  - path: trebovaniya.md
    version: 85
  - path: docs/architecture/security-conventions.md
    version: null
  - path: docs/decisions/US-040-open-decisions.md
    version: 2
supersedes: null
---

# US-040 Specification — Responses are not cached (Cache-Control no-store)

## 1. Overview

`trebovaniya.md` v85 §8 ("Ответы не кешируются") requires that every response
of both hosts, except the static files of the interface, carries the header
`Cache-Control: no-store`. The header is set by **one rule per host**, not by
marks on individual pages, and a test checks it on every address of the host
(SC-14).

This Story adds that rule to the installation (`ClassroomAgent.Web`, public and
private port) and to the Control Plane (`ClassroomAgent.ControlPlane`), and the
tests that prove it. No page, endpoint, status code, cookie or session
behaviour changes.

## 2. Business Goal

On a shared computer (staff room, library) nobody may see a journal or a report
with students' grades — personal data, possibly of minors — from the browser's
memory after the Dean or Admin signed out (the Back button), and no
intermediate server may keep a copy (`trebovaniya.md` v85 §8, header changelog).
The rule must be in place before the export Stories US-028 … US-030 add files
with such data.

## 3. Business Flow

1. A user (signed in or anonymous) or the Control Plane channel sends a request
   to a host.
2. The host processes it as today — page, redirect, file, `/api/v1` answer,
   error page, or a refusal by authentication, authorization or antiforgery.
3. Before the response leaves the host, the host-wide rule makes sure it
   carries `Cache-Control` with the `no-store` directive.
4. A request for a static file of the interface that the host serves from its
   static-file directory (`wwwroot`) is the only exception: its response keeps
   the caching the host gives static files today.

## 4. Functional Requirements

**FR-001 Installation, every response.** Every HTTP response of the
installation, on the public port and on the private port, carries a
`Cache-Control` header whose directives include `no-store`. This covers:
signed-in pages, anonymous pages (sign-in, landing, error pages), redirects
(`302`/`303`, including the redirect to sign-in), downloaded files, `/api/v1`
responses, health-check responses, and every error response — `400`, `401`,
`403`, `404`, `405`, `409`, `500` — including responses of requests refused by
authentication, authorization, antiforgery or read-only mode, and the response
produced by the exception handler and the status-code page re-execution.
(`trebovaniya.md` v85 §8; SC-14)

**FR-002 Control Plane, every response.** The same as FR-001 for every HTTP
response of the Control Plane: Owner pages, anonymous pages (sign-in, first
run, error pages), redirects, `/api/v1` responses of the installation channel,
health checks and every error or refusal response. (`trebovaniya.md` v85 §8;
SC-14)

**FR-003 One host-wide rule.** In each host the header is set by exactly one
rule registered in the host's request pipeline, which applies to every response
without any per-page, per-controller or per-action mark. A page or endpoint
added later is covered with no change of its own. No controller, Razor page,
action or filter attribute sets `Cache-Control` for this purpose. (SC-14)

**FR-004 Static files excluded.** A response that serves an existing file from
the host's static-file directory (`wwwroot`: CSS, JS, images) does not carry
the `no-store` directive; its caching stays as it is today. The exception is
limited to the file actually being served: a request under a static path that
finds no file (`404`) is an ordinary response and falls under FR-001/FR-002.
(`trebovaniya.md` v68, v85 §8; SC-14)

**FR-005 No other change.** The rule does not change the status code, body,
redirect target, cookies, session lifetime or any other header a response
carries today, except `Cache-Control` (and the `Pragma`/`Expires` headers if
the existing pipeline already sets them). Sign-out and session limits (SC-2)
are untouched.

**FR-006 Directives already set by the framework.** Where the framework
already writes a `Cache-Control` value that contains `no-store` (for example
antiforgery token generation writes `no-cache, no-store`, the exception handler
writes `no-cache,no-store`), that value satisfies FR-001/FR-002 and is not
required to be rewritten to exactly `no-store`. Where an existing value lacks
`no-store`, the rule makes the final value carry it.

## 5. Acceptance Criteria

| Id | Criterion (from the Story) | Covered by |
|---|---|---|
| AC-001 | Every response of the installation, public and private port — signed in or anonymous, success, redirect or error (`400`, `403`, `404`, `409`, `500`) — carries `Cache-Control: no-store`. | FR-001, FR-003, FR-006, VR-001, VR-002 |
| AC-002 | Every response of the Control Plane — signed in or anonymous, success, redirect or error — carries `Cache-Control: no-store`. | FR-002, FR-003, FR-006, VR-001, VR-002 |
| AC-003 | A static file of the interface (CSS, JS, image) of either host does not carry `Cache-Control: no-store`. | FR-004, VR-003 |
| AC-004 | Per host, a test enumerates every endpoint (as the US-008 enumeration test does) and asserts the header on each; the header is set by one host-wide rule, not by attributes. | FR-003, VR-004 |
| AC-005 | The existing test suite stays green; no page, status code, redirect, cookie or session behaviour changes. | FR-005 |

## 6. Validation Rules

This Story accepts no new input; the rules below define when a response
satisfies the requirement.

- **VR-001 Directive check.** A response satisfies FR-001/FR-002 when it has a
  `Cache-Control` header and splitting its value on `,` and trimming gives a
  directive equal to `no-store` (case-insensitive). A missing header, or a
  header without that directive (e.g. `no-cache` alone, `private`,
  `max-age=…`), fails.
- **VR-002 Response kinds checked.** For each host the checks cover at least:
  one anonymous page, one signed-in page per role that has pages on that host,
  a redirect to sign-in, a `403` refusal, a `404`, an antiforgery refusal
  (`400`), an exception-handler `500`, a `/api/v1` success and a `/api/v1`
  error response, and — for the installation — a response on the private port
  and a downloaded file where one exists at the time of implementation.
- **VR-003 Static file check.** A request for an existing file in each host's
  `wwwroot` returns `200` and its `Cache-Control` either is absent or contains
  no `no-store` directive. A request for a missing file under the same path
  returns a response that satisfies VR-001.
- **VR-004 Enumeration.** The per-host test reads every routed endpoint of the
  host from the endpoint data source (the `HostEndpoint` enumeration of
  US-008/TC-5) and sends a request to each; each response, whatever its
  status, satisfies VR-001. An endpoint the test cannot reach is a test failure,
  not a skip.

## 7. Security Requirements

- **SR-001** The rule is a confidentiality control for personal data of
  students (SC-14; `AGENTS.md` Security Policy). It applies regardless of the
  caller's role, of authentication state and of read-only mode.
- **SR-002** The rule is deny-by-default in the caching sense: a response is
  non-cacheable unless it is a served static file (FR-004). No configuration
  switch turns it off.
- **SR-003** It does not replace sign-out or the session limits of SC-2; an
  unfinished session is protected by its expiry (`trebovaniya.md` v85 §8, v68).
- **SR-004** It adds no endpoint, no anonymous access (SC-4 unchanged) and no
  outbound data flow (SC-13 unchanged).

## 8. Error Handling

- Error responses are in scope, not exempt: the exception handler's `500` page,
  status-code pages (`/error/{code}`) and refusal responses carry the header
  (FR-001, FR-002).
- The rule itself raises no error and writes no log or audit event; it changes
  a header only. A response whose headers were already sent cannot be changed —
  the rule must therefore apply before the response starts.
- Responses that Kestrel produces before a request reaches the application
  pipeline (a malformed request line, oversized headers) cannot be reached by
  a pipeline rule; by **OD-001 (a)** they are outside the rule — they carry
  no data and nothing user-dependent.

## 9. Non-Functional Requirements

- No new NuGet package (`AGENTS.md` Technology Stack); ASP.NET Core middleware
  covers it.
- Negligible overhead: one header per response.
- Applies identically in every environment (Development included), so tests see
  the production behaviour.

## 10. Out of Scope

- Sign-out, session limits, cookie settings (SC-2, `trebovaniya.md` v68).
- Any other security header (CSP, `X-Frame-Options`, `Referrer-Policy` …) — no
  requirement names them.
- Changing the caching of static files.
- Page content or behaviour.
- New endpoints; persistence (DB_DESIGN is expected `NOT_APPLICABLE`).

## 11. Open Decisions

Recorded in `docs/decisions/US-040-open-decisions.md`.

- **OD-001 Responses produced before the application pipeline** — resolved
  (a) by the Owner on 2026-10-05. Responses that Kestrel writes itself before a
  request reaches the application pipeline (e.g. `400` for a malformed request,
  `431` for oversized headers) are outside the rule: they carry no data. "Every
  response" in FR-001/FR-002 means every response that passes through the
  application pipeline.

## 12. Traceability

| Acceptance Criterion | Requirements | Validation | Source |
|---|---|---|---|
| AC-001 | FR-001, FR-003, FR-006 | VR-001, VR-002, VR-004 | `trebovaniya.md` v85 §8; SC-14 |
| AC-002 | FR-002, FR-003, FR-006 | VR-001, VR-002, VR-004 | `trebovaniya.md` v85 §8; SC-14 |
| AC-003 | FR-004 | VR-003 | `trebovaniya.md` v68, v85 §8; SC-14 |
| AC-004 | FR-003 | VR-004 | SC-14; TC-5 enumeration (US-008) |
| AC-005 | FR-005 | existing suite | Story Scope |
