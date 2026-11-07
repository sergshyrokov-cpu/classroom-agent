---
artifact_type: ac_test_matrix
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
  - path: docs/tests/US-075-test-strategy.md
    version: 1
supersedes: null
---

# US-075 Acceptance Criteria → Test Matrix

Classes: `HintsT` = `Web/Pages/DataHealthHintsTests`, `BannerT` = `Web/Pages/DataHealthBannerTests`,
`TransT` = `Web/Localization/DataHealthHintTranslationTests`, `SearchT` = `Web/Pages/PageSearchHintTests`,
`PageT` = `Web/Pages/DataHealthPageTests` (existing, regression). Level: I = integration (HTTP + PostgreSQL),
S = source/file. Status is the red-phase status recorded in the test generation report.

| AC | Scenario | Level | Test Class | Test Method | Expected Result | Status |
|---|---|---|---|---|---|---|
| AC-001 | N, M > 0 on `/`, `/workspace/courses`, «Стан даних»: two links to `#data-health-technical` / `#data-health-teaching`, no-wrap class, comma outside, text unchanged | I | BannerT | `EachPartOfTheBanner_IsALinkToItsSection` (×3 pages) | two links as in strategy §3.1 | red |
| AC-001 | the no-wrap rule lives in the host's stylesheet | S | TransT | `TheStylesheet_KeepsEachBannerLinkOnOneLine` | `.data-health-banner-part { white-space: nowrap }` | red |
| AC-001 | the banner keeps its restrained form with two links (no button, script, popup) | I | BannerT | `TheBanner_IsRestrained` (adapted: whole `<p>`) | none of the forbidden elements | green (regression) |
| AC-001 | banner wording and keys unchanged | S, I | TransT / BannerT | `TheAdminsX4Title_AndTheBannerKeys_AreUnchanged`; `TheBannerText_UsesTheLanguagesPluralForms` (existing) | same text as before | green (regression) |
| AC-002 | one part zero (uk 0/3, 2/0; en 0/1, 1/0): that part plain text, the other the single link | I | BannerT | `AZeroPart_IsPlainText_AndTheOtherPartIsTheLink` | one link, to the non-zero part's section | red |
| AC-003 | Dean/Admin × uk/en: T1 hint ×2 and T2 hint ×2 for three courses, exact text, leaf paragraph | I | HintsT | `EachTeachingHint_AppearsOncePerCourseThatHasIt` | counts 2 and 2 | red |
| AC-003 | each teaching hint under its heading, before the list (I-1) | I | HintsT | `TheTeachingHints_SitUnderTheHeading_BeforeTheList` | heading < hint < first item | red |
| AC-004 | Admin × X2a, X2b, X3, X4, X5 × uk/en: one hint with exact text after the title; connection link after the hint for X2a / X2b only | I | HintsT | `TheAdmin_SeesEachTechnicalKindsHint_AfterItsTitle` (×10) | as in strategy §3.2 | red |
| AC-004 | X5 hint before the list and form; X3 hint after the course name | I | HintsT | `TheX5Hint_PrecedesTheForm_AndTheX3Hint_FollowsTheCourseName` | placement holds | red |
| AC-004 | existing details stay (X1 diagnosis and link, X2a last success, X5 list and form, X6) | I | PageT | existing technical tests | as before | green (regression) |
| AC-005 | Dean × X2a, X2b, X3, X4, X5 × uk/en: no hint paragraph, no FR-003 text in either language, no connection link, «Повідомте Адміністратора» | I | HintsT | `TheDean_SeesNoTechnicalHint_OnlyTheHintToTellTheAdmin` (×10) | as stated | green expected before implementation (absence; see report) |
| AC-005 | Dean's X4 title ends at its date, no role instruction (OD-001 a), uk/en | I | HintsT | `TheDeansX4Title_EndsAtItsDate` (×2) | short title, no «перевірте роль» | red |
| AC-005 | Admin's X4 title stays whole, uk/en | I | HintsT | `TheAdminsX4Title_StaysWhole` (×2) | full title | green (regression) |
| AC-006 | archived course with T1, T2, X3 (Dean, Admin): no hint paragraph or new text in the archived section; marks and existing texts as before | I | HintsT | `TheArchivedSection_ShowsNoNewHint` (×2) | as stated | green expected before implementation (absence; see report) |
| AC-007 | read-only (Suspended), Admin: T1, T2, X3, X4 hints shown | I | HintsT | `InReadOnlyMode_TheHintsThatPromiseNothingBlocked_AreShown` (×4) | one hint each | red |
| AC-007 | read-only, Admin: X5 title, list, form stay; no hint | I | HintsT | `InReadOnlyMode_TheX5Problem_HasNoHint` | no hint | green expected before implementation (absence; see report) |
| AC-007 | read-only, English: both banner parts are links | I | BannerT | `InReadOnlyMode_TheBannerParts_AreLinks` | two links | red |
| AC-007 | no script on the page, hints are leaf paragraphs (scripts-off behaviour identical) | I | HintsT | `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent`; `AssertLeaf` in the hint tests | no `<script`, no `style=` | red (needs the hints) |
| AC-008 | no Google port call, no audit event while showing every hint | I | HintsT | `ThePageWithEveryHint_UsesNoScript_CallsNoGoogleApi_AndWritesNoAuditEvent` | counts unchanged | red (needs the hints) |
| AC-008 | eight new keys in both resx files with the agreed texts; no view literal | S | TransT | `EachNewText_IsAnEntry_WithTheAgreedValue` (×2); `NoView_HoldsAHintTextAsALiteral` | entries exist; no literal | red / green |
| AC-008 | the hint class styled in the host's stylesheet | S | TransT | `TheStylesheet_StylesTheHintClass` | rule exists | red |
| AC-008 | page-search hint covers «Стан даних» for both roles, uk/en, read-only, no rows, phone, writes nothing (FR-007) | I | SearchT | the teaching-page theories with `data-health` | ordinary hint once, under the title | green (page already renders it) |
| AC-008 | counts, kinds and rights as before | I | PageT, BannerT, `PermissionMatrixTests`, US-062/US-061/US-063 suites | existing | as before | green (regression) |
| AC-008 | a person checks the page and the banner on a computer and a phone (NFR-076) | Manual | — | IMPLEMENTATION manual check, recorded in the implementation report §3.1 | no horizontal scroll; links do not break inside | manual |

Every Acceptance Criterion AC-001 … AC-008 has at least one automated test; AC-008's phone check is manual by the
Story's own wording.
