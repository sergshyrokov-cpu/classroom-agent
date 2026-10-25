---
artifact_type: specification_review
story: US-057
version: 1
status: APPROVED
created_at: 2026-10-25T18:12:00Z
updated_at: 2026-10-25T18:12:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/specifications/US-057-spec.md
    version: 1
  - path: docs/specifications/US-057-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-057-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-057 Specification Review

Delegation: none. The reviewer read `trebovaniya.md` (v98 changelog; §4 Epic 1 "Когда повторы не
помогли", "Инкрементальная синхронизация"; §4 Epic 3 "Перевод в шкалу школы") and the code the
Specification cites directly, at `bb67ffb`.

## 1. Summary

The Specification is faithful to `trebovaniya.md` v98 and covers all eight Story Acceptance
Criteria plus two derived ones. Every code fact it relies on was checked and holds. No Critical or
Major finding; three Minor findings, two of them about the digest's wording.

**Verdict: PASS.**

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-057-out-of-range-grades-do-not-stop-sync.md` | — |
| Requirements | `trebovaniya.md` | 98 |
| Specification | `docs/specifications/US-057-spec.md` | 1 |
| Digest | `docs/specifications/US-057-spec-digest.ru.md` | 1 (inputs spec v1 — current) |
| Open Decisions | `docs/decisions/US-057-open-decisions.md` | 1 |

## 3. Fidelity

- **Skip, not stop** (FR-001, FR-002): §4 Epic 1 v98 — "Так же пропускается курс, в котором Google
  отдал баллы, оценку или максимум больше, чем программа может хранить (больше примерно
  7,9·10²⁸)". Matches; the skip is placed next to the `CourseGone` catch, before the transaction
  (`RunSynchronizationUseCase.cs:495–517` verified), so "уже загруженные данные этого курса
  остаются как были" (FR-002.1, AC-003) follows from the existing structure.
- **Log without values** (FR-003): "в лог прогона пишется номер курса без самих значений".
  Matches; SC-10 cited correctly. Event id 5135 is unused (5121–5134 in
  `SynchronizationBackgroundService.cs`).
- **Next run reads again** (FR-004): "следующий прогон читает курс снова". Met by writing nothing,
  including the complete-read `updateTime` (§4 "Инкрементальная синхронизация" v90). See m-1 for
  the one qualification FR-004 states.
- **Percent bounded** (FR-005): §4 Epic 3 v98 — "Процент ограничен пределами 0–100 %: баллы не
  меньше максимума дают 100 %, отрицательные — 0 %; отчёт строится при любых сохранённых
  баллах". Matches. Checked: for 0 < points < max, `points / max * 100` is below 100 and cannot
  overflow; for every non-overflowing pair rules 1–3 equal today's `Round` + `Clamp`
  (`ReportBuilder.cs:256–270`). `ReportBuilder.Grade` is the only `* 100m` percent in `src`, and
  assigned and draft points both go through it (`:201`, `:207`).
- **Reader facts**: the three `(decimal)double` casts (`GoogleClassroomReader.cs:352`, `:384–385`)
  are the only ones in `Infrastructure/Google`; `Snapshot` runs outside `GuardAsync`; the port
  methods are consumed only by `RunSynchronizationUseCase`, so no other caller meets the new kind.
- **VR-001 boundary**: `(double)decimal.MaxValue` rounds to 2⁹⁶, which is itself beyond
  `decimal.MaxValue`; VR-001's "or the conversion otherwise reports overflow" covers that exact
  value. Correct as written.

No invented rule, no contradiction.

## 4. Story Coverage

AC-001 … AC-008 map one-to-one to Specification AC-001 … AC-008 with the same Given/When/Then;
AC-006 adds the draft-grade case, AC-004 makes "the value" testable as concrete formattings. Scope
and out-of-scope match the Story's lists. Nothing beyond the Story's scope.

## 5. Consistency

- US-017 (`CourseGone`): same placement, same "not counted" rule (I-5), same outcome on both
  completed and failed paths (FR-002.2, AC-009). Consistent.
- US-018 (incremental sync): no new stored marker; consistent with `IsUnchangedArchive`.
- US-055: in-range conversion unchanged (FR-001, AC-010).
- US-056 (membership start from course creation): a course skipped on its first appearance has no
  first-load record until a later run loads it, which then is its first load — consistent with
  v98's "когда синхронизация впервые загрузила этот курс".
- Permission matrix, read-only mode (BR-026), SC-4, SC-11, NFR-073: untouched; the Specification
  says so correctly (no endpoint, no audit event, no user-visible string).
- AD-4: the overflow is converted to the port exception in Infrastructure.

## 6. Open Items

`trebovaniya.md` §7 holds no item this Story depends on. The Story has no Open Decisions; the
`open_decisions` artifact correctly records none.

## 7. Interpretations

All found are listed in Specification §11 and digest §2:

| # | Interpretation | In digest |
|---|---|---|
| I-1 | "больше, чем может хранить" in both directions | yes |
| I-2 | one value skips the whole course | yes (and it is the literal §4 reading) |
| I-3 | not counted as processed; own event 5135 | yes |
| I-4 | "reads again" by writing nothing; no new marker | yes (see m-1) |
| I-5 | "отрицательные" applied as "≤ 0" | yes |
| — | derived AC-009, AC-010 | yes |

No unlisted interpretation found.

## 8. Digest Fidelity

Every digest statement traces to the Specification; language is plain Russian. Two softenings,
both Minor (m-1, m-3).

## 9. Findings

| Id | Severity | Location | Finding |
|---|---|---|---|
| m-1 | Minor | Digest §1 bullet 2 vs spec FR-004 | The digest says unconditionally "Курс читается снова в следующем прогоне". FR-004 states one exception: an archived course whose earlier complete read already recorded the same `updateTime` is not re-read. FR-004 shows it cannot arise (an archived course cannot change, and an out-of-range value never completed a read), so the behaviour is right; the digest just does not carry the qualification. |
| m-2 | Minor | Spec FR-001 | FR-001 names the new kind `ValueOutOfRange` but not the `Diagnosis` the exception carries (`GoogleReadFailedException(kind, diagnosis)`; `CourseGone` uses `null`). It matters only if the exception ever escaped the FR-002 catch (`DiagnosisOf` would then give `Unexpected`); a technical choice for IMPLEMENTATION, with AC-009 guarding the run outcome. |
| m-3 | Minor | Digest §1 bullet 1 | "прогон завершается успешно" omits FR-002.4's "if nothing else fails"; the AC-009 line in digest §2 implies it but does not say it in the bullet. |

## 10. Verdict

**PASS** — no Critical or Major finding. m-1 … m-3 are advisory, for the Owner at
`HUMAN_SPEC_APPROVAL`.
