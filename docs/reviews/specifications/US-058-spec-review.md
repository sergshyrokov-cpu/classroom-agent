---
artifact_type: specification_review
story: US-058
version: 1
status: APPROVED
created_at: 2026-10-26T19:42:31Z
updated_at: 2026-10-26T19:42:31Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md
    version: null
  - path: trebovaniya.md
    version: 99
  - path: docs/specifications/US-058-spec.md
    version: 1
  - path: docs/specifications/US-058-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-058-open-decisions.md
    version: 1
supersedes: null
---

# US-058 Specification Review

## Summary

**Verdict: PASS** — 0 Critical, 0 Major, 4 Minor.

The Specification is faithful to `trebovaniya.md` v99 (§3 `CourseWork`, §3 `SyncState`,
§4 Epic 1 "Когда повторы не помогли", "Задание или материал, которого нет в Classroom",
"Состояние данных") and to BR-043, BR-045, BR-046, BR-047. Every Story AC is covered; the
three derived ACs (AC-013…AC-015) trace to OD-001 (resolved) and to disclosed interpretations.
Every place where the Specification chooses one reading is listed in §11 and named in digest
section 2.

Delegation: none. All passages were read directly by the reviewer: the Story, the
Specification, the digest, the Open Decisions, `trebovaniya.md` lines 1–20 (v99 changelog),
820–875 (§3), 985–1070 (§4 Epic 1), `business-rules.md` BR-043…BR-047, and
`RunSynchronizationUseCase.cs` `ImportCourseWorkAsync` / `GoogleClassroomReader.ReadCourseWorkAsync`
(to check that no item Google returns is dropped before the "absent" comparison — none is).

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-058-sync-marks-items-gone-and-records-skipped-courses.md` | — (no version field) |
| Specification | `docs/specifications/US-058-spec.md` | 1 |
| Spec digest | `docs/specifications/US-058-spec-digest.ru.md` | 1 (input: spec v1 — current) |
| Open Decisions | `docs/decisions/US-058-open-decisions.md` | 1 |
| Requirements | `trebovaniya.md` | 99 |

## Fidelity

- FR-001…FR-003 match §4 Epic 1 "Задание или материал, которого нет в Classroom" and BR-045
  word for word in substance: marked not deleted, submissions and grades kept, mark removed on
  return, skipped and unchanged archived courses neither gain nor lose marks. FR-001.4 matches
  §3 `CourseWork` "запись синхронизации, а не правка зеркала".
- FR-005 matches §3 `SyncState` and BR-046 (id, name from the course list, reason, since when)
  for the two reasons BR-047 gives a notice kind (out-of-range, unknown state).
- FR-007.1 matches "Курс считается пропущенным, пока прогон не прочитает его успешно".
  FR-007.3 comes from the Story scope (authority 2), FR-007.2 from interpretation I-5 —
  both disclosed.
- FR-008 is required by "данные не обновлялись с <даты последнего успешного чтения курса>"
  (§4 Epic 1, BR-046); OD-001 resolved (a) by the Owner.
- FR-004 (log counts, no names) cites SC-10 correctly; VR-004 (program clock, not Google
  times) is consistent with the retention claim in §7, checked against §1.1.
- Code facts in §1.1 that matter for the absent-item rule were verified:
  `ImportCourseWorkAsync` loads every stored item keyed by `(Resource, GoogleId)`, and the
  reader returns every item of both resources unfiltered, so "not among the items Google
  returned" is well defined.

No rule without a source; no rule contradicting its source.

## Story Coverage

AC-001…AC-012 of the Story are all represented in spec §5 with unchanged meaning; AC-005,
AC-006 and AC-012 are made more precise, not altered. Nothing goes beyond the Story's scope
except the disclosed derived ACs (AC-013 from OD-001; AC-014 from I-5; AC-015 from the
Story's Notes on a failed run).

## Consistency

- Permission matrix: untouched (no endpoint, page or role).
- Read-only mode (BR-026): synchronization does not run in it; no new service write — correct.
- Localization (NFR-073): no user-visible string; reason is a code — correct.
- Audit (SC-11): synchronization writes are not in the list — correct.
- v90 incremental sync, v98 out-of-range skip, US-017 blank-name skip: consistent with §1.1 order.
- PC-11 "synchronization never deletes teaching data": FR-001.3 respects it.

## Open Items

`trebovaniya.md` §7 holds no item this Story depends on. OD-001 is RESOLVED (a) with the
resolution written next to the question. No open Open Decision remains.

## Interpretations

All are in spec §11 and in digest section 2:

| # | Reading chosen | Where the source allows more |
|---|---|---|
| I-1 | `404` mid-run not recorded | BR-046 cites BR-043, which also covers `404` |
| I-2 | Blank-name skip not recorded | BR-046 names BR-043 and unknown state only |
| I-3 | Reason refreshed, first instant kept | BR-046 silent on a changed reason |
| I-4 | Log counts only new marks/unmarks; no line when both zero | Story: "one run-log line" |
| I-5 | Aged-out course loses its record | BR-046 / Story silent |
| I-6 | Retention purge leaves skip records alone | §5 silent |
| I-7 | Unchanged archived course keeps its record | BR-046 silent |
| FR-007.3 | Removal on leaving the list only after the list is enumerated to its end | Story: "when the course is no longer in Google's course list" |
| FR-008.2 | No backfill of last-read instant for courses stored before the migration | OD-001 resolution |

## Digest Fidelity

Every digest statement traces to the Specification; OD-001 is presented as resolved with the
Owner's choice; no interpretation is hidden or presented as settled law. Plain Russian. One
softening noted as m-2.

## Findings

**m-1 (Minor) — I-1 rests on an assumption the digest states as fact.** Spec §11 I-1, digest
§2 bullet 1; checked against BR-043, BR-046. The reading is defensible (BR-047 gives `404` no
notice kind), but "к следующему прогону он уйдёт из списка" holds only if the course really
leaves the list. A course that stays listed while every read of it returns `404` would be
skipped every run with no record and no notice. The Owner should confirm I-1 knowing that edge.

**m-2 (Minor) — "no personal data" in a skip record is too absolute.** Spec §7 "no student or
teacher data", I-6; digest §2 "в них нет личных данных". Checked against §3 `Course` (any
naming scheme is allowed): a course name may contain a teacher's name (e.g. "Алгебра 10-А
(Іваненко)"). The record is short-lived (removed when read, aged out or unlisted), so I-6
still stands, but the statement should say "only the course name, as Google lists it".

**m-3 (Minor) — a record can have no name.** VR-002: an unknown-state course is checked before
the blank-name check, so its record may carry null. BR-046 says a never-loaded course "is shown
by that name"; Story 3 will need a fallback (e.g. the Google id). Not stated in the digest.
Advisory for Story 3.

**m-4 (Minor, advisory for TEST_WRITING) — AC-006 can pass vacuously.** Per §1.1 all Google
reads finish before the course transaction opens, so a failing read writes nothing whatever the
implementation does. A meaningful test needs the course to already hold a marked item and an
absent unmarked one, and should assert both are unchanged; ideally also a failure inside the
transaction (after marks are computed) to prove FR-001.5 rollback.

## Verdict

PASS — no Critical or Major findings. Minor findings m-1…m-4 are non-blocking and go to the
Owner at `HUMAN_SPEC_APPROVAL`. This PASS is not human approval.

```yaml
result:
  verdict: PASS
  stage: SPEC_REVIEW
  story: US-058
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/specifications/US-058-spec-review.md
  next_stage: HUMAN_SPEC_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "m-1 I-1 (404 not recorded) assumes the course leaves the list; a course listed but persistently 404 would be invisible"
    - "m-2 spec §7 / digest say skip records hold no personal data; a course name may contain a teacher's name"
    - "m-3 an unknown-state course may be recorded with no name; Story 3 needs a fallback"
    - "m-4 AC-006 can pass vacuously; test must start from existing marks and ideally fail inside the transaction"
```
