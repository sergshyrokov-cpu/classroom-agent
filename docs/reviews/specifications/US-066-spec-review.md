---
artifact_type: specification_review
story: US-066
version: 1
status: APPROVED
created_at: 2026-10-28T18:23:03Z
updated_at: 2026-10-28T18:23:03Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/specifications/US-066-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
supersedes: null
---

# US-066 Specification Review

Delegation: none. The reviewer read the cited `trebovaniya.md` text itself (header changelog v104,
§4 Epic 3 lines 1320–1340), BR-086, and checked the code facts of spec §1.1 in
`GetJournalQuery.cs`, `JournalCellRule.cs`, `ReportBuilder.cs`, `ReportPersonNameRule.cs`,
`ExportJournalCommand.cs`, `GroupLessonsBlankMapper.cs`, `ReportWorkbookMapper.cs`.

## Summary

**PASS.** The Specification is faithful to v104 «Ученики с одинаковым именем» and BR-086, covers
all nine Acceptance Criteria, resolves both Story Open Decisions as the Owner decided, and lists
its interpretations openly. One Minor finding: an interpretation stated in the Specification but
not retold in the digest.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | docs/stories/US-066-namesakes-shown-with-email.md | — |
| Requirements | trebovaniya.md | 104 |
| Specification | docs/specifications/US-066-spec.md | 1 |
| Spec digest | docs/specifications/US-066-spec-digest.ru.md | 1 (inputs: spec v1 — current) |
| Open Decisions | docs/decisions/US-066-open-decisions.md | 1 |

## Fidelity

- FR-001 (students only; journal, report, export, blanks) — v104 «в журнале или отчёте … на
  экране и в выгрузке»; teachers excluded by OD-002 a). Faithful.
- FR-002 (namesake = displayed name coincides with another row's) — v104 «совпадает показанное
  имя». Faithful; case-insensitive comparison follows AC-005.
- FR-003/FR-004 (email in the same cell, `<name> (<email>)`) — v104 «в той же клетке, что и имя»,
  OD-001 a). Faithful. Other rows unchanged — v104 «у остальных строк ничего не меняется».
- FR-005 order unchanged — v86 «упорядочены по показанному имени»; Story out of scope. Faithful.
- FR-006 read-only — BR-026 viewing and export keep working. Faithful.
- Security §7 — no new role, endpoint or destination; email already visible to the same roles
  (SC-4, SC-13, SC-14, SC-10/11). Faithful.
- Code facts §1.1 verified: both builders use `StringComparer.Create(uiCulture, ignoreCase:
  false)` (`GetJournalQuery.cs:58`, `ReportBuilder.cs:81`); `JournalCellRule.Label` at line 106;
  export and blank both map the same built `Report`, the blank via `GroupLessonsBlankMapper` →
  `StudentCell` (`ExportJournalCommand.cs` ~128).

No rule without a source; no contradiction.

## Story Coverage

AC-001…AC-009 are all carried (spec §5) and traced (§13). Nothing beyond the Story's scope.

## Consistency

- Permission matrix: no new cell; journal/report/export policies unchanged.
- US-060 "left" mark in the cell note — kept (FR-004.3/4, AC-009).
- US-042 page name switcher and template name source — the comparison runs after the chosen
  variant (FR-002.1), consistent with v86 «экспорт … не расходится с экраном».
- NFR-073: no new translatable label; the email is data.
- SC-11: no new audit event; the existing export event carries no personal data.

## Open Items

`trebovaniya.md` §7 holds no item this Story depends on. OD-001 and OD-002 are resolved by the
Owner (2026-10-28) and applied.

## Interpretations

1. Case ignored for coincidence, ordering unchanged (FR-002.3, FR-005) — spec I-1, digest §2. ✔
2. A journal row named by its full email gets no second email (FR-002.5) — I-2, digest. ✔
3. With names from the email, a coinciding part before `@` gets the full email; general rule even
   where v104 says no coincidences occur (FR-002.2) — I-3, digest. ✔
4. Blanks judge over the whole report, not one copy (FR-002.1) — I-4, digest. ✔
5. The `<name> (<email>)` form is not translated (FR-004.2) — I-5, digest. ✔
6. Names compared as stored, no trimming or whitespace normalisation (FR-002.3) — stated in the
   Specification with its source (mirror, v83), **not** in §11 nor in the digest. → m-1.
7. A leaver and a current student with the same name are namesakes (rows "as built" include the
   leaver) — implied by FR-002.1 and confirmed by AC-009; no separate disclosure needed.

## Digest Fidelity

Every digest statement traces to the Specification; OD-001/OD-002 are presented as the Owner's
resolved choices; nothing is softened. Plain Russian. Omission: interpretation 6 (m-1).

## Findings

- **m-1 (Minor)** — Spec FR-002.3 / digest §2. The Specification compares names "as stored: no
  trimming, no other normalisation". On a web page extra spaces collapse, so two rows whose names
  differ only by a trailing or double space look identical yet would not get the email — the exact
  situation the Story exists for. Google normally stores names without such spaces, so the case is
  rare, and the Specification states the choice openly with a source; but it is a choice the Owner
  does not see in the digest. Advisory: add one line to digest §2 (and spec §11) or confirm at the
  gate.

## Verdict

PASS — no Critical or Major findings; m-1 is advisory and goes to the Owner at
HUMAN_SPEC_APPROVAL.
