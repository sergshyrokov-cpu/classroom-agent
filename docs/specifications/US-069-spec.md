---
artifact_type: specification
story: US-069
version: 3
status: APPROVED
created_at: 2026-11-02T11:06:50Z
updated_at: 2026-11-02T11:47:36Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
  - path: trebovaniya.md
    version: 106
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
supersedes: docs/specifications/US-069-spec.md@v2
---

# US-069 Specification — Small interface text fixes and a show-password control

Grounded in `trebovaniya.md` v106 §8 («Подписи на страницах», «Показ пароля»), NFR-077 and
SC-2 («Showing a typed password»). Nothing was delegated: the code facts below were read
directly at commit `f081084`.

## Overview

Five small changes in views, translation files, `site.css` and one new script file per host:
a labelled role line, one page title, named blocks on `/database`, a course prompt on Meet
reports, and an eye control on all 9 password fields. No endpoint, schema, policy, form field,
audit event, export or business rule changes.

## Business Goal

A first-time user understands each page without asking and makes fewer mistakes typing a
password. The items come from the Owner's look check at US-068 AC-007, before the first demo.

## Business Flow

1. An Admin or Dean signs in and reads «Роль: Декан» on the landing page.
2. On `/database` they see three named blocks; on Meet reports the course field asks for a course.
3. Anyone typing a password may press the eye to check the text, press it again to hide it, and
   submit; the field is hidden again before the page leaves, and the request is the one a hidden
   field would send.

## Functional Requirements

### FR-001 Role line (Scope 1, OD-002 a)

`Views/Home/Index.cshtml`, element `#signed-in-role` (id kept): one new translation key with a
placeholder — uk `Роль: {0}`, en `Role: {0}` — filled with the existing `Landing.Role.*` text
for the user's role. So: «Роль: Декан», «Роль: Адміністратор школи», "Role: Dean",
"Role: School administrator". The `Landing.Role.*` keys keep their values.

### FR-002 One page title (Scope 2)

Remove the view's own `<h1>` from `MeetCodes/Index`, `MeetCodes/CourseChoice`, `Erasure/Index`
and `Erasure/Confirmation`. Each already sets `ViewData["Title"]` from the same key as the removed
`<h1>`, so the layout's `<h1>` keeps the text. Nothing else in these views changes; on
`MeetCodes/Index` the search hint now directly follows the layout's `<h1>`.

The layouts of both hosts are the only views that may hold `<h1>`. Every non-partial view of both
hosts sets `ViewData["Title"]` today (checked by listing), so no page has an empty title.

### FR-003 Named `/database` blocks (Scope 3)

`Views/Database/Index.cshtml`: each of the three `dl` gets an `<h2>` directly above it, from new
translation keys:

| Block | Counters (unchanged) | uk | en |
|---|---|---|---|
| first `dl` in `#database-counters` | course states, total | Курси | Courses |
| second `dl` in `#database-counters` | teachers … Meet codes | Дані | Data |
| `dl` in `#database-synchronization` | synchronization lines | Синхронізація | Synchronization |

- In `#database-counters` each heading and its `dl` are wrapped together in one element, so the
  flex row in `site.css` keeps two blocks side by side; `site.css` is adjusted only as far as that
  needs. The `#database-synchronization` heading is the section's first child.
- All ids and `data-counter` / `data-sync-line` attributes, every label and value, the failure hint
  or link, and the layout (three in one row, stacked on a phone) stay.

### FR-004 Meet reports course prompt (Scope 4)

`Views/MeetReports/Index.cshtml`: the first option of `#meet-reports-course` shows the existing
key `Journal.Form.ChooseCourse` («— оберіть курс —», "— choose a course —"), with `value=""`. No
new key: reusing it guarantees the same words as the journal and the report (§8). The field name,
the options after it, the no-course branch and what a request without a course does are unchanged.

### FR-005 The control's markup (Scope 5, OD-004 a, OD-005 a)

All 9 password fields in 6 views carry the control, through one shared partial per host so that the
markup has one source:

| Host | View | Fields |
|---|---|---|
| Control Plane | `Setup/Index` | `password`, `passwordConfirmation` |
| Control Plane | `SignIn/Index` | `password` |
| Installation | `SignIn/Index` | `password` |
| Installation | `DeanPassword/Forced` | `newPassword` |
| Installation | `DeanPassword/Own` | `currentPassword`, `newPassword` |
| Installation | `DeanAccounts/Index` | `temporaryPassword` (per-Dean row form), `temporaryPassword` (create form) |

The row field repeats once per Dean, so the control uses no element id and the script handles any
number of fields on a page.

- FR-005.1 Field and control sit together in a wrapper element (`span`, class `password-field`). The
  input comes first. Where a `label` wraps the input today (the six installation fields) it keeps
  wrapping both, so a press on the label text still focuses the input (the first labelable
  descendant), and a press on the control does not. This is a known trade-off: a `button` inside a
  `label` breaks the HTML content model, and a screen reader may add the control's name to the
  field's name. It is kept, as the Owner accepted on 2026-11-02 (review finding m-1), because the Dean-accounts rows line up labels and fields through a grid on
  that `label` (US-068), and moving the control or the label out would change that layout, which this
  Story keeps. Browsers behave as described here; AC-011 checks the result.
- FR-005.2 The control is `<button type="button" hidden>` with **no** `name`, `value` or `form`
  attribute, no icon word, class `password-toggle`, `aria-pressed="false"` and the two translated
  accessible names in data attributes. Its `aria-label` is the "show" name. The view adds no other
  element to the form.
- FR-005.3 Every one of the 9 inputs keeps its `type="password"`, name, `autocomplete` and `value`
  exactly as today, and gains `spellcheck="false"` and `autocapitalize="off"` in the markup, so the
  rule of AC-006 holds from the first render and a server-side test can prove it.
- FR-005.4 Accessible names, uk «Показати пароль» / «Сховати пароль», en "Show password" /
  "Hide password", live in the translation files of both hosts.
- FR-005.5 The script `wwwroot/js/password-toggle.js` of the host is referenced with `defer` from
  those 6 views only, in the existing per-view pattern, not from the layout.

### FR-006 The control's behaviour

The script, on load, for each `.password-field`:

- FR-006.1 Removes `hidden` from the control and adds class `password-field--ready` to the wrapper.
  Until this runs the control is invisible (AC-007). The `hidden` attribute must really hide it:
  no `site.css` rule for `.password-toggle` may defeat it.
- FR-006.2 A press on a control whose field is hidden sets the input's `type` to `text`,
  `aria-pressed` to `true` and `aria-label` to the "hide" name. A second press restores
  `type="password"`, `aria-pressed="false"` and the "show" name. The value is never read, copied or
  changed by the script.
- FR-006.3 The control is a native button: reachable by Tab, pressed by Enter or Space, never submits
  the form. Enter in the field still submits through the form's submit button.
- FR-006.4 On any `submit` event in the document, and on `pagehide`, the script returns **every**
  shown field of the page to hidden (FR-006.2's second state). The `submit` listener covers every form
  on the page, the language switcher included: it is a POST form, not a link. `pagehide` covers leaving
  with no form at all: a link (the data-health banner on signed-in pages, the sign-in link on Control
  Plane setup), the address bar, a bookmark or the browser's own "back". So a page restored on "back"
  shows no password by any route. Under SC-14 every page is `no-store`, so "back" normally loads the
  page anew, hidden and empty, whatever the script did; a browser may still restore a page in some
  cases, and the listeners are the barrier for those.
- FR-006.5 The script makes no network request, uses no browser storage, writes nothing to the
  console, and uses no inline code. If it fails to run the page behaves as FR-007.

### FR-007 Without scripts

With scripts off the control stays `hidden`, no `password-field--ready` class exists, and the field
works as it does today, Edge's own reveal button included (FR-008.2).

### FR-008 Styles and icons

- FR-008.1 Two same-host icons in each host: `wwwroot/img/password/eye.svg` and `eye-off.svg`
  (the Control Plane has no `wwwroot/img` yet). The `.password-toggle` background image comes from
  `site.css` and is selected by `aria-pressed`, so icon and state cannot disagree. The icons hold no
  script, external reference or embedded data, which `EmbeddedImageOverrideTests` already checks for
  images `site.css` references.
- FR-008.2 Edge's own reveal button is hidden through `.password-field--ready input::-ms-reveal`
  only, so a field never shows two controls, and with scripts off it keeps the browser's own.
- FR-008.3 The control sits inside the right edge of the field; a ready field has enough right
  padding that the control covers no text. The wrapper takes the field's place in every existing
  layout, the grid of the Dean-accounts rows included, so fields keep the width and alignment US-068
  left them with. These rules go in the part of `site.css` that is shared
  word for word between the hosts. They add no `content` with words and no animation
  (`BootstrapLayoutRuleTests`).

## Acceptance Criteria

Ids and meaning are the Story's.

- **AC-001** The landing page role line reads the word for role followed by the role name, from the
  translation files, in the page's language, for Admin and Dean, uk and en.
- **AC-002** Each of the four views holds exactly one `<h1>`, with the same text as before, for a
  role allowed to open it; a test fails if any view of either host other than the layouts renders
  its own `<h1>`.
- **AC-003** On `/database`, in both languages, each of the three blocks has its heading above its
  numbers; every counter and synchronization line keeps its label and value.
- **AC-004** On Meet reports with at least one course, in both languages, the first course option
  shows the prompt of the journal and the report with an empty value; a request with no course
  behaves as today.
- **AC-005** On any of the 9 fields with a script-capable browser, a press shows the typed text and
  the control's accessible name and state say so; a second press hides it; the text is kept; the
  control does not submit the form and is reachable by keyboard.
- **AC-006** A field shown as text returns to hidden before the page leaves on submit; the browser
  neither stores the value as ordinary text nor restores it visible on "back"; the request equals a
  hidden field's; spell checking and auto-capitalisation are off.
- **AC-007** With scripts off, no dead control shows and every password field works as today.
- **AC-008** Both hosts' CSP string is unchanged and the existing header tests pass unchanged;
  `InlineCodeRuleTests` passes: the behaviour lives in a script file in each host's `wwwroot/js`, with
  no inline handler or style; nothing is loaded from another site; a server-side test proves the
  control adds no field to any form.
- **AC-009** Existing sign-in, lockout and password tests of both hosts pass without a change of
  expectation; no password value appears in a log or a response.
- **AC-010** Every new text exists in both languages of the host using it; existing translation tests
  pass.
- **AC-011 (manual)** Run locally with synthetic data, in Microsoft Edge, at desktop width and about
  375 px: Dean sign-in, Control Plane sign-in, the Dean-accounts page, the landing and `/database`
  pages. Each field shows one control only, sitting inside or next to the field without covering
  text; the console shows no CSP violation. The result is recorded in the implementation report and
  confirmed by the Owner at `HUMAN_PR_APPROVAL`.

**How the browser behaviour is proved.** The project has no browser test tool and none is added, so
automated tests cannot press the control. They prove the rendered markup and the script's source
(see Traceability). The dynamic part of AC-005, AC-006 and AC-007 is therefore also walked by hand
within the AC-011 session and recorded with it. The person walking it is the Owner, as in the US-068
look check; the implementor prepares the local run with synthetic data and records the result.

1. type, press, press again: text shows, hides, is kept; keyboard only as well (Dean sign-in page and
   a Dean-accounts row);
2. press to show, submit a wrong password, then "back": the field is hidden (Dean sign-in page;
   leaving by `submit`);
3. press to show, leave through the address bar, then "back": the field is hidden (a Dean-accounts
   row; leaving with no form);
4. scripts off: no control, the field accepts and submits a password (Dean sign-in page and a
   Dean-accounts row).

Steps 2 and 3 confirm the outcome AC-006 asks for and nothing more. Under SC-14 "back" normally loads
the page anew, so both steps can pass with the `submit` and `pagehide` listeners missing; they do not
tell whether a listener ran. That the listeners exist and do what FR-006.4 says rests on the script
source scan and on SECURITY_REVIEW reading the script. No test watches them run: the project has no
tool that could.

## Validation Rules

No input is added and no validation changes. The 9 fields keep their names and server-side rules; the
empty prompt option keeps `value=""` and meets today's "no course" handling. The control contributes
nothing to a request, so there is nothing new to validate.

## Security Requirements

- SC-2 «Showing a typed password» governs the control: same-host script and icon; nothing reaches the
  server that a hidden field would not send; hidden again on submit; spell checking and
  auto-capitalisation off (FR-005.3, FR-006.4). A difference in password rules, sign-in checks,
  lockout, storage or logging caused by the control is a finding.
- SC-15: the CSP string of both hosts is unchanged. The script is a same-host file (`script-src
  'self'`), with no inline handler or style; icons are same-host (`img-src 'self'`).
- SC-13: nothing is loaded from or sent to another site (FR-006.5).
- SC-4: the sign-in and first-run setup pages load a same-host script for the first time, as a
  static file already on the closed list. **SECURITY_REVIEW looks at this specifically**, since these
  pages take the password: the script must only toggle the type and never read the value (FR-006.2).
- SC-14: `no-store` is unchanged. It stands behind FR-006.4 and hides from the manual walk whether the
  listeners run, so **SECURITY_REVIEW reads the `submit` and `pagehide` listeners in the script**.
- No policy changes; every changed view keeps its existing authorization. No password value is logged
  or returned (SC-10); the Control Plane's `value=""` stays.

## Error Handling

No server-side path is added and no error text changes. A field re-rendered after a failed submit
starts hidden with the control reset, as every page load does. A script failure leaves the behaviour
of FR-007.

## Non-Functional Requirements

- NFR-077: labels as above; no new visible word from the control. Icon names come from translation
  files.
- NFR-073: every new text in uk and en of the host using it (AC-010): role line and three headings in
  the installation; the two accessible names in both hosts. No text is hard-coded.
- NFR-076 / §8 v105: the look stays as US-068 left it, apart from the additions above. No package, no
  tool, no CDN.
- Phone width: the control keeps the field usable at about 375 px (AC-011).

## Out of Scope

- search in the teacher list and the Meet codes list (a separate discussion);
- the language labels: they stay `УКР` and `ENG` (OD-003 a);
- any change of password rules, sign-in checks, lockout, what is stored or logged;
- any route, policy, form field name, export file, audit event or business rule;
- any change of the security headers or the CSP string;
- any other wording; the look of pages stays as US-068 left it;
- a browser test tool.

API_DESIGN and DB_DESIGN: this Story **does not change public API behaviour** (no endpoint, parameter,
status code or response body beyond the view text above) and **does not change persistence behaviour**
(no entity, schema or migration).

## Open Decisions

OD-001…OD-005 were all resolved a) by the Owner on 2026-11-02, recorded in
`docs/decisions/US-069-open-decisions.md`; none is open. `trebovaniya.md` §7 holds no item this Story
depends on.

## Traceability

| AC | Requirement | Proof |
|---|---|---|
| AC-001 | FR-001 | HTTP test: role line, Admin and Dean, uk and en |
| AC-002 | FR-002 | HTTP test: one `<h1>` per view; source scan of both hosts' views. `PageSearchHintTestData` ordinal for `MeetCodes/Index` adapts from 2 to 1 |
| AC-003 | FR-003 | HTTP test: headings before their `dl`, uk and en; existing `data-counter` tests unchanged |
| AC-004 | FR-004 | HTTP test: first option text and empty value, uk and en; existing no-course test unchanged |
| AC-005 | FR-005, FR-006.1…3 | Markup test on all 9 fields; script source scan; manual steps 1 |
| AC-006 | FR-005.3, FR-006.4 | Markup test of the two attributes; script source scan for `submit` and `pagehide`; SECURITY_REVIEW reads the listeners; manual steps 2 and 3 for the outcome only, not for the listeners (SC-14) |
| AC-007 | FR-006.1, FR-007, FR-008.2 | Markup test: control rendered `hidden`, ready class absent from the markup; CSS scan; manual step 4 |
| AC-008 | FR-005.2, FR-005.5, FR-006.5, FR-008 | Header and `InlineCodeRuleTests` unchanged; rendered-form test: field names of each form the same with the control; scan: no external URL, network or storage call in the script |
| AC-009 | SC-2 | Existing tests unchanged; log and response checks for a known password value |
| AC-010 | FR-001, FR-003, FR-005.4 | Translation tests in both hosts |
| AC-011 | FR-008, FR-005.1 | Manual, recorded in the implementation report |
