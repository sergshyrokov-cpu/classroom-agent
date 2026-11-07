---
artifact_type: test_strategy
story: US-075
version: 1
status: DRAFT
created_at: 2026-11-07T18:52:37Z
updated_at: 2026-11-07T18:52:37Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/designs/database/US-075-db-design.md
    version: 1
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
supersedes: null
---

# US-075 Test Strategy — a short hint under each kind of data problem, and banner links to each section

## 1. Scope

Presentation only (Specification v2 Overview; API and DB design NOT_APPLICABLE). Under test:

- the banner in the installation layout: two links to the sections of «Стан даних», separator outside them, a
  zero part plain text, a no-wrap class from the host's stylesheet (FR-001);
- the new hints under each kind on `GET /workspace/data-health`, by role, section and read-only flag
  (FR-002 … FR-006, FR-009);
- the Dean's X4 title without the Admin's instruction (FR-004.2, OD-001 a);
- the eight new translation entries in both files (FR-008, VR-003);
- the page-search hint on «Стан даних» joining the hint test list (FR-007, v113 NFR-075);
- nothing else changed: no Google call, no audit event, no script or inline style (FR-009.2, SEC-004, SEC-005).

Not under test here: counts, kinds and rights themselves — the US-062 / US-061 / US-063 suites already pin them and
must stay green (AC-008 "as before" is their regression run).

## 2. Test levels

| Level | Used for |
|---|---|
| Integration (HTTP + PostgreSQL, `WebApplicationFactory`, Testcontainers; TC-2) | banner links, hints, titles, read-only mode, no Google call, no audit event, page-search hint |
| Source / file tests (no host) | translation entries in both resx files; stylesheet rules in `wwwroot/css/site.css` |
| Unit | none — no new rule outside the view |
| Manual | the page and the banner on a computer and a phone (AC-008, NFR-076) |

No contract tests: no endpoint, status code or body changes (API design §1).

## 3. Markup the tests fix

The Specification leaves class names to design; API design §2 says "classes in the host's own stylesheet". The tests
fix the hooks below. They are presentation hooks, not behaviour, and the implementation must use them.

### 3.1 Banner (FR-001)

```html
<p id="data-health-banner" class="data-health-banner">
  <a href="/workspace/data-health#data-health-technical" class="data-health-banner-part">1 технічна проблема</a>, <a href="/workspace/data-health#data-health-teaching" class="data-health-banner-part">3 навчальні</a>
</p>
```

- The banner's text (tags removed, white space collapsed) is unchanged: «N …, M …».
- A non-zero part is an `<a>` with the section href and class `data-health-banner-part`.
- A zero part is not inside any `<a>`; it may sit in any other element.
- The separator ", " is outside every `<a>` (no link text contains a comma).
- `site.css` has a rule selecting `.data-health-banner-part` with `white-space: nowrap` (FR-001.4).

### 3.2 Hints (FR-002, FR-003, FR-009.1)

- A new hint is a `<p>` whose class list contains `data-health-hint` and whose text is exactly the hint (FR-008).
  The hint paragraph holds no `<a>`, `<script>`, `<button>`, `<details>` or `style=` attribute.
- `site.css` has a rule selecting `.data-health-hint` (restrained secondary text; its look is the manual check).
- The X2a / X2b connection page link (`href="/settings/workspace-connection"`) follows the hint inside the same
  problem, before the next problem or section.
- T1 / T2: the hint follows the kind's heading line and precedes the kind's first list item.
- X5: the hint precedes the confirmation form (`id="course-deletion-confirmation"`).
- X3: the hint follows the course name.

## 4. Scenarios

### 4.1 Positive

| Id | Scenario |
|---|---|
| P-1 | N > 0, M > 0: two links, each to its section, comma outside, no-wrap class; on three layout pages (AC-001) |
| P-2 | Dean and Admin, uk and en: course A with T1 (two grades) and T2, course B with T1, course C with T2 → each teaching hint once per course with it (2 + 2), exact text (AC-003) |
| P-3 | T1 / T2 hint placement under the heading, before the list (AC-003, FR-002.1/2, I-1) |
| P-4 | Admin, uk and en, one kind at a time — X2a, X2b, X3, X4, X5: the kind's title, its hint once, exact text; link after the hint for X2a / X2b only (AC-004) |
| P-5 | Admin X4: the hint shown and the Admin's title whole, with «— перевірте роль…» (AC-005 last clause, FR-003.2, I-5) |
| P-6 | Admin X5: the hint precedes the form; X3: after the course name (FR-003) |
| P-7 | Read-only (Suspended), Admin: T1, T2, X3, X4 hints shown as outside the mode (AC-007, FR-005.4) |

### 4.2 Negative

| Id | Scenario |
|---|---|
| N-1 | Dean, each technical kind X2a, X2b, X3, X4, X5: no FR-003 hint text in either language, no connection link, «Повідомте Адміністратора» shown (AC-005, SEC-002) |
| N-2 | Dean X4: the title is exactly «Google не повертає жодного курсу з <дата>» — no «перевірте роль» / «check the technical account's role», in both languages (AC-005, OD-001 a) |
| N-3 | Archived course with T1, T2 and X3: no new hint in the archived section; the archived X3 line and the Dean's «Повідомте Адміністратора» stay (AC-006, FR-006.1, I-3) |
| N-4 | Read-only, Admin, X5: no X5 hint; the X5 title, list and form stay (AC-007, FR-005.3, I-4) |
| N-5 | Hints and banner carry no script, inline style or interactive element; the page has no `<script>` (FR-009.2, SEC-004) |
| N-6 | Showing the page with every kind calls no substituted Google port and writes no audit event (AC-008, SEC-005) |

### 4.3 Boundary

| Id | Scenario |
|---|---|
| B-1 | N = 0, M > 0 and N > 0, M = 0, uk and en: the zero part is plain text with its plural wording, the other part a link (AC-002) |
| B-2 | Existing plural theory (0/1, 3/0, 2/0, 0/3 …) keeps the banner text unchanged under the new markup |

### 4.4 Validation

No new input (VR-001). VR-002 is covered by P-2 … N-4 (hint chosen by kind, role, section, read-only flag). VR-003:
each of the eight keys exists in both resx files with the exact FR-008 value, non-empty.

### 4.5 Security

- SEC-001: authorization unchanged — the existing allowed/forbidden tests of the page and banner
  (`DataHealthPageTests`, `DataHealthBannerTests`, `PermissionMatrixTests`) stay green; no new endpoint, so no new
  TC-5 pair is needed.
- SEC-002: N-1, N-2.
- SEC-003: hints are fixed literals; course names around them already HTML-encoded (existing tests).
- SEC-004: N-5 plus the stylesheet rules (no inline style).
- SEC-005: N-6.

### 4.6 Persistence

None: no schema, no write. N-6 asserts the page writes no audit row.

## 5. Fixtures

- `PostgreSqlFixture`, `InstallationTestHost` via `JournalHostExtensions.StartAsync` (Admin / Dean, read-only cause).
- Language chosen with `ChooseLanguageAsync` after sign-in (the `DataMarksHostExtensions` pattern), for both roles.
- Seeding with the existing `DataHealthHostExtensions` helpers: `RecentSuccessAsync`, `SyncStateAsync(emptySince)`,
  `SkipAsync(UnknownStateReason)`, `CourseAsync`, `GradedAsync`, `ItemNotInClassroomAsync`, `MarkCourseAsync`.
- New `DataHealthHintTexts`: the FR-008 literals in both languages (the US-062 precedent: tests pin literals).
- Helpers added to `DataHealthHostExtensions`: `SectionHtml`, `HintParagraphs`, `BannerLinks`; `Banner` now returns
  the whole banner element's text, no longer requiring one link to the page.

## 6. Existing tests changed

The banner's markup changes by FR-001, so two existing helpers or assertions are adapted, not weakened:

- `DataHealthHostExtensions.Banner` asserted the banner is one link to `/workspace/data-health`; it now returns the
  text of the whole banner element (text assertions unchanged); the links are asserted by the new tests.
- `DataHealthBannerTests.TheBanner_IsRestrained` ended the banner at the first `</a>`; it now ends at `</p>`, so the
  forbidden-element check covers both links.
- `PageSearchHintTests`: «Стан даних» joins the teaching-page list (FR-007) and the "no rows at all" theory.

## 7. Excluded scenarios

- X1, X6, T3, T4 get no new hint; their existing texts are pinned by the US-062 / US-063 suites (Out of Scope).
- Scripts off: the page uses no script (N-5), so a script-off browser receives the same markup; no browser test.
- Phone width / no horizontal scroll: manual (AC-008, NFR-076); the no-wrap class is asserted in the stylesheet.
- The X5 form in read-only mode stays as today (I-4, Out of Scope); only its presence is checked in N-4.

## 8. Known limitations

- Visual restraint (secondary colour) is judged by the person in the manual check; the tests prove only that the
  class exists and that no banner, popup or expandable element is used.
- A link "does not break inside itself" is proved through the CSS rule, not by rendering.

## 9. Open Decisions affecting testing

None. OD-001 resolved a) (2026-11-04). Finding `US-075/SPEC_REVIEW/m-1` closed ACCEPTED: the T2 text is pinned as agreed.
