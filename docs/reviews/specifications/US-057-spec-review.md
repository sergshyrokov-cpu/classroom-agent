---
artifact_type: specification_review
story: US-057
version: 2
status: APPROVED
created_at: 2026-10-25T18:47:05Z
updated_at: 2026-10-25T18:47:05Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/specifications/US-057-spec.md
    version: 2
  - path: docs/specifications/US-057-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-057-open-decisions.md
    version: 2
  - path: docs/designs/database/US-057-db-design.md
    version: 1
supersedes: docs/reviews/specifications/US-057-spec-review.md@v1
verdict: PASS
---

# US-057 Specification Review (v2)

Delegation: none. The reviewer read `trebovaniya.md` (v98 changelog; §4 Epic 1 "Когда повторы не
помогли", "Инкрементальная синхронизация"; §4 Epic 3 "Перевод в шкалу школы"), the DB design v1
(BLOCKED B-1/B-2) and the code the Specification cites directly, at `40452fb`.

Review v1 (PASS, against spec v1) missed that the existing table constraints refuse negative
values, which made spec v1 AC-007 and AC-010 unsatisfiable (DB design v1 B-1, B-2). This review
checked the constraints explicitly.

## 1. Summary

Spec v2 is faithful to `trebovaniya.md` v98 and, with the Owner's OD-001 (keep the schema), every
Acceptance Criterion can now be set up and observed under the existing schema. The v2 changes are
confined to what the loop-back required; the rest is v1 unchanged and was re-read. No Critical or
Major finding; four Minor findings (two carried from v1).

**Verdict: PASS.**

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md` | — |
| Requirements | `trebovaniya.md` | 98 |
| Specification | `docs/specifications/US-057-spec.md` | 2 (DRAFT) |
| Digest | `docs/specifications/US-057-spec-digest.ru.md` | 2 (DRAFT) |
| Open Decisions | `docs/decisions/US-057-open-decisions.md` | 2 (DRAFT) |
| DB design (context) | `docs/designs/database/US-057-db-design.md` | 1 (BLOCKED) |

Inputs are current: the spec names the Story and v98; the digest names spec v2.

## 3. Fidelity

- §4 Epic 1 v98 ("пропускается … уже загруженные данные этого курса остаются как были, в лог
  прогона пишется номер курса без самих значений, следующий прогон читает курс снова") →
  FR-001…FR-004. Matches.
- §4 Epic 3 v98 ("Процент ограничен пределами 0–100 %: баллы не меньше максимума дают 100 %,
  отрицательные — 0 %; отчёт строится при любых сохранённых баллах") → FR-005. Matches.
- §4 "Инкрементальная синхронизация" v90 (archived course not re-read when `updateTime` equals the
  one saved at the last complete read) → FR-004 relies on not writing that `updateTime`. Matches.
- SC-10 (no value, task, student or submission in logs) → FR-003, §7. AD-4 → FR-001, §7.

Code facts checked:

| Spec claim | Evidence | Holds |
|---|---|---|
| `(decimal)double` casts | `GoogleClassroomReader.cs:352`, `:384–385` | yes |
| Snapshots built outside `GuardAsync` | `:198/201`, `:223/230` | yes |
| `CourseGone` caught before the transaction; other exceptions → `catch (Exception)` | `RunSynchronizationUseCase.cs:511–515`, `:132` | yes |
| `CoursesGone` carried on both outcome paths | `:121`, `:151` | yes |
| `ck_course_work_max_points_non_negative`, `ck_submission_grades_non_negative` | migrations snapshot (`AddCourseListSupport.Designer.cs:664`, `:1220`) | yes |
| No sign check in Application | no negative/sanitising check in `RunSynchronizationUseCase` | yes |
| `ReportBuilder.Grade` divides, rounds, then clamps | `ReportBuilder.cs:256–270` | yes |
| Report reads through `IJournalFieldSource` port; test fakes exist | `ReportBuilder.cs:24`, `GetReportQuery.cs:23`, `ExportJournalCommand.cs:27`; `tests/.../FakeJournalSource.cs` | yes |

**Constraint consistency (the v1 gap).** AC-001…AC-005, AC-009 store only non-negative values
or nothing (skipped courses write nothing; AC-002's −1·10³⁰ is skipped before the transaction).
AC-006 stores 7·10²⁸ / 0.0001 (unconstrained `numeric` since US-055, non-negative). AC-007a stores
0, AC-007b stores nothing. AC-010 stores 7.9·10²⁸ only. AC-008 ordinary values. None violates a
constraint.

VR-001 correctly keeps "or the conversion otherwise reports overflow": `(double)decimal.MaxValue`
rounds to 2⁹⁶, one above `decimal.MaxValue`, so a bare `>` comparison alone would let that one
value through to a throwing cast.

## 4. Story Coverage

Story AC-001…AC-006 and AC-008 are carried unchanged in meaning. Story AC-007 is restated
(OD-001): its Given — a **stored** −7·10²⁸ — cannot exist under the kept constraint; spec AC-007
a) stored 0 and b) a negative pair through a substituted journal port, both through the report
and export use cases. The v98 rule is fully exercised; only the storage framing changes. Derived
AC-009 and AC-010 stay within scope. Nothing beyond the Story's scope is added.

## 5. Consistency

- US-015 VR-001 and US-055 (`NegativeValues_AreStillRefusedByTheExistingChecks`): kept, OD-001.
- US-018 incremental sync: FR-004 consistent.
- US-017 I-5 (processed counter never claims unheld data): FR-002.3 consistent.
- US-053 (failed run recorded as failed): AC-009 consistent — a later failure still fails the run.
- §4 v83 mirror rule ("в зеркале всегда исходные баллы"): the pre-existing non-negative constraints
  already deviate from it for negatives; the spec does not widen the deviation and discloses its
  consequence (L-1). Not introduced by this Story.
- Permission matrix, read-only mode (BR-026), SC-11, NFR-073: untouched (no endpoint, no write
  path, no user-visible string).

## 6. Open Items

`trebovaniya.md` §7 holds no open item this Story depends on. OD-001 is resolved by the Owner with
the resolution recorded verbatim. L-1 (an in-range negative value from Google stops the run) is
correctly left as a possible new §7 item for the Owner, not resolved by the spec.

## 7. Interpretations

All named in the digest §2:

| Id | Interpretation | In digest |
|---|---|---|
| I-1 | "больше, чем программа может хранить" in both directions | yes |
| I-2 | one value skips the whole course | yes |
| I-3 | own event 5135; skipped course not counted as processed | yes |
| I-4 | re-read achieved by writing nothing, no new marker | yes |
| I-5 | "отрицательные — 0 %" applied as "≤ 0 → 0 %" | yes |
| I-6 | "при любых сохранённых баллах" met for every storable value; negative proved without a stored row | yes (AC-007 bullet) |
| — | AC-007 / AC-010 restated (OD-001) | yes |
| — | FR-003 log reason wording "in words" (no fixed text) | not named; harmless (not user-visible) |

## 8. Digest Fidelity

The digest states the v2 change up front, names OD-001 as the Owner's decision, explains AC-007's
two parts and AC-010's missing lower bound in plain Russian, and states L-1 honestly as a
limitation. v1 finding m-3 ("если ничего другого не сломалось") is fixed. v1 finding m-1 remains
(see m-1).

## 9. Findings

| Id | Severity | Location | Finding |
|---|---|---|---|
| m-1 | Minor (carried from v1) | Digest §1 bullet 2 vs FR-004 | The digest says the course "читается снова в следующем прогоне" without FR-004's unreachable exception (an archived course whose same `updateTime` was already recorded by an earlier complete read). Practically unreachable, as FR-004 explains. |
| m-2 | Minor (carried from v1) | FR-001 | The `Diagnosis` carried by the `ValueOutOfRange` exception is not named (null, like `CourseGone`, is the natural choice) — decide at IMPLEMENTATION. |
| m-3 | Minor | Story AC-007 vs spec AC-007 | The Story — which outranks a resolved Open Decision in the order of authority — still says "a stored grade of −7·10²⁸". The spec restates it under OD-001 and discloses this; the Owner may want to amend the Story file's AC-007 to match, so the higher-authority artifact no longer describes an impossible Given. |
| m-4 | Minor | L-1 | The in-range-negative limitation is, by §4 v80/v98's own logic, the same "one value stops the whole school" pattern v98 removes. It is correctly out of scope here; the Owner should decide whether to add it as a `trebovaniya.md` §7 item before the pilot. |

## 10. Verdict

**PASS** — no Critical or Major finding. m-1…m-4 are advisory and go to the Owner at
`HUMAN_SPEC_APPROVAL`.
