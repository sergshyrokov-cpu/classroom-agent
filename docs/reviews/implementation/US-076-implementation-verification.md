---
artifact_type: implementation_verification
story: US-076
version: 1
status: APPROVED
created_at: 2026-11-07T12:00:54Z
updated_at: 2026-11-07T12:00:54Z
produced_by: implementation-verifier
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: docs/specifications/US-076-spec.md
    version: 3
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
  - path: docs/designs/api/US-076-api-design.md
    version: 2
  - path: docs/designs/database/US-076-db-design.md
    version: 2
  - path: docs/tests/US-076-test-strategy.md
    version: 2
  - path: docs/tests/US-076-ac-test-matrix.md
    version: 2
  - path: docs/evidence/US-076-test-generation-report.md
    version: 2
  - path: docs/evidence/US-076-implementation-report.md
    version: 2
verdict: PASS
---

# US-076 Implementation Verification — page help on five pages

## 1. Summary

**PASS.** The code does what Specification v3 and API design v2 say: the five views name their page id, the
layout renders one title block with a native `<details>` only for those ids, the 18 translation keys hold the
approved texts in both languages, the styling is in `site.css` only, and no script, endpoint, header, policy or
write was added. The Story's tests prove AC-001…AC-006; AC-007 is the Owner's manual look walk, not done yet
(implementation report §3.1) — it must be done before `HUMAN_PR_APPROVAL`. Story tests: 161/161 green.
Two Minor findings, no Critical or Major.

## 2. Reviewed Artifacts

As in the front matter. `openapi` and `entity_model` were not produced (API_DESIGN and DB_DESIGN recorded
NOT_APPLICABLE). Working tree: `git diff` against `2e14506` plus the untracked files of `git status`, found
with ripgrep.

## 3. AC Verification

| AC | Tests (ac_test_matrix v2) | Verdict |
|---|---|---|
| AC-001 | `PageHelpTests.EachOfTheFivePages_…` (Admin/Dean × uk/en), `InReadOnlyMode_…` (3 causes), `WithNoRowsAtAll_…`, `TheJournalWithAValidationMessage_…` | proved — `AssertHelp` reads the real response: exactly one `<details>`, not `open`, summary first, «?», accessible name in the user's language |
| AC-002 | `AssertHelp` (nothing but wrapper markup between `</h1>` and `<details>`; hint first after the block), `PageSearchHintTests`, `OnePageTitleRuleTests` | proved — open/close by mouse, touch, Enter, Space is the native behaviour of `<details>`/`<summary>`, which the markup check fixes |
| AC-003 | `EveryLoad_IsCollapsed_NothingIsStoredOrWritten_AndTheHeadersAreUnchanged` | proved — two loads and one after a language change; cookies limited to the existing three; scripts limited to the existing two |
| AC-004 | `AssertHelp` (paragraphs equal the approved lines), `TheTranslationFiles_HoldExactlyTheApprovedTexts`, `NoSourceFile_HoldsAHelpText` | proved — the source scan is non-vacuous (it first requires every fragment in the resx files) |
| AC-005 | `NoOtherInstallationPage_HasTheHelp`, `TheForcedPasswordChange_HasNoHelp`, `NoControlPlanePage_HasTheHelp` | proved — the shared reader is shown non-vacuous by `TheReader_TellsAProperHelpFromABrokenOne` and by the journal control inside the first test |
| AC-006 | `AssertHelp` (no link/form/script/style/handler; nothing but the lines), `EveryLoad_…` (SC-15 headers, teaching fingerprint and audit count unchanged), existing tests of the five pages | proved — existing assertions unchanged; their text readers skip the help per I-8 (see m-2) |
| AC-007 | manual walk by the Owner | **pending** — to be recorded in implementation report §3.1 before `HUMAN_PR_APPROVAL` |
| FR-010 | `MeetCodeOrganizerNamePageTests.SeveralOrganizers_AreOrdered_AndEachShowsOnce` | done — raw-cell assertions added, existing ones kept |

## 4. Specification Coverage

- FR-001, VR-001: `PageHelpTextKeys.PageOf` accepts only the five ids (unknown → plain title); the id is set in
  each view's code, never from request input.
- FR-002, FR-003, FR-009: `_PageHelp.cshtml` renders `<details class="page-help">` without `open`, `<summary
  aria-label=…>?</summary>`; no script, cookie or storage.
- FR-004: `_Layout.cshtml` wraps `<h1>` and the partial in `<div class="page-title">` only when `helpPage` is set;
  every other page renders the title exactly as before; still one `<h1>`.
- FR-005, FR-006, VR-002: each line is a `<p>@T[key]</p>` (Razor-encoded); 36 resx entries added (18 × 2), no
  existing entry changed.
- FR-007: `site.css` section «US-076»; 2.75rem (44 px) summary square, `:focus-visible` outline, secondary
  colour, no animation, no inline style.
- FR-008: no endpoint, form, link, log or audit line added.
- No behaviour beyond the Specification found.

## 5. Design Conformance

- API design v2 §3: key names `PageHelp.<Page>.Line<n>` and `PageHelp.Control.Name` exactly; constants class
  `Web/Security/PageHelpTextKeys.cs` beside `PageSearchHintTextKeys`; §3.1 journal line 2 begins «Позначка» /
  "The mark" as the Owner decided (SPEC_REVIEW/m-4).
- No route, status code, entity or migration change — consistent with NOT_APPLICABLE designs.
- AD-3, AD-4, AD-6, AD-8: untouched — presentation-only change, no business rule, no DbContext, no domain type in
  a view model.

## 6. Deviations

- Implementation report §7: the layout opens `<div class="page-title">` only on the five pages — an
  implementation choice inside FR-004, not a departure. **Accepted.**
- No undisclosed deviation found. B-1 (attempt 1) was resolved upstream (Spec v3 I-8, TEST_WRITING v2).

## 7. Report Accuracy

The change set of report §4 equals the `src/` diff (11 files: 2 new, 9 modified). The six test files the report
attributes to TEST_WRITING match the `tests/` diff and untracked files. AC-to-test locations exist. The count
20/20 for `PageHelpTests` matches the methods and cases. No `TODO`/`TBD`/`FIXME`, skip or
`NotImplementedException` in the change.

## 8. Story Test Run

Delegated to `cheap-worker` (no files touched), 2026-11-07 ~11:58–12:00Z: `dotnet build ClassroomAgent.sln`
exit 0, 0 warnings, 0 errors; then one filter per call, `--no-build`:

| Filter | Exit | Total | Passed | Failed | Skipped |
|---|---|---|---|---|---|
| PageHelpTests | 0 | 20 | 20 | 0 | 0 |
| PageSearchHintTests | 0 | 63 | 63 | 0 | 0 |
| OnePageTitleRuleTests | 0 | 11 | 11 | 0 | 0 |
| MeetCodeOrganizerNamePageTests | 0 | 48 | 48 | 0 | 0 |
| JournalMarksPageTests | 0 | 19 | 19 | 0 | 0 |

The full suite was not rerun here (security-reviewer runs it; implementation report §5: 7779/7779).

## 9. Findings

- **m-1 (Minor, documentation).** `docs/specifications/US-076-spec.md` v3 §4.1, journal line 2 still reads
  «"Вибув(-ла)" з датою…» / "\"Left\" with a date…", while the shipped value (resx, `PageHelpTestData`) begins
  «Позначка «вибув(-ла)»…» / "The mark \"left\"…" as the Owner decided at HUMAN_SPEC_APPROVAL (SPEC_REVIEW/m-4,
  recorded in API design v2 §3.1). The code is right; the Specification text was not brought in line when v3 was
  written. Advisory: fold the wording into the Specification if it is revised again. Loop-back key if acted on:
  `invalid_specification` (not required).
- **m-2 (Minor, tests).** `TestInfrastructure/PageResponse.Text` and `MeetCodesHostExtensions.VisibleText` now
  strip **every** `<details>` element of **every** page in the suite (`PageHelpTestData.WithoutHelp`), not only the
  page help. Today the help is the only `<details>` in `src/` (ripgrep), so nothing is hidden; but a future Story
  that adds a `<details>` would see its content silently vanish from whole-page text assertions, making
  `DoesNotContain` checks on it vacuous. Advisory: narrow the reader to the help (`class="page-help"`) in a later
  Story. Loop-back key if acted on: `test_gap` (not required).

## 10. Verdict

**PASS** → SECURITY_REVIEW. Before HUMAN_PR_APPROVAL the Owner's AC-007 walk (computer and phone, both
languages, five pages) must be recorded in implementation report §3.1. OPEN finding US-076/SPECIFICATION/m-2 is
outside this Story and left OPEN as instructed.
