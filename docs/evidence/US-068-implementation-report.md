---
artifact_type: implementation_report
story: US-068
version: 2
status: DRAFT
created_at: 2026-11-01T14:13:35Z
updated_at: 2026-11-02T10:06:20Z
produced_by: dotnet-implementor
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
  - path: docs/tests/US-068-test-strategy.md
    version: 1
  - path: docs/tests/US-068-ac-test-matrix.md
    version: 1
supersedes: docs/evidence/US-068-implementation-report.md@v1
attempt: 2
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-068 Implementation Report — Both hosts share one restrained look built on Bootstrap

Revision v2 (attempt 2): the loop-back from `/so:reject` at HUMAN_PR_APPROVAL, finding
`US-068/HUMAN_PR_APPROVAL/R-1`. The look-check changes were made in the working tree during the Owner's AC-007 check;
this attempt keeps them, traces them (§4.1), records the Owner's decisions (§7) and the AC-007 results (§3.1), deletes
the temporary stand and re-runs the full validation.

## 1. Summary

Bootstrap 5.3.8 (CSS only) is vendored unchanged in both hosts with its MIT licence and a version note; both layouts
link it before `site.css` and put the header and main column in a Bootstrap container; the views carry Bootstrap
classes added to their existing elements; each `site.css` is written on top of Bootstrap (light theme only, quiet
notices, amber data-state marks and Control Plane warnings, danger outline per OD-003 a), then tuned to the Owner's
look check (compact type, one 48rem content column, right-aligned numbers and dates). The installation's four `data:`
images in use are replaced by same-host SVG files. No endpoint, policy, header, CSP, translation, script or business
rule changed. Build clean, full suite 6990/6990, format clean. AC-007 was done by the Owner on 2026-11-02 (§3.1);
the console part covers five pages, not all the AC's pages (§7 m-1).

`security_sensitive: true` only because the Story touches what the browser loads under the CSP (SC-15): nothing in
the security headers changed.

## 2. Source artifacts

Story `docs/stories/US-068-restrained-look-with-bootstrap.md`; spec v2 (APPROVED); open decisions v2 (OD-001…OD-003
a); API and DB design v1 (NOT_APPLICABLE); test strategy v1; AC test matrix v1; ledger finding
`US-068/HUMAN_PR_APPROVAL/R-1` in `docs/workflow/findings.jsonl` (the Owner's rejection comment, full text).

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001 | both `Views/Shared/_Layout.cshtml`: `<link>` to `/lib/bootstrap/css/bootstrap.min.css` then `/css/site.css`; `_ViewStart` unchanged | `BootstrapLayoutRuleTests` (layout, view start, no own layout), `InstallationStylesheetLinkTests`, `ControlPlaneStylesheetLinkTests` | PASS |
| AC-002 | `src/*/wwwroot/lib/bootstrap/{css/bootstrap.min.css, LICENSE.txt, VERSION.txt}`, byte-identical; `.gitattributes` keeps them byte-for-byte | `VendoredBootstrapTests` (13), `…StylesheetLinkTests.ALibraryFile_IsServedWithItsContentType` | PASS |
| AC-003 | no change to `SecurityHeadersMiddleware`, `Program.cs` or content types | existing `SecurityHeadersResponseTests`, `NoStoreResponseTests`, `UnknownFileLikePathTests`, `SecurityHeadersRuleTests` (in the full suite; they now also request every new static file) | PASS |
| AC-004 | installation `site.css` overrides `.form-select`, `.form-check-input:checked[type=checkbox]`, `.form-check-input:checked[type=radio]`, `.form-check-input[type=checkbox]:indeterminate` with `/img/bootstrap/*.svg`; four SVGs reproduce Bootstrap's drawing | `EmbeddedImageOverrideTests` (9), `InstallationStylesheetLinkTests.EveryOverrideImage_IsServedAsSvg` | PASS |
| AC-005 | class additions only; no style/script/handler added | `InlineCodeRuleTests` 29/29 unchanged | PASS |
| AC-006 | no `.resx`, text or translation key changed (no `.resx` in `git status`); no test helper needed adapting in either attempt | full suite 6990/6990; `BootstrapLayoutRuleTests.TheSiteStylesheet_AddsNoTextAndNoAnimation` | PASS |
| AC-007 | §3.1 | manual, Owner | DONE by the Owner with the §4.1 changes; console part partial (§7 m-1); confirmed by the Owner at HUMAN_PR_APPROVAL |
| AC-008 | installation `site.css` no longer declares `color-scheme: light dark`; no `data-bs-theme` anywhere | `BootstrapLayoutRuleTests.TheSiteStylesheet_KeepsTheLightThemeOnly`, `NoView_SetsABootstrapTheme` | PASS |

### 3.1 Manual look check (AC-007)

Source: the Owner's rejection comment, finding R-1. Synthetic data only, on a temporary look-check stand.

| Field | Value |
|---|---|
| Date | 2026-11-02 |
| Browser and version | Microsoft Edge 156.0.4314.58 (64-bit) |
| Widths | desktop width and DevTools device mode 375 px |
| No horizontal page scroll at 375 px | Dean sign-in, course list, journal, Meet reports, Meet codes, Dean accounts, database, error page, CP sign-in, CP installation list |
| Result | approved by the Owner only with the changes of §4.1 |
| CSP messages in the console | console clean, no CSP violation, seen on installation home, course list, journal, CP home, CP installation list |
| Not seen in the console | of the AC-007 pages: Meet reports and the error page (stated by the Owner in R-1); Dean sign-in, Dean accounts and CP sign-in are also not on the list above — see §7 m-1 |

## 4. Change set

| File | Trace |
|---|---|
| `.gitattributes` | supporting change: `src/*/wwwroot/lib/** -text` keeps the vendored files byte-for-byte (AC-002 pinned hash; closes TEST_WRITING INFO-1) |
| `src/ClassroomAgent.Web/wwwroot/lib/bootstrap/css/bootstrap.min.css`, `LICENSE.txt`, `VERSION.txt` | FR-001, AC-002 |
| `src/ClassroomAgent.ControlPlane/wwwroot/lib/bootstrap/css/bootstrap.min.css`, `LICENSE.txt`, `VERSION.txt` | FR-001, FR-010.1, AC-002 |
| `src/ClassroomAgent.Web/wwwroot/img/bootstrap/{select-arrow,checkbox-checked,checkbox-indeterminate,radio-checked}.svg` | FR-007.3, AC-004 |
| `src/ClassroomAgent.Web/Views/Shared/_Layout.cshtml`, `src/ClassroomAgent.ControlPlane/Views/Shared/_Layout.cshtml` | FR-002.1, FR-004.2, AC-001 |
| `src/ClassroomAgent.Web/wwwroot/css/site.css` | FR-003…FR-008, FR-010, AC-004, AC-008; AC-007 look check, §4.1 |
| `src/ClassroomAgent.ControlPlane/wwwroot/css/site.css` | FR-004, FR-005, FR-006.1 (`warning`), FR-010; AC-007 look check, §4.1 |
| 25 installation views (`AccessCheck/Index`, `ConnectionInstruction/Index`, `Courses/Card`, `Courses/Index`, `DataHealth/Index`, `Database/Index`, `DeanAccounts/Index`, `DeanPassword/Forced`, `DeanPassword/Own`, `Erasure/Confirmation`, `Erasure/Index`, `Home/Index`, `Journal/Index`, `MeetCodes/CourseChoice`, `MeetCodes/Index`, `MeetReports/Index`, `MeetReports/Meeting`, `Report/Index`, `ReportTemplates/Delete`, `ReportTemplates/Form`, `ReportTemplates/Index`, `Shared/_LanguageSwitcher`, `SignIn/Index`, `Teachers/Index`, `WorkspaceConnection/Index`) | FR-005 (class additions only), OD-003 a); AC-004 needs `form-select`/`form-check-input` in use; `text-end` per §4.1 |
| 13 Control Plane views (`AllowedAdmins/New`, `AllowedAdmins/Revocation`, `Home/Index`, `InstallationStatus/Confirmation`, `Installations/{ClientId,Detail,Index,Name,New,PushAddress}`, `Setup/Index`, `Shared/_LanguageSwitcher`, `SignIn/Index`) | FR-005, OD-003 a); `text-end` per §4.1 |
| tests: `Architecture/BootstrapLayoutRuleTests.cs`, `Architecture/VendoredBootstrapTests.cs`, `Architecture/EmbeddedImageOverrideTests.cs`, `Web/Pages/InstallationStylesheetLinkTests.cs`, `ControlPlane/Controllers/ControlPlaneStylesheetLinkTests.cs`, `TestInfrastructure/Styling.cs`, `TestInfrastructure/CssDeclaration.cs` | TEST_WRITING (unchanged in both attempts) |

Deleted in this attempt, never committed: `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs` (the temporary
stand, R-1 "before commit"). Nothing else referred to it.

Class rules applied to views: text-like inputs and `<textarea>` `form-control`; `<select>` `form-select`;
checkbox/radio `form-check-input` with a wrapping label `form-check`; other labels `form-label`; the first submit of a
form `btn btn-primary`, other buttons `btn btn-outline-secondary`, buttons in table cells `btn-sm`; language switcher
buttons `btn-sm btn-outline-secondary`, current language `btn-sm btn-secondary active` (keeps `aria-current`);
tables `table table-sm table-bordered`. Danger outline (`btn btn-outline-danger`) only on Erasure confirmation,
report-template deletion, Admin revocation and — by a Razor condition — installation suspension; resumption stays
`btn-primary` (OD-003 a). `InstallationStatus/Confirmation.cshtml` gained `@using ClassroomAgent.ControlPlane.Services`
for that condition. In `MeetCodes/Index.cshtml` the button built in the C# `MarkForm` string got the same class text.

Re-checked in this attempt, after all look-check edits: `class` attributes stripped from HEAD and the working copy of
all 40 changed views and compared. Three differ beyond classes, all expected: the two layouts (the Bootstrap `<link>`)
and `InstallationStatus/Confirmation` (the `@using` and the Razor class condition above).

No secret, generated database file or IDE-local config is in the change set. Not in this set: the workflow state
files and the Story's docs artifacts (closed list of the Git Policy).

### 4.1 Changes from the AC-007 look check (R-1)

All in both `site.css` unless stated; the shared part (the first block of each file) is identical, checked with
`diff`. Every one traces to AC-007 and R-1; the FR column names the requirement it refines.

| Change | Where | FR |
|---|---|---|
| Compact type and spacing: h1 22 px, h2 1.1rem, body 0.9375rem, tables 0.875rem with tight rows | shared | FR-004, FR-005.1 |
| One 48rem content column through `--content-width`; language switcher on the title row, ending at the column's right edge, with the banner | shared | FR-004.1, FR-004.2 (DOM order unchanged) |
| All legends body size and bold; space before a submit button under a field | shared | FR-005.2 |
| Installation tables in `.table-scroll` do not wrap | installation | FR-005.1, FR-008 |
| Date range and «Показати» on one line; on a phone each date label stays with its date | installation | FR-005, FR-008 |
| «Показати» on the Status line, course and teacher lists | installation | FR-005 |
| Course card section titles like field titles, more space between links; workspace and course card links do not break inside | installation | FR-005, FR-008 |
| Data health course names like h2 | installation | FR-005 |
| `/database` `dl` blocks as compact tables, three in one row, stacked on a phone | installation | FR-005, FR-008 |
| Space before the export block | installation | FR-005 |
| Meet codes tabs spaced, row actions on one line and looking like links | installation | deviation from FR-005.3, §7 O-1 |
| Dean accounts: three lines per dean, notice as wide as the table, table scrolls inside its area on a phone | installation | FR-005, FR-008 |
| Numbers and dates right-aligned, by `text-end` in 9 views: `Courses/Index`, `Courses/Card`, `Teachers/Index`, `MeetCodes/Index`, `MeetReports/Index`, `Report/Index`, `ReportTemplates/Index`, CP `Installations/Index`, CP `Installations/Detail` | views | FR-005.1 (class additions only) |
| Numbers and dates right-aligned, by CSS: journal, report grading, Meet person table, Dean last sign-in | installation | FR-005.1, §7 O-3 |

## 5. Validation evidence

Attempt 2, run after the stand was deleted and every CSS edit was in place:

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| Full suite | `dotnet test ClassroomAgent.sln` | exit 0, 6990/6990, 0 failed, 0 skipped, 10 m 24 s, no environment failure |
| Format | `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0 |
| Shared part of both `site.css` | `diff` of lines 6–115 | identical |
| Every id and class selector of the installation `site.css` look-check rules | search under `src/ClassroomAgent.Web/Views` | all found |
| `site.css` of both hosts | search for `content:`, `data:` values, `color-scheme`, `prefers-color-scheme`, `@import`, external URLs | none |

From attempt 1, files unchanged since: SHA-384 of the downloaded `css/bootstrap.min.css` equals `css_hash` in
`twbs/bootstrap@v5.3.8/config.yml` (`sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB`).

No automated test checks the look itself: the §4.1 rules are proved by the Owner's check, the automated suite proves
only that they broke nothing and left the headers, texts and inline-code rules intact.

Delegated in attempt 2: the build, full suite and format runs (one `cheap-worker` job). Inline: the class-strip
comparison, the selector search, the one-line CSS alignment, this report. Delegated in attempt 1: the class additions
to the views (two `cheap-worker` jobs) and every build, test and format run (three jobs).

## 6. Configuration changes

None (no `appsettings`, `Program.cs` or middleware change). `.gitattributes` is repository configuration, see §4.

## 7. Deviations and discovered problems

Owner's decisions (R-1), not an implementor's choice:

- O-1 **Deviation from spec FR-005.3.** The Meet codes row actions look like links. The state change stays a POST form
  button styled by CSS (`#meet-codes-table`), so API-4 and antiforgery are untouched.
- O-2 A Dean's email may wrap: long addresses are expected. The Dean table is outside `.table-scroll` and keeps wrapping.
- O-3 Text in journal and report cells is right-aligned with the numbers (the rule covers the whole cell).
- O-4 Control Plane table word breaks at 375 px are accepted (`overflow-wrap: anywhere` on its cells).

Discovered:

- m-1 AC-007 asks for no CSP violation on its pages. The console was seen on five pages (§3.1); of the AC's eight pages
  Meet reports, the error page, Dean sign-in, Dean accounts and CP sign-in were not. R-1 names only the first two as not
  checked; the other three simply are not on its list. Not a defect found: all pages share one layout and stylesheets,
  and the automated AC-003/AC-004/AC-005 tests cover the CSP string, embedded images and inline code. For the Owner to
  accept or to look at those pages.
- D-4 R-1 says `text-end` is in 8 views but lists 9. The list is right (§4.1): 9 views carry it.
- D-5 The two shared parts differed by one line (`max-width` on `.data-health-banner` was only in the installation
  file), though the file headers say "word for word" and R-1 says identical. The installation's line was copied to the
  Control Plane file. It has no effect there: that host has no banner.

Carried from attempt 1:

- D-1 The report-template scale table (`ReportTemplates/Form.cshtml`, `#scale-rows`) gets rows from
  `report-template-scale.js`, which creates bare inputs and buttons. FR-009.2 allows a script change only for an
  adapted selector, so the script is untouched; instead the three server-rendered row inputs carry no Bootstrap class
  and `site.css` styles every input and button inside `#scale-rows` alike, with Bootstrap's variables (same look as a
  small form-control and an outline button).
- D-2 The Control Plane carries no override images: it uses no select, checkbox or radio (spec §1.1), so per FR-010.1
  those rules live only in the installation's `site.css`. `EmbeddedImageOverrideTests` confirms the Control Plane
  list is empty and its check passes.
- D-3 The installation `.notice`, `.form-message`, `.form-error`, `.field-error`, `.note`, `.page-search-hint` are
  styled in `site.css` with Bootstrap's palette variables rather than by adding alert classes (FR-005.4 bans no
  class, but `alert` would bring role-like semantics and a dismiss pattern the spec excludes); the look is the quiet
  bordered box / danger text / muted small text the spec names.
- INFO-1 The Control Plane home page's sign-out button is the page's only form submit and is therefore styled as the
  primary button (rule "first submit of a form"). R-1 does not mention it, so it is left OPEN for the Owner rather than
  counted as judged.

Out of scope, for a separate Story (R-1): the word «Роль», duplicate h1 on 4 pages, `/database` block headings, short
language labels, a show-password control, the empty first option of the Meet reports course field.

## 8. Open Decisions

None touched or needed. OD-003 a) applied as written. The Owner's decisions O-1…O-4 are recorded in the ledger (R-1)
and here; no new Open Decision was raised.
