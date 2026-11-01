---
id: US-068
epic: EPIC-6
title: Both hosts share one restrained look built on Bootstrap
slug: restrained-look-with-bootstrap
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-01 from trebovaniya.md v105 (§8
# «Оформление»; derived NFR-076, SC-15 "Styling does not loosen the headers").
# The Owner's request of 2026-10-23 (before the first showing to real users) and
# the Owner's answers of 2026-11-01 (both hosts, v105 first, no school name or logo, light
# theme only, tests + manual look check). Before the pilot / first demo.
---

# User Story

As an **Admin or Dean** (and the Owner in the Control Plane)

I want the program's pages to look tidy and consistent, on a computer and on a phone

So that a first-time user sees a finished, calm tool rather than bare HTML, and
reads tables, forms and notices without effort.

---

# Business Value

Today both hosts render plain HTML with a 78-line `site.css` each; nothing has
been styled since the first Stories. Before the program is shown to deans and
zavuchs, the pages need a presentable look. v105 fixes how: one CSS library
(Bootstrap 5.3) shipped with the application, restrained, light theme, no text
change, and the browser security headers (v95) untouched.

---

# Scope

**In scope:**

- both hosts — installation (`ClassroomAgent.Web`, 31 views) and Control Plane
  (`ClassroomAgent.ControlPlane`, 18 views);
- Bootstrap 5.3 **CSS only**, vendored into each host's `wwwroot` (for example
  `wwwroot/lib/bootstrap/`) with its MIT `LICENSE` file and a short note of the
  exact version and where it came from (OD-001);
- linking it from each host's `_Layout.cshtml` before `site.css`, so the existing
  `site.css` refines it; reworking `site.css` and adding Bootstrap classes to the
  views — tables, forms, buttons, notices, the header, the language switcher, the
  data-health banner, error and field messages, the page-search hint;
- replacing the images Bootstrap embeds as `data:` URIs for the components the
  views actually use (select arrow, check box / radio mark, any other found) with
  small SVG files in the same host's static files, so the CSP needs no change
  (SC-15, v105);
- phone width: pages read without horizontal page scrolling; wide tables keep
  their own horizontal scroll area (`table-scroll`, NFR-070);
- restrained look (the Owner's standing preference): data-state marks and the
  banner are highlighted by colour only — no sound, pop-up, blinking or emphatic
  styling;
- the automated proofs in the Acceptance Criteria and the manual look check
  (AC-007), recorded in the implementation report.

**Out of scope:**

- a navigation menu, or any new link or page — the menu arrives with «Базова»
  (v89 agenda item 3); Bootstrap scripts are therefore not shipped;
- dark theme, colour settings, the school's name or logo;
- any change of a visible text, a translation key, a route, a policy, a form field,
  an export file or a business rule;
- any change of the security headers or the CSP string of either host;
- loading anything from a CDN or another site; a new NuGet package or .NET tool.

---

# Acceptance Criteria

## AC-001 Every page of both hosts links the library and the site stylesheet

**Given** each host, and every page it serves as HTML — anonymous pages (sign-in,
first-run setup, the error page for 400/403/404/500) and pages after sign-in for
each role that may open them

**When** the page is requested

**Then** its HTML links the host's own Bootstrap stylesheet and then its
`site.css`, both by a same-host path, and both files are served with status 200
and a CSS content type.

## AC-002 The library ships with its licence and its source is recorded

**Given** each host's static files

**Then** the Bootstrap stylesheet, its `LICENSE` file and the version note are
present; the note names the exact Bootstrap version and how the file was obtained
and checked (OD-001); no Bootstrap script file is shipped or linked.

## AC-003 The security headers are unchanged

**Given** each host

**When** any page and the Bootstrap stylesheet are requested

**Then** each response carries the same security headers as before this Story —
the CSP string unchanged, with no `data:` in `img-src` and no `'unsafe-inline'`
anywhere — and the existing header tests (US-050) pass unchanged.

## AC-004 No embedded image is left for the components the pages use

**Given** the components the views use whose Bootstrap rule draws a `data:` image
(at least the select list and the check box / radio)

**Then** `site.css` overrides each such image with a same-host SVG file that is
served with status 200; a test lists the overridden selectors and fails when a
view starts using another component whose Bootstrap rule draws a `data:` image
without an override.

## AC-005 No inline style or script appears

**Given** every view of both hosts after the change

**Then** the existing inline-code guard (`InlineCodeRuleTests`) passes: no `style`
attribute, no `<style>` or `<script>` block with a body, no event-handler
attribute.

## AC-006 No visible text changes

**Given** the full existing test suite, whose page tests assert the texts users see

**Then** it passes without any change to a text assertion, a translation file entry
or a translation key; the implementation report states that no `.resx` file was
changed.

## AC-007 The key pages look right on a computer and on a phone (manual)

**Given** the application running locally with synthetic data (no real school data)

**When** a person opens, at desktop width and at phone width (about 375 px):
installation — Dean sign-in, the course list, the journal, Meet reports, the
Dean-accounts settings page, the error page; Control Plane — sign-in and the
installation list

**Then** each page is tidy and readable, has no horizontal page scroll (a wide
table scrolls inside its own area), the data-state marks and banner are visible
but restrained, and the browser console shows no Content-Security-Policy
violation. The person records the result (pages, widths, browser, date) in the
implementation report; the Owner confirms it at `HUMAN_PR_APPROVAL`.

---

# Open Decisions

Resolved by the Owner on 2026-11-01 before activation («всё по умолчанию» covers
the recommended options below; confirm or change at `HUMAN_SPEC_APPROVAL`).

## OD-001 How the Bootstrap files enter the repository

- **a) (recommended)** — the implementor downloads the official Bootstrap 5.3
  release archive (the latest 5.3 patch at least two weeks old) from the project's
  GitHub releases, checks the stylesheet against the integrity hash Bootstrap
  publishes for that version, and commits `bootstrap.min.css` and `LICENSE`
  unchanged; the version note records version, URL, date and hash. No package
  manager, no build step, nothing fetched at run time.
- b) — a .NET tool (LibMan) restores the files at build time: adds a tool and a
  network fetch to every build, against the "no new tool" rule.

**Resolved 2026-11-01 by the Owner: a)** («всё по умолчанию»).

## OD-002 Colour palette

- **a) (recommended)** — Bootstrap's default light palette: blue for links and the
  main button, grey tables and borders, the existing warning colours for marks;
  familiar and calm.
- b) — a monochrome grey palette with colour only for marks: even quieter, but
  links and buttons are harder to spot for first-time users.

**Resolved 2026-11-01 by the Owner: a)** («всё по умолчанию»).

---

# Notes

- **Code today:** layouts `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml`,
  `src/ClassroomAgent.ControlPlane/Views/Shared/_Layout.cshtml`; stylesheets
  `wwwroot/css/site.css` in each host; CSP in each host's
  `Security/SecurityHeadersMiddleware.cs` (`img-src 'self'`, `style-src 'self'`) —
  not to be edited. Most-used classes: `table-scroll`, `form-error`,
  `form-message`, `notice`, `note`, `field-error`, `warning`.
- **Existing guards to reuse:** `tests/.../Architecture/InlineCodeRuleTests.cs`,
  `tests/.../Architecture/SecurityHeadersRuleTests.cs`,
  `tests/.../ControlPlane/Security/SecurityHeadersResponseTests.cs`; static files
  are served anonymously (SC-4) and cached normally (SC-14).
- **Why `data:` matters:** Bootstrap draws the select arrow, check marks, switches,
  validation icons and the close button as `data:` SVGs inside its CSS; under
  `img-src 'self'` the browser blocks them silently (the control loses its arrow
  or mark). v105 forbids loosening the CSP, so they are overridden from files.
- **Map file:** `bootstrap.min.css` ends with a `sourceMappingURL` comment; the
  specification decides whether the `.map` file ships or the request for it
  simply returns 404 (a technical choice, not the Owner's).
- **Manual check (AC-007)** — synthetic data only (the AGENTS.md Hard Stop on
  `classroom_cache.db` and real exports applies); the US-047/049/050 pattern of a
  human check recorded in the implementation report §3.1.
- Expected stages: SPECIFICATION, API_DESIGN and DB_DESIGN NOT_APPLICABLE (no
  endpoint, no schema).
- No new NuGet package, no new tool; requirements version v105.
