---
artifact_type: specification_review
story: US-068
version: 2
status: APPROVED
created_at: 2026-11-01T13:08:57Z
updated_at: 2026-11-01T13:08:57Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-068-restrained-look-with-bootstrap.md
    version: null
  - path: trebovaniya.md
    version: 105
  - path: docs/specifications/US-068-spec.md
    version: 2
  - path: docs/specifications/US-068-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-068-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-068-spec-review.md@v1
verdict: PASS
---

# US-068 Specification Review (v2)

## Summary

Re-review after the loop-back from v1. Both v1 findings are fixed: M-1 (licence and version note now
`LICENSE.txt` / `VERSION.txt`, served `text/plain`, so the unchanged every-static-file tests keep
passing; I-8 and the digest now say truthfully that the files are openable and harmless) and m-1
(FR-006.1–006.3 name exactly the coloured elements). The Specification is faithful to
`trebovaniya.md` §8 «Оформление» (v105), NFR-076 and SC-15, covers all seven Story ACs and adds a
testable AC-008. One new Minor finding (m-2) on the wording of FR-005.3. Verdict: **PASS**.

Delegation: none. The reviewer read the Story, the Specification v2, the digest v2, the review v1,
`trebovaniya.md` lines 1192–1199 (banner: «Допустимо выделение цветом»), 2190–2226 (§8 «Оформление»,
static files, no-store), `tests/.../TestInfrastructure/NoStore.cs` (`StaticFilesOf`), both hosts'
`UnknownFileLikePathTests.AnExistingStaticFile_IsStillServedAnonymously` and
`SecurityHeadersResponseTests.EveryStaticFile_CarriesTheSecurityHeaders`, and the views carrying
`warning`, `mark-above-maximum`, `data-health-banner` and the Control Plane status confirmation.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | docs/stories/US-068-restrained-look-with-bootstrap.md | — |
| Specification | docs/specifications/US-068-spec.md | 2 |
| Digest | docs/specifications/US-068-spec-digest.ru.md | 2 (inputs: spec v2) |
| Open Decisions | docs/decisions/US-068-open-decisions.md | 1 |
| Requirements | trebovaniya.md | 105 |

Inputs are current: the spec names Story and `trebovaniya.md` v105; the digest names spec v2.

## Fidelity

Every §8 «Оформление» clause maps to a requirement: both hosts, Bootstrap 5.3 (FR-001, FR-010);
files shipped with the app, licence beside them, nothing from other sites (FR-001, FR-002.1, §7
SC-13); headers not loosened, `data:` images replaced by same-directory files (FR-007, FR-009.3,
AC-003, AC-004); styles only, no scripts (FR-001.4, FR-005.5); one light theme (FR-003, AC-008);
header unchanged, no school name or logo (FR-004.1); marks highlighted only by colour (FR-006);
readable on a phone (FR-008); no text added or removed (FR-009.1, AC-006); tests + manual check of
the listed key pages (AC-001…AC-008; AC-007 page list matches §8 and the Story). Static files stay
anonymous and cacheable (SC-4, SC-14; FR-001.5). No unsourced business or security rule found.

**M-1 re-check.** `NoStore.StaticFilesOf` enumerates every file under `wwwroot`; the three callers
require `200`. `.txt` is in the framework's default map (`text/plain`), as is `.svg`
(`image/svg+xml`), so `LICENSE.txt`, `VERSION.txt` and the override SVGs pass those tests unchanged.
`UnknownFileLikePathTests` asserts 404 only for a non-existent `/zz-marker-41.txt` — unaffected.
Renaming the licence file keeps §8 "рядом — файл её лицензии (MIT)" satisfied (content unchanged).
Fixed.

**m-1 re-check.** FR-006.1 names banner, `mark-above-maximum` and the CP `warning` paragraphs
(verified: `Journal/Index`, `Report/Index`, `_Layout`, `AllowedAdmins/Revocation`,
`Installations/Detail`); FR-006.2 keeps the US-059…063 labels plain. Consistent with
`trebovaniya.md` line 1198 ("Допустимо выделение цветом" — permitted, not required) and §8. Fixed.

## Story Coverage

AC-001…AC-007 represented with sharper proofs; AC-008 makes the "light theme only" scope item
testable. Out-of-scope list matches the Story and adds `.xlsx` styling (consistent: exports are not
pages). Nothing beyond the Story's scope.

## Consistency

No change to the permission matrix, read-only mode, audit (SC-11) or localization (NFR-073: no text
change). CSP and no-store behaviour of earlier Stories (US-040, US-050) kept, proved by their
unchanged tests. Test-helper adaptation (FR-009.4) is bounded to markup location, texts unchanged.

## Open Items

`trebovaniya.md` §7 has no item this Story depends on. OD-001 and OD-002 resolved a) by the Owner,
carried into the Open Decisions artifact. No new Open Decision needed.

## Interpretations

All named in spec §11 and digest §2: I-1 proof of "every page" structurally + sample HTTP; I-2 wider
main column, narrow forms; I-3 danger outline on irreversible confirmations; I-4 no Bootstrap
validation styling; I-5 no source map, CSS unmodified; I-6 markup-locating helpers may be adapted;
I-7 automatic dark theme removed; I-8 `LICENSE.txt` / `VERSION.txt`, browser-openable; I-9 only
three element kinds coloured. No unnamed interpretation found.

## Digest Fidelity

Every digest statement traces to spec v2; all nine interpretations appear in plain Russian; both ODs
shown as resolved by the Owner; the I-8 explanation is now true (files openable, harmless content,
why not extensionless). No softened or hidden decision.

## Findings

### m-2 (Minor) — FR-005.3 calls reversible actions "cannot be undone"

- **Location:** spec FR-005.3, I-3; digest §2 «Кнопки необратимых действий красные».
- **Checked against:** Control Plane `InstallationStatus/Confirmation.cshtml` serves both suspension
  and resumption; an allowed Admin can be added again after revocation.
- **What is wrong:** "installation status change" includes resuming an installation, which is
  neither irreversible nor dangerous; the implementor may colour the resume button red, or read the
  list as wrong. Advisory: name the actions as "suspension of an installation" (not any status
  change) and describe the group as destructive/consequential rather than strictly irreversible.
  Presentation only; does not block approval.

## Verdict

**PASS** — no Critical or Major finding. m-2 is advisory and retold to the Owner at
`HUMAN_SPEC_APPROVAL`.
