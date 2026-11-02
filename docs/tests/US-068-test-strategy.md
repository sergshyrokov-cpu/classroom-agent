---
artifact_type: test_strategy
story: US-068
version: 1
status: DRAFT
created_at: 2026-11-01T13:47:46Z
updated_at: 2026-11-01T13:47:46Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/decisions/US-068-open-decisions.md
    version: 2
  - path: docs/designs/api/US-068-api-design.md
    version: 1
  - path: docs/designs/database/US-068-db-design.md
    version: 1
supersedes: null
---

# US-068 Test Strategy — Both hosts share one restrained look built on Bootstrap

## 1. Scope

Presentation only (spec §1): the vendored library and its licence/version note (FR-001), the two layouts' links
(FR-002), one light theme (FR-003), the `data:` image overrides (FR-007) and the guards that keep the headers, the
inline-code rule and every visible text unchanged (FR-009). API_DESIGN and DB_DESIGN are NOT_APPLICABLE: no endpoint,
schema, policy, audit event or translation changes, so there are no allowed/forbidden-role, persistence, migration,
audit or translation-key scenarios to add (TC-5 role pairs are unchanged because no endpoint is added).

The look itself — container width, component styling, danger outline per OD-003 a), restrained marks (FR-004…FR-006,
FR-008) — is AC-007, a manual check by the Owner. It is not automated: the spec makes it manual (FR-011.2) and an
assertion on class names would test markup choices, not the look.

## 2. Test levels

| Level | Where | What |
|---|---|---|
| Structural (file-system) | `Architecture/BootstrapLayoutRuleTests`, `Architecture/VendoredBootstrapTests`, `Architecture/EmbeddedImageOverrideTests` | layouts, `_ViewStart`, views, `site.css`, the vendored files and their hash; the `data:`-image cross-check between `bootstrap.min.css`, the views and `site.css` |
| Integration (HTTP, Testcontainers PostgreSQL) | `Web/Pages/InstallationStylesheetLinkTests`, `ControlPlane/Controllers/ControlPlaneStylesheetLinkTests` | the rendered `<head>` of anonymous, error and signed-in pages; content types of the new static files and override images |
| Existing guards, unchanged | `InlineCodeRuleTests`, both `SecurityHeadersResponseTests`, both `NoStoreResponseTests`, `UnknownFileLikePathTests`, `SecurityHeadersRuleTests`, both error-page test classes, the full suite | AC-003, AC-005, AC-006 |
| Manual | implementation report §3.1 | AC-007 |

## 3. Scenarios

### Positive
- Each layout's `<head>` links exactly `/lib/bootstrap/css/bootstrap.min.css` then `/css/site.css` (AC-001 a).
- Rendered pages carry the same two links in that order: installation Dean sign-in, `/` as Admin and as Dean, the
  error page for 400/403/404/500; Control Plane `/sign-in`, `/setup` (before the Owner exists), `/` and
  `/installations` as Owner, the error page for 400/403/404/500 (AC-001 b).
- Both stylesheets `200 text/css`; `LICENSE.txt`, `VERSION.txt` `200 text/plain` (AC-001 b, AC-002, FR-001.5).
- `wwwroot/lib/bootstrap/` holds exactly `LICENSE.txt`, `VERSION.txt`, `css/bootstrap.min.css`; the licence is the MIT
  licence naming Bootstrap; the note names a `5.3.x` version, a `https://github.com/twbs/bootstrap/releases/…` URL
  containing `v<version>`, an ISO date, a `sha384-` hash and the word "unmodified"; SHA-384 (base64) of the stylesheet
  equals the recorded hash; the stylesheet header `Bootstrap v5.3.x` equals the recorded version; both hosts'
  three files are byte-identical (AC-002).
- Every (selector, property) of `bootstrap.min.css` whose value has `data:`, not scoped to `[data-bs-theme=dark]`,
  whose classes all occur in the host's views, is declared in that host's `site.css` with the same normalised
  selector and property and a root-relative `url("/…")`; each referenced image is an `.svg` under `wwwroot`, served
  `200 image/svg+xml` (AC-004).

### Negative
- No `*.js` / `*.map` under `wwwroot/lib/bootstrap/`; no view references a `bootstrap…js` (AC-002, FR-001.4).
- No view other than `_ViewStart.cshtml` assigns `Layout` (AC-001 a, FR-002.2).
- No view sets `data-bs-theme`; no `site.css` has `prefers-color-scheme` or a `color-scheme` naming `dark` (AC-008).
- Override SVGs hold no `<script`, no `data:`, no `<image`, no `href`/`xlink:href` other than a `#fragment` (AC-004).
- No view uses `is-invalid`, `is-valid`, `was-validated`, `btn-close`, `alert-dismissible`, `form-switch`, `navbar`,
  `dropdown`, `modal`, `accordion`, `tooltip`, `popover`, `carousel`, spinners, `data-bs-toggle`/`data-bs-dismiss`
  (FR-005.4, FR-005.5 — guard for AC-004/AC-007).
- `site.css` has no `content:` producing letters, no `text-transform` other than `none`, no `animation*`, no `blink`
  (FR-009.1, FR-006.3 — guard for AC-006).

### Boundary
- AC-004 "at least" list for the installation: `.form-select`, the checked checkbox, the indeterminate checkbox and
  the checked radio selectors of `.form-check-input` — makes the cross-check non-vacuous once the views use
  form-select/form-check (FR-005.2). The Control Plane has no select/checkbox/radio (spec §1.1), so its list may be
  empty; its check then guards against a future component without an override.
- Dark-theme-scoped selectors are excluded (FR-007.4).

### Validation
No user input changes (spec §6). The shipped-file validation is the hash and SVG checks above.

### Security
- CSP and the other headers unchanged, every new static file `200` with the headers and without `no-store`: proved
  by the existing every-static-file tests, which enumerate all of `wwwroot` and therefore pick up every new file
  without change (AC-003, spec §1.1).
- No inline code: `InlineCodeRuleTests` unchanged (AC-005).
- Nothing from another site: the layout link test allows exactly two same-host stylesheets; the override check
  requires root-relative same-host URLs (`//host` and relative URLs are rejected by the reader — reader self-test).
- Supply chain: the integrity hash pinned by `TheStylesheet_MatchesTheRecordedIntegrityHash`.

### Persistence
None (DB_DESIGN NOT_APPLICABLE).

## 4. Fixtures

- `Styling` (TestInfrastructure): paths, a small stylesheet reader (nesting at-rules, selector lists, strings,
  comments; selector normalisation removes quotes and combinator spaces), view tokens, `<head>` stylesheet links.
  Its behaviour is pinned by two reader self-tests in `BootstrapLayoutRuleTests`.
- The existing `JournalHostExtensions.StartAsync` (Admin / Dean / Anonymous), `InstallationTestHost`,
  `ControlPlaneTestHost` with `CreateOwnerAsync`; PostgreSQL via Testcontainers (TC-2). No Google port is reached.

## 5. Excluded scenarios

- Requesting each of the ~38 page routes with seeded data (spec I-1): replaced by the structural proof (one layout, no
  view overrides it) plus the HTTP sample.
- Automated assertions on component classes, container width, danger-outline buttons (OD-003 a), mark colours and
  phone width (FR-004…FR-006, FR-008): AC-007 manual, recorded in implementation report §3.1.
- `.resx` unchanged (AC-006): stated in the implementation report from the diff; the full suite's text assertions
  are the automated part.

## 6. Known limitations and notes for IMPLEMENTATION

- **Line endings.** The repository has `core.autocrlf=true` and `.gitattributes` does not cover the vendored files.
  A fresh checkout on Windows may rewrite `bootstrap.min.css` with CRLF and break the hash test. The implementor
  should mark `src/*/wwwroot/lib/**` as `-text` in `.gitattributes` (a supporting change the Story cannot work
  without) — recorded as INFO-1.
- The AC-004 selector match is by normalised selector text: `site.css` must repeat Bootstrap's selector (quotes and
  spaces around `> + ~` may differ). A selector with more specificity but different text does not count.
- View tokens are a wide superset (every name-like token outside comments), so a word used elsewhere in a view could
  demand an override that is not strictly needed — a stricter, never a weaker check.
- Test helpers that locate elements by exact markup (`UiLanguageTestData` `<nav class="language-switcher"`,
  `AccessCheckHostExtensions`) may need adaptation per FR-009.4 once classes are added; that is IMPLEMENTATION's.

## 7. Open Decisions affecting testing

None. OD-001…OD-003 resolved a); OD-003 affects only the manual AC-007.
