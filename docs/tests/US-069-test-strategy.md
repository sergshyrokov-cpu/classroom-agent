---
artifact_type: test_strategy
story: US-069
version: 1
status: DRAFT
created_at: 2026-11-02T12:01:20Z
updated_at: 2026-11-02T12:01:20Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-069-small-interface-texts-and-show-password.md
    version: null
  - path: docs/specifications/US-069-spec.md
    version: 3
  - path: docs/decisions/US-069-open-decisions.md
    version: 1
  - path: docs/designs/api/US-069-api-design.md
    version: 1
  - path: docs/designs/database/US-069-db-design.md
    version: 1
supersedes: null
---

# US-069 Test Strategy

## 1. Scope

Views, translation files, `site.css` and one script file per host. No endpoint, schema, policy or form field
changes (API_DESIGN and DB_DESIGN are NOT_APPLICABLE). No compile-only skeleton: the tests reference no new C# type.

## 2. Test levels

| Level | Used for | Classes |
|---|---|---|
| HTTP integration (real PostgreSQL, TC-2) | rendered pages of both hosts | `SmallTextsPageTests`, `PasswordToggleMarkupTests`, `ControlPlanePasswordToggleMarkupTests`, `PasswordValueNotExposedTests`, `ControlPlanePasswordValueNotExposedTests` |
| Source scan | script, views, `site.css`, translation files | `PasswordToggleSourceTests`, `OnePageTitleRuleTests`, `SmallTextsTranslationTests`, `PasswordToggleTranslationTests` |
| Manual (Owner, recorded in the implementation report) | what only a browser shows | AC-011 and spec steps 1-4 |

The project has no browser test tool and none is added (spec). So the automated tests prove the rendered markup and the
script's source; no test watches a listener run. Under SC-14 the manual "back" steps confirm the outcome only;
SECURITY_REVIEW reads the listeners.

## 3. Markup hooks the tests fix

The Specification names these and the tests assert them: `#signed-in-role`, `span.password-field`, `password-field--ready`,
`button.password-toggle`, `/js/password-toggle.js`, `/img/password/eye.svg`, `/img/password/eye-off.svg`,
`#database-counters`, `#database-synchronization`, `#meet-reports-course`.

The Specification does not name these, and the tests stay free of them, so IMPLEMENTATION chooses:

- the two `data-*` attributes holding the accessible names (the tests look for the values among all `data-*` values);
- the partial's file name (the test requires one per host, starting with `_`);
- the new translation keys (the tests look for a key whose uk and en values are both as the Specification gives);
- which icon goes with which state (the tests require two different icons, one of them under `[aria-pressed=true]`).

## 4. Scenarios

**Positive.** Role line for Admin and Dean, uk and en. One `<h1>` on each of the four views. Three headings directly above
their `dl`, uk and en. The prompt as first option, uk and en. Every one of the 9 fields carries the control. The script and
both icons are served. New texts in both languages.

**Negative.** No view but the layout holds `<h1>`. The script reads no value, reaches no network, uses no storage or console,
submits nothing, holds no URL of another site. A page with no password field does not load the script. The layout does not
reference it. No `site.css` rule defeats `hidden`. Edge's reveal button is hidden only on a ready field. No view holds a new
text as a literal.

**Boundary.** The Dean-accounts page with two Deans: three fields, no element id on any control. Meet reports with a selected
course: the prompt is still first and not selected. An empty `courseId` behaves as no course.

**Validation.** None is added (spec Validation Rules). A field re-rendered after a refused submit starts hidden with no value:
Dean sign-in, Dean creation, Owner sign-in, setup.

**Security.** AC-008: each form holding a password field names exactly the fields it names today. FR-005.3: the two attributes in
the markup on all 9 fields. FR-006.4: page-wide `submit` listener and `pagehide` on the window. AC-009: a known password value
is in no response and no log line, on both hosts. CSP and header tests, `InlineCodeRuleTests`, `EmbeddedImageOverrideTests` and
`BootstrapLayoutRuleTests` stay as they are and now cover the new files.

**Persistence.** None: the Story changes no entity, schema or migration.

## 5. Fixtures

`InstallationTestHost`, `ControlPlaneTestHost`, `JournalHostExtensions` (Admin, Dean), `DeanAccountHostExtensions`,
`MeetReportsHostExtensions`, `MeetCodesHostExtensions`, `ErasureHostExtensions`, `DatabaseStatisticsHostExtensions`, and the
new `PasswordFieldMarkup` (reads the control out of a page and out of the script). All data is synthetic (TC-4); no Google or
Control Plane call is made.

## 6. Existing tests adapted

`PageSearchHintTests.TitleOrdinalOf`: Meet codes now has one title, so the ordinal goes from 2 to 1 for every page (spec
Traceability). `PageSearchHintTestData.AfterTitle`'s summary is updated to match. No other existing expectation changes;
AC-009 requires that.

## 7. Excluded, with reason

- Pressing the control, keyboard reach, the listeners running, Edge's rendering, 375 px layout, CSP violations in the console:
  no browser test tool (spec); manual steps and AC-011.
- `autocomplete` on the Control Plane's fields: absent today and asserted absent, since FR-005.3 keeps it as it is.
- The English accessible names on Control Plane pages over HTTP: its two password pages are anonymous and render in the default
  language, so English is proved in the translation file (AC-010).

## 8. Known limitations

- The script scan is textual: it proves what the source holds, not what a browser executes. SECURITY_REVIEW reads it (spec).
- `AReadyField_MakesRoomForTheControl` proves a right padding exists; whether it is enough is AC-011.

## 9. Open Decisions

OD-001...OD-005 are resolved; none affects testing, and this stage raised none. `US-069/SPEC_REVIEW/m-4` concerns the digest's
wording, not the tests, and stays OPEN.
