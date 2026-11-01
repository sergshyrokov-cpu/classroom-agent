---
artifact_type: specification
story: US-068
version: 2
status: APPROVED
created_at: 2026-11-01T12:51:01Z
updated_at: 2026-11-01T13:06:03Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: trebovaniya.md
    version: 105
  - path: docs/decisions/US-068-open-decisions.md
    version: 1
supersedes: docs/specifications/US-068-spec.md@v1
---

# US-068 Specification — Both hosts share one restrained look built on Bootstrap

Delegation: one read-only lookup (quick-look) collected the code facts in §1.1 — both layouts,
the view inventory, the inputs the views use, the `wwwroot` contents, the CSP strings, the
static-file pipeline and the existing header, no-store, inline-code and error-page tests. The spec
writer read `trebovaniya.md` §8 «Оформление» (lines 2190–2203), §8 static files (lines 2223–2226),
§5 «Мобильная версия» (line 1994), §7, NFR-070, NFR-076, SC-4, SC-14 and SC-15 directly, and both
`site.css` files and the class attributes used in the views.

Revision v2 (loop-back from SPEC_REVIEW v1): M-1 — the licence and version note get a `.txt`
extension so the host serves them and the existing every-static-file tests keep passing unchanged
(FR-001.1, FR-001.5, I-8); m-1 — FR-006.1 names exactly which elements are coloured. Facts checked
for v2: `tests/.../TestInfrastructure/NoStore.cs` `StaticFilesOf` and its callers; the .NET 10
default content-type map (`.txt` → `text/plain`, `.md` → `text/markdown`, no entry for a name
without extension); the Journal view's data-mark `<span>`s.

## 1. Overview

`trebovaniya.md` §8 (v105, lines 2190–2203): «оба хоста — инсталляция и Control Plane — оформлены
одинаково и сдержанно на основе CSS-библиотеки Bootstrap (версия 5.3). Её файлы поставляются вместе
с приложением в каталоге статических файлов, рядом — файл её лицензии (MIT); с чужих сайтов ничего
не загружается … Защитные заголовки ради оформления не ослабляются: картинки, которые Bootstrap
встраивает в стили (`data:` — стрелка списка, отметка флажка), заменяются файлами из того же
каталога. Подключаются только стили … Тема одна — светлая. Состав шапки не меняется … Пометки
состояния данных выделяются только цветом … Страницы читаются на телефоне … Оформление не добавляет
и не убирает ни одного видимого текста.» Derived: NFR-076, SC-15 "Styling does not loosen the
headers".

This Story changes presentation only: static files, the two layouts, `site.css` and the `class`
attributes of the views. No endpoint, schema, translation, policy or business rule changes; API and
database design are expected to be NOT_APPLICABLE.

### 1.1 Code facts this Specification must not contradict

- Each host has one layout, `Views/Shared/_Layout.cshtml`, set by its `_ViewStart.cshtml`; no view
  sets `Layout` itself. Both layouts link only `/css/site.css`. The installation header holds the
  language switcher and the data-health banner (US-062); the Control Plane header holds the language
  switcher.
- Views: installation 25 page views + 3 partials; Control Plane 13 page views + 2 partials. No
  Razor Pages.
- Inputs used (installation only; the Control Plane has none of these): `<select>` in 7 views,
  `type="checkbox"` in 2, `type="radio"` in 5, `type="date"` in 3; no `type="file"`, `<details>` or
  `<dialog>`.
- `wwwroot`: installation `css/site.css` (78 lines) + 4 scripts in `js/`; Control Plane
  `css/site.css` (76 lines) + 1 script. No vendored library. The installation `site.css` declares
  `:root { color-scheme: light dark; }`.
- CSP (both hosts, `Security/SecurityHeadersMiddleware.cs`): `default-src 'self'; script-src 'self';
  style-src 'self'; img-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self';
  form-action 'self' [+ Google sign-in origin in the installation]; frame-ancestors 'none'`.
  The security headers are applied to static files too; `no-store` is not (SC-14 exception,
  `NoStoreMiddleware.MarkStaticFile`). No custom content-type mapping exists, so the framework
  defaults apply (`.css` → `text/css`, `.svg` → `image/svg+xml`, `.txt` → `text/plain`,
  `.md` → `text/markdown`; a file whose extension is unknown to the default map, or that has no
  extension, is answered `404`).
- `NoStore.StaticFilesOf(host)` enumerates **every** file under a host's `wwwroot`; the tests
  `EveryStaticFile_CarriesTheSecurityHeaders`, `AStaticFile_DoesNotCarryNoStore` (via
  `NoStoreResponseTests`) and `UnknownFileLikePathTests` request each one and require `200`. Every
  file this Story adds under `wwwroot` is therefore requested by those tests.
- Existing guards: `InlineCodeRuleTests`, `SecurityHeadersRuleTests`, both hosts'
  `SecurityHeadersResponseTests` (incl. `EveryStaticFile_CarriesTheSecurityHeaders`,
  `TheSentPolicy_IsStrict…`), both `NoStoreResponseTests` (incl. `AStaticFile_DoesNotCarryNoStore`),
  `InstallationErrorPageTests`, Control Plane `ErrorPageTests`. `HostEndpoint.All` enumerates a
  host's routes; `InlineCode.ViewsOf(host)` enumerates its views.
- Classes in use today: `table-scroll`, `form-error`, `form-message`, `notice`, `note`,
  `field-error`, `warning`, `page-header`, `page`, `language-switcher`, `data-health-banner`,
  `mark-above-maximum`, `page-search-hint` and several element markers (`journal-title`,
  `dean-email`, `access-check-step`, …). Some test helpers locate elements by these classes or by
  markup (`UiLanguageTestData` matches `<nav class="language-switcher"`,
  `AccessCheckHostExtensions` matches `class="…access-check-step…"`).

## 2. Business Goal

Before the program is shown to deans and zavuchs, every page of both hosts looks finished, calm and
consistent, on a computer and on a phone, without weakening the browser security headers (v95) and
without changing a word users read.

## 3. Business Flow

No user flow changes. Every page a user already opens renders with the shared look: the same
header, the same main column, styled tables, forms, buttons, notices and marks. A wide table
scrolls inside its own area on a phone. The Owner checks the key pages by eye before approving the
Story (AC-007).

## 4. Functional Requirements

### FR-001 The library is vendored in each host

- FR-001.1 Each host's `wwwroot/lib/bootstrap/` holds exactly:
  `css/bootstrap.min.css` (Bootstrap 5.3, the CSS-only distribution file), `LICENSE.txt`
  (Bootstrap's MIT `LICENSE` file, content byte-for-byte unchanged, only the file name gains `.txt`)
  and `VERSION.txt` (the version note, plain text). The two hosts carry byte-identical copies of
  all three files.
- FR-001.2 The files are obtained per OD-001 a): from the official Bootstrap 5.3 release archive on
  the project's GitHub releases, the latest 5.3 patch released at least two weeks before the
  download; `bootstrap.min.css` is checked against the integrity (SRI, `sha384-…`) hash Bootstrap
  publishes for that version and committed **unchanged**; the licence text is committed unchanged
  as `LICENSE.txt`.
- FR-001.3 `VERSION.txt` states: the exact version (e.g. `5.3.x`), the archive URL, the download
  date, the published integrity hash of `bootstrap.min.css`, and that the file is unmodified.
- FR-001.4 No Bootstrap script file (`*.js`), no source map (`*.map`), no font file and no other
  Bootstrap file is shipped. `bootstrap.min.css` keeps its trailing `sourceMappingURL` comment
  (the file stays unmodified, FR-001.2); a browser that asks for the map receives the host's
  existing `404` answer for a missing static file. Nothing is fetched at build or run time; no
  package manager, NuGet package or .NET tool is added.
- FR-001.5 All three files are ordinary static files: served anonymously with `200`, the security
  headers and normal caching like every other file in `wwwroot` (SC-4, SC-14) — `.css` as
  `text/css`, the two `.txt` files as `text/plain`. They hold no data and nothing user-specific
  (licence text, version, URL, date, hash). The existing every-static-file tests cover them
  without change.

### FR-002 Every page links the library, then the site stylesheet

- FR-002.1 Each `_Layout.cshtml` links, in its `<head>`, first
  `/lib/bootstrap/css/bootstrap.min.css`, then `/css/site.css` — both root-relative same-host paths,
  `rel="stylesheet"`, no `integrity`/`crossorigin` attribute needed (same host). No other
  stylesheet is linked; no Bootstrap script is linked.
- FR-002.2 Every view keeps rendering through its host's single layout: no view sets `Layout` to
  `null` or to another file, and `_ViewStart.cshtml` keeps pointing at `Shared/_Layout.cshtml`.
  This is what makes the link present on every page, including the anonymous ones (sign-in, the
  Control Plane first-run setup, the error page for 400/403/404/500).

### FR-003 One light theme

- FR-003.1 The look is Bootstrap's default light theme with its default palette (OD-002 a): blue for
  links and the main button, grey for tables, borders and secondary text.
- FR-003.2 The installation `site.css` drops `color-scheme: light dark`; neither `site.css` contains
  a `prefers-color-scheme` rule; no view or layout sets `data-bs-theme` (Bootstrap's light theme is
  its default when the attribute is absent). A user whose operating system uses a dark theme sees the
  light theme.

### FR-004 Page frame

- FR-004.1 The header keeps exactly its current content and order: the language switcher and, in
  the installation, the data-health banner. No school name, logo, menu, link or page is added.
- FR-004.2 The header and `<main>` use a Bootstrap responsive container, so content widens with the
  screen up to Bootstrap's container maximum (the journal and reports gain width compared with
  today's 40rem column); forms and short texts stay in a readable width (a narrow column within the
  container) so a sign-in form is not stretched across a wide screen.
- FR-004.3 The page title (`<h1>`) and its text are unchanged.

### FR-005 Components

Bootstrap classes are **added** to the existing elements. Every class an element carries today stays
on it (tests, scripts and the restrained marks rely on them); `site.css` may restyle those classes.

- FR-005.1 Tables: Bootstrap table styling (`table`, compact size, light borders). A table inside
  `table-scroll` keeps scrolling horizontally inside that element (FR-008).
- FR-005.2 Forms: labels, text/password/e-mail/number/date inputs and `<textarea>` use Bootstrap's
  form-control styling; `<select>` uses form-select; checkboxes and radios use form-check
  styling. Field names, ids, values, order, required attributes and button texts do not change.
- FR-005.3 Buttons: the main action of a form uses Bootstrap's primary button; other actions
  (language switcher, secondary buttons, links styled as buttons where they already are buttons)
  use a secondary outline button. The submit button of a confirmation page for an action that cannot
  be undone (Erasure confirmation, report-template deletion, revocation of an allowed Admin,
  installation status change in the Control Plane) uses the default palette's danger colour as an
  outline button — colour only, no icon. The language switcher's current language stays marked by
  `aria-current="true"` and is shown as the active (filled) button.
- FR-005.4 Messages: `notice` and `form-message` look like a quiet bordered box (Bootstrap's neutral
  "light"/"secondary" alert colours, no icon, no close button); `form-error` and `field-error` are
  text in the palette's danger colour; `note` and `page-search-hint` are smaller muted text.
  Bootstrap's validation-state classes (`is-invalid`, `is-valid`, `was-validated`) are **not** used:
  a field error stays the text element it is today.
- FR-005.5 Bootstrap components that need its scripts or draw an embedded image beyond FR-007 —
  close buttons, dismissible alerts, switches, accordions, navbars, dropdowns, modals, tooltips,
  carousels, spinners and animations — are not used.

### FR-006 Data-state marks stay restrained

- FR-006.1 Exactly these elements are colour-highlighted, by a background tint and/or a coloured
  left border in the palette's warning (amber) family:
  - installation: the data-health banner (`data-health-banner`, US-062) and a grade above the
    maximum (`mark-above-maximum`, US-060) — both coloured today; the tint may be retuned to the
    palette, the meaning stays;
  - Control Plane: the `warning` paragraphs (fewer than two allowed Admins, missing push address) —
    not a data-state mark but an Owner warning; today unstyled, it gets the same quiet amber
    treatment so it is told apart from an ordinary `note`.
- FR-006.2 The text labels of US-059…US-063 («нет в Classroom», «не виден», «вибув(-ла)» and the
  like, plain `<span>`s today) stay plain text in the body colour: no class is added to them and no
  colour is given to them. Their words carry the meaning, and colouring them would need markup
  changes beyond this Story's styling.
- FR-006.3 For every element above: no icon, sound, pop-up, blinking, animation, bold upper-case or
  dismiss control. The text stays visible on its own (colour is never the only carrier of meaning).

### FR-007 Embedded images are replaced by same-host files

- FR-007.1 For every Bootstrap rule that draws a `data:` image and applies to the components the
  views use, `site.css` (loaded after Bootstrap) overrides the image with a same-host SVG file. With
  the components of FR-005 this is at least: the select arrow (`.form-select`), the checked
  checkbox mark, the indeterminate checkbox mark and the checked radio dot (`.form-check-input`).
- FR-007.2 The override sets the same property (the CSS custom property Bootstrap 5.3 uses, e.g.
  `--bs-form-select-bg-img`, `--bs-form-check-bg-image`, or `background-image` where Bootstrap sets
  it directly) on the same selector, with a **root-relative** URL (e.g.
  `url("/img/bootstrap/select-arrow.svg")`) — a relative URL inside a custom property is resolved
  where the property is used, so root-relative avoids ambiguity.
- FR-007.3 The SVG files live in each host's `wwwroot/img/bootstrap/`, reproduce Bootstrap's own
  drawing (same path data and colours as its embedded image) and contain no script, no external
  reference and no embedded raster image.
- FR-007.4 Selectors Bootstrap scopes to its dark theme (`[data-bs-theme=dark] …`) need no override:
  that theme is never active (FR-003.2).

### FR-008 Phone width

- FR-008.1 At a viewport width of about 375 CSS px every page reads without horizontal scrolling of
  the page itself. A wide table scrolls horizontally inside its own `table-scroll` area (NFR-070,
  US-025); a long unbroken value (an e-mail, an identifier) wraps rather than widening the page.
- FR-008.2 The existing `viewport` meta tag of both layouts stays as it is.

### FR-009 What does not change

- FR-009.1 No visible text, translation file (`.resx`) entry or translation key changes; no text is
  produced by CSS (`content:` with words, text-transform that changes letters is not used).
- FR-009.2 No route, controller, authorization policy, form field, script behaviour, export file,
  audit event or business rule changes. The five existing script files are not modified unless a
  script selects an element by markup that this Story changes — then only its selector is adapted
  and the change is listed in the implementation report.
- FR-009.3 The security headers and the CSP string of both hosts do not change; no middleware, no
  `Program.cs` pipeline line and no content-type mapping changes.
- FR-009.4 A test helper that locates an element by exact markup (for example the
  `<nav class="language-switcher"` pattern) may be adapted so that it still locates the same element
  after a class is added; the expected text it checks does not change. Each such adaptation is listed
  in the implementation report.

### FR-010 One look across both hosts

- FR-010.1 Both hosts use the same Bootstrap file (FR-001.1) and the same styling of shared elements
  (header, language switcher, main column, tables, forms, buttons, notices, errors, notes). Rules
  for elements only one host has (the data-health banner, journal marks, page-search hint) live only
  in that host's `site.css`.

### FR-011 Proof

- FR-011.1 Automated tests per §5 (AC-001…AC-006, AC-008).
- FR-011.2 A manual look check (AC-007) with synthetic data only, recorded in the implementation
  report §3.1 (pages, widths, browser and version, date, result, any console message), and confirmed
  by the Owner at `HUMAN_PR_APPROVAL`.

## 5. Acceptance Criteria

AC-001…AC-007 are the Story's, with the proof each requires made precise; AC-008 makes the Story's
"light theme only" scope item testable.

- **AC-001 Every page of both hosts links the library and the site stylesheet.** Proved in two
  parts. (a) Structural, covering every view: each host's layout links
  `/lib/bootstrap/css/bootstrap.min.css` before `/css/site.css` and no other stylesheet;
  `_ViewStart.cshtml` sets that layout; no view under `Views/` assigns `Layout`. (b) HTTP, for each
  host: the anonymous pages (installation: Dean sign-in and the error page for 400, 403, 404, 500;
  Control Plane: sign-in, first-run setup, the error page for 400, 403, 404, 500) and at least one
  signed-in page for each role that has pages (installation: Admin and Dean; Control Plane: Owner)
  return HTML whose `<head>` links both stylesheets in that order; requesting both stylesheet paths
  returns `200` with a `text/css` content type.
- **AC-002 The library ships with its licence and its source is recorded.** In each host's
  `wwwroot/lib/bootstrap/`: `css/bootstrap.min.css`, `LICENSE.txt` and `VERSION.txt` exist and no
  other file; `LICENSE.txt` is the MIT licence text naming Bootstrap; `VERSION.txt` names a
  `5.3.x` version, the archive URL, the
  date and a `sha384-` hash; the SHA-384 of `bootstrap.min.css` (base64) equals that hash; the
  stylesheet's own header comment names the same version; both hosts' three files are byte-identical;
  no `*.js` or `*.map` file exists under either `wwwroot/lib/bootstrap/` and no layout or view
  references a Bootstrap script. Requesting `LICENSE.txt` and `VERSION.txt` returns `200` with
  `text/plain` (covered also by the unchanged every-static-file tests, AC-003).
- **AC-003 The security headers are unchanged.** For each host the existing security-header and
  no-store tests (US-050, US-040) pass unchanged; the sent CSP equals the string in §1.1 (no `data:`
  in `img-src`, no `'unsafe-inline'` or `'unsafe-eval'` anywhere); every new file under
  `wwwroot` (the Bootstrap stylesheet, `LICENSE.txt`, `VERSION.txt`, each override SVG) is served
  with `200`, the security headers and without `no-store` — the existing every-static-file tests
  prove this unchanged.
- **AC-004 No embedded image is left for the components the pages use.** A test reads the vendored
  `bootstrap.min.css`, collects every (selector, property) whose value contains a `data:` URL,
  ignores selectors scoped to `[data-bs-theme=dark]`, and keeps those whose classes all occur in the
  host's views. For each kept pair, the host's `site.css` declares the same property on the same
  selector with a root-relative `url("/…")`, and that path is served with `200` and
  `image/svg+xml`. The test fails when a view starts using such a component without an override, and
  it lists the overridden selectors; for the installation the list contains at least `.form-select`
  and the `.form-check-input` checked/indeterminate checkbox and checked radio selectors. Each
  override SVG contains no `<script`, no `href`/`xlink:href` to another file and no `data:` URL.
- **AC-005 No inline style or script appears.** `InlineCodeRuleTests` passes unchanged over every
  view of both hosts.
- **AC-006 No visible text changes.** The full existing suite passes with no text assertion, `.resx`
  entry or translation key changed; only markup-locating helpers are adapted per FR-009.4. The
  implementation report states that no `.resx` file changed and lists each adapted helper.
- **AC-007 The key pages look right on a computer and on a phone (manual).** As in the Story:
  installation — Dean sign-in, course list, journal, Meet reports, Dean-accounts settings, error page;
  Control Plane — sign-in, installation list; at desktop width and at about 375 px; synthetic data
  only; tidy and readable, no horizontal page scroll (wide tables scroll inside their area), marks
  and banner visible but restrained (FR-006), no CSP violation in the browser console; recorded in the
  implementation report §3.1 and confirmed by the Owner at `HUMAN_PR_APPROVAL`.
- **AC-008 One light theme.** Neither `site.css` contains `color-scheme` with `dark` or a
  `prefers-color-scheme` rule, and no layout or view contains `data-bs-theme`.

## 6. Validation Rules

No user input is added or changed. The "validation" of this Story is of the shipped files:

- `bootstrap.min.css` must match the published integrity hash recorded in `VERSION.txt` (AC-002);
  a modified or swapped file fails the build's tests.
- Override SVGs: same-host, `image/svg+xml`, no script, no external reference, no `data:` (AC-004).
- Field-level validation of existing forms is unchanged (FR-005.4: Bootstrap validation classes are
  not used, server-side validation and its messages stay as they are).

## 7. Security Requirements

- SC-15 (v105): the CSP and the other security headers of both hosts are not changed; `img-src`
  never gains `data:`, `style-src` never gains `'unsafe-inline'` (FR-007, FR-009.3, AC-003).
- SC-15 "No inline code": no `style` attribute, no `<style>`/`<script>` block with a body, no event
  handler attribute (AC-005). Bootstrap scripts are not shipped (FR-001.4).
- SC-13: nothing is loaded from another site — no CDN, no web font, no counter (FR-001, FR-002.1).
- SC-4: static files stay anonymous, read-only, with no data and nothing user-specific; the new
  files are plain CSS, SVG, a licence and a version note, all public content.
- SC-14: static files keep normal caching; every other response keeps `no-store` (AC-003).
- Supply chain: the library comes from the official release, is checked against the published hash
  and its hash is pinned by a test (FR-001.2, AC-002).
- The manual check uses synthetic data only (AGENTS.md Hard Stop on `classroom_cache.db` and real
  exports).

## 8. Error Handling

- The error page of each host renders with the shared look (it uses the layout) and keeps its
  texts and status codes.
- A request for `bootstrap.min.css.map` (or any other missing static file) gets the host's existing
  `404` answer with the security headers and `no-store` (existing tests
  `AMissingFileUnderAStaticPath_*`).
- If the stylesheet cannot load, pages remain usable as unstyled HTML (no content depends on CSS).

## 9. Non-Functional Requirements

- NFR-076 (the whole Story); NFR-070 (phone width, FR-008); NFR-073 (no text change: both languages
  keep their texts, FR-009.1).
- Size: one minified stylesheet (~230 KB) per host, cached by the browser (SC-14 exception); no
  build step.
- Accessibility is not reduced: marks keep their text labels, the current language keeps
  `aria-current`, focus outlines stay Bootstrap's defaults.

## 10. Out of Scope

- A navigation menu or any new link or page (comes with «Базова», v89 agenda item 3); hence no
  Bootstrap scripts.
- Dark theme, colour settings, the school's name or logo.
- Any change of visible text, translation key, route, policy, form field, export file or business
  rule.
- Any change of the security headers or the CSP of either host.
- Loading anything from a CDN or another site; a new NuGet package or .NET tool.
- Restyling the generated `.xlsx` exports or the report templates.

## 11. Open Decisions

`trebovaniya.md` §7 holds no open item this Story depends on (items 26 and 27 and the others concern
documentation, re-loading purged leavers, Meet fields and delegation). The Story's OD-001 and OD-002
were resolved a) by the Owner on 2026-11-01 and are carried into
`docs/decisions/US-068-open-decisions.md`. No new Open Decision.

Interpretations made here, for confirmation at `HUMAN_SPEC_APPROVAL`:

- I-1 AC-001 "every page" is proved structurally (one layout, no view overrides it) plus HTTP checks
  on every anonymous page and one signed-in page per role — not by requesting each of the ~38 page
  routes with seeded data (FR-002.2, AC-001).
- I-2 The main column widens from today's 40rem (installation) / 28rem (Control Plane) to Bootstrap's
  responsive container; forms stay narrow (FR-004.2).
- I-3 Submit buttons of irreversible confirmations use the danger colour as an outline button
  (FR-005.3).
- I-4 Bootstrap's validation-state styling and icons are not used; field errors stay plain red text
  (FR-005.4).
- I-5 The source map is not shipped; `bootstrap.min.css` stays byte-for-byte unchanged (FR-001.4) —
  a technical choice the Story left to the Specification.
- I-6 Test helpers that find an element by exact markup may be adapted to the added classes without
  changing the text they check (FR-009.4, AC-006).
- I-7 The installation's automatic dark theme (`color-scheme: light dark`) is removed (FR-003.2).
- I-8 The licence and the version note are plain-text files `LICENSE.txt` and `VERSION.txt` next to
  the stylesheet, and like every static file they can be opened in a browser at
  `/lib/bootstrap/LICENSE.txt` and `/lib/bootstrap/VERSION.txt` (FR-001.1, FR-001.5). A file without
  extension would be answered `404` and break the existing every-static-file tests; the content
  is public anyway (licence text, version, download URL and hash), so serving it exposes nothing.
  `bootstrap.min.css` also keeps its own licence header.
- I-9 Only the data-health banner, the grade-above-maximum mark and the Control Plane warnings are
  coloured; the US-059…US-063 text labels stay plain text (FR-006.1, FR-006.2).

## 12. Traceability

| AC | Story | Requirements |
|---|---|---|
| AC-001 | AC-001 | FR-002, FR-011.1; NFR-076 |
| AC-002 | AC-002, OD-001 | FR-001 (incl. FR-001.5); NFR-076, SC-4, SC-13 |
| AC-003 | AC-003 | FR-009.3; SC-14, SC-15 |
| AC-004 | AC-004 | FR-007, FR-005.5; SC-15 (v105) |
| AC-005 | AC-005 | FR-009, §7; SC-15 |
| AC-006 | AC-006 | FR-009.1, FR-009.4; NFR-073, NFR-076 |
| AC-007 | AC-007, OD-002 | FR-003…FR-008 (incl. FR-006.1–FR-006.3), FR-010, FR-011.2; NFR-070, NFR-076 |
| AC-008 | Scope "light theme only" | FR-003; NFR-076 |
