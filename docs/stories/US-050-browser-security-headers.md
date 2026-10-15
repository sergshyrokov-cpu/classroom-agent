---
id: US-050
epic: EPIC-6
title: Every response carries the browser security headers
slug: browser-security-headers
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from the pre-pilot security audit, finding F-3
# (docs/reviews/audits/2026-10-pre-pilot-security-audit.md); to be reviewed and
# edited by the Owner before activation. Aligned with trebovaniya.md v95.
# Option 1 (the full set) chosen by the Owner on 2026-10-15; the rule went into
# trebovaniya.md v95 (section 8) first.
---

# User Story

As the **Owner** of the service

I want every response of both hosts to tell the browser not to frame the page,
not to guess file types, to load code only from the same host and not to pass
the page address to other sites

So that a mistake in a future page, or a hostile site, cannot turn a signed-in
Admin's, Dean's or Owner's browser against the school's data.

---

# Business Value

The pre-pilot security audit (finding F-3) found that neither host sends
`X-Content-Type-Options`, a frame policy on every page, a
`Content-Security-Policy` or a `Referrer-Policy`. `X-Frame-Options` appears only
on responses that generate an antiforgery token. No exploitable hole was found —
no raw HTML output, every state change is a POST with a token — so these
headers are a second layer: they limit what a future mistake can do, and they
stop clickjacking of pages without a form.

`trebovaniya.md` §8 (v95) and SC-15 now require them. Every script is already a
separate file in the static files directory and no page has inline code, so the
strict policy fits the interface as it is.

---

# Scope

**In scope:**

- one host-wide rule per host (installation: public and private port; Control
  Plane) that adds the four headers of §8 v95 / SC-15 to every response —
  pages, the error page, redirects, downloaded files, `/api/v1` responses and
  static files;
- the installation's CSP permits form submission to the Google sign-in address
  the "Sign in with Google" form leads to, and to nothing else outside the host;
- a test per host that enumerates its endpoints and asserts the headers on every
  one, and a test that no view contains inline script, inline event handlers or
  `style` attributes;
- confirmation that every existing page — the Google sign-in round trip, the
  report and Meet exports, copying the instruction and identifiers, the report
  template form — still works under the policy.

**Out of scope:**

- `Cache-Control: no-store` (SC-14, US-040) — unchanged;
- HSTS and HTTPS redirection (SC-2) — unchanged;
- any change to what pages show.

---

# Acceptance Criteria

## AC-001 Installation: every response carries the headers

**Given** the installation, public and private port

**When** any endpoint is requested — a signed-in page, the sign-in page, the
error page, a redirect, an export, an `/api/v1` response, a static file, an
unknown address

**Then** the response carries `X-Frame-Options: DENY`,
`X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and the
`Content-Security-Policy` of SC-15, with `frame-ancestors 'none'`.

## AC-002 Control Plane: the same

**Given** the Control Plane — with the Owner account existing, and before it
exists

**When** any endpoint is requested

**Then** the response carries the same four headers; its CSP allows no form
target outside the host.

## AC-003 The CSP is strict

**Given** the CSP of either host

**When** it is read

**Then** it allows scripts, styles, images and fonts from the same host only,
contains neither `'unsafe-inline'` nor `'unsafe-eval'`, sets
`object-src 'none'` and `base-uri 'self'`, and names no other origin except, in
the installation's `form-action`, the Google sign-in address.

## AC-004 The Google sign-in still works

**Given** the installation's sign-in page

**When** "Sign in with Google" is pressed

**Then** the browser is allowed to follow the form to Google, and the return
through the callback signs the Admin in as today.

## AC-005 No inline code in the interface

**Given** every Razor view of both hosts

**When** the test scans them

**Then** none contains a `<script>` block with a body, an `on…=` event-handler
attribute or a `style` attribute.

## AC-006 Nothing else changes

**Given** the existing test suite

**When** it runs after the change

**Then** it stays green.

---

# Open Decisions

None. The rule and its values are fixed by `trebovaniya.md` §8 v95 and SC-15.

---

# Notes

- Origin: pre-pilot security audit 2026-10-15, finding F-3.
- Pattern to follow: US-040's `NoStoreMiddleware` (one middleware per host,
  first in the pipeline, with an endpoint-enumerating test).
- Which Google address the form leads to (the authorization endpoint the
  handler redirects to) is fixed in the Specification from the Google
  authentication handler's configuration — not a new requirement.
- AC-004 needs a manual check in a real browser: a header-only test cannot prove
  the browser follows the form. Record it in the implementation report, as
  US-047 did for its manual check.
- Expected stages: API_DESIGN NOT_APPLICABLE (headers only, no endpoint);
  DB_DESIGN NOT_APPLICABLE.
- No new NuGet package.
