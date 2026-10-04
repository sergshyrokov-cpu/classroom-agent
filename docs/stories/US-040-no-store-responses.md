---
id: US-040
epic: EPIC-6
title: Responses are not cached (Cache-Control no-store)
slug: no-store-responses
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v85.
---

# User Story

As the **Owner** of the service, responsible for students' personal data in
every school

I want no page, file or API answer of either host to be kept in a browser's or a
proxy's cache

So that on a shared computer — a staff room, a library — nobody can open a
journal or a report with students' grades from the browser's memory after the
Dean or Admin has signed out.

---

# Business Value

`trebovaniya.md` §8 (v85) requires `Cache-Control: no-store` on every response
of both hosts except the static files of the interface (SC-14). Without it the
Back button after sign-out can show a page with personal data, possibly of
minors, without asking the server. The journal (US-025) and the report
(US-027) already show such data, and the export Stories (US-028 … US-030) will
produce files with it; the rule should be in place before them.

---

# Scope

**In scope:**

- one host-wide rule in the **installation** (public and private port) and in
  the **Control Plane** that sets `Cache-Control: no-store` on every response:
  signed-in pages, anonymous pages, the error page, redirects, downloaded files,
  `/api/v1` responses — including error responses and responses of requests
  refused by authentication, authorization or antiforgery;
- static files of the interface (`wwwroot`: CSS, JS, images) are excluded and
  keep their normal caching;
- a test per host that enumerates every endpoint and asserts the header, and
  asserts that a static file does not carry it.

**Out of scope:**

- any change to sign-out, session limits or cookies (SC-2, `trebovaniya.md` v68);
- any other security header (CSP, `X-Frame-Options` etc.) — no requirement
  names them;
- any change to page content or behaviour.

---

# Acceptance Criteria

## AC-001 Every response of the installation is not cached

**Given** the installation, public and private port

**When** any endpoint answers — signed in or anonymous, success, redirect or
error (`400`, `403`, `404`, `409`, `500`)

**Then** the response carries `Cache-Control: no-store`.

## AC-002 Every response of the Control Plane is not cached

**Given** the Control Plane

**When** any endpoint answers — signed in or anonymous, success, redirect or
error

**Then** the response carries `Cache-Control: no-store`.

## AC-003 Static files keep normal caching

**Given** either host

**When** a static file of the interface (CSS, JS, image) is requested

**Then** the response does not carry `Cache-Control: no-store`.

## AC-004 One rule per host, proven for every endpoint

**Given** each host

**When** its test suite runs

**Then**:

- a test enumerates every endpoint of the host (as the US-008 endpoint
  enumeration test does) and asserts the header on each, so a page added later
  without the header fails the build;
- the header is set by one host-wide rule, not by attributes on individual
  controllers or actions.

## AC-005 Nothing else changes

**Given** the existing test suite

**When** it runs after the change

**Then** it stays green: no page, status code, redirect, cookie or session
behaviour changes.

---

# Open Decisions

None. The rule, its scope and its single exception are fixed by
`trebovaniya.md` §8 (v85) and SC-14.

---

# Notes

- Expected stages: API_DESIGN records the host-wide header rule (no new
  endpoint); DB_DESIGN is NOT_APPLICABLE (no persistence change).
- No new NuGet package is needed: ASP.NET Core middleware covers it.
