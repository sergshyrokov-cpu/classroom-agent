---
id: US-041
epic: EPIC-6
title: An unknown file-like address answers 404 to anyone
slug: unknown-file-path-404
priority: LOW
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent from US-040 finding TEST F-2; to be reviewed and
# edited by the Owner before activation. Aligned with trebovaniya.md v85.
---

# User Story

As the **Owner** of the service

I want every address that nothing serves to answer `404` to anyone, signed in
or not, including addresses that look like a file name (`/robots.txt`,
`/css/missing.css`, `/favicon.ico`)

So that both hosts behave exactly as `trebovaniya.md` §8 (v66) and SC-4 state,
and an anonymous visitor is never sent to sign-in for a page that does not
exist.

---

# Business Value

`trebovaniya.md` v66 fixed that "an unknown address answers `404` to an
anonymous visitor too" (SC-4: "the fallback catch-all that answers `404` for any
unmatched request"). US-040 found (test-generation report, finding F-2) that on
the installation's public port an anonymous request to a missing **file-like**
path — one whose last segment contains a dot — answers `302 /sign-in` instead.

The likely cause: the catch-all registered by `MapFallback` matches only paths
that do not look like a file (route constraint `nonfile`), so such a request
has no endpoint, falls under the fallback authorization policy and is
challenged. Nothing is disclosed — the redirect reveals no data and no page —
so this is a conformance gap, not a leak. It is noted so the rule and the code
agree and a later security review does not raise it again.

---

# Scope

**In scope:**

- the installation (public port) and the Control Plane: an unmatched request
  whose path looks like a file answers `404` with the host's error page, to an
  anonymous caller and to a signed-in one;
- static files that exist keep being served as today (SC-4 "Static files",
  `trebovaniya.md` v68);
- tests per host for an anonymous and a signed-in caller.

**Out of scope:**

- the private port of the installation (it already answers `404` to everything
  it does not serve, DC-6) — to be confirmed in the Specification;
- the OAuth callback `/signin-google`, which has no routed endpoint and is
  served by the authentication handler (US-008);
- any change to the error page, sign-in or `Cache-Control` (US-040).

---

# Acceptance Criteria

## AC-001 Installation: a missing file-like path answers 404 to anyone

**Given** the installation's public port

**When** an anonymous caller, an Admin or a Dean requests a path that nothing
serves and whose last segment contains a dot (e.g. `/robots.txt`,
`/css/no-such-file.css`)

**Then** the response is `404` with the error page — never a redirect to
sign-in.

## AC-002 Control Plane: the same

**Given** the Control Plane — with the Owner account existing, and also before
it exists

**When** an anonymous caller or the Owner requests such a path

**Then** the response is `404` with the error page. Before setup it is `404`
too, not the setup gate's `302 /setup`: the gate redirects only requests that
match an endpoint and leaves the rest to the `404` handling (US-001 FR-001,
`SetupGateMiddleware`), and an unknown address is one of them (OD-001).

## AC-003 Existing static files are unchanged

**Given** either host

**When** a file that exists in `wwwroot` is requested anonymously

**Then** it is served with `200` as today.

## AC-004 Nothing else changes

**Given** the existing test suite

**When** it runs after the change

**Then** it stays green: the setup gate of the Control Plane, the sign-in
redirect for real protected pages and the OAuth callback behave as before.

---

# Open Decisions

## OD-001 The Control Plane before setup

**Question.** Before the Owner account exists, should the Control Plane answer
an unknown file-like path with `404` or with the setup gate's `302 /setup`?

**Resolution (the Owner, 2026-10-05):** `404`. One rule — an unknown address
answers `404` to anyone (SC-4 v66) — with no first-run exception; the setup
gate already leaves unmatched requests to the `404` handling, and only a person
setting the Control Plane up needs `/setup`, which they reach from `/`.

---

# Notes

- Origin: US-040 test-generation report §7, finding F-2.
- Expected stages: API_DESIGN records the changed fallback behaviour (no new
  endpoint); DB_DESIGN is NOT_APPLICABLE.
- No new NuGet package is expected.
