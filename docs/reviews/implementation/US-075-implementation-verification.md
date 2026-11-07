---
artifact_type: implementation_verification
story: US-075
version: 2
status: APPROVED
created_at: 2026-11-07T19:41:51Z
updated_at: 2026-11-07T22:25:16Z
produced_by: implementation-verifier
attempt: 2
supersedes: docs/reviews/implementation/US-075-implementation-verification.md@v1
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
  - path: docs/designs/api/US-075-api-design.md
    version: 1
  - path: docs/designs/database/US-075-db-design.md
    version: 1
  - path: docs/tests/US-075-test-strategy.md
    version: 1
  - path: docs/tests/US-075-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-075-test-generation-report.md
    version: 1
  - path: docs/evidence/US-075-implementation-report.md
    version: 2
verdict: PASS
---

# US-075 Implementation Verification (attempt 2)

## 1. Summary

**PASS.** Attempt 2 answers the Owner's rejection at `HUMAN_PR_APPROVAL` (history 2026-11-07T22:03:20Z) with two
presentation changes and nothing else:

- **R-1** — the comma of the banner no longer starts the second line. `_Layout.cshtml` puts the first part and the
  separator's punctuation into `<span class="data-health-banner-lead">` with `white-space: nowrap`; the comma stays
  outside the link (FR-001.2).
- **R-2** — at phone width (`max-width: 575.98px`) the banner drops the 11rem right reserve kept for the language
  switcher, so both parts fit inside the fill.

Everything verified in v1 (AC-001 … AC-008, FR-001 … FR-009, deviations D-1 … D-3) is unchanged by attempt 2 and
still holds; sections 3–6 of v1 are carried over and re-checked against the current diff. No Critical or Major
finding. Two Minor findings and one INFO, none blocking. The Owner's manual re-check of AC-008 is still open
(US-075/IMPLEMENTATION/INFO-1).

## 2. Reviewed Artifacts

As in the front matter `inputs`. API and DB design are NOT_APPLICABLE, so there is no `openapi` and no entity model.
Also reviewed: the working-tree diff against `3d3c63e` (`git status`, `git diff` of `_Layout.cshtml` and `site.css`)
and the untracked files, found with `git status` and ripgrep.

## 3. AC Verification

Unchanged from v1 §3: every AC's tests prove it. Attempt 2 changed no test file. Its effect on the AC tests:

| AC | Effect of attempt 2 | Verdict |
|---|---|---|
| AC-001 | The links now sit inside a `<span>` within the banner `<p>`. The tests read every `<a>` in the `<p>`, the hrefs, the no-wrap class, "no comma inside a link" and the whole text «A, B» — all still green (33/33). | proves |
| AC-002 | Zero part is still plain text; for a zero first part the lead span holds plain text plus the comma. | proves |
| AC-003 … AC-007 | Not touched (`Index.cshtml`, resx, keys unchanged in attempt 2). | proves |
| AC-008 | Automated part unchanged and green. The manual part (phone and computer, both roles) has to be repeated by the Owner: R-1 and R-2 came from it. | proves (automated); manual pending |

## 4. Specification Coverage

- **FR-001.1** keys and wording unchanged: the separator is still taken from `DataHealth.Banner`; only its trailing
  whitespace is split off (`separator.TrimEnd()`), so the rendered text is identical.
- **FR-001.2** the comma is outside both links (inside the lead span, after the first link's `</a>`).
- **FR-001.4** a part still never breaks inside itself; the lead span only adds that the comma travels with the
  first part. The stylesheet class mechanism (no inline style, no script) is kept (SC-15).
- **FR-009.3 / NFR-076** the phone rule removes the cause of R-2. It overrides line 75 of `site.css` by being later
  with equal specificity inside the media query; wider screens keep the US-068 reserve.
- No behaviour beyond the Specification and the Owner's rejection comment was added.

## 5. Design Conformance

Unchanged from v1 §5. Attempt 2 touches only the layout markup and the stylesheet: no endpoint, DTO, schema,
query or policy. AD-3/AD-4/AD-6/AD-8 hold.

## 6. Deviations

| Id | Departs from | Reason | Verdict |
|---|---|---|---|
| D-1 | TEST_WRITING helper `DataHealthHostExtensions.Banner` | as v1: reads text as a browser shows it; no assertion changed | sound — accepted (v1) |
| D-2 | — (implementation choice) | as v1: splits `DataHealth.Banner` at `{0}`/`{1}`, pinned by `TheAdminsX4Title_AndTheBannerKeys_AreUnchanged`. Attempt 2 adds a split of the separator itself at its trailing whitespace, which is safe for any separator (an empty remainder renders nothing). | sound — accepted |
| D-3 | — | CRLF kept | sound — no effect |
| D-4 | TC (no browser automation) | the browser's line break itself cannot be observed by markup tests, so R-1/R-2 rest on the Owner's manual re-check. Partly overstated — see m-2. | accepted, with m-2 |

## 7. Report Accuracy

- The report's change set matches the tracked diff and the three untracked Story test files, each traced.
- **Not listed:** `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs` (untracked, created 2026-11-07 for the
  Owner's manual walk; its header says "delete after the check, never commit"). It is compiled into the test project
  and adds one `[Fact]` that returns at once unless `LOOKCHECK=1`. This explains the report's unexplained
  7838 vs 7837 (§5.1). See m-1.
- AC-to-test names exist; no `TODO`, `TBD`, `FIXME`, `NotImplementedException` or skip in the changed files
  (ripgrep exit 1).

## 8. Story Test Run

Delegated to `cheap-worker` (touched no files, reported verbatim):

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0, 0 warnings, 0 errors |
| `--no-build --filter FullyQualifiedName~DataHealthHintTranslationTests` | 6/6 |
| `~DataHealthHintsTests` | 38/38 |
| `~DataHealthBannerTests` | 33/33 |
| `~DataHealthPageTests` | 26/26 |
| `~PageSearchHintTests` | 69/69 |

All exited 0, no skipped tests, no Docker/Npgsql errors. The full suite is not rerun here; SECURITY_REVIEW runs it.
`quick-look` was not used; symbol lookups were done inline.

## 9. Findings

- **m-1 (Minor, report accuracy / DoD 8–9).** `tests/ClassroomAgent.Tests/LookCheck/LookCheckStand.cs` is in the
  working tree but not in the implementation report §4, and it is not a Story file. It must not reach the final
  commit: delete it after the Owner's AC-008 re-check (or leave it out of the staged files). Checked against
  `AGENTS.md` Definition of Done items 8–9.
- **m-2 (Minor, test strength).** The R-1 and R-2 fixes have no regression guard, although both are observable
  without a browser: the markup can assert the comma inside `.data-health-banner-lead` and after the first link,
  and the stylesheet can assert the `.data-health-banner-lead { white-space: nowrap; }` rule and the phone-width
  banner rule, the way `TheStylesheet_KeepsEachBannerLinkOnOneLine` pins FR-001.4. Only the actual line break needs
  the manual check (D-4). Checked against TC and report §7 D-4. Builds on US-075/IMPLEMENTATION/INFO-2.
- **INFO-1.** The R-2 rule assumes that at phone width the switcher sits on its own row above the header search
  field. Today every page that shows both the banner and the switcher also shows the search field: they differ only
  by the temporary-password claim, and `TemporaryPasswordMiddleware` sends such a session to the forced change form,
  where the banner is not shown. A future page with banner and switcher but no search field would let the banner
  run under the switcher on a phone.

Open findings considered:

- **US-075/IMPLEMENTATION/INFO-1** — stays OPEN: the manual re-check is the Owner's, before `HUMAN_PR_APPROVAL`.
- **US-075/IMPLEMENTATION/INFO-2** — stays OPEN; m-2 narrows it to what really needs a browser.
- **US-075/SECURITY_REVIEW/INFO-1, INFO-2** — security-reviewer's; attempt 2 does not change the `{0}`/`{1}` slicing
  or the X5 hint, so both stand as they were.

## 10. Verdict

**PASS** → SECURITY_REVIEW.
