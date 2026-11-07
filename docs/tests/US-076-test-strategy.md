---
artifact_type: test_strategy
story: US-076
version: 2
status: DRAFT
created_at: 2026-11-07T07:33:06Z
updated_at: 2026-11-07T11:26:06Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/designs/database/US-076-db-design.md
    version: 2
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: docs/tests/US-076-test-strategy.md@v1
---

# US-076 Test Strategy — a «?» beside the title opens short help on five pages

**Version 2** (spec v3, Interpretation I-8; finding US-076/IMPLEMENTATION/B-1): existing tests read the five pages' text
outside the help block — section 3a. Everything else as in version 1.

## 1. Scope

The Story renders a native disclosure (`<details>`/`<summary>`) beside the layout title on five installation pages
(journal, Meet codes, Meet report list, one-meeting page, report template list), with texts from the translation files.
No endpoint, schema, policy, form or write changes (API and DB design: NOT_APPLICABLE). The tests therefore read
**rendered pages** of both roles and both languages, as the page-search hint tests (US-046) do, plus the translation
files and the source tree.

Supporting change (no AC): FR-010 tightens `MeetCodeOrganizerNamePageTests.SeveralOrganizers_AreOrdered_AndEachShowsOnce`
(US-074/IMPLEMENTATION/m-1).

Not in scope of these tests: the look on a phone (AC-007, a person's walk recorded in the implementation report);
the page-search hint on the data-health page (finding US-076/SPECIFICATION/m-2 — the Owner deferred it to
`trebovaniya.md` v113 on 2026-11-07; no hint test is added here).

## 2. Test levels

| Level | What | Where |
|---|---|---|
| Integration (page) | rendered five pages, both roles, both languages, read-only causes, empty and validation states; no help on any other page; cookies, writes, scripts, security headers | `Web/Pages/PageHelpTests` (real PostgreSQL via Testcontainers, TC-2; Google ports substituted, TC-4) |
| Integration (resources) | exact values of the 18 keys in `uk` and `en`; no other `PageHelp.` key | `PageHelpTests.TheTranslationFiles_HoldExactlyTheApprovedTexts` (host's localizer) |
| Source scan | no help text literal in any `.cshtml`, `.cs`, `.css`, `.js` under `src/` | `PageHelpTests.NoSourceFile_HoldsAHelpText` |
| Unit (test reader) | the markup reader tells a proper help from an open, overfull, linked or misplaced one | `PageHelpTests.TheReader_TellsAProperHelpFromABrokenOne` |
| Regression | page-search hint still first after the title block; one `<h1>` only in the layout | `PageSearchHintTests` (helper adjusted), `Architecture/OnePageTitleRuleTests` (unchanged) |

No unit test of Application: the Story adds nothing to Application (FR-008, spec §7).

## 3. What the tests treat as the help (markup contract)

Read by element, not by class or id — the spec fixes the element (FR-002) and leaves names to implementation
(api-design §3: constant class and view-data key names are implementation choices):

- exactly one `<details>` on each of the five pages (no installation view has one today), without `open`;
- its first child is the `<summary>` (only wrapper `div`/`span` tags and white space may precede it);
- the summary's visible text is `?`; its accessible name is «Довідка про сторінку» / "Help about this page", as
  `aria-label` **or** as visually-hidden text inside the summary (either is accepted; the visible remainder must be `?`);
- after the summary, the expandable part holds exactly the approved lines as `<p>` paragraphs in order (decoded,
  white space collapsed) and nothing else but bare `div`/`span` wrappers;
- no `a`, `form`, `input`, `button`, `select`, `textarea`, `script`, `style`, `iframe`, `img`, `link` element and
  no `href`, `src`, `style` or `on…` attribute anywhere in the help (FR-008, SC-15);
- the `<details>` follows the first `</h1>` in `<main>` with nothing but wrapper tags between (beside the title,
  FR-002/FR-004); a `<details>` before the title or after page content fails;
- on the four pages with the page-search hint, the hint is the first element after the help (closing wrapper `div`s
  allowed) — FR-004, AC-002, NFR-075.

`PageSearchHintTestData.AfterTitle` now skips a `<details>` directly after `</h1>` and the closing tags of a title
wrapper, so the existing US-046 placement assertions keep their meaning on the five pages (spec AC-006 allows exactly
this edit). `OnePageTitleRuleTests` is unchanged and binds implementation: the layout keeps its single `<h1>`, and a
partial holding the help must not hold an `<h1>`.

## 3a. Existing tests read the page outside the help (spec I-8)

The approved help lines name labels their page shows («Курсу немає в Classroom з …», «не в списку курсу», «Інші
учасники», …). An existing test that reads the **whole page text** either fails (it counts a label or asserts that a
label is absent) or, worse, passes vacuously (it asserts a label is present, and the help alone would satisfy it). Both
are fixed at the source, uniformly, instead of test by test:

- `PageHelpTestData.WithoutHelp(html)` removes every `<details>` element (the help is the only one in the installation,
  FR-002; no view outside `_PageHelp.cshtml` holds one).
- `PageResponse.Text` — the decoded whole-page text most page tests assert on — is now read through `WithoutHelp`.
- `MeetCodesHostExtensions.VisibleText` — the whole-page visible text of the Meet and several journal tests — likewise.

What each test asserts about the page is unchanged (I-8); on pages without help both readers return exactly what they
did. The help itself is read only from `PageResponse.Body` (`PageHelpTests`), so the help tests are not blinded.
`TheReader_TellsAProperHelpFromABrokenOne` proves `WithoutHelp` removes the disclosure, keeps the rest, and is a no-op
on a page without help.

Found by running every `Web.*` test namespace against the US-076 implementation before the fix: 15 failures in
`JournalMarksPageTests` (12, `PageResponse.Text`), `MembershipStartMeetTests` (2) and `MeetReportMeetingPageTests` (1,
both `VisibleText`). The other whole-page reader patterns were checked by search (raw `Body` assertions on labels,
other decoded-body helpers): none reads a label of the five pages from the whole page.

## 4. Scenarios

### Positive
- P-1 each of the five pages shows its help for Admin and Dean, in `uk` and in `en` (4 hosts × 5 pages).
- P-2 read-only mode of every cause (Suspended, GracePeriodExpired, NeverConfirmed) shows the same help (Dean).
- P-3 empty states render the help: journal with no course chosen, Meet codes with no codes, Meet reports with no
  rows, template list of a fresh school (built-in template only).
- P-4 the journal with a validation message (400 malformed course, 404 unknown course) still shows the help.
- P-5 the 18 keys hold exactly the approved texts in both files (api-design §3.1, journal line 2 with «Позначка» /
  "The mark").

### Negative
- N-1 no help on any other installation page: landing, course list and card, teacher list, search results, template
  report, template new/edit/delete, `MeetCodes/CourseChoice`, database, data health, Dean accounts, workspace
  connection, connection instruction, access check, erasure; Dean's landing, own password, course list, data health;
  error pages (404 course card, `/error/404`, `/error/500`, anonymous 404); sign-in; forced password change.
  Control: the journal on the same host has the help (so the test fails in the red phase, not vacuously).
- N-2 no help on any Control Plane page (setup, home, installations, sign-in).
- N-3 no help text literal in any source file (scan proven non-vacuous on the resx files).
- N-4 no other `PageHelp.` key in either resx.

### Boundary
- B-1 every load collapsed: two visits in a row and a visit after a language change.
- B-2 four-line pages (journal, Meet codes) and three-line pages (others): exact line count by sequence equality.

### Validation
- The Story adds no input (spec §6). VR-001 (view-set page id, unknown id → plain title) is internal; its observable
  effect — help on exactly five pages — is N-1/N-2. VR-002 (HTML encoding): the texts contain `«»`, `"`, `'`, `—`, `→`, `…`;
  the reader decodes entities and compares exact text, so raw or double-encoded output fails P-1.

### Security
- S-1 no cookie beyond session, antiforgery and the existing TempData cookie on any of the five pages, ever (FR-003).
- S-2 no teaching row and no audit event written by viewing the five pages three rounds (FR-008).
- S-3 every `<script>` on the five pages is one the views already load (`/js/picker-narrow.js`, `/js/meet-export.js`);
  the help adds none (FR-003, SC-15).
- S-4 the four browser security headers on the five pages equal the SC-15 values exactly (`SecurityHeaders.Problems`).
- S-5 no link, form, script, style or handler inside the help (FR-008, SC-13: nothing sent).
- Role coverage: the five pages are already protected; allowed roles (Admin, Dean) are P-1; their forbidden/anonymous
  tests exist from earlier Stories and are unchanged (no policy change, spec §7).

### Persistence
- None: nothing is stored (FR-003); S-2 proves no write.

## 5. Fixtures

Existing only: `PostgreSqlFixture`, `JournalHostExtensions.StartAsync` (roles, read-only causes),
`MeetReportsHostExtensions.SeedWorldAsync` (course, meetings, codes), `UiLanguageHostExtensions.ChooseLanguageAsync`,
`ControlPlaneTestHost`, `SecurityHeaders`, `InstallationTestHost.Text/AllTexts/TeachingFingerprintAsync/AuditRowsAsync`.
New: `TestInfrastructure/PageHelpTestData` (page ids, keys, the 34 lines + 2 names verbatim, markup reader).

## 6. Excluded scenarios

- Mouse, touch, Enter and Space opening the disclosure: browser behaviour of a native `<details>`; the tests prove the
  element is native and holds nothing that needs script. The real press is part of the AC-007 walk.
- "Opening sends no request": opening a native `<details>` is client-side only; with no script, link or form in the help
  (S-3, S-5) no request can follow. Not observable in an HTTP test.
- Phone layout (44×44 touch target, no sideways scroll, restrained look, FR-007): AC-007, a person's walk.
- The data-health page's search hint (SPECIFICATION/m-2): deferred by the Owner to v113.

## 7. Known limitations

- A future test that wants the help's text from `PageResponse.Text` or `VisibleText` will not find it there; it reads
  `Body` through `PageHelpTestData` (documented on both readers).

- The markup reader assumes no nested `<details>` (none exists in the installation).
- The literal scan uses the first 40 characters of each line; a source file quoting a line from its middle would not be
  caught.

## 8. Open Decisions affecting testing

None.
