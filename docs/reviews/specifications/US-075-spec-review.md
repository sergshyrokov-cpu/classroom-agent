---
artifact_type: specification_review
story: US-075
version: 2
status: APPROVED
created_at: 2026-11-07T18:04:32Z
updated_at: 2026-11-07T18:20:07Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-075-data-problem-hints.md
    version: null
  - path: trebovaniya.md
    version: 113
  - path: docs/specifications/US-075-spec.md
    version: 2
  - path: docs/specifications/US-075-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-075-open-decisions.md
    version: 2
supersedes: 1
verdict: PASS
---

# US-075 Specification Review (attempt 2)

## Summary

Specification v2 closes the Major finding of review v1: in read-only mode the X5 hint is not shown (FR-005.3),
and every hint raised in that mode is checked against BR-026 (FR-005.4). The derived read-only text is dropped,
so no new wording beyond the Dean's X4 title is introduced. The m-2 interpretation (X5 with archived courses) is
now named as I-8 in the Specification and digest. m-1 (T2 text vs. materials) stays open by design: the agreed
text is the Owner's, and the digest puts it to him as I-7. No new finding.

**Verdict: PASS → HUMAN_SPEC_APPROVAL.**

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-075-data-problem-hints.md` | (unversioned) |
| Requirements | `trebovaniya.md` | 113 |
| Specification | `docs/specifications/US-075-spec.md` | 2 |
| Digest | `docs/specifications/US-075-spec-digest.ru.md` | 2 |
| Open Decisions | `docs/decisions/US-075-open-decisions.md` | 2 |
| Previous review | this file | 1 |

Also read: `trebovaniya.md` section 4 Epic 1 «Состояние данных» lines 1245–1269 (v110 «баннер», «подсказка у
каждой проблемы», «только текущее состояние»), `src/ClassroomAgent.Web/Views/DataHealth/Index.cshtml` (the
read-only notice, section ids, X5 form, the Dean's «Повідомте Адміністратора»). Inputs are current: the
Specification names `trebovaniya.md` v113, the Story and review v1; the digest names Specification v2.

## Fidelity

- FR-001 matches v110 «баннер» (two links, no wrap inside a link, zero part plain text, restrained); SC-15 for
  the stylesheet class is correct.
- FR-002…FR-004 match v110 «подсказка у каждой проблемы»: teaching hints for the Dean (and the Admin), technical
  hints for the Admin, the Dean keeps «сообщите Администратору» without details; existing hints stay.
- FR-005.3 (new): checked against BR-026 and the page. The read-only notice `Landing.ReadOnly.*` is indeed rendered
  at the top of the page (`Index.cshtml` lines 25–31), so the claim that the mode is stated on the same page is
  true. Every X5 sentence names an action or result the mode blocks (connection settings, synchronization, the
  confirmation), so dropping the hint is the reading that satisfies Story AC-007 «no hint promises an action the
  mode blocks» without inventing text.
- FR-005.4 (new): the table covers exactly the hints that can be raised in read-only mode (T1, T2, X3, X4, X5;
  X2a/X2b/X6 are not raised — confirmed in review v1 against `DataHealthRules`). The T1 judgement (the teacher's
  correction is not blocked; «після наступної синхронізації» names no time) is a sound reading, disclosed in I-4.
- FR-006 matches v110 «Архивному курсу подсказка об исправлении не показывается»; FR-006.2 is disclosed (I-8).
- FR-008 texts equal the Story's table; NFR-078 period rule holds. Only one new text: the Dean's X4 title (OD-001 a).

## Story Coverage

Story AC-001…AC-008 are each represented. AC-007 is now fully met: the confirmation sentence's behaviour in
read-only mode is stated (the whole X5 hint is not shown), and no shown hint promises a blocked action
(FR-005.4). FR-007 goes beyond the Story text; disclosed as I-6 and traced to v113 NFR-075.

## Consistency

- Permission matrix: no new cell; SEC-002 keeps the Dean without technical details.
- Read-only mode (BR-026): consistent after FR-005.3/FR-005.4; the X5 form stays as today and submitting is
  refused in that mode (outside scope, I-4).
- Localization NFR-073: every new key in both files (FR-008, VR-003). With `CoursesNotVisible.ReadOnly` dropped,
  the key list and the digest's «Новый текст один» now agree.
- Audit SC-11: unchanged.
- Earlier Specifications (US-062, US-061, US-063): the view structure relied on is as they left it.

## Open Items

`trebovaniya.md` section 7 holds no open item this Story depends on. OD-001 is resolved (a) and applied in FR-004.2
and AC-005.

## Interpretations

| # | Specification choice | In digest §2 |
|---|---|---|
| I-1 | Teaching hint under the kind's heading, before the list | yes |
| I-2 | Connection link reuses the existing text, after the hint | yes |
| I-3 | Archived section: no new hints; existing texts stay | yes |
| I-4 | Read-only mode: no X5 hint; T1/T2/X3/X4 hints stay; form stays | yes |
| I-5 | X4 hint chosen by role, not by details | yes |
| I-6 | v113 page-search hint test fix rides in this Story | yes |
| I-7 | T2 agreed text kept although the kind covers materials | yes |
| I-8 | X5 hint shown once even when its list holds archived courses | yes |

No unnamed interpretation found.

## Digest Fidelity

The digest traces to Specification v2, is plain Russian, presents OD-001 as resolved (true) and names I-1…I-8.
I-4 now states plainly why the X5 hint is absent in read-only mode and that the other hints stay, with the
reason. I-7 puts the T2 wording to the Owner as a choice, not as settled.

## Findings

### Closed from review v1

- **M-1** — closed by Specification v2 FR-005.3/FR-005.4 (recorded by SPECIFICATION).
- **m-2** — closed by I-8 (recorded by SPECIFICATION).

### Still open from review v1

- **m-1 (Minor)** — the T2 hint speaks only of tasks and grades while the kind also covers materials. Not
  closable at this stage: the text is the Owner's agreed wording; the digest asks him (I-7). Stays OPEN for
  `HUMAN_SPEC_APPROVAL`.

### New

None.

## Verdict

PASS → HUMAN_SPEC_APPROVAL. m-1 is advisory and goes to the Owner.

## Delegation

Nothing was delegated; all sources were read directly.
