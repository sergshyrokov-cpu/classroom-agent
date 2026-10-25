---
artifact_type: specification_review
story: US-055
version: 1
status: APPROVED
created_at: 2026-10-25T10:40:00Z
updated_at: 2026-10-25T10:40:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-055-grade-columns-fit-any-value.md
    version: null
  - path: trebovaniya.md
    version: 97
  - path: docs/specifications/US-055-spec.md
    version: 1
  - path: docs/specifications/US-055-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-055-open-decisions.md
    version: 1
supersedes: null
---

# US-055 Specification Review

## Summary

**Verdict: PASS.** 0 Critical, 0 Major, 3 Minor. The Specification is a faithful,
narrow storage fix: it removes the `numeric(10,4)` limit from the three columns
§3 defines as «в баллах как есть», keeps the run-stops rule (OD-001 a) and changes
no requirement, endpoint or text. The code facts it relies on were checked
against `src/` and hold. The digest names every interpretation.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-055-grade-columns-fit-any-value.md` | — (no version field) |
| Requirements | `trebovaniya.md` | 97 |
| Specification | `docs/specifications/US-055-spec.md` | 1 |
| Spec digest | `docs/specifications/US-055-spec-digest.ru.md` | 1 (inputs spec v1 — current) |
| Open Decisions | `docs/decisions/US-055-open-decisions.md` | 1 |

Inputs are current; nothing is stale.

## Fidelity

- §3 (lines 788–797): CourseWork «максимум баллов», Submission `assignedGrade` /
  `draftGrade` «в баллах как есть» — checked; FR-001/FR-002 follow it.
- Mirror rule (lines 102, 134–135): «данные из Google — зеркало, которое
  пользователь не правит» — FR-002 is consistent.
- US-015 DB design lines 66, 165–166 declare `numeric(10,4)` — the Specification's
  description of the current state is correct.
- Code facts verified: `CourseWorkConfiguration.cs:47`, `SubmissionConfiguration.cs:47–48`
  (`numeric(10,4)`); `GoogleClassroomReader.cs:352, 384–385` (`(decimal)double`);
  `ReportWorkbookMapper.cs:20/208` (`0.############################`);
  `ReportBuilder.cs:256` (percent clamp). The Razor references are off by one line
  (m-3).
- No rule lacks a source; FR-003 cites PC-2, security rows cite SC-/TC- ids.

## Story Coverage

AC-001 … AC-006 are all carried with unchanged values. AC-005 is tightened
(nulls and four-decimal values included) — within the Story's "keeps every stored
value unchanged". AC-006a is derived from the in-scope line "the reports and
their exports" and is disclosed as derived (I-4). Out of scope matches the Story;
I-5 (no restoration of already rounded values) is a consequence, not new scope.

## Consistency

- US-017 FR-005 / US-053 OD-001 (run stops on unexpected error): unchanged (FR-004, §10).
- BR-052 / US-015 VR-007 (absent max points = ungraded): nullability kept (FR-002).
- Permission matrix, read-only mode (BR-026), NFR-073, SC-11: not touched; S-01/S-04 state it.
- No other column stores these values: every `decimal` grade/points property in
  `Domain`/`Application` traces to the three columns (grep of `src/`).

## Open Items

No `trebovaniya.md` §7 item bears on this Story. OD-001 is resolved (a) by the
Owner and carried with its resolution. No TODO/TBD markers.

## Interpretations

| Id | Choice | In digest §2 |
|---|---|---|
| I-1 | "As Google returned it" = the value the port delivers after the existing `double`→`decimal` cast (≈15 significant digits) | yes |
| I-2 | Concrete column type left to DB_DESIGN | yes |
| I-3 | Formatters are not changed unless a test fails | yes |
| I-4 | AC-006 tested with the «без перевода» template; AC-006a added | yes |
| I-5 | Values already rounded by the old column are not restored | yes |
| I-6 | "Meet step runs" proven through the substituted Meet port | no — a test-mechanics detail, not a product choice; acceptable |
| §9 | DB_DESIGN to state table rewrite / lock time | yes |

No unlisted interpretation found beyond those in the Findings.

## Digest Fidelity

Every statement traces to the Specification. The 15-digit limit of reading (I-1)
is stated, not hidden; "ровно такими, как их дал Google" in §1 is qualified by §2.
OD-001 is presented as the Owner's decision, which it is. Plain Russian.

## Findings

### m-1 (Minor) — I-3's overflow argument covers only the AC values

Spec I-3, FR-005; `ReportBuilder.cs:256`. With the column limit gone,
`points / max * 100m` in a scale-template report can exceed `decimal`'s range
for an extreme pair (huge points over a tiny positive maximum) and throw
`OverflowException`; before, points ≤ 10⁶ and max ≥ 10⁻⁴ made this impossible.
Not reachable with any realistic value and outside the ACs, but FR-005's "none of
them fails" is stated without that boundary. Advisory: TEST_WRITING may add a
scale-template case with the AC-001/AC-002 values (percent ≈ 20, no overflow),
and the residual extreme case falls under the Story's "cannot represent" class.

### m-2 (Minor) — AC-006a does not name the export layout

Spec AC-006a; `ExportJournalCommand.cs` / `GroupLessonsBlankMapper.cs:122–126`.
The report export has two layouts — the table and the shipped blank form
(US-043) — both rendering grades through `ReportWorkbookMapper.GradingCell`.
AC-006a says "the export" only. Advisory: TEST_WRITING states which layout the
test uses (the table is enough, since both share `GradingCell`).

### m-3 (Minor) — Razor line references off by one

Spec §1: `Journal/Index.cshtml:144` and `Report/Index.cshtml:268` are at 145 and
269 today. Cosmetic.

## Verdict

PASS — no Critical or Major findings; Minor m-1 … m-3 are non-blocking.

Delegation: none; all lookups were done directly.
